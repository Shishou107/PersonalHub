package com.anonymous.AiDailyExpense.Mobile

import android.speech.tts.TextToSpeech
import com.facebook.react.bridge.Promise
import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.facebook.react.bridge.ReactMethod
import java.util.Locale

class StudySpeechModule(private val reactContext: ReactApplicationContext) :
  ReactContextBaseJavaModule(reactContext) {

  private var tts: TextToSpeech? = null

  override fun getName(): String = "StudySpeech"

  @ReactMethod
  fun speak(text: String, language: String?, promise: Promise) {
    val cleanText = text.trim()
    if (cleanText.isBlank()) {
      promise.resolve(false)
      return
    }

    val locale = when (language?.lowercase(Locale.US)) {
      "japanese", "ja", "ja-jp" -> Locale.JAPAN
      "english", "en", "en-us" -> Locale.US
      else -> if (containsJapanese(cleanText)) Locale.JAPAN else Locale.US
    }

    val existing = tts
    if (existing != null) {
      speakNow(existing, cleanText, locale, promise)
      return
    }

    tts = TextToSpeech(reactContext) { status ->
      val engine = tts
      if (status != TextToSpeech.SUCCESS || engine == null) {
        promise.reject("TTS_INIT_ERROR", "Không khởi tạo được giọng đọc.")
        return@TextToSpeech
      }

      speakNow(engine, cleanText, locale, promise)
    }
  }

  @ReactMethod
  fun stop(promise: Promise) {
    tts?.stop()
    promise.resolve(true)
  }

  override fun invalidate() {
    tts?.stop()
    tts?.shutdown()
    tts = null
    super.invalidate()
  }

  private fun speakNow(engine: TextToSpeech, text: String, locale: Locale, promise: Promise) {
    try {
      engine.language = locale
      engine.setSpeechRate(if (locale == Locale.JAPAN) 0.86f else 0.92f)
      val result = engine.speak(text, TextToSpeech.QUEUE_FLUSH, null, "study-${System.currentTimeMillis()}")
      promise.resolve(result != TextToSpeech.ERROR)
    } catch (error: Exception) {
      promise.reject("TTS_SPEAK_ERROR", error)
    }
  }

  private fun containsJapanese(value: String): Boolean {
    return value.any { ch ->
      (ch in '\u3040'..'\u30ff') || (ch in '\u4e00'..'\u9fff')
    }
  }
}
