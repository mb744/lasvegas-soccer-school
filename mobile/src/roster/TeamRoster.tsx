import React from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchRoster } from '../api/endpoints';
import { colors, radius, spacing } from '../theme';

export function jerseyLabel(numbers: string[]): string | null {
  return numbers.length === 0 ? null : numbers.map((n) => `#${n}`).join(' · ');
}

/** A team's players: full name with the jersey number underneath. Tap a player for their parents. */
export function TeamRoster({ teamId }: { teamId: number }) {
  const { t } = useTranslation();
  const router = useRouter();
  const { data, isLoading, isError, refetch, isRefetching } = useQuery({
    queryKey: ['roster', teamId],
    queryFn: () => fetchRoster(teamId),
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

  return (
    <FlatList
      style={styles.list}
      contentContainerStyle={{ padding: spacing.lg }}
      data={data.players}
      keyExtractor={(p) => String(p.playerId)}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
      ListHeaderComponent={
        <Text style={styles.count}>{t('roster.playerCount', { count: data.players.length })}</Text>
      }
      ListEmptyComponent={<Text style={styles.empty}>{t('roster.noPlayers')}</Text>}
      renderItem={({ item }) => {
        const jersey = jerseyLabel(item.jerseyNumbers);
        return (
          <TouchableOpacity
            style={styles.row}
            activeOpacity={0.8}
            onPress={() => router.push(`/roster/${teamId}/${item.playerId}`)}
          >
            <View style={{ flex: 1 }}>
              <Text style={styles.name}>{`${item.firstName} ${item.lastName}`.trim()}</Text>
              {jersey ? <Text style={styles.jersey}>{jersey}</Text> : null}
            </View>
            <Text style={styles.chevron}>›</Text>
          </TouchableOpacity>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  list: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  retry: { color: colors.brand, fontSize: 16, fontWeight: '700' },
  count: { fontSize: 13, fontWeight: '800', color: colors.subtext, textTransform: 'uppercase', marginBottom: spacing.sm },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    marginBottom: spacing.sm,
  },
  name: { fontSize: 16, fontWeight: '700', color: colors.text },
  jersey: { fontSize: 14, fontWeight: '800', color: colors.brand, marginTop: 2 },
  chevron: { fontSize: 24, color: colors.subtext, marginLeft: spacing.sm },
});
