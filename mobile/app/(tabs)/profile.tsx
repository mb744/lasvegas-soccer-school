import React, { useState } from 'react';
import { Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { deleteAccount, resendEmailConfirmation } from '../../src/api/endpoints';
import { colors, radius, spacing } from '../../src/theme';

export default function ProfileScreen() {
  const { t } = useTranslation();
  const { me, signOut } = useAuth();
  const [deleting, setDeleting] = useState(false);
  const [verifyState, setVerifyState] = useState<'idle' | 'sending' | 'sent' | 'failed'>('idle');

  if (!me) return null;

  const onResend = async () => {
    setVerifyState('sending');
    try {
      await resendEmailConfirmation();
      setVerifyState('sent');
    } catch {
      setVerifyState('failed');
    }
  };

  const onDelete = () => {
    Alert.alert(
      t('profile.deletePromptTitle'),
      t('profile.deletePromptMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('profile.deleteConfirm'),
          style: 'destructive',
          onPress: async () => {
            setDeleting(true);
            try {
              await deleteAccount();
              // Backend has revoked tokens, anonymized the user, and locked the account.
              // Clear local session so the app returns to the sign-in screen.
              await signOut();
            } catch {
              setDeleting(false);
              Alert.alert(t('profile.deleteErrorTitle'), t('profile.deleteErrorMessage'));
            }
          },
        },
      ],
      { cancelable: true },
    );
  };

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <View style={styles.header}>
        <View style={styles.avatar}>
          <Text style={styles.avatarText}>
            {(me.firstName?.[0] ?? '') + (me.lastName?.[0] ?? '')}
          </Text>
        </View>
        <Text style={styles.name}>
          {me.firstName} {me.lastName}
        </Text>
        <Text style={styles.email}>{me.email}</Text>
      </View>

      {!me.emailConfirmed && (
        <View style={styles.verifyCard}>
          <Text style={styles.verifyTitle}>{t('profile.verifyTitle')}</Text>
          <Text style={styles.verifyBody}>{t('profile.verifyBody', { email: me.email })}</Text>
          {verifyState === 'sent' ? (
            <Text style={styles.verifyStatus}>{t('profile.verifySent')}</Text>
          ) : (
            <TouchableOpacity onPress={onResend} disabled={verifyState === 'sending'} accessibilityRole="button">
              <Text style={styles.verifyLink}>{t('profile.verifyResend')}</Text>
            </TouchableOpacity>
          )}
          {verifyState === 'failed' && <Text style={styles.verifyError}>{t('profile.verifyFailed')}</Text>}
        </View>
      )}

      <Text style={styles.sectionTitle}>{t('profile.players')}</Text>
      {me.players.length === 0 ? (
        <Text style={styles.muted}>—</Text>
      ) : (
        me.players.map((p) => (
          <View key={p.id} style={styles.playerCard}>
            <Text style={styles.playerName}>
              {p.firstName} {p.lastName}
            </Text>
            <Text style={styles.playerTeams}>
              {p.teams.length > 0 ? p.teams.map((tm) => tm.teamName).join(', ') : t('profile.noTeams')}
            </Text>
          </View>
        ))
      )}

      <TouchableOpacity style={styles.signOut} onPress={signOut} disabled={deleting}>
        <Text style={styles.signOutText}>{t('profile.signOut')}</Text>
      </TouchableOpacity>

      <TouchableOpacity style={styles.deleteAccount} onPress={onDelete} disabled={deleting}>
        <Text style={styles.deleteAccountText}>{t('profile.deleteAccount')}</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  header: { alignItems: 'center', marginBottom: spacing.xl },
  avatar: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: colors.brand,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: spacing.md,
  },
  avatarText: { color: colors.white, fontSize: 28, fontWeight: '800' },
  name: { fontSize: 22, fontWeight: '800', color: colors.text },
  email: { fontSize: 14, color: colors.subtext, marginTop: 2 },
  verifyCard: {
    backgroundColor: '#fff8e6',
    borderColor: colors.warning,
    borderWidth: 1,
    borderRadius: radius.md,
    padding: spacing.lg,
    marginBottom: spacing.xl,
    gap: spacing.xs,
  },
  verifyTitle: { fontSize: 15, fontWeight: '800', color: colors.text },
  verifyBody: { fontSize: 14, color: colors.text, lineHeight: 20 },
  verifyLink: { fontSize: 15, fontWeight: '800', color: colors.brandLight, marginTop: spacing.xs },
  verifyStatus: { fontSize: 14, fontWeight: '700', color: colors.success, marginTop: spacing.xs },
  verifyError: { fontSize: 13, color: colors.danger },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  muted: { color: colors.subtext },
  playerCard: {
    backgroundColor: colors.card,
    borderRadius: radius.md,
    padding: spacing.lg,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border,
  },
  playerName: { fontSize: 16, fontWeight: '700', color: colors.text },
  playerTeams: { fontSize: 14, color: colors.subtext, marginTop: 2 },
  signOut: {
    marginTop: spacing.xl,
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  signOutText: { color: colors.danger, fontSize: 16, fontWeight: '800' },
  deleteAccount: {
    marginTop: spacing.md,
    backgroundColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  deleteAccountText: { color: colors.white, fontSize: 16, fontWeight: '800' },
});
