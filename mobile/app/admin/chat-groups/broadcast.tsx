import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { broadcastChatMessage, fetchAdminChatGroups } from '../../../src/api/endpoints';
import { colors, radius, spacing } from '../../../src/theme';

/** Admin → Chat groups → "Message all groups": one message into every group (or the ticked ones).
 *  Each parent gets one notification even if they're in several of the groups. */
export default function ChatBroadcastScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();
  const groups = useQuery({ queryKey: ['adminChatGroups'], queryFn: fetchAdminChatGroups });
  const [body, setBody] = useState('');
  const [selected, setSelected] = useState<Set<number>>(new Set());

  // Everything ticked once the groups load.
  useEffect(() => {
    if (groups.data) setSelected(new Set(groups.data.map((g) => g.id)));
  }, [groups.data]);

  const list = groups.data ?? [];
  const allSelected = list.length > 0 && selected.size === list.length;

  const send = useMutation({
    // An empty list means "every group".
    mutationFn: () => broadcastChatMessage(body.trim(), allSelected ? [] : [...selected]),
    onSuccess: (r) => {
      void qc.invalidateQueries({ queryKey: ['adminChatGroups'] });
      Alert.alert(t('admin.broadcastSentTitle'), t('admin.broadcastSentBody', { groups: r.groups, people: r.people }), [
        { text: t('common.ok'), onPress: () => router.back() },
      ]);
    },
    onError: (e) => {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      Alert.alert(t('admin.broadcastErrorTitle'), typeof data === 'string' && data ? data : t('admin.broadcastErrorBody'));
    },
  });

  const confirmSend = () => {
    Alert.alert(
      t('admin.broadcastConfirmTitle'),
      t('admin.broadcastConfirmBody', { count: selected.size }),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('admin.broadcastSend'), onPress: () => send.mutate() },
      ],
    );
  };

  const toggle = (id: number) => {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelected(next);
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: t('admin.broadcastTitle') }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg, paddingBottom: spacing.xl * 2 }} keyboardShouldPersistTaps="handled">
        <Text style={styles.help}>{t('admin.broadcastHelp')}</Text>
        <TextInput
          style={styles.input}
          value={body}
          onChangeText={setBody}
          placeholder={t('admin.broadcastPlaceholder')}
          placeholderTextColor={colors.subtext}
          multiline
          maxLength={4000}
          textAlignVertical="top"
        />

        <View style={styles.groupsHeader}>
          <Text style={styles.groupsTitle}>{t('admin.broadcastGroupsCount', { selected: selected.size, total: list.length })}</Text>
          {list.length > 0 && (
            <TouchableOpacity onPress={() => setSelected(allSelected ? new Set() : new Set(list.map((g) => g.id)))}>
              <Text style={styles.link}>{allSelected ? t('admin.broadcastSelectNone') : t('admin.broadcastSelectAll')}</Text>
            </TouchableOpacity>
          )}
        </View>

        {groups.isLoading ? (
          <ActivityIndicator color={colors.brand} />
        ) : (
          list.map((g) => {
            const on = selected.has(g.id);
            return (
              <TouchableOpacity
                key={g.id}
                style={styles.groupRow}
                onPress={() => toggle(g.id)}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: on }}
              >
                <View style={[styles.check, on && styles.checkOn]}>{on ? <Text style={styles.checkMark}>✓</Text> : null}</View>
                <Text style={styles.groupName} numberOfLines={1}>{g.title}</Text>
                <Text style={styles.groupMeta}>{g.memberCount}</Text>
              </TouchableOpacity>
            );
          })
        )}

        <TouchableOpacity
          style={[styles.send, (!body.trim() || selected.size === 0 || send.isPending) && styles.sendDisabled]}
          onPress={confirmSend}
          disabled={!body.trim() || selected.size === 0 || send.isPending}
        >
          {send.isPending ? <ActivityIndicator color={colors.white} /> : <Text style={styles.sendText}>{t('admin.broadcastSend')}</Text>}
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  help: { fontSize: 14, color: colors.subtext, marginBottom: spacing.md, lineHeight: 20 },
  input: {
    minHeight: 120,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text,
  },
  groupsHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: spacing.xl,
    marginBottom: spacing.sm,
  },
  groupsTitle: { fontSize: 13, fontWeight: '800', color: colors.subtext, textTransform: 'uppercase' },
  link: { fontSize: 14, fontWeight: '700', color: colors.brandLight },
  groupRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.md,
    marginBottom: spacing.xs,
    gap: spacing.md,
  },
  check: {
    width: 22,
    height: 22,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  checkOn: { backgroundColor: colors.brand, borderColor: colors.brand },
  checkMark: { color: colors.white, fontSize: 14, fontWeight: '800' },
  groupName: { flex: 1, fontSize: 15, fontWeight: '600', color: colors.text },
  groupMeta: { fontSize: 13, color: colors.subtext },
  send: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  sendDisabled: { opacity: 0.5 },
  sendText: { color: colors.white, fontSize: 16, fontWeight: '800' },
});
