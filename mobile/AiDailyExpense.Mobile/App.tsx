// Make by TÀI
import React, { useEffect } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider } from './src/context/AuthContext';
import RootNavigator from './src/navigation/RootNavigator';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { API_BASE_URL } from './src/utils/constants';
import { AppState, Platform } from 'react-native';
import { initNotifications, requestPermissions } from './src/utils/notificationHelper';
import { initLocalDatabase } from './src/database/db';
import { rebindNotificationListener } from './src/utils/backgroundPermission';

export default function App() {
  useEffect(() => {
    // Khởi tạo Database SQLite cục bộ trên thiết bị
    initLocalDatabase();

    AsyncStorage.setItem('@api_url', API_BASE_URL).catch((err) => {
      console.error('Lỗi khi lưu API URL vào AsyncStorage:', err);
    });

    const setupNotifications = async () => {
      if (Platform.OS !== 'web') {
        await initNotifications();
        await requestPermissions();
        await rebindNotificationListener().catch(() => {});
      }
    };
    setupNotifications();

    const sub = AppState.addEventListener('change', (nextState) => {
      if (nextState === 'active' && Platform.OS !== 'web') {
        rebindNotificationListener().catch(() => {});
      }
    });

    return () => sub.remove();
  }, []);

  return (
    <SafeAreaProvider style={{ flex: 1 }}>
      <AuthProvider>
        <RootNavigator />
      </AuthProvider>
    </SafeAreaProvider>
  );
}
