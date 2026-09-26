import React from 'react';
import { ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { can, Perm } from '../../src/auth/can';
import { colors, radius, spacing } from '../../src/theme';

/** Admin landing tab — hub of tiles that open into each section screen. Admins see every tile;
 *  coaches with Event creator see just Events (their own teams). */
export default function AdminHubScreen() {
  const { t } = useTranslation();
  const { me } = useAuth();
  const router = useRouter();

  if (!can(me, Perm.AdminAccess, Perm.EventsCreate)) return null;

  const allTiles: { key: string; icon: string; label: string; blurb: string; path: string; permission: string }[] = [
    {
      key: 'announcements',
      icon: '📣',
      label: t('admin.hubAnnouncements'),
      blurb: t('admin.hubAnnouncementsBlurb'),
      path: '/admin/announcements',
      permission: Perm.AdminAccess,
    },
    {
      key: 'teams',
      icon: '⚽',
      label: t('admin.hubTeams'),
      blurb: t('admin.hubTeamsBlurb'),
      path: '/admin/teams',
      permission: Perm.AdminAccess,
    },
    {
      key: 'events',
      icon: '📅',
      label: t('admin.hubEvents'),
      blurb: t('admin.hubEventsBlurb'),
      path: '/admin/events',
      permission: Perm.EventsCreate,
    },
    {
      key: 'chatGroups',
      icon: '💭',
      label: t('admin.hubChatGroups'),
      blurb: t('admin.hubChatGroupsBlurb'),
      path: '/admin/chat-groups',
      permission: Perm.AdminAccess,
    },
    {
      key: 'users',
      icon: '👥',
      label: t('admin.hubUsers'),
      blurb: t('admin.hubUsersBlurb'),
      path: '/admin/users',
      permission: Perm.AdminAccess,
    },
  ];
  const tiles = allTiles.filter((tile) => can(me, tile.permission));

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Text style={styles.title}>{t('admin.title')}</Text>
      <Text style={styles.subtitle}>{t('admin.hubSubtitle')}</Text>

      {tiles.map((tile) => (
        <TouchableOpacity
          key={tile.key}
          style={styles.tile}
          onPress={() => router.push(tile.path as never)}
          activeOpacity={0.85}
        >
          <Text style={styles.tileIcon}>{tile.icon}</Text>
          <View style={styles.tileBody}>
            <Text style={styles.tileLabel}>{tile.label}</Text>
            <Text style={styles.tileBlurb}>{tile.blurb}</Text>
          </View>
          <Text style={styles.tileChevron}>›</Text>
        </TouchableOpacity>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  title: { fontSize: 24, fontWeight: '800', color: colors.text, marginBottom: spacing.xs },
  subtitle: { fontSize: 14, color: colors.subtext, marginBottom: spacing.lg, lineHeight: 20 },

  tile: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    marginBottom: spacing.md,
  },
  tileIcon: { fontSize: 28, marginRight: spacing.md },
  tileBody: { flex: 1 },
  tileLabel: { fontSize: 16, fontWeight: '800', color: colors.text },
  tileBlurb: { fontSize: 13, color: colors.subtext, marginTop: 2, lineHeight: 18 },
  tileChevron: { fontSize: 24, color: colors.subtext, marginLeft: spacing.sm },
});
