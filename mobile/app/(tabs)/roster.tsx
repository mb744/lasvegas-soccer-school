import React from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchRosterTeams } from '../../src/api/endpoints';
import { TeamRoster } from '../../src/roster/TeamRoster';
import { colors, radius, spacing } from '../../src/theme';

/** Staff Roster tab: one team → its roster right here; several → pick a team first. */
export default function RosterTab() {
  const { t } = useTranslation();
  const router = useRouter();
  const { data, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: ['rosterTeams'],
    queryFn: fetchRosterTeams,
  });

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }
  if (isError || !data) {
    return (
      <View style={styles.center}>
        <TouchableOpacity onPress={() => refetch()}>
          <Text style={styles.retry}>{t('common.retry')}</Text>
        </TouchableOpacity>
      </View>
    );
  }
  if (data.length === 1) return <TeamRoster teamId={data[0].id} />;

  return (
    <FlatList
      style={styles.list}
      contentContainerStyle={{ padding: spacing.lg }}
      data={data}
      keyExtractor={(team) => String(team.id)}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
      ListEmptyComponent={<Text style={styles.empty}>{t('roster.noTeams')}</Text>}
      renderItem={({ item }) => (
        <TouchableOpacity style={styles.row} activeOpacity={0.8} onPress={() => router.push(`/roster/${item.id}`)}>
          <View style={{ flex: 1 }}>
            <Text style={styles.name}>{item.name}</Text>
            <Text style={styles.meta}>{t('roster.playerCount', { count: item.playerCount })}</Text>
          </View>
          <Text style={styles.chevron}>›</Text>
        </TouchableOpacity>
      )}
    />
  );
}

const styles = StyleSheet.create({
  list: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  retry: { color: colors.brand, fontSize: 16, fontWeight: '700' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15, paddingHorizontal: spacing.lg },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.lg,
    marginBottom: spacing.sm,
  },
  name: { fontSize: 16, fontWeight: '800', color: colors.text },
  meta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  chevron: { fontSize: 24, color: colors.subtext, marginLeft: spacing.sm },
});
