import React from 'react';
import {
  ActionSheetIOS,
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createAnnouncement,
  fetchAdminAnnouncements,
  fetchAdminTeams,
  updateAnnouncement,
} from '../../../src/api/endpoints';
import type { SaveAnnouncementRequest, TeamOption } from '../../../src/api/types';
import { colors, radius, spacing } from '../../../src/theme';

/**
 * Announcement composer. The `[id]` param is "new" for a fresh announcement or the numeric row id
 * when editing; expo-router treats both as the same route so we can reuse the same form.
 */
export default function AnnouncementComposer() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const isNew = id === 'new';
  const numericId = isNew ? null : Number(id);
  const router = useRouter();
  const qc = useQueryClient();

  const list = useQuery({
    queryKey: ['adminAnnouncements'],
    queryFn: fetchAdminAnnouncements,
    enabled: !isNew,
  });
  const teams = useQuery({ queryKey: ['adminTeams'], queryFn: fetchAdminTeams });

  const editing = React.useMemo(
    () => (isNew ? null : (list.data ?? []).find((a) => a.id === numericId) ?? null),
    [list.data, isNew, numericId],
  );

  const [title, setTitle] = React.useState('');
  const [body, setBody] = React.useState('');
  const [teamId, setTeamId] = React.useState<number | null>(null);
  const [endsAt, setEndsAt] = React.useState<string | null>(null);
  const [isActive, setIsActive] = React.useState(true);
  const [seeded, setSeeded] = React.useState(false);

  React.useEffect(() => {
    if (isNew || seeded || !editing) return;
    setTitle(editing.title);
    setBody(editing.body);
    setTeamId(editing.teamId);
    setEndsAt(editing.endsAt);
    setIsActive(editing.isActive);
    setSeeded(true);
  }, [editing, isNew, seeded]);

  const mutation = useMutation({
    mutationFn: async (payload: SaveAnnouncementRequest) =>
      isNew ? await createAnnouncement(payload) : await updateAnnouncement(numericId!, payload),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminAnnouncements'] });
      await qc.invalidateQueries({ queryKey: ['announcements'] });
      router.back();
    },
  });

  const submit = () => {
    if (!title.trim() || !body.trim()) {
      Alert.alert(t('common.retry'), 'Title and message are required.');
      return;
    }
    mutation.mutate({
      title: title.trim(),
      body: body.trim(),
      teamId,
      endsAt,
      isActive,
    });
  };

  const openAudiencePicker = () => {
    const options = [
      t('admin.audienceEveryoneOption'),
      ...(teams.data ?? []).map((tm) => tm.name),
      t('common.cancel'),
    ];
    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          title: t('admin.audiencePickerTitle'),
          options,
          cancelButtonIndex: options.length - 1,
        },
        (idx) => {
          if (idx === options.length - 1) return;
          if (idx === 0) setTeamId(null);
          else setTeamId(teams.data?.[idx - 1]?.id ?? null);
        },
      );
    } else {
      Alert.alert(
        t('admin.audiencePickerTitle'),
        '',
        options.slice(0, -1).map((label, idx) => ({
          text: label,
          onPress: () => setTeamId(idx === 0 ? null : teams.data?.[idx - 1]?.id ?? null),
        })).concat([{ text: t('common.cancel'), onPress: () => {} }]),
      );
    }
  };

  const currentAudience = teamId
    ? teams.data?.find((tm) => tm.id === teamId)?.name ?? t('admin.audienceEveryone')
    : t('admin.audienceEveryone');

  if (!isNew && !editing && list.isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('admin.editAnnouncement') }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: isNew ? t('admin.newAnnouncement') : t('admin.editAnnouncement') }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        <Text style={styles.label}>{t('admin.titleLabel')}</Text>
        <TextInput
          style={styles.input}
          value={title}
          onChangeText={setTitle}
          maxLength={200}
          placeholder="Field 3 will be closed Saturday"
          placeholderTextColor={colors.subtext}
        />

        <Text style={styles.label}>{t('admin.bodyLabel')}</Text>
        <TextInput
          style={[styles.input, styles.textarea]}
          value={body}
          onChangeText={setBody}
          maxLength={2000}
          multiline
          placeholder="Practice moved to Field 5. Same time, same coach."
          placeholderTextColor={colors.subtext}
        />

        <Text style={styles.label}>{t('admin.audience')}</Text>
        <TouchableOpacity style={styles.pickerRow} onPress={openAudiencePicker} activeOpacity={0.85}>
          <Text style={styles.pickerText}>{currentAudience}</Text>
          <Text style={styles.pickerChevron}>›</Text>
        </TouchableOpacity>

        <View style={styles.switchRow}>
          <Text style={styles.switchLabel}>{t('admin.activeLabel')}</Text>
          <Switch value={isActive} onValueChange={setIsActive} />
        </View>

        <TouchableOpacity
          style={[styles.submit, mutation.isPending && styles.submitDisabled]}
          onPress={submit}
          disabled={mutation.isPending}
        >
          <Text style={styles.submitText}>{mutation.isPending ? t('admin.saving') : t('admin.save')}</Text>
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
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
  textarea: { minHeight: 120, textAlignVertical: 'top' },
  pickerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
  },
  pickerText: { fontSize: 15, color: colors.text, fontWeight: '600' },
  pickerChevron: { fontSize: 22, color: colors.subtext },
  switchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: spacing.lg,
    paddingHorizontal: spacing.sm,
  },
  switchLabel: { fontSize: 15, color: colors.text, fontWeight: '600' },
  submit: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  submitDisabled: { opacity: 0.6 },
  submitText: { color: colors.white, fontSize: 16, fontWeight: '800' },
});
