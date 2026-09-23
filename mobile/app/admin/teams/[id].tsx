import React from 'react';
import { ActivityIndicator, Linking, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchAdminTeamDetail } from '../../../src/api/endpoints';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminTeamDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const teamId = Number(id);

  const { data, isLoading } = useQuery({
    queryKey: ['adminTeamDetail', teamId],
    queryFn: () => fetchAdminTeamDetail(teamId),
    enabled: Number.isFinite(teamId),
  });

  if (isLoading || !data) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        {isLoading ? <ActivityIndicator size="large" color={colors.brand} /> : null}
      </View>
    );
  }

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: data.name }} />

      <Text style={styles.sectionTitle}>
        {t('admin.roster')} · {data.players.length}
      </Text>
      {data.players.length === 0 ? (
        <Text style={styles.empty}>{t('admin.noPlayers')}</Text>
      ) : (
        <View style={styles.card}>
          {data.players.map((p, i) => (
            <View
              key={p.id}
              style={[styles.playerRow, i === data.players.length - 1 && styles.playerRowLast]}
            >
              <View style={{ flex: 1 }}>
                <Text style={styles.playerName}>
                  {p.firstName} {p.lastName}
                </Text>
                {p.parentName ? <Text style={styles.playerMeta}>{p.parentName}</Text> : null}
              </View>
              {p.parentPhone ? (
                <TouchableOpacity onPress={() => Linking.openURL(`tel:${p.parentPhone}`)}>
                  <Text style={styles.callBtn}>📞</Text>
                </TouchableOpacity>
              ) : null}
            </View>
          ))}
        </View>
      )}

      <Text style={styles.sectionTitle}>
        {t('admin.coaches')} · {data.coaches.length}
      </Text>
      {data.coaches.length === 0 ? (
        <Text style={styles.empty}>{t('admin.noCoaches')}</Text>
      ) : (
        <View style={styles.card}>
          {data.coaches.map((c, i) => (
            <View
              key={c.id}
              style={[styles.playerRow, i === data.coaches.length - 1 && styles.playerRowLast]}
            >
              <View style={{ flex: 1 }}>
                <Text style={styles.playerName}>{c.name}</Text>
                <Text style={styles.playerMeta}>{c.role}</Text>
              </View>
              <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                {c.phone ? (
                  <TouchableOpacity onPress={() => Linking.openURL(`tel:${c.phone}`)}>
                    <Text style={styles.callBtn}>📞</Text>
                  </TouchableOpacity>
                ) : null}
                {c.email ? (
                  <TouchableOpacity onPress={() => Linking.openURL(`mailto:${c.email}`)}>
                    <Text style={styles.callBtn}>✉️</Text>
                  </TouchableOpacity>
                ) : null}
              </View>
            </View>
          ))}
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.lg,
    marginBottom: spacing.sm,
  },
  empty: { color: colors.subtext, fontSize: 14, marginBottom: spacing.sm },
  card: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
  },
  playerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  playerRowLast: { borderBottomWidth: 0 },
  playerName: { fontSize: 15, fontWeight: '700', color: colors.text },
  playerMeta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  callBtn: { fontSize: 20 },
});
