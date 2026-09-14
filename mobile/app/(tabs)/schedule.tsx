import React, { useMemo } from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  SectionList,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchSchedule, setAttendance } from '../../src/api/endpoints';
import { AttendanceStatus, ScheduledEventKind, type EventPlayer, type ScheduleEvent } from '../../src/api/types';
import { dayKey, timeLabel } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function ScheduleScreen() {
  const { t } = useTranslation();
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: ['schedule'],
    queryFn: fetchSchedule,
  });

  const mutation = useMutation({
    mutationFn: (vars: { eventId: number; playerId: number; status: AttendanceStatus }) =>
      setAttendance(vars.eventId, vars.playerId, vars.status),
    // Optimistic: flip the chip immediately, roll back on error.
    onMutate: async (vars) => {
      await qc.cancelQueries({ queryKey: ['schedule'] });
      const prev = qc.getQueryData<ScheduleEvent[]>(['schedule']);
      qc.setQueryData<ScheduleEvent[]>(['schedule'], (old: ScheduleEvent[] | undefined) =>
        (old ?? []).map((ev) =>
          ev.id === vars.eventId
            ? {
                ...ev,
                players: ev.players.map((p) =>
                  p.playerId === vars.playerId ? { ...p, status: vars.status } : p,
                ),
              }
            : ev,
        ),
      );
      return { prev };
    },
    onError: (_e, _v, ctx) => {
      if (ctx?.prev) qc.setQueryData(['schedule'], ctx.prev);
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['schedule'] }),
  });

  const sections = useMemo(() => groupByDay(data ?? []), [data]);

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  if (isError) {
    return (
      <View style={styles.center}>
        <TouchableOpacity onPress={() => refetch()}>
          <Text style={styles.retry}>{t('common.retry')}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <SectionList
      style={styles.list}
      contentContainerStyle={{ padding: spacing.lg, paddingBottom: spacing.xl }}
      sections={sections}
      keyExtractor={(item) => String(item.id)}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
      renderSectionHeader={({ section }) => <Text style={styles.dayHeader}>{section.title}</Text>}
      ListEmptyComponent={<Text style={styles.empty}>{t('schedule.empty')}</Text>}
      renderItem={({ item }) => (
        <EventCard
          event={item}
          onSet={(playerId, status) => mutation.mutate({ eventId: item.id, playerId, status })}
        />
      )}
    />
  );
}

function EventCard({
  event,
  onSet,
}: {
  event: ScheduleEvent;
  onSet: (playerId: number, status: AttendanceStatus) => void;
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
      : event.summary || kindLabel;

  return (
    <View style={[styles.card, event.isCancelled && styles.cardCancelled]}>
      <View style={styles.cardHead}>
        <View style={[styles.kindBadge, badgeStyle(event.kind)]}>
          <Text style={styles.kindBadgeText}>{kindLabel}</Text>
        </View>
        <Text style={styles.time}>{timeLabel(event.startsAt)}</Text>
      </View>

      <Text style={styles.cardTitle}>{title}</Text>
      <Text style={styles.team}>{event.teamName}</Text>

      {event.isCancelled ? <Text style={styles.cancelled}>{t('schedule.cancelled')}</Text> : null}

      {event.venueName || event.location ? (
        <Text style={styles.meta}>📍 {event.venueName ?? event.location}</Text>
      ) : null}
      {event.arriveAt ? (
        <Text style={styles.meta}>⏰ {t('schedule.arrive')}: {timeLabel(event.arriveAt)}</Text>
      ) : null}
      {event.uniformName ? (
        <Text style={styles.meta}>👕 {t('schedule.uniform')}: {event.uniformName}</Text>
      ) : null}

      {!event.isCancelled &&
        event.players.map((p) => (
          <PlayerAttendance key={p.playerId} player={p} onSet={(s) => onSet(p.playerId, s)} />
        ))}
    </View>
  );
}

function PlayerAttendance({
  player,
  onSet,
}: {
  player: EventPlayer;
  onSet: (status: AttendanceStatus) => void;
}) {
  const { t } = useTranslation();
  const options: { status: AttendanceStatus; label: string; color: string }[] = [
    { status: AttendanceStatus.Confirmed, label: t('attendance.going'), color: colors.success },
    { status: AttendanceStatus.Maybe, label: t('attendance.maybe'), color: colors.warning },
    { status: AttendanceStatus.Declined, label: t('attendance.notGoing'), color: colors.danger },
  ];

  return (
    <View style={styles.attendanceRow}>
      <Text style={styles.playerName}>{player.firstName}</Text>
      <View style={styles.chips}>
        {options.map((opt) => {
          const active = player.status === opt.status;
          return (
            <TouchableOpacity
              key={opt.status}
              style={[styles.chip, active && { backgroundColor: opt.color, borderColor: opt.color }]}
              onPress={() => onSet(opt.status)}
            >
              <Text style={[styles.chipText, active && styles.chipTextActive]}>{opt.label}</Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );
}

function groupByDay(events: ScheduleEvent[]): { title: string; data: ScheduleEvent[] }[] {
  const map = new Map<string, ScheduleEvent[]>();
  for (const ev of events) {
    const key = dayKey(ev.startsAt);
    const arr = map.get(key) ?? [];
    arr.push(ev);
    map.set(key, arr);
  }
  return Array.from(map.entries()).map(([title, data]) => ({ title, data }));
}

function badgeStyle(kind: ScheduledEventKind) {
  if (kind === ScheduledEventKind.Practice) return { backgroundColor: colors.brandLight };
  if (kind === ScheduledEventKind.Miscellaneous) return { backgroundColor: colors.subtext };
  return { backgroundColor: colors.brand };
}

const styles = StyleSheet.create({
  list: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  retry: { color: colors.brand, fontSize: 16, fontWeight: '700' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
  dayHeader: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.lg,
    marginBottom: spacing.sm,
  },
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
  },
  cardCancelled: { opacity: 0.6 },
  cardHead: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  kindBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.sm },
  kindBadgeText: { color: colors.white, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  time: { fontSize: 15, fontWeight: '700', color: colors.text },
  cardTitle: { fontSize: 18, fontWeight: '800', color: colors.text, marginTop: spacing.sm },
  team: { fontSize: 14, color: colors.subtext, marginTop: 2 },
  cancelled: { color: colors.danger, fontWeight: '700', marginTop: spacing.xs },
  meta: { fontSize: 14, color: colors.subtext, marginTop: spacing.xs },
  attendanceRow: {
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  playerName: { fontSize: 15, fontWeight: '700', color: colors.text, marginBottom: spacing.sm },
  chips: { flexDirection: 'row', gap: spacing.sm },
  chip: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
  },
  chipText: { fontSize: 13, fontWeight: '700', color: colors.subtext },
  chipTextActive: { color: colors.white },
});
