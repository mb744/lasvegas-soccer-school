import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActionSheetIOS,
  ActivityIndicator,
  Alert,
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
import {
  blockChatUser,
  fetchChatBlocks,
  fetchChatMessages,
  markChatRead,
  reportChatMessage,
  sendChatMessage,
} from '../../src/api/endpoints';
import { onMessage, sendViaHub } from '../../src/chat/signalr';
import { useAuth } from '../../src/auth/AuthContext';
import type { BlockedUser, ChatGroup, ChatMessage, MediaItem } from '../../src/api/types';
import { messageTime } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';
import { pickMedia, uploadMedia } from '../../src/media/upload';
import { alertMediaError, askMediaSource, MediaThumb, MediaViewer, UploadProgress } from '../../src/media/MediaViews';

export default function ChatThreadScreen() {
  const { groupId: groupIdParam } = useLocalSearchParams<{ groupId: string }>();
  const groupId = Number(groupIdParam);
  const { t } = useTranslation();
  const { me } = useAuth();
  const qc = useQueryClient();
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [uploadFraction, setUploadFraction] = useState<number | null>(null);
  const [viewing, setViewing] = useState<MediaItem | null>(null);

  // Title from the cached group list (avoids an extra fetch).
  const title = useMemo(() => {
    const groups = qc.getQueryData<ChatGroup[]>(['chatGroups']);
    return groups?.find((g) => g.id === groupId)?.title ?? '';
  }, [qc, groupId]);

  const { data, isLoading } = useQuery({
    queryKey: ['chatMessages', groupId],
    queryFn: () => fetchChatMessages(groupId),
  });

  // Blocked-user list is cached app-wide so both the message-list filter and the live SignalR
  // filter can read from the same source. Server enforces on REST; this handles the live stream.
  const { data: blocks } = useQuery({
    queryKey: ['chatBlocks'],
    queryFn: fetchChatBlocks,
    staleTime: 60_000,
  });
  const blockedIds = useMemo(() => new Set((blocks ?? []).map((b: BlockedUser) => b.userId)), [blocks]);

  const messages = useMemo(
    () => (data ?? []).filter((m) => !blockedIds.has(m.senderUserId)),
    [data, blockedIds],
  );

  // Append live messages for this group to the cache (newest-first, deduped). Drop messages from
  // blocked senders so a mute is instant even before the next REST refresh.
  useEffect(
    () =>
      onMessage((msg) => {
        if (msg.groupId !== groupId) return;
        if (blockedIds.has(msg.senderUserId)) return;
        qc.setQueryData<ChatMessage[]>(['chatMessages', groupId], (old: ChatMessage[] | undefined) => {
          if (old?.some((m) => m.id === msg.id)) return old;
          return [msg, ...(old ?? [])];
        });
        void markChatRead(groupId, msg.id);
        void qc.invalidateQueries({ queryKey: ['chatGroups'] });
      }),
    [groupId, qc, blockedIds],
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

  // Photo/video: whatever's typed in the box goes along as the caption.
  const onAttach = useCallback(async () => {
    if (sending || uploadFraction !== null) return;
    try {
      const source = await askMediaSource(t);
      if (!source) return;
      const picked = await pickMedia(source);
      if (!picked) return;

      setUploadFraction(0);
      const media = await uploadMedia(picked, setUploadFraction);
      const caption = text.trim();
      const saved = await sendChatMessage(groupId, caption, media.mediaId);
      setText('');
      qc.setQueryData<ChatMessage[]>(['chatMessages', groupId], (old: ChatMessage[] | undefined) =>
        old?.some((m) => m.id === saved.id) ? old : [saved, ...(old ?? [])],
      );
    } catch (e) {
      alertMediaError(t, e);
    } finally {
      setUploadFraction(null);
    }
  }, [sending, uploadFraction, t, text, groupId, qc]);

  const onReport = useCallback(
    (message: ChatMessage) => {
      Alert.alert(
        t('chat.reportPromptTitle'),
        t('chat.reportPromptMessage'),
        [
          { text: t('common.cancel'), style: 'cancel' },
          {
            text: t('chat.reportSubmit'),
            style: 'destructive',
            onPress: async () => {
              try {
                await reportChatMessage(message.id);
                Alert.alert(t('chat.reportedTitle'), t('chat.reportedMessage'));
              } catch {
                // Silent — user can retry.
              }
            },
          },
        ],
        { cancelable: true },
      );
    },
    [t],
  );

  const onBlock = useCallback(
    (message: ChatMessage) => {
      Alert.alert(
        t('chat.blockPromptTitle'),
        t('chat.blockPromptMessage'),
        [
          { text: t('common.cancel'), style: 'cancel' },
          {
            text: t('chat.blockConfirm'),
            style: 'destructive',
            onPress: async () => {
              try {
                await blockChatUser(message.senderUserId);
                await qc.invalidateQueries({ queryKey: ['chatBlocks'] });
              } catch {
                // Silent — user can retry.
              }
            },
          },
        ],
        { cancelable: true },
      );
    },
    [t, qc],
  );

  const onLongPressMessage = useCallback(
    (message: ChatMessage) => {
      // Don't offer moderation actions on your own messages.
      if (message.senderUserId === me?.userId) return;

      const cancel = t('common.cancel');
      const report = t('chat.report');
      const block = t('chat.block');

      if (Platform.OS === 'ios') {
        ActionSheetIOS.showActionSheetWithOptions(
          {
            title: t('chat.messageActions'),
            options: [cancel, report, block],
            cancelButtonIndex: 0,
            destructiveButtonIndex: 2,
          },
          (index) => {
            if (index === 1) onReport(message);
            else if (index === 2) onBlock(message);
          },
        );
      } else {
        Alert.alert(
          t('chat.messageActions'),
          '',
          [
            { text: cancel, style: 'cancel' },
            { text: report, onPress: () => onReport(message) },
            { text: block, style: 'destructive', onPress: () => onBlock(message) },
          ],
          { cancelable: true },
        );
      }
    },
    [me?.userId, onBlock, onReport, t],
  );

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
          renderItem={({ item }) => (
            <Bubble
              message={item}
              mine={item.senderUserId === me?.userId}
              onLongPress={() => onLongPressMessage(item)}
              onOpenMedia={setViewing}
            />
          )}
        />
      )}

      {uploadFraction !== null ? <UploadProgress fraction={uploadFraction} label={t('media.uploading')} /> : null}

      <View style={styles.composer}>
        <TouchableOpacity
          style={[styles.attachBtn, uploadFraction !== null && styles.sendBtnDisabled]}
          onPress={onAttach}
          disabled={uploadFraction !== null}
          accessibilityLabel={t('media.attach')}
        >
          <Text style={styles.attachText}>＋</Text>
        </TouchableOpacity>
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
      <MediaViewer media={viewing} onClose={() => setViewing(null)} />
    </KeyboardAvoidingView>
  );
}

function Bubble({
  message,
  mine,
  onLongPress,
  onOpenMedia,
}: {
  message: ChatMessage;
  mine: boolean;
  onLongPress: () => void;
  onOpenMedia: (media: MediaItem) => void;
}) {
  return (
    <TouchableOpacity
      activeOpacity={0.8}
      onLongPress={onLongPress}
      delayLongPress={350}
      style={[styles.bubbleRow, mine ? styles.rowMine : styles.rowTheirs]}
    >
      <View style={[styles.bubble, mine ? styles.bubbleMine : styles.bubbleTheirs]}>
        {!mine ? (
          <Text style={[styles.sender, message.isFromAdmin && styles.senderAdmin]}>{message.senderName}</Text>
        ) : null}
        {message.media ? (
          <MediaThumb
            media={message.media}
            style={styles.bubbleMedia}
            onPress={() => onOpenMedia(message.media!)}
            onLongPress={onLongPress}
          />
        ) : null}
        {message.body ? <Text style={[styles.body, mine && styles.bodyMine]}>{message.body}</Text> : null}
        <Text style={[styles.time, mine && styles.timeMine]}>{messageTime(message.sentAt)}</Text>
      </View>
    </TouchableOpacity>
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
  attachBtn: {
    marginRight: spacing.sm,
    width: 42,
    height: 42,
    borderRadius: 21,
    backgroundColor: colors.bg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  attachText: { fontSize: 22, color: colors.brand, fontWeight: '800' },
  bubbleMedia: { width: 220, height: 220, marginBottom: 4 },
  sendText: { color: colors.white, fontWeight: '800' },
});
