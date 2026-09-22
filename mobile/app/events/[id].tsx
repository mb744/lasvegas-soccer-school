import React, { useCallback } from 'react';
import {
  ActivityIndicator,
  Alert,
  Linking,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { WebView } from 'react-native-webview';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchSchedule, setAttendance } from '../../src/api/endpoints';
import {
  AttendanceStatus,
  ScheduledEventKind,
  type EventPlayer,
  type ScheduleEvent,
} from '../../src/api/types';
import { longDate, timeLabel } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function EventDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const eventId = Number(id);
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['schedule'],
    queryFn: fetchSchedule,
  });
  const event = React.useMemo(() => (data ?? []).find((e) => e.id === eventId), [data, eventId]);

  // Optimistic mutation — no invalidate on settle so the parent screens don't reflow.
  const mutation = useMutation({
    mutationFn: (vars: { playerId: number; status: AttendanceStatus }) =>
      setAttendance(eventId, vars.playerId, vars.status),
    onMutate: async (vars) => {
      await qc.cancelQueries({ queryKey: ['schedule'] });
      const prev = qc.getQueryData<ScheduleEvent[]>(['schedule']);
      qc.setQueryData<ScheduleEvent[]>(['schedule'], (old: ScheduleEvent[] | undefined) =>
        (old ?? []).map((ev) =>
          ev.id === eventId
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

  if (isLoading || !event) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('event.title') }} />
        {isLoading ? <ActivityIndicator size="large" color={colors.brand} /> : null}
      </View>
    );
  }

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

  const detailRows = [
    event.kind === ScheduledEventKind.Game && typeof event.isHome === 'boolean'
      ? {
          label: t('event.homeAway'),
          value: event.isHome ? t('schedule.home') : t('schedule.away'),
        }
      : null,
    event.location ? { label: t('event.field'), value: event.location } : null,
    event.venueName ? { label: t('event.venue'), value: event.venueName } : null,
    event.venueAddress ? { label: t('event.address'), value: event.venueAddress } : null,
    event.uniformName ? { label: t('schedule.uniform'), value: event.uniformName } : null,
  ].filter(Boolean) as { label: string; value: string }[];

  const addressForMap =
    [event.venueName, event.venueAddress ?? event.location].filter(Boolean).join(', ') ||
    event.venueAddress ||
    event.location ||
    event.venueName ||
    '';

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: kindLabel }} />

      <View style={[styles.heroCard, event.isCancelled && styles.heroCardCancelled]}>
        <View style={[styles.kindBadge, badgeStyle(event.kind)]}>
          <Text style={styles.kindBadgeText}>{kindLabel}</Text>
        </View>
        <Text style={styles.date}>
          {longDate(event.startsAt)} · {timeLabel(event.startsAt)}
        </Text>
        {event.arriveAt ? (
          <Text style={styles.date}>
            {t('event.arrive')} · {timeLabel(event.arriveAt)}
          </Text>
        ) : null}
        {title ? <Text style={styles.title}>{title}</Text> : null}
        {event.isCancelled ? <Text style={styles.cancelled}>{t('schedule.cancelled')}</Text> : null}

        {!event.isCancelled && event.players.length > 0 ? (
          <View style={styles.heroAttendance}>
            {event.players.map((p) => (
              <PlayerAttendance
                key={p.playerId}
                player={p}
                showName={event.players.length > 1}
                onSet={(status) => mutation.mutate({ playerId: p.playerId, status })}
              />
            ))}
          </View>
        ) : null}
      </View>

      {detailRows.length > 0 ? (
        <View style={styles.card}>
          {detailRows.map((r, i) => (
            <Row key={r.label} label={r.label} value={r.value} isLast={i === detailRows.length - 1} />
          ))}
        </View>
      ) : null}

      {event.notes ? (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>{t('event.notes')}</Text>
          <Text style={styles.notesBody}>{event.notes}</Text>
        </View>
      ) : null}

      {addressForMap ? <LocationMap address={addressForMap} /> : null}
    </ScrollView>
  );
}

function Row({ label, value, isLast }: { label: string; value: string; isLast: boolean }) {
  return (
    <View style={[styles.row, isLast && styles.rowLast]}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function LocationMap({ address }: { address: string }) {
  const { t } = useTranslation();
  const encoded = encodeURIComponent(address);
  const embedUrl = `https://www.google.com/maps?q=${encoded}&z=15&output=embed`;

  const openMaps = useCallback(async () => {
    const url = `https://www.google.com/maps/search/?api=1&query=${encoded}`;
    const canOpen = await Linking.canOpenURL(url);
    if (!canOpen) {
      Alert.alert(t('event.mapUnavailableTitle'), t('event.mapUnavailableMessage'));
      return;
    }
    await Linking.openURL(url);
  }, [encoded, t]);

  return (
    <TouchableOpacity style={styles.mapCard} activeOpacity={0.85} onPress={openMaps}>
      <View style={styles.mapWrap} pointerEvents="none">
        <WebView
          source={{ uri: embedUrl }}
          style={styles.mapWebview}
          scrollEnabled={false}
          scalesPageToFit
          javaScriptEnabled
          domStorageEnabled
          startInLoadingState
          renderLoading={() => (
            <View style={styles.mapLoading}>
              <ActivityIndicator color={colors.brand} />
            </View>
          )}
          onError={() => {}}
        />
      </View>
      <View style={styles.openMapsBtn}>
        <Text style={styles.openMapsBtnText}>{t('event.openInMaps')} →</Text>
      </View>
    </TouchableOpacity>
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
      {showName ? <Text style={styles.playerName}>{player.firstName}</Text> : null}
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

function badgeStyle(kind: ScheduledEventKind) {
  if (kind === ScheduledEventKind.Practice) return { backgroundColor: colors.brandLight };
  if (kind === ScheduledEventKind.Miscellaneous) return { backgroundColor: colors.subtext };
  return { backgroundColor: colors.brand };
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },

  heroCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.lg,
  },
  heroCardCancelled: { opacity: 0.6 },
  kindBadge: {
    alignSelf: 'flex-start',
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
    marginBottom: spacing.sm,
  },
  kindBadgeText: { color: colors.white, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  date: { fontSize: 20, fontWeight: '800', color: colors.text, marginBottom: spacing.xs },
  title: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: spacing.xs },
  cancelled: { color: colors.danger, fontWeight: '800', marginTop: spacing.sm },

  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.lg,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  rowLast: { borderBottomWidth: 0 },
  rowLabel: { fontSize: 13, fontWeight: '700', color: colors.subtext, textTransform: 'uppercase' },
  rowValue: { fontSize: 15, color: colors.text, flexShrink: 1, textAlign: 'right', marginLeft: spacing.md },

  notesBody: { fontSize: 15, color: colors.text, lineHeight: 22 },

  mapCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    overflow: 'hidden',
    marginBottom: spacing.lg,
  },
  mapWrap: { height: 200, backgroundColor: colors.border },
  mapWebview: { flex: 1, backgroundColor: 'transparent' },
  mapLoading: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  openMapsBtn: {
    backgroundColor: colors.brand,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  openMapsBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },

  heroAttendance: {
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  attendanceRow: { marginTop: spacing.sm },
  playerName: { fontSize: 13, fontWeight: '700', color: colors.subtext, marginBottom: 4, textTransform: 'uppercase' },
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
