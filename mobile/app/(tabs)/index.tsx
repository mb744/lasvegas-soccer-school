import React from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchAnnouncements, fetchInvoices, fetchSchedule, setAttendance } from '../../src/api/endpoints';
import {
  AttendanceStatus,
  InvoiceStatus,
  ScheduledEventKind,
  type Announcement,
  type EventPlayer,
  type InvoiceSummary,
  type ScheduleEvent,
} from '../../src/api/types';
import { useAuth } from '../../src/auth/AuthContext';
import { ReadOnlyAttendance, useCanManagePlayer } from '../../src/family/access';
import { dueDateLabel, longDate, money, timeLabel } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

/**
 * Home landing screen. Stacks (top-down): greeting, outstanding-invoice card, active
 * announcements, upcoming events. All three data sources are read via React Query with a shared
 * pull-to-refresh gesture on the outer scroll view.
 */
export default function HomeScreen() {
  const { t } = useTranslation();
  const { me } = useAuth();
  const router = useRouter();
  const qc = useQueryClient();

  const invoices = useQuery({ queryKey: ['invoices'], queryFn: fetchInvoices });
  const announcements = useQuery({ queryKey: ['announcements'], queryFn: fetchAnnouncements });
  const schedule = useQuery({ queryKey: ['schedule'], queryFn: fetchSchedule });

  // Refetch on tab focus so admin-side additions (new game, cancelled practice) show up as soon
  // as the parent switches back to Home instead of only after the 30s stale window elapses.
  useFocusEffect(
    React.useCallback(() => {
      void invoices.refetch();
      void announcements.refetch();
      void schedule.refetch();
      // Intentionally empty deps — refetch fns are stable and we want to fire once per focus.
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  // Same mutation shape the Schedule tab + Event detail use — sharing the ['schedule'] key means
  // an attendance change from any of the three screens updates all three instantly. NOTE: we
  // intentionally do NOT invalidate on settle — the optimistic update in onMutate already puts
  // the right state on screen, and invalidating would trigger a background refetch that re-renders
  // every card in the list, causing visible reflow / scroll drift after each tap. The next
  // natural refetch (pull-to-refresh, tab re-focus) picks up any server-side drift.
  const attendanceMutation = useMutation({
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

  const outstanding = React.useMemo(
    () =>
      (invoices.data ?? []).find(
        (i) => i.status === InvoiceStatus.New || i.status === InvoiceStatus.Sent,
      ),
    [invoices.data],
  );

  const upcoming = React.useMemo(() => {
    // Anchor to start-of-today rather than "right now" so a game/practice earlier in the day
    // doesn't silently drop off Home the moment its clock time passes — parents expect to see
    // today's events all day, matching the Schedule tab's day-grouped view.
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const future = (schedule.data ?? [])
      .filter((e) => new Date(e.startsAt) >= startOfToday)
      .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());
    // Same practice can show up twice on the schedule when a kid is rostered on two teams that
    // run their practices together — collapse those into a single card, keyed on start time +
    // kind + the player set so genuinely-different events at the same time (two siblings on two
    // teams, or a game and a practice at the same slot) still both surface.
    const seen = new Set<string>();
    return future.filter((e) => {
      const key = `${e.startsAt}|${e.kind}|${e.players.map((p) => p.playerId).sort().join(',')}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }, [schedule.data]);

  const anyRefetching = invoices.isRefetching || announcements.isRefetching || schedule.isRefetching;
  const onRefresh = () => {
    void invoices.refetch();
    void announcements.refetch();
    void schedule.refetch();
  };

  const greeting = t('home.greeting', { name: me?.firstName || '' });

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={{ padding: spacing.lg }}
      refreshControl={<RefreshControl refreshing={anyRefetching} onRefresh={onRefresh} tintColor={colors.brand} />}
    >
      <Text style={styles.greeting}>{greeting}</Text>

      {invoices.isLoading ? (
        <>
          <Text style={styles.sectionTitle}>{t('home.outstandingTitle')}</Text>
          <View style={styles.loadingCard}>
            <ActivityIndicator color={colors.brand} />
          </View>
        </>
      ) : (invoices.data ?? []).length === 0 ? null : (
        <>
          <Text style={styles.sectionTitle}>{t('home.outstandingTitle')}</Text>
          {outstanding ? (
            <OutstandingInvoiceCard invoice={outstanding} onPress={() => router.push(`/invoices/${outstanding.id}`)} />
          ) : (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyCardText}>{t('home.noOutstanding')}</Text>
            </View>
          )}
          <TouchableOpacity style={styles.viewAllBtn} onPress={() => router.push('/invoices')}>
            <Text style={styles.viewAllBtnText}>{t('home.viewAll')}</Text>
          </TouchableOpacity>
        </>
      )}

      {(announcements.data ?? []).length > 0 && (
        <>
          <Text style={styles.sectionTitle}>{t('home.announcementsTitle')}</Text>
          {(announcements.data ?? []).map((a) => (
            <AnnouncementCard key={a.id} announcement={a} />
          ))}
        </>
      )}

      <Text style={styles.sectionTitle}>{t('home.upcomingTitle')}</Text>
      {schedule.isLoading ? (
        <View style={styles.loadingCard}>
          <ActivityIndicator color={colors.brand} />
        </View>
      ) : upcoming.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyCardText}>{t('home.upcomingEmpty')}</Text>
        </View>
      ) : (
        upcoming.map((ev) => (
          <UpcomingEventRow
            key={ev.id}
            event={ev}
            // Admins scan across teams — surfacing the team keeps rows disambiguable.
            showTeamName={!!me?.isAdmin}
            onPress={() => router.push(`/events/${ev.id}`)}
            onSetAttendance={(playerId, status) =>
              attendanceMutation.mutate({ eventId: ev.id, playerId, status })
            }
          />
        ))
      )}
    </ScrollView>
  );
}

function OutstandingInvoiceCard({ invoice, onPress }: { invoice: InvoiceSummary; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <TouchableOpacity style={styles.invoiceCard} onPress={onPress} activeOpacity={0.8}>
      <View style={styles.invoiceCardHeader}>
        <Text style={styles.invoiceAmount}>{money(invoice.amount, invoice.currency)}</Text>
        <View style={styles.dueBadge}>
          <Text style={styles.dueBadgeText}>
            {invoice.dueDate ? t('invoices.due', { date: dueDateLabel(invoice.dueDate) }) : t('invoices.noDueDate')}
          </Text>
        </View>
      </View>
      <Text style={styles.invoiceDescription} numberOfLines={2}>
        {invoice.description}
      </Text>
      {invoice.playerName ? <Text style={styles.invoiceMeta}>{invoice.playerName}</Text> : null}
      <View style={styles.invoiceCta}>
        <Text style={styles.invoiceCtaText}>{t('home.seeInvoice')} →</Text>
      </View>
    </TouchableOpacity>
  );
}

function AnnouncementCard({ announcement }: { announcement: Announcement }) {
  return (
    <View style={styles.announcementCard}>
      <Text style={styles.announcementTitle}>{announcement.title}</Text>
      <Text style={styles.announcementBody}>{announcement.body}</Text>
      <Text style={styles.announcementWhen}>{longDate(announcement.createdAt)}</Text>
    </View>
  );
}

function UpcomingEventRow({
  event,
  showTeamName,
  onPress,
  onSetAttendance,
}: {
  event: ScheduleEvent;
  /** Render a small "team X" line under the title so admin viewers can tell cross-team rows apart. */
  showTeamName: boolean;
  onPress: () => void;
  onSetAttendance: (playerId: number, status: AttendanceStatus) => void;
}) {
  const { t } = useTranslation();
  const canManage = useCanManagePlayer();
  const kindLabel =
    event.kind === ScheduledEventKind.Practice
      ? t('schedule.practice')
      : event.kind === ScheduledEventKind.Miscellaneous
        ? t('schedule.event')
        : t('schedule.game');
  // Only surface a title line when there's something meaningful beyond the kind badge — for a
  // plain practice with no admin-typed summary the badge already says "Practice", so a redundant
  // "Practice" title just wastes vertical space.
  const showTitle =
    (event.kind === ScheduledEventKind.Game && !!event.opponentName) ||
    (!!event.summary && event.summary !== kindLabel);
  const title = showTitle
    ? event.kind === ScheduledEventKind.Game && event.opponentName
      ? t('schedule.vs', { opponent: event.opponentName })
      : event.summary!
    : null;

  return (
    <TouchableOpacity style={styles.upcomingRow} onPress={onPress} activeOpacity={0.85}>
      <View style={styles.upcomingDate}>
        <Text style={styles.upcomingDateDay}>{shortDay(event.startsAt)}</Text>
        <Text style={styles.upcomingDateNum}>{shortDayNum(event.startsAt)}</Text>
      </View>
      <View style={styles.upcomingBody}>
        <View style={styles.upcomingHeader}>
          <View style={[styles.upcomingKind, badgeColor(event.kind)]}>
            <Text style={styles.upcomingKindText}>{kindLabel}</Text>
          </View>
          <Text style={styles.upcomingTime}>{timeLabel(event.startsAt)}</Text>
        </View>
        {title ? (
          <Text style={styles.upcomingTitle} numberOfLines={2}>
            {title}
          </Text>
        ) : null}
        {showTeamName ? (
          <Text style={styles.upcomingTeamName} numberOfLines={1}>
            {event.teamName}
          </Text>
        ) : null}
        {event.players.length > 0 ? (
          <Text style={styles.upcomingTeam} numberOfLines={1}>
            {event.players.map((p) => `${p.firstName} ${p.lastName}`.trim()).join(', ')}
          </Text>
        ) : null}
        {event.arriveAt ? (
          <Text style={styles.upcomingLocation} numberOfLines={1}>
            ⏰ {t('schedule.arrive')} {timeLabel(event.arriveAt)}
          </Text>
        ) : null}
        {event.venueName || event.location ? (
          <Text style={styles.upcomingLocation} numberOfLines={1}>
            📍 {event.venueName ?? event.location}
          </Text>
        ) : null}
        {!event.isCancelled && event.players.length > 0 ? (
          <View style={styles.upcomingAttendance}>
            {event.players.map((p) =>
              canManage(p.playerId) ? (
                <UpcomingAttendance
                  key={p.playerId}
                  player={p}
                  showName={event.players.length > 1}
                  onSet={(status) => onSetAttendance(p.playerId, status)}
                />
              ) : (
                <ReadOnlyAttendance key={p.playerId} player={p} showName={event.players.length > 1} />
              ),
            )}
          </View>
        ) : null}
      </View>
    </TouchableOpacity>
  );
}

function UpcomingAttendance({
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
    <View style={styles.upcomingAttendanceRow}>
      {showName ? <Text style={styles.upcomingAttendanceName}>{player.firstName}</Text> : null}
      <View style={styles.upcomingChips}>
        {options.map((opt) => {
          const active = player.status === opt.status;
          return (
            <TouchableOpacity
              key={opt.status}
              style={[styles.upcomingChip, active && { backgroundColor: opt.color, borderColor: opt.color }]}
              onPress={() => onSet(opt.status)}
            >
              <Text style={[styles.upcomingChipText, active && styles.upcomingChipTextActive]}>{opt.label}</Text>
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

function badgeColor(kind: ScheduledEventKind) {
  if (kind === ScheduledEventKind.Practice) return { backgroundColor: colors.brandLight };
  if (kind === ScheduledEventKind.Miscellaneous) return { backgroundColor: colors.subtext };
  return { backgroundColor: colors.brand };
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  greeting: { fontSize: 24, fontWeight: '800', color: colors.text, marginBottom: spacing.lg },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.lg,
    marginBottom: spacing.sm,
  },
  loadingCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  emptyCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  emptyCardText: { color: colors.subtext, fontSize: 15 },

  invoiceCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.brand,
    marginBottom: spacing.md,
  },
  invoiceCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  invoiceAmount: { fontSize: 28, fontWeight: '800', color: colors.brand },
  dueBadge: {
    backgroundColor: colors.danger,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
  },
  dueBadgeText: { color: colors.white, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  invoiceDescription: { fontSize: 16, fontWeight: '700', color: colors.text, marginBottom: spacing.xs },
  invoiceMeta: { fontSize: 14, color: colors.subtext, marginBottom: spacing.sm },
  invoiceCta: {
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    alignItems: 'flex-end',
  },
  invoiceCtaText: { color: colors.brand, fontSize: 14, fontWeight: '800' },

  viewAllBtn: {
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  viewAllBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },

  announcementCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderLeftWidth: 4,
    borderLeftColor: colors.accent,
    borderTopWidth: 1,
    borderRightWidth: 1,
    borderBottomWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    marginBottom: spacing.sm,
  },
  announcementTitle: { fontSize: 16, fontWeight: '800', color: colors.text },
  announcementBody: { fontSize: 14, color: colors.text, marginTop: spacing.xs, lineHeight: 20 },
  announcementWhen: { fontSize: 12, color: colors.subtext, marginTop: spacing.sm },

  upcomingRow: {
    flexDirection: 'row',
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    marginBottom: spacing.sm,
    alignItems: 'stretch',
  },
  upcomingDate: {
    width: 52,
    alignItems: 'center',
    justifyContent: 'center',
    borderRightWidth: 1,
    borderRightColor: colors.border,
    paddingRight: spacing.sm,
    marginRight: spacing.md,
  },
  upcomingDateDay: { fontSize: 11, fontWeight: '800', color: colors.subtext, letterSpacing: 1 },
  upcomingDateNum: { fontSize: 22, fontWeight: '800', color: colors.brand },
  upcomingBody: { flex: 1 },
  upcomingHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  upcomingKind: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.sm },
  upcomingKindText: { color: colors.white, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  upcomingTime: { fontSize: 13, fontWeight: '700', color: colors.text, marginLeft: 'auto' },
  upcomingTitle: { fontSize: 15, fontWeight: '700', color: colors.text, marginTop: spacing.xs },
  upcomingTeam: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  upcomingTeamName: { fontSize: 12, fontWeight: '700', color: colors.brand, marginTop: 2, textTransform: 'uppercase' },
  upcomingLocation: { fontSize: 12, color: colors.subtext, marginTop: 2 },

  upcomingAttendance: {
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  upcomingAttendanceRow: { marginTop: spacing.xs },
  upcomingAttendanceName: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.subtext,
    marginBottom: 4,
    textTransform: 'uppercase',
  },
  upcomingChips: { flexDirection: 'row', gap: 6 },
  upcomingChip: {
    flex: 1,
    paddingVertical: 6,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
  },
  upcomingChipText: { fontSize: 11, fontWeight: '700', color: colors.subtext },
  upcomingChipTextActive: { color: colors.white },
});
