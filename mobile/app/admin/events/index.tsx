import React from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cancelAdminEvent, fetchAdminEvents, uncancelAdminEvent } from '../../../src/api/endpoints';
import type { AdminEvent } from '../../../src/api/types';
import { ScheduledEventKind } from '../../../src/api/types';
import { longDate, timeLabel } from '../../../src/format';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminEventsListScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();

  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['adminEvents'],
    queryFn: () => fetchAdminEvents(),
  });

  useFocusEffect(
    React.useCallback(() => {
      void refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  const cancelMut = useMutation({
    mutationFn: (id: number) => cancelAdminEvent(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminEvents'] }),
  });
  const uncancelMut = useMutation({
    mutationFn: (id: number) => uncancelAdminEvent(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminEvents'] }),
  });

  const confirmCancel = (ev: AdminEvent) => {
    Alert.alert(
      ev.isCancelled ? t('admin.uncancelConfirmTitle') : t('admin.cancelConfirmTitle'),
      ev.isCancelled ? t('admin.uncancelConfirmMessage') : t('admin.cancelConfirmMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: ev.isCancelled ? t('admin.uncancelConfirm') : t('admin.cancelConfirm'),
          style: 'destructive',
          onPress: () => (ev.isCancelled ? uncancelMut.mutate(ev.id) : cancelMut.mutate(ev.id)),
        },
      ],
      { cancelable: true },
    );
  };

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('admin.hubEvents') }} />
      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(e) => String(e.id)}
          contentContainerStyle={{ padding: spacing.lg, paddingBottom: 96 }}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
          ListEmptyComponent={<Text style={styles.empty}>{t('admin.noEvents')}</Text>}
          renderItem={({ item }) => (
            <EventRow
              event={item}
              onPress={() => router.push(`/admin/events/${item.id}`)}
              onToggleCancel={() => confirmCancel(item)}
            />
          )}
        />
      )}

      <TouchableOpacity
        style={styles.fab}
        onPress={() => router.push('/admin/events/new')}
        activeOpacity={0.85}
      >
        <Text style={styles.fabText}>+ {t('admin.newEvent')}</Text>
      </TouchableOpacity>
    </View>
  );
}

function EventRow({
  event,
  onPress,
  onToggleCancel,
}: {
  event: AdminEvent;
  onPress: () => void;
  onToggleCancel: () => void;
}) {
  const { t } = useTranslation();
  const kindLabel =
    event.kind === ScheduledEventKind.Practice
      ? t('schedule.practice')
      : event.kind === ScheduledEventKind.Miscellaneous
        ? t('schedule.event')
        : t('schedule.game');
  const title =
    event.kind === ScheduledEventKind.Game && event.opponentName
      ? t('schedule.vs', { opponent: event.opponentName })
      : kindLabel;

  return (
    <TouchableOpacity
      style={[styles.card, event.isCancelled && styles.cardCancelled]}
      onPress={onPress}
      activeOpacity={0.85}
    >
      <View style={styles.cardHead}>
        <View style={[styles.kindBadge, badgeStyle(event.kind)]}>
          <Text style={styles.kindBadgeText}>{kindLabel}</Text>
        </View>
        <Text style={styles.time}>
          {longDate(event.startsAt)} · {timeLabel(event.startsAt)}
        </Text>
      </View>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.team}>{event.teamName}</Text>
      {event.venueName || event.location ? (
        <Text style={styles.meta}>📍 {event.venueName ?? event.location}</Text>
      ) : null}
      {event.isCancelled ? <Text style={styles.cancelled}>{t('schedule.cancelled')}</Text> : null}
      <TouchableOpacity
        style={[styles.actionBtn, event.isCancelled && styles.actionBtnUncancel]}
        onPress={(e) => { e.stopPropagation?.(); onToggleCancel(); }}
      >
        <Text style={styles.actionBtnText}>
          {event.isCancelled ? t('admin.uncancelEvent') : t('admin.cancelEvent')}
        </Text>
      </TouchableOpacity>
    </TouchableOpacity>
  );
}

function badgeStyle(kind: ScheduledEventKind) {
  if (kind === ScheduledEventKind.Practice) return { backgroundColor: colors.brandLight };
  if (kind === ScheduledEventKind.Miscellaneous) return { backgroundColor: colors.subtext };
  return { backgroundColor: colors.brand };
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },

  card: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.sm,
  },
  cardCancelled: { opacity: 0.55 },
  cardHead: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.xs },
  kindBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.sm },
  kindBadgeText: { color: colors.white, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  time: { fontSize: 13, fontWeight: '700', color: colors.text, marginLeft: 'auto' },
  title: { fontSize: 15, fontWeight: '800', color: colors.text },
  team: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  meta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  cancelled: { color: colors.danger, fontWeight: '800', marginTop: spacing.xs, fontSize: 13 },
  actionBtn: {
    marginTop: spacing.md,
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
  },
  actionBtnUncancel: { borderColor: colors.brand },
  actionBtnText: { color: colors.text, fontSize: 13, fontWeight: '800', textTransform: 'uppercase' },

  fab: {
    position: 'absolute',
    left: spacing.lg,
    right: spacing.lg,
    bottom: spacing.lg,
    backgroundColor: colors.brand,
    borderRadius: 999,
    paddingVertical: spacing.md,
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.25,
    shadowRadius: 6,
    elevation: 6,
  },
  fabText: { color: colors.white, fontSize: 15, fontWeight: '800' },
});
