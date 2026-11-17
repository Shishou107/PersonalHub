package com.anonymous.AiDailyExpense.Mobile

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.widget.RemoteViews

class AiWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        for (appWidgetId in appWidgetIds) {
            val views = RemoteViews(context.packageName, R.layout.ai_widget_layout)

            // 1. PendingIntent for Voice Button (opens VoiceWidgetActivity)
            val voiceIntent = Intent(context, VoiceWidgetActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            val voicePendingIntent = PendingIntent.getActivity(
                context,
                0, // Unique request code for voice
                voiceIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.widget_button_voice, voicePendingIntent)

            // 2. PendingIntent for Text/Manual Button (opens TextWidgetActivity)
            val textIntent = Intent(context, TextWidgetActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            val textPendingIntent = PendingIntent.getActivity(
                context,
                1, // Unique request code for text/manual
                textIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.widget_button_text, textPendingIntent)

            // Update the widget
            appWidgetManager.updateAppWidget(appWidgetId, views)
        }
    }
}
