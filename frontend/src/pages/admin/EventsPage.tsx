import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { TeamScheduleSection } from '../../components/TeamScheduleSection'
import { Api } from '../../api/client'
import type { RosterTeamSummary, RosterTeamDetail, TournamentSummary } from '../../api/types'

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

/** datetime-local value (local time) → UTC ISO string. */
function toIso(local: string): string {
  return new Date(local).toISOString()
}

function TournamentsTab({
  teams, onError, onNotice,
}: {
  teams: RosterTeamSummary[]
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [tournaments, setTournaments] = useState<TournamentSummary[]>([])
  const [busy, setBusy] = useState(false)
  const [syncingId, setSyncingId] = useState<number | null>(null)

  // Create form
  const [name, setName] = useState('')
  const [teamId, setTeamId] = useState<number | ''>('')
  const [scheduleUrl, setScheduleUrl] = useState('')

  // Add-game form (per tournament)
  const [gameFor, setGameFor] = useState<number | null>(null)
  const [gStart, setGStart] = useState('')
  const [gOpponent, setGOpponent] = useState('')
  const [gHome, setGHome] = useState<'home' | 'away' | 'unknown'>('unknown')
  const [gLocation, setGLocation] = useState('')

  const refresh = async () => {
    try { setTournaments(await Api.listTournaments()) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { refresh() }, [])

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (!name.trim()) { onError(t('common.required')); return }
    if (teamId === '') { onError(t('admin.evtPickTeam')); return }
    if (!scheduleUrl.trim()) { onError(t('admin.evtTournUrlHelp')); return }
    setBusy(true)
    try {
      await Api.createTournament({ name: name.trim(), teamId: Number(teamId), scheduleUrl: scheduleUrl.trim() })
      setName(''); setTeamId(''); setScheduleUrl('')
      await refresh()
      onNotice(t('admin.teamSaved'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const sync = async (id: number) => {
    setSyncingId(id); onError(''); onNotice('')
    try {
      const r = await Api.syncTournament(id)
      onNotice(r.message)
      await refresh()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSyncingId(null) }
  }

  const remove = async (id: number) => {
    if (!confirm(t('admin.evtDeleteTournamentConfirm'))) return
    try { await Api.deleteTournament(id); await refresh() }
    catch (e: any) { onError(errMsg(e)) }
  }

  const addGame = async (tour: TournamentSummary, e: React.FormEvent) => {
    e.preventDefault()
    onError(''); onNotice('')
    if (!gStart) { onError(t('admin.teamStartRequired')); return }
    setBusy(true)
    try {
      await Api.createGame(tour.teamId, {
        startsAt: toIso(gStart),
        opponentName: gOpponent.trim() || null,
        isHome: gHome === 'home' ? true : gHome === 'away' ? false : null,
        location: gLocation.trim() || null,
        tournamentId: tour.id,
      })
      setGameFor(null); setGStart(''); setGOpponent(''); setGHome('unknown'); setGLocation('')
      await refresh()
      onNotice(t('admin.teamSaved'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <div className="space-y-5">
      {/* Create */}
      <section className="bg-white border border-slate-200 rounded-lg p-5">
        <h2 className="font-bold text-emerald-800">{t('admin.evtNewTournament')}</h2>
        <form onSubmit={create} className="mt-3 grid sm:grid-cols-2 gap-3">
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.evtTournName')}</span>
            <input type="text" value={name} onChange={e => setName(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.evtTournTeam')}</span>
            <select value={teamId} onChange={e => setTeamId(e.target.value === '' ? '' : Number(e.target.value))}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
              <option value="">{t('admin.evtPickTeam')}</option>
              {teams.map(tm => <option key={tm.id} value={tm.id}>{tm.name}</option>)}
            </select>
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.evtTournUrl')}</span>
            <input type="url" value={scheduleUrl} onChange={e => setScheduleUrl(e.target.value)}
              placeholder="https://system.gotsport.com/org_event/events/48082/schedules?team=3764244"
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm font-mono" />
            <span className="block text-xs text-slate-500 mt-1">{t('admin.evtTournUrlHelp')}</span>
          </label>
          <div className="sm:col-span-2">
            <button type="submit" disabled={busy}
              className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {t('admin.evtCreate')}
            </button>
          </div>
        </form>
      </section>

      {/* List */}
      <ul className="space-y-3">
        {tournaments.map(tour => (
          <li key={tour.id} className="bg-white border border-slate-200 rounded-lg p-4">
            <div className="flex items-start justify-between gap-3 flex-wrap">
              <div>
                <div className="font-bold text-emerald-800">{tour.name}</div>
                <div className="text-xs text-slate-500">
                  {tour.teamName} · {t('admin.evtGameCount', { count: tour.gameCount })}
                  {tour.lastSyncedAt && <> · {t('admin.evtLastSynced')}: {new Date(tour.lastSyncedAt).toLocaleString()}</>}
                </div>
                {tour.lastSyncMessage && <div className="text-xs text-slate-400 mt-0.5">{tour.lastSyncMessage}</div>}
              </div>
              <div className="text-sm whitespace-nowrap">
                <button onClick={() => sync(tour.id)} disabled={syncingId === tour.id}
                  className="text-emerald-700 hover:underline disabled:opacity-60">
                  {syncingId === tour.id ? t('admin.evtSyncing') : t('admin.evtSync')}
                </button>
                <span className="mx-2 text-slate-300">|</span>
                <button onClick={() => setGameFor(gameFor === tour.id ? null : tour.id)}
                  className="text-emerald-700 hover:underline">+ {t('admin.evtAddGame')}</button>
                <span className="mx-2 text-slate-300">|</span>
                <button onClick={() => remove(tour.id)} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
              </div>
            </div>

            {gameFor === tour.id && (
              <form onSubmit={e => addGame(tour, e)} className="mt-3 grid sm:grid-cols-2 gap-2 border-t border-slate-100 pt-3">
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgPracticeStart')}</span>
                  <input type="datetime-local" value={gStart} onChange={e => setGStart(e.target.value)}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
                </label>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameOpponent')}</span>
                  <input type="text" value={gOpponent} onChange={e => setGOpponent(e.target.value)}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
                </label>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameHomeAway')}</span>
                  <select value={gHome} onChange={e => setGHome(e.target.value as 'home' | 'away' | 'unknown')}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                    <option value="unknown">{t('admin.msgGameHomeAwayUnknown')}</option>
                    <option value="home">{t('admin.msgGameHome')}</option>
                    <option value="away">{t('admin.msgGameAway')}</option>
                  </select>
                </label>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgLocation')}</span>
                  <input type="text" value={gLocation} onChange={e => setGLocation(e.target.value)}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
                </label>
                <div className="sm:col-span-2 flex items-center gap-3">
                  <button type="submit" disabled={busy}
                    className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                    {t('admin.evtAddGame')}
                  </button>
                  <button type="button" onClick={() => setGameFor(null)} className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
                </div>
              </form>
            )}
          </li>
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
