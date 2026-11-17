package com.anonymous.AiDailyExpense.Mobile

import android.content.ComponentName
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.PowerManager
import android.provider.Settings
import com.facebook.react.bridge.Promise
import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.facebook.react.bridge.ReactMethod

class BackgroundPermissionModule(private val reactContext: ReactApplicationContext) :
  ReactContextBaseJavaModule(reactContext) {

  override fun getName(): String = "BackgroundPermission"

  @ReactMethod
  fun isIgnoringBatteryOptimizations(promise: Promise) {
    try {
      if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
        promise.resolve(true)
        return
      }

      val powerManager = reactContext.getSystemService(PowerManager::class.java)
      promise.resolve(powerManager.isIgnoringBatteryOptimizations(reactContext.packageName))
    } catch (error: Exception) {
      promise.reject("BATTERY_STATUS_ERROR", error)
    }
  }

  @ReactMethod
  fun isNotificationListenerConnected(promise: Promise) {
    try {
      promise.resolve(NotificationReceiverService.isConnected)
    } catch (error: Exception) {
      promise.reject("STATUS_ERROR", error)
    }
  }

  @ReactMethod
  fun isNotificationAccessEnabled(promise: Promise) {
    try {
      val pkgName = reactContext.packageName
      val flat = Settings.Secure.getString(reactContext.contentResolver, "enabled_notification_listeners")
      var enabled = false
      if (!android.text.TextUtils.isEmpty(flat)) {
        val names = flat.split(":")
        for (name in names) {
          val cn = ComponentName.unflattenFromString(name)
          if (cn != null && android.text.TextUtils.equals(pkgName, cn.packageName)) {
            enabled = true
            break
          }
        }
      }
      promise.resolve(enabled)
    } catch (error: Exception) {
      promise.reject("PERMISSION_ERROR", error)
    }
  }

  @ReactMethod
  fun rebindNotificationListener(promise: Promise) {
    try {
      NotificationReceiverService.rebindService(reactContext)
      promise.resolve(true)
    } catch (error: Exception) {
      promise.reject("REBIND_ERROR", error)
    }
  }

  @ReactMethod
  fun openNotificationAccessSettings(promise: Promise) {
    try {
      val intent = Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS").apply {
        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
      }
      reactContext.startActivity(intent)
      promise.resolve(true)
    } catch (error: Exception) {
      promise.reject("OPEN_SETTINGS_ERROR", error)
    }
  }

  @ReactMethod
  fun openBatteryOptimizationSettings(promise: Promise) {
    try {
      val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
        Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
          data = Uri.parse("package:${reactContext.packageName}")
        }
      } else {
        Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
          data = Uri.parse("package:${reactContext.packageName}")
        }
      }

      start(intent)
      promise.resolve(true)
    } catch (_: Exception) {
      openAppDetails(promise)
    }
  }

  @ReactMethod
  fun openAutoStartSettings(promise: Promise) {
    val candidates = buildAutoStartIntents()

    for (intent in candidates) {
      try {
        start(intent)
        promise.resolve(true)
        return
      } catch (_: Exception) {
        // Some manufacturers hide these screens between OS versions.
      }
    }

    openAppDetails(promise)
  }

  private fun buildAutoStartIntents(): List<Intent> {
    val packageName = reactContext.packageName
    val manufacturer = Build.MANUFACTURER.lowercase()
    val intents = mutableListOf<Intent>()

    fun add(packageName: String, className: String) {
      intents.add(Intent().setComponent(ComponentName(packageName, className)))
    }

    when {
      manufacturer.contains("huawei") || manufacturer.contains("honor") -> {
        add("com.huawei.systemmanager", "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity")
        add("com.huawei.systemmanager", "com.huawei.systemmanager.optimize.process.ProtectActivity")
        add("com.hihonor.systemmanager", "com.hihonor.systemmanager.startupmgr.ui.StartupNormalAppListActivity")
      }
      manufacturer.contains("xiaomi") || manufacturer.contains("redmi") || manufacturer.contains("poco") -> {
        add("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity")
      }
      manufacturer.contains("oppo") || manufacturer.contains("realme") || manufacturer.contains("oneplus") -> {
        add("com.coloros.safecenter", "com.coloros.safecenter.permission.startup.StartupAppListActivity")
        add("com.oppo.safe", "com.oppo.safe.permission.startup.StartupAppListActivity")
      }
      manufacturer.contains("vivo") || manufacturer.contains("iqoo") -> {
        add("com.vivo.permissionmanager", "com.vivo.permissionmanager.activity.BgStartUpManagerActivity")
      }
      manufacturer.contains("samsung") -> {
        add("com.samsung.android.lool", "com.samsung.android.sm.ui.battery.BatteryActivity")
        add("com.samsung.android.sm", "com.samsung.android.sm.ui.battery.BatteryActivity")
      }
    }

    intents.add(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
      data = Uri.parse("package:$packageName")
    })
    intents.add(Intent(Settings.ACTION_SETTINGS))
    return intents
  }

  private fun openAppDetails(promise: Promise) {
    try {
      start(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
        data = Uri.parse("package:${reactContext.packageName}")
      })
      promise.resolve(false)
    } catch (error: Exception) {
      promise.reject("OPEN_SETTINGS_ERROR", error)
    }
  }

  private fun start(intent: Intent) {
    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    reactContext.startActivity(intent)
  }
}
