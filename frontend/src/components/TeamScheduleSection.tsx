import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import { pickLatestTemplate } from '../api/templateNaming'
import { RequiredLabel, useRequiredValidation } from './RequiredField'
import { VenuePicker } from './VenuePicker'
import { SHOE_TYPES, shoeTypeKey } from './shoeType'
import type { ScheduledGame, EventAttendanceList, EventAttendanceSummary, AttendanceStatus, WhatsAppTemplate, TemplatePreviewResponse, Venue, ShoeType, TournamentSendPreview } from '../api/types'

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
  kind?: 'practice' | 'game' | 'misc'
}) {
  const { t } = useTranslation()
  // Time-sorted list, optionally filtered to one kind (0 = Game, 1 = Practice, 2 = Misc).
  const events = useMemo(
    () => [...games]
      .filter(g => kind === undefined
        || (kind === 'game' && g.kind === 0)
        || (kind === 'practice' && g.kind === 1)
        || (kind === 'misc' && g.kind === 2))
      .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime()),
    [games, kind])

  // `editingId` discriminator:
  //   'new-practice' | 'new-game' | 'new-misc' | 'series' → opening one of the four new forms
  //   number → editing an existing row (Kind taken from the row)
  //   null → no form open
  const [editingId, setEditingId] = useState<number | 'new-practice' | 'new-game' | 'new-misc' | 'series' | null>(null)
  const [editingKind, setEditingKind] = useState<'practice' | 'game' | 'misc'>('practice')
  // Game-specific fields (only meaningful when the active form is a game form):
  const [opponentName, setOpponentName] = useState('')
  const [isHome, setIsHome] = useState<boolean | null>(null)
  const [startsAt, setStartsAt] = useState('')      // datetime-local format (local time)
  const [endsAt, setEndsAt] = useState('')
  const [location, setLocation] = useState('')
  const [venueId, setVenueId] = useState<number | ''>('')
  const [shoeType, setShoeType] = useState<ShoeType>(0)
  const [summary, setSummary] = useState('')

  // Club-wide venues for the location picker; loaded once.
  const [venues, setVenues] = useState<Venue[]>([])
  const reloadVenues = async () => {
    try { setVenues(await Api.listVenues()) } catch (e: any) { onError(extractError(e)) }
  }
  useEffect(() => { reloadVenues() }, [])

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
    setStartsAt(''); setEndsAt(''); setLocation(''); setVenueId(''); setShoeType(0); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startNewMisc = () => {
    setEditingId('new-misc'); setEditingKind('misc')
    setStartsAt(''); setEndsAt(''); setLocation(''); setVenueId(''); setShoeType(0); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startNewGame = () => {
    setEditingId('new-game'); setEditingKind('game')
    setStartsAt(''); setEndsAt(''); setLocation(''); setVenueId(''); setShoeType(0); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startSeries = () => {
    setEditingId('series'); setEditingKind('practice')
    setSeriesStartDate(''); setSeriesEndDate('')
    setSeriesStartTime('17:00'); setSeriesEndTime('')
    setSeriesDays(new Set()); setLocation(''); setVenueId(''); setShoeType(0); setSummary('')
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
    setEditingKind(ev.kind === 0 ? 'game' : ev.kind === 2 ? 'misc' : 'practice')
    // datetime-local wants YYYY-MM-DDTHH:mm in local time (no timezone). Strip seconds/ms.
    setStartsAt(toDateTimeLocal(ev.startsAt))
    setEndsAt(ev.endsAt ? toDateTimeLocal(ev.endsAt) : '')
    setLocation(ev.location ?? '')
    setVenueId(ev.venueId ?? '')
    setShoeType(ev.shoeType)
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
          venueId: venueId === '' ? null : venueId,
          shoeType,
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
      const venueIdValue = venueId === '' ? null : venueId
      if (editingKind === 'game') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          opponentName: opponentName.trim() || null,
          isHome,
          location: trimmedLocation,
          summary: trimmedSummary,
          venueId: venueIdValue,
          shoeType,
        }
        if (editingId === 'new-game') await Api.createGame(teamId, payload)
        else if (typeof editingId === 'number') await Api.updateGame(editingId, payload)
        onNotice(t('admin.msgGameSaved'))
      } else if (editingKind === 'misc') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
          venueId: venueIdValue,
          shoeType,
        }
        if (editingId === 'new-misc') await Api.createMiscEvent(teamId, payload)
        else if (typeof editingId === 'number') await Api.updateMiscEvent(editingId, payload)
        onNotice(t('admin.msgMiscSaved'))
      } else {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
          venueId: venueIdValue,
          shoeType,
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
    const label = ev.kind === 0 ? 'game' : ev.kind === 2 ? 'event' : 'practice'
    if (!confirm(`Delete this ${label} on ${new Date(ev.startsAt).toLocaleString()}?`)) return
    try {
      if (ev.kind === 0) await Api.deleteGame(ev.id)
      else if (ev.kind === 2) await Api.deleteMiscEvent(ev.id)
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

  // Track the values dict the current `cancelState.preview` was built from. When the admin
  // edits one of the variable inputs in the modal, the values object changes; we debounce a
  // re-fetch of /template-preview so both EN and ES panes refresh with the new wording.
  const lastFetchedCancelValuesRef = useRef<string | null>(null)
  useEffect(() => {
    if (!cancelState) { lastFetchedCancelValuesRef.current = null; return }
    const current = JSON.stringify(cancelState.values)
    if (current === lastFetchedCancelValuesRef.current) return
    if (lastFetchedCancelValuesRef.current === null) {
      // First time we see this values set — openCancel just produced it + the initial preview.
      lastFetchedCancelValuesRef.current = current
      return
    }
    const id = setTimeout(async () => {
      try {
        const p = await Api.templatePreview({ templateId: cancelState.template.id, values: cancelState.values })
        setCancelState(s => s ? { ...s, preview: p } : s)
        lastFetchedCancelValuesRef.current = current
      } catch { /* leave previous render in place */ }
    }, 400)
    return () => clearTimeout(id)
  }, [cancelState])

  const kindLabel = (ev: ScheduledGame) =>
    ev.kind === 0 ? t('admin.msgKindGame')
    : ev.kind === 2 ? t('admin.msgKindMisc')
    : t('admin.msgKindPractice')

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
      const baseName = ev.kind === 1 ? 'practice' : 'game'
      // English-first (the values are authored in EN; the preview/send pipeline routes Spanish
      // recipients to the paired template with translated values). Falls back to ES if the
      // admin only configured the Spanish side. Highest version of `{base}v{N}_english`
      // wins per the shared convention.
      const tmpl = pickLatestTemplate(baseName, 0, templates) ?? pickLatestTemplate(baseName, 1, templates)
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
  // Resend preview modal — opened by the per-event Re-send button instead of the old
  // confirm() dialog. Carries which event we're previewing so the modal can fire the
  // matching send (with edited overrides) when the admin confirms.
  const [resendPreviewEventId, setResendPreviewEventId] = useState<number | null>(null)
  const [resendPreview, setResendPreview] = useState<TournamentSendPreview | null>(null)
  const [resendPreviewLoading, setResendPreviewLoading] = useState(false)

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
      const r = await Api.resendEventToPlayer(eventId, playerId)
      onNotice(t('admin.evtResendDone', { sent: r.sent, total: r.total }))
    } catch (e: any) { onError(extractError(e)) }
  }

  /** Open the resend preview modal: fetches the bilingual preview of what the first pending
   *  player would receive, then renders the editable EventResendPreviewModal. Replaces the
   *  old confirm()-and-fire flow so admins can see (and edit) the personalized body. */
  const openResendPreview = async (eventId: number) => {
    onError(''); onNotice('')
    setResendPreviewEventId(eventId); setResendPreviewLoading(true); setResendPreview(null)
    try {
      setResendPreview(await Api.getEventResendPreview(eventId))
    } catch (e: any) {
      onError(extractError(e))
      setResendPreviewEventId(null)
    } finally {
      setResendPreviewLoading(false)
    }
  }

  const confirmResend = async (eventId: number, overrides: Record<number, string>) => {
    setResendingId(eventId); onError(''); onNotice('')
    try {
      const r = await Api.resendEventMessage(eventId, Object.keys(overrides).length > 0 ? overrides : null)
      onNotice(t('admin.evtResendDone', { sent: r.sent, total: r.total }))
      await loadSummary()
      setResendPreviewEventId(null); setResendPreview(null)
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
              {(kind === undefined || kind === 'game') && (
                <button onClick={startNewGame}
                  className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddGame')}</button>
              )}
              {(kind === undefined || kind === 'practice') && (
                <>
                  <button onClick={startNewPractice}
                    className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPractice')}</button>
                  <button onClick={startSeries}
                    className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPracticeSeries')}</button>
                </>
              )}
              {(kind === undefined || kind === 'misc') && (
                <button onClick={startNewMisc}
                  className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddMisc')}</button>
              )}
            </div>
          )}
        </div>

        {editingId !== null && editingId !== 'series' && (
          <form onSubmit={save} noValidate className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <div className="sm:col-span-2 text-xs uppercase tracking-wide text-slate-500">
              {editingKind === 'game' ? t('admin.msgFormGame')
                : editingKind === 'misc' ? t('admin.msgFormMisc')
                : t('admin.msgFormPractice')}
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
              <span className="font-medium text-slate-700">{t('admin.evtVenue')}</span>
              <VenuePicker venues={venues} value={venueId} onChange={setVenueId}
                onVenuesChanged={reloadVenues} onError={onError} />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.evtField')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.evtShoeType')}</span>
              <select value={shoeType} onChange={e => setShoeType(Number(e.target.value) as ShoeType)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                {SHOE_TYPES.map(s => <option key={s} value={s}>{t(shoeTypeKey(s))}</option>)}
              </select>
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeLabel')}</span>
              <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
                placeholder={editingKind === 'game' ? 'Game' : editingKind === 'misc' ? 'Event' : 'Practice'}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2 flex items-center gap-3 pt-2">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {editingId === 'new-practice' ? t('admin.msgAddPractice') :
                 editingId === 'new-game' ? t('admin.msgAddGame') :
                 editingId === 'new-misc' ? t('admin.msgAddMisc') :
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
              <span className="font-medium text-slate-700">{t('admin.evtVenue')}</span>
              <VenuePicker venues={venues} value={venueId} onChange={setVenueId}
                onVenuesChanged={reloadVenues} onError={onError} />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.evtField')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.evtShoeType')}</span>
              <select value={shoeType} onChange={e => setShoeType(Number(e.target.value) as ShoeType)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                {SHOE_TYPES.map(s => <option key={s} value={s}>{t(shoeTypeKey(s))}</option>)}
              </select>
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
              <th className="py-1 pr-4">{t('admin.evtField')}</th>
              <th className="py-1 pr-4"></th>
            </tr>
          </thead>
          <tbody>
            {events.map(ev => {
              const isGame = ev.kind === 0
              const isMisc = ev.kind === 2
              const evSummary = isGame
                ? (ev.opponentName ? `vs ${ev.opponentName}` : (ev.summary ?? 'Game'))
                : (ev.summary ?? (isMisc ? 'Event' : 'Practice'))
              const homeAway = isGame && ev.isHome === true ? ' (H)' : isGame && ev.isHome === false ? ' (A)' : ''
              const kindBadgeClass = isGame
                ? 'bg-blue-100 text-blue-800'
                : isMisc ? 'bg-slate-200 text-slate-700'
                : 'bg-purple-100 text-purple-800'
              const s = attnSummary[ev.id]
              return (
                <Fragment key={ev.id}>
                <tr className={`border-b last:border-0 ${ev.isCancelled ? 'text-slate-400 line-through' : ''}`}>
                  <td className="py-1 pr-4 whitespace-nowrap">{new Date(ev.startsAt).toLocaleString()}</td>
                  <td className="py-1 pr-4 no-underline">
                    <span className={`inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded ${kindBadgeClass}`}>
                      {isGame ? t('admin.msgKindGame') : isMisc ? t('admin.msgKindMisc') : t('admin.msgKindPractice')}
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
                        <button onClick={() => openResendPreview(ev.id)}
                          disabled={resendingId === ev.id || resendPreviewLoading || !s || s.pending === 0}
                          className="text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                          {resendingId === ev.id
                            ? t('admin.sending')
                            : resendPreviewLoading && resendPreviewEventId === ev.id
                              ? t('admin.evtCancelLoading')
                              : t('admin.evtResend')}
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
              <div className="border border-slate-200 rounded p-3 bg-slate-50 space-y-2">
                <div className="text-[10px] uppercase tracking-wide text-slate-500">{t('admin.evtPreviewEditParams')}</div>
                {cancelState.template.variables
                  .slice()
                  .sort((a, b) => a.position - b.position)
                  .map(v => {
                    const key = String(v.position)
                    return (
                      <label key={v.position} className="grid grid-cols-[6rem_1fr] items-center gap-2 text-xs">
                        <span className="text-slate-600">
                          {`{{${v.position}}}`} {v.label}
                        </span>
                        <input type="text" value={cancelState.values[key] ?? ''}
                          onChange={e => setCancelState(s => s ? { ...s, values: { ...s.values, [key]: e.target.value } } : s)}
                          className="border border-slate-300 rounded-md px-2 py-1 text-sm bg-white" />
                      </label>
                    )
                  })}
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

        {resendPreview && resendPreviewEventId !== null && (
          <EventResendPreviewModal
            preview={resendPreview}
            sending={resendingId === resendPreviewEventId}
            onConfirm={(overrides) => confirmResend(resendPreviewEventId, overrides)}
            onCancel={() => { setResendPreviewEventId(null); setResendPreview(null) }} />
        )}
      </div>
    </div>
  )
}

/** Resend-to-no-shows preview + edit modal. Mirrors TournamentSendPreviewModal but with
 *  i18n keys for the event resend flow and a fallback empty-state for free-form (non-template)
 *  original sends where there's nothing to edit. */
function EventResendPreviewModal({
  preview, sending, onConfirm, onCancel,
}: {
  preview: TournamentSendPreview
  sending: boolean
  onConfirm: (overrides: Record<number, string>) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const initialValues = Object.fromEntries(preview.variables.map(v => [String(v.position), v.value]))
  const [values, setValues] = useState<Record<string, string>>(initialValues)
  const [en, setEn] = useState<{ name: string; rendered: string | null }>({
    name: preview.englishTemplateName, rendered: preview.englishRendered,
  })
  const [es, setEs] = useState<{ name: string; rendered: string | null }>({
    name: preview.spanishTemplateName, rendered: preview.spanishRendered,
  })

  useEffect(() => {
    if (preview.templateId === 0) return
    const dirty = preview.variables.some(v => values[String(v.position)] !== v.value)
    if (!dirty) return
    const id = setTimeout(async () => {
      try {
        const r = await Api.templatePreview({ templateId: preview.templateId, values })
        setEn({ name: r.english.templateName, rendered: r.english.rendered })
        setEs({ name: r.spanish.templateName, rendered: r.spanish.rendered })
      } catch { /* leave previous render in place */ }
    }, 400)
    return () => clearTimeout(id)
  }, [values, preview.templateId, preview.variables])

  const fireSend = () => {
    const overrides: Record<number, string> = {}
    for (const v of preview.variables) {
      const edited = values[String(v.position)] ?? ''
      if (edited !== v.value) overrides[v.position] = edited
    }
    onConfirm(overrides)
  }

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-start sm:items-center justify-center p-4 overflow-y-auto">
      <div className="bg-white rounded-lg shadow-xl max-w-3xl w-full p-5 space-y-3 my-8">
        <div className="flex items-start justify-between">
          <h4 className="text-base font-semibold text-slate-800">{t('admin.evtResendPreviewTitle')}</h4>
          <button onClick={onCancel} className="text-slate-400 hover:text-slate-700 text-xl leading-none">×</button>
        </div>
        <div className="text-xs text-slate-500">
          <div>{t('admin.evtResendPreviewHelp', { count: preview.rosterCount })}</div>
          <div className="mt-1 text-slate-700">
            {t('admin.evtResendPreviewSample', { name: preview.samplePlayerName })}
          </div>
        </div>
        {preview.variables.length > 0 ? (
          <div className="border border-slate-200 rounded p-3 bg-slate-50 space-y-2">
            <div className="text-[10px] uppercase tracking-wide text-slate-500">{t('admin.evtPreviewEditParams')}</div>
            {preview.variables.map(v => (
              <label key={v.position} className="grid grid-cols-[6rem_1fr] items-center gap-2 text-xs">
                <span className="text-slate-600">
                  {`{{${v.position}}}`} {v.label}
                  {v.propertyKey && <span className="block text-[10px] text-slate-400">{v.propertyKey}</span>}
                </span>
                <input type="text" value={values[String(v.position)] ?? ''}
                  onChange={e => setValues(prev => ({ ...prev, [String(v.position)]: e.target.value }))}
                  className="border border-slate-300 rounded-md px-2 py-1 text-sm bg-white" />
              </label>
            ))}
          </div>
        ) : (
          <div className="border border-amber-200 rounded p-3 bg-amber-50 text-xs text-amber-800">
            {t('admin.evtResendPreviewFreeForm')}
          </div>
        )}
        <div className="grid md:grid-cols-2 gap-3">
          <div className="border border-slate-200 rounded p-3 bg-slate-50 text-sm whitespace-pre-wrap">
            <div className="text-[10px] uppercase tracking-wide text-slate-500 mb-1">English · {en.name}</div>
            {en.rendered ?? '—'}
          </div>
          <div className="border border-slate-200 rounded p-3 bg-slate-50 text-sm whitespace-pre-wrap">
            <div className="text-[10px] uppercase tracking-wide text-slate-500 mb-1">Español · {es.name}</div>
            {es.rendered ?? '—'}
          </div>
        </div>
        <div className="flex items-center gap-3 pt-1">
          <button onClick={fireSend} disabled={sending}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {sending ? t('admin.sending') : t('admin.evtResendSend')}
          </button>
          <button onClick={onCancel} disabled={sending}
            className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
        </div>
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
