import React from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Linking,
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
  addTeamPlayer,
  createTeam,
  deleteTeam,
  fetchAdminPlayers,
  fetchAdminTeamDetail,
  removeTeamCoach,
  removeTeamPlayer,
  updateTeam,
} from '../../../../src/api/endpoints';
import type { AdminPlayerOption } from '../../../../src/api/types';
import { colors, radius, spacing } from '../../../../src/theme';

export default function AdminTeamDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const isNew = id === 'new';
  const teamId = isNew ? null : Number(id);
  const router = useRouter();
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['adminTeamDetail', teamId],
    queryFn: () => fetchAdminTeamDetail(teamId!),
    enabled: !!teamId,
  });

  const [name, setName] = React.useState('');
  const [nameSeeded, setNameSeeded] = React.useState(false);
  const [showAddPlayer, setShowAddPlayer] = React.useState(false);

  React.useEffect(() => {
    if (isNew || nameSeeded || !data) return;
    setName(data.name);
    setNameSeeded(true);
  }, [data, isNew, nameSeeded]);

  const saveTeam = useMutation({
    mutationFn: async () => {
      if (isNew) {
        const t = await createTeam({ name: name.trim() });
        return t.id;
      }
      await updateTeam(teamId!, { name: name.trim() });
      return teamId!;
    },
    onSuccess: async (savedId) => {
      await qc.invalidateQueries({ queryKey: ['adminTeams'] });
      await qc.invalidateQueries({ queryKey: ['adminTeamDetail', savedId] });
      if (isNew) router.replace(`/admin/teams/${savedId}`);
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.saveFailed')),
  });

  const delTeam = useMutation({
    mutationFn: () => deleteTeam(teamId!),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminTeams'] });
      router.back();
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.deleteFailed')),
  });

  const addPlayerMut = useMutation({
    mutationFn: (playerId: number) => addTeamPlayer(teamId!, playerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminTeamDetail', teamId] }),
  });
  const removePlayerMut = useMutation({
    mutationFn: (playerId: number) => removeTeamPlayer(teamId!, playerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminTeamDetail', teamId] }),
  });
  const removeCoachMut = useMutation({
    mutationFn: (coachRowId: number) => removeTeamCoach(teamId!, coachRowId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['adminTeamDetail', teamId] }),
  });

  const confirmDeleteTeam = () => {
    Alert.alert(
      t('admin.deleteTeamConfirmTitle'),
      t('admin.deleteTeamConfirmMessage'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('admin.delete'), style: 'destructive', onPress: () => delTeam.mutate() },
      ],
      { cancelable: true },
    );
  };

  if (!isNew && (isLoading || !data)) {
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
      <Stack.Screen options={{ title: isNew ? t('admin.newTeam') : (data?.name ?? '') }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg, paddingBottom: 96 }}>
        <Text style={styles.label}>{t('admin.teamNameLabel')}</Text>
        <TextInput
          style={styles.input}
          value={name}
          onChangeText={setName}
          maxLength={128}
          placeholder="B2015 Blue"
          placeholderTextColor={colors.subtext}
        />

        <TouchableOpacity
          style={[styles.primaryBtn, saveTeam.isPending && styles.btnDisabled]}
          onPress={() => saveTeam.mutate()}
          disabled={saveTeam.isPending || !name.trim()}
        >
          <Text style={styles.primaryBtnText}>{saveTeam.isPending ? t('admin.saving') : t('admin.save')}</Text>
        </TouchableOpacity>

        {!isNew && data ? (
          <>
            <Text style={styles.sectionTitle}>
              {t('admin.roster')} · {data.players.length}
            </Text>
            {data.players.map((p) => (
              <View key={p.id} style={styles.row}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.rowName}>
                    {p.firstName} {p.lastName}
                  </Text>
                  {p.parentName ? <Text style={styles.rowMeta}>{p.parentName}</Text> : null}
                </View>
                {p.parentPhone ? (
                  <TouchableOpacity onPress={() => Linking.openURL(`tel:${p.parentPhone}`)}>
                    <Text style={styles.iconBtn}>📞</Text>
                  </TouchableOpacity>
                ) : null}
                <TouchableOpacity onPress={() => removePlayerMut.mutate(p.id)}>
                  <Text style={styles.removeText}>✕</Text>
                </TouchableOpacity>
              </View>
            ))}
            {showAddPlayer ? (
              <PlayerPicker
                onPick={(playerId) => {
                  addPlayerMut.mutate(playerId);
                  setShowAddPlayer(false);
                }}
                onCancel={() => setShowAddPlayer(false)}
              />
            ) : (
              <TouchableOpacity style={styles.secondaryBtn} onPress={() => setShowAddPlayer(true)}>
                <Text style={styles.secondaryBtnText}>+ {t('admin.addPlayer')}</Text>
              </TouchableOpacity>
            )}

            <Text style={styles.sectionTitle}>
              {t('admin.coaches')} · {data.coaches.length}
            </Text>
            {data.coaches.map((c) => (
              <TouchableOpacity
                key={c.id}
                style={styles.row}
                onPress={() => router.push(`/admin/teams/${teamId}/coaches/${c.id}`)}
                activeOpacity={0.85}
              >
                <View style={{ flex: 1 }}>
                  <Text style={styles.rowName}>{c.name}</Text>
                  <Text style={styles.rowMeta}>{c.role}</Text>
                </View>
                {c.phone ? (
                  <TouchableOpacity onPress={() => Linking.openURL(`tel:${c.phone}`)}>
                    <Text style={styles.iconBtn}>📞</Text>
                  </TouchableOpacity>
                ) : null}
                <TouchableOpacity onPress={() => removeCoachMut.mutate(c.id)}>
                  <Text style={styles.removeText}>✕</Text>
                </TouchableOpacity>
              </TouchableOpacity>
            ))}
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => router.push(`/admin/teams/${teamId}/coaches/new`)}
            >
              <Text style={styles.secondaryBtnText}>+ {t('admin.addCoach')}</Text>
            </TouchableOpacity>

            <TouchableOpacity style={styles.dangerBtn} onPress={confirmDeleteTeam}>
              <Text style={styles.dangerBtnText}>{t('admin.deleteTeam')}</Text>
            </TouchableOpacity>
          </>
        ) : null}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function PlayerPicker({ onPick, onCancel }: { onPick: (playerId: number) => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const [q, setQ] = React.useState('');
  const [results, setResults] = React.useState<AdminPlayerOption[]>([]);
  const [busy, setBusy] = React.useState(false);

  const search = async () => {
    setBusy(true);
    try {
      setResults(await fetchAdminPlayers(q.trim() || undefined));
    } finally {
      setBusy(false);
    }
  };

  React.useEffect(() => {
    void search();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <View style={styles.pickerCard}>
      <View style={{ flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.sm }}>
        <TextInput
          style={[styles.input, { flex: 1 }]}
          value={q}
          onChangeText={setQ}
          placeholder={t('admin.searchPlayers')}
          placeholderTextColor={colors.subtext}
          onSubmitEditing={search}
        />
        <TouchableOpacity style={styles.searchBtn} onPress={search} disabled={busy}>
          <Text style={styles.searchBtnText}>{t('admin.search')}</Text>
        </TouchableOpacity>
      </View>
      {results.map((p) => (
        <TouchableOpacity key={p.id} style={styles.pickerRow} onPress={() => onPick(p.id)}>
          <View style={{ flex: 1 }}>
            <Text style={styles.rowName}>
              {p.firstName} {p.lastName}
            </Text>
            {p.parentName ? <Text style={styles.rowMeta}>{p.parentName}</Text> : null}
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
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.xl,
    marginBottom: spacing.sm,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.xs,
  },
  rowName: { fontSize: 14, fontWeight: '700', color: colors.text },
  rowMeta: { fontSize: 12, color: colors.subtext, marginTop: 2 },
  iconBtn: { fontSize: 20 },
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
  searchBtnText: { color: colors.white, fontWeight: '800' },
});
