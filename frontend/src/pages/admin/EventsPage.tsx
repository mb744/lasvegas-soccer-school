import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { TeamScheduleSection } from '../../components/TeamScheduleSection'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { Api } from '../../api/client'
import type {
  RosterTeamSummary, RosterTeamDetail, TournamentSummary,
  AttendanceStatus, TournamentAttendanceList, AvailablePlayer,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

type Tab = 'practices' | 'games' | 'tournaments'

export function AdminEventsPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('practices')
  const [teams, setTeams] = useState<RosterTeamSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    Api.listRosterTeams().then(setTeams).catch(e => setError(errMsg(e)))
  }, [])

  const tabBtn = (key: Tab, label: string) => (
    <button
      onClick={() => { setTab(key); setError(null); setNotice(null) }}
      className={`px-4 py-2 text-sm font-medium border-b-2 ${tab === key
        ? 'border-emerald-600 text-emerald-800'
        : 'border-transparent text-slate-500 hover:text-slate-700'}`}>
      {label}
    </button>
  )

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.evtTitle')}</h1>
          <p className="text-sm text-slate-600 mt-1">{t('admin.evtBlurb')}</p>
        </div>

        <div className="flex gap-1 border-b border-slate-200">
          {tabBtn('practices', t('admin.evtTabPractices'))}
          {tabBtn('games', t('admin.evtTabGames'))}
          {tabBtn('tournaments', t('admin.evtTabTournaments'))}
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        {tab === 'practices' && (
          <TeamEventsTab teams={teams} kind="practice" onError={setError} onNotice={setNotice} />
        )}
        {tab === 'games' && (
          <TeamEventsTab teams={teams} kind="game" onError={setError} onNotice={setNotice} />
        )}
        {tab === 'tournaments' && (
          <TournamentsTab teams={teams} onError={setError} onNotice={setNotice} />
        )}
      </div>
    </Layout>
  )
}

/** Practices/Games tab: pick a team, then reuse the schedule editor (filtered to one kind). */
function TeamEventsTab({
  teams, kind, onError, onNotice,
}: {
  teams: RosterTeamSummary[]
  kind: 'practice' | 'game'
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [teamId, setTeamId] = useState<number | ''>('')
  const [detail, setDetail] = useState<RosterTeamDetail | null>(null)

  const load = async (id: number) => {
    try { setDetail(await Api.getRosterTeam(id)) }
    catch (e: any) { onError(errMsg(e)) }
  }
  const onSelect = (v: string) => {
    const id = v === '' ? '' : Number(v)
    setTeamId(id)
    if (id === '') setDetail(null); else load(id)
  }

  return (
    <div className="space-y-4">
      <select value={teamId} onChange={e => onSelect(e.target.value)}
        className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-72">
        <option value="">{t('admin.evtPickTeam')}</option>
        {teams.map(tm => <option key={tm.id} value={tm.id}>{tm.name}</option>)}
      </select>

      {!detail ? (
        <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-slate-400 text-sm">
          {t('admin.evtSelectTeamPrompt')}
        </div>
      ) : (
        <TeamScheduleSection
          teamId={detail.id}
          games={detail.upcomingGames}
          kind={kind}
          onChanged={() => load(detail.id)}
          onError={onError}
          onNotice={onNotice}
        />
      )}
    </div>
  )
}

/** Refactored tournament workflow. Three lifecycle stages per tournament:
 *  (1) Create the tournament (name + dates + costs).
 *  (2) Create its dedicated team + pick the roster from registered players.
 *  (3) Send the WhatsApp tournament_* template to each rostered family and track confirmations
 *      per player. Inbound WhatsApp replies auto-update the attendance row via the webhook. */
function TournamentsTab({
  onError, onNotice,
}: {
  teams: RosterTeamSummary[]
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [tournaments, setTournaments] = useState<TournamentSummary[]>([])
  const [busy, setBusy] = useState(false)
  const [expandedId, setExpandedId] = useState<number | null>(null)

  const [name, setName] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [totalCost, setTotalCost] = useState('')
  const [costPerPlayer, setCostPerPlayer] = useState('')
  const vTour = useRequiredValidation(['name', 'startDate', 'endDate', 'costPerPlayer'])

  const refresh = async () => {
    try { setTournaments(await Api.listTournaments()) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { refresh() }, [])

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (!vTour.checkSubmit({ name, startDate, endDate, costPerPlayer })) {
      onError(t('common.required')); return
    }
    setBusy(true)
    try {
      await Api.createTournament({
        name: name.trim(),
        startDate: startDate || null,
        endDate: endDate || null,
        totalCost: totalCost === '' ? null : Number(totalCost),
        costPerPlayer: costPerPlayer === '' ? null : Number(costPerPlayer),
      })
      setName(''); setStartDate(''); setEndDate(''); setTotalCost(''); setCostPerPlayer('')
      await refresh()
      onNotice(t('admin.evtTournCreated'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async (id: number) => {
    if (!confirm(t('admin.evtDeleteTournamentConfirm'))) return
    try { await Api.deleteTournament(id); await refresh() }
    catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <div className="space-y-5">
      <section className="bg-white border border-slate-200 rounded-lg p-5">
        <h2 className="font-bold text-emerald-800">{t('admin.evtNewTournament')}</h2>
        <p className="text-xs text-slate-500 mt-1">{t('admin.evtTournCreateHelp')}</p>
        <form onSubmit={create} noValidate className="mt-3 grid sm:grid-cols-2 gap-3">
          <label className="block text-sm sm:col-span-2">
            <RequiredLabel>{t('admin.evtTournName')}</RequiredLabel>
            <input ref={vTour.register('name')} type="text" value={name}
              onChange={e => setName(e.target.value)}
              onBlur={e => vTour.onFieldBlur('name', e.target.value)}
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vTour.fieldCls('name')}`} />
          </label>
          <label className="block text-sm">
            <RequiredLabel>{t('admin.evtTournStartDate')}</RequiredLabel>
            <input ref={vTour.register('startDate')} type="date" value={startDate}
              onChange={e => setStartDate(e.target.value)}
              onBlur={e => vTour.onFieldBlur('startDate', e.target.value)}
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vTour.fieldCls('startDate')}`} />
          </label>
          <label className="block text-sm">
            <RequiredLabel>{t('admin.evtTournEndDate')}</RequiredLabel>
            <input ref={vTour.register('endDate')} type="date" value={endDate}
              onChange={e => setEndDate(e.target.value)}
              onBlur={e => vTour.onFieldBlur('endDate', e.target.value)}
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vTour.fieldCls('endDate')}`} />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.evtTournTotalCost')}</span>
            <input type="number" min={0} step="0.01" value={totalCost}
              onChange={e => setTotalCost(e.target.value)} placeholder="0.00"
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <RequiredLabel>{t('admin.evtTournCostPerPlayer')}</RequiredLabel>
            <input ref={vTour.register('costPerPlayer')} type="number" min={0} step="0.01"
              value={costPerPlayer}
              onChange={e => setCostPerPlayer(e.target.value)}
              onBlur={e => vTour.onFieldBlur('costPerPlayer', e.target.value)}
              placeholder="0.00"
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vTour.fieldCls('costPerPlayer')}`} />
          </label>
          <div className="sm:col-span-2">
            <button type="submit" disabled={busy}
              className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {t('admin.evtCreate')}
            </button>
          </div>
        </form>
      </section>

      <ul className="space-y-3">
        {tournaments.map(tour => (
          <TournamentCard key={tour.id} tour={tour}
            expanded={expandedId === tour.id}
            onToggle={() => setExpandedId(expandedId === tour.id ? null : tour.id)}
            onChanged={refresh}
            onDelete={() => remove(tour.id)}
            onError={onError} onNotice={onNotice} />
        ))}
        {tournaments.length === 0 && (
          <li className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-slate-400 text-sm">
            {t('admin.evtNoTournaments')}
          </li>
        )}
      </ul>
    </div>
  )
}

/** Each tournament row: summary + Manage panel (create team / roster / send / attendance). */
function TournamentCard({
  tour, expanded, onToggle, onChanged, onDelete, onError, onNotice,
}: {
  tour: TournamentSummary
  expanded: boolean
  onToggle: () => void
  onChanged: () => Promise<void> | void
  onDelete: () => void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()

  const dateLabel = formatDateLabel(tour.startDate, tour.endDate)
  const costLabel = tour.costPerPlayer !== null ? `$${tour.costPerPlayer.toFixed(2)}/player` : '—'

  return (
    <li className="bg-white border border-slate-200 rounded-lg p-4">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <div className="font-bold text-emerald-800">{tour.name}</div>
          <div className="text-xs text-slate-500">
            {dateLabel} · {costLabel}
            {tour.teamName
              ? <> · {tour.teamName} ({t('admin.evtTournRosterCount', { count: tour.rosterCount })})</>
              : <> · <span className="text-amber-700">{t('admin.evtTournNoTeam')}</span></>}
          </div>
        </div>
        <div className="text-sm whitespace-nowrap">
          <button onClick={onToggle} className="text-emerald-700 hover:underline">
            {expanded ? t('admin.hide') : t('admin.evtTournManage')}
          </button>
          <span className="mx-2 text-slate-300">|</span>
          <button onClick={onDelete} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
        </div>
      </div>

      {expanded && (
        <TournamentManage tour={tour} onChanged={onChanged} onError={onError} onNotice={onNotice} />
      )}
    </li>
  )
}

function TournamentManage({
  tour, onChanged, onError, onNotice,
}: {
  tour: TournamentSummary
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [teamName, setTeamName] = useState('')
  const [busy, setBusy] = useState(false)
  const vTeam = useRequiredValidation(['teamName'])

  const [available, setAvailable] = useState<AvailablePlayer[]>([])
  const [picked, setPicked] = useState<Set<number>>(new Set())
  const [search, setSearch] = useState('')

  const [attendance, setAttendance] = useState<TournamentAttendanceList | null>(null)
  const [sending, setSending] = useState(false)

  const loadAvailable = async () => {
    if (tour.teamId === null) return
    try { setAvailable(await Api.listAvailablePlayers(tour.teamId)) }
    catch (e: any) { onError(errMsg(e)) }
  }
  const loadAttendance = async () => {
    if (tour.teamId === null) return
    try { setAttendance(await Api.getTournamentAttendance(tour.id)) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { loadAvailable(); loadAttendance() }, [tour.teamId])

  const createTeam = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (!vTeam.checkSubmit({ teamName })) { onError(t('common.required')); return }
    setBusy(true)
    try {
      await Api.createTournamentTeam(tour.id, { name: teamName.trim() })
      setTeamName(''); vTeam.reset()
      await onChanged()
      onNotice(t('admin.evtTournTeamCreated'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const addPicked = async () => {
    if (tour.teamId === null || picked.size === 0) return
    setBusy(true); onError(''); onNotice('')
    try {
      await Api.addRosterMembers(tour.teamId, { playerIds: [...picked] })
      setPicked(new Set())
      await loadAvailable()
      await loadAttendance()
      await onChanged()
      onNotice(t('admin.teamMemberAdded'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const sendConfirmations = async () => {
    if (!confirm(t('admin.evtTournSendConfirm'))) return
    setSending(true); onError(''); onNotice('')
    try {
      const r = await Api.sendTournamentConfirmations(tour.id)
      onNotice(t('admin.evtTournSendDone', { sent: r.sent, total: r.total }))
      await loadAttendance()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSending(false) }
  }

  const setStatus = async (playerId: number, status: AttendanceStatus) => {
    try { setAttendance(await Api.setTournamentAttendance(tour.id, playerId, status)) }
    catch (e: any) { onError(errMsg(e)) }
  }

  if (tour.teamId === null) {
    return (
      <div className="mt-3 pt-3 border-t border-slate-100 space-y-3">
        <p className="text-sm text-slate-600">{t('admin.evtTournCreateTeamHelp')}</p>
        <form onSubmit={createTeam} noValidate className="flex flex-wrap gap-2 items-end">
          <label className="block text-sm flex-1 min-w-[180px]">
            <RequiredLabel className="text-xs text-slate-600">{t('admin.evtTournTeamName')}</RequiredLabel>
            <input ref={vTeam.register('teamName')} type="text" value={teamName}
              onChange={e => setTeamName(e.target.value)}
              onBlur={e => vTeam.onFieldBlur('teamName', e.target.value)}
              placeholder={`e.g. "${tour.name} roster"`}
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vTeam.fieldCls('teamName')}`} />
          </label>
          <button type="submit" disabled={busy}
            className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {t('admin.evtTournCreateTeam')}
          </button>
        </form>
      </div>
    )
  }

  const filtered = available.filter(p =>
    !search.trim() ||
    `${p.firstName} ${p.lastName}`.toLowerCase().includes(search.trim().toLowerCase()))

  return (
    <div className="mt-3 pt-3 border-t border-slate-100 space-y-4">
      <section>
        <div className="flex items-center justify-between mb-1">
          <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.evtTournRosterHeader')}</h3>
          <button onClick={sendConfirmations}
            disabled={sending || tour.rosterCount === 0 || tour.costPerPlayer === null}
            className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50">
            {sending ? t('admin.sending') : t('admin.evtTournSend')}
          </button>
        </div>
        <p className="text-xs text-slate-500">{t('admin.evtTournSendHelp')}</p>
      </section>

      {attendance && attendance.items.length > 0 && (
        <section className="bg-slate-50 border border-slate-200 rounded p-3">
          <div className="text-xs text-emerald-800 font-medium mb-2">
            {t('admin.attnConfirmed')}: {attendance.confirmed} · {t('admin.attnMaybe')}: {attendance.maybe} · {t('admin.attnDeclined')}: {attendance.declined} · {t('admin.attnPending')}: {attendance.pending}
          </div>
          <table className="w-full text-xs">
            <tbody>
              {attendance.items.map(it => (
                <tr key={it.playerId} className="border-b last:border-0">
                  <td className="py-1 pr-3">
                    <div className="font-medium text-slate-800">{it.firstName} {it.lastName}</div>
                    <div className="text-[10px] text-slate-400">{it.parentName ?? ''}{it.parentPhone ? ` · ${it.parentPhone}` : ''}</div>
                  </td>
                  <td className="py-1 text-right whitespace-nowrap">
                    <div className="inline-flex items-center gap-1">
                      {([1, 3, 2, 0] as AttendanceStatus[]).map(s => (
                        <button key={s} onClick={() => setStatus(it.playerId, s)}
                          className={`px-2 py-0.5 rounded border text-[11px] ${it.status === s
                            ? s === 1 ? 'bg-emerald-600 text-white border-emerald-600'
                              : s === 3 ? 'bg-amber-500 text-white border-amber-500'
                              : s === 2 ? 'bg-rose-600 text-white border-rose-600'
                              : 'bg-slate-500 text-white border-slate-500'
                            : 'bg-white border-slate-300 text-slate-600 hover:bg-slate-100'}`}>
                          {s === 1 ? t('admin.attnConfirmed') : s === 3 ? t('admin.attnMaybe') : s === 2 ? t('admin.attnDeclined') : t('admin.attnPending')}
                        </button>
                      ))}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}

      <section>
        <h3 className="font-semibold text-emerald-800 text-sm mb-1">{t('admin.evtTournAddPlayers')}</h3>
        <p className="text-xs text-slate-500 mb-2">{t('admin.evtTournAddHelp')}</p>
        <input type="text" value={search} onChange={e => setSearch(e.target.value)}
          placeholder={t('admin.evtTournPlayerSearch')}
          className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm mb-2" />
        <div className="max-h-64 overflow-y-auto border border-slate-200 rounded">
          {filtered.map(p => (
            <label key={p.playerId} className="flex items-center gap-2 px-2 py-1 text-xs border-b last:border-0 hover:bg-slate-50">
              <input type="checkbox" checked={picked.has(p.playerId)}
                onChange={e => setPicked(prev => {
                  const next = new Set(prev)
                  if (e.target.checked) next.add(p.playerId); else next.delete(p.playerId)
                  return next
                })} />
              <span className="font-medium">{p.firstName} {p.lastName}</span>
              <span className="text-slate-400">{p.ageBracket ?? ''}</span>
              <span className="text-slate-400 ml-auto">{p.parentName ?? ''}</span>
            </label>
          ))}
          {filtered.length === 0 && <div className="text-xs text-slate-400 p-3 text-center">{t('admin.evtTournNoAvailable')}</div>}
        </div>
        <div className="mt-2 flex items-center gap-3">
          <button onClick={addPicked} disabled={busy || picked.size === 0}
            className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50">
            {t('admin.evtTournAddPicked', { count: picked.size })}
          </button>
        </div>
      </section>
    </div>
  )
}

function formatDateLabel(start: string | null, end: string | null): string {
  if (!start) return '—'
  const s = new Date(start + 'T00:00:00')
  if (!end || end === start) return s.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  const e = new Date(end + 'T00:00:00')
  if (s.getFullYear() === e.getFullYear())
    return `${s.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} – ${e.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}`
  return `${s.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })} – ${e.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}`
}
