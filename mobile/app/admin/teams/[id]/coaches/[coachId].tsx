import React from 'react';
import {
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
import { addTeamCoach, fetchAdminTeamDetail, updateTeamCoach } from '../../../../../src/api/endpoints';
import type { SaveTeamCoachRequest } from '../../../../../src/api/types';
import { colors, radius, spacing } from '../../../../../src/theme';

type RoleValue = 0 | 1 | 2;

export default function CoachEditScreen() {
  const { t } = useTranslation();
  const { id, coachId } = useLocalSearchParams<{ id: string; coachId: string }>();
  const teamId = Number(id);
  const isNew = coachId === 'new';
  const coachRowId = isNew ? null : Number(coachId);
  const router = useRouter();
  const qc = useQueryClient();

  const { data } = useQuery({
    queryKey: ['adminTeamDetail', teamId],
    queryFn: () => fetchAdminTeamDetail(teamId),
    enabled: !!teamId && !isNew,
  });
  const existing = React.useMemo(
    () => (isNew ? null : data?.coaches.find((c) => c.id === coachRowId) ?? null),
    [data, isNew, coachRowId],
  );

  const [name, setName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [phone, setPhone] = React.useState('');
  const [role, setRole] = React.useState<RoleValue>(0);
  const [seeded, setSeeded] = React.useState(false);

  React.useEffect(() => {
    if (isNew || seeded || !existing) return;
    setName(existing.name);
    setEmail(existing.email ?? '');
    setPhone(existing.phone ?? '');
    setRole(existing.role === 'HeadCoach' ? 0 : existing.role === 'AssistantCoach' ? 1 : 2);
    setSeeded(true);
  }, [existing, isNew, seeded]);

  const save = useMutation({
    mutationFn: async () => {
      const payload: SaveTeamCoachRequest = {
        name: name.trim(),
        email: email.trim() || null,
        phone: phone.trim() || null,
        role,
      };
      if (isNew) await addTeamCoach(teamId, payload);
      else await updateTeamCoach(teamId, coachRowId!, payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminTeamDetail', teamId] });
      router.back();
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.saveFailed')),
  });

  const roleLabel = (r: RoleValue) =>
    r === 0 ? t('admin.roleHead') : r === 1 ? t('admin.roleAssistant') : t('admin.roleManager');

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: isNew ? t('admin.addCoach') : t('admin.editCoach') }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        <Text style={styles.label}>{t('admin.coachName')}</Text>
        <TextInput style={styles.input} value={name} onChangeText={setName} maxLength={160} />

        <Text style={styles.label}>{t('admin.coachEmail')}</Text>
        <TextInput
          style={styles.input}
          value={email}
          onChangeText={setEmail}
          keyboardType="email-address"
          autoCapitalize="none"
          maxLength={256}
        />

        <Text style={styles.label}>{t('admin.coachPhone')}</Text>
        <TextInput
          style={styles.input}
          value={phone}
          onChangeText={setPhone}
          keyboardType="phone-pad"
          maxLength={32}
        />

        <Text style={styles.label}>{t('admin.coachRole')}</Text>
        <View style={{ flexDirection: 'row', gap: spacing.sm }}>
          {[0, 1, 2].map((r) => (
            <TouchableOpacity
              key={r}
              style={[styles.pill, role === r && styles.pillActive]}
              onPress={() => setRole(r as RoleValue)}
            >
              <Text style={[styles.pillText, role === r && styles.pillTextActive]}>{roleLabel(r as RoleValue)}</Text>
            </TouchableOpacity>
          ))}
        </View>

        <TouchableOpacity
          style={[styles.primaryBtn, (save.isPending || !name.trim()) && styles.btnDisabled]}
          onPress={() => save.mutate()}
          disabled={save.isPending || !name.trim()}
        >
          <Text style={styles.primaryBtnText}>{save.isPending ? t('admin.saving') : t('admin.save')}</Text>
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
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
  pill: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.card,
    alignItems: 'center',
  },
  pillActive: { backgroundColor: colors.brand, borderColor: colors.brand },
  pillText: { fontSize: 13, fontWeight: '700', color: colors.subtext },
  pillTextActive: { color: colors.white },
  primaryBtn: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  btnDisabled: { opacity: 0.5 },
});
