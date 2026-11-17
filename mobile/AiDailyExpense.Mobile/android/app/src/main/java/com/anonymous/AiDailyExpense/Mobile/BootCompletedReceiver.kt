package com.anonymous.AiDailyExpense.Mobile

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log

class BootCompletedReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val action = intent.action
        Log.d("BootCompletedReceiver", "Received broadcast action: $action")
        if (Intent.ACTION_BOOT_COMPLETED == action ||
            Intent.ACTION_MY_PACKAGE_REPLACED == action ||
            "android.intent.action.QUICKBOOT_POWERON" == action ||
            "com.htc.intent.action.QUICKBOOT_POWERON" == action
        ) {
            // Re-bind notification listener service automatically after phone boot / app update
            NotificationReceiverService.rebindService(context)
        }
    }
}
