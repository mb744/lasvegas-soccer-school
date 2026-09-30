import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'
import type { EmailPreference, NotificationPreferences } from '../api/types'

/** Opens Profile in the parent app, where the same settings live. */
const APP_LINK = 'lvss://profile?section=notifications'

function mobilePlatform(): 'ios' | 'android' | null {
  const ua = navigator.userAgent
  if (/iPhone|iPad|iPod/i.test(ua)) return 'ios'
  if (/Android/i.test(ua)) return 'android'
  return null
}

/**
 * "Update your preferences here" from event emails. The signed link says whose settings these are,
 * so no sign-in is needed. On a phone that has the app, it hands off to the app's Profile.
 */
export function NotificationPreferencesPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('t') ?? ''

  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const handedOff = useRef(false)
  const platform = mobilePlatform()

  useEffect(() => {
    Api.getNotificationPreferences(token)
      .then(p => {
        setPrefs(p)
        // The app is on this kind of phone for this login: open it there. If it isn't installed
        // after all, nothing happens and the form below still works.
        if (!handedOff.current && platform && p.hasLogin && p.appPlatforms.includes(platform)) {
          handedOff.current = true
          window.location.href = APP_LINK
        }
      })
      .catch((e: any) => setError(typeof e?.response?.data === 'string' ? e.response.data : t('prefs.error')))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const save = async () => {
    if (!prefs) return
    setSaving(true); setError(null); setSaved(false)
    try {
      setPrefs(await Api.saveNotificationPreferences({
        token,
        gameEmails: prefs.gameEmails,
        eventEmails: prefs.eventEmails,
        pushNotifications: prefs.hasLogin ? prefs.pushNotifications : null,
      }))
      setSaved(true)
    } catch (e: any) {
      setError(typeof e?.response?.data === 'string' ? e.response.data : t('prefs.error'))
    } finally {
      setSaving(false)
    }
  }

  const emailOptions: { value: EmailPreference; label: string }[] = [
    { value: 0, label: t('prefs.optDefault') },
    { value: 1, label: t('prefs.optEmail') },
    { value: 2, label: t('prefs.optDontEmail') },
  ]

  const emailRow = (label: string, value: EmailPreference, onChange: (v: EmailPreference) => void) => (
    <label className="block">
      <span className="text-sm text-slate-700">{label}</span>
      <select
        value={value}
        onChange={e => { onChange(Number(e.target.value) as EmailPreference); setSaved(false) }}
        className="mt-1 w-full border border-slate-300 rounded-md px-2 py-2 text-sm bg-white"
      >
        {emailOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </label>
  )

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10">
        <h1 className="text-2xl font-bold text-emerald-800 mb-1">{t('prefs.title')}</h1>
        {prefs && <p className="text-sm text-slate-600 mb-5">{t('prefs.for', { name: prefs.firstName || prefs.email, email: prefs.email })}</p>}

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3 mb-4">{error}</div>}
        {!error && !prefs && <div className="text-slate-500">{t('prefs.loading')}</div>}

        {prefs && (
          <div className="space-y-5">
            {platform && prefs.hasLogin && (
              <a href={APP_LINK}
                className="block text-center bg-white border border-emerald-700 text-emerald-800 font-semibold rounded-md py-2.5">
                {t('prefs.openApp')}
              </a>
            )}

            {prefs.hasLogin && (
              <section className="bg-white border border-slate-200 rounded-lg p-4">
                <h2 className="font-semibold text-slate-800">{t('prefs.pushTitle')}</h2>
                <div className="mt-2 flex gap-4 text-sm">
                  {[true, false].map(v => (
                    <label key={String(v)} className="flex items-center gap-2">
                      <input type="radio" name="push" checked={prefs.pushNotifications === v}
                        onChange={() => { setPrefs({ ...prefs, pushNotifications: v }); setSaved(false) }} />
                      {v ? t('prefs.pushSend') : t('prefs.pushDontSend')}
                    </label>
                  ))}
                </div>
              </section>
            )}

            <section className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
              <h2 className="font-semibold text-slate-800">{t('prefs.emailTitle')}</h2>
              {emailRow(t('prefs.games'), prefs.gameEmails, v => setPrefs({ ...prefs, gameEmails: v }))}
              {emailRow(t('prefs.events'), prefs.eventEmails, v => setPrefs({ ...prefs, eventEmails: v }))}
              <p className="text-xs text-slate-500">{t('prefs.emailHelp')}</p>
            </section>

            <button onClick={save} disabled={saving}
              className="w-full bg-emerald-700 text-white font-semibold py-2.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {saving ? t('prefs.saving') : t('prefs.save')}
            </button>
            {saved && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{t('prefs.saved')}</div>}
          </div>
        )}
      </div>
    </Layout>
  )
}
