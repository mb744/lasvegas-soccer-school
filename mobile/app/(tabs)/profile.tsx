import React, { useState } from 'react';
import { Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { cancelAccountDeletion, deleteAccount } from '../../src/api/endpoints';
import { longDate } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function ProfileScreen() {
  const { t } = useTranslation();
  const { me, signOut, refreshMe } = useAuth();
  const [busy, setBusy] = useState(false);

  if (!me) return null;

  const pendingDeletion = me.pendingDeletionAt ?? null;

  const onSchedule = () => {
    Alert.alert(
      t('profile.deletePromptTitle'),
      t('profile.deletePromptMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('profile.deleteConfirm'),
          style: 'destructive',
          onPress: async () => {
            setBusy(true);
            try {
              const res = await deleteAccount();
              const when = longDate(res.pendingDeletionAt);
              Alert.alert(
                t('profile.deleteScheduledTitle'),
                t('profile.deleteScheduledMessage', { date: when }),
                [{ text: t('common.ok'), onPress: () => void signOut() }],
                { cancelable: false },
              );
            } catch {
              setBusy(false);
              Alert.alert(t('profile.deleteErrorTitle'), t('profile.deleteErrorMessage'));
            }
          },
        },
      ],
      { cancelable: true },
    );
  };

  const onCancelDeletion = () => {
    Alert.alert(
      t('profile.cancelDeletionConfirmTitle'),
      t('profile.cancelDeletionConfirmMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('profile.cancelDeletionConfirm'),
          onPress: async () => {
            setBusy(true);
            try {
              await cancelAccountDeletion();
              await refreshMe();
              Alert.alert(t('profile.cancelDeletionSuccessTitle'), t('profile.cancelDeletionSuccessMessage'));
            } catch {
              Alert.alert(t('profile.cancelDeletionErrorTitle'), t('profile.cancelDeletionErrorMessage'));
            } finally {
              setBusy(false);
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

      {pendingDeletion ? (
        <View style={styles.deletionBanner}>
          <Text style={styles.deletionBannerTitle}>{t('profile.deleteBannerTitle')}</Text>
          <Text style={styles.deletionBannerBody}>
            {t('profile.deleteBannerBody', { date: longDate(pendingDeletion) })}
          </Text>
          <TouchableOpacity style={styles.cancelDeletionBtn} onPress={onCancelDeletion} disabled={busy}>
            <Text style={styles.cancelDeletionBtnText}>{t('profile.cancelDeletion')}</Text>
          </TouchableOpacity>
        </View>
      ) : null}

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

      <TouchableOpacity style={styles.signOut} onPress={signOut} disabled={busy}>
        <Text style={styles.signOutText}>{t('profile.signOut')}</Text>
      </TouchableOpacity>

      {!pendingDeletion ? (
        <TouchableOpacity style={styles.deleteAccount} onPress={onSchedule} disabled={busy}>
          <Text style={styles.deleteAccountText}>{t('profile.deleteAccount')}</Text>
        </TouchableOpacity>
      ) : null}
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
  deletionBanner: {
    backgroundColor: '#fff5f2',
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.md,
    padding: spacing.lg,
    marginBottom: spacing.xl,
  },
  deletionBannerTitle: { fontSize: 15, fontWeight: '800', color: colors.danger, marginBottom: spacing.xs },
  deletionBannerBody: { fontSize: 14, color: colors.text, marginBottom: spacing.md },
  cancelDeletionBtn: {
    backgroundColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  cancelDeletionBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
});
