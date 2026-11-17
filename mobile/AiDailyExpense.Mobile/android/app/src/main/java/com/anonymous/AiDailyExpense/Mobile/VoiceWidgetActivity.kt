package com.anonymous.AiDailyExpense.Mobile

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.database.sqlite.SQLiteDatabase
import android.os.Bundle
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import android.util.Log
import android.widget.Toast
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.MediaType.Companion.toMediaType
import org.json.JSONObject
import java.io.IOException

class VoiceWidgetActivity : Activity() {
    private val REQUEST_CODE_SPEECH = 100

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val rawToken = getValueFromAsyncStorage(this, "@ai_daily_expense_token")
        val rawApiUrl = getValueFromAsyncStorage(this, "@api_url")

        val token = cleanJsString(rawToken)
        val apiUrl = cleanJsString(rawApiUrl)

        if (token.isNullOrEmpty() || apiUrl.isNullOrEmpty()) {
            Toast.makeText(this, "Vui lòng mở ứng dụng và đăng nhập trước!", Toast.LENGTH_LONG).show()
            finish()
            return
        }

        // Check if speech recognizer is available on the device to prevent OS-level Google services prompt
        if (!SpeechRecognizer.isRecognitionAvailable(this)) {
            Toast.makeText(this, "Nhận diện giọng nói chưa sẵn sàng trên thiết bị này.", Toast.LENGTH_LONG).show()
            finish()
            return
        }

        // Launch speech recognizer
        try {
            val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                putExtra(RecognizerIntent.EXTRA_LANGUAGE, "vi-VN")
                putExtra(RecognizerIntent.EXTRA_PROMPT, "Nói chi tiêu hoặc công việc...")
            }
            startActivityForResult(intent, REQUEST_CODE_SPEECH)
        } catch (e: Exception) {
            Toast.makeText(this, "Thiết bị không hỗ trợ nhận diện giọng nói", Toast.LENGTH_LONG).show()
            finish()
        }
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)

        if (requestCode == REQUEST_CODE_SPEECH) {
            if (resultCode == RESULT_OK && data != null) {
                val results = data.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS)
                val transcript = results?.firstOrNull()?.trim()
                if (!transcript.isNullOrEmpty()) {
                    sendToAi(transcript)
                    return
                }
            }
            finish()
        }
    }

    private fun sendToAi(text: String) {
        val rawToken = getValueFromAsyncStorage(this, "@ai_daily_expense_token")
        val rawApiUrl = getValueFromAsyncStorage(this, "@api_url")

        val token = cleanJsString(rawToken) ?: ""
        val apiUrl = cleanJsString(rawApiUrl) ?: ""

        runOnUiThread {
            Toast.makeText(this, "Đang phân tích: \"$text\"...", Toast.LENGTH_SHORT).show()
        }

        val client = OkHttpClient()
        val mediaType = "application/json; charset=utf-8".toMediaType()
        val json = JSONObject().apply {
            put("text", text)
        }
        val body = json.toString().toRequestBody(mediaType)
        val request = Request.Builder()
            .url("$apiUrl/ai/parse")
            .post(body)
            .addHeader("Authorization", "Bearer $token")
            .build()

        client.newCall(request).enqueue(object : okhttp3.Callback {
            override fun onFailure(call: okhttp3.Call, e: IOException) {
                runOnUiThread {
                    Toast.makeText(applicationContext, "Lỗi kết nối mạng: ${e.message}", Toast.LENGTH_LONG).show()
                    finish()
                }
            }

            override fun onResponse(call: okhttp3.Call, response: okhttp3.Response) {
                val bodyStr = response.body?.string()
                runOnUiThread {
                    if (response.isSuccessful && !bodyStr.isNullOrEmpty()) {
                        try {
                            val jsonRes = JSONObject(bodyStr)
                            if (jsonRes.optBoolean("success", false)) {
                                val dataObj = jsonRes.optJSONObject("data")
                                val intent = dataObj?.optString("intent")
                                
                                val message = when (intent) {
                                    "add_expense" -> {
                                        val savedItems = dataObj.optJSONArray("savedItems")
                                        if (savedItems != null && savedItems.length() > 0) {
                                            val firstItem = savedItems.getJSONObject(0)
                                            val itemTitle = firstItem.optString("title")
                                            val amount = firstItem.optDouble("amount")
                                            "Đã ghi nhận chi tiêu: $itemTitle (-${String.format("%,.0f", amount)}đ)"
                                        } else {
                                            "Đã lưu chi tiêu thành công!"
                                        }
                                    }
                                    "add_task" -> {
                                        val task = dataObj.optJSONObject("task")
                                        val taskTitle = task?.optString("title") ?: ""
                                        "Đã thêm công việc: $taskTitle"
                                    }
                                    else -> "Đã xử lý thông tin thành công!"
                                }
                                Toast.makeText(applicationContext, message, Toast.LENGTH_LONG).show()
                            } else {
                                val msg = jsonRes.optString("message", "Không thể xử lý nội dung này.")
                                Toast.makeText(applicationContext, "Lỗi: $msg", Toast.LENGTH_LONG).show()
                            }
                        } catch (e: Exception) {
                            Toast.makeText(applicationContext, "Đã lưu thành công!", Toast.LENGTH_LONG).show()
                        }
                    } else {
                        Toast.makeText(applicationContext, "Lỗi máy chủ: ${response.code}", Toast.LENGTH_LONG).show()
                    }
                    finish()
                }
            }
        })
    }

    private fun cleanJsString(jsString: String?): String? {
        if (jsString == null) return null
        var clean = jsString.trim()
        if (clean.startsWith("\"") && clean.endsWith("\"") && clean.length >= 2) {
            clean = clean.substring(1, clean.length - 1)
        }
        return clean
    }

    private fun getValueFromAsyncStorage(context: Context, key: String): String? {
        val databases = context.databaseList() ?: emptyArray()
        val candidateDbs = mutableListOf<String>()
        databases.forEach { db ->
            val name = db.lowercase()
            if ((name.startsWith("rkstorage") || name.contains("asynclocalstorage")) && 
                !name.endsWith("-journal") && !name.endsWith("-wal") && !name.endsWith("-shm")) {
                candidateDbs.add(db)
            }
        }
        candidateDbs.add("RKStorage")
        candidateDbs.add("AsyncLocalStorage.db")

        for (dbName in candidateDbs) {
            var db: SQLiteDatabase? = null
            try {
                val dbFile = context.getDatabasePath(dbName)
                if (!dbFile.exists()) {
                    continue
                }
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
                    return value
                }
                cursor.close()
            } catch (e: Exception) {
                Log.w("VoiceWidget", "Không thể đọc key $key từ database $dbName: ${e.message}")
            } finally {
                db?.close()
            }
        }
        return null
    }
}
