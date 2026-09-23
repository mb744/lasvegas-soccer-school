import React from 'react';
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchAdminChatGroups } from '../../../src/api/endpoints';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminChatGroupsListScreen() {
  const { t } = useTranslation();
  const router = useRouter();

  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['adminChatGroups'],
    queryFn: fetchAdminChatGroups,
  });

  useFocusEffect(
    React.useCallback(() => {
      void refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('admin.hubChatGroups') }} />
      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(g) => String(g.id)}
          contentContainerStyle={{ padding: spacing.lg }}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
          ListEmptyComponent={<Text style={styles.empty}>{t('admin.noChatGroups')}</Text>}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.row}
              onPress={() => router.push(`/admin/chat-groups/${item.id}`)}
              activeOpacity={0.85}
            >
              <View style={{ flex: 1 }}>
                <Text style={styles.rowName}>{item.title}</Text>
                {item.teamName ? <Text style={styles.rowMeta}>{item.teamName}</Text> : null}
                <Text style={styles.rowMeta}>
                  {t('admin.membersMessages', { members: item.memberCount, messages: item.messageCount })}
                </Text>
              </View>
              <Text style={styles.rowChevron}>›</Text>
            </TouchableOpacity>
          )}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
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
  rowName: { fontSize: 16, fontWeight: '700', color: colors.text },
  rowMeta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  rowChevron: { fontSize: 22, color: colors.subtext, marginLeft: spacing.sm },
});
