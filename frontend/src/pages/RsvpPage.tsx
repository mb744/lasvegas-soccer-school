import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'
import type { AttendanceStatus, RsvpInfo } from '../api/types'

type Answer = 'going' | 'maybe' | 'no'

const TZ = 'America/Los_Angeles'

/**
 * Landing page for the Going / Maybe / Not going buttons in event emails. The answer is saved from
 * here (a POST from the browser), not by opening the link, so email link scanners can't answer.
 */
export function RsvpPage() {
  const { t, i18n } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('t') ?? ''
  const initial = params.get('s') as Answer | null

  const [info, setInfo] = useState<RsvpInfo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [justSaved, setJustSaved] = useState(false)
  const ran = useRef(false)

  const answer = async (a: Answer) => {
    setSaving(true); setError(null)
    try {
      const r = await Api.answerRsvp(token, a)
      setInfo(r)
      setJustSaved(r.canChange)
    } catch (e: any) {
      setError(typeof e?.response?.data === 'string' ? e.response.data : t('common.error', 'Something went wrong.'))
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => {
    if (ran.current) return // React strict mode runs effects twice in dev; answer only once
    ran.current = true
    if (initial === 'going' || initial === 'maybe' || initial === 'no') void answer(initial)
    else Api.getRsvp(token).then(setInfo).catch((e: any) =>
      setError(typeof e?.response?.data === 'string' ? e.response.data : 'Error'))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const statusLabel = (s: AttendanceStatus) =>
    s === 1 ? t('rsvp.going') : s === 3 ? t('rsvp.maybe') : s === 2 ? t('rsvp.notGoing') : t('rsvp.noAnswer')
  const fmt = (iso: string, opts: Intl.DateTimeFormatOptions) =>
    new Date(iso).toLocaleString(i18n.language?.startsWith('es') ? 'es-US' : 'en-US', { timeZone: TZ, ...opts })

  const title = info
    ? (info.kind === 0 && info.opponentName ? `vs ${info.opponentName}` : info.summary || '')
    : ''

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10">
        <h1 className="text-2xl font-bold text-emerald-800 mb-4">{t('rsvp.title')}</h1>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {!error && !info && <div className="text-slate-500">{t('rsvp.loading')}</div>}

        {info && (
          <div className="bg-white border border-slate-200 rounded-lg overflow-hidden">
            <div className="bg-emerald-800 text-white px-5 py-4">
              <div className="text-xs uppercase tracking-wide opacity-80">{info.teamName}</div>
              {title && <div className="text-xl font-bold mt-1">{title}</div>}
              <div className="mt-1">
                {fmt(info.startsAt, { weekday: 'long', month: 'long', day: 'numeric' })} · {fmt(info.startsAt, { hour: 'numeric', minute: '2-digit' })}
              </div>
              {info.arriveAt && (
                <div className="text-sm opacity-90">{t('rsvp.arrive', { time: fmt(info.arriveAt, { hour: 'numeric', minute: '2-digit' }) })}</div>
              )}
              {info.place && <div className="text-sm opacity-90 mt-1">📍 {info.place}</div>}
            </div>

            <div className="p-5 space-y-3">
              {info.isCancelled ? (
                <div className="text-rose-700 font-semibold">{t('rsvp.cancelled')}</div>
              ) : (
                <>
                  {justSaved ? (
                    <div className="text-lg font-semibold text-emerald-800">
                      ✓ {t('rsvp.saved', { name: info.playerFirstName, status: statusLabel(info.status) })}
                    </div>
                  ) : (
                    <>
                      <div className="text-lg font-semibold text-slate-800">{t('rsvp.question', { name: info.playerFirstName })}</div>
                      <div className="text-sm text-slate-500">{t('rsvp.current', { status: statusLabel(info.status) })}</div>
                    </>
                  )}

                  {info.lockedByCoach ? (
                    <div className="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-md p-3">{t('rsvp.lockedByCoach')}</div>
                  ) : (
                    <div>
                      {justSaved && <div className="text-sm text-slate-500 mb-2">{t('rsvp.change')}</div>}
                      <div className="grid grid-cols-3 gap-2">
                        {([
                          ['going', 1, 'bg-emerald-600', t('rsvp.going')],
                          ['maybe', 3, 'bg-amber-600', t('rsvp.maybe')],
                          ['no', 2, 'bg-rose-600', t('rsvp.notGoing')],
                        ] as const).map(([a, s, color, label]) => {
                          const active = info.status === s
                          return (
                            <button key={a} type="button" disabled={saving} onClick={() => answer(a)}
                              className={`rounded-md py-2 text-sm font-semibold border disabled:opacity-60 ${active
                                ? `${color} text-white border-transparent`
                                : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'}`}>
                              {label}
                            </button>
                          )
                        })}
                      </div>
                    </div>
                  )}
                </>
              )}
              <p className="text-xs text-slate-400 pt-2">{t('rsvp.openApp')}</p>
            </div>
          </div>
        )}
      </div>
    </Layout>
  )
}
