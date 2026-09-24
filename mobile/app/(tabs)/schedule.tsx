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
import { useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchSchedule, fetchStaffEventAttendance, setAttendance } from '../../src/api/endpoints';
import { AttendanceStatus, ScheduledEventKind, type EventPlayer, type ScheduleEvent, type StaffEventAttendance } from '../../src/api/types';
import { useAuth } from '../../src/auth/AuthContext';
import { dayKey, timeLabel } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

type Filter = 'all' | 'games' | 'practices';

export default function ScheduleScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();
  const { me } = useAuth();

  const { data, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: ['schedule'],
    queryFn: fetchSchedule,
  });

  // Refetch on tab focus so new/changed events from the admin side show up without waiting for
  // the default 30s stale window to elapse.
  useFocusEffect(
    React.useCallback(() => {
      void refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  // NOTE: no onSettled invalidation — the optimistic setQueryData already updates the UI; refetching
  // after every chip tap re-renders every card and makes the list visibly reflow under the tap.
  const mutation = useMutation({
    mutationFn: (vars: { eventId: number; playerId: number; status: AttendanceStatus }) =>
      setAttendance(vars.eventId, vars.playerId, vars.status),
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
  });

  const [filter, setFilter] = useState<Filter>('all');
  const filtered = useMemo(() => {
    const list = data ?? [];
    if (filter === 'all') return list;
    if (filter === 'games') return list.filter((e) => e.kind === ScheduledEventKind.Game);
    // 'practices' bucket includes school events too — anything that isn't a game.
    return list.filter((e) => e.kind !== ScheduledEventKind.Game);
  }, [data, filter]);

  // Same dedup as Home — a kid rostered on two teams that practice together shouldn't produce
  // two identical cards. Kind is part of the key so a game and a practice at the same slot for
  // the same player (rare but real — game and separate practice at the same time on different
  // teams) don't collapse into one card.
  const deduped = useMemo(() => {
    const seen = new Set<string>();
    return filtered.filter((e) => {
      const key = `${e.startsAt}|${e.kind}|${e.players.map((p) => p.playerId).sort().join(',')}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }, [filtered]);

  const sections = useMemo(() => groupByDay(deduped), [deduped]);

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

  const buttonPlacement: 'top' | 'bottom' | 'hidden' = useMemo(() => {
    if (jumpIndex < 0) return 'hidden';
    if (visibleSectionIdx.has(jumpIndex)) return 'hidden';
    if (visibleSectionIdx.size === 0) return 'hidden';
    const min = Math.min(...visibleSectionIdx);
    const max = Math.max(...visibleSectionIdx);
    if (jumpIndex < min) return 'top';
    if (jumpIndex > max) return 'bottom';
    return 'hidden';
  }, [jumpIndex, visibleSectionIdx]);

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
            isCoachOnly={item.players.length === 0 && !!me && (me.coachTeamIds ?? []).includes(item.teamId)}
            // Admins see rows across every team they're on and often want to know which team an
            // event belongs to at a glance. Parents already see their kid's name — no need for the team.
            showTeamName={!!me?.isAdmin}
            onPress={() => router.push(`/events/${item.id}`)}
            onSet={(playerId, status) => mutation.mutate({ eventId: item.id, playerId, status })}
          />
        )}
        onViewableItemsChanged={onViewableItemsChanged}
        viewabilityConfig={viewabilityConfig}
        onScrollToIndexFailed={(info) => {
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

/**
 * Row visual matches the Home screen's UpcomingEventRow: date block on the left, badge + time on
 * the right, then title (only when there's something beyond the kind label), player names comma-
 * joined, location, and inline attendance chips per player. Tapping the row navigates to the event
 * detail; chip taps consume the touch so they don't also navigate.
 */
function EventCard({
  event,
  isCoachOnly,
  showTeamName,
  onPress,
  onSet,
}: {
  event: ScheduleEvent;
  isCoachOnly: boolean;
  /** Render the team name row for viewers who span multiple teams (admins). Coach-only rows
   *  already surface the team as their card title, so this stays false for them. */
  showTeamName: boolean;
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

  const showTitle =
    (event.kind === ScheduledEventKind.Game && !!event.opponentName) ||
    (!!event.summary && event.summary !== kindLabel);
  const title = showTitle
    ? event.kind === ScheduledEventKind.Game && event.opponentName
      ? t('schedule.vs', { opponent: event.opponentName })
      : event.summary!
    : null;

  // Coach viewing their team's event with no kids on that team: no per-player chips, instead show
  // team-wide Going/Maybe/Not-going counts fetched from the staff endpoint. Only enabled for
  // coach-only rows so parent cards don't fire the extra request.
  const { data: staffCounts } = useQuery({
    queryKey: ['staff-event-attendance', event.id],
    queryFn: () => fetchStaffEventAttendance(event.id),
    enabled: isCoachOnly && !event.isCancelled,
    staleTime: 30_000,
  });

  return (
    <TouchableOpacity
      style={[styles.card, event.isCancelled && styles.cardCancelled]}
      onPress={onPress}
      activeOpacity={0.85}
    >
      <View style={styles.dateBlock}>
        <Text style={styles.dateDay}>{shortDay(event.startsAt)}</Text>
        <Text style={styles.dateNum}>{shortDayNum(event.startsAt)}</Text>
      </View>
      <View style={styles.cardBody}>
        <View style={styles.cardHeader}>
          <View style={[styles.kindBadge, badgeStyle(event.kind)]}>
            <Text style={styles.kindBadgeText}>{kindLabel}</Text>
          </View>
          {isCoachOnly ? (
            <View style={styles.coachBadge}>
              <Text style={styles.coachBadgeText}>{t('admin.coach')}</Text>
            </View>
          ) : null}
          <Text style={styles.time}>{timeLabel(event.startsAt)}</Text>
        </View>
        {isCoachOnly ? (
          <Text style={styles.title} numberOfLines={1}>
            {event.teamName}
          </Text>
        ) : null}
        {title ? (
          <Text style={styles.title} numberOfLines={2}>
            {title}
          </Text>
        ) : null}
        {showTeamName && !isCoachOnly ? (
          <Text style={styles.teamLine} numberOfLines={1}>
            {event.teamName}
          </Text>
        ) : null}
        {event.players.length > 0 ? (
          <Text style={styles.players} numberOfLines={1}>
            {event.players.map((p) => `${p.firstName} ${p.lastName}`.trim()).join(', ')}
          </Text>
        ) : null}
        {event.isCancelled ? <Text style={styles.cancelled}>{t('schedule.cancelled')}</Text> : null}
        {event.arriveAt ? (
          <Text style={styles.location} numberOfLines={1}>
            ⏰ {t('schedule.arrive')} {timeLabel(event.arriveAt)}
          </Text>
        ) : null}
        {event.venueName || event.location ? (
          <Text style={styles.location} numberOfLines={1}>
            📍 {event.venueName ?? event.location}
          </Text>
        ) : null}
        {!event.isCancelled && event.players.length > 0 ? (
          <View style={styles.attendanceBlock}>
            {event.players.map((p) => (
              <PlayerAttendance
                key={p.playerId}
                player={p}
                showName={event.players.length > 1}
                onSet={(s) => onSet(p.playerId, s)}
              />
            ))}
          </View>
        ) : null}
        {isCoachOnly && !event.isCancelled ? (
          <StaffCounts counts={staffCounts} />
        ) : null}
      </View>
    </TouchableOpacity>
  );
}

function StaffCounts({ counts }: { counts: StaffEventAttendance | undefined }) {
  const { t } = useTranslation();
  const items = [
    { label: t('attendance.going'), color: colors.success, count: counts?.going ?? 0 },
    { label: t('attendance.maybe'), color: colors.warning, count: counts?.maybe ?? 0 },
    { label: t('attendance.notGoing'), color: colors.danger, count: counts?.notGoing ?? 0 },
  ];
  return (
    <View style={styles.attendanceBlock}>
      <View style={styles.chips}>
        {items.map((it) => (
          <View
            key={it.label}
            style={[styles.chip, { backgroundColor: it.color, borderColor: it.color }]}
          >
            <Text style={[styles.chipText, styles.chipTextActive]}>
              {it.label} · {it.count}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}

function PlayerAttendance({
  player,
  showName,
  onSet,
}: {
  player: EventPlayer;
  showName: boolean;
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
      {showName ? <Text style={styles.attendanceName}>{player.firstName}</Text> : null}
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

function shortDay(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { weekday: 'short' }).toUpperCase();
}

function shortDayNum(iso: string): string {
  return String(new Date(iso).getDate());
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
    flexDirection: 'row',
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    marginBottom: spacing.sm,
    alignItems: 'stretch',
  },
  cardCancelled: { opacity: 0.6 },
  dateBlock: {
    width: 52,
    alignItems: 'center',
    justifyContent: 'center',
    borderRightWidth: 1,
    borderRightColor: colors.border,
    paddingRight: spacing.sm,
    marginRight: spacing.md,
  },
  dateDay: { fontSize: 11, fontWeight: '800', color: colors.subtext, letterSpacing: 1 },
  dateNum: { fontSize: 22, fontWeight: '800', color: colors.brand },
  cardBody: { flex: 1 },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  kindBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.sm },
  kindBadgeText: { color: colors.white, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  coachBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.brand,
  },
  coachBadgeText: { color: colors.brand, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  time: { fontSize: 13, fontWeight: '700', color: colors.text, marginLeft: 'auto' },
  title: { fontSize: 15, fontWeight: '700', color: colors.text, marginTop: spacing.xs },
  teamLine: { fontSize: 12, fontWeight: '700', color: colors.brand, marginTop: 2, textTransform: 'uppercase' },
  players: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  location: { fontSize: 12, color: colors.subtext, marginTop: 2 },
  cancelled: { color: colors.danger, fontWeight: '700', marginTop: spacing.xs, fontSize: 13 },

  attendanceBlock: {
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  attendanceRow: { marginTop: spacing.xs },
  attendanceName: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.subtext,
    marginBottom: 4,
    textTransform: 'uppercase',
  },
  chips: { flexDirection: 'row', gap: 6 },
  chip: {
    flex: 1,
    paddingVertical: 6,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
  },
  chipText: { fontSize: 11, fontWeight: '700', color: colors.subtext },
  chipTextActive: { color: colors.white },

  filterBar: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    paddingBottom: spacing.sm,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  filterRow: { flexDirection: 'row', gap: spacing.sm },
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
