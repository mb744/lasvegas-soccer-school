import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import { TemplatesTab, DictionaryTab, SettingsTab as AutoResponseSettingsTab } from './MessagingPage'
import { AgeClassificationsSection } from './AgeClassificationsPage'
import { UniformsSection } from './UniformsSection'
import { VenuesSection } from './VenuesSection'
import { MappedFieldsSection } from './MappedFieldsSection'
import { FailedMessagesSection } from './FailedMessagesSection'
import { ChargeTypesSection } from './ChargeTypesSection'
import type { WhatsAppTemplate, EmailTemplate } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

type Tab = 'templates' | 'dictionary' | 'autoResponse' | 'ageClassifications' | 'uniforms' | 'venues' | 'chargeTypes' | 'mappedFields' | 'failedMessages' | 'backfill'

/** Top-level admin settings hub. Tabs mirror the Messaging page so the layout is consistent
 *  across admin cards. Currently houses:
 *    - WhatsApp + Email templates (moved from Messaging → Templates).
 *    - Phrase translation dictionary (moved from Messaging → Dictionary).
 *    - Auto-response settings (moved from Messaging → Settings).
 *    - Age classifications (moved from top-level admin card).
 *    - Twilio message backfill (1-30 day window) — companion to the hourly background
 *      reconciler for recovering older gaps. */
export function AdminSettingsPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('templates')
  const [templates, setTemplates] = useState<WhatsAppTemplate[]>([])
  const [emailTemplates, setEmailTemplates] = useState<EmailTemplate[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Backfill control state.
  const [backfillDays, setBackfillDays] = useState<number>(7)
  const [backfillBusy, setBackfillBusy] = useState(false)
  const [backfillResult, setBackfillResult] = useState<{
    outboundInserted: number; inboundInserted: number; message: string
  } | null>(null)

  const refreshTemplates = async () => {
    try {
      const [wa, em] = await Promise.all([Api.listWhatsAppTemplates(), Api.listEmailTemplates()])
      setTemplates(wa); setEmailTemplates(em)
    } catch (e: any) { setError(errMsg(e)) }
  }

  useEffect(() => { refreshTemplates() }, [])

  const runBackfill = async () => {
    if (backfillBusy) return
    setBackfillBusy(true); setError(null); setNotice(null); setBackfillResult(null)
    try {
      const r = await Api.reconcileTwilioMessages(backfillDays)
      setBackfillResult(r)
      setNotice(r.message)
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBackfillBusy(false) }
  }

  const tabBtn = (key: Tab, label: string) => (
    <button
      onClick={() => setTab(key)}
      className={`px-4 py-2 text-sm font-medium border-b-2 ${tab === key
        ? 'border-emerald-700 text-emerald-800'
        : 'border-transparent text-slate-500 hover:text-slate-700'}`}
    >{label}</button>
  )

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.settingsTitle')}</h1>
          <p className="text-sm text-slate-600 mt-1">{t('admin.settingsSubtitle')}</p>
        </div>

        <div className="flex flex-wrap gap-1 border-b border-slate-200">
          {tabBtn('templates', t('admin.settingsTabTemplates'))}
          {tabBtn('dictionary', t('admin.settingsTabDictionary'))}
          {tabBtn('autoResponse', t('admin.settingsTabAutoResponse'))}
          {tabBtn('ageClassifications', t('admin.settingsTabAgeClassifications'))}
          {tabBtn('uniforms', t('admin.settingsTabUniforms'))}
          {tabBtn('venues', t('admin.settingsTabVenues'))}
          {tabBtn('chargeTypes', t('admin.settingsTabChargeTypes'))}
          {tabBtn('mappedFields', t('admin.settingsTabMappedFields'))}
          {tabBtn('failedMessages', t('admin.settingsTabFailedMessages'))}
          {tabBtn('backfill', t('admin.settingsTabBackfill'))}
        </div>

        {error && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>
        )}
        {notice && (
          <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>
        )}

        {tab === 'templates' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <p className="text-xs text-slate-500 mb-3">{t('admin.settingsTemplatesBlurb')}</p>
            <TemplatesTab
              templates={templates}
              emailTemplates={emailTemplates}
              onChanged={refreshTemplates}
              onError={(e) => { setError(e); setNotice(null) }}
              onNotice={(n) => { setNotice(n); setError(null) }}
            />
          </section>
        )}

        {tab === 'dictionary' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <p className="text-xs text-slate-500 mb-3">{t('admin.settingsDictionaryBlurb')}</p>
            <DictionaryTab
              onError={(e) => { setError(e); setNotice(null) }}
              onNotice={(n) => { setNotice(n); setError(null) }}
            />
          </section>
        )}

        {tab === 'autoResponse' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <p className="text-xs text-slate-500 mb-3">{t('admin.settingsAutoResponseBlurb')}</p>
            <AutoResponseSettingsTab
              onError={(e) => { setError(e); setNotice(null) }}
              onNotice={(n) => { setNotice(n); setError(null) }}
            />
          </section>
        )}

        {tab === 'ageClassifications' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <p className="text-xs text-slate-500 mb-3">{t('admin.settingsAgeClassificationsBlurb')}</p>
            <AgeClassificationsSection
              onError={(e) => { setError(e); if (e) setNotice(null) }}
              onNotice={(n) => { setNotice(n); if (n) setError(null) }}
            />
          </section>
        )}

        {tab === 'uniforms' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <UniformsSection
              onError={(e) => { setError(e); if (e) setNotice(null) }}
              onNotice={(n) => { setNotice(n); if (n) setError(null) }}
            />
          </section>
        )}

        {tab === 'venues' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <VenuesSection
              onError={(e) => { setError(e); if (e) setNotice(null) }}
              onNotice={(n) => { setNotice(n); if (n) setError(null) }}
            />
          </section>
        )}

        {tab === 'chargeTypes' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <ChargeTypesSection
              onError={(e) => { setError(e); if (e) setNotice(null) }}
              onNotice={(n) => { setNotice(n); if (n) setError(null) }}
            />
          </section>
        )}

        {tab === 'mappedFields' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <MappedFieldsSection
              onError={(e) => { setError(e); if (e) setNotice(null) }}
              onNotice={(n) => { setNotice(n); if (n) setError(null) }}
            />
          </section>
        )}

        {tab === 'failedMessages' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <FailedMessagesSection onError={(e) => { setError(e); if (e) setNotice(null) }} />
          </section>
        )}

        {tab === 'backfill' && (
          <section className="bg-white border border-slate-200 rounded-lg p-4">
            <p className="text-xs text-slate-500 mb-3">{t('admin.settingsBackfillBlurb')}</p>
            <div className="flex flex-wrap items-end gap-3">
              <label className="block text-sm">
                <span className="text-slate-700">{t('admin.settingsBackfillDays')}</span>
                <input type="number" min={1} max={30} value={backfillDays}
                  onChange={e => setBackfillDays(Math.max(1, Math.min(30, Number(e.target.value) || 1)))}
                  className="mt-1 w-24 border border-slate-300 rounded-md px-2 py-1 text-sm" />
              </label>
              <button onClick={runBackfill} disabled={backfillBusy}
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {backfillBusy ? t('admin.settingsBackfillRunning') : t('admin.settingsBackfillRun')}
              </button>
            </div>
            {backfillResult && (
              <div className="mt-3 text-xs text-slate-700 bg-slate-50 border border-slate-200 rounded p-2">
                <div>{t('admin.settingsBackfillResult', {
                  out: backfillResult.outboundInserted,
                  inb: backfillResult.inboundInserted,
                })}</div>
                <div className="text-slate-500 mt-1">{backfillResult.message}</div>
              </div>
            )}
          </section>
        )}
      </div>
    </Layout>
  )
}
