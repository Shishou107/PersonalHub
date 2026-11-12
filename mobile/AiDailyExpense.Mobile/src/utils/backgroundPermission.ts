import { NativeModules, Platform } from 'react-native';

type BackgroundPermissionModule = {
  isIgnoringBatteryOptimizations: () => Promise<boolean>;
  openBatteryOptimizationSettings: () => Promise<boolean>;
  openAutoStartSettings: () => Promise<boolean>;
  isNotificationListenerConnected: () => Promise<boolean>;
  isNotificationAccessEnabled: () => Promise<boolean>;
  rebindNotificationListener: () => Promise<boolean>;
  openNotificationAccessSettings: () => Promise<boolean>;
};

const nativeModule = NativeModules.BackgroundPermission as BackgroundPermissionModule | undefined;

export const isBackgroundPermissionAvailable = Platform.OS === 'android' && !!nativeModule;

export const isIgnoringBatteryOptimizations = async () => {
  if (!isBackgroundPermissionAvailable) return true;
  return nativeModule!.isIgnoringBatteryOptimizations();
};

export const openBatteryOptimizationSettings = async () => {
  if (!isBackgroundPermissionAvailable) return false;
  return nativeModule!.openBatteryOptimizationSettings();
};

export const openAutoStartSettings = async () => {
  if (!isBackgroundPermissionAvailable) return false;
  return nativeModule!.openAutoStartSettings();
};

export const isNotificationListenerConnected = async (): Promise<boolean> => {
  if (!isBackgroundPermissionAvailable || !nativeModule?.isNotificationListenerConnected) return true;
  try {
    return await nativeModule.isNotificationListenerConnected();
  } catch {
    return true;
  }
};

export const isNotificationAccessEnabled = async (): Promise<boolean> => {
  if (!isBackgroundPermissionAvailable || !nativeModule?.isNotificationAccessEnabled) return true;
  try {
    return await nativeModule.isNotificationAccessEnabled();
  } catch {
    return true;
  }
};

export const rebindNotificationListener = async (): Promise<boolean> => {
  if (!isBackgroundPermissionAvailable || !nativeModule?.rebindNotificationListener) return false;
  try {
    return await nativeModule.rebindNotificationListener();
  } catch {
    return false;
  }
};

export const openNotificationAccessSettings = async (): Promise<boolean> => {
  if (!isBackgroundPermissionAvailable || !nativeModule?.openNotificationAccessSettings) return false;
  try {
    return await nativeModule.openNotificationAccessSettings();
  } catch {
    return false;
  }
};
