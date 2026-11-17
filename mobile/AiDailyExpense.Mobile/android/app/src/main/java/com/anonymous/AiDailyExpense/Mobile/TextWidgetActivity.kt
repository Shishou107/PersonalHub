package com.anonymous.AiDailyExpense.Mobile

import android.app.Activity
import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.os.Bundle
import android.util.Log
import android.view.inputmethod.InputMethodManager
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.MediaType.Companion.toMediaType
import org.json.JSONObject
import java.io.IOException

class TextWidgetActivity : Activity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_text_widget)

        // Make dialog window look nice and wrap content
        window.setLayout(
            (resources.displayMetrics.widthPixels * 0.95).toInt(),
            android.view.ViewGroup.LayoutParams.WRAP_CONTENT
        )

        val rawToken = getValueFromAsyncStorage(this, "@ai_daily_expense_token")
        val rawApiUrl = getValueFromAsyncStorage(this, "@api_url")

        val token = cleanJsString(rawToken)
        val apiUrl = cleanJsString(rawApiUrl)

        if (token.isNullOrEmpty() || apiUrl.isNullOrEmpty()) {
            Toast.makeText(this, "Vui lòng mở ứng dụng và đăng nhập trước!", Toast.LENGTH_LONG).show()
            finish()
            return
        }

        val editTextInput = findViewById<EditText>(R.id.edit_text_input)
        val buttonCancel = findViewById<Button>(R.id.button_cancel)
        val buttonSend = findViewById<Button>(R.id.button_send)

        // Auto focus and show keyboard
        editTextInput.requestFocus()
        editTextInput.postDelayed({
            val imm = getSystemService(Context.INPUT_METHOD_SERVICE) as InputMethodManager
            imm.showSoftInput(editTextInput, InputMethodManager.SHOW_IMPLICIT)
        }, 150)

        buttonCancel.setOnClickListener {
            finish()
        }

        buttonSend.setOnClickListener {
            val text = editTextInput.text.toString().trim()
            if (text.isEmpty()) {
                Toast.makeText(this, "Vui lòng nhập nội dung!", Toast.LENGTH_SHORT).show()
            } else {
                sendToAi(text)
            }
        }
    }

    private fun sendToAi(text: String) {
        val rawToken = getValueFromAsyncStorage(this, "@ai_daily_expense_token")
        val rawApiUrl = getValueFromAsyncStorage(this, "@api_url")

        val token = cleanJsString(rawToken) ?: ""
        val apiUrl = cleanJsString(rawApiUrl) ?: ""

        runOnUiThread {
            Toast.makeText(this, "Đang gửi phân tích...", Toast.LENGTH_SHORT).show()
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
                Log.w("TextWidget", "Không thể đọc key $key từ database $dbName: ${e.message}")
            } finally {
                db?.close()
            }
        }
        return null
    }
}
