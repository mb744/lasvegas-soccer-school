import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { TeamScheduleSection } from '../../components/TeamScheduleSection'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { Api } from '../../api/client'
import type {
  RosterTeamSummary, RosterTeamDetail, TournamentSummary, TournamentTeam,
  AttendanceStatus, TournamentAttendanceList, AvailablePlayer,
  TeamDetail, ScheduledGame,
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

/** Each tournament row: summary + multi-team Manage panel (tabs per team + Add team). */
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
  const teamsLabel = tour.teams.length === 0
    ? <span className="text-amber-700">{t('admin.evtTournNoTeams')}</span>
    : tour.teams.map(tt => tt.teamName).join(', ')

  return (
    <li className="bg-white border border-slate-200 rounded-lg p-4">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <div className="font-bold text-emerald-800">{tour.name}</div>
          <div className="text-xs text-slate-500">
            {dateLabel} · {costLabel} · {teamsLabel}
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

/** Multi-team tournament panel: tabs per team + an "+ Add team" tab. Each team tab shows
 *  its GotSport sync state, manual add-game, roster builder, send-confirmations, attendance. */
function TournamentManage({
  tour, onChanged, onError, onNotice,
}: {
  tour: TournamentSummary
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<number | 'add'>(
    tour.teams.length > 0 ? tour.teams[0].id : 'add')

  // Keep the active tab in range when the teams list changes (e.g., a team was added/removed).
  useEffect(() => {
    if (activeTab === 'add') return
    if (tour.teams.length === 0) { setActiveTab('add'); return }
    if (!tour.teams.some(tt => tt.id === activeTab)) setActiveTab(tour.teams[0].id)
  }, [tour.teams])

  const activeTeam = typeof activeTab === 'number' ? tour.teams.find(tt => tt.id === activeTab) : null

  return (
    <div className="mt-3 pt-3 border-t border-slate-100 space-y-3">
      <div className="flex flex-wrap gap-1 border-b border-slate-200">
        {tour.teams.map(tt => (
          <button key={tt.id} onClick={() => setActiveTab(tt.id)}
            className={`text-sm px-3 py-1.5 rounded-t-md ${activeTab === tt.id
              ? 'bg-emerald-700 text-white'
              : 'bg-slate-100 text-slate-700 hover:bg-slate-200'}`}>
            {tt.teamName}
            <span className={`ml-1 text-[10px] ${activeTab === tt.id ? 'text-emerald-100' : 'text-slate-400'}`}>
              ({tt.rosterCount})
            </span>
          </button>
        ))}
        <button onClick={() => setActiveTab('add')}
          className={`text-sm px-3 py-1.5 rounded-t-md ${activeTab === 'add'
            ? 'bg-emerald-700 text-white'
            : 'bg-slate-100 text-emerald-700 hover:bg-slate-200'}`}>
          + {t('admin.evtTournAddTeam')}
        </button>
      </div>

      {activeTeam && (
        <TournamentTeamPanel key={activeTeam.id} tour={tour} tt={activeTeam}
          onChanged={onChanged} onError={onError} onNotice={onNotice} />
      )}
      {activeTab === 'add' && (
        <AddTournamentTeamForm tour={tour}
          onAdded={async (newTour) => {
            await onChanged()
            // Jump to the new team's tab (last one in the refreshed list).
            const added = newTour.teams[newTour.teams.length - 1]
            if (added) setActiveTab(added.id)
          }}
          onError={onError} onNotice={onNotice} />
      )}
    </div>
  )
}

/** Per-team tab inside a tournament: GotSport sync state, manual add-game, roster builder,
 *  send-confirmations, and per-team attendance panel. */
function TournamentTeamPanel({
  tour, tt, onChanged, onError, onNotice,
}: {
  tour: TournamentSummary
  tt: TournamentTeam
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [available, setAvailable] = useState<AvailablePlayer[]>([])
  const [picked, setPicked] = useState<Set<number>>(new Set())
  const [search, setSearch] = useState('')
  const [attendance, setAttendance] = useState<TournamentAttendanceList | null>(null)
  const [teamDetail, setTeamDetail] = useState<TeamDetail | null>(null)
  const [sending, setSending] = useState(false)
  const [syncing, setSyncing] = useState(false)
  const [busy, setBusy] = useState(false)

  // GotSport sync state editor
  const [gsEventId, setGsEventId] = useState(String(tt.gotSportEventId || ''))
  const [gsTeamId, setGsTeamId] = useState(String(tt.gotSportTeamId || ''))
  const [gsUrl, setGsUrl] = useState('')

  // Manual add-game form
  const [showAdd, setShowAdd] = useState(false)
  const [gStart, setGStart] = useState('')
  const [gOpponent, setGOpponent] = useState('')
  const [gHome, setGHome] = useState<'home' | 'away' | 'unknown'>('unknown')
  const [gLocation, setGLocation] = useState('')
  const vGame = useRequiredValidation(['startsAt'])

  const reloadAll = async () => {
    try {
      const [av, att, td] = await Promise.all([
        Api.listAvailablePlayers(tt.teamId),
        Api.getTournamentTeamAttendance(tour.id, tt.teamId),
        Api.getTeam(tt.teamId),
      ])
      setAvailable(av); setAttendance(att); setTeamDetail(td)
    } catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { reloadAll() }, [tt.id])
  // Keep the editable GotSport fields in sync when the underlying TT changes
  // (e.g., refresh after Update).
  useEffect(() => {
    setGsEventId(String(tt.gotSportEventId || ''))
    setGsTeamId(String(tt.gotSportTeamId || ''))
  }, [tt.gotSportEventId, tt.gotSportTeamId])

  const addPicked = async () => {
    if (picked.size === 0) return
    setBusy(true); onError(''); onNotice('')
    try {
      await Api.addRosterMembers(tt.teamId, { playerIds: [...picked] })
      setPicked(new Set())
      await reloadAll()
      await onChanged()
      onNotice(t('admin.teamMemberAdded'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const sendConfirmations = async () => {
    if (!confirm(t('admin.evtTournSendConfirm'))) return
    setSending(true); onError(''); onNotice('')
    try {
      const r = await Api.sendTournamentTeamConfirmations(tour.id, tt.teamId)
      onNotice(t('admin.evtTournSendDone', { sent: r.sent, total: r.total }))
      const att = await Api.getTournamentTeamAttendance(tour.id, tt.teamId)
      setAttendance(att)
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSending(false) }
  }

  const setStatus = async (playerId: number, status: AttendanceStatus) => {
    try {
      await Api.setTournamentAttendance(tour.id, playerId, status)
      const att = await Api.getTournamentTeamAttendance(tour.id, tt.teamId)
      setAttendance(att)
    } catch (e: any) { onError(errMsg(e)) }
  }

  const saveGotSport = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    try {
      await Api.updateTournamentTeam(tour.id, tt.id, {
        gotSportEventId: gsEventId === '' ? null : Number(gsEventId),
        gotSportTeamId: gsTeamId === '' ? null : Number(gsTeamId),
        scheduleUrl: gsUrl.trim() || null,
      })
      setGsUrl('')
      await onChanged()
      onNotice(t('admin.evtTournGsSaved'))
    } catch (e: any) { onError(errMsg(e)) }
  }

  const sync = async () => {
    setSyncing(true); onError(''); onNotice('')
    try {
      const r = await Api.syncTournamentTeam(tour.id, tt.id)
      onNotice(r.message)
      await reloadAll()
      await onChanged()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSyncing(false) }
  }

  const addManualGame = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (!vGame.checkSubmit({ startsAt: gStart })) { onError(t('admin.teamStartRequired')); return }
    setBusy(true)
    try {
      await Api.createGame(tt.teamId, {
        startsAt: new Date(gStart).toISOString(),
        opponentName: gOpponent.trim() || null,
        isHome: gHome === 'home' ? true : gHome === 'away' ? false : null,
        location: gLocation.trim() || null,
        tournamentId: tour.id,
      })
      setShowAdd(false); setGStart(''); setGOpponent(''); setGHome('unknown'); setGLocation('')
      await reloadAll()
      await onChanged()
      onNotice(t('admin.teamSaved'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async () => {
    if (!confirm(t('admin.evtTournRemoveTeamConfirm', { name: tt.teamName }))) return
    try {
      await Api.removeTournamentTeam(tour.id, tt.id)
      await onChanged()
    } catch (e: any) { onError(errMsg(e)) }
  }

  const filtered = available.filter(p =>
    !search.trim() ||
    `${p.firstName} ${p.lastName}`.toLowerCase().includes(search.trim().toLowerCase()))

  const tournamentGames = (teamDetail?.upcomingGames ?? []).filter(g => g.tournamentId === tour.id)

  return (
    <div className="space-y-4 border border-slate-200 rounded-md p-3 bg-slate-50/40">
      {/* GotSport sync */}
      <section>
        <div className="flex items-center justify-between mb-1 flex-wrap gap-2">
          <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.evtTournGsHeader')}</h3>
          <div className="text-sm whitespace-nowrap">
            <button onClick={sync}
              disabled={syncing || tt.gotSportEventId === 0 || tt.gotSportTeamId === 0}
              className="text-emerald-700 hover:underline disabled:opacity-50">
              {syncing ? t('admin.evtSyncing') : t('admin.evtSync')}
            </button>
            <span className="mx-2 text-slate-300">|</span>
            <button onClick={remove} className="text-rose-700 hover:underline">
              {t('admin.evtTournRemoveTeam')}
            </button>
          </div>
        </div>
        <form onSubmit={saveGotSport} className="grid sm:grid-cols-3 gap-2 items-end">
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.evtTournGsEventId')}</span>
            <input type="number" value={gsEventId} onChange={e => setGsEventId(e.target.value)}
              placeholder="48082"
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.evtTournGsTeamId')}</span>
            <input type="number" value={gsTeamId} onChange={e => setGsTeamId(e.target.value)}
              placeholder="3764244"
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
          </label>
          <label className="block text-xs sm:col-span-3">
            <span className="text-slate-600">{t('admin.evtTournGsUrl')}</span>
            <input type="url" value={gsUrl} onChange={e => setGsUrl(e.target.value)}
              placeholder="https://system.gotsport.com/org_event/events/48082/schedules?team=3764244"
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
            <span className="block text-[11px] text-slate-500 mt-0.5">{t('admin.evtTournGsUrlHelp')}</span>
          </label>
          <div className="sm:col-span-3">
            <button type="submit"
              className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800">
              {t('admin.evtTournGsSave')}
            </button>
            {tt.lastSyncedAt && (
              <span className="ml-3 text-xs text-slate-500">
                {t('admin.evtLastSynced')}: {new Date(tt.lastSyncedAt).toLocaleString()}
                {tt.lastSyncMessage && <> · {tt.lastSyncMessage}</>}
              </span>
            )}
          </div>
        </form>
      </section>

      {/* Manual add-game + games list */}
      <section>
        <div className="flex items-center justify-between mb-1">
          <h3 className="font-semibold text-emerald-800 text-sm">
            {t('admin.evtTournGames', { count: tournamentGames.length })}
          </h3>
          {!showAdd && (
            <button onClick={() => setShowAdd(true)} className="text-sm text-emerald-700 hover:underline">
              + {t('admin.msgAddGame')}
            </button>
          )}
        </div>
        {showAdd && (
          <form onSubmit={addManualGame} noValidate className="grid sm:grid-cols-2 gap-2 mb-2 border border-slate-200 rounded p-2 bg-white">
            <label className="block text-xs">
              <RequiredLabel className="text-slate-600">{t('admin.msgPracticeStart')}</RequiredLabel>
              <input ref={vGame.register('startsAt')} type="datetime-local" value={gStart}
                onChange={e => setGStart(e.target.value)}
                onBlur={e => vGame.onFieldBlur('startsAt', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm ${vGame.fieldCls('startsAt')}`} />
            </label>
            <label className="block text-xs">
              <span className="text-slate-600">{t('admin.msgGameOpponent')}</span>
              <input type="text" value={gOpponent} onChange={e => setGOpponent(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            </label>
            <label className="block text-xs">
              <span className="text-slate-600">{t('admin.msgGameHomeAway')}</span>
              <select value={gHome} onChange={e => setGHome(e.target.value as 'home' | 'away' | 'unknown')}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
                <option value="unknown">{t('admin.msgGameHomeAwayUnknown')}</option>
                <option value="home">{t('admin.msgGameHome')}</option>
                <option value="away">{t('admin.msgGameAway')}</option>
              </select>
            </label>
            <label className="block text-xs">
              <span className="text-slate-600">{t('admin.msgLocation')}</span>
              <input type="text" value={gLocation} onChange={e => setGLocation(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            </label>
            <div className="sm:col-span-2 flex gap-2">
              <button type="submit" disabled={busy}
                className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {t('admin.evtAddGame')}
              </button>
              <button type="button" onClick={() => setShowAdd(false)} className="text-xs text-slate-600 hover:underline">
                {t('admin.cancel')}
              </button>
            </div>
          </form>
        )}
        {tournamentGames.length > 0 ? (
          <table className="w-full text-xs">
            <thead>
              <tr className="text-left text-slate-500 border-b">
                <th className="py-1 pr-2">{t('admin.msgWhen')}</th>
                <th className="py-1 pr-2">{t('admin.msgGameOpponent')}</th>
                <th className="py-1 pr-2">{t('admin.msgLocation')}</th>
              </tr>
            </thead>
            <tbody>
              {tournamentGames.map((g: ScheduledGame) => (
                <tr key={g.id} className={`border-b last:border-0 ${g.isCancelled ? 'text-slate-400 line-through' : ''}`}>
                  <td className="py-1 pr-2 whitespace-nowrap">{new Date(g.startsAt).toLocaleString()}</td>
                  <td className="py-1 pr-2">{g.opponentName ?? '—'}{g.isHome === true ? ' (H)' : g.isHome === false ? ' (A)' : ''}</td>
                  <td className="py-1 pr-2">{g.location ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="text-xs text-slate-400">{t('admin.evtTournNoGames')}</p>
        )}
      </section>

      {/* Roster + Send confirmations + Attendance */}
      <section>
        <div className="flex items-center justify-between mb-1">
          <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.evtTournRosterHeader')}</h3>
          <button onClick={sendConfirmations}
            disabled={sending || tt.rosterCount === 0 || tour.costPerPlayer === null}
            className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50">
            {sending ? t('admin.sending') : t('admin.evtTournSend')}
          </button>
        </div>
        <p className="text-xs text-slate-500 mb-2">{t('admin.evtTournSendHelp')}</p>

        {attendance && attendance.items.length > 0 && (
          <div className="bg-white border border-slate-200 rounded p-2 mb-3">
            <div className="text-xs text-emerald-800 font-medium mb-1">
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
          </div>
        )}

        <div>
          <h4 className="font-medium text-slate-700 text-xs mb-1">{t('admin.evtTournAddPlayers')}</h4>
          <input type="text" value={search} onChange={e => setSearch(e.target.value)}
            placeholder={t('admin.evtTournPlayerSearch')}
            className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm mb-2" />
          <div className="max-h-56 overflow-y-auto border border-slate-200 rounded bg-white">
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
            {filtered.length === 0 && <div className="text-xs text-slate-400 p-2 text-center">{t('admin.evtTournNoAvailable')}</div>}
          </div>
          <div className="mt-2">
            <button onClick={addPicked} disabled={busy || picked.size === 0}
              className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50">
              {t('admin.evtTournAddPicked', { count: picked.size })}
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}

/** "+ Add team" tab: pick from existing Teams or create a new one inline, with optional
 *  GotSport sync wiring up-front. */
function AddTournamentTeamForm({
  tour, onAdded, onError, onNotice,
}: {
  tour: TournamentSummary
  onAdded: (updated: TournamentSummary) => void | Promise<void>
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [mode, setMode] = useState<'existing' | 'new'>('new')
  const [allTeams, setAllTeams] = useState<RosterTeamSummary[]>([])
  const [existingId, setExistingId] = useState<number | ''>('')
  const [newName, setNewName] = useState('')
  const [gsEventId, setGsEventId] = useState('')
  const [gsTeamId, setGsTeamId] = useState('')
  const [gsUrl, setGsUrl] = useState('')
  const [busy, setBusy] = useState(false)
  const vNew = useRequiredValidation(['newName'])
  const vExisting = useRequiredValidation(['existingId'])

  useEffect(() => {
    Api.listRosterTeams().then(setAllTeams).catch(e => onError(errMsg(e)))
  }, [])

  const alreadyInTournament = new Set(tour.teams.map(tt => tt.teamId))
  const pickable = allTeams.filter(at => !alreadyInTournament.has(at.id))

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (mode === 'new') {
      if (!vNew.checkSubmit({ newName })) { onError(t('common.required')); return }
    } else {
      if (!vExisting.checkSubmit({ existingId: existingId === '' ? '' : String(existingId) })) {
        onError(t('common.required')); return
      }
    }
    setBusy(true)
    try {
      const updated = await Api.addTournamentTeam(tour.id, {
        existingTeamId: mode === 'existing' ? Number(existingId) : null,
        newTeamName: mode === 'new' ? newName.trim() : null,
        gotSportEventId: gsEventId === '' ? null : Number(gsEventId),
        gotSportTeamId: gsTeamId === '' ? null : Number(gsTeamId),
        scheduleUrl: gsUrl.trim() || null,
      })
      setExistingId(''); setNewName(''); setGsEventId(''); setGsTeamId(''); setGsUrl('')
      onNotice(t('admin.evtTournTeamAdded'))
      await onAdded(updated)
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} noValidate className="border border-slate-200 rounded-md p-3 bg-slate-50/40 space-y-3">
      <div className="flex gap-2">
        <button type="button" onClick={() => setMode('new')}
          className={`text-sm px-3 py-1 rounded border ${mode === 'new' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
          {t('admin.evtTournAddModeNew')}
        </button>
        <button type="button" onClick={() => setMode('existing')}
          className={`text-sm px-3 py-1 rounded border ${mode === 'existing' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
          {t('admin.evtTournAddModeExisting')}
        </button>
      </div>

      {mode === 'new' ? (
        <label className="block text-sm">
          <RequiredLabel>{t('admin.evtTournTeamName')}</RequiredLabel>
          <input ref={vNew.register('newName')} type="text" value={newName}
            onChange={e => setNewName(e.target.value)}
            onBlur={e => vNew.onFieldBlur('newName', e.target.value)}
            placeholder={`e.g. "${tour.name} U10"`}
            className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${vNew.fieldCls('newName')}`} />
        </label>
      ) : (
        <label className="block text-sm">
          <RequiredLabel>{t('admin.evtTournAddPickTeam')}</RequiredLabel>
          <select ref={vExisting.register('existingId')} value={existingId}
            onChange={e => setExistingId(e.target.value === '' ? '' : Number(e.target.value))}
            onBlur={e => vExisting.onFieldBlur('existingId', e.target.value)}
            className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-2 text-sm ${vExisting.fieldCls('existingId')}`}>
            <option value="">— {t('admin.evtPickTeam')} —</option>
            {pickable.map(at => <option key={at.id} value={at.id}>{at.name}</option>)}
          </select>
          {pickable.length === 0 && <p className="text-xs text-slate-500 mt-1">{t('admin.evtTournAddNoneLeft')}</p>}
        </label>
      )}

      <div className="grid sm:grid-cols-2 gap-2">
        <label className="block text-xs">
          <span className="text-slate-600">{t('admin.evtTournGsEventId')}</span>
          <input type="number" value={gsEventId} onChange={e => setGsEventId(e.target.value)}
            placeholder="48082"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
        </label>
        <label className="block text-xs">
          <span className="text-slate-600">{t('admin.evtTournGsTeamId')}</span>
          <input type="number" value={gsTeamId} onChange={e => setGsTeamId(e.target.value)}
            placeholder="3764244"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
        </label>
        <label className="block text-xs sm:col-span-2">
          <span className="text-slate-600">{t('admin.evtTournGsUrl')}</span>
          <input type="url" value={gsUrl} onChange={e => setGsUrl(e.target.value)}
            placeholder="https://system.gotsport.com/org_event/events/48082/schedules?team=3764244"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
          <span className="block text-[11px] text-slate-500 mt-0.5">{t('admin.evtTournGsUrlHelp')}</span>
        </label>
      </div>

      <div>
        <button type="submit" disabled={busy}
          className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.evtTournAddTeam')}
        </button>
      </div>
    </form>
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
