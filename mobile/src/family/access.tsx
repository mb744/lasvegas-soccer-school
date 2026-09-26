import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import { AttendanceStatus, type EventPlayer } from '../api/types';
import { colors, radius, spacing } from '../theme';

/** True when this login is only a view-only family member (grandparent, friend) on the family the
 *  app shows. The server enforces the same rules; this just hides what they can't use. */
export function useIsFamilyViewer(): boolean {
  const { me } = useAuth();
  return me?.familyRole === 'viewer';
}

/** Returns whether this login can change things for a child (attendance, training login). A
 *  child missing from the profile, or an older server without the flag, counts as manageable. */
export function useCanManagePlayer(): (playerId: number) => boolean {
  const { me } = useAuth();
  return React.useCallback(
    (playerId: number) => me?.players.find((p) => p.id === playerId)?.canManage !== false,
    [me],
  );
}

/** A child's attendance for a view-only family member: the answer, without the buttons. */
export function ReadOnlyAttendance({ player, showName }: { player: EventPlayer; showName: boolean }) {
  const { t } = useTranslation();
  const [label, color] =
    player.status === AttendanceStatus.Confirmed ? [t('attendance.going'), colors.success]
    : player.status === AttendanceStatus.Maybe ? [t('attendance.maybe'), colors.warning]
    : player.status === AttendanceStatus.Declined ? [t('attendance.notGoing'), colors.danger]
    : [t('attendance.notAnswered'), colors.subtext];
  return (
    <View style={styles.row}>
      {showName ? <Text style={styles.name}>{player.firstName}</Text> : null}
      <View style={[styles.pill, { borderColor: color }]}>
        <Text style={[styles.pillText, { color }]}>{label}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginTop: spacing.xs },
  name: { fontSize: 13, fontWeight: '700', color: colors.text },
  pill: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.sm, paddingVertical: 2 },
  pillText: { fontSize: 12, fontWeight: '700' },
});
