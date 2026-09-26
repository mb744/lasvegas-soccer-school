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
import {
  createGame,
  createMiscEvent,
  createPractice,
  deleteGame,
  deleteMiscEvent,
  deletePractice,
  fetchAdminEvents,
  fetchAdminTeams,
  fetchAdminUniforms,
  fetchAdminVenues,
  updateGame,
  updateMiscEvent,
  updatePractice,
} from '../../../src/api/endpoints';
import { ScheduledEventKind } from '../../../src/api/types';
import { colors, radius, spacing } from '../../../src/theme';
import { useOptionPicker } from '../../../src/ui/useOptionPicker';

type KindStr = 'game' | 'practice' | 'misc';

export default function AdminEventComposer() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const isNew = id === 'new';
  const numericId = isNew ? null : Number(id);
  const router = useRouter();
  const qc = useQueryClient();

  // For an existing event, we read from the admin events list cache (already fetched by the
  // list screen). Fresh loads fetch on mount.
  const list = useQuery({ queryKey: ['adminEvents'], queryFn: () => fetchAdminEvents() });
  const existing = React.useMemo(
    () => (isNew ? null : (list.data ?? []).find((e) => e.id === numericId) ?? null),
    [list.data, isNew, numericId],
  );

  const teams = useQuery({ queryKey: ['adminTeams'], queryFn: fetchAdminTeams });
  const uniforms = useQuery({ queryKey: ['adminUniforms'], queryFn: fetchAdminUniforms });
  const venues = useQuery({ queryKey: ['adminVenues'], queryFn: fetchAdminVenues });

  const [kind, setKind] = React.useState<KindStr>('practice');
  const [teamId, setTeamId] = React.useState<number | null>(null);
  const [startsAt, setStartsAt] = React.useState('');
  const [endsAt, setEndsAt] = React.useState('');
  const [arriveAt, setArriveAt] = React.useState('');
  const [opponentName, setOpponentName] = React.useState('');
  const [isHome, setIsHome] = React.useState<boolean | null>(null);
  const [location, setLocation] = React.useState('');
  const [venueId, setVenueId] = React.useState<number | null>(null);
  const [uniformId, setUniformId] = React.useState<number | null>(null);
  const [summary, setSummary] = React.useState('');
  const [notes, setNotes] = React.useState('');
  // Not editable here yet, but carried through so saving from the phone keeps what the web set.
  const [shoeType, setShoeType] = React.useState(0);
  const [seeded, setSeeded] = React.useState(false);

  React.useEffect(() => {
    if (isNew || seeded || !existing) return;
    setKind(
      existing.kind === ScheduledEventKind.Game ? 'game' :
      existing.kind === ScheduledEventKind.Miscellaneous ? 'misc' : 'practice'
    );
    setTeamId(existing.teamId);
    setStartsAt(toLocalInput(existing.startsAt));
    setEndsAt(existing.endsAt ? toLocalInput(existing.endsAt) : '');
    setArriveAt(existing.arriveAt ? toLocalInput(existing.arriveAt) : '');
    setOpponentName(existing.opponentName ?? '');
    setIsHome(existing.isHome ?? null);
    setLocation(existing.location ?? '');
    setVenueId(existing.venueId ?? null);
    setUniformId(existing.uniformId ?? null);
    setSummary(existing.summary ?? '');
    setNotes(existing.notes ?? '');
    setShoeType(existing.shoeType ?? 0);
    setSeeded(true);
  }, [existing, isNew, seeded]);

  const save = useMutation({
    mutationFn: async () => {
      if (!teamId) throw new Error('team required');
      if (!startsAt) throw new Error('start required');
      const startsAtIso = new Date(startsAt).toISOString();
      const endsAtIso = endsAt ? new Date(endsAt).toISOString() : null;
      const arriveAtIso = arriveAt ? new Date(arriveAt).toISOString() : null;
      const trimmedLocation = location.trim() || null;
      const trimmedSummary = summary.trim() || null;
      const trimmedNotes = notes.trim() || null;

      if (kind === 'game') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          arriveAt: arriveAtIso,
          opponentName: opponentName.trim() || null,
          isHome,
          location: trimmedLocation,
          summary: trimmedSummary,
          notes: trimmedNotes,
          uniformId,
          venueId,
          shoeType,
        };
        if (isNew) await createGame(teamId, payload);
        else await updateGame(numericId!, payload);
      } else if (kind === 'misc') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
          notes: trimmedNotes,
          venueId,
          shoeType,
        };
        if (isNew) await createMiscEvent(teamId, payload);
        else await updateMiscEvent(numericId!, payload);
      } else {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
          notes: trimmedNotes,
          venueId,
          shoeType,
        };
        if (isNew) await createPractice(teamId, payload);
        else await updatePractice(numericId!, payload);
      }
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminEvents'] });
      await qc.invalidateQueries({ queryKey: ['schedule'] });
      router.back();
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.saveFailed')),
  });

  const del = useMutation({
    mutationFn: async () => {
      if (kind === 'game') await deleteGame(numericId!);
      else if (kind === 'misc') await deleteMiscEvent(numericId!);
      else await deletePractice(numericId!);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['adminEvents'] });
      await qc.invalidateQueries({ queryKey: ['schedule'] });
      router.back();
    },
    onError: () => Alert.alert(t('common.retry'), t('admin.deleteFailed')),
  });

  const picker = useOptionPicker();

  const openTeamPicker = () => picker.open(
    t('admin.pickTeam'),
    (teams.data ?? []).map((tm) => ({ label: tm.name, onPick: () => setTeamId(tm.id) })),
  );

  const openVenuePicker = () => picker.open(t('admin.pickVenue'), [
    { label: t('admin.noneOption'), onPick: () => setVenueId(null) },
    ...(venues.data ?? []).map((v) => ({ label: v.name, onPick: () => setVenueId(v.id) })),
  ]);

  const openUniformPicker = () => picker.open(t('admin.pickUniform'), [
    { label: t('admin.uniformAuto'), onPick: () => setUniformId(null) },
    ...(uniforms.data ?? []).map((u) => ({ label: u.name, onPick: () => setUniformId(u.id) })),
  ]);

  if (!isNew && list.isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: '' }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  const teamLabel = teamId ? teams.data?.find((tm) => tm.id === teamId)?.name ?? '?' : t('admin.pickTeam');
  const venueLabel = venueId ? venues.data?.find((v) => v.id === venueId)?.name ?? '?' : t('admin.noneOption');
  const uniformLabel = uniformId ? uniforms.data?.find((u) => u.id === uniformId)?.name ?? '?' : t('admin.uniformAuto');

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 100 : 0}
    >
      <Stack.Screen options={{ title: isNew ? t('admin.newEvent') : t('admin.editEvent') }} />
      {picker.sheet}
      <ScrollView contentContainerStyle={{ padding: spacing.lg }}>
        {isNew ? (
          <>
            <Text style={styles.label}>{t('admin.eventKind')}</Text>
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              {(['practice', 'game', 'misc'] as KindStr[]).map((k) => (
                <TouchableOpacity
                  key={k}
                  style={[styles.pill, kind === k && styles.pillActive]}
                  onPress={() => setKind(k)}
                >
                  <Text style={[styles.pillText, kind === k && styles.pillTextActive]}>{t(`admin.kind${k[0].toUpperCase() + k.slice(1)}` as const)}</Text>
                </TouchableOpacity>
              ))}
            </View>
          </>
        ) : null}

        <Text style={styles.label}>{t('admin.pickTeam')}</Text>
        <TouchableOpacity style={styles.picker} onPress={openTeamPicker}>
          <Text style={styles.pickerText}>{teamLabel}</Text>
          <Text style={styles.pickerChevron}>›</Text>
        </TouchableOpacity>

        <Text style={styles.label}>{t('admin.startLabel')}</Text>
        <TextInput style={styles.input} value={startsAt} onChangeText={setStartsAt} placeholder="YYYY-MM-DDTHH:MM" placeholderTextColor={colors.subtext} />

        <Text style={styles.label}>{t('admin.endLabel')}</Text>
        <TextInput style={styles.input} value={endsAt} onChangeText={setEndsAt} placeholder="YYYY-MM-DDTHH:MM" placeholderTextColor={colors.subtext} />

        {kind === 'game' ? (
          <>
            <Text style={styles.label}>{t('admin.arriveLabel')}</Text>
            <TextInput style={styles.input} value={arriveAt} onChangeText={setArriveAt} placeholder="YYYY-MM-DDTHH:MM" placeholderTextColor={colors.subtext} />

            <Text style={styles.label}>{t('admin.opponentLabel')}</Text>
            <TextInput style={styles.input} value={opponentName} onChangeText={setOpponentName} maxLength={256} />

            <Text style={styles.label}>{t('admin.homeAwayLabel')}</Text>
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              {[
                { v: true as boolean | null, l: t('schedule.home') },
                { v: false as boolean | null, l: t('schedule.away') },
                { v: null as boolean | null, l: t('admin.homeAwayUnknown') },
              ].map((o) => (
                <TouchableOpacity
                  key={String(o.v)}
                  style={[styles.pill, isHome === o.v && styles.pillActive]}
                  onPress={() => setIsHome(o.v)}
                >
                  <Text style={[styles.pillText, isHome === o.v && styles.pillTextActive]}>{o.l}</Text>
                </TouchableOpacity>
              ))}
            </View>

            <Text style={styles.label}>{t('admin.uniformLabel')}</Text>
            <TouchableOpacity style={styles.picker} onPress={openUniformPicker}>
              <Text style={styles.pickerText}>{uniformLabel}</Text>
              <Text style={styles.pickerChevron}>›</Text>
            </TouchableOpacity>
          </>
        ) : null}

        <Text style={styles.label}>{t('admin.venueLabel')}</Text>
        <TouchableOpacity style={styles.picker} onPress={openVenuePicker}>
          <Text style={styles.pickerText}>{venueLabel}</Text>
          <Text style={styles.pickerChevron}>›</Text>
        </TouchableOpacity>

        <Text style={styles.label}>{t('admin.fieldLabel')}</Text>
        <TextInput style={styles.input} value={location} onChangeText={setLocation} placeholder="Field 3" placeholderTextColor={colors.subtext} maxLength={512} />

        <Text style={styles.label}>{t('admin.summaryLabel')}</Text>
        <TextInput style={styles.input} value={summary} onChangeText={setSummary} maxLength={512} />

        <Text style={styles.label}>{t('admin.notesLabel')}</Text>
        <TextInput
          style={[styles.input, { minHeight: 100, textAlignVertical: 'top' }]}
          value={notes}
          onChangeText={setNotes}
          multiline
          maxLength={2000}
        />

        <TouchableOpacity
          style={[styles.primaryBtn, (save.isPending || !teamId || !startsAt) && styles.btnDisabled]}
          onPress={() => save.mutate()}
          disabled={save.isPending || !teamId || !startsAt}
        >
          <Text style={styles.primaryBtnText}>{save.isPending ? t('admin.saving') : t('admin.save')}</Text>
        </TouchableOpacity>

        {!isNew ? (
          <TouchableOpacity
            style={styles.dangerBtn}
            onPress={() => Alert.alert(
              t('admin.deleteEventConfirmTitle'), t('admin.deleteEventConfirmMessage'),
              [
                { text: t('common.cancel'), style: 'cancel' },
                { text: t('admin.delete'), style: 'destructive', onPress: () => del.mutate() },
              ],
            )}
          >
            <Text style={styles.dangerBtnText}>{t('admin.deleteEvent')}</Text>
          </TouchableOpacity>
        ) : null}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
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
  dangerBtn: {
    marginTop: spacing.lg,
    backgroundColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  dangerBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
  btnDisabled: { opacity: 0.5 },
});
