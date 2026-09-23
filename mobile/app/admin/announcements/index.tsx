import React from 'react';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { deleteAnnouncement, fetchAdminAnnouncements } from '../../../src/api/endpoints';
import type { AdminAnnouncement } from '../../../src/api/types';
import { longDate } from '../../../src/format';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminAnnouncementsListScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();

  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['adminAnnouncements'],
    queryFn: fetchAdminAnnouncements,
  });

  useFocusEffect(
    React.useCallback(() => {
      void refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []),
  );

  const remove = (row: AdminAnnouncement) => {
    Alert.alert(
      t('admin.deleteConfirmTitle'),
      t('admin.deleteConfirmMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('admin.delete'),
          style: 'destructive',
          onPress: async () => {
            try {
              await deleteAnnouncement(row.id);
              await qc.invalidateQueries({ queryKey: ['adminAnnouncements'] });
              await qc.invalidateQueries({ queryKey: ['announcements'] });
            } catch { /* silent */ }
          },
        },
      ],
      { cancelable: true },
    );
  };

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('admin.hubAnnouncements') }} />

      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(a) => String(a.id)}
          contentContainerStyle={{ padding: spacing.lg, paddingBottom: 96 }}
          refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
          ListEmptyComponent={<Text style={styles.empty}>{t('admin.noAnnouncements')}</Text>}
          renderItem={({ item }) => (
            <AnnouncementRow
              row={item}
              onEdit={() => router.push(`/admin/announcements/${item.id}`)}
              onDelete={() => remove(item)}
            />
          )}
        />
      )}

      <TouchableOpacity
        style={styles.fab}
        onPress={() => router.push('/admin/announcements/new')}
        activeOpacity={0.85}
      >
        <Text style={styles.fabText}>+ {t('admin.newAnnouncement')}</Text>
      </TouchableOpacity>
    </View>
  );
}

function AnnouncementRow({
  row,
  onEdit,
  onDelete,
}: {
  row: AdminAnnouncement;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const { t } = useTranslation();
  const expired = row.endsAt ? new Date(row.endsAt) < new Date() : false;
  const audience = row.teamName ? t('admin.audienceTeam', { team: row.teamName }) : t('admin.audienceEveryone');

  return (
    <TouchableOpacity
      style={[styles.card, (!row.isActive || expired) && styles.cardMuted]}
      onPress={onEdit}
      onLongPress={onDelete}
      activeOpacity={0.85}
      delayLongPress={400}
    >
      <View style={styles.cardHeader}>
        <Text style={styles.cardTitle} numberOfLines={2}>
          {row.title}
        </Text>
        <View style={styles.cardBadges}>
          {!row.isActive ? (
            <View style={[styles.badge, styles.badgeMuted]}>
              <Text style={styles.badgeText}>{t('admin.hidden')}</Text>
            </View>
          ) : null}
          {expired ? (
            <View style={[styles.badge, styles.badgeExpired]}>
              <Text style={styles.badgeText}>{t('admin.expired')}</Text>
            </View>
          ) : null}
        </View>
      </View>
      <Text style={styles.cardBody} numberOfLines={3}>
        {row.body}
      </Text>
      <View style={styles.cardFooter}>
        <Text style={styles.cardMeta}>{audience}</Text>
        <Text style={styles.cardMeta}>{longDate(row.createdAt)}</Text>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },

  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderLeftWidth: 4,
    borderLeftColor: colors.accent,
    borderTopWidth: 1,
    borderRightWidth: 1,
    borderBottomWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    marginBottom: spacing.sm,
  },
  cardMuted: { opacity: 0.55 },
  cardHeader: { flexDirection: 'row', alignItems: 'flex-start' },
  cardTitle: { flex: 1, fontSize: 16, fontWeight: '800', color: colors.text, marginRight: spacing.sm },
  cardBadges: { flexDirection: 'row', gap: 4 },
  badge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.sm },
  badgeMuted: { backgroundColor: colors.subtext },
  badgeExpired: { backgroundColor: colors.warning },
  badgeText: { color: colors.white, fontSize: 10, fontWeight: '800', textTransform: 'uppercase' },
  cardBody: { fontSize: 14, color: colors.text, marginTop: spacing.xs, lineHeight: 20 },
  cardFooter: { flexDirection: 'row', justifyContent: 'space-between', marginTop: spacing.sm },
  cardMeta: { fontSize: 12, color: colors.subtext },

  fab: {
    position: 'absolute',
    left: spacing.lg,
    right: spacing.lg,
    bottom: spacing.lg,
    backgroundColor: colors.brand,
    borderRadius: 999,
    paddingVertical: spacing.md,
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.25,
    shadowRadius: 6,
    elevation: 6,
  },
  fabText: { color: colors.white, fontSize: 15, fontWeight: '800' },
});
