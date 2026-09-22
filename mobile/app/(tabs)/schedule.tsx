import React, { useCallback, useMemo, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Animated,
  Easing,
  RefreshControl,
  SectionList,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
  type ViewToken,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchSchedule, setAttendance } from '../../src/api/endpoints';
import { AttendanceStatus, ScheduledEventKind, type EventPlayer, type ScheduleEvent } from '../../src/api/types';
import { dayKey, timeLabel } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

type Filter = 'all' | 'games' | 'practices';

export default function ScheduleScreen() {
  const { t } = useTranslation();
  const router = useRouter();
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

  const [filter, setFilter] = useState<Filter>('all');
  const filtered = useMemo(() => {
    const list = data ?? [];
    if (filter === 'all') return list;
    if (filter === 'games') return list.filter((e) => e.kind === ScheduledEventKind.Game);
    // 'practices' bucket includes school events too — anything that isn't a game.
    return list.filter((e) => e.kind !== ScheduledEventKind.Game);
  }, [data, filter]);

  const sections = useMemo(() => groupByDay(filtered), [filtered]);

  // Index of the first section that starts today or later — that's what "jump to today" targets.
  // Falls back to -1 (button hidden) when every event is in the past.
  const jumpIndex = useMemo(() => {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    return sections.findIndex((s) => {
      const first = s.data[0];
      return !!first && new Date(first.startsAt) >= startOfToday;
    });
  }, [sections]);

  const listRef = useRef<SectionList<ScheduleEvent, { title: string; data: ScheduleEvent[] }>>(null);
  const [visibleSectionIdx, setVisibleSectionIdx] = useState<Set<number>>(new Set());

  const viewabilityConfig = useMemo(() => ({ itemVisiblePercentThreshold: 30 }), []);
  const onViewableItemsChanged = useRef(({ viewableItems }: { viewableItems: ViewToken[] }) => {
    const idxs = new Set<number>();
    for (const it of viewableItems) if (typeof it.section?.index === 'number') idxs.add(it.section.index as number);
    setVisibleSectionIdx(idxs);
  }).current;

  // Where the floating button lives (or should hide) based on today's position vs. what's visible.
  const buttonPlacement: 'top' | 'bottom' | 'hidden' = useMemo(() => {
    if (jumpIndex < 0) return 'hidden';
    if (visibleSectionIdx.has(jumpIndex)) return 'hidden';
    // Empty visible set can happen briefly during layout — hide until we know.
    if (visibleSectionIdx.size === 0) return 'hidden';
    const min = Math.min(...visibleSectionIdx);
    const max = Math.max(...visibleSectionIdx);
    if (jumpIndex < min) return 'top';
    if (jumpIndex > max) return 'bottom';
    return 'hidden';
  }, [jumpIndex, visibleSectionIdx]);

  // Fade the button in/out so it doesn't pop.
  const buttonOpacity = useRef(new Animated.Value(0)).current;
  const shouldShowButton = buttonPlacement !== 'hidden';
  React.useEffect(() => {
    Animated.timing(buttonOpacity, {
      toValue: shouldShowButton ? 1 : 0,
      duration: 180,
      easing: Easing.out(Easing.quad),
      useNativeDriver: true,
    }).start();
  }, [shouldShowButton, buttonOpacity]);

  const scrollToToday = useCallback(() => {
    if (jumpIndex < 0) return;
    listRef.current?.scrollToLocation({
      sectionIndex: jumpIndex,
      itemIndex: 0,
      animated: true,
      viewOffset: 8,
    });
  }, [jumpIndex]);

  const buttonLabel =
    jumpIndex >= 0 && sections[jumpIndex]
      ? sameDay(new Date(sections[jumpIndex].data[0].startsAt), new Date())
        ? t('schedule.jumpToday')
        : t('schedule.jumpNext')
      : t('schedule.jumpToday');

  const filterRow = (
    <View style={styles.filterRow}>
      {(['all', 'games', 'practices'] as Filter[]).map((f) => (
        <TouchableOpacity
          key={f}
          onPress={() => setFilter(f)}
          style={[styles.filterPill, filter === f && styles.filterPillActive]}
        >
          <Text style={[styles.filterPillText, filter === f && styles.filterPillTextActive]}>
            {t(`schedule.filter.${f}` as const)}
          </Text>
        </TouchableOpacity>
      ))}
    </View>
  );

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
    <View style={styles.wrapper}>
      <View style={styles.filterBar}>{filterRow}</View>
      <SectionList
        ref={listRef}
        style={styles.list}
        contentContainerStyle={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.xl }}
        sections={sections.map((s, index) => ({ ...s, index }))}
        keyExtractor={(item) => String(item.id)}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
        renderSectionHeader={({ section }) => <Text style={styles.dayHeader}>{section.title}</Text>}
        ListEmptyComponent={<Text style={styles.empty}>{t('schedule.empty')}</Text>}
        renderItem={({ item }) => (
          <EventCard
            event={item}
            onPress={() => router.push(`/events/${item.id}`)}
            onSet={(playerId, status) => mutation.mutate({ eventId: item.id, playerId, status })}
          />
        )}
        onViewableItemsChanged={onViewableItemsChanged}
        viewabilityConfig={viewabilityConfig}
        onScrollToIndexFailed={(info) => {
          // scrollToLocation on a section not yet measured — retry after a beat.
          const wait = new Promise((resolve) => setTimeout(resolve, 200));
          void wait.then(() =>
            listRef.current?.scrollToLocation({
              sectionIndex: info.index,
              itemIndex: 0,
              animated: true,
              viewOffset: 8,
            }),
          );
        }}
      />

      {shouldShowButton && (
        <Animated.View
          pointerEvents="box-none"
          style={[
            styles.jumpWrap,
            buttonPlacement === 'top' ? styles.jumpTop : styles.jumpBottom,
            { opacity: buttonOpacity },
          ]}
        >
          <TouchableOpacity style={styles.jumpBtn} onPress={scrollToToday} activeOpacity={0.85}>
            <Text style={styles.jumpBtnArrow}>{buttonPlacement === 'top' ? '↑' : '↓'}</Text>
            <Text style={styles.jumpBtnText}>{buttonLabel}</Text>
          </TouchableOpacity>
        </Animated.View>
      )}
    </View>
  );
}

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function EventCard({
  event,
  onPress,
  onSet,
}: {
  event: ScheduleEvent;
  onPress: () => void;
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
    <TouchableOpacity
      activeOpacity={0.85}
      onPress={onPress}
      style={[styles.card, event.isCancelled && styles.cardCancelled]}
    >
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
    </TouchableOpacity>
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
  wrapper: { flex: 1, backgroundColor: colors.bg },
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

  filterBar: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    paddingBottom: spacing.sm,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  filterRow: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  filterPill: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.card,
    alignItems: 'center',
  },
  filterPillActive: { backgroundColor: colors.brand, borderColor: colors.brand },
  filterPillText: { fontSize: 13, fontWeight: '800', color: colors.subtext },
  filterPillTextActive: { color: colors.white },

  jumpWrap: {
    position: 'absolute',
    left: 0,
    right: 0,
    alignItems: 'center',
  },
  jumpTop: { top: spacing.lg + spacing.xl },
  jumpBottom: { bottom: spacing.xl + spacing.md },
  jumpBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.brand,
    borderRadius: 999,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.25,
    shadowRadius: 6,
    elevation: 6,
  },
  jumpBtnArrow: { color: colors.white, fontSize: 16, fontWeight: '800', marginRight: 6 },
  jumpBtnText: { color: colors.white, fontSize: 14, fontWeight: '800' },
});
