import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { useAuth } from '../context/AuthContext';
import AuthNavigator from './AuthNavigator';
import MainTabNavigator from './MainTabNavigator';
import Loading from '../components/Loading';
import { Platform, View, StyleSheet } from 'react-native';


const linking = {
  prefixes: ['aidailyexpense://'],
  config: {
    screens: {
      AiTab: 'ai',
    },
  },
};

const RootNavigator: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return <Loading fullScreen message="Đang chuẩn bị ứng dụng..." />;
  }

  const content = (
    <NavigationContainer linking={linking}>
      {isAuthenticated ? <MainTabNavigator /> : <AuthNavigator />}
    </NavigationContainer>
  );


  if (Platform.OS === 'web') {
    return (
      <View style={styles.webWrapper}>
        <View style={styles.webContainer}>
          {content}
        </View>
      </View>
    );
  }
  return content;
}

const styles = StyleSheet.create({
  webWrapper: {
    flex: 1,
    width: '100%',
    height: '100vh' as any,
    minHeight: '100vh' as any,
    backgroundColor: '#E2E8F0', // Màu nền xám nhạt bao bên ngoài màn hình máy tính
    alignItems: 'center',
    justifyContent: 'center',
  },
  webContainer: {
    width: '100%',
    maxWidth: 480,             // 👈 Giới hạn chiều rộng chuẩn màn hình điện thoại cho toàn bộ App
    height: '100%',
    maxHeight: '100vh' as any,
    backgroundColor: '#FFFFFF',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 20,
    overflow: 'hidden',
    borderRadius: 16,
  },
});

export default RootNavigator;
