import React, { useEffect } from 'react';
import { ActivityIndicator, FlatList, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchChatGroups } from '../../src/api/endpoints';
import { onMessage } from '../../src/chat/signalr';
import type { ChatGroup } from '../../src/api/types';
import { messageTime } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function ChatListScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['chatGroups'],
    queryFn: fetchChatGroups,
  });

  // Refresh the group list (last message + unread badge) whenever a live message arrives.
  useEffect(() => onMessage(() => qc.invalidateQueries({ queryKey: ['chatGroups'] })), [qc]);

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  return (
    <FlatList
      style={styles.list}
      contentContainerStyle={{ padding: spacing.lg }}
      data={data ?? []}
      keyExtractor={(g) => String(g.id)}
      onRefresh={refetch}
      refreshing={false}
      ListEmptyComponent={<Text style={styles.empty}>{t('chat.empty')}</Text>}
      renderItem={({ item }) => <GroupRow group={item} onPress={() => router.push(`/chat/${item.id}`)} />}
    />
  );
}

function GroupRow({ group, onPress }: { group: ChatGroup; onPress: () => void }) {
  const preview = group.lastMessagePreview
    ? `${group.lastMessageSender ? `${group.lastMessageSender}: ` : ''}${group.lastMessagePreview}`
    : '';
  return (
    <TouchableOpacity style={styles.row} onPress={onPress}>
      <View style={styles.avatar}>
        <Text style={styles.avatarText}>{group.title.charAt(0).toUpperCase()}</Text>
      </View>
      <View style={styles.rowBody}>
        <View style={styles.rowTop}>
          <Text style={styles.title} numberOfLines={1}>
            {group.title}
          </Text>
          {group.lastMessageAt ? (
            <Text style={styles.when}>{messageTime(group.lastMessageAt)}</Text>
          ) : null}
        </View>
        <View style={styles.rowBottom}>
          <Text style={styles.preview} numberOfLines={1}>
            {preview}
          </Text>
          {group.unreadCount > 0 ? (
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{group.unreadCount}</Text>
            </View>
          ) : null}
        </View>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  list: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border,
  },
  avatar: {
    width: 46,
    height: 46,
    borderRadius: 23,
    backgroundColor: colors.brand,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.md,
  },
  avatarText: { color: colors.white, fontSize: 20, fontWeight: '800' },
  rowBody: { flex: 1 },
  rowTop: { flexDirection: 'row', justifyContent: 'space-between' },
  rowBottom: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 2 },
  title: { fontSize: 16, fontWeight: '800', color: colors.text, flex: 1 },
  when: { fontSize: 12, color: colors.subtext, marginLeft: spacing.sm },
  preview: { fontSize: 14, color: colors.subtext, flex: 1 },
  badge: {
    backgroundColor: colors.accent,
    minWidth: 22,
    height: 22,
    borderRadius: 11,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 6,
    marginLeft: spacing.sm,
  },
  badgeText: { color: colors.brand, fontSize: 12, fontWeight: '800' },
});
