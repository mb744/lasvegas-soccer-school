import React from 'react';
import {
  ActionSheetIOS,
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
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addChatGroupMember,
  createChatGroup,
  deleteChatGroup,
  fetchAdminChatGroups,
  fetchAdminTeams,
  postAdminChatMessage,
  removeChatGroupMember,
  searchChatParents,
  updateChatGroup,
} from '../../../src/api/endpoints';
import type { AdminChatParentSearch } from '../../../src/api/endpoints';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminChatGroupDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const isNew = id === 'new';
  const numericId = isNew ? null : Number(id);
  const router = useRouter();
  const qc = useQueryClient();

  const groups = useQuery({ queryKey: ['adminChatGroups'], queryFn: fetchAdminChatGroups });
  const group = React.useMemo(
    () => (isNew ? null : (groups.data ?? []).find((g) => g.id === numericId) ?? null),
    [groups.data, isNew, numericId],
  );

  const teams = useQuery({ queryKey: ['adminTeams'], queryFn: fetchAdminTeams, enabled: isNew });

  const [title, setTitle] = React.useState('');
  const [seedTeamId, setSeedTeamId] = React.useState<number | null>(null);
  const [message, setMessage] = React.useState('');
  const [showAddMember, setShowAddMember] = React.useState(false);
  const [titleSeeded, setTitleSeeded] = React.useState(false);

  React.useEffect(() => {
    if (isNew || titleSeeded || !group) return;
    setTitle(group.title);
    setTitleSeeded(true);
  }, [group, isNew, titleSeeded]);

  const saveGroup = useMutation({
    mutationFn: async () => {
      if (isNew) {
        const g = await createChatGroup({ title: title.trim(), seedFromTeamId: seedTeamId });
        return g.id;
      }
      await updateChatGroup(numericId!, { title: title.trim(), seedFromTeamId: null });
      return numericId!;
    },
    onSuccess: async (savedId) => {
      await qc.invalidateQueries({ queryKey: ['adminChatGroups'] });
      if (isNew) router.replace(`/admin/chat-groups/${savedId}`);
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.saveFailed')),
  });

  const delGroup = useMutation({
    mutationFn: () => deleteChatGroup(numericId!),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminChatGroups'] });
      router.back();
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.deleteFailed')),
  });

  const postMut = useMutation({
    mutationFn: (body: string) => postAdminChatMessage(numericId!, body),
    onSuccess: () => {
      setMessage('');
      Alert.alert(t('admin.chatPostedTitle'), t('admin.chatPostedMessage'));
    },
    onError: () => Alert.alert(t('admin.chatPostFailedTitle'), t('admin.chatPostFailedMessage')),
  });

  const addMemberMut = useMutation({
    mutationFn: (parentAccountId: number) => addChatGroupMember(numericId!, parentAccountId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminChatGroups'] }),
  });
  const removeMemberMut = useMutation({
    mutationFn: (memberId: number) => removeChatGroupMember(numericId!, memberId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminChatGroups'] }),
  });

  const confirmDelete = () => {
    Alert.alert(
      t('admin.deleteChatGroupConfirmTitle'),
      t('admin.deleteChatGroupConfirmMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('admin.delete'), style: 'destructive', onPress: () => delGroup.mutate() },
      ],
      { cancelable: true },
    );
  };

  const openSeedPicker = () => {
    const opts = [t('admin.noneOption'), ...(teams.data ?? []).map((tm) => tm.name), t('common.cancel')];
    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        { options: opts, cancelButtonIndex: opts.length - 1, title: t('admin.seedFromTeam') },
        (idx) => {
          if (idx === opts.length - 1) return;
          setSeedTeamId(idx === 0 ? null : teams.data?.[idx - 1]?.id ?? null);
        },
      );
    }
  };

  if (!isNew && (groups.isLoading || !group)) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        {groups.isLoading ? <ActivityIndicator size="large" color={colors.brand} /> : null}
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: isNew ? t('admin.newChatGroup') : group?.title ?? '' }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        <Text style={styles.label}>{t('admin.groupTitle')}</Text>
        <TextInput style={styles.input} value={title} onChangeText={setTitle} maxLength={128} />

        {isNew ? (
          <>
            <Text style={styles.label}>{t('admin.seedFromTeam')}</Text>
            <TouchableOpacity style={styles.picker} onPress={openSeedPicker}>
              <Text style={styles.pickerText}>
                {seedTeamId ? teams.data?.find((tm) => tm.id === seedTeamId)?.name ?? '?' : t('admin.noneOption')}
              </Text>
              <Text style={styles.pickerChevron}>›</Text>
            </TouchableOpacity>
          </>
        ) : null}

        <TouchableOpacity
          style={[styles.primaryBtn, (saveGroup.isPending || !title.trim()) && styles.btnDisabled]}
          onPress={() => saveGroup.mutate()}
          disabled={saveGroup.isPending || !title.trim()}
        >
          <Text style={styles.primaryBtnText}>{saveGroup.isPending ? t('admin.saving') : t('admin.save')}</Text>
        </TouchableOpacity>

        {!isNew && group ? (
          <>
            <Text style={styles.sectionTitle}>{t('admin.postAsAdmin')}</Text>
            <TextInput
              style={[styles.input, { minHeight: 100, textAlignVertical: 'top' }]}
              value={message}
              onChangeText={setMessage}
              multiline
              placeholder={t('admin.postAsAdminPlaceholder')}
              placeholderTextColor={colors.subtext}
              maxLength={4000}
            />
            <TouchableOpacity
              style={[styles.primaryBtn, (postMut.isPending || !message.trim()) && styles.btnDisabled]}
              onPress={() => postMut.mutate(message.trim())}
              disabled={postMut.isPending || !message.trim()}
            >
              <Text style={styles.primaryBtnText}>{postMut.isPending ? t('admin.saving') : t('admin.postMessage')}</Text>
            </TouchableOpacity>

            <Text style={styles.sectionTitle}>
              {t('admin.members')} · {group.memberCount}
            </Text>
            {group.members.map((m) => (
              <View key={m.id} style={styles.memberRow}>
                <Text style={{ flex: 1, color: colors.text }}>{m.displayName}</Text>
                {m.role === 1 ? <Text style={styles.adminBadge}>{t('admin.admin')}</Text> : null}
                {m.isCoach ? <Text style={styles.coachBadge}>{t('admin.coach')}</Text> : null}
                <TouchableOpacity onPress={() => removeMemberMut.mutate(m.id)}>
                  <Text style={styles.removeText}>✕</Text>
                </TouchableOpacity>
              </View>
            ))}
            {showAddMember ? (
              <MemberPicker
                onPick={(parentAccountId) => {
                  addMemberMut.mutate(parentAccountId);
                  setShowAddMember(false);
                }}
                onCancel={() => setShowAddMember(false)}
              />
            ) : (
              <TouchableOpacity style={styles.secondaryBtn} onPress={() => setShowAddMember(true)}>
                <Text style={styles.secondaryBtnText}>+ {t('admin.addMember')}</Text>
              </TouchableOpacity>
            )}

            <TouchableOpacity style={styles.dangerBtn} onPress={confirmDelete}>
              <Text style={styles.dangerBtnText}>{t('admin.deleteChatGroup')}</Text>
            </TouchableOpacity>
          </>
        ) : null}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function MemberPicker({
  onPick,
  onCancel,
}: {
  onPick: (parentAccountId: number) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [q, setQ] = React.useState('');
  const [results, setResults] = React.useState<AdminChatParentSearch[]>([]);
  const [busy, setBusy] = React.useState(false);

  const search = async () => {
    setBusy(true);
    try {
      setResults(await searchChatParents(q.trim()));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.pickerCard}>
      <View style={{ flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.sm }}>
        <TextInput
          style={[styles.input, { flex: 1 }]}
          value={q}
          onChangeText={setQ}
          placeholder={t('admin.searchParents')}
          placeholderTextColor={colors.subtext}
          onSubmitEditing={search}
        />
        <TouchableOpacity style={styles.searchBtn} onPress={search} disabled={busy}>
          <Text style={{ color: colors.white, fontWeight: '800' }}>{t('admin.search')}</Text>
        </TouchableOpacity>
      </View>
      {results.map((p) => (
        <TouchableOpacity key={p.parentAccountId} style={styles.pickerRow} onPress={() => onPick(p.parentAccountId)}>
          <View>
            <Text style={{ color: colors.text, fontWeight: '700' }}>{p.name}</Text>
            {p.email ? <Text style={{ color: colors.subtext, fontSize: 12 }}>{p.email}</Text> : null}
          </View>
        </TouchableOpacity>
      ))}
      <TouchableOpacity onPress={onCancel} style={{ marginTop: spacing.sm, alignSelf: 'center' }}>
        <Text style={{ color: colors.subtext, fontWeight: '700' }}>{t('common.cancel')}</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  label: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.md,
    marginBottom: spacing.xs,
  },
  input: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 15,
    color: colors.text,
  },
  picker: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
  },
  pickerText: { fontSize: 15, color: colors.text, fontWeight: '600' },
  pickerChevron: { fontSize: 22, color: colors.subtext },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.xl,
    marginBottom: spacing.sm,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.xs,
  },
  adminBadge: { color: colors.accent, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  coachBadge: { color: colors.brand, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  removeText: { color: colors.danger, fontSize: 18, fontWeight: '800' },
  primaryBtn: {
    marginTop: spacing.md,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  secondaryBtn: {
    marginTop: spacing.sm,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  secondaryBtnText: { color: colors.brand, fontSize: 14, fontWeight: '800' },
  dangerBtn: {
    marginTop: spacing.xl,
    backgroundColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  dangerBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  btnDisabled: { opacity: 0.5 },
  pickerCard: {
    backgroundColor: colors.card,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    marginTop: spacing.sm,
  },
  pickerRow: {
    padding: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  searchBtn: {
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    justifyContent: 'center',
  },
});
