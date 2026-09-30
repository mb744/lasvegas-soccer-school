import React from 'react';
import { ActivityIndicator, Alert, StyleSheet, Switch, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchNotificationPreferences, saveNotificationPreferences } from '../api/endpoints';
import { EmailPreference, type NotificationPreferences } from '../api/types';
import { colors, radius, spacing } from '../theme';

/** Profile → Notifications: push to this person's phones on/off, and the game / event emails.
 *  The same settings the "Update your preferences here" link in event emails opens. */
export function NotificationsSection() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const prefs = useQuery({ queryKey: ['notificationPreferences'], queryFn: fetchNotificationPreferences, retry: false });

  const save = useMutation({
    mutationFn: saveNotificationPreferences,
    // Show the change right away; roll back if the server says no.
    onMutate: async (next) => {
      await qc.cancelQueries({ queryKey: ['notificationPreferences'] });
      const previous = qc.getQueryData<NotificationPreferences>(['notificationPreferences']);
      if (previous) {
        qc.setQueryData<NotificationPreferences>(['notificationPreferences'], {
          ...previous,
          gameEmails: next.gameEmails,
          eventEmails: next.eventEmails,
          pushNotifications: next.pushNotifications ?? previous.pushNotifications,
        });
      }
      return { previous };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.previous) qc.setQueryData(['notificationPreferences'], ctx.previous);
      Alert.alert(t('notifications.errorTitle'), t('notifications.errorBody'));
    },
    onSuccess: (data) => qc.setQueryData(['notificationPreferences'], data),
  });

  const p = prefs.data;
  const update = (patch: Partial<Pick<NotificationPreferences, 'gameEmails' | 'eventEmails' | 'pushNotifications'>>) => {
    if (!p) return;
    save.mutate({
      gameEmails: patch.gameEmails ?? p.gameEmails,
      eventEmails: patch.eventEmails ?? p.eventEmails,
      pushNotifications: patch.pushNotifications ?? p.pushNotifications,
    });
  };

  const options: { value: EmailPreference; label: string }[] = [
    { value: EmailPreference.Default, label: t('notifications.optDefault') },
    { value: EmailPreference.Email, label: t('notifications.optEmail') },
    { value: EmailPreference.DontEmail, label: t('notifications.optDontEmail') },
  ];

  const emailRow = (label: string, value: EmailPreference, onPick: (v: EmailPreference) => void) => (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <View style={styles.segment}>
        {options.map((o) => {
          const active = o.value === value;
          return (
            <TouchableOpacity
              key={o.value}
              style={[styles.segmentItem, active && styles.segmentItemActive]}
              onPress={() => !active && onPick(o.value)}
              accessibilityRole="button"
              accessibilityState={{ selected: active }}
            >
              <Text style={[styles.segmentText, active && styles.segmentTextActive]}>{o.label}</Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );

  return (
    <View style={styles.wrap}>
      <Text style={styles.sectionTitle}>{t('notifications.title')}</Text>
      {prefs.isLoading ? (
        <ActivityIndicator color={colors.brand} />
      ) : !p ? (
        <Text style={styles.muted}>{t('notifications.loadFailed')}</Text>
      ) : (
        <View style={styles.card}>
          <View style={styles.pushRow}>
            <View style={{ flex: 1 }}>
              <Text style={styles.rowLabel}>{t('notifications.push')}</Text>
              <Text style={styles.help}>{p.pushNotifications ? t('notifications.pushSend') : t('notifications.pushDontSend')}</Text>
            </View>
            <Switch value={p.pushNotifications} onValueChange={(v) => update({ pushNotifications: v })} />
          </View>

          <Text style={styles.groupTitle}>{t('notifications.emailTitle')}</Text>
          {emailRow(t('notifications.games'), p.gameEmails, (v) => update({ gameEmails: v }))}
          {emailRow(t('notifications.events'), p.eventEmails, (v) => update({ eventEmails: v }))}
          <Text style={styles.help}>{t('notifications.emailHelp')}</Text>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: spacing.xl },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  muted: { color: colors.subtext },
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.md,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.md,
  },
  pushRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  groupTitle: { fontSize: 15, fontWeight: '800', color: colors.text, marginTop: spacing.sm },
  row: { gap: spacing.xs },
  rowLabel: { fontSize: 15, fontWeight: '600', color: colors.text },
  help: { fontSize: 13, color: colors.subtext, lineHeight: 18 },
  segment: {
    flexDirection: 'row',
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    overflow: 'hidden',
  },
  segmentItem: { flex: 1, paddingVertical: spacing.sm, alignItems: 'center', backgroundColor: colors.card },
  segmentItemActive: { backgroundColor: colors.brand },
  segmentText: { fontSize: 13, fontWeight: '600', color: colors.text, textAlign: 'center' },
  segmentTextActive: { color: colors.white },
});
