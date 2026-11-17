import * as SecureStore from 'expo-secure-store';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

export const saveSecureToken = async (key: string, value: string): Promise<void> => {
  if (Platform.OS === 'web') {
    await AsyncStorage.setItem(key, value);
    return;
  }
  await SecureStore.setItemAsync(key, value);
};

export const getSecureToken = async (key: string): Promise<string | null> => {
  if (Platform.OS === 'web') {
    return AsyncStorage.getItem(key);
  }
  try {
    const token = await SecureStore.getItemAsync(key);
    if (token) return token;

    // Backward-compatibility: migrate existing token from AsyncStorage if present
    const legacyToken = await AsyncStorage.getItem(key);
    if (legacyToken) {
      await SecureStore.setItemAsync(key, legacyToken);
      await AsyncStorage.removeItem(key);
      return legacyToken;
    }
    return null;
  } catch {
    return AsyncStorage.getItem(key);
  }
};

export const deleteSecureToken = async (key: string): Promise<void> => {
  if (Platform.OS === 'web') {
    await AsyncStorage.removeItem(key);
    return;
  }
  try {
    await SecureStore.deleteItemAsync(key);
  } catch {
    // Ignore native delete errors
  }
  await AsyncStorage.removeItem(key).catch(() => {});
};
