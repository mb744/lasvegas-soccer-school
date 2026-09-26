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
import { Stack, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { inviteFamilyMember } from '../../src/api/endpoints';
import { FamilyAccessLevel, Language } from '../../src/api/types';
import { colors, radius, spacing } from '../../src/theme';

/** Invite a grandparent, friend or co-parent by email. They get an email with the app link and are
 *  linked to this family when they sign in with that address. */
export default function FamilyInviteScreen() {
  const { t, i18n } = useTranslation();
  const router = useRouter();
  const qc = useQueryClient();

  const [firstName, setFirstName] = React.useState('');
  const [lastName, setLastName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [level, setLevel] = React.useState<FamilyAccessLevel>(FamilyAccessLevel.Viewer);
  const [language, setLanguage] = React.useState<Language>(
    i18n.language.startsWith('es') ? Language.Spanish : Language.English,
  );

  const valid = firstName.trim().length > 0 && /^\S+@\S+\.\S+$/.test(email.trim());

  const send = useMutation({
    mutationFn: () =>
      inviteFamilyMember({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        accessLevel: level,
        language,
      }),
    onSuccess: (family) => {
      qc.setQueryData(['family'], family);
      Alert.alert(t('family.sentTitle'), t('family.sentBody', { name: firstName.trim(), email: email.trim() }), [
        { text: t('common.done'), onPress: () => router.back() },
      ]);
    },
    onError: (e: unknown) => {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      Alert.alert(t('family.errorTitle'), typeof data === 'string' && data ? data : t('family.errorBody'));
    },
  });

  const levels: { value: FamilyAccessLevel; label: string; blurb: string }[] = [
    { value: FamilyAccessLevel.Viewer, label: t('family.roleViewer'), blurb: t('family.viewerBlurb') },
    { value: FamilyAccessLevel.Guardian, label: t('family.roleGuardian'), blurb: t('family.guardianBlurb') },
  ];

  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: colors.bg }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <Stack.Screen options={{ title: t('family.inviteTitle') }} />
      <ScrollView contentContainerStyle={{ padding: spacing.lg }} keyboardShouldPersistTaps="handled">
        <Text style={styles.intro}>{t('family.inviteIntro')}</Text>

        <Text style={styles.label}>{t('family.firstName')}</Text>
        <TextInput style={styles.input} value={firstName} onChangeText={setFirstName} autoCapitalize="words" maxLength={80} />

        <Text style={styles.label}>{t('family.lastName')}</Text>
        <TextInput style={styles.input} value={lastName} onChangeText={setLastName} autoCapitalize="words" maxLength={80} />

        <Text style={styles.label}>{t('family.email')}</Text>
        <TextInput
          style={styles.input}
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          maxLength={256}
        />
        <Text style={styles.hint}>{t('family.emailHint')}</Text>

        <Text style={styles.label}>{t('family.accessLabel')}</Text>
        {levels.map((o) => {
          const on = level === o.value;
          return (
            <TouchableOpacity key={o.value} style={[styles.option, on && styles.optionOn]} onPress={() => setLevel(o.value)}>
              <View style={[styles.radio, on && styles.radioOn]}>{on ? <View style={styles.radioDot} /> : null}</View>
              <View style={{ flex: 1 }}>
                <Text style={styles.optionLabel}>{o.label}</Text>
                <Text style={styles.optionBlurb}>{o.blurb}</Text>
              </View>
            </TouchableOpacity>
          );
        })}

        <Text style={styles.label}>{t('family.emailLanguage')}</Text>
        <View style={{ flexDirection: 'row', gap: spacing.sm }}>
          {[
            { v: Language.English, l: 'English' },
            { v: Language.Spanish, l: 'Español' },
          ].map((o) => (
            <TouchableOpacity key={o.v} style={[styles.pill, language === o.v && styles.pillOn]} onPress={() => setLanguage(o.v)}>
              <Text style={[styles.pillText, language === o.v && styles.pillTextOn]}>{o.l}</Text>
            </TouchableOpacity>
          ))}
        </View>

        <TouchableOpacity
          style={[styles.primaryBtn, (!valid || send.isPending) && { opacity: 0.5 }]}
          onPress={() => send.mutate()}
          disabled={!valid || send.isPending}
        >
          <Text style={styles.primaryBtnText}>{send.isPending ? t('family.sending') : t('family.send')}</Text>
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  intro: { fontSize: 14, color: colors.subtext, lineHeight: 20 },
  label: {
    fontSize: 12,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginTop: spacing.lg,
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
  hint: { fontSize: 12, color: colors.subtext, marginTop: spacing.xs, lineHeight: 16 },
  option: {
    flexDirection: 'row',
    gap: spacing.md,
    alignItems: 'flex-start',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  optionOn: { borderColor: colors.brand },
  radio: {
    width: 20,
    height: 20,
    borderRadius: 10,
    borderWidth: 2,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 2,
  },
  radioOn: { borderColor: colors.brand },
  radioDot: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.brand },
  optionLabel: { fontSize: 15, fontWeight: '800', color: colors.text },
  optionBlurb: { fontSize: 13, color: colors.subtext, marginTop: 2, lineHeight: 18 },
  pill: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.card,
    alignItems: 'center',
  },
  pillOn: { backgroundColor: colors.brand, borderColor: colors.brand },
  pillText: { fontSize: 14, fontWeight: '700', color: colors.subtext },
  pillTextOn: { color: colors.white },
  primaryBtn: {
    marginTop: spacing.xl,
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryBtnText: { color: colors.white, fontSize: 15, fontWeight: '800' },
});
