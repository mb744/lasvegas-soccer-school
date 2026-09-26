import React from 'react';
import { ActivityIndicator, Alert, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchFamily,
  removeFamilyMember,
  resendFamilyInvite,
  setFamilyMemberAccess,
} from '../api/endpoints';
import { FamilyAccessLevel, type FamilyMember } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { useOptionPicker, type PickerOption } from '../ui/useOptionPicker';
import { colors, radius, spacing } from '../theme';

/** Profile tab: everyone on the family, with invite / resend / change access / remove for parents
 *  and guardians. View-only members see the list and can leave. */
export function FamilySection() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();
  const { refreshMe } = useAuth();
  const picker = useOptionPicker();
  const family = useQuery({ queryKey: ['family'], queryFn: fetchFamily, retry: false });

  const onError = (e: unknown) => {
    const data = (e as { response?: { data?: unknown } })?.response?.data;
    Alert.alert(t('family.errorTitle'), typeof data === 'string' && data ? data : t('family.errorBody'));
  };
  const refresh = () => qc.invalidateQueries({ queryKey: ['family'] });

  const resend = useMutation({
    mutationFn: (m: FamilyMember) => resendFamilyInvite(m.contactId!),
    onSuccess: () => { void refresh(); Alert.alert(t('family.resentTitle'), t('family.resentBody')); },
    onError,
  });
  const setAccess = useMutation({
    mutationFn: (v: { m: FamilyMember; level: FamilyAccessLevel }) => setFamilyMemberAccess(v.m.contactId!, v.level),
    onSuccess: () => void refresh(),
    onError,
  });
  const remove = useMutation({
    mutationFn: (m: FamilyMember) => removeFamilyMember(m),
    onSuccess: async (_, m) => {
      await refresh();
      // Leaving a family changes what this login sees everywhere.
      if (m.isYou) await refreshMe();
    },
    onError,
  });

  if (family.isLoading) return <ActivityIndicator color={colors.brand} style={{ marginVertical: spacing.lg }} />;
  // Not part of a family (e.g. a coach-only login): nothing to show.
  if (!family.data) return null;
  const { canManage, members } = family.data;

  const confirmRemove = (m: FamilyMember) =>
    Alert.alert(
      m.isYou ? t('family.leaveTitle') : t('family.removeTitle', { name: m.name }),
      m.isYou ? t('family.leaveBody') : t('family.removeBody', { name: m.name }),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: m.isYou ? t('family.leave') : t('family.remove'), style: 'destructive', onPress: () => remove.mutate(m) },
      ],
    );

  const openActions = (m: FamilyMember) => {
    const options: PickerOption[] = [];
    if (canManage && m.status === 'invited' && m.contactId !== null)
      options.push({ label: t('family.resend'), onPick: () => resend.mutate(m) });
    if (canManage && m.contactId !== null && !m.isYou) {
      options.push(
        m.role === 'viewer'
          ? { label: t('family.makeGuardian'), onPick: () => setAccess.mutate({ m, level: FamilyAccessLevel.Guardian }) }
          : { label: t('family.makeViewer'), onPick: () => setAccess.mutate({ m, level: FamilyAccessLevel.Viewer }) },
      );
    }
    if (canManage || m.isYou)
      options.push({ label: m.isYou ? t('family.leave') : t('family.remove'), onPick: () => confirmRemove(m) });
    if (options.length > 0) picker.open(m.name || m.email || '', options);
  };

  const busy = resend.isPending || setAccess.isPending || remove.isPending;

  return (
    <View style={{ marginTop: spacing.lg }}>
      {picker.sheet}
      <Text style={styles.sectionTitle}>{t('family.title')}</Text>
      <Text style={styles.blurb}>{canManage ? t('family.blurbManage') : t('family.blurbViewer')}</Text>

      {members.map((m, i) => {
        const tappable = m.role !== 'owner' && (canManage || m.isYou);
        return (
          <TouchableOpacity
            key={`${m.contactId ?? 'c'}-${m.collaboratorId ?? 'l'}-${i}`}
            style={styles.row}
            onPress={() => openActions(m)}
            disabled={!tappable || busy}
            accessibilityRole={tappable ? 'button' : undefined}
          >
            <View style={{ flex: 1 }}>
              <Text style={styles.name}>
                {m.name || m.email}
                {m.isYou ? <Text style={styles.you}>  {t('family.you')}</Text> : null}
              </Text>
              {m.email ? <Text style={styles.email}>{m.email}</Text> : null}
              {m.status === 'invited' ? (
                <Text style={styles.pending}>
                  {m.inviteSentAt
                    ? t('family.invitedOn', { date: new Date(m.inviteSentAt).toLocaleDateString() })
                    : t('family.notSignedInYet')}
                </Text>
              ) : null}
            </View>
            <View style={[styles.badge, m.role === 'viewer' ? styles.badgeViewer : styles.badgeGuardian]}>
              <Text style={[styles.badgeText, m.role === 'viewer' ? styles.badgeTextViewer : styles.badgeTextGuardian]}>
                {m.role === 'owner' ? t('family.roleOwner') : m.role === 'guardian' ? t('family.roleGuardian') : t('family.roleViewer')}
              </Text>
            </View>
            {tappable ? <Text style={styles.chevron}>›</Text> : null}
          </TouchableOpacity>
        );
      })}

      {canManage ? (
        <TouchableOpacity style={styles.inviteBtn} onPress={() => router.push('/family/invite')} disabled={busy}>
          <Text style={styles.inviteBtnText}>＋ {t('family.invite')}</Text>
        </TouchableOpacity>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.xs,
  },
  blurb: { fontSize: 13, color: colors.subtext, lineHeight: 18, marginBottom: spacing.sm },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  name: { fontSize: 15, fontWeight: '700', color: colors.text },
  you: { fontSize: 12, fontWeight: '600', color: colors.subtext },
  email: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  pending: { fontSize: 12, color: colors.warning, marginTop: 2 },
  badge: { borderRadius: radius.md, paddingHorizontal: spacing.sm, paddingVertical: 2 },
  badgeGuardian: { backgroundColor: '#e3f1ea' },
  badgeViewer: { backgroundColor: '#eef0ef' },
  badgeText: { fontSize: 11, fontWeight: '800' },
  badgeTextGuardian: { color: colors.brand },
  badgeTextViewer: { color: colors.subtext },
  chevron: { fontSize: 22, color: colors.subtext },
  inviteBtn: {
    borderWidth: 1,
    borderColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  inviteBtnText: { color: colors.brand, fontSize: 15, fontWeight: '800' },
});
