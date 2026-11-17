package com.anonymous.AiDailyExpense.Mobile

import android.os.Build
import android.os.Bundle
import android.content.Intent
import android.provider.Settings
import android.app.AlertDialog
import android.content.ComponentName
import android.text.TextUtils

import com.facebook.react.ReactActivity
import com.facebook.react.ReactActivityDelegate
import com.facebook.react.defaults.DefaultNewArchitectureEntryPoint.fabricEnabled
import com.facebook.react.defaults.DefaultReactActivityDelegate

import expo.modules.ReactActivityDelegateWrapper

class MainActivity : ReactActivity() {
  override fun onCreate(savedInstanceState: Bundle?) {
    // Set the theme to AppTheme BEFORE onCreate to support
    // coloring the background, status bar, and navigation bar.
    // This is required for expo-splash-screen.
    setTheme(R.style.AppTheme);
    super.onCreate(null)
  }

  override fun onResume() {
    super.onResume()
    if (!isNotificationServiceEnabled()) {
      showNotificationAccessDialog()
    } else {
      // Re-bind to ensure the listener service is connected without manual toggle
      NotificationReceiverService.rebindService(this)
    }
  }

  private fun isNotificationServiceEnabled(): Boolean {
    val pkgName = packageName
    val flat = Settings.Secure.getString(contentResolver, "enabled_notification_listeners")
    if (!TextUtils.isEmpty(flat)) {
      val names = flat.split(":")
      for (name in names) {
        val cn = ComponentName.unflattenFromString(name)
        if (cn != null && TextUtils.equals(pkgName, cn.packageName)) {
          return true
        }
      }
    }
    return false
  }

  private fun showNotificationAccessDialog() {
    AlertDialog.Builder(this)
      .setTitle("Quyền truy cập thông báo")
      .setMessage("Để ứng dụng tự động nhận diện giao dịch chuyển khoản từ các ứng dụng ngân hàng, vui lòng cấp quyền truy cập thông báo cho 'Quản lý cuộc sống'.")
      .setPositiveButton("Cấp quyền") { _, _ ->
        startActivity(Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"))
      }
      .setNegativeButton("Để sau", null)
      .show()
  }

  /**
   * Returns the name of the main component registered from JavaScript. This is used to schedule
   * rendering of the component.
   */
  override fun getMainComponentName(): String = "main"

  /**
   * Returns the instance of the [ReactActivityDelegate]. We use [DefaultReactActivityDelegate]
   * which allows you to enable New Architecture with a single boolean flags [fabricEnabled]
   */
  override fun createReactActivityDelegate(): ReactActivityDelegate {
    return ReactActivityDelegateWrapper(
          this,
          BuildConfig.IS_NEW_ARCHITECTURE_ENABLED,
          object : DefaultReactActivityDelegate(
              this,
              mainComponentName,
              fabricEnabled
          ){})
  }

  /**
    * Align the back button behavior with Android S
    * where moving root activities to background instead of finishing activities.
    * @see <a href="https://developer.android.com/reference/android/app/Activity#onBackPressed()">onBackPressed</a>
    */
  override fun invokeDefaultOnBackPressed() {
      if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.R) {
          if (!moveTaskToBack(false)) {
              // For non-root activities, use the default implementation to finish them.
              super.invokeDefaultOnBackPressed()
          }
          return
      }

      // Use the default back button implementation on Android S
      // because it's doing more than [Activity.moveTaskToBack] in fact.
      super.invokeDefaultOnBackPressed()
  }
}
