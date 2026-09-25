import React from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { deleteTrainingLogin, fetchTrainingLogin, saveTrainingLogin } from '../../src/api/endpoints';
import { useAuth } from '../../src/auth/AuthContext';
import { colors, radius, spacing } from '../../src/theme';

// Mirrors PlayerCredentials on the backend so parents get instant, translated feedback.
const USERNAME_PATTERN = /^[a-z0-9._-]{3,32}$/;
const MIN_PASSWORD = 6;

/** Parent manages one child's login for the Daily Training app: create, rename, change password,
 *  or remove. */
export default function PlayerTrainingLoginScreen() {
  const { t, i18n } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const playerId = Number(id);
  const { me } = useAuth();
  const qc = useQueryClient();
  const player = me?.players.find((p) => p.id === playerId);
  const name = player?.firstName ?? '';

  const { data, isLoading } = useQuery({
    queryKey: ['trainingLogin', playerId],
    queryFn: () => fetchTrainingLogin(playerId),
    enabled: Number.isFinite(playerId),
  });

  const [username, setUsername] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [seeded, setSeeded] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (seeded || !data) return;
    setUsername(data.username ?? '');
    setSeeded(true);
  }, [data, seeded]);

  const hasLogin = !!data?.hasLogin;

  const save = useMutation({
    mutationFn: () =>
      saveTrainingLogin(playerId, {
        username: username.trim().toLowerCase(),
        ...(password ? { password } : {}),
      }),
    onSuccess: (saved) => {
      qc.setQueryData(['trainingLogin', playerId], saved);
      setUsername(saved.username ?? '');
      setPassword('');
      setError(null);
      Alert.alert(t('common.ok'), t('training.saved', { name }));
    },
    onError: (e) => {
      setError(isAxiosError(e) && e.response?.status === 409 ? t('training.usernameTaken') : t('training.failed'));
    },
  });

  const remove = useMutation({
    mutationFn: () => deleteTrainingLogin(playerId),
    onSuccess: () => {
      qc.setQueryData(['trainingLogin', playerId], { hasLogin: false, username: null, lastLoginAt: null });
      setUsername('');
      setPassword('');
    },
    onError: () => Alert.alert(t('common.retry'), t('training.failed')),
  });

  const submit = () => {
    const u = username.trim().toLowerCase();
    if (!USERNAME_PATTERN.test(u)) {
      setError(t('training.usernameHelp'));
      return;
    }
    if ((!hasLogin || password) && password.length < MIN_PASSWORD) {
      setError(t('training.passwordHelp'));
      return;
    }
    setError(null);
    save.mutate();
  };

  const confirmRemove = () => {
    Alert.alert(
      t('training.removeTitle'),
      t('training.removeMessage', { name }),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('training.removeConfirm'), style: 'destructive', onPress: () => remove.mutate() },
      ],
      { cancelable: true },
    );
  };

  if (isLoading || !data) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        {isLoading ? <ActivityIndicator size="large" color={colors.brand} /> : null}
      </View>
    );
  }

  const lastSignIn = data.lastLoginAt
    ? t('training.lastSignIn', { when: new Date(data.lastLoginAt).toLocaleString(i18n.language) })
    : t('training.neverSignedIn');

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: player ? `${player.firstName} ${player.lastName}` : '' }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg, paddingBottom: 96 }} keyboardShouldPersistTaps="handled">
        <Text style={styles.sectionTitle}>{t('training.heading')}</Text>
        <Text style={styles.body}>{t('training.intro', { name })}</Text>

        <View style={styles.statusCard}>
          <Text style={styles.statusText}>{hasLogin ? lastSignIn : t('training.noLogin', { name })}</Text>
        </View>

        <Text style={styles.label}>{t('training.username')}</Text>
        <TextInput
          style={styles.input}
          value={username}
          onChangeText={setUsername}
          autoCapitalize="none"
          autoCorrect={false}
          maxLength={32}
          placeholder="sofia.m"
          placeholderTextColor={colors.subtext}
        />
        <Text style={styles.help}>{t('training.usernameHelp')}</Text>

        <Text style={[styles.label, { marginTop: spacing.lg }]}>
          {hasLogin ? t('training.newPassword') : t('training.password')}
        </Text>
        <TextInput
          style={styles.input}
          value={password}
          onChangeText={setPassword}
          secureTextEntry
          autoCapitalize="none"
          autoComplete="new-password"
          maxLength={128}
        />
        <Text style={styles.help}>
          {hasLogin ? t('training.passwordKeepHelp', { name }) : t('training.passwordHelp')}
        </Text>

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <TouchableOpacity
          style={[styles.primaryBtn, save.isPending && styles.btnDisabled]}
          onPress={submit}
          disabled={save.isPending}
        >
          {save.isPending ? (
            <ActivityIndicator color={colors.white} />
          ) : (
            <Text style={styles.primaryBtnText}>{hasLogin ? t('training.save') : t('training.create')}</Text>
          )}
        </TouchableOpacity>

        {hasLogin && (
          <TouchableOpacity
            style={[styles.dangerBtn, remove.isPending && styles.btnDisabled]}
            onPress={confirmRemove}
            disabled={remove.isPending}
          >
            <Text style={styles.dangerBtnText}>{t('training.remove')}</Text>
          </TouchableOpacity>
        )}

        <Text style={[styles.help, { marginTop: spacing.xl }]}>{t('training.getApp')}</Text>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  body: { fontSize: 15, color: colors.text, lineHeight: 21 },
  statusCard: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginVertical: spacing.lg,
  },
  statusText: { fontSize: 14, color: colors.subtext },
  label: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.xs,
  },
  input: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 15,
    color: colors.text,
  },
  help: { fontSize: 12, color: colors.subtext, marginTop: spacing.xs },
  error: { fontSize: 14, color: colors.danger, fontWeight: '700', marginTop: spacing.md },
  primaryBtn: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  dangerBtn: {
    marginTop: spacing.md,
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  dangerBtnText: { color: colors.danger, fontSize: 15, fontWeight: '800' },
  btnDisabled: { opacity: 0.5 },
});
