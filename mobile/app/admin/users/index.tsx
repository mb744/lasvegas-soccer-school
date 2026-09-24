import React from 'react';
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchAdminUsers } from '../../../src/api/endpoints';
import type { AdminUserRow } from '../../../src/api/types';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminUsersListScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const [q, setQ] = React.useState('');

  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['adminUsers'],
    queryFn: fetchAdminUsers,
  });

  useFocusEffect(
    React.useCallback(() => {
      void refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  // Local case-insensitive filter across name + email. Cheap; 500 rows is the server-side cap.
  const filtered = React.useMemo(() => {
    const rows = data ?? [];
    const needle = q.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter((u) => {
      const hay = `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase();
      return hay.includes(needle);
    });
  }, [data, q]);

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('admin.hubUsers') }} />

      <View style={styles.searchWrap}>
        <TextInput
          style={styles.search}
          value={q}
          onChangeText={setQ}
          placeholder={t('admin.usersSearchPlaceholder')}
          placeholderTextColor={colors.subtext}
          autoCorrect={false}
          autoCapitalize="none"
        />
      </View>

      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          data={filtered}
          keyExtractor={(u) => u.id}
          contentContainerStyle={{ padding: spacing.lg, paddingBottom: spacing.xl }}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
          ListEmptyComponent={<Text style={styles.empty}>{t('admin.noUsers')}</Text>}
          renderItem={({ item }) => (
            <UserRow user={item} onPress={() => router.push(`/admin/users/${encodeURIComponent(item.id)}`)} />
          )}
        />
      )}
    </View>
  );
}

function UserRow({ user, onPress }: { user: AdminUserRow; onPress: () => void }) {
  const { t } = useTranslation();
  const name = `${user.firstName} ${user.lastName}`.trim() || user.email;
  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.85}>
      <View style={{ flex: 1 }}>
        <Text style={styles.name} numberOfLines={1}>
          {name}
        </Text>
        <Text style={styles.email} numberOfLines={1}>
          {user.email}
        </Text>
      </View>
      <View style={styles.badges}>
        {user.isAdmin ? <Text style={styles.adminBadge}>{t('admin.admin')}</Text> : null}
        {user.isCoach ? <Text style={styles.coachBadge}>{t('admin.coach')}</Text> : null}
        {user.isBanned ? <Text style={styles.bannedBadge}>{t('admin.banned')}</Text> : null}
      </View>
      <Text style={styles.chevron}>›</Text>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },

  searchWrap: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    paddingBottom: spacing.sm,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  search: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 15,
    color: colors.text,
  },

  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.xs,
    gap: spacing.sm,
  },
  name: { fontSize: 15, fontWeight: '800', color: colors.text },
  email: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  badges: { flexDirection: 'row', gap: 6, alignItems: 'center' },
  adminBadge: { color: colors.accent, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  coachBadge: { color: colors.brand, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  bannedBadge: { color: colors.danger, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  chevron: { fontSize: 22, color: colors.subtext, marginLeft: spacing.xs },
});
