import React from 'react';
import { Alert, AppState, Linking, Modal, Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import * as Application from 'expo-application';
import * as SecureStore from 'expo-secure-store';
import * as Updates from 'expo-updates';
import { useTranslation } from 'react-i18next';
import { fetchAppVersion } from '../api/endpoints';
import { colors, radius, spacing } from '../theme';

/** How often a foregrounded app re-checks (launches always check). */
const STORE_CHECK_EVERY_MS = 6 * 60 * 60 * 1000;
const OTA_CHECK_EVERY_MS = 60 * 60 * 1000;
/** The store version the parent last tapped "Later" on, so we ask once per release. */
const DISMISSED_KEY = 'lvss.update.dismissedVersion';

/** Numeric dotted-version compare: 1.10.0 > 1.9.2. */
export function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map((n) => parseInt(n, 10) || 0);
  const pb = b.split('.').map((n) => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
}

/**
 * Keeps parents on a current app even when their phone doesn't auto-update:
 * - older than the admin-set minimum → full-screen "Please update" that can't be dismissed;
 * - older than the App Store release → "A new version is available", once per release;
 * - an over-the-air update downloaded → "Restart to update".
 * Checks on launch and when the app comes back to the foreground. Never throws.
 */
export function UpdateGate() {
  const { t } = useTranslation();
  const [required, setRequired] = React.useState<{ storeUrl: string | null } | null>(null);
  const lastStoreCheck = React.useRef(0);
  const lastOtaCheck = React.useRef(0);
  const prompting = React.useRef(false);

  const checkStore = React.useCallback(async (): Promise<boolean> => {
    const current = Application.nativeApplicationVersion;
    if (!current) return false; // simulator / Expo Go: nothing to compare
    lastStoreCheck.current = Date.now();
    try {
      const info = await fetchAppVersion(Platform.OS === 'android' ? 'android' : 'ios');

      if (info.minimumVersion && compareVersions(current, info.minimumVersion) < 0) {
        setRequired({ storeUrl: info.storeUrl });
        return true;
      }
      setRequired(null);

      if (info.latestVersion && info.storeUrl && compareVersions(current, info.latestVersion) < 0) {
        const dismissed = await SecureStore.getItemAsync(DISMISSED_KEY).catch(() => null);
        if (dismissed === info.latestVersion || prompting.current) return false;
        prompting.current = true;
        const url = info.storeUrl;
        Alert.alert(
          t('update.availableTitle'),
          t('update.availableBody', { version: info.latestVersion }),
          [
            {
              text: t('update.later'),
              style: 'cancel',
              onPress: () => {
                prompting.current = false;
                void SecureStore.setItemAsync(DISMISSED_KEY, info.latestVersion!).catch(() => undefined);
              },
            },
            {
              text: t('update.update'),
              onPress: () => {
                prompting.current = false;
                void Linking.openURL(url);
              },
            },
          ],
          { cancelable: false },
        );
        return true;
      }
    } catch {
      // Offline or server hiccup: try again next time.
    }
    return false;
  }, [t]);

  const checkOta = React.useCallback(async () => {
    if (!Updates.isEnabled || __DEV__ || prompting.current) return;
    lastOtaCheck.current = Date.now();
    try {
      const check = await Updates.checkForUpdateAsync();
      if (!check.isAvailable) return;
      await Updates.fetchUpdateAsync();
      prompting.current = true;
      Alert.alert(t('update.readyTitle'), t('update.readyBody'), [
        // "Later" is fine: a downloaded update applies on the next launch anyway.
        { text: t('update.later'), style: 'cancel', onPress: () => { prompting.current = false; } },
        { text: t('update.restart'), onPress: () => void Updates.reloadAsync() },
      ]);
    } catch {
      // No network or no update server: the app keeps running the current version.
    }
  }, [t]);

  const runChecks = React.useCallback(async (force: boolean) => {
    const now = Date.now();
    let blocked = false;
    if (force || now - lastStoreCheck.current > STORE_CHECK_EVERY_MS) blocked = await checkStore();
    if (!blocked && (force || now - lastOtaCheck.current > OTA_CHECK_EVERY_MS)) await checkOta();
  }, [checkStore, checkOta]);

  React.useEffect(() => {
    void runChecks(true);
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active') void runChecks(false);
    });
    return () => sub.remove();
  }, [runChecks]);

  if (!required) return null;

  return (
    <Modal visible animationType="fade" onRequestClose={() => undefined}>
      <View style={styles.screen}>
        <Text style={styles.icon}>⬆️</Text>
        <Text style={styles.title}>{t('update.requiredTitle')}</Text>
        <Text style={styles.body}>
          {required.storeUrl ? t('update.requiredBody') : t('update.requiredBodyNoStore')}
        </Text>
        {required.storeUrl ? (
          <TouchableOpacity style={styles.button} onPress={() => void Linking.openURL(required.storeUrl!)}>
            <Text style={styles.buttonText}>{t('update.update')}</Text>
          </TouchableOpacity>
        ) : null}
        <TouchableOpacity onPress={() => void runChecks(true)} style={{ marginTop: spacing.lg }}>
          <Text style={styles.link}>{t('update.checkAgain')}</Text>
        </TouchableOpacity>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: colors.bg,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.xl,
  },
  icon: { fontSize: 48, marginBottom: spacing.lg },
  title: { fontSize: 22, fontWeight: '800', color: colors.text, textAlign: 'center' },
  body: { fontSize: 15, color: colors.subtext, textAlign: 'center', marginTop: spacing.md, lineHeight: 21 },
  button: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
  },
  buttonText: { color: colors.white, fontSize: 16, fontWeight: '800' },
  link: { color: colors.brand, fontSize: 14, fontWeight: '700' },
});
