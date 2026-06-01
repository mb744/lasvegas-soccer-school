import { Fragment, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import { RequiredLabel, useRequiredValidation } from './RequiredField'
import type { ScheduledGame, EventAttendanceList, EventAttendanceSummary, AttendanceStatus, WhatsAppTemplate, TemplatePreviewResponse } from '../api/types'

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
  teamId, games, onChanged, onError, onNotice, kind,
}: {
  teamId: number
  games: ScheduledGame[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
  /** Restrict the table + add-buttons to one event kind. Omit to show practices and games together. */
  kind?: 'practice' | 'game'
}) {
  const { t } = useTranslation()
  // Time-sorted list, optionally filtered to one kind (0 = Game, 1 = Practice).
  const events = useMemo(
    () => [...games]
      .filter(g => kind === undefined || (kind === 'game' ? g.kind === 0 : g.kind === 1))
      .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime()),
    [games, kind])

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

  // Single shared validator for the per-event start time and the series date range.
  const vEvent = useRequiredValidation(['startsAt'])
  const vSeries = useRequiredValidation(['seriesStartDate', 'seriesEndDate'])

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
      if (!vSeries.checkSubmit({ seriesStartDate, seriesEndDate })) { onError('Start and end date are required.'); return }
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
    if (!vEvent.checkSubmit({ startsAt })) { onError('Start date/time is required.'); return }
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

  // Templated cancel-&-notify: pick the event-kind WhatsApp template (game_* / practice_*), build
  // variable values from the event (game: param 1 = "Has been cancelled"; practice: param 1 =
  // "CANCELLED — <when>"), fetch the bilingual preview so admin can confirm Spanish + date/time
  // read right, then on confirm send the templated WhatsApp message to the team-roster families
  // and mark the event cancelled (for not-yet-cancelled rows). Replaces the old SMS notify flow.
  const [cancelState, setCancelState] = useState<{
    event: ScheduledGame
    markAfter: boolean
    template: WhatsAppTemplate
    values: Record<string, string>
    preview: TemplatePreviewResponse
  } | null>(null)
  const [cancelLoading, setCancelLoading] = useState<number | null>(null)
  const [cancelSending, setCancelSending] = useState(false)

  const kindLabel = (ev: ScheduledGame) =>
    ev.kind === 0 ? t('admin.msgKindGame') : t('admin.msgKindPractice')

  // Build the template variable map by walking the template's own variables and filling each
  // position by label substring (matches Compose's autofill convention). For a game cancellation
  // the opponent slot is overridden with "Has been cancelled"; for a practice the when slot is
  // prefixed with "CANCELLED — " since the practice template has no opponent slot. Any leftover
  // slot (e.g. uniform) is filled with "—" so CreateBroadcast's required-vars check is satisfied.
  // Values are authored in English ("en-US" locale for the when string) — the bilingual preview
  // endpoint translates each value into Spanish via the phrase dictionary on the way out.
  const buildCancelValues = (ev: ScheduledGame, template: WhatsAppTemplate): Record<string, string> => {
    const d = new Date(ev.startsAt)
    const when = `${d.toLocaleDateString('en-US', { weekday: 'short', month: 'numeric', day: 'numeric' })} ${d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}`
    const loc = ev.location?.trim() || '—'
    const isGame = ev.kind === 0
    const values: Record<string, string> = {}
    for (const v of template.variables) {
      const key = v.position.toString()
      const label = v.label.toLowerCase()
      if (label.includes('opponent')) {
        values[key] = isGame ? 'Has been cancelled' : (ev.opponentName?.trim() || '—')
      } else if (label.includes('when')) {
        values[key] = isGame ? when : `CANCELLED — ${when}`
      } else if (label.includes('where') || label.includes('location')) {
        values[key] = loc
      } else {
        values[key] = '—'
      }
    }
    return values
  }

  const openCancel = async (ev: ScheduledGame, markAfter: boolean) => {
    setCancelLoading(ev.id); onError(''); onNotice('')
    try {
      const templates = await Api.listWhatsAppTemplates()
      const kindMatch = (tmpl: WhatsAppTemplate) => {
        const n = tmpl.name.toLowerCase()
        return ev.kind === 1 ? n.includes('practice') : n.includes('game')
      }
      // English-first (language=0) so values are authored in EN; the preview/send pipeline routes
      // Spanish recipients to the paired template with translated values.
      const tmpl = templates.filter(kindMatch).sort((a, b) => a.language - b.language)[0]
      if (!tmpl) {
        onError(t('admin.evtCancelNoTemplate', { kind: kindLabel(ev) }))
        return
      }
      const values = buildCancelValues(ev, tmpl)
      const preview = await Api.templatePreview({ templateId: tmpl.id, values })
      setCancelState({ event: ev, markAfter, template: tmpl, values, preview })
    } catch (e: any) { onError(extractError(e)) }
    finally { setCancelLoading(null) }
  }

  const confirmCancel = async () => {
    if (!cancelState) return
    setCancelSending(true); onError('')
    try {
      const b = await Api.createBroadcast({
        channel: 1, // WhatsApp — the only channel templates send on
        whatsAppTemplateId: cancelState.template.id,
        templateVariables: cancelState.values,
        scheduledGameId: cancelState.event.id,
        target: { kind: 2, dynamicGroupKey: `team-${teamId}` },
      })
      if (cancelState.markAfter) {
        if (cancelState.event.kind === 0) await Api.cancelGame(cancelState.event.id)
        else await Api.cancelPractice(cancelState.event.id)
      }
      const sent = b.recipients.filter(r => r.status !== 4 && r.status !== 5).length
      onNotice(t('admin.evtCancelSent', { sent, total: b.recipients.length }))
      setCancelState(null)
      await onChanged()
      await loadSummary()
    } catch (e: any) { onError(extractError(e)) }
    finally { setCancelSending(false) }
  }

  // Attendance ("confirm rostered players") panel — opens inline under a clicked event.
  const [attendanceFor, setAttendanceFor] = useState<number | null>(null)
  const [attendance, setAttendance] = useState<EventAttendanceList | null>(null)
  // Per-event confirmation counts (eventId → summary), for the row badge + re-send enablement.
  const [attnSummary, setAttnSummary] = useState<Record<number, EventAttendanceSummary>>({})
  const [resendingId, setResendingId] = useState<number | null>(null)

  const loadSummary = async () => {
    try {
      const rows = await Api.getTeamAttendanceSummary(teamId)
      setAttnSummary(Object.fromEntries(rows.map(s => [s.eventId, s])))
    } catch { /* badge is non-critical; ignore */ }
  }
  useEffect(() => { loadSummary() }, [teamId, games])

  const toggleAttendance = async (eventId: number) => {
    if (attendanceFor === eventId) { setAttendanceFor(null); setAttendance(null); return }
    setAttendanceFor(eventId); setAttendance(null)
    try { setAttendance(await Api.getEventAttendance(eventId)) }
    catch (e: any) { onError(extractError(e)) }
  }

  const setStatus = async (eventId: number, playerId: number, status: AttendanceStatus) => {
    try {
      setAttendance(await Api.setEventAttendance(eventId, playerId, status))
      await loadSummary()
    }
    catch (e: any) { onError(extractError(e)) }
  }

  const resendToPlayer = async (eventId: number, playerId: number) => {
    if (!confirm(t('admin.attnResendConfirm'))) return
    onError(''); onNotice('')
    try {
      const b = await Api.resendEventToPlayer(eventId, playerId)
      onNotice(t('admin.evtResendSent', { count: b.recipients.length }))
    } catch (e: any) { onError(extractError(e)) }
  }

  const resend = async (eventId: number) => {
    if (!confirm(t('admin.evtResendConfirm'))) return
    setResendingId(eventId); onError(''); onNotice('')
    try {
      const b = await Api.resendEventMessage(eventId)
      onNotice(t('admin.evtResendSent', { count: b.recipients.length }))
      await loadSummary()
    } catch (e: any) { onError(extractError(e)) }
    finally { setResendingId(null) }
  }

  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-4">
      <div>
        <div className="flex items-center justify-between mb-2 flex-wrap gap-2">
          <h3 className="font-medium text-slate-700">{t('admin.msgTeamScheduleHeader')}</h3>
          {editingId === null && (
            <div className="flex gap-3 flex-wrap">
              {kind !== 'practice' && (
                <button onClick={startNewGame}
                  className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddGame')}</button>
              )}
              {kind !== 'game' && (
                <>
                  <button onClick={startNewPractice}
                    className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPractice')}</button>
                  <button onClick={startSeries}
                    className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPracticeSeries')}</button>
                </>
              )}
            </div>
          )}
        </div>

        {editingId !== null && editingId !== 'series' && (
          <form onSubmit={save} noValidate className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <div className="sm:col-span-2 text-xs uppercase tracking-wide text-slate-500">
              {editingKind === 'game' ? t('admin.msgFormGame') : t('admin.msgFormPractice')}
            </div>
            <label className="block text-sm">
              <RequiredLabel>{t('admin.msgPracticeStart')}</RequiredLabel>
              <input ref={vEvent.register('startsAt')} type="datetime-local" value={startsAt}
                onChange={e => setStartsAt(e.target.value)}
                onBlur={e => vEvent.onFieldBlur('startsAt', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vEvent.fieldCls('startsAt')}`} />
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
          <form onSubmit={save} noValidate className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <label className="block text-sm">
              <RequiredLabel>{t('admin.msgSeriesStartDate')}</RequiredLabel>
              <input ref={vSeries.register('seriesStartDate')} type="date" value={seriesStartDate}
                onChange={e => setSeriesStartDate(e.target.value)}
                onBlur={e => vSeries.onFieldBlur('seriesStartDate', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vSeries.fieldCls('seriesStartDate')}`} />
            </label>
            <label className="block text-sm">
              <RequiredLabel>{t('admin.msgSeriesEndDate')}</RequiredLabel>
              <input ref={vSeries.register('seriesEndDate')} type="date" value={seriesEndDate}
                onChange={e => setSeriesEndDate(e.target.value)}
                onBlur={e => vSeries.onFieldBlur('seriesEndDate', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vSeries.fieldCls('seriesEndDate')}`} />
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
              const s = attnSummary[ev.id]
              return (
                <Fragment key={ev.id}>
                <tr className={`border-b last:border-0 ${ev.isCancelled ? 'text-slate-400 line-through' : ''}`}>
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
                    {s && !ev.isCancelled && (
                      <span className="ml-2 inline-block text-[10px] px-1.5 py-0.5 rounded bg-emerald-50 text-emerald-700 no-underline">
                        {t('admin.evtRowConfirmed', { count: s.confirmed })} · {t('admin.evtRowNoResponse', { count: s.pending })}
                      </span>
                    )}
                  </td>
                  <td className="py-1 pr-4">{ev.location ?? '—'}</td>
                  <td className="py-1 pr-4 text-right whitespace-nowrap no-underline">
                    {!ev.isCancelled && (
                      <>
                        <button onClick={() => toggleAttendance(ev.id)}
                          className="text-emerald-700 hover:underline">{t('admin.attnConfirm')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => resend(ev.id)} disabled={resendingId === ev.id || !s || s.pending === 0}
                          className="text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                          {resendingId === ev.id ? t('admin.evtSyncing') : t('admin.evtResend')}
                        </button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => startEdit(ev)}
                          className="text-emerald-700 hover:underline">{t('admin.details')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => openCancel(ev, true)} disabled={cancelLoading === ev.id}
                          className="text-amber-700 hover:underline disabled:opacity-40 disabled:no-underline">
                          {cancelLoading === ev.id ? t('admin.evtCancelLoading') : t('admin.evtCancelNotify')}
                        </button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => remove(ev)}
                          className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                      </>
                    )}
                    {ev.isCancelled && (
                      <button onClick={() => openCancel(ev, false)} disabled={cancelLoading === ev.id}
                        className="text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                        {cancelLoading === ev.id ? t('admin.evtCancelLoading') : t('admin.evtNotifyAgain')}
                      </button>
                    )}
                  </td>
                </tr>
                {attendanceFor === ev.id && (
                  <tr>
                    <td colSpan={5} className="bg-slate-50 px-3 py-3 no-underline">
                      <AttendancePanel
                        data={attendance}
                        onSet={(playerId, status) => setStatus(ev.id, playerId, status)}
                        onResend={(playerId) => resendToPlayer(ev.id, playerId)}
                      />
                    </td>
                  </tr>
                )}
                </Fragment>
              )
            })}
            {events.length === 0 && (
              <tr><td colSpan={5} className="py-3 text-center text-slate-400">{t('admin.msgScheduleEmpty')}</td></tr>
            )}
          </tbody>
        </table>

        {cancelState && (
          <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-start sm:items-center justify-center p-4 overflow-y-auto">
            <div className="bg-white rounded-lg shadow-xl max-w-3xl w-full p-5 space-y-3 my-8">
              <div className="flex items-start justify-between">
                <h4 className="text-base font-semibold text-slate-800">
                  {t('admin.evtCancelTitle', { kind: kindLabel(cancelState.event) })}
                </h4>
                <button onClick={() => setCancelState(null)}
                  className="text-slate-400 hover:text-slate-700 text-xl leading-none">×</button>
              </div>
              <div className="text-xs text-slate-500">
                <div>{t('admin.evtCancelHelp')}</div>
                <div className="mt-1 text-slate-700">
                  {kindLabel(cancelState.event)} · {new Date(cancelState.event.startsAt).toLocaleString()}
                  {cancelState.event.location ? ` · ${cancelState.event.location}` : ''}
                </div>
              </div>
              <div className="grid md:grid-cols-2 gap-3">
                <div className="border border-slate-200 rounded p-3 bg-slate-50 text-sm whitespace-pre-wrap">
                  <div className="text-[10px] uppercase tracking-wide text-slate-500 mb-1">
                    English · {cancelState.preview.english.templateName}
                  </div>
                  {cancelState.preview.english.rendered ?? '—'}
                </div>
                <div className="border border-slate-200 rounded p-3 bg-slate-50 text-sm whitespace-pre-wrap">
                  <div className="text-[10px] uppercase tracking-wide text-slate-500 mb-1">
                    Español · {cancelState.preview.spanish.templateName}
                  </div>
                  {cancelState.preview.spanish.rendered ?? '—'}
                </div>
              </div>
              <div className="flex items-center gap-3 pt-1">
                <button onClick={confirmCancel} disabled={cancelSending}
                  className="bg-rose-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-rose-800 disabled:opacity-60">
                  {cancelSending ? t('admin.sending') : t('admin.evtCancelSend')}
                </button>
                <button onClick={() => setCancelState(null)} disabled={cancelSending}
                  className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

/** Inline roster confirmation list for one event: per-player status with four set-buttons + counts. */
function AttendancePanel({
  data, onSet, onResend,
}: {
  data: EventAttendanceList | null
  onSet: (playerId: number, status: AttendanceStatus) => void
  onResend: (playerId: number) => void
}) {
  const { t } = useTranslation()
  if (!data) return <div className="text-xs text-slate-400">…</div>

  const setBtn = (it: EventAttendanceList['items'][number], s: AttendanceStatus, label: string, activeCls: string) => (
    <button onClick={() => onSet(it.playerId, s)}
      className={`px-2 py-0.5 rounded border text-[11px] ${it.status === s ? activeCls : 'bg-white border-slate-300 text-slate-600 hover:bg-slate-100'}`}>
      {label}
    </button>
  )

  return (
    <div className="space-y-2">
      <div className="text-xs font-medium text-emerald-800">
        {t('admin.attnHeading')} — {t('admin.attnConfirmed')}: {data.confirmed} · {t('admin.attnMaybe')}: {data.maybe} · {t('admin.attnDeclined')}: {data.declined} · {t('admin.attnPending')}: {data.pending}
      </div>
      <table className="w-full text-xs">
        <tbody>
          {data.items.map(it => (
            <tr key={it.playerId} className="border-b last:border-0">
              <td className="py-1 pr-3">
                <div className="font-medium text-slate-800">{it.firstName} {it.lastName}</div>
                <div className="text-[10px] text-slate-400">
                  {it.parentName ?? ''}{it.parentPhone ? ` · ${it.parentPhone}` : ''}
                  {it.source === 0 && it.status !== 0 ? ` · ${t('admin.attnFromReply')}` : ''}
                </div>
              </td>
              <td className="py-1 text-right whitespace-nowrap">
                <div className="inline-flex items-center gap-1">
                  {setBtn(it, 1, t('admin.attnConfirmed'), 'bg-emerald-600 text-white border-emerald-600')}
                  {setBtn(it, 3, t('admin.attnMaybe'), 'bg-amber-500 text-white border-amber-500')}
                  {setBtn(it, 2, t('admin.attnDeclined'), 'bg-rose-600 text-white border-rose-600')}
                  {setBtn(it, 0, t('admin.attnPending'), 'bg-slate-500 text-white border-slate-500')}
                  <button onClick={() => onResend(it.playerId)} disabled={it.status !== 0}
                    className="ml-1 text-[11px] text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                    {t('admin.evtResend')}
                  </button>
                </div>
              </td>
            </tr>
          ))}
          {data.items.length === 0 && (
            <tr><td className="py-2 text-slate-400">{t('admin.attnNoRoster')}</td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
