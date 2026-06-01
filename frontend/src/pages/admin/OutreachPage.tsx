import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { Api } from '../../api/client'
import { OUTREACH_STATUS_LABELS, type Language, type OutreachResponse } from '../../api/types'

type Channel = 'email' | 'sms'

export function AdminOutreachPage() {
  const { t } = useTranslation()
  const [channel, setChannel] = useState<Channel>('email')
  const [recipient, setRecipient] = useState('')
  const [language, setLanguage] = useState<Language>(0)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [outreach, setOutreach] = useState<OutreachResponse[]>([])
  const [copiedId, setCopiedId] = useState<number | null>(null)
  const v = useRequiredValidation(['recipient'])

  const load = async () => {
    setError(null)
    try { setOutreach(await Api.listOutreach()) }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  useEffect(() => { load() }, [])

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!v.checkSubmit({ recipient })) {
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
      await load()
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
    try { await Api.resendOutreach(id); await load() }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-8">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.outreachTitle')}</h1>
        </div>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="font-bold text-emerald-800">{t('admin.send')}</h2>
          <form onSubmit={send} noValidate className="mt-4 grid sm:grid-cols-2 gap-4">
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
              <RequiredLabel className="font-medium text-slate-700 mb-1">
                {channel === 'email' ? t('admin.emailAddress') : t('admin.phoneNumber')}
              </RequiredLabel>
              <input
                ref={v.register('recipient')}
                type={channel === 'email' ? 'email' : 'tel'}
                value={recipient}
                onChange={e => setRecipient(e.target.value)}
                onBlur={e => v.onFieldBlur('recipient', e.target.value)}
                className={`border border-slate-300 rounded-md px-3 py-2 ${v.fieldCls('recipient')}`}
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
            <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
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
      </div>
    </Layout>
  )
}

function statusBadge(status: number) {
  const base = 'inline-block text-xs font-semibold rounded px-2 py-0.5'
  switch (status) {
    case 0: return `${base} bg-slate-100 text-slate-700`
    case 1: return `${base} bg-blue-100 text-blue-800`
    case 2: return `${base} bg-amber-100 text-amber-800`
    case 3: return `${base} bg-emerald-100 text-emerald-800`
    case 4: return `${base} bg-rose-100 text-rose-800`
    default: return base
  }
}
