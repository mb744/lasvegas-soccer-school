import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import type { UserSummary } from '../../api/types'

export function AdminUsersPage() {
  const { t } = useTranslation()
  const { me } = useAuth()
  const [users, setUsers] = useState<UserSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [q, setQ] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editFirst, setEditFirst] = useState('')
  const [editLast, setEditLast] = useState('')
  const [saving, setSaving] = useState(false)

  const load = async () => {
    setError(null)
    try { setUsers(await Api.listUsers()) }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  useEffect(() => { load() }, [])

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase()
    if (!needle) return users
    return users.filter(u => `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(needle))
  }, [users, q])

  const banUser = async (u: UserSummary) => {
    if (!confirm(`Ban ${u.email}? They won't be able to log in or sign up again with this email.`)) return
    try { await Api.banUser(u.id); await load() }
    catch (e: any) { setError(e?.response?.data ?? e?.message ?? 'Error') }
  }

  const unbanUser = async (u: UserSummary) => {
    try { await Api.unbanUser(u.id); await load() }
    catch (e: any) { setError(e?.response?.data ?? e?.message ?? 'Error') }
  }

  const startEdit = (u: UserSummary) => {
    setEditingId(u.id)
    setEditFirst(u.firstName)
    setEditLast(u.lastName)
    setError(null)
  }

  const cancelEdit = () => {
    setEditingId(null)
    setEditFirst('')
    setEditLast('')
  }

  const saveEdit = async (u: UserSummary) => {
    const first = editFirst.trim()
    const last = editLast.trim()
    if (!first || !last) { setError('First and last name are required.'); return }
    setSaving(true)
    setError(null)
    try {
      await Api.updateUserProfile(u.id, { firstName: first, lastName: last })
      await load()
      cancelEdit()
    } catch (e: any) {
      setError(e?.response?.data ?? e?.message ?? 'Error')
    } finally {
      setSaving(false)
    }
  }

  const toggleAdmin = async (u: UserSummary, next: boolean) => {
    setError(null)
    // Optimistic UI so the checkbox doesn't feel laggy on slow links. Reload on response so any
    // derived flags (isBanned etc.) stay in sync.
    setUsers(prev => prev.map(x => x.id === u.id ? { ...x, isAdmin: next } : x))
    try { await Api.setUserAdmin(u.id, next); await load() }
    catch (e: any) {
      setError(e?.response?.data ?? e?.message ?? 'Error')
      // Revert on failure — server rejected (usually self-demotion guard).
      setUsers(prev => prev.map(x => x.id === u.id ? { ...x, isAdmin: !next } : x))
    }
  }

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-8">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.usersTitle')}</h1>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <div className="flex items-center justify-between gap-4">
            <h2 className="font-bold text-emerald-800">{t('admin.users')}</h2>
            <div className="flex items-center gap-3">
              <input
                type="text"
                value={q}
                onChange={e => setQ(e.target.value)}
                placeholder="Search name or email"
                className="text-sm border border-slate-300 rounded-md px-3 py-1.5 w-64"
              />
              <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
            </div>
          </div>
          <p className="text-xs text-slate-500 mt-1">{t('admin.usersHelp')}</p>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">Email</th>
                  <th className="py-2 pr-4">Name</th>
                  <th className="py-2 pr-4">Roles</th>
                  <th className="py-2 pr-4">{t('admin.usersCreated')}</th>
                  <th className="py-2 pr-4">{t('admin.usersLastLogin')}</th>
                  <th className="py-2 pr-4">{t('admin.usersRegs')}</th>
                  <th className="py-2 pr-4">{t('admin.status')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(u => {
                  const editing = editingId === u.id
                  const isSelf = me?.userId === u.id
                  const noProfile = u.parentAccountId === null
                  return (
                    <tr key={u.id} className={`border-b last:border-0 align-top ${u.isBanned ? 'bg-rose-50' : ''}`}>
                      <td className="py-2 pr-4">{u.email}</td>
                      <td className="py-2 pr-4">
                        {editing ? (
                          <div className="flex flex-col gap-1 min-w-[220px]">
                            <input
                              type="text"
                              value={editFirst}
                              onChange={e => setEditFirst(e.target.value)}
                              placeholder="First name"
                              className="border border-slate-300 rounded px-2 py-1"
                              disabled={noProfile}
                            />
                            <input
                              type="text"
                              value={editLast}
                              onChange={e => setEditLast(e.target.value)}
                              placeholder="Last name"
                              className="border border-slate-300 rounded px-2 py-1"
                              disabled={noProfile}
                            />
                            {noProfile && (
                              <span className="text-xs text-slate-500 italic">
                                Seed admin — no parent profile to rename.
                              </span>
                            )}
                          </div>
                        ) : (
                          [u.firstName, u.lastName].filter(Boolean).join(' ') || '—'
                        )}
                      </td>
                      <td className="py-2 pr-4">
                        <label className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={u.isAdmin}
                            disabled={isSelf}
                            onChange={e => toggleAdmin(u, e.target.checked)}
                          />
                          <span className={u.isAdmin
                            ? 'text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold'
                            : 'text-xs text-slate-500'}>Admin</span>
                        </label>
                        {u.isCoach && (
                          <span
                            className="mt-1 inline-block text-xs bg-sky-100 text-sky-800 rounded px-2 py-0.5 font-semibold"
                            title="Derived — set by adding this email to a team's coach card."
                          >
                            Coach
                          </span>
                        )}
                        {isSelf && (
                          <div className="text-[10px] text-slate-400 mt-1">You cannot demote yourself.</div>
                        )}
                      </td>
                      <td className="py-2 pr-4 text-slate-500 whitespace-nowrap">
                        {u.createdAt ? new Date(u.createdAt).toLocaleString() : '—'}
                      </td>
                      <td className="py-2 pr-4 text-slate-500 whitespace-nowrap">
                        {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : <span className="text-slate-400">{t('admin.usersNeverLogin')}</span>}
                      </td>
                      <td className="py-2 pr-4 text-center">{u.registrationCount}</td>
                      <td className="py-2 pr-4">
                        {u.isBanned
                          ? <span className="text-xs bg-rose-100 text-rose-800 rounded px-2 py-0.5 font-semibold">{t('admin.usersBanned')}</span>
                          : <span className="text-xs bg-emerald-50 text-emerald-700 rounded px-2 py-0.5">{t('admin.usersActive')}</span>}
                      </td>
                      <td className="py-2 pr-4 whitespace-nowrap">
                        {editing ? (
                          <div className="flex flex-col gap-1">
                            <button
                              onClick={() => saveEdit(u)}
                              disabled={saving || noProfile}
                              className="text-emerald-700 hover:underline disabled:text-slate-300 disabled:no-underline"
                            >
                              {saving ? 'Saving…' : 'Save'}
                            </button>
                            <button onClick={cancelEdit} className="text-slate-500 hover:underline">Cancel</button>
                          </div>
                        ) : (
                          <div className="flex flex-col gap-1">
                            <button onClick={() => startEdit(u)} className="text-emerald-700 hover:underline">Edit</button>
                            {u.isAdmin ? (
                              <span className="text-xs text-slate-400">—</span>
                            ) : u.isBanned ? (
                              <button onClick={() => unbanUser(u)} className="text-emerald-700 hover:underline">{t('admin.usersUnban')}</button>
                            ) : (
                              <button onClick={() => banUser(u)} className="text-rose-700 hover:underline">{t('admin.usersBan')}</button>
                            )}
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
                {filtered.length === 0 && (
                  <tr><td colSpan={8} className="py-4 text-center text-slate-400">—</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </Layout>
  )
}
