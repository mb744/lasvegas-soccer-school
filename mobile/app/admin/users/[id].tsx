import React from 'react';
import {
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
import { fetchAdminUsers, setAdminUserRole, updateAdminUserProfile } from '../../../src/api/endpoints';
import { useAuth } from '../../../src/auth/AuthContext';
import { colors, radius, spacing } from '../../../src/theme';

export default function AdminUserEditScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { me } = useAuth();

  const list = useQuery({ queryKey: ['adminUsers'], queryFn: fetchAdminUsers });
  const user = React.useMemo(() => (list.data ?? []).find((u) => u.id === id) ?? null, [list.data, id]);

  const [firstName, setFirstName] = React.useState('');
  const [lastName, setLastName] = React.useState('');
  const [isAdmin, setIsAdmin] = React.useState(false);
  const [seeded, setSeeded] = React.useState(false);

  // Seed local form state once from the fetched row. Re-seeding on every render would clobber
  // the user's edits as they type.
  React.useEffect(() => {
    if (seeded || !user) return;
    setFirstName(user.firstName);
    setLastName(user.lastName);
    setIsAdmin(user.isAdmin);
    setSeeded(true);
  }, [user, seeded]);

  const isSelf = !!me && !!user && me.userId === user.id;

  const saveProfile = useMutation({
    mutationFn: () => updateAdminUserProfile(id, { firstName: firstName.trim(), lastName: lastName.trim() }),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminUsers'] });
    },
    onError: (e: unknown) =>
      Alert.alert(t('common.retry'), (e as { message?: string })?.message ?? t('admin.saveFailed')),
  });

  const saveRole = useMutation({
    mutationFn: (next: boolean) => setAdminUserRole(id, next),
    onMutate: (next) => setIsAdmin(next),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminUsers'] });
    },
    onError: (e: unknown, next) => {
      // Revert the toggle if the server rejected it (e.g. self-demotion guard).
      setIsAdmin(!next);
      Alert.alert(t('common.retry'), (e as { message?: string })?.message ?? t('admin.saveFailed'));
    },
  });

  if (list.isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('admin.editUser') }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }
  if (!user) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('admin.editUser') }} />
        <Text style={styles.empty}>{t('admin.userNotFound')}</Text>
      </View>
    );
  }

  const canSaveProfile =
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    (firstName.trim() !== user.firstName || lastName.trim() !== user.lastName);

  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: colors.bg }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        <Stack.Screen options={{ title: t('admin.editUser') }} />

        <View style={styles.card}>
          <Text style={styles.label}>{t('admin.email')}</Text>
          <Text style={styles.readonly}>{user.email}</Text>

          <Text style={styles.label}>{t('admin.firstName')}</Text>
          <TextInput
            style={styles.input}
            value={firstName}
            onChangeText={setFirstName}
            autoCapitalize="words"
            editable={user.parentAccountId !== null}
          />

          <Text style={styles.label}>{t('admin.lastName')}</Text>
          <TextInput
            style={styles.input}
            value={lastName}
            onChangeText={setLastName}
            autoCapitalize="words"
            editable={user.parentAccountId !== null}
          />

          {user.parentAccountId === null ? (
            <Text style={styles.hint}>{t('admin.userNoProfileHint')}</Text>
          ) : null}

          <TouchableOpacity
            style={[styles.primaryBtn, (!canSaveProfile || saveProfile.isPending) && styles.disabled]}
            onPress={() => saveProfile.mutate()}
            disabled={!canSaveProfile || saveProfile.isPending}
          >
            <Text style={styles.primaryBtnText}>
              {saveProfile.isPending ? t('admin.saving') : t('admin.save')}
            </Text>
          </TouchableOpacity>
        </View>

        <View style={styles.card}>
          <Text style={styles.sectionTitle}>{t('admin.rolesTitle')}</Text>

          <View style={styles.roleRow}>
            <View style={{ flex: 1 }}>
              <Text style={styles.roleLabel}>{t('admin.admin')}</Text>
              <Text style={styles.roleBlurb}>{t('admin.adminRoleBlurb')}</Text>
            </View>
            <Switch
              value={isAdmin}
              disabled={isSelf || saveRole.isPending}
              onValueChange={(next) => saveRole.mutate(next)}
            />
          </View>
          {isSelf ? <Text style={styles.hint}>{t('admin.cannotDemoteSelf')}</Text> : null}

          <View style={styles.roleRow}>
            <View style={{ flex: 1 }}>
              <Text style={styles.roleLabel}>{t('admin.coach')}</Text>
              <Text style={styles.roleBlurb}>{t('admin.coachRoleBlurb')}</Text>
            </View>
            <Text style={user.isCoach ? styles.coachOn : styles.coachOff}>
              {user.isCoach ? t('admin.coachOn') : t('admin.coachOff')}
            </Text>
          </View>
        </View>

        <TouchableOpacity style={styles.secondaryBtn} onPress={() => router.back()}>
          <Text style={styles.secondaryBtnText}>{t('common.done')}</Text>
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  empty: { color: colors.subtext, fontSize: 15 },

  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    marginBottom: spacing.lg,
  },
  label: {
    fontSize: 12,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.md,
    marginBottom: 4,
  },
  readonly: { fontSize: 15, color: colors.text },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 15,
    color: colors.text,
    backgroundColor: colors.bg,
  },
  hint: { fontSize: 12, color: colors.subtext, marginTop: spacing.xs, fontStyle: 'italic' },

  primaryBtn: {
    marginTop: spacing.lg,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  disabled: { opacity: 0.5 },

  secondaryBtn: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  secondaryBtnText: { color: colors.text, fontSize: 15, fontWeight: '800' },

  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  roleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    gap: spacing.md,
  },
  roleLabel: { fontSize: 15, fontWeight: '800', color: colors.text },
  roleBlurb: { fontSize: 12, color: colors.subtext, marginTop: 2, lineHeight: 16 },
  coachOn: { color: colors.brand, fontSize: 13, fontWeight: '800', textTransform: 'uppercase' },
  coachOff: { color: colors.subtext, fontSize: 13, fontWeight: '800', textTransform: 'uppercase' },
});
