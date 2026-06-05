import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import { TemplatesTab } from './MessagingPage'
import type { WhatsAppTemplate, EmailTemplate } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/** Top-level admin settings hub. Currently houses the WhatsApp + Email templates editor that
 *  used to live as a tab inside the Messaging page. Future per-admin configuration sections
 *  (sender numbers, default language, etc.) will land here too — kept separate from Messaging
 *  so the day-to-day Compose/Inbox flow isn't cluttered with config tabs. */
export function AdminSettingsPage() {
  const { t } = useTranslation()
  const [templates, setTemplates] = useState<WhatsAppTemplate[]>([])
  const [emailTemplates, setEmailTemplates] = useState<EmailTemplate[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const refresh = async () => {
    try {
      const [wa, em] = await Promise.all([Api.listWhatsAppTemplates(), Api.listEmailTemplates()])
      setTemplates(wa); setEmailTemplates(em)
    } catch (e: any) { setError(errMsg(e)) }
  }

  useEffect(() => { refresh() }, [])

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-8 space-y-4">
        <header>
          <h1 className="text-2xl font-bold text-emerald-800">{t('admin.settingsTitle')}</h1>
          <p className="mt-1 text-sm text-slate-500">{t('admin.settingsSubtitle')}</p>
        </header>

        {error && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>
        )}
        {notice && (
          <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>
        )}

        <section className="bg-white border border-slate-200 rounded-lg p-4">
          <h2 className="text-lg font-bold text-emerald-800 mb-2">{t('admin.settingsTemplatesSection')}</h2>
          <p className="text-xs text-slate-500 mb-3">{t('admin.settingsTemplatesBlurb')}</p>
          <TemplatesTab
            templates={templates}
            emailTemplates={emailTemplates}
            onChanged={refresh}
            onError={(e) => { setError(e); setNotice(null) }}
            onNotice={(n) => { setNotice(n); setError(null) }}
          />
        </section>
      </div>
    </Layout>
  )
}
