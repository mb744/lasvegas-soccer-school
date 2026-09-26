import React from 'react';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchAccessAudit, fetchAccessCatalog, setRolePermission } from '../../../src/api/endpoints';
import type { AccessCatalog, EditableRole, PermissionInfo } from '../../../src/api/types';
import { colors, radius, spacing } from '../../../src/theme';

/** Never switched on for Coach or Parent (mirrors Permissions.AdminRoleOnly on the server). */
const ADMIN_ROLE_ONLY = new Set(['admin.access', 'users.manage', 'roles.manage']);

type Tab = 'roles' | 'log';

/** Role permissions (Coach / Parent) and the change log. Per-person extra permissions live on each
 *  user's screen under Admin → Users. */
export default function AdminAccessScreen() {
  const { t } = useTranslation();
  const [tab, setTab] = React.useState<Tab>('roles');
  const catalog = useQuery({ queryKey: ['accessCatalog'], queryFn: fetchAccessCatalog });

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: t('admin.accessTitle') }} />
      <View style={styles.tabs}>
        {(['roles', 'log'] as Tab[]).map((k) => (
          <TouchableOpacity key={k} style={[styles.tab, tab === k && styles.tabActive]} onPress={() => setTab(k)}>
            <Text style={[styles.tabText, tab === k && styles.tabTextActive]}>
              {k === 'roles' ? t('admin.accessTabRoles') : t('admin.accessTabLog')}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {catalog.isLoading || !catalog.data ? (
        <ActivityIndicator color={colors.brand} style={{ marginTop: spacing.xl }} />
      ) : tab === 'roles' ? (
        <RolesTab catalog={catalog.data} />
      ) : (
        <LogTab catalog={catalog.data} />
      )}
    </ScrollView>
  );
}

function RolesTab({ catalog }: { catalog: AccessCatalog }) {
  const { t } = useTranslation();
  const name = usePermissionName();
  const qc = useQueryClient();

  const toggle = useMutation({
    mutationFn: (v: { role: EditableRole; key: string; enabled: boolean }) => setRolePermission(v.role, v.key, v.enabled),
    onMutate: (v) => {
      // Optimistic: flip the switch now, roll back on error.
      qc.setQueryData<AccessCatalog>(['accessCatalog'], (old) =>
        old ? { ...old, [v.role]: v.enabled ? [...old[v.role], v.key] : old[v.role].filter((k) => k !== v.key) } : old,
      );
    },
    onError: (e: unknown) => {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      Alert.alert(t('common.retry'), typeof data === 'string' ? data : t('admin.saveFailed'));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ['accessCatalog'] }),
  });

  return (
    <View>
      <Text style={styles.help}>{t('admin.accessRolesHelp')}</Text>
      {groupByArea(catalog.permissions).map(([area, perms]) => (
        <View key={area} style={styles.card}>
          <Text style={styles.area}>{t(`admin.accessArea_${area}`, area)}</Text>
          {perms.map((p, i) => (
            <View key={p.key} style={[styles.permRow, i === perms.length - 1 && { borderBottomWidth: 0 }]}>
              <Text style={styles.permName}>{name(p)}</Text>
              <View style={styles.switches}>
                {(['coach', 'parent'] as EditableRole[]).map((role) => {
                  const on = catalog[role].includes(p.key);
                  return (
                    <View key={role} style={styles.switchCell}>
                      <Text style={styles.switchLabel}>
                        {role === 'coach' ? t('admin.accessRoleCoach') : t('admin.accessRoleParent')}
                      </Text>
                      <Switch
                        value={on}
                        disabled={toggle.isPending || (ADMIN_ROLE_ONLY.has(p.key) && !on)}
                        onValueChange={(enabled) => toggle.mutate({ role, key: p.key, enabled })}
                      />
                    </View>
                  );
                })}
              </View>
            </View>
          ))}
        </View>
      ))}
    </View>
  );
}

function LogTab({ catalog }: { catalog: AccessCatalog }) {
  const { t } = useTranslation();
  const name = usePermissionName();
  const log = useQuery({ queryKey: ['accessAudit'], queryFn: () => fetchAccessAudit(100) });
  const byKey = React.useMemo(() => new Map(catalog.permissions.map((p) => [p.key, p])), [catalog]);

  if (log.isLoading) return <ActivityIndicator color={colors.brand} style={{ marginTop: spacing.xl }} />;
  const rows = log.data ?? [];
  if (rows.length === 0) return <Text style={styles.help}>{t('admin.accessLogEmpty')}</Text>;

  const roleName = (r: string) =>
    r === 'coach' ? t('admin.accessRoleCoach') : r === 'parent' ? t('admin.accessRoleParent') : t('admin.accessRoleAdmin');

  return (
    <View style={styles.card}>
      {rows.map((r, i) => {
        const perm = r.permission ? (byKey.get(r.permission) ? name(byKey.get(r.permission)!) : r.permission) : '';
        const target = r.targetUserEmail ?? (r.role ? roleName(r.role) : '');
        return (
          <View key={r.id} style={[styles.logRow, i === rows.length - 1 && { borderBottomWidth: 0 }]}>
            <Text style={styles.logText}>{t(`admin.accessAction${r.action}`, { permission: perm, target })}</Text>
            <Text style={styles.logMeta}>
              {new Date(r.at).toLocaleString()}
              {r.actorEmail ? ` · ${t('admin.accessBy', { who: r.actorEmail })}` : ''}
            </Text>
          </View>
        );
      })}
    </View>
  );
}

function usePermissionName() {
  const { i18n } = useTranslation();
  const es = i18n.language.startsWith('es');
  return (p: PermissionInfo) => (es ? p.nameEs : p.nameEn);
}

function groupByArea(perms: PermissionInfo[]): [string, PermissionInfo[]][] {
  const map = new Map<string, PermissionInfo[]>();
  for (const p of perms) map.set(p.area, [...(map.get(p.area) ?? []), p]);
  return Array.from(map.entries());
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  tabs: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.lg },
  tab: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.card,
    alignItems: 'center',
  },
  tabActive: { backgroundColor: colors.brand, borderColor: colors.brand },
  tabText: { fontSize: 14, fontWeight: '700', color: colors.subtext },
  tabTextActive: { color: colors.white },
  help: { fontSize: 13, color: colors.subtext, lineHeight: 18, marginBottom: spacing.md },
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    marginBottom: spacing.md,
  },
  area: {
    fontSize: 12,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    paddingVertical: spacing.sm,
  },
  permRow: { paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  permName: { fontSize: 15, color: colors.text, fontWeight: '600' },
  switches: { flexDirection: 'row', gap: spacing.lg, marginTop: spacing.xs },
  switchCell: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  switchLabel: { fontSize: 12, color: colors.subtext },
  logRow: { paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border },
  logText: { fontSize: 14, color: colors.text },
  logMeta: { fontSize: 12, color: colors.subtext, marginTop: 2 },
});
