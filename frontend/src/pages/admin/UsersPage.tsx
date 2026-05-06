import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type { UserSummary } from '../../api/types'

export function AdminUsersPage() {
  const { t } = useTranslation()
  const [users, setUsers] = useState<UserSummary[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setError(null)
    try { setUsers(await Api.listUsers()) }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  useEffect(() => { load() }, [])

  const banUser = async (u: UserSummary) => {
    if (!confirm(`Ban ${u.email}? They won't be able to log in or sign up again with this email.`)) return
    try { await Api.banUser(u.id); await load() }
    catch (e: any) { setError(e?.response?.data ?? e?.message ?? 'Error') }
  }

  const unbanUser = async (u: UserSummary) => {
    try { await Api.unbanUser(u.id); await load() }
    catch (e: any) { setError(e?.response?.data ?? e?.message ?? 'Error') }
  }

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-8">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.usersTitle')}</h1>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <div className="flex items-center justify-between">
            <h2 className="font-bold text-emerald-800">{t('admin.users')}</h2>
            <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
          </div>
          <p className="text-xs text-slate-500 mt-1">{t('admin.usersHelp')}</p>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">Email</th>
                  <th className="py-2 pr-4">Name</th>
                  <th className="py-2 pr-4">Role</th>
                  <th className="py-2 pr-4">{t('admin.usersCreated')}</th>
                  <th className="py-2 pr-4">{t('admin.usersLastLogin')}</th>
                  <th className="py-2 pr-4">{t('admin.usersRegs')}</th>
                  <th className="py-2 pr-4">{t('admin.status')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id} className={`border-b last:border-0 ${u.isBanned ? 'bg-rose-50' : ''}`}>
                    <td className="py-2 pr-4">{u.email}</td>
                    <td className="py-2 pr-4">{[u.firstName, u.lastName].filter(Boolean).join(' ') || '—'}</td>
                    <td className="py-2 pr-4">
                      {u.isAdmin
                        ? <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">Admin</span>
                        : <span className="text-xs text-slate-500">Parent</span>}
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
                      {u.isAdmin ? (
                        <span className="text-xs text-slate-400">—</span>
                      ) : u.isBanned ? (
                        <button onClick={() => unbanUser(u)} className="text-emerald-700 hover:underline">{t('admin.usersUnban')}</button>
                      ) : (
                        <button onClick={() => banUser(u)} className="text-rose-700 hover:underline">{t('admin.usersBan')}</button>
                      )}
                    </td>
                  </tr>
                ))}
                {users.length === 0 && (
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
