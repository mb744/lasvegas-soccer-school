import React from 'react';
import { ActivityIndicator, Alert, FlatList, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchChatGroups, fetchChatMembers, openDirectChat } from '../../../src/api/endpoints';
import type { ChatPerson } from '../../../src/api/types';
import { colors, radius, spacing } from '../../../src/theme';

/** Who's in a group chat. Tap anyone (parent, coach or admin) to message them privately. */
export default function ChatMembersScreen() {
  const { groupId: groupIdParam } = useLocalSearchParams<{ groupId: string }>();
  const groupId = Number(groupIdParam);
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();

  const members = useQuery({ queryKey: ['chatMembers', groupId], queryFn: () => fetchChatMembers(groupId) });

  const open = useMutation({
    mutationFn: (p: ChatPerson) => openDirectChat(p.userId, groupId),
    onSuccess: async (r) => {
      // The chat screen takes its title from the chat list, so load the new chat into it first.
      await qc.fetchQuery({ queryKey: ['chatGroups'], queryFn: fetchChatGroups });
      router.replace(`/chat/${r.groupId}`);
    },
    onError: (e) => {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      Alert.alert(t('chat.dmErrorTitle'), typeof data === 'string' && data ? data : t('chat.dmErrorBody'));
    },
  });

  const list = members.data ?? [];
  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('chat.membersTitle', { count: list.length }) }} />
      {members.isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          data={list}
          keyExtractor={(p) => p.userId}
          contentContainerStyle={{ padding: spacing.lg }}
          ListHeaderComponent={<Text style={styles.help}>{t('chat.membersHelp')}</Text>}
          ListEmptyComponent={<Text style={styles.empty}>{t('chat.membersLoadFailed')}</Text>}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.row}
              disabled={item.isYou || open.isPending}
              onPress={() => open.mutate(item)}
              accessibilityRole="button"
              accessibilityLabel={item.isYou ? item.name : t('chat.messagePerson', { name: item.name })}
            >
              <View style={[styles.avatar, (item.isAdmin || item.isCoach) && styles.avatarStaff]}>
                <Text style={styles.avatarText}>{item.name.charAt(0).toUpperCase()}</Text>
                {item.onApp ? <View style={styles.onAppDot} /> : null}
              </View>
              <View style={{ flex: 1 }}>
                <View style={styles.nameRow}>
                  <Text style={styles.name} numberOfLines={1}>{item.name}</Text>
                  {item.isYou ? <Tag label={t('chat.tagYou')} muted /> : null}
                  {item.isAdmin ? <Tag label={t('chat.tagAdmin')} /> : null}
                  {item.isCoach ? <Tag label={t('chat.tagCoach')} /> : null}
                </View>
                {item.detail ? <Text style={styles.meta}>{t('chat.parentOf', { kids: item.detail })}</Text> : null}
                <Text style={[styles.meta, item.onApp ? styles.onApp : null]}>
                  {item.onApp ? t('chat.onApp') : t('chat.notOnApp')}
                </Text>
              </View>
              {!item.isYou ? <Text style={styles.message}>{t('chat.message')}</Text> : null}
            </TouchableOpacity>
          )}
        />
      )}
    </View>
  );
}

function Tag({ label, muted }: { label: string; muted?: boolean }) {
  return (
    <View style={[styles.tag, muted && styles.tagMuted]}>
      <Text style={[styles.tagText, muted && styles.tagTextMuted]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  help: { fontSize: 13, color: colors.subtext, marginBottom: spacing.md, lineHeight: 18 },
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    gap: spacing.md,
  },
  avatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.brandLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarStaff: { backgroundColor: colors.brand },
  avatarText: { color: colors.white, fontSize: 16, fontWeight: '800' },
  onAppDot: {
    position: 'absolute',
    right: 0,
    bottom: 0,
    width: 12,
    height: 12,
    borderRadius: 6,
    backgroundColor: colors.success,
    borderWidth: 2,
    borderColor: colors.card,
  },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs, flexWrap: 'wrap' },
  name: { fontSize: 16, fontWeight: '700', color: colors.text, flexShrink: 1 },
  meta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  onApp: { color: colors.success },
  message: { fontSize: 14, fontWeight: '800', color: colors.brandLight },
  tag: { backgroundColor: colors.brand, borderRadius: 999, paddingHorizontal: 8, paddingVertical: 2 },
  tagMuted: { backgroundColor: colors.border },
  tagText: { color: colors.white, fontSize: 11, fontWeight: '800' },
  tagTextMuted: { color: colors.subtext },
});
