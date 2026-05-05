import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'
import {
  OUTREACH_STATUS_LABELS,
  type OutreachResponse,
  type Language,
  type RegistrationSummary,
  type RegistrationDetail,
  type RegistrationPlayerDetail,
} from '../api/types'

type Channel = 'email' | 'sms'

export function AdminPage() {
  const { t } = useTranslation()
  const [channel, setChannel] = useState<Channel>('email')
  const [recipient, setRecipient] = useState('')
  const [language, setLanguage] = useState<Language>(0)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [outreach, setOutreach] = useState<OutreachResponse[]>([])
  const [registrations, setRegistrations] = useState<RegistrationSummary[]>([])
  const [copiedId, setCopiedId] = useState<number | null>(null)
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [details, setDetails] = useState<Record<number, RegistrationDetail>>({})
  const [loadingDetail, setLoadingDetail] = useState<number | null>(null)

  const loadAll = async () => {
    setError(null)
    try {
      const [out, regs] = await Promise.all([Api.listOutreach(), Api.listRegistrations()])
      setOutreach(out)
      setRegistrations(regs)
    } catch (e: any) {
      setError(e?.message ?? 'Error')
    }
  }

  useEffect(() => { loadAll() }, [])

  const toggleDetail = async (id: number) => {
    if (expandedId === id) { setExpandedId(null); return }
    setExpandedId(id)
    if (!details[id]) {
      setLoadingDetail(id)
      try {
        const d = await Api.getRegistration(id)
        setDetails(prev => ({ ...prev, [id]: d }))
      } catch {
        setError('Could not load registration details.')
      } finally {
        setLoadingDetail(null)
      }
    }
  }

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!recipient.trim()) {
      setError(channel === 'email' ? 'Enter an email address.' : 'Enter a phone number.')
      return
    }
    setSending(true)
    setError(null)
    try {
      await Api.createOutreach({
        email: channel === 'email' ? recipient.trim() : undefined,
        phone: channel === 'sms' ? recipient.trim() : undefined,
        language,
      })
      setRecipient('')
      await loadAll()
    } catch (e: any) {
      const status = e?.response?.status
      if (status === 401) setError('Session expired. Please reload and sign in again.')
      else if (status === 403) setError('Admin role required.')
      else if (e?.code === 'ERR_NETWORK') setError('Cannot reach the API.')
      else setError(e?.response?.data?.title ?? e?.response?.data ?? e?.message ?? 'Error')
    } finally {
      setSending(false)
    }
  }

  const copyLink = async (o: OutreachResponse) => {
    await navigator.clipboard.writeText(o.link)
    setCopiedId(o.id)
    setTimeout(() => setCopiedId(c => (c === o.id ? null : c)), 1500)
  }

  const resend = async (id: number) => {
    setError(null)
    try { await Api.resendOutreach(id); await loadAll() }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  const deleteRegistration = async (id: number) => {
    if (!confirm(t('admin.confirmDelete'))) return
    try {
      await Api.deleteRegistration(id)
      setRegistrations(prev => prev.filter(r => r.id !== id))
      if (expandedId === id) setExpandedId(null)
    } catch (e: any) { setError(e?.message ?? 'Error') }
  }

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-8">
        <h1 className="text-3xl font-bold text-emerald-800">{t('admin.title')}</h1>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="font-bold text-emerald-800">{t('admin.send')}</h2>
          <form onSubmit={send} className="mt-4 grid sm:grid-cols-2 gap-4">
            <label className="flex flex-col text-sm sm:col-span-2">
              <span className="font-medium text-slate-700 mb-1">{t('admin.deliverBy')}</span>
              <div className="flex gap-2">
                <button type="button"
                  className={`px-3 py-2 rounded-md border ${channel === 'email' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300'}`}
                  onClick={() => setChannel('email')}>{t('admin.email')}</button>
                <button type="button"
                  className={`px-3 py-2 rounded-md border ${channel === 'sms' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300'}`}
                  onClick={() => setChannel('sms')}>{t('admin.sms')}</button>
              </div>
            </label>

            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">
                {channel === 'email' ? t('admin.emailAddress') : t('admin.phoneNumber')}
              </span>
              <input
                type={channel === 'email' ? 'email' : 'tel'}
                value={recipient}
                onChange={e => setRecipient(e.target.value)}
                required
                className="border border-slate-300 rounded-md px-3 py-2"
              />
            </label>

            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('admin.language')}</span>
              <select
                value={language}
                onChange={e => setLanguage(Number(e.target.value) as Language)}
                className="border border-slate-300 rounded-md px-3 py-2"
              >
                <option value={0}>English</option>
                <option value={1}>Español</option>
              </select>
            </label>

            <div className="sm:col-span-2 flex items-center gap-3">
              <button
                type="submit"
                disabled={sending}
                className="bg-emerald-700 text-white font-semibold px-5 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
              >
                {sending ? t('admin.sending') : t('admin.send')}
              </button>
            </div>
          </form>
          {error && <div className="mt-4 text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        </section>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <div className="flex items-center justify-between">
            <h2 className="font-bold text-emerald-800">{t('admin.recent')}</h2>
            <button onClick={loadAll} className="text-sm text-emerald-700 hover:underline">↻</button>
          </div>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">{t('admin.sentTo')}</th>
                  <th className="py-2 pr-4">Lang</th>
                  <th className="py-2 pr-4">{t('admin.status')}</th>
                  <th className="py-2 pr-4">{t('admin.created')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {outreach.map(o => (
                  <tr key={o.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{o.email ?? o.phone}</td>
                    <td className="py-2 pr-4">{o.language === 1 ? 'ES' : 'EN'}</td>
                    <td className="py-2 pr-4">
                      <span className={statusBadge(o.status)}>{OUTREACH_STATUS_LABELS[o.status]}</span>
                      {o.statusMessage && (
                        <span className="block text-xs text-slate-500 mt-1">{o.statusMessage}</span>
                      )}
                    </td>
                    <td className="py-2 pr-4 text-slate-500 whitespace-nowrap">
                      {new Date(o.createdAt).toLocaleString()}
                    </td>
                    <td className="py-2 pr-4 whitespace-nowrap">
                      <button onClick={() => copyLink(o)} className="text-emerald-700 hover:underline">
                        {copiedId === o.id ? t('admin.copied') : t('admin.copy')}
                      </button>
                      <span className="mx-2 text-slate-300">|</span>
                      <button onClick={() => resend(o.id)} className="text-emerald-700 hover:underline">
                        {t('admin.resend')}
                      </button>
                    </td>
                  </tr>
                ))}
                {outreach.length === 0 && (
                  <tr><td colSpan={5} className="py-4 text-center text-slate-400">—</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </section>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="font-bold text-emerald-800">{t('admin.registrations')}</h2>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">Season</th>
                  <th className="py-2 pr-4">Parent</th>
                  <th className="py-2 pr-4">Email</th>
                  <th className="py-2 pr-4">Phone</th>
                  <th className="py-2 pr-4">Players</th>
                  <th className="py-2 pr-4">Lang</th>
                  <th className="py-2 pr-4">Created</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {registrations.map(r => (
                  <RegistrationRow
                    key={r.id}
                    r={r}
                    expanded={expandedId === r.id}
                    detail={details[r.id]}
                    loading={loadingDetail === r.id}
                    onToggle={() => toggleDetail(r.id)}
                    onDelete={() => deleteRegistration(r.id)}
                  />
                ))}
                {registrations.length === 0 && (
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

function RegistrationRow({
  r, expanded, detail, loading, onToggle, onDelete,
}: {
  r: RegistrationSummary
  expanded: boolean
  detail: RegistrationDetail | undefined
  loading: boolean
  onToggle: () => void
  onDelete: () => void
}) {
  const { t } = useTranslation()
  return (
    <>
      <tr className="border-b last:border-0">
        <td className="py-2 pr-4 font-mono text-xs">{r.season}</td>
        <td className="py-2 pr-4 font-medium">{r.parentFirstName} {r.parentLastName}</td>
        <td className="py-2 pr-4">{r.email}</td>
        <td className="py-2 pr-4">{r.cellPhone}</td>
        <td className="py-2 pr-4">{r.playerCount}</td>
        <td className="py-2 pr-4">{r.language === 1 ? 'ES' : 'EN'}</td>
        <td className="py-2 pr-4 text-slate-500 whitespace-nowrap">{new Date(r.createdAt).toLocaleString()}</td>
        <td className="py-2 pr-4 whitespace-nowrap">
          <button onClick={onToggle} className="text-emerald-700 hover:underline">
            {expanded ? t('admin.hide') : t('admin.details')}
          </button>
          <span className="mx-2 text-slate-300">|</span>
          <button onClick={onDelete} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
        </td>
      </tr>
      {expanded && (
        <tr className="border-b last:border-0 bg-slate-50">
          <td colSpan={8} className="p-4">
            {loading && <span className="text-slate-500">{t('common.loading')}</span>}
            {!loading && detail && <RegistrationDetailPanel detail={detail} />}
          </td>
        </tr>
      )}
    </>
  )
}

function RegistrationDetailPanel({ detail }: { detail: RegistrationDetail }) {
  const { t } = useTranslation()
  const r = detail
  const fullAddress = [
    r.addressLine1,
    r.addressLine2,
    `${r.city}, ${r.state} ${r.postalCode}`,
  ].filter(Boolean).join(' • ')

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-md border border-slate-200 p-4">
        <h3 className="font-bold text-emerald-800 mb-2">{t('admin.parentInfo')}</h3>
        <dl className="grid sm:grid-cols-2 gap-x-6 gap-y-1 text-sm">
          <Definition label={t('admin.parentInfo')} value={`${r.parentFirstName} ${r.parentLastName}`} />
          <Definition label={t('admin.address')} value={fullAddress} />
          <Definition label={t('admin.phone')} value={r.cellPhone} />
          <Definition label={t('admin.emailLbl')} value={r.email} />
          <Definition label="Season" value={r.season} />
          <Definition label={t('admin.submitted')} value={new Date(r.createdAt).toLocaleString()} />
          <Definition
            label={t('admin.waiverSignedAt')}
            value={r.waiverSignedAt ? new Date(r.waiverSignedAt).toLocaleString() : '—'}
          />
        </dl>
        <div className="mt-3">
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => Api.viewWaivers(r.id)}
              className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800"
            >
              {t('admin.viewAllWaivers')}
            </button>
            <button
              onClick={() => Api.downloadWaivers(r.id)}
              className="text-sm bg-white border border-emerald-700 text-emerald-700 px-3 py-1.5 rounded-md hover:bg-emerald-50"
            >
              {t('admin.downloadAllWaivers')}
            </button>
          </div>
          <p className="text-xs text-slate-500 mt-1.5">{t('admin.packetHelp')}</p>
        </div>
      </div>

      <div>
        <h3 className="font-bold text-emerald-800 mb-2">{t('admin.playersHeading')}</h3>
        <div className="grid sm:grid-cols-2 gap-3">
          {detail.players.map((p, idx) => (
            <PlayerCard key={p.id} regId={detail.id} idx={idx + 1} p={p} />
          ))}
        </div>
      </div>
    </div>
  )
}

function PlayerCard({ regId, idx, p }: { regId: number; idx: number; p: RegistrationPlayerDetail }) {
  const { t } = useTranslation()
  const stem = `${regId}-${p.lastName}-${p.firstName}`.replace(/[^a-zA-Z0-9-_]/g, '')
  return (
    <div className="bg-white rounded-md border border-slate-200 p-4 text-sm">
      <div className="flex items-center justify-between mb-2">
        <h4 className="font-semibold text-slate-700">
          {t('admin.playerLabel')} {idx}: {p.firstName} {p.lastName}
        </h4>
        {p.hasSignature ? (
          <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">✓ Signed</span>
        ) : (
          <span className="text-xs bg-amber-100 text-amber-800 rounded px-2 py-0.5">{t('admin.notSigned')}</span>
        )}
      </div>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
        <Definition label={t('admin.dob')} value={p.dateOfBirth} />
        <Definition label={t('admin.grade')} value={p.schoolGrade} />
        <Definition label={t('admin.sizes')} value={`${p.uniformSize} / ${p.shoeSize}`} />
        {p.waiverTeamName && <Definition label={t('admin.teamLbl')} value={p.waiverTeamName} />}
        {p.heardFrom && <Definition label={t('admin.heardFromLbl')} value={p.heardFrom} />}
      </dl>
      <div className="mt-3 flex flex-wrap gap-2">
        <button
          onClick={() => Api.viewPlayerWaiver(regId, p.id)}
          disabled={!p.hasSignature}
          className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50"
        >
          {t('admin.view')}
        </button>
        <button
          onClick={() => Api.downloadPlayerWaiver(regId, p.id, stem)}
          disabled={!p.hasSignature}
          className="text-xs bg-white border border-emerald-700 text-emerald-700 px-3 py-1.5 rounded-md hover:bg-emerald-50 disabled:opacity-50"
        >
          {t('admin.download')}
        </button>
      </div>
    </div>
  )
}

function Definition({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-slate-900">{value || '—'}</dd>
    </>
  )
}

function statusBadge(status: number) {
  const base = 'inline-block text-xs font-semibold rounded px-2 py-0.5'
  switch (status) {
    case 0: return `${base} bg-slate-100 text-slate-700`        // Pending
    case 1: return `${base} bg-blue-100 text-blue-800`          // Sent
    case 2: return `${base} bg-amber-100 text-amber-800`        // AccountCreated
    case 3: return `${base} bg-emerald-100 text-emerald-800`    // Registered
    case 4: return `${base} bg-rose-100 text-rose-800`          // Failed
    default: return base
  }
}
