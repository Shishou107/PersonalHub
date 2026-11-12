import * as Notifications from 'expo-notifications';
import { Platform, Alert, Linking } from 'react-native';
import { Task } from '../types/task';
import taskService from '../services/taskService';

// Configure foreground notification presentation
if (Platform.OS !== 'web') {
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldShowAlert: true,
      shouldPlaySound: true,
      shouldSetBadge: true,
      shouldShowBanner: true,
      shouldShowList: true,
    }),
  });
}

/**
 * Initialize Android notification channels with custom sounds and importance levels
 */
export const initNotifications = async () => {
  if (Platform.OS === 'android') {
    // 1. High priority channel: plays default system sound, importance MAX, shows banner
    await Notifications.setNotificationChannelAsync('high_priority', {
      name: 'Độ ưu tiên cao (High)',
      importance: Notifications.AndroidImportance.MAX,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#EF4444',
    });

    // 2. Normal priority channel: plays default system sound, importance DEFAULT
    await Notifications.setNotificationChannelAsync('normal_priority', {
      name: 'Độ ưu tiên vừa (Normal)',
      importance: Notifications.AndroidImportance.DEFAULT,
      lightColor: '#3B82F6',
    });

    // 3. Low priority channel: silent, importance LOW
    await Notifications.setNotificationChannelAsync('low_priority', {
      name: 'Độ ưu tiên thấp (Low)',
      importance: Notifications.AndroidImportance.LOW,
      sound: null, // silent
    });
  }
};

/**
 * Request notification permissions from the user
 */
export const requestPermissions = async () => {
  if (Platform.OS === 'web') return false;
  const { status: existingStatus, canAskAgain } = await Notifications.getPermissionsAsync();
  let finalStatus = existingStatus;

  if (existingStatus !== 'granted') {
    if (canAskAgain) {
      const { status } = await Notifications.requestPermissionsAsync();
      finalStatus = status;
    } else {
      // Permission was denied and we cannot ask again automatically
      Alert.alert(
        'Quyền thông báo bị tắt',
        'Ứng dụng cần quyền gửi thông báo để nhắc nhở công việc và cập nhật giao dịch. Vui lòng bật quyền thông báo trong cài đặt thiết bị.',
        [
          { text: 'Để sau', style: 'cancel' },
          { text: 'Mở Cài đặt', onPress: () => Linking.openSettings() }
        ]
      );
      return false;
    }
  }

  if (finalStatus !== 'granted') {
    Alert.alert(
      'Quyền thông báo bị từ chối',
      'Bạn đã từ chối quyền thông báo. Một số tính năng như nhắc nhở công việc sẽ không hoạt động ổn định.',
      [
        { text: 'Đồng ý', style: 'default' },
        { text: 'Mở Cài đặt', onPress: () => Linking.openSettings() }
      ]
    );
  }

  return finalStatus === 'granted';
};

/**
 * Synchronize local scheduled notifications with the upcoming pending tasks from the database
 */
export const syncNotifications = async (tasks: Task[]) => {
  try {
    const hasPermission = await requestPermissions();
    if (!hasPermission) {
      console.log('[Notification] Quyền thông báo chưa được cấp.');
      return;
    }

    // Cancel all scheduled notifications to prevent duplicates
    await Notifications.cancelAllScheduledNotificationsAsync();

    const now = Date.now();
    const getReminderReferenceTime = (task: Task) =>
      task.taskType === 'Schedule'
        ? (task.startAt || task.dueAt)
        : task.dueAt;

    // Schedules remind before StartAt; regular tasks remind before DueAt.
    const pendingTasksWithReminders = tasks.filter(task => 
      task.status === 'Pending' && 
      getReminderReferenceTime(task) &&
      task.reminderBeforeMinutes && 
      task.reminderBeforeMinutes > 0
    );

    console.log(`[Notification] Đang đồng bộ thông báo cho ${pendingTasksWithReminders.length} công việc.`);

    for (const task of pendingTasksWithReminders) {
      const reminderReferenceTime = getReminderReferenceTime(task)!;
      const referenceTime = new Date(reminderReferenceTime).getTime();
      const triggerTime = referenceTime - (task.reminderBeforeMinutes! * 60 * 1000);

      // Only schedule if the trigger time is in the future
      if (triggerTime > now) {
        let channelId = 'normal_priority';
        let soundName = 'normal_priority.mp3';
        let titlePrefix = 'Nhắc nhở công việc';

        if (task.priority === 'High') {
          channelId = 'high_priority';
          soundName = 'high_priority.wav';
        } else if (task.priority === 'Low') {
          channelId = 'low_priority';
          soundName = ''; // silent
        }

        const triggerDate = new Date(triggerTime);

        let title = `⏰ Đã đến: ${task.title}`;
        if (task.priority === 'High') {
          title = `🚨 [Quan trọng] Đã đến: ${task.title}`;
        }

        // Clean body to remove creator attribution like "Tạo bằng AI" or "Tạo tự động bằng AI"
        let body = '';
        if (task.description) {
          const descLower = task.description.toLowerCase();
          if (!descLower.includes('bằng ai') && !descLower.includes('bởi ai') && !descLower.includes('tạo tự động')) {
            body = task.description;
          }
        }
        if (!body) {
          body = task.taskType === 'Schedule'
            ? `Còn ${task.reminderBeforeMinutes} phút nữa là diễn ra.`
            : `Còn ${task.reminderBeforeMinutes} phút nữa là đến hạn thực hiện.`;
        }

        await Notifications.scheduleNotificationAsync({
          content: {
            title,
            body,
            sound: task.priority === 'Low' ? undefined : true,
            data: { taskId: task.taskId },
          },
          trigger: {
            type: 'date',
            date: triggerDate,
            channelId: Platform.OS === 'android' ? channelId : undefined,
          } as any,
        });

        console.log(`[Notification] Đã lên lịch thông báo cho công việc "${task.title}" lúc ${triggerDate.toLocaleString()}`);
      }
    }
  } catch (error) {
    console.error('[Notification] Lỗi khi đồng bộ thông báo:', error);
  }
};

/**
 * Fetch all pending tasks from the backend and synchronize notifications
 */
export const syncAllPendingTasksNotifications = async () => {
  try {
    const res = await taskService.getTasks(undefined, 'Pending');
    if (res.success && res.data) {
      await syncNotifications(res.data);
    }
  } catch (error) {
    console.error('[Notification] Lỗi khi đồng bộ toàn bộ công việc:', error);
  }
};
