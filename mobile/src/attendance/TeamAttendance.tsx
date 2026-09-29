import React from 'react';
import { FlatList, Modal, Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { AttendanceStatus, type StaffEventAttendance } from '../api/types';
import { colors, radius, spacing } from '../theme';

type Bucket = { status: AttendanceStatus; label: string; color: string; count: number };

/**
 * Staff-only team attendance: four tappable counts (Going / Maybe / Not going / No reply). Tapping
 * one opens the players with that answer, with tabs to switch between answers.
 */
export function TeamAttendance({ counts }: { counts: StaffEventAttendance }) {
  const { t } = useTranslation();
  const [open, setOpen] = React.useState<AttendanceStatus | null>(null);

  const buckets: Bucket[] = [
    { status: AttendanceStatus.Confirmed, label: t('attendance.going'), color: colors.success, count: counts.going },
    { status: AttendanceStatus.Maybe, label: t('attendance.maybe'), color: colors.warning, count: counts.maybe },
    { status: AttendanceStatus.Declined, label: t('attendance.notGoing'), color: colors.danger, count: counts.notGoing },
    { status: AttendanceStatus.Pending, label: t('attendance.noReply'), color: colors.subtext, count: counts.pending },
  ];

  return (
    <View>
      <Text style={styles.heading}>{t('attendance.team', { count: counts.rosterSize })}</Text>
      <View style={styles.tiles}>
        {buckets.map((b) => (
          <TouchableOpacity
            key={b.status}
            style={[styles.tile, { borderColor: b.color }]}
            activeOpacity={0.8}
            onPress={() => setOpen(b.status)}
            accessibilityLabel={`${b.label}: ${b.count}`}
          >
            <Text style={[styles.tileCount, { color: b.color }]}>{b.count}</Text>
            <Text style={styles.tileLabel} numberOfLines={1}>{b.label}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Modal
        visible={open !== null}
        animationType="slide"
        presentationStyle={Platform.OS === 'ios' ? 'pageSheet' : undefined}
        onRequestClose={() => setOpen(null)}
      >
        {open !== null ? (
          <PlayerList counts={counts} buckets={buckets} selected={open} onSelect={setOpen} onClose={() => setOpen(null)} />
        ) : null}
      </Modal>
    </View>
  );
}

function PlayerList({
  counts, buckets, selected, onSelect, onClose,
}: {
  counts: StaffEventAttendance;
  buckets: Bucket[];
  selected: AttendanceStatus;
  onSelect: (s: AttendanceStatus) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const players = (counts.players ?? []).filter((p) => p.status === selected);
  const current = buckets.find((b) => b.status === selected)!;

  return (
    <View style={styles.sheet}>
      <View style={styles.sheetHead}>
        <Text style={styles.sheetTitle}>{t('attendance.teamTitle')}</Text>
        <TouchableOpacity onPress={onClose} accessibilityLabel={t('common.close')}>
          <Text style={styles.done}>{t('common.done')}</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.tabs}>
        {buckets.map((b) => {
          const active = b.status === selected;
          return (
            <TouchableOpacity
              key={b.status}
              style={[styles.tab, active && { backgroundColor: b.color, borderColor: b.color }]}
              onPress={() => onSelect(b.status)}
            >
              <Text style={[styles.tabText, active && styles.tabTextActive]} numberOfLines={1}>
                {b.label} · {b.count}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>

      <FlatList
        data={players}
        keyExtractor={(p) => String(p.playerId)}
        contentContainerStyle={{ padding: spacing.lg }}
        ListEmptyComponent={
          <Text style={styles.empty}>
            {counts.players ? t('attendance.nobody', { status: current.label }) : t('attendance.listUnavailable')}
          </Text>
        }
        renderItem={({ item }) => (
          <View style={styles.playerRow}>
            <View style={[styles.dot, { backgroundColor: current.color }]} />
            <Text style={styles.playerName}>{`${item.firstName} ${item.lastName}`.trim()}</Text>
          </View>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  heading: { fontSize: 12, fontWeight: '800', color: colors.subtext, textTransform: 'uppercase', marginBottom: spacing.xs },
  tiles: { flexDirection: 'row', gap: 6 },
  tile: {
    flex: 1,
    borderWidth: 1.5,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    backgroundColor: colors.card,
  },
  tileCount: { fontSize: 20, fontWeight: '800' },
  tileLabel: { fontSize: 11, fontWeight: '700', color: colors.subtext, marginTop: 1 },

  sheet: { flex: 1, backgroundColor: colors.bg },
  sheetHead: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    paddingBottom: spacing.sm,
  },
  sheetTitle: { fontSize: 18, fontWeight: '800', color: colors.text },
  done: { fontSize: 16, fontWeight: '800', color: colors.brand },
  tabs: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, paddingHorizontal: spacing.lg },
  tab: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 999,
    paddingVertical: 6,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.card,
  },
  tabText: { fontSize: 13, fontWeight: '700', color: colors.subtext },
  tabTextActive: { color: colors.white },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
  playerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    marginBottom: spacing.xs,
  },
  dot: { width: 10, height: 10, borderRadius: 5, marginRight: spacing.md },
  playerName: { fontSize: 16, fontWeight: '600', color: colors.text },
});
