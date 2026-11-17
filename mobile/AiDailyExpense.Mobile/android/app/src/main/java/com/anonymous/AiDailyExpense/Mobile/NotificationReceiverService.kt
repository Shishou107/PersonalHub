package com.anonymous.AiDailyExpense.Mobile

import android.content.ComponentName
import android.content.Context
import android.content.SharedPreferences
import android.content.pm.PackageManager
import android.database.sqlite.SQLiteDatabase
import android.os.Handler
import android.os.Looper
import android.service.notification.NotificationListenerService
import android.service.notification.StatusBarNotification
import android.util.Log
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException
import java.security.MessageDigest
import java.text.Normalizer
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit

class NotificationReceiverService : NotificationListenerService() {

    companion object {
        private const val TAG = "NotificationReceiver"
        private const val DEFAULT_API_URL = "http://testapi.somee.com/api"
        private const val PREFS_NAME = "bank_notification_sync"
        private const val PENDING_PREF_KEY = "pending_bank_notifications"
        private const val PROCESSED_PREF_KEY = "processed_bank_notification_keys"
        private const val HOURLY_SCAN_INTERVAL_MS = 60L * 60L * 1000L
        private const val ACTIVE_SCAN_MAX_AGE_MS = 15L * 60L * 1000L // Chỉ bù thông báo trong vòng 15 phút gần nhất
        private const val PENDING_MAX_AGE_MS = 2L * 60L * 60L * 1000L // Tối đa 2 giờ (thay vì 72 giờ)
        private const val MAX_PROCESSED_KEYS = 500
        private const val MAX_PENDING_SENDS_PER_DRAIN = 12

        @Volatile
        var isConnected: Boolean = false
            private set

        /**
         * Triggers an automatic re-bind of the NotificationListenerService.
         * Toggling the component enabled state forces Android's NotificationManagerService
         * to reconnect to the listener without requiring manual user toggle in Settings.
         */
        fun rebindService(context: Context) {
            try {
                val componentName = ComponentName(context, NotificationReceiverService::class.java)
                val pm = context.packageManager
                pm.setComponentEnabledSetting(
                    componentName,
                    PackageManager.COMPONENT_ENABLED_STATE_DISABLED,
                    PackageManager.DONT_KILL_APP
                )
                pm.setComponentEnabledSetting(
                    componentName,
                    PackageManager.COMPONENT_ENABLED_STATE_ENABLED,
                    PackageManager.DONT_KILL_APP
                )
                Log.d(TAG, "NotificationReceiverService re-bind triggered via component toggle")
            } catch (e: Exception) {
                Log.w(TAG, "Cannot toggle NotificationReceiverService: ${e.message}")
            }
        }
    }

    private data class PendingNotification(
        val key: String,
        val appName: String,
        val messageText: String,
        val receivedAtMillis: Long,
        val createdAtMillis: Long
    )

    private val scanHandler = Handler(Looper.getMainLooper())
    private val inFlightKeys = mutableSetOf<String>()
    private var hourlyScanStarted = false

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(20, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()

    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    private val hourlyScanRunnable = object : Runnable {
        override fun run() {
            // Chỉ gửi bù hàng đợi rớt mạng nếu có, không quét vét thanh thông báo mỗi giờ
            drainPendingQueue("hourly")
            scanHandler.postDelayed(this, HOURLY_SCAN_INTERVAL_MS)
        }
    }

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "Notification listener service created")
        drainPendingQueue("service_create")
    }

    override fun onDestroy() {
        isConnected = false
        stopHourlyScan()
        super.onDestroy()
    }

    override fun onListenerConnected() {
        super.onListenerConnected()
        isConnected = true
        Log.d(TAG, "Notification listener connected")
        startHourlyScan()
        scanActiveNotifications("connected")
        drainPendingQueue("connected")
    }

    override fun onListenerDisconnected() {
        super.onListenerDisconnected()
        isConnected = false
        Log.w(TAG, "Notification listener disconnected, requesting rebind")
        try {
            requestRebind(ComponentName(this, NotificationReceiverService::class.java))
        } catch (e: Exception) {
            Log.w(TAG, "Cannot request notification listener rebind: ${e.message}")
        }
    }

    override fun onNotificationPosted(sbn: StatusBarNotification?) {
        super.onNotificationPosted(sbn)
        if (sbn == null) return
        processNotificationCandidate(sbn, "posted")
    }

    private fun startHourlyScan() {
        if (hourlyScanStarted) return
        hourlyScanStarted = true
        scanHandler.removeCallbacks(hourlyScanRunnable)
        scanHandler.postDelayed(hourlyScanRunnable, HOURLY_SCAN_INTERVAL_MS)
    }

    private fun stopHourlyScan() {
        hourlyScanStarted = false
        scanHandler.removeCallbacks(hourlyScanRunnable)
    }

    private fun scanActiveNotifications(source: String) {
        val notifications = try {
            activeNotifications ?: emptyArray()
        } catch (e: Exception) {
            Log.w(TAG, "Cannot read active notifications from $source: ${e.message}")
            return
        }

        notifications.forEach { processNotificationCandidate(it, source) }
    }

    private fun processNotificationCandidate(sbn: StatusBarNotification, source: String) {
        val packageName = sbn.packageName ?: return
        if (packageName == applicationContext.packageName) return

        val extras = sbn.notification.extras ?: return
        val title = extras.getCharSequence("android.title")?.toString().orEmpty()
        val bodyText = extractFullBodyText(extras)
        val combinedText = listOf(title, bodyText)
            .filter { it.isNotBlank() }
            .joinToString(" - ")
            .trim()

        if (combinedText.isBlank()) return

        if (packageName.contains("zalo", ignoreCase = true) || packageName.contains("momo", ignoreCase = true)) {
            Log.d(TAG, "Wallet notification candidate from $source: pkg=$packageName, text=$combinedText")
        }

        if (!shouldProcessNotification(packageName, combinedText)) return

        val now = System.currentTimeMillis()
        val receivedAtMillis = if (sbn.postTime in 1..now) sbn.postTime else now

        // Nếu là do quét lại thanh thông báo (lúc mở app/hồi sinh service),
        // chỉ nhận thông báo trong vòng 15 phút gần nhất (đề phòng bị lỡ lúc OS tạm ngắt service).
        // Bỏ qua toàn bộ thông báo cũ hơn 15 phút để tuyệt đối không gọi API oan!
        if (source != "posted" && (now - receivedAtMillis > ACTIVE_SCAN_MAX_AGE_MS)) {
            Log.d(TAG, "Skipping old active notification from $source: age=${(now - receivedAtMillis) / 1000}s, text=$combinedText")
            return
        }

        val key = buildNotificationKey(packageName, combinedText, receivedAtMillis)

        if (isProcessedKey(key)) {
            Log.d(TAG, "Notification already processed: $key")
            return
        }

        val queued = queueNotification(key, packageName, combinedText, receivedAtMillis)
        if (queued) {
            Log.d(TAG, "Queued bank notification from $source: ${getBankNameFromPackage(packageName, combinedText)}")
        }

        drainPendingQueue(source)
    }

    private fun shouldProcessNotification(packageName: String, combinedText: String): Boolean {
        val packageLower = packageName.lowercase(Locale.US)
        val normalized = normalizeVietnamese(combinedText)

        val promotionalKeywords = listOf(
            "quang cao",
            "khuyen mai",
            "uu dai",
            "moi vay",
            "bao hiem",
            "otp",
            "ma xac thuc",
            "ma xac nhan",
            "dang nhap",
            "giam gia",
            "ma giam",
            "voucher",
            "coupon",
            "qua tang",
            "nhan qua",
            "doi qua",
            "hoan xu",
            "tich diem",
            "luu ma",
            "don tiktok",
            "chuc mung",
            "trung thuong",
            "may man"
        )
        if (promotionalKeywords.any { normalized.contains(it) }) return false

        val isWallet = packageLower.contains("momo") ||
            packageLower.contains("zalopay") ||
            packageLower.contains("viettelpes") ||
            packageLower.contains("viettelpay") ||
            (packageLower.contains("zalo") && normalized.contains("zalopay"))

        val transactionKeywords = listOf(
            "giao dich",
            "thanh toan",
            "chuyen",
            "nhan",
            "rut",
            "nap",
            "tru tien",
            "cong tien",
            "bien dong",
            "so du",
            "so tien",
            "tai khoan",
            "tk ",
            "stk"
        )
        val hasTransactionKeyword = transactionKeywords.any { normalized.contains(it) }

        val amountWithUnit = Regex("""\d+(?:[.,]\d{3})*\s*(?:vnd|d|k|dong)""").containsMatchIn(normalized)
        val signedAmount = Regex("""[+-]\s*\d""").containsMatchIn(normalized)
        val plainAmount = Regex("""\d{1,3}(?:[.,]\d{3})+|\d{4,}""").containsMatchIn(normalized)
        val hasAmountPattern = amountWithUnit || signedAmount || (plainAmount && hasTransactionKeyword)

        return hasAmountPattern && (isBankOrSmsApp(packageName) || isWallet || hasTransactionKeyword)
    }

    private fun isBankOrSmsApp(packageName: String): Boolean {
        val lower = packageName.lowercase(Locale.US)
        return lower.contains("vietcombank") ||
            lower.contains("vcb") ||
            lower.contains("techcombank") ||
            lower.contains("mbbank") ||
            lower.contains("mb.android") ||
            lower.contains("vietinbank") ||
            lower.contains("vbi") ||
            lower.contains("acb") ||
            lower.contains("bidv") ||
            lower.contains("tpb") ||
            lower.contains("tpbank") ||
            lower.contains("vib") ||
            lower.contains("vpbank") ||
            lower.contains("shb") ||
            lower.contains("sacombank") ||
            lower.contains("agribank") ||
            lower.contains("msb") ||
            lower.contains("ocb") ||
            lower.contains("seabank") ||
            lower.contains("hdbank") ||
            lower.contains("eximbank") ||
            lower.contains("lpbank") ||
            lower.contains("telephony") ||
            lower.contains("messaging") ||
            lower.contains("mms")
    }

    private fun queueNotification(
        key: String,
        appName: String,
        messageText: String,
        receivedAtMillis: Long
    ): Boolean {
        if (isProcessedKey(key)) return false

        val pending = readPendingNotifications()
        if (pending.any { it.key == key }) return false

        pending.add(
            PendingNotification(
                key = key,
                appName = appName,
                messageText = messageText,
                receivedAtMillis = receivedAtMillis,
                createdAtMillis = System.currentTimeMillis()
            )
        )
        writePendingNotifications(pending)
        return true
    }

    private fun drainPendingQueue(source: String) {
        val pending = readPendingNotifications()
        if (pending.isEmpty()) return

        Log.d(TAG, "Draining ${pending.size} pending bank notifications from $source")
        pending
            .sortedBy { it.createdAtMillis }
            .take(MAX_PENDING_SENDS_PER_DRAIN)
            .forEach { sendPendingNotification(it, 0) }
    }

    private fun sendPendingNotification(item: PendingNotification, attempt: Int) {
        if (isProcessedKey(item.key)) {
            removePendingNotification(item.key)
            return
        }

        if (!markInFlight(item.key)) return

        val token = cleanJsString(getValueFromAsyncStorage(this, "@ai_daily_expense_token"))
        val apiUrl = resolveApiUrl()

        if (token.isNullOrBlank()) {
            clearInFlight(item.key)
            Log.w(TAG, "Cannot send bank notification yet: user token is not available.")
            if (attempt < 3) {
                scanHandler.postDelayed({ sendPendingNotification(item, attempt + 1) }, 1500L * (attempt + 1))
            }
            return
        }

        val json = JSONObject().apply {
            put("bankName", getBankNameFromPackage(item.appName, item.messageText))
            put("rawMessage", item.messageText)
            put("receivedAt", toUtcIsoString(item.receivedAtMillis))
        }

        val request = Request.Builder()
            .url("$apiUrl/bank-transactions/from-notification")
            .post(json.toString().toRequestBody(jsonMediaType))
            .addHeader("Authorization", "Bearer $token")
            .build()

        Log.d(TAG, "Sending queued bank notification to $apiUrl/bank-transactions/from-notification")

        httpClient.newCall(request).enqueue(object : okhttp3.Callback {
            override fun onFailure(call: okhttp3.Call, e: IOException) {
                clearInFlight(item.key)
                Log.e(TAG, "Network/server error while sending bank notification: ${e.message}")
                if (attempt < 2) {
                    scanHandler.postDelayed({ sendPendingNotification(item, attempt + 1) }, 2000L * (attempt + 1))
                }
            }

            override fun onResponse(call: okhttp3.Call, response: okhttp3.Response) {
                try {
                    clearInFlight(item.key)
                    val code = response.code
                    if (response.isSuccessful) {
                        markNotificationDone(item.key)
                        Log.d(TAG, "Bank notification saved successfully.")
                        return
                    }

                    Log.w(TAG, "Bank notification API returned $code (${response.message}).")

                    if (code in 400..499 && code != 401 && code != 408 && code != 429) {
                        // Bad/non-transaction notification. Mark it done so it does not loop forever.
                        markNotificationDone(item.key)
                        return
                    }

                    if (attempt < 2) {
                        scanHandler.postDelayed({ sendPendingNotification(item, attempt + 1) }, 2000L * (attempt + 1))
                    }
                } finally {
                    response.close()
                }
            }
        })
    }

    private fun resolveApiUrl(): String {
        val rawApiUrl = cleanJsString(getValueFromAsyncStorage(this, "@api_url"))
        val configured = rawApiUrl?.trim()?.trimEnd('/').takeUnless { it.isNullOrBlank() }
        val apiUrl = (configured ?: DEFAULT_API_URL)
            .replace("https://testapi.somee.com", "http://testapi.somee.com")
            .trimEnd('/')

        return if (apiUrl.endsWith("/api", ignoreCase = true)) {
            apiUrl
        } else {
            "$apiUrl/api"
        }
    }

    private fun readPendingNotifications(): MutableList<PendingNotification> {
        val now = System.currentTimeMillis()
        val stored = prefs().getStringSet(PENDING_PREF_KEY, emptySet()).orEmpty()
        val pending = stored
            .mapNotNull { parsePendingNotification(it) }
            .filter { now - it.createdAtMillis <= PENDING_MAX_AGE_MS }
            .distinctBy { it.key }
            .toMutableList()

        if (pending.size != stored.size) {
            writePendingNotifications(pending)
        }

        return pending
    }

    private fun writePendingNotifications(pending: List<PendingNotification>) {
        val serialized = pending.map { pendingToJson(it) }.toSet()
        prefs().edit().putStringSet(PENDING_PREF_KEY, serialized).apply()
    }

    private fun parsePendingNotification(value: String): PendingNotification? {
        return try {
            val obj = JSONObject(value)
            PendingNotification(
                key = obj.getString("key"),
                appName = obj.getString("appName"),
                messageText = obj.getString("messageText"),
                receivedAtMillis = obj.optLong("receivedAtMillis", System.currentTimeMillis()),
                createdAtMillis = obj.optLong("createdAtMillis", System.currentTimeMillis())
            )
        } catch (e: Exception) {
            Log.w(TAG, "Cannot parse pending notification: ${e.message}")
            null
        }
    }

    private fun pendingToJson(item: PendingNotification): String {
        return JSONObject().apply {
            put("key", item.key)
            put("appName", item.appName)
            put("messageText", item.messageText)
            put("receivedAtMillis", item.receivedAtMillis)
            put("createdAtMillis", item.createdAtMillis)
        }.toString()
    }

    private fun removePendingNotification(key: String) {
        val pending = readPendingNotifications().filterNot { it.key == key }
        writePendingNotifications(pending)
    }

    private fun markNotificationDone(key: String) {
        markProcessedKey(key)
        removePendingNotification(key)
    }

    private fun isProcessedKey(key: String): Boolean {
        return prefs().getStringSet(PROCESSED_PREF_KEY, emptySet()).orEmpty().contains(key)
    }

    private fun markProcessedKey(key: String) {
        val processed = prefs().getStringSet(PROCESSED_PREF_KEY, emptySet())
            .orEmpty()
            .toMutableList()

        if (!processed.contains(key)) {
            processed.add(key)
        }

        while (processed.size > MAX_PROCESSED_KEYS) {
            processed.removeAt(0)
        }

        prefs().edit().putStringSet(PROCESSED_PREF_KEY, processed.toSet()).apply()
    }

    private fun markInFlight(key: String): Boolean {
        synchronized(inFlightKeys) {
            return inFlightKeys.add(key)
        }
    }

    private fun clearInFlight(key: String) {
        synchronized(inFlightKeys) {
            inFlightKeys.remove(key)
        }
    }

    private fun prefs(): SharedPreferences {
        return getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
    }

    private fun cleanJsString(jsString: String?): String? {
        if (jsString == null) return null
        var clean = jsString.trim()
        if (clean.startsWith("\"") && clean.endsWith("\"") && clean.length >= 2) {
            clean = clean.substring(1, clean.length - 1)
        }
        return clean
    }

    private fun getBankNameFromPackage(packageName: String, notificationText: String = ""): String {
        val lowerText = normalizeVietnamese(notificationText)
        return when {
            packageName.contains("vietcombank", ignoreCase = true) || packageName.contains("vcb", ignoreCase = true) -> "Vietcombank"
            packageName.contains("techcombank", ignoreCase = true) -> "Techcombank"
            packageName.contains("mbbank", ignoreCase = true) || packageName.contains("mb.android", ignoreCase = true) -> "MBBank"
            packageName.contains("vietinbank", ignoreCase = true) || packageName.contains("vbi", ignoreCase = true) -> "VietinBank"
            packageName.contains("acb", ignoreCase = true) -> "ACB"
            packageName.contains("bidv", ignoreCase = true) -> "BIDV"
            packageName.contains("tpb", ignoreCase = true) || packageName.contains("tpbank", ignoreCase = true) -> "TPBank"
            packageName.contains("vib", ignoreCase = true) -> "VIB"
            packageName.contains("vpbank", ignoreCase = true) -> "VPBank"
            packageName.contains("shb", ignoreCase = true) -> "SHB"
            packageName.contains("sacombank", ignoreCase = true) -> "Sacombank"
            packageName.contains("agribank", ignoreCase = true) -> "Agribank"
            packageName.contains("momo", ignoreCase = true) -> "MoMo"
            packageName.contains("zalopay", ignoreCase = true) ||
                (packageName.contains("zalo", ignoreCase = true) && lowerText.contains("zalopay")) -> "ZaloPay"
            packageName.contains("viettelpes", ignoreCase = true) ||
                packageName.contains("viettelpay", ignoreCase = true) -> "ViettelPay"
            packageName.contains("android.provider.telephony", ignoreCase = true) ||
                packageName.contains("messaging", ignoreCase = true) ||
                packageName.contains("mms", ignoreCase = true) -> "SMS"
            else -> packageName.substringAfterLast('.').uppercase(Locale.US)
        }
    }

    private fun getValueFromAsyncStorage(context: Context, key: String): String? {
        val databases = context.databaseList() ?: emptyArray()
        val candidateDbs = mutableListOf<String>()

        databases.forEach { db ->
            val name = db.lowercase(Locale.US)
            if ((name.startsWith("rkstorage") || name.contains("asynclocalstorage")) &&
                !name.endsWith("-journal") &&
                !name.endsWith("-wal") &&
                !name.endsWith("-shm")
            ) {
                candidateDbs.add(db)
            }
        }

        candidateDbs.add("RKStorage")
        candidateDbs.add("AsyncLocalStorage.db")

        for (dbName in candidateDbs.distinct()) {
            var db: SQLiteDatabase? = null
            try {
                val dbFile = context.getDatabasePath(dbName)
                if (!dbFile.exists()) continue

                db = SQLiteDatabase.openDatabase(
                    dbFile.absolutePath,
                    null,
                    SQLiteDatabase.OPEN_READONLY or SQLiteDatabase.NO_LOCALIZED_COLLATORS
                )

                val tableCheckCursor = db.rawQuery(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name='catalystLocalStorage'",
                    null
                )
                val tableExists = tableCheckCursor.moveToFirst()
                tableCheckCursor.close()

                if (!tableExists) {
                    db.close()
                    continue
                }

                val cursor = db.rawQuery("SELECT value FROM catalystLocalStorage WHERE key = ?", arrayOf(key))
                if (cursor.moveToFirst()) {
                    val value = cursor.getString(0)
                    cursor.close()
                    db.close()
                    Log.d(TAG, "Read AsyncStorage key $key from $dbName")
                    return value
                }
                cursor.close()
            } catch (e: Exception) {
                Log.w(TAG, "Cannot read AsyncStorage key $key from $dbName: ${e.message}")
            } finally {
                db?.close()
            }
        }

        Log.w(TAG, "AsyncStorage key $key not found.")
        return null
    }

    private fun extractFullBodyText(extras: android.os.Bundle): String {
        val text = extras.getCharSequence("android.text")?.toString().orEmpty()
        val bigText = extras.getCharSequence("android.bigText")?.toString().orEmpty()
        val subText = extras.getCharSequence("android.subText")?.toString().orEmpty()
        val infoText = extras.getCharSequence("android.infoText")?.toString().orEmpty()
        val summaryText = extras.getCharSequence("android.summaryText")?.toString().orEmpty()

        var body = text
        if (bigText.length > body.length) body = bigText
        if (subText.length > body.length) body = subText
        if (infoText.length > body.length) body = infoText
        if (summaryText.length > body.length) body = summaryText

        try {
            val textLines = extras.getCharSequenceArray("android.textLines")
            if (textLines != null && textLines.isNotEmpty()) {
                val linesText = textLines.joinToString(" ") { it?.toString().orEmpty() }.trim()
                if (linesText.length > body.length && !linesText.contains("...")) {
                    body = linesText
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Cannot read android.textLines: ${e.message}")
        }

        try {
            val messages = extras.getParcelableArray("android.messages")
            if (messages != null && messages.isNotEmpty()) {
                val builder = StringBuilder()
                for (message in messages) {
                    if (message is android.os.Bundle) {
                        val textValue = message.getCharSequence("text")
                        if (textValue != null) {
                            builder.append(textValue).append(' ')
                        }
                    }
                }
                val messagesText = builder.toString().trim()
                if (messagesText.length > body.length) {
                    body = messagesText
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Cannot read android.messages: ${e.message}")
        }

        return body
    }

    private fun buildNotificationKey(packageName: String, messageText: String, receivedAtMillis: Long): String {
        val normalizedMessage = normalizeVietnamese(messageText).replace(Regex("""\s+"""), " ").trim()
        // Dùng mốc 24 giờ thay vì 5 phút để một thông báo trong ngày không bao giờ bị đổi key và gửi lại
        val dayBucket = receivedAtMillis / (24L * 60L * 60L * 1000L)
        return sha256("$packageName|$dayBucket|$normalizedMessage")
    }

    private fun normalizeVietnamese(value: String): String {
        val lower = value.lowercase(Locale("vi", "VN"))
        val decomposed = Normalizer.normalize(lower, Normalizer.Form.NFD)
        return decomposed
            .replace(Regex("""\p{M}+"""), "")
            .replace('\u0111', 'd')
            .replace('\u0110', 'd')
    }

    private fun sha256(value: String): String {
        val bytes = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(Charsets.UTF_8))
        return bytes.joinToString("") { "%02x".format(it) }
    }

    private fun toUtcIsoString(millis: Long): String {
        return SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
            timeZone = TimeZone.getTimeZone("UTC")
        }.format(Date(millis))
    }
}
