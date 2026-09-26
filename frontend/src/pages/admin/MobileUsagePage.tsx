import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type { MobileUsageRow } from '../../api/types'

type Filter = 'all' | 'installed' | 'notInstalled'

export function AdminMobileUsagePage() {
  const { t } = useTranslation()
  const [rows, setRows] = useState<MobileUsageRow[]>([])
  const [error, setError] = useState<string | null>(null)
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState<Filter>('all')

  const load = async () => {
    setError(null)
    try { setRows(await Api.mobileUsageReport()) }
    catch (e: any) { setError(e?.response?.data ?? e?.message ?? 'Error') }
  }

  useEffect(() => { load() }, [])

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase()
    return rows.filter(r => {
      if (filter === 'installed' && !r.hasMobileApp) return false
      if (filter === 'notInstalled' && r.hasMobileApp) return false
      if (!needle) return true
      return `${r.name} ${r.email}`.toLowerCase().includes(needle)
    })
  }, [rows, q, filter])

  const totals = useMemo(() => {
    const installed = rows.filter(r => r.hasMobileApp).length
    // Active-30d: last seen within the last 30 days.
    const cutoff = Date.now() - 30 * 24 * 60 * 60 * 1000
    const active30 = rows.filter(r => r.lastSeenAt && new Date(r.lastSeenAt).getTime() >= cutoff).length
    const withKids = rows.filter(r => r.playerCount > 0).length
    return { installed, active30, withKids, total: rows.length }
  }, [rows])

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.hubMobileUsage')}</h1>
          <p className="mt-1 text-sm text-slate-600">{t('admin.mobileUsageBlurb')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}

        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <StatCard label={t('admin.mobileUsageTotalUsers')} value={totals.total} />
          <StatCard label={t('admin.mobileUsageWithApp')} value={totals.installed} accent />
          <StatCard label={t('admin.mobileUsageActive30d')} value={totals.active30} accent />
          <StatCard label={t('admin.mobileUsageWithKids')} value={totals.withKids} />
        </div>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex gap-2">
              {(['all', 'installed', 'notInstalled'] as Filter[]).map(f => (
                <button
                  key={f}
                  onClick={() => setFilter(f)}
                  className={`text-sm px-3 py-1.5 rounded-md border ${
                    filter === f
                      ? 'bg-emerald-600 border-emerald-600 text-white font-semibold'
                      : 'bg-white border-slate-300 text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  {t(`admin.mobileUsageFilter.${f}` as const)}
                </button>
              ))}
            </div>
            <div className="flex items-center gap-3">
              <input
                type="text"
                value={q}
                onChange={e => setQ(e.target.value)}
                placeholder={t('admin.mobileUsageSearchPlaceholder')}
                className="text-sm border border-slate-300 rounded-md px-3 py-1.5 w-64"
              />
              <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
            </div>
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">{t('admin.mobileUsageColUser')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColKids')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColApp')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColPlatform')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColNotifications')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColInstalled')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColLastSeen')}</th>
                  <th className="py-2 pr-4">{t('admin.mobileUsageColLastMobileLogin')}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(r => (
                  <tr key={r.userId} className="border-b last:border-0 align-top">
                    <td className="py-2 pr-4">
                      <div className="font-semibold text-slate-800">{r.name || '—'}</div>
                      <div className="text-xs text-slate-500">{r.email}</div>
                    </td>
                    <td className="py-2 pr-4 text-center">{r.playerCount}</td>
                    <td className="py-2 pr-4">
                      {r.hasMobileApp
                        ? <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">{t('admin.mobileUsageInstalled')}</span>
                        : <span className="text-xs text-slate-400">{t('admin.mobileUsageNotInstalled')}</span>}
                    </td>
                    <td className="py-2 pr-4 whitespace-nowrap">
                      {r.hasIos && <span className="text-xs bg-sky-50 text-sky-700 rounded px-2 py-0.5 mr-1"></span>}
                      {r.hasAndroid && <span className="text-xs bg-lime-50 text-lime-700 rounded px-2 py-0.5"></span>}
                      {!r.hasIos && !r.hasAndroid && <span className="text-slate-300">—</span>}
                      {r.deviceCount > 1 && (
                        <span className="text-xs text-slate-500 ml-2">×{r.deviceCount}</span>
                      )}
                      {r.appVersion && <div className="text-xs text-slate-500 mt-0.5">v{r.appVersion}</div>}
                    </td>
                    <td className="py-2 pr-4 whitespace-nowrap">
                      {r.hasMobileApp ? <PushStatus row={r} /> : <span className="text-slate-300">—</span>}
                    </td>
                    <td className="py-2 pr-4 text-slate-600 whitespace-nowrap">{fmt(r.firstInstalledAt)}</td>
                    <td className="py-2 pr-4 text-slate-600 whitespace-nowrap">{fmt(r.lastSeenAt)}</td>
                    <td className="py-2 pr-4 text-slate-600 whitespace-nowrap">{fmt(r.lastMobileLoginAt)}</td>
                  </tr>
                ))}
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

function PushStatus({ row }: { row: MobileUsageRow }) {
  const { t } = useTranslation()
  if (row.pushEnabled)
    return <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">{t('admin.mobileUsagePushOn')}</span>
  if (row.pushPermission === 'denied')
    return <span className="text-xs bg-amber-100 text-amber-800 rounded px-2 py-0.5">{t('admin.mobileUsagePushDenied')}</span>
  if (row.pushError)
    return <span className="text-xs bg-rose-100 text-rose-800 rounded px-2 py-0.5" title={row.pushError}>{t('admin.mobileUsagePushError')}</span>
  if (row.pushPermission === 'undetermined')
    return <span className="text-xs text-slate-500">{t('admin.mobileUsagePushNotAsked')}</span>
  return <span className="text-xs text-slate-400">{t('admin.mobileUsagePushUnknown')}</span>
}

function fmt(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString()
}

function StatCard({ label, value, accent = false }: { label: string; value: number; accent?: boolean }) {
  return (
    <div className={`rounded-lg border p-4 ${accent ? 'bg-emerald-50 border-emerald-200' : 'bg-white border-slate-200'}`}>
      <div className="text-xs uppercase font-semibold text-slate-500">{label}</div>
      <div className={`text-2xl font-bold mt-1 ${accent ? 'text-emerald-800' : 'text-slate-800'}`}>{value}</div>
    </div>
  )
}
