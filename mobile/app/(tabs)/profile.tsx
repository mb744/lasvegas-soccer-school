import React from 'react';
import { ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { colors, radius, spacing } from '../../src/theme';

export default function ProfileScreen() {
  const { t } = useTranslation();
  const { me, signOut } = useAuth();

  if (!me) return null;

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <View style={styles.header}>
        <View style={styles.avatar}>
          <Text style={styles.avatarText}>
            {(me.firstName?.[0] ?? '') + (me.lastName?.[0] ?? '')}
          </Text>
        </View>
        <Text style={styles.name}>
          {me.firstName} {me.lastName}
        </Text>
        <Text style={styles.email}>{me.email}</Text>
      </View>

      <Text style={styles.sectionTitle}>{t('profile.players')}</Text>
      {me.players.length === 0 ? (
        <Text style={styles.muted}>—</Text>
      ) : (
        me.players.map((p) => (
          <View key={p.id} style={styles.playerCard}>
            <Text style={styles.playerName}>
              {p.firstName} {p.lastName}
            </Text>
            <Text style={styles.playerTeams}>
              {p.teams.length > 0 ? p.teams.map((tm) => tm.teamName).join(', ') : t('profile.noTeams')}
            </Text>
          </View>
        ))
      )}

      <TouchableOpacity style={styles.signOut} onPress={signOut}>
        <Text style={styles.signOutText}>{t('profile.signOut')}</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  header: { alignItems: 'center', marginBottom: spacing.xl },
  avatar: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: colors.brand,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: spacing.md,
  },
  avatarText: { color: colors.white, fontSize: 28, fontWeight: '800' },
  name: { fontSize: 22, fontWeight: '800', color: colors.text },
  email: { fontSize: 14, color: colors.subtext, marginTop: 2 },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  muted: { color: colors.subtext },
  playerCard: {
    backgroundColor: colors.card,
    borderRadius: radius.md,
    padding: spacing.lg,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border,
  },
  playerName: { fontSize: 16, fontWeight: '700', color: colors.text },
  playerTeams: { fontSize: 14, color: colors.subtext, marginTop: 2 },
  signOut: {
    marginTop: spacing.xl,
    borderWidth: 1,
    borderColor: colors.danger,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  signOutText: { color: colors.danger, fontSize: 16, fontWeight: '800' },
});
