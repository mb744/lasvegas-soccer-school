import React, { useCallback } from 'react';
import {
  ActivityIndicator,
  Alert,
  Image,
  Linking,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import Constants from 'expo-constants';
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
    onSettled: () => qc.invalidateQueries({ queryKey: ['schedule'] }),
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

  const title =
    event.kind === ScheduledEventKind.Game && event.opponentName
      ? t('schedule.vs', { opponent: event.opponentName })
      : event.summary || kindLabel;

  const addressForMap =
    [event.venueName, event.location].filter(Boolean).join(', ') || event.location || event.venueName || '';

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: kindLabel }} />

      <View style={[styles.heroCard, event.isCancelled && styles.heroCardCancelled]}>
        <View style={[styles.kindBadge, badgeStyle(event.kind)]}>
          <Text style={styles.kindBadgeText}>{kindLabel}</Text>
        </View>
        <Text style={styles.title}>{title}</Text>
        <Text style={styles.team}>{event.teamName}</Text>
        {event.isCancelled ? <Text style={styles.cancelled}>{t('schedule.cancelled')}</Text> : null}
      </View>

      <View style={styles.card}>
        <Row label={t('event.when')} value={`${longDate(event.startsAt)} · ${timeLabel(event.startsAt)}`} />
        {event.arriveAt ? (
          <Row label={t('schedule.arrive')} value={timeLabel(event.arriveAt)} />
        ) : null}
        {event.uniformName ? <Row label={t('schedule.uniform')} value={event.uniformName} /> : null}
        {event.opponentName ? <Row label={t('event.opponent')} value={event.opponentName} /> : null}
      </View>

      {addressForMap ? <LocationCard address={addressForMap} venueName={event.venueName ?? null} /> : null}

      {!event.isCancelled && event.players.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>{t('event.attendance')}</Text>
          {event.players.map((p) => (
            <PlayerAttendance
              key={p.playerId}
              player={p}
              onSet={(status) => mutation.mutate({ playerId: p.playerId, status })}
            />
          ))}
        </View>
      ) : null}
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function LocationCard({ address, venueName }: { address: string; venueName: string | null }) {
  const { t } = useTranslation();
  const encoded = encodeURIComponent(address);
  const staticMapKey = ((Constants.expoConfig?.extra as Record<string, unknown> | undefined)?.googleMapsKey ?? '') as string;
  const staticMapUrl = staticMapKey
    ? `https://maps.googleapis.com/maps/api/staticmap?center=${encoded}&zoom=15&size=640x320&scale=2&markers=color:red|${encoded}&key=${staticMapKey}`
    : null;

  const openMaps = useCallback(async () => {
    // Universal Google Maps URL — Apple Maps handles it on iOS, Google Maps or default maps app on Android.
    const url = `https://www.google.com/maps/search/?api=1&query=${encoded}`;
    const canOpen = await Linking.canOpenURL(url);
    if (!canOpen) {
      Alert.alert(t('event.mapUnavailableTitle'), t('event.mapUnavailableMessage'));
      return;
    }
    await Linking.openURL(url);
  }, [encoded, t]);

  return (
    <TouchableOpacity style={styles.locationCard} activeOpacity={0.85} onPress={openMaps}>
      {staticMapUrl ? (
        <Image source={{ uri: staticMapUrl }} style={styles.mapImage} resizeMode="cover" />
      ) : null}
      <View style={styles.locationBody}>
        {venueName ? <Text style={styles.locationVenue}>{venueName}</Text> : null}
        <Text style={styles.locationAddress}>{address}</Text>
        <View style={styles.openMapsBtn}>
          <Text style={styles.openMapsBtnText}>{t('event.openInMaps')} →</Text>
        </View>
      </View>
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
  title: { fontSize: 22, fontWeight: '800', color: colors.text },
  team: { fontSize: 15, color: colors.subtext, marginTop: spacing.xs },
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
  rowLabel: { fontSize: 13, fontWeight: '700', color: colors.subtext, textTransform: 'uppercase' },
  rowValue: { fontSize: 15, color: colors.text, flexShrink: 1, textAlign: 'right', marginLeft: spacing.md },

  locationCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    overflow: 'hidden',
    marginBottom: spacing.lg,
  },
  mapImage: { width: '100%', height: 180, backgroundColor: colors.border },
  locationBody: { padding: spacing.lg },
  locationVenue: { fontSize: 16, fontWeight: '800', color: colors.text, marginBottom: 4 },
  locationAddress: { fontSize: 14, color: colors.subtext, marginBottom: spacing.md, lineHeight: 20 },
  openMapsBtn: {
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  openMapsBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },

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
