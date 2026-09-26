import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import { can } from '../../auth/can'
import type {
  AccessCatalog,
  EditableRole,
  PermissionAuditEntry,
  PermissionInfo,
  UserAccess,
  UserGrants,
  UserSummary,
} from '../../api/types'

type Tab = 'roles' | 'people' | 'log'

/** Mirrors Permissions.AdminRoleOnly on the server: never switched on for Coach or Parent. */
const ADMIN_ROLE_ONLY = new Set(['admin.access', 'users.manage', 'roles.manage'])

/** Admin screen for role permissions, per-person grants and the change log. Open to anyone with
 *  roles.manage or users.manage; each tab gates its own edits on the matching permission. */
export function AdminAccessPage() {
  const { t } = useTranslation()
  const { me } = useAuth()
  const [params, setParams] = useSearchParams()
  const selectedUserId = params.get('user')
  const canRoles = can(me, 'roles.manage')
  const canUsers = can(me, 'users.manage')

  const tab: Tab = selectedUserId ? 'people' : ((params.get('tab') as Tab | null) ?? (canRoles ? 'roles' : 'people'))
  const setTab = (next: Tab) => setParams(next === 'roles' ? {} : { tab: next })

  const [catalog, setCatalog] = useState<AccessCatalog | null>(null)
  const [error, setError] = useState<string | null>(null)

  const loadCatalog = useCallback(async () => {
    try { setCatalog(await Api.accessCatalog()) }
    catch (e: any) { setError(errorText(e)) }
  }, [])
  useEffect(() => { loadCatalog() }, [loadCatalog])

  const tabs: Tab[] = [...(canRoles ? ['roles' as Tab] : []), ...(canUsers ? ['people' as Tab] : []), 'log']

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.hubAccess')}</h1>
          <p className="mt-1 text-sm text-slate-600">{t('admin.accessBlurb')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}

        <div className="flex gap-2 border-b border-slate-200">
          {tabs.map(k => (
            <button
              key={k}
              onClick={() => setTab(k)}
              className={`px-4 py-2 text-sm font-semibold -mb-px border-b-2 ${
                tab === k ? 'border-emerald-600 text-emerald-800' : 'border-transparent text-slate-500 hover:text-slate-700'
              }`}
            >
              {t(`admin.accessTab.${k}` as const)}
            </button>
          ))}
        </div>

        {!catalog ? (
          <div className="text-sm text-slate-500">{t('common.loading')}</div>
        ) : tab === 'roles' && canRoles ? (
          <RolesTab catalog={catalog} onChanged={setCatalog} onError={setError} />
        ) : tab === 'people' && canUsers ? (
          <PeopleTab
            catalog={catalog}
            selectedUserId={selectedUserId}
            onSelect={id => setParams(id ? { user: id } : { tab: 'people' })}
            onError={setError}
          />
        ) : (
          <LogTab catalog={catalog} onError={setError} />
        )}
      </div>
    </Layout>
  )
}

// ---- Roles ----

function RolesTab({ catalog, onChanged, onError }: {
  catalog: AccessCatalog
  onChanged: (c: AccessCatalog) => void
  onError: (msg: string | null) => void
}) {
  const { t } = useTranslation()
  const name = usePermissionName()
  const [busy, setBusy] = useState<string | null>(null)
  const areas = useMemo(() => groupByArea(catalog.permissions), [catalog])

  const toggle = async (role: EditableRole, key: string, enabled: boolean) => {
    onError(null)
    setBusy(`${role}:${key}`)
    const apply = (on: boolean): AccessCatalog => ({
      ...catalog,
      [role]: on ? [...catalog[role], key] : catalog[role].filter(k => k !== key),
    })
    onChanged(apply(enabled))
    try {
      await Api.setRolePermission(role, key, enabled)
    } catch (e: any) {
      onChanged(apply(!enabled))
      onError(errorText(e))
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
      <p className="text-sm text-slate-600">{t('admin.accessRolesHelp')}</p>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-2 pr-4">{t('admin.accessColPermission')}</th>
              <th className="py-2 px-3 text-center">{t('admin.accessRole.admin')}</th>
              <th className="py-2 px-3 text-center">{t('admin.accessRole.coach')}</th>
              <th className="py-2 px-3 text-center">{t('admin.accessRole.parent')}</th>
            </tr>
          </thead>
          <tbody>
            {areas.map(([area, perms]) => (
              <AreaRows key={area} area={area} colSpan={4}>
                {perms.map(p => (
                  <tr key={p.key} className="border-b last:border-0">
                    <td className="py-2 pr-4">
                      <div className="text-slate-800">{name(p)}</div>
                      <div className="text-xs text-slate-400 font-mono">{p.key}
                        {p.grantable && <span className="ml-2 font-sans text-amber-700">· {t('admin.accessGrantableNote')}</span>}
                      </div>
                    </td>
                    <td className="py-2 px-3 text-center">
                      <input type="checkbox" checked disabled title={t('admin.accessAdminAlways')} />
                    </td>
                    {(['coach', 'parent'] as EditableRole[]).map(role => (
                      <td key={role} className="py-2 px-3 text-center">
                        <input
                          type="checkbox"
                          checked={catalog[role].includes(p.key)}
                          disabled={busy !== null || (ADMIN_ROLE_ONLY.has(p.key) && !catalog[role].includes(p.key))}
                          onChange={e => toggle(role, p.key, e.target.checked)}
                          aria-label={`${t(`admin.accessRole.${role}` as const)}: ${name(p)}`}
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </AreaRows>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

// ---- People ----

function PeopleTab({ catalog, selectedUserId, onSelect, onError }: {
  catalog: AccessCatalog
  selectedUserId: string | null
  onSelect: (id: string | null) => void
  onError: (msg: string | null) => void
}) {
  const { t } = useTranslation()
  const name = usePermissionName()
  const [holders, setHolders] = useState<UserGrants[] | null>(null)
  const [users, setUsers] = useState<UserSummary[] | null>(null)
  const [q, setQ] = useState('')
  const byKey = useMemo(() => new Map(catalog.permissions.map(p => [p.key, p])), [catalog])

  const loadHolders = useCallback(async () => {
    try { setHolders(await Api.listAccessGrants()) }
    catch (e: any) { onError(errorText(e)) }
  }, [onError])
  useEffect(() => { loadHolders() }, [loadHolders])

  useEffect(() => {
    if (!q.trim() || users) return
    Api.listUsers().then(setUsers).catch((e: any) => onError(errorText(e)))
  }, [q, users, onError])

  const matches = useMemo(() => {
    const needle = q.trim().toLowerCase()
    if (!needle || !users) return []
    return users
      .filter(u => !u.isBanned && `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(needle))
      .slice(0, 8)
  }, [q, users])

  return (
    <div className="grid lg:grid-cols-5 gap-6">
      <section className="lg:col-span-2 bg-white border border-slate-200 rounded-lg p-6 space-y-4">
        <div>
          <label className="text-sm font-semibold text-slate-700">{t('admin.accessFindPerson')}</label>
          <input
            type="text"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder={t('admin.mobileUsageSearchPlaceholder')}
            className="mt-1 w-full text-sm border border-slate-300 rounded-md px-3 py-1.5"
          />
          {matches.length > 0 && (
            <ul className="mt-2 border border-slate-200 rounded-md divide-y">
              {matches.map(u => (
                <li key={u.id}>
                  <button
                    onClick={() => { onSelect(u.id); setQ('') }}
                    className="w-full text-left px-3 py-2 hover:bg-slate-50"
                  >
                    <div className="text-sm font-semibold text-slate-800">{`${u.firstName} ${u.lastName}`.trim() || u.email}</div>
                    <div className="text-xs text-slate-500">{u.email}</div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div>
          <h2 className="font-bold text-emerald-800">{t('admin.accessHoldersTitle')}</h2>
          <p className="text-xs text-slate-500 mt-1">{t('admin.accessHoldersHelp')}</p>
          {!holders ? (
            <div className="text-sm text-slate-500 mt-3">{t('common.loading')}</div>
          ) : holders.length === 0 ? (
            <div className="text-sm text-slate-400 mt-3">{t('admin.accessHoldersNone')}</div>
          ) : (
            <ul className="mt-3 divide-y border border-slate-200 rounded-md">
              {holders.map(h => (
                <li key={h.userId}>
                  <button
                    onClick={() => onSelect(h.userId)}
                    className={`w-full text-left px-3 py-2 hover:bg-slate-50 ${selectedUserId === h.userId ? 'bg-emerald-50' : ''}`}
                  >
                    <div className="text-sm font-semibold text-slate-800">{h.name}</div>
                    <div className="text-xs text-slate-500">{h.email}</div>
                    <div className="mt-1 flex flex-wrap gap-1">
                      {h.grants.map(g => (
                        <span key={g} className="text-xs bg-amber-100 text-amber-800 rounded px-2 py-0.5">
                          {byKey.get(g) ? name(byKey.get(g)!) : g}
                        </span>
                      ))}
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      <section className="lg:col-span-3 bg-white border border-slate-200 rounded-lg p-6">
        {selectedUserId ? (
          <UserAccessPanel
            key={selectedUserId}
            userId={selectedUserId}
            catalog={catalog}
            onChanged={loadHolders}
            onClose={() => onSelect(null)}
            onError={onError}
          />
        ) : (
          <div className="text-sm text-slate-400">{t('admin.accessPickPerson')}</div>
        )}
      </section>
    </div>
  )
}

function UserAccessPanel({ userId, catalog, onChanged, onClose, onError }: {
  userId: string
  catalog: AccessCatalog
  onChanged: () => void
  onClose: () => void
  onError: (msg: string | null) => void
}) {
  const { t } = useTranslation()
  const name = usePermissionName()
  const [access, setAccess] = useState<UserAccess | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setAccess(await Api.userAccess(userId)) }
    catch (e: any) { onError(errorText(e)) }
  }, [userId, onError])
  useEffect(() => { load() }, [load])

  const toggle = async (key: string, enabled: boolean) => {
    onError(null)
    setBusy(true)
    try {
      await Api.setUserGrant(userId, key, enabled)
      await load()
      onChanged()
    } catch (e: any) {
      onError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  if (!access) return <div className="text-sm text-slate-500">{t('common.loading')}</div>

  const grantable = catalog.permissions.filter(p => p.grantable)
  const effective = new Set(access.effective)
  const areas = groupByArea(catalog.permissions.filter(p => effective.has(p.key)))

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-slate-800">{access.email}</h2>
          <div className="mt-1 flex flex-wrap gap-1">
            {access.roles.map(r => (
              <span key={r} className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">
                {t(`admin.accessRole.${r}` as 'admin.accessRole.admin', r)}
              </span>
            ))}
            {access.isCoach && (
              <span className="text-xs text-slate-500">{t('admin.accessCoachTeams', { count: access.coachTeamIds.length })}</span>
            )}
          </div>
        </div>
        <button onClick={onClose} className="text-sm text-slate-500 hover:text-slate-700">✕</button>
      </div>

      <div>
        <h3 className="font-semibold text-slate-700">{t('admin.accessExtraTitle')}</h3>
        {access.isAdmin ? (
          <p className="text-sm text-slate-500 mt-1">{t('admin.accessAdminAlways')}</p>
        ) : (
          <>
            <p className="text-xs text-slate-500 mt-1">{t('admin.accessExtraHelp')}</p>
            <div className="mt-2 space-y-2">
              {grantable.map(p => {
                const granted = access.grants.includes(p.key)
                const fromRole = effective.has(p.key) && !granted
                return (
                  <label key={p.key} className="flex items-start gap-2 text-sm">
                    <input
                      type="checkbox"
                      className="mt-0.5"
                      checked={granted}
                      disabled={busy}
                      onChange={e => toggle(p.key, e.target.checked)}
                    />
                    <span>
                      <span className="text-slate-800">{name(p)}</span>
                      {fromRole && <span className="ml-2 text-xs text-slate-500">({t('admin.accessAlreadyFromRole')})</span>}
                    </span>
                  </label>
                )
              })}
            </div>
            {!access.isCoach && (
              <p className="text-xs text-amber-700 mt-2">{t('admin.accessNotCoachWarning')}</p>
            )}
          </>
        )}
      </div>

      <div>
        <h3 className="font-semibold text-slate-700">{t('admin.accessEffectiveTitle')}</h3>
        {areas.length === 0 ? (
          <p className="text-sm text-slate-400 mt-1">—</p>
        ) : (
          <div className="mt-2 space-y-2">
            {areas.map(([area, perms]) => (
              <div key={area}>
                <div className="text-xs uppercase font-semibold text-slate-500">{areaName(t, area)}</div>
                <div className="mt-1 flex flex-wrap gap-1">
                  {perms.map(p => (
                    <span
                      key={p.key}
                      className={`text-xs rounded px-2 py-0.5 ${access.grants.includes(p.key) ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-700'}`}
                    >
                      {name(p)}
                    </span>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

// ---- Change log ----

function LogTab({ catalog, onError }: { catalog: AccessCatalog; onError: (msg: string | null) => void }) {
  const { t } = useTranslation()
  const name = usePermissionName()
  const [rows, setRows] = useState<PermissionAuditEntry[] | null>(null)
  const byKey = useMemo(() => new Map(catalog.permissions.map(p => [p.key, p])), [catalog])

  useEffect(() => {
    Api.accessAudit().then(setRows).catch((e: any) => onError(errorText(e)))
  }, [onError])

  if (!rows) return <div className="text-sm text-slate-500">{t('common.loading')}</div>

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-6">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-2 pr-4">{t('admin.accessColWhen')}</th>
              <th className="py-2 pr-4">{t('admin.accessColWho')}</th>
              <th className="py-2 pr-4">{t('admin.accessColChange')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(r => {
              const perm = r.permission ? (byKey.get(r.permission) ? name(byKey.get(r.permission)!) : r.permission) : ''
              const target = r.targetUserEmail ?? (r.role ? t(`admin.accessRole.${r.role}` as 'admin.accessRole.admin', r.role) : '')
              return (
                <tr key={r.id} className="border-b last:border-0 align-top">
                  <td className="py-2 pr-4 whitespace-nowrap text-slate-600">{new Date(r.at).toLocaleString()}</td>
                  <td className="py-2 pr-4 text-slate-700">{r.actorEmail ?? '—'}</td>
                  <td className="py-2 pr-4 text-slate-800">
                    {t(`admin.accessAction.${r.action}` as const, { permission: perm, target })}
                  </td>
                </tr>
              )
            })}
            {rows.length === 0 && (
              <tr><td colSpan={3} className="py-4 text-center text-slate-400">{t('admin.accessLogEmpty')}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}

// ---- helpers ----

function AreaRows({ area, colSpan, children }: { area: string; colSpan: number; children: React.ReactNode }) {
  const { t } = useTranslation()
  return (
    <>
      <tr className="bg-slate-50">
        <td colSpan={colSpan} className="py-1.5 px-2 text-xs uppercase font-semibold text-slate-500">{areaName(t, area)}</td>
      </tr>
      {children}
    </>
  )
}

function groupByArea(perms: PermissionInfo[]): [string, PermissionInfo[]][] {
  const map = new Map<string, PermissionInfo[]>()
  for (const p of perms) {
    const list = map.get(p.area) ?? []
    list.push(p)
    map.set(p.area, list)
  }
  return Array.from(map.entries())
}

function usePermissionName() {
  const { i18n } = useTranslation()
  const es = i18n.language.startsWith('es')
  return (p: PermissionInfo) => (es ? p.nameEs : p.nameEn)
}

function areaName(t: (key: string, fallback: string) => string, area: string): string {
  return t(`admin.accessArea.${area}`, area.charAt(0).toUpperCase() + area.slice(1))
}

function errorText(e: any): string {
  const data = e?.response?.data
  if (typeof data === 'string' && data) return data
  return e?.message ?? 'Error'
}
