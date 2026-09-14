import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  KeyboardAvoidingView,
  Platform,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchChatMessages, markChatRead, sendChatMessage } from '../../src/api/endpoints';
import { onMessage, sendViaHub } from '../../src/chat/signalr';
import { useAuth } from '../../src/auth/AuthContext';
import type { ChatGroup, ChatMessage } from '../../src/api/types';
import { messageTime } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function ChatThreadScreen() {
  const { groupId: groupIdParam } = useLocalSearchParams<{ groupId: string }>();
  const groupId = Number(groupIdParam);
  const { t } = useTranslation();
  const { me } = useAuth();
  const qc = useQueryClient();
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);

  // Title from the cached group list (avoids an extra fetch).
  const title = useMemo(() => {
    const groups = qc.getQueryData<ChatGroup[]>(['chatGroups']);
    return groups?.find((g) => g.id === groupId)?.title ?? '';
  }, [qc, groupId]);

  const { data, isLoading } = useQuery({
    queryKey: ['chatMessages', groupId],
    queryFn: () => fetchChatMessages(groupId),
  });

  const messages = data ?? [];

  // Append live messages for this group to the cache (newest-first, deduped).
  useEffect(
    () =>
      onMessage((msg) => {
        if (msg.groupId !== groupId) return;
        qc.setQueryData<ChatMessage[]>(['chatMessages', groupId], (old: ChatMessage[] | undefined) => {
          if (old?.some((m) => m.id === msg.id)) return old;
          return [msg, ...(old ?? [])];
        });
        void markChatRead(groupId, msg.id);
        void qc.invalidateQueries({ queryKey: ['chatGroups'] });
      }),
    [groupId, qc],
  );

  // Mark the latest history message read on open.
  useEffect(() => {
    const newest = messages[0];
    if (newest) {
      void markChatRead(groupId, newest.id);
      void qc.invalidateQueries({ queryKey: ['chatGroups'] });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [messages.length === 0]);

  const onSend = useCallback(async () => {
    const body = text.trim();
    if (!body || sending) return;
    setSending(true);
    setText('');
    try {
      // Prefer the realtime hub; if the socket is down, send over REST. Either way the server
      // echoes the message back through the hub, so we don't optimistically insert here.
      try {
        await sendViaHub(groupId, body);
      } catch {
        const saved = await sendChatMessage(groupId, body);
        qc.setQueryData<ChatMessage[]>(['chatMessages', groupId], (old: ChatMessage[] | undefined) =>
          old?.some((m) => m.id === saved.id) ? old : [saved, ...(old ?? [])],
        );
      }
    } catch {
      setText(body); // restore on failure
    } finally {
      setSending(false);
    }
  }, [text, sending, groupId, qc]);

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 90 : 0}
    >
      <Stack.Screen options={{ title }} />
      {isLoading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      ) : (
        <FlatList
          style={styles.list}
          contentContainerStyle={{ padding: spacing.lg }}
          data={messages}
          inverted
          keyExtractor={(m) => String(m.id)}
          renderItem={({ item }) => <Bubble message={item} mine={item.senderUserId === me?.userId} />}
        />
      )}

      <View style={styles.composer}>
        <TextInput
          style={styles.input}
          placeholder={t('chat.placeholder')}
          placeholderTextColor={colors.subtext}
          value={text}
          onChangeText={setText}
          multiline
        />
        <TouchableOpacity
          style={[styles.sendBtn, (!text.trim() || sending) && styles.sendBtnDisabled]}
          onPress={onSend}
          disabled={!text.trim() || sending}
        >
          <Text style={styles.sendText}>{t('common.send')}</Text>
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  );
}

function Bubble({ message, mine }: { message: ChatMessage; mine: boolean }) {
  return (
    <View style={[styles.bubbleRow, mine ? styles.rowMine : styles.rowTheirs]}>
      <View style={[styles.bubble, mine ? styles.bubbleMine : styles.bubbleTheirs]}>
        {!mine ? (
          <Text style={[styles.sender, message.isFromAdmin && styles.senderAdmin]}>{message.senderName}</Text>
        ) : null}
        <Text style={[styles.body, mine && styles.bodyMine]}>{message.body}</Text>
        <Text style={[styles.time, mine && styles.timeMine]}>{messageTime(message.sentAt)}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  list: { flex: 1 },
  bubbleRow: { marginBottom: spacing.sm, flexDirection: 'row' },
  rowMine: { justifyContent: 'flex-end' },
  rowTheirs: { justifyContent: 'flex-start' },
  bubble: { maxWidth: '80%', borderRadius: radius.lg, padding: spacing.md },
  bubbleMine: { backgroundColor: colors.brand, borderBottomRightRadius: 4 },
  bubbleTheirs: { backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderBottomLeftRadius: 4 },
  sender: { fontSize: 12, fontWeight: '800', color: colors.brandLight, marginBottom: 2 },
  senderAdmin: { color: colors.accent },
  body: { fontSize: 15, color: colors.text },
  bodyMine: { color: colors.white },
  time: { fontSize: 10, color: colors.subtext, marginTop: 4, alignSelf: 'flex-end' },
  timeMine: { color: '#cfe0d8' },
  composer: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    padding: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    backgroundColor: colors.card,
  },
  input: {
    flex: 1,
    maxHeight: 120,
    backgroundColor: colors.bg,
    borderRadius: radius.lg,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    fontSize: 15,
    color: colors.text,
  },
  sendBtn: {
    marginLeft: spacing.sm,
    backgroundColor: colors.brand,
    borderRadius: radius.lg,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  sendBtnDisabled: { opacity: 0.5 },
  sendText: { color: colors.white, fontWeight: '800' },
});
