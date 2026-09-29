import React from 'react';
import { ActivityIndicator, Linking, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchRosterPlayer } from '../../../src/api/endpoints';
import { Language, type RosterGuardian } from '../../../src/api/types';
import { jerseyLabel } from '../../../src/roster/TeamRoster';
import { colors, radius, spacing } from '../../../src/theme';

/** One player's parents/guardians with contact details. Phone and email open the dialer / mail app. */
export default function RosterPlayerScreen() {
  const { t } = useTranslation();
  const { teamId, playerId } = useLocalSearchParams<{ teamId: string; playerId: string }>();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['rosterPlayer', Number(teamId), Number(playerId)],
    queryFn: () => fetchRosterPlayer(Number(teamId), Number(playerId)),
  });

  const name = data ? `${data.firstName} ${data.lastName}`.trim() : '';

  if (isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }
  if (isError || !data) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        <TouchableOpacity onPress={() => refetch()}>
          <Text style={styles.retry}>{t('common.retry')}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  const jersey = jerseyLabel(data.jerseyNumbers);

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: name }} />
      <View style={styles.hero}>
        <Text style={styles.playerName}>{name}</Text>
        {jersey ? <Text style={styles.jersey}>{jersey}</Text> : null}
        <Text style={styles.team}>{data.teamName}</Text>
      </View>

      <Text style={styles.sectionTitle}>{t('roster.parents')}</Text>
      {data.guardians.length === 0 ? (
        <Text style={styles.empty}>{t('roster.noParents')}</Text>
      ) : (
        data.guardians.map((g, i) => <GuardianCard key={i} guardian={g} />)
      )}
    </ScrollView>
  );
}

function GuardianCard({ guardian }: { guardian: RosterGuardian }) {
  const { t } = useTranslation();
  return (
    <View style={styles.card}>
      <View style={styles.cardHead}>
        <Text style={styles.guardianName}>{guardian.name || '—'}</Text>
        {guardian.isPrimary ? <Text style={styles.primary}>{t('roster.primary')}</Text> : null}
      </View>

      <ContactRow
        label={t('roster.phone')}
        value={guardian.phone}
        onPress={guardian.phone ? () => void Linking.openURL(`tel:${guardian.phone}`) : undefined}
      />
      <ContactRow
        label={t('roster.email')}
        value={guardian.email}
        onPress={guardian.email ? () => void Linking.openURL(`mailto:${guardian.email}`) : undefined}
      />
      <ContactRow
        label={t('roster.language')}
        value={guardian.language === Language.Spanish ? t('roster.spanish') : t('roster.english')}
      />
    </View>
  );
}

function ContactRow({ label, value, onPress }: { label: string; value: string | null; onPress?: () => void }) {
  return (
    <View style={styles.contactRow}>
      <Text style={styles.contactLabel}>{label}</Text>
      {onPress ? (
        <TouchableOpacity onPress={onPress} style={{ flexShrink: 1 }}>
          <Text style={[styles.contactValue, styles.link]}>{value}</Text>
        </TouchableOpacity>
      ) : (
        <Text style={[styles.contactValue, !value && styles.missing]}>{value ?? '—'}</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  retry: { color: colors.brand, fontSize: 16, fontWeight: '700' },
  hero: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.lg,
  },
  playerName: { fontSize: 22, fontWeight: '800', color: colors.text },
  jersey: { fontSize: 18, fontWeight: '800', color: colors.brand, marginTop: 2 },
  team: { fontSize: 13, color: colors.subtext, marginTop: spacing.xs },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  empty: { fontSize: 15, color: colors.subtext },
  card: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.sm,
  },
  cardHead: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: spacing.xs },
  guardianName: { fontSize: 16, fontWeight: '800', color: colors.text, flexShrink: 1 },
  primary: { fontSize: 11, fontWeight: '800', color: colors.brand, textTransform: 'uppercase', marginLeft: spacing.sm },
  contactRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  contactLabel: { fontSize: 13, fontWeight: '700', color: colors.subtext, textTransform: 'uppercase', marginRight: spacing.md },
  contactValue: { fontSize: 15, color: colors.text, textAlign: 'right' },
  link: { color: colors.brand, fontWeight: '700' },
  missing: { color: colors.subtext },
});
