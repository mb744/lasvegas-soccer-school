import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import type { EventRecipient, ScheduledGame } from '../api/types'

function extractError(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/** Format a UTC ISO string as a local-time value usable in <input type="datetime-local">.
 *  datetime-local has no timezone; this strips to local YYYY-MM-DDTHH:mm. */
function toDateTimeLocal(iso: string): string {
  const d = new Date(iso)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/**
 * Admin schedule editor for a single team: manual games + practices (single and recurring series),
 * edit/cancel/delete, and a cancellation-notification helper. Operates on the shared ScheduledGames
 * for the team via the /api/schedule endpoints.
 */
export function TeamScheduleSection({
  teamId, games, onChanged, onError, onNotice,
}: {
  teamId: number
  games: ScheduledGame[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  // Unified, time-sorted list. Games and practices show in the same table; admin distinguishes
  // by the Kind badge on each row.
  const events = useMemo(
    () => [...games].sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime()),
    [games])

  // `editingId` discriminator:
  //   'new-practice' | 'new-game' | 'series' → opening one of the three new forms
  //   number → editing an existing row (Kind taken from the row)
  //   null → no form open
  const [editingId, setEditingId] = useState<number | 'new-practice' | 'new-game' | 'series' | null>(null)
  const [editingKind, setEditingKind] = useState<'practice' | 'game'>('practice')
  // Game-specific fields (only meaningful when the active form is a game form):
  const [opponentName, setOpponentName] = useState('')
  const [isHome, setIsHome] = useState<boolean | null>(null)
  const [startsAt, setStartsAt] = useState('')      // datetime-local format (local time)
  const [endsAt, setEndsAt] = useState('')
  const [location, setLocation] = useState('')
  const [summary, setSummary] = useState('')

  // Recurring-series form state (only used when editingId === 'series'):
  const [seriesStartDate, setSeriesStartDate] = useState('')
  const [seriesEndDate, setSeriesEndDate] = useState('')
  const [seriesStartTime, setSeriesStartTime] = useState('17:00')
  const [seriesEndTime, setSeriesEndTime] = useState('')
  const [seriesDays, setSeriesDays] = useState<Set<number>>(new Set())

  const startNewPractice = () => {
    setEditingId('new-practice'); setEditingKind('practice')
    setStartsAt(''); setEndsAt(''); setLocation(''); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startNewGame = () => {
    setEditingId('new-game'); setEditingKind('game')
    setStartsAt(''); setEndsAt(''); setLocation(''); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startSeries = () => {
    setEditingId('series'); setEditingKind('practice')
    setSeriesStartDate(''); setSeriesEndDate('')
    setSeriesStartTime('17:00'); setSeriesEndTime('')
    setSeriesDays(new Set()); setLocation(''); setSummary('')
  }
  const toggleDay = (d: number) => {
    setSeriesDays(prev => {
      const next = new Set(prev)
      if (next.has(d)) next.delete(d); else next.add(d)
      return next
    })
  }
  const startEdit = (ev: ScheduledGame) => {
    setEditingId(ev.id)
    setEditingKind(ev.kind === 0 ? 'game' : 'practice')
    // datetime-local wants YYYY-MM-DDTHH:mm in local time (no timezone). Strip seconds/ms.
    setStartsAt(toDateTimeLocal(ev.startsAt))
    setEndsAt(ev.endsAt ? toDateTimeLocal(ev.endsAt) : '')
    setLocation(ev.location ?? '')
    setSummary(ev.summary ?? '')
    setOpponentName(ev.opponentName ?? '')
    setIsHome(ev.isHome)
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (editingId === 'series') {
      if (!seriesStartDate || !seriesEndDate) { onError('Start and end date are required.'); return }
      if (seriesDays.size === 0) { onError('Pick at least one day of the week.'); return }
      try {
        const r = await Api.createPracticeSeries(teamId, {
          startDate: seriesStartDate,
          endDate: seriesEndDate,
          startTime: seriesStartTime,
          endTime: seriesEndTime || null,
          daysOfWeek: Array.from(seriesDays).sort(),
          location: location.trim() || null,
          summary: summary.trim() || null,
        })
        setEditingId(null)
        await onChanged()
        onNotice(t('admin.msgPracticeSeriesCreated', { count: r.count }))
      } catch (e: any) { onError(extractError(e)) }
      return
    }
    if (!startsAt) { onError('Start date/time is required.'); return }
    try {
      const startsAtIso = new Date(startsAt).toISOString()
      const endsAtIso = endsAt ? new Date(endsAt).toISOString() : null
      const trimmedLocation = location.trim() || null
      const trimmedSummary = summary.trim() || null
      if (editingKind === 'game') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          opponentName: opponentName.trim() || null,
          isHome,
          location: trimmedLocation,
          summary: trimmedSummary,
        }
        if (editingId === 'new-game') await Api.createGame(teamId, payload)
        else if (typeof editingId === 'number') await Api.updateGame(editingId, payload)
        onNotice(t('admin.msgGameSaved'))
      } else {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
        }
        if (editingId === 'new-practice') await Api.createPractice(teamId, payload)
        else if (typeof editingId === 'number') await Api.updatePractice(editingId, payload)
        onNotice(t('admin.msgPracticeSaved'))
      }
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async (ev: ScheduledGame) => {
    const label = ev.kind === 0 ? 'game' : 'practice'
    if (!confirm(`Delete this ${label} on ${new Date(ev.startsAt).toLocaleString()}?`)) return
    try {
      if (ev.kind === 0) await Api.deleteGame(ev.id)
      else await Api.deletePractice(ev.id)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const cancel = async (ev: ScheduledGame) => {
    const label = ev.kind === 0 ? 'game' : 'practice'
    if (!confirm(`Cancel this ${label} on ${new Date(ev.startsAt).toLocaleString()}? You'll be able to notify parents who already received the reminder.`)) return
    try {
      if (ev.kind === 0) await Api.cancelGame(ev.id)
      else await Api.cancelPractice(ev.id)
      await onChanged()
      onNotice(t('admin.msgPracticeCancelled'))
    } catch (e: any) { onError(extractError(e)) }
  }

  const [notifyState, setNotifyState] = useState<{
    practice: ScheduledGame
    recipients: EventRecipient[]
    bodyEn: string
    bodyEs: string
  } | null>(null)
  const [sendingNotify, setSendingNotify] = useState(false)

  const startNotify = async (p: ScheduledGame) => {
    try {
      const recipients = await Api.listEventRecipients(p.id)
      const when = new Date(p.startsAt).toLocaleString()
      const where = p.location ? ` at ${p.location}` : ''
      setNotifyState({
        practice: p,
        recipients,
        bodyEn: `The practice scheduled for ${when}${where} has been cancelled. Sorry for the late notice.`,
        bodyEs: `La práctica programada para ${when}${where ? ` en ${p.location}` : ''} ha sido cancelada. Disculpe el aviso tardío.`,
      })
    } catch (e: any) { onError(extractError(e)) }
  }

  const sendNotify = async () => {
    if (!notifyState) return
    if (notifyState.recipients.length === 0) {
      onError(t('admin.msgNotifyNoRecipients'))
      return
    }
    setSendingNotify(true)
    try {
      await Api.createBroadcast({
        channel: 0, // SMS — free-form cancellation works anytime without a template
        bodyEn: notifyState.bodyEn.trim() || null,
        bodyEs: notifyState.bodyEs.trim() || null,
        scheduledGameId: notifyState.practice.id,
        target: {
          kind: 3, // AdHocList
          recipients: notifyState.recipients.map(r => ({ phone: r.phone, name: r.name })),
        },
      })
      onNotice(t('admin.msgCancellationSent', { count: notifyState.recipients.length }))
      setNotifyState(null)
    } catch (e: any) { onError(extractError(e)) }
    finally { setSendingNotify(false) }
  }

  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-4">
      <div>
        <div className="flex items-center justify-between mb-2 flex-wrap gap-2">
          <h3 className="font-medium text-slate-700">{t('admin.msgTeamScheduleHeader')}</h3>
          {editingId === null && (
            <div className="flex gap-3 flex-wrap">
              <button onClick={startNewGame}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddGame')}</button>
              <button onClick={startNewPractice}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPractice')}</button>
              <button onClick={startSeries}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPracticeSeries')}</button>
            </div>
          )}
        </div>

        {editingId !== null && editingId !== 'series' && (
          <form onSubmit={save} className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <div className="sm:col-span-2 text-xs uppercase tracking-wide text-slate-500">
              {editingKind === 'game' ? t('admin.msgFormGame') : t('admin.msgFormPractice')}
            </div>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeStart')}</span>
              <input type="datetime-local" value={startsAt} onChange={e => setStartsAt(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeEnd')}</span>
              <input type="datetime-local" value={endsAt} onChange={e => setEndsAt(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            {editingKind === 'game' && (
              <>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameOpponent')}</span>
                  <input type="text" value={opponentName} onChange={e => setOpponentName(e.target.value)}
                    placeholder="PRIME SC B17 White"
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
                </label>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameHomeAway')}</span>
                  <select
                    value={isHome === null ? '' : isHome ? 'home' : 'away'}
                    onChange={e => setIsHome(e.target.value === '' ? null : e.target.value === 'home')}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                    <option value="">— {t('admin.msgGameHomeAwayUnknown')} —</option>
                    <option value="home">{t('admin.msgGameHome')}</option>
                    <option value="away">{t('admin.msgGameAway')}</option>
                  </select>
                </label>
              </>
            )}
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgLocation')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Sunset Park, field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeLabel')}</span>
              <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
                placeholder={editingKind === 'game' ? 'Game' : 'Practice'}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2 flex items-center gap-3 pt-2">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {editingId === 'new-practice' ? t('admin.msgAddPractice') :
                 editingId === 'new-game' ? t('admin.msgAddGame') :
                 t('admin.msgSave')}
              </button>
              <button type="button" onClick={() => setEditingId(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </form>
        )}

        {editingId === 'series' && (
          <form onSubmit={save} className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesStartDate')}</span>
              <input type="date" value={seriesStartDate} onChange={e => setSeriesStartDate(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesEndDate')}</span>
              <input type="date" value={seriesEndDate} onChange={e => setSeriesEndDate(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesStartTime')}</span>
              <input type="time" value={seriesStartTime} onChange={e => setSeriesStartTime(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesEndTime')}</span>
              <input type="time" value={seriesEndTime} onChange={e => setSeriesEndTime(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2">
              <span className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgSeriesDays')}</span>
              <div className="flex flex-wrap gap-1">
                {[
                  { d: 0, label: t('admin.msgDaySun') },
                  { d: 1, label: t('admin.msgDayMon') },
                  { d: 2, label: t('admin.msgDayTue') },
                  { d: 3, label: t('admin.msgDayWed') },
                  { d: 4, label: t('admin.msgDayThu') },
                  { d: 5, label: t('admin.msgDayFri') },
                  { d: 6, label: t('admin.msgDaySat') },
                ].map(({ d, label }) => (
                  <button key={d} type="button" onClick={() => toggleDay(d)}
                    className={`text-xs px-2 py-1 rounded border ${seriesDays.has(d) ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
                    {label}
                  </button>
                ))}
              </div>
            </div>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgLocation')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Sunset Park, field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeLabel')}</span>
              <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
                placeholder="Practice"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2 flex items-center gap-3 pt-2">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {t('admin.msgCreateSeries')}
              </button>
              <button type="button" onClick={() => setEditingId(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </form>
        )}

        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-1 pr-4">{t('admin.msgWhen')}</th>
              <th className="py-1 pr-4">{t('admin.msgKind')}</th>
              <th className="py-1 pr-4">{t('admin.msgSummary')}</th>
              <th className="py-1 pr-4">{t('admin.msgLocation')}</th>
              <th className="py-1 pr-4"></th>
            </tr>
          </thead>
          <tbody>
            {events.map(ev => {
              const isGame = ev.kind === 0
              const evSummary = isGame
                ? (ev.opponentName ? `vs ${ev.opponentName}` : (ev.summary ?? 'Game'))
                : (ev.summary ?? 'Practice')
              const homeAway = isGame && ev.isHome === true ? ' (H)' : isGame && ev.isHome === false ? ' (A)' : ''
              const kindBadgeClass = isGame ? 'bg-blue-100 text-blue-800' : 'bg-purple-100 text-purple-800'
              return (
                <tr key={ev.id} className={`border-b last:border-0 ${ev.isCancelled ? 'text-slate-400 line-through' : ''}`}>
                  <td className="py-1 pr-4 whitespace-nowrap">{new Date(ev.startsAt).toLocaleString()}</td>
                  <td className="py-1 pr-4 no-underline">
                    <span className={`inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded ${kindBadgeClass}`}>
                      {isGame ? t('admin.msgKindGame') : t('admin.msgKindPractice')}
                    </span>
                  </td>
                  <td className="py-1 pr-4">
                    {evSummary}{homeAway}
                    {ev.seriesId && <span className="ml-2 inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 no-underline">{t('admin.msgSeriesBadge')}</span>}
                    {ev.isCancelled && <span className="ml-2 inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-rose-100 text-rose-700 no-underline">{t('admin.msgCancelledBadge')}</span>}
                  </td>
                  <td className="py-1 pr-4">{ev.location ?? '—'}</td>
                  <td className="py-1 pr-4 text-right whitespace-nowrap no-underline">
                    {!ev.isCancelled && (
                      <>
                        <button onClick={() => startEdit(ev)}
                          className="text-emerald-700 hover:underline">{t('admin.details')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => cancel(ev)}
                          className="text-amber-700 hover:underline">{t('admin.msgCancelPractice')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => remove(ev)}
                          className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                      </>
                    )}
                    {ev.isCancelled && (
                      <button onClick={() => startNotify(ev)}
                        className="text-emerald-700 hover:underline">{t('admin.msgNotifyParents')}</button>
                    )}
                  </td>
                </tr>
              )
            })}
            {events.length === 0 && (
              <tr><td colSpan={5} className="py-3 text-center text-slate-400">{t('admin.msgScheduleEmpty')}</td></tr>
            )}
          </tbody>
        </table>

        {notifyState && (
          <div className="mt-3 border border-amber-300 bg-amber-50 rounded p-3 space-y-2">
            <div className="text-sm">
              <strong>{t('admin.msgNotifyHeader', { count: notifyState.recipients.length })}</strong>
              <div className="text-xs text-slate-600 mt-1">{t('admin.msgNotifyHelp')}</div>
            </div>
            <div className="grid md:grid-cols-2 gap-2">
              <label className="block text-xs">
                <span className="font-medium text-slate-700">English</span>
                <textarea rows={3} value={notifyState.bodyEn}
                  onChange={e => setNotifyState(s => s ? { ...s, bodyEn: e.target.value } : s)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
              </label>
              <label className="block text-xs">
                <span className="font-medium text-slate-700">Español</span>
                <textarea rows={3} value={notifyState.bodyEs}
                  onChange={e => setNotifyState(s => s ? { ...s, bodyEs: e.target.value } : s)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
              </label>
            </div>
            <div className="flex items-center gap-3">
              <button onClick={sendNotify} disabled={sendingNotify}
                className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {sendingNotify ? t('admin.sending') : t('admin.msgSendCancellation')}
              </button>
              <button onClick={() => setNotifyState(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
