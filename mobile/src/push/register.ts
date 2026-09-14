import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { registerDevice } from '../api/endpoints';
import { DevicePlatform } from '../api/types';

// Foreground notifications: show a banner + play a sound even while the app is open.
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
    shouldShowBanner: true,
    shouldShowList: true,
  }),
});

/**
 * Asks for notification permission, gets the Expo push token, and registers it with the backend so
 * chat + attendance reminders can reach this device. No-op on simulators (no push) and when the
 * user declines. Safe to call on every sign-in — registration is idempotent server-side.
 */
export async function registerForPush(): Promise<void> {
  if (!Device.isDevice) return;

  const { status: existing } = await Notifications.getPermissionsAsync();
  let status = existing;
  if (status !== 'granted') {
    const req = await Notifications.requestPermissionsAsync();
    status = req.status;
  }
  if (status !== 'granted') return;

  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'default',
      importance: Notifications.AndroidImportance.DEFAULT,
    });
  }

  const projectId =
    Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;
  const tokenResponse = await Notifications.getExpoPushTokenAsync(
    projectId ? { projectId } : undefined,
  );
  const expoPushToken = tokenResponse.data;

  const platform =
    Platform.OS === 'ios' ? DevicePlatform.Ios : Platform.OS === 'android' ? DevicePlatform.Android : DevicePlatform.Unknown;

  try {
    await registerDevice(expoPushToken, platform);
  } catch {
    // Non-fatal: the app works without push; we'll retry on the next sign-in.
  }
}
