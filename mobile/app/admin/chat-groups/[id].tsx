import React from 'react';
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
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchAdminChatGroups, postAdminChatMessage } from '../../../src/api/endpoints';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminChatGroupDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const groupId = Number(id);
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['adminChatGroups'],
    queryFn: fetchAdminChatGroups,
  });
  const group = React.useMemo(() => (data ?? []).find((g) => g.id === groupId), [data, groupId]);

  const [message, setMessage] = React.useState('');

  const postMut = useMutation({
    mutationFn: (body: string) => postAdminChatMessage(groupId, body),
    onSuccess: async () => {
      setMessage('');
      await qc.invalidateQueries({ queryKey: ['adminChatGroups'] });
      Alert.alert(t('admin.chatPostedTitle'), t('admin.chatPostedMessage'));
    },
    onError: () => {
      Alert.alert(t('admin.chatPostFailedTitle'), t('admin.chatPostFailedMessage'));
    },
  });

  const submit = () => {
    if (!message.trim()) return;
    postMut.mutate(message.trim());
  };

  if (isLoading || !group) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        {isLoading ? <ActivityIndicator size="large" color={colors.brand} /> : null}
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: group.title }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        <Text style={styles.sectionTitle}>
          {t('admin.postAsAdmin')}
        </Text>
        <TextInput
          style={styles.input}
          value={message}
          onChangeText={setMessage}
          multiline
          placeholder={t('admin.postAsAdminPlaceholder')}
          placeholderTextColor={colors.subtext}
          maxLength={4000}
        />
        <TouchableOpacity
          style={[styles.submit, (postMut.isPending || !message.trim()) && styles.submitDisabled]}
          onPress={submit}
          disabled={postMut.isPending || !message.trim()}
        >
          <Text style={styles.submitText}>{postMut.isPending ? t('admin.saving') : t('admin.postMessage')}</Text>
        </TouchableOpacity>

        <Text style={[styles.sectionTitle, { marginTop: spacing.xl }]}>
          {t('admin.members')} · {group.memberCount}
        </Text>
        <View style={styles.card}>
          {group.members.map((m, i) => (
            <View key={m.id} style={[styles.memberRow, i === group.members.length - 1 && styles.memberRowLast]}>
              <Text style={styles.memberName}>{m.displayName}</Text>
              {m.role === 1 ? <Text style={styles.adminBadge}>{t('admin.admin')}</Text> : null}
            </View>
          ))}
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
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
    marginBottom: spacing.sm,
  },
  input: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    color: colors.text,
    minHeight: 100,
    textAlignVertical: 'top',
  },
  submit: {
    marginTop: spacing.md,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  submitDisabled: { opacity: 0.5 },
  submitText: { color: colors.white, fontSize: 15, fontWeight: '800' },

  card: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  memberRowLast: { borderBottomWidth: 0 },
  memberName: { fontSize: 14, color: colors.text, flex: 1 },
  adminBadge: {
    color: colors.accent,
    fontSize: 11,
    fontWeight: '800',
    textTransform: 'uppercase',
  },
});
