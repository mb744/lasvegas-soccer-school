import { Platform } from 'react-native';
import * as Application from 'expo-application';
import Constants from 'expo-constants';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';
import { checkInDevice } from '../api/endpoints';
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

const INSTALLATION_ID_KEY = 'lvss.installationId';

/** Random id for this install, created once and kept in secure storage. */
async function getInstallationId(): Promise<string> {
  const existing = await SecureStore.getItemAsync(INSTALLATION_ID_KEY);
  if (existing) return existing;
  const id = Crypto.randomUUID();
  await SecureStore.setItemAsync(INSTALLATION_ID_KEY, id);
  return id;
}

/**
 * Tells the backend this install is in use, and registers its push token when it can get one.
 * Called on every signed-in launch and when the app comes back to the foreground, so the admin
 * "Mobile app usage" report sees parents who declined notifications too, and gets the reason when
 * push registration fails instead of the failure being swallowed.
 *
 * `askPermission` shows the OS notification prompt if the parent hasn't answered it yet; pass it
 * right after sign-in. Never throws.
 */
export async function checkIn({ askPermission = false }: { askPermission?: boolean } = {}): Promise<void> {
  try {
    let pushPermission: string = 'unavailable';
    let expoPushToken: string | null = null;
    let pushError: string | null = null;

    if (Device.isDevice) {
      try {
        let { status } = await Notifications.getPermissionsAsync();
        if (status === 'undetermined' && askPermission) {
          status = (await Notifications.requestPermissionsAsync()).status;
        }
        pushPermission = status;

        if (status === 'granted') {
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
          expoPushToken = tokenResponse.data;
        }
      } catch (e: unknown) {
        pushError = (e instanceof Error ? e.message : String(e)).slice(0, 500);
      }
    }

    await checkInDevice({
      installationId: await getInstallationId(),
      platform:
        Platform.OS === 'ios' ? DevicePlatform.Ios : Platform.OS === 'android' ? DevicePlatform.Android : DevicePlatform.Unknown,
      appVersion: Application.nativeApplicationVersion,
      buildNumber: Application.nativeBuildVersion,
      osVersion: Device.osVersion,
      pushPermission,
      expoPushToken,
      pushError,
    });
  } catch {
    // Non-fatal: the app works without it; the next launch tries again.
  }
}
