import React, { useCallback, useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Linking,
  Platform,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Redirect } from 'expo-router';
import Constants from 'expo-constants';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../src/auth/AuthContext';
import {
  facebookConfigured,
  googleConfigured,
  signInWithFacebook,
  signInWithGoogle,
} from '../src/auth/oauth';
import { colors, radius, spacing } from '../src/theme';

export default function LoginScreen() {
  const { t } = useTranslation();
  const { me, signIn, signInWithTokens } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (me) return <Redirect href="/(tabs)" />;

  const onSubmit = async () => {
    setError(null);
    setBusy(true);
    try {
      await signIn(email.trim(), password);
    } catch (e: any) {
      // The backend returns a friendly message for OAuth-only accounts and for locked-out
      // accounts — surface it so the parent knows to tap the Google/Facebook button instead
      // of retrying with the same password.
      const serverMsg = typeof e?.response?.data === 'string' ? e.response.data : undefined;
      setError(serverMsg && serverMsg.length > 0 ? serverMsg : t('login.error'));
    } finally {
      setBusy(false);
    }
  };

  const openForgotPassword = useCallback(() => {
    // Password reset flow lives on the web — the mobile app deep-links out to it so parents
    // land on the same page whether they hit "Forgot password?" from web or from mobile.
    const base = (Constants.expoConfig?.extra as { apiBaseUrl?: string } | undefined)?.apiBaseUrl
      ?? 'https://registration.lasvegassoccerschool.org';
    const trimmed = base.replace(/\/$/, '');
    const url = email.trim()
      ? `${trimmed}/forgot-password?email=${encodeURIComponent(email.trim())}`
      : `${trimmed}/forgot-password`;
    void Linking.openURL(url);
  }, [email]);

  const runSocial = async (kind: 'google' | 'facebook') => {
    setError(null);
    setBusy(true);
    try {
      const res = kind === 'google' ? await signInWithGoogle() : await signInWithFacebook();
      await signInWithTokens(res);
    } catch (e: any) {
      // 'cancel'/'dismiss' means the user closed the browser — silent.
      const msg = String(e?.message ?? '');
      if (msg === 'cancel' || msg === 'dismiss') return;

      // Show the actual failure so we can diagnose in production. Server 4xx bodies (e.g. "Google
      // sign-in failed.") arrive on e.response.data as a string; expo-auth-session errors surface
      // on e.message. Fall back to the canned translation only when we have nothing useful.
      const serverMsg = typeof e?.response?.data === 'string' ? e.response.data : undefined;
      const detail = serverMsg || msg || '';
      // eslint-disable-next-line no-console
      console.log(`[oauth:${kind}]`, detail, e);
      setError(detail ? `${t('login.errorSocial')} (${detail})` : t('login.errorSocial'));
    } finally {
      setBusy(false);
    }
  };

  const onGoogle = () => runSocial('google');
  const onFacebook = () => runSocial('facebook');

  const showGoogle = googleConfigured();
  const showFacebook = facebookConfigured();
  const showAnySocial = showGoogle || showFacebook;

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <View style={styles.inner}>
        <Text style={styles.brand}>{t('appName')}</Text>
        <Text style={styles.title}>{t('login.title')}</Text>
        <Text style={styles.subtitle}>{t('login.subtitle')}</Text>

        <TextInput
          style={styles.input}
          placeholder={t('login.email')}
          placeholderTextColor={colors.subtext}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          value={email}
          onChangeText={setEmail}
        />
        <TextInput
          style={styles.input}
          placeholder={t('login.password')}
          placeholderTextColor={colors.subtext}
          secureTextEntry
          value={password}
          onChangeText={setPassword}
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <TouchableOpacity
          style={[styles.button, busy && styles.buttonDisabled]}
          onPress={onSubmit}
          disabled={busy || !email || !password}
        >
          {busy ? (
            <ActivityIndicator color={colors.white} />
          ) : (
            <Text style={styles.buttonText}>{t('login.signIn')}</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity onPress={openForgotPassword} style={styles.forgotBtn}>
          <Text style={styles.forgotText}>{t('login.forgotPassword')}</Text>
        </TouchableOpacity>

        {showAnySocial && (
          <>
            <View style={styles.dividerRow}>
              <View style={styles.dividerLine} />
              <Text style={styles.dividerText}>{t('login.or')}</Text>
              <View style={styles.dividerLine} />
            </View>

            {showGoogle && (
              <TouchableOpacity style={styles.socialBtn} onPress={onGoogle} disabled={busy}>
                <Text style={styles.socialText}>{t('login.continueWithGoogle')}</Text>
              </TouchableOpacity>
            )}
            {showFacebook && (
              <TouchableOpacity style={[styles.socialBtn, styles.facebookBtn]} onPress={onFacebook} disabled={busy}>
                <Text style={styles.socialTextFacebook}>{t('login.continueWithFacebook')}</Text>
              </TouchableOpacity>
            )}
          </>
        )}
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.brand },
  inner: { flex: 1, justifyContent: 'center', padding: spacing.xl },
  brand: { color: colors.accent, fontSize: 16, fontWeight: '700', marginBottom: spacing.sm },
  title: { color: colors.white, fontSize: 30, fontWeight: '800' },
  subtitle: { color: '#cfe0d8', fontSize: 15, marginTop: spacing.sm, marginBottom: spacing.xl },
  input: {
    backgroundColor: colors.white,
    borderRadius: radius.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    fontSize: 16,
    color: colors.text,
    marginBottom: spacing.md,
  },
  error: { color: '#ffd2cc', marginBottom: spacing.md },
  button: {
    backgroundColor: colors.accent,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  buttonDisabled: { opacity: 0.6 },
  buttonText: { color: colors.brand, fontSize: 17, fontWeight: '800' },
  forgotBtn: { marginTop: spacing.md, alignItems: 'center' },
  forgotText: { color: colors.white, fontSize: 14, textDecorationLine: 'underline' },
  dividerRow: { flexDirection: 'row', alignItems: 'center', marginVertical: spacing.lg },
  dividerLine: { flex: 1, height: 1, backgroundColor: 'rgba(255,255,255,0.25)' },
  dividerText: { color: '#cfe0d8', paddingHorizontal: spacing.md, fontSize: 13, fontWeight: '700' },
  socialBtn: {
    backgroundColor: colors.white,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  socialText: { color: colors.text, fontSize: 15, fontWeight: '700' },
  facebookBtn: { backgroundColor: '#1877F2' },
  socialTextFacebook: { color: colors.white, fontSize: 15, fontWeight: '700' },
});
