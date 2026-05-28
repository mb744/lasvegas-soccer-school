import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { TeamScheduleSection } from '../../components/TeamScheduleSection'
import { Api } from '../../api/client'
import type {
  RosterTeamSummary,
  RosterTeamDetail,
  AvailablePlayer,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

function gotSportUrl(eventId: number, teamId: number): string {
  return `https://system.gotsport.com/org_event/events/${eventId}/schedules?team=${teamId}`
}

export function AdminTeamsPage() {
  const { t } = useTranslation()
  const [teams, setTeams] = useState<RosterTeamSummary[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detail, setDetail] = useState<RosterTeamDetail | null>(null)
  const [available, setAvailable] = useState<AvailablePlayer[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const [newName, setNewName] = useState('')
  const [renamingId, setRenamingId] = useState<number | null>(null)
  const [renameName, setRenameName] = useState('')

  const [search, setSearch] = useState('')
  const [bracketFilter, setBracketFilter] = useState('')
  const [picked, setPicked] = useState<Set<number>>(new Set())

  const [scheduleUrl, setScheduleUrl] = useState('')
  const [syncing, setSyncing] = useState(false)

  const refreshTeams = async () => {
    try { setTeams(await Api.listRosterTeams()) }
    catch (e: any) { setError(errMsg(e)) }
  }

  useEffect(() => { refreshTeams() }, [])

  const loadTeam = async (id: number) => {
    setError(null); setNotice(null)
    setSelectedId(id)
    setPicked(new Set()); setSearch(''); setBracketFilter('')
    try {
      const [d, avail] = await Promise.all([Api.getRosterTeam(id), Api.listAvailablePlayers(id)])
      setDetail(d); setAvailable(avail)
      setScheduleUrl(d.gotSportLinked ? gotSportUrl(d.gotSportEventId, d.gotSportTeamId) : '')
    } catch (e: any) { setError(errMsg(e)) }
  }

  const reloadSelected = async () => {
    if (selectedId == null) return
    const [d, avail] = await Promise.all([Api.getRosterTeam(selectedId), Api.listAvailablePlayers(selectedId)])
    setDetail(d); setAvailable(avail)
    await refreshTeams()
  }

  const createTeam = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null); setNotice(null)
    if (!newName.trim()) { setError(t('common.required')); return }
    setBusy(true)
    try {
      const created = await Api.createRosterTeam({ name: newName.trim() })
      setNewName('')
      await refreshTeams()
      await loadTeam(created.id)
      setNotice(t('admin.teamSaved'))
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const submitRename = async (id: number) => {
    if (!renameName.trim()) { setError(t('common.required')); return }
    setBusy(true)
    try {
      await Api.renameRosterTeam(id, { name: renameName.trim() })
      setRenamingId(null)
      await refreshTeams()
      if (selectedId === id) setDetail(d => d ? { ...d, name: renameName.trim() } : d)
      setNotice(t('admin.teamSaved'))
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const deleteTeam = async (id: number) => {
    if (!confirm(t('admin.teamDeleteConfirm'))) return
    try {
      await Api.deleteRosterTeam(id)
      if (selectedId === id) { setSelectedId(null); setDetail(null); setAvailable([]) }
      await refreshTeams()
    } catch (e: any) { setError(errMsg(e)) }
  }

  const addPicked = async () => {
    if (selectedId == null || picked.size === 0) return
    setBusy(true); setError(null); setNotice(null)
    try {
      const d = await Api.addRosterMembers(selectedId, { playerIds: [...picked] })
      setDetail(d)
      setAvailable(await Api.listAvailablePlayers(selectedId))
      setPicked(new Set())
      await refreshTeams()
      setNotice(t('admin.teamMemberAdded'))
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const removeMember = async (playerId: number) => {
    if (selectedId == null) return
    if (!confirm(t('admin.teamRemoveConfirm'))) return
    try {
      await Api.removeRosterMember(selectedId, playerId)
      await reloadSelected()
      setNotice(t('admin.teamMemberRemoved'))
    } catch (e: any) { setError(errMsg(e)) }
  }

  // Link this team to its GotSport event so schedule sync can scrape games. The schedule URL
  // carries both IDs; the backend parses them. Reuses the schedule team-update endpoint, which
  // operates on the same Team row.
  const saveScheduleLink = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!detail) return
    setError(null); setNotice(null); setBusy(true)
    try {
      await Api.updateTeam(detail.id, {
        name: detail.name,
        scheduleUrl: scheduleUrl.trim() || null,
        messageGroupId: detail.messageGroupId,
      })
      await reloadSelected()
      setNotice(t('admin.teamSaved'))
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const runSync = async () => {
    if (!detail) return
    setError(null); setNotice(null); setSyncing(true)
    try {
      const r = await Api.syncTeam(detail.id)
      setNotice(r.message)
      await reloadSelected()
    } catch (e: any) { setError(errMsg(e)) }
    finally { setSyncing(false) }
  }

  const brackets = useMemo(() => {
    const s = new Set<string>()
    for (const p of available) if (p.ageBracket) s.add(p.ageBracket)
    return [...s].sort()
  }, [available])

  const filteredAvailable = useMemo(() => {
    const q = search.trim().toLowerCase()
    return available.filter(p => {
      if (bracketFilter && p.ageBracket !== bracketFilter) return false
      if (!q) return true
      const hay = `${p.firstName} ${p.lastName} ${p.parentName ?? ''}`.toLowerCase()
      return hay.includes(q)
    })
  }, [available, search, bracketFilter])

  const togglePick = (id: number) => {
    setPicked(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id); else next.add(id)
      return next
    })
  }

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.teamsTitle')}</h1>
          <p className="text-sm text-slate-600 mt-1">{t('admin.teamsBlurb')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        <div className="grid lg:grid-cols-3 gap-6">
          {/* Teams list */}
          <section className="lg:col-span-1 bg-white border border-slate-200 rounded-lg p-5 space-y-4 h-fit">
            <h2 className="font-bold text-emerald-800">{t('admin.teamsListHeading')}</h2>
            <form onSubmit={createTeam} className="flex gap-2">
              <input type="text" value={newName} onChange={e => setNewName(e.target.value)}
                placeholder={t('admin.teamNamePlaceholder')}
                className="flex-1 border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <button type="submit" disabled={busy}
                className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {t('admin.teamCreate')}
              </button>
            </form>
            <ul className="divide-y divide-slate-100">
              {teams.map(team => (
                <li key={team.id} className="py-2">
                  {renamingId === team.id ? (
                    <div className="flex gap-2">
                      <input type="text" value={renameName} onChange={e => setRenameName(e.target.value)}
                        className="flex-1 border border-slate-300 rounded-md px-2 py-1 text-sm" />
                      <button onClick={() => submitRename(team.id)} disabled={busy}
                        className="text-emerald-700 text-sm hover:underline">{t('admin.save')}</button>
                      <button onClick={() => setRenamingId(null)} className="text-slate-500 text-sm hover:underline">{t('admin.cancel')}</button>
                    </div>
                  ) : (
                    <div className="flex items-center justify-between gap-2">
                      <button onClick={() => loadTeam(team.id)}
                        className={`text-left flex-1 ${selectedId === team.id ? 'font-bold text-emerald-800' : 'text-slate-700 hover:text-emerald-700'}`}>
                        {team.name}
                        <span className="block text-xs text-slate-400 font-normal">
                          {t('admin.teamRosterCount', { count: team.rosterCount })} · {t('admin.teamUpcomingCount', { count: team.upcomingGameCount })}
                        </span>
                      </button>
                      <div className="text-xs whitespace-nowrap">
                        <button onClick={() => { setRenamingId(team.id); setRenameName(team.name) }}
                          className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                        <span className="mx-1 text-slate-300">|</span>
                        <button onClick={() => deleteTeam(team.id)} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                      </div>
                    </div>
                  )}
                </li>
              ))}
              {teams.length === 0 && <li className="py-3 text-sm text-slate-400">{t('admin.teamNone')}</li>}
            </ul>
          </section>

          {/* Selected team */}
          <div className="lg:col-span-2 space-y-6">
            {!detail ? (
              <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-slate-400 text-sm">
                {t('admin.teamSelectPrompt')}
              </div>
            ) : (
              <>
                <h2 className="text-xl font-bold text-emerald-800">{detail.name}</h2>

                {/* Roster */}
                <CollapsibleSection
                  title={t('admin.teamRosterHeading')}
                  subtitle={t('admin.teamRosterCount', { count: detail.roster.length })}
                  defaultOpen>
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="text-left text-slate-500 border-b">
                        <th className="py-2 pr-4">{t('admin.playerLabel')}</th>
                        <th className="py-2 pr-4">{t('admin.ageClassification')}</th>
                        <th className="py-2 pr-4">{t('admin.teamColParent')}</th>
                        <th className="py-2 pr-4"></th>
                      </tr>
                    </thead>
                    <tbody>
                      {detail.roster.map(m => (
                        <tr key={m.playerId} className="border-b last:border-0">
                          <td className="py-2 pr-4 font-medium">{m.firstName} {m.lastName}<span className="block text-xs text-slate-400 font-normal">{m.dateOfBirth}</span></td>
                          <td className="py-2 pr-4">{m.ageBracket ?? '—'}</td>
                          <td className="py-2 pr-4 text-slate-600">{m.parentName ?? '—'}<span className="block text-xs text-slate-400">{m.parentPhone ?? ''}</span></td>
                          <td className="py-2 pr-4 text-right">
                            <button onClick={() => removeMember(m.playerId)} className="text-rose-700 hover:underline">{t('admin.teamRemove')}</button>
                          </td>
                        </tr>
                      ))}
                      {detail.roster.length === 0 && (
                        <tr><td colSpan={4} className="py-4 text-center text-slate-400">{t('admin.teamRosterEmpty')}</td></tr>
                      )}
                    </tbody>
                  </table>
                </CollapsibleSection>

                {/* Add players from registrations */}
                <CollapsibleSection title={t('admin.teamAddHeading')} defaultOpen>
                  <div className="flex items-center justify-end mb-2">
                    <button onClick={addPicked} disabled={busy || picked.size === 0}
                      className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-50">
                      {t('admin.teamAddSelected', { count: picked.size })}
                    </button>
                  </div>
                  <div className="flex gap-2 flex-wrap">
                    <input type="text" value={search} onChange={e => setSearch(e.target.value)}
                      placeholder={t('admin.teamSearchPlaceholder')}
                      className="flex-1 min-w-[12rem] border border-slate-300 rounded-md px-3 py-2 text-sm" />
                    <select value={bracketFilter} onChange={e => setBracketFilter(e.target.value)}
                      className="border border-slate-300 rounded-md px-3 py-2 text-sm">
                      <option value="">{t('admin.teamFilterAllAges')}</option>
                      {brackets.map(b => <option key={b} value={b}>{b}</option>)}
                    </select>
                  </div>
                  <table className="w-full text-sm mt-3">
                    <tbody>
                      {filteredAvailable.map(p => (
                        <tr key={p.playerId} className="border-b last:border-0">
                          <td className="py-2 pr-2 w-8">
                            <input type="checkbox" checked={picked.has(p.playerId)} onChange={() => togglePick(p.playerId)} />
                          </td>
                          <td className="py-2 pr-4 font-medium">{p.firstName} {p.lastName}<span className="block text-xs text-slate-400 font-normal">{p.dateOfBirth}</span></td>
                          <td className="py-2 pr-4">{p.ageBracket ?? '—'}</td>
                          <td className="py-2 pr-4 text-slate-600">{p.parentName ?? '—'}</td>
                        </tr>
                      ))}
                      {filteredAvailable.length === 0 && (
                        <tr><td colSpan={4} className="py-4 text-center text-slate-400">{t('admin.teamNoAvailable')}</td></tr>
                      )}
                    </tbody>
                  </table>
                </CollapsibleSection>

                {/* GotSport schedule sync (optional) */}
                <CollapsibleSection
                  title={t('admin.teamGotSportHeading')}
                  subtitle={detail.lastSyncedAt ? `${t('admin.msgLastSynced')}: ${new Date(detail.lastSyncedAt).toLocaleString()}` : undefined}>
                  <p className="text-xs text-slate-500">{t('admin.msgTeamScheduleUrlHelp')}</p>
                  <form onSubmit={saveScheduleLink} className="mt-3 flex flex-col gap-2">
                    <input type="url" value={scheduleUrl} onChange={e => setScheduleUrl(e.target.value)}
                      placeholder="https://system.gotsport.com/org_event/events/48082/schedules?team=3764244"
                      className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm font-mono" />
                    <div className="flex items-center gap-3">
                      <button type="submit" disabled={busy}
                        className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                        {t('admin.msgSave')}
                      </button>
                      <button type="button" onClick={runSync} disabled={syncing || !detail.gotSportLinked}
                        className="text-sm border border-emerald-300 text-emerald-700 rounded-md px-3 py-1.5 hover:bg-emerald-50 disabled:opacity-50">
                        {syncing ? t('admin.msgSyncing') : t('admin.msgSyncNow')}
                      </button>
                    </div>
                    {detail.lastSyncedAt && (
                      <p className="text-xs text-slate-500">
                        {t('admin.msgLastSynced')}: {new Date(detail.lastSyncedAt).toLocaleString()} — {detail.lastSyncMessage}
                      </p>
                    )}
                  </form>
                </CollapsibleSection>

                {/* Schedule (practices + games) */}
                <CollapsibleSection
                  title={t('admin.teamScheduleHeading')}
                  subtitle={t('admin.teamUpcomingCount', { count: detail.upcomingGames.length })}>
                  <TeamScheduleSection
                    teamId={detail.id}
                    games={detail.upcomingGames}
                    onChanged={reloadSelected}
                    onError={setError}
                    onNotice={setNotice}
                  />
                </CollapsibleSection>

                {/* Communicate */}
                <section className="bg-white border border-slate-200 rounded-lg p-5">
                  <h2 className="font-bold text-emerald-800">{t('admin.teamCommunicateHeading')}</h2>
                  <p className="text-sm text-slate-600 mt-1">{t('admin.teamCommunicateBlurb')}</p>
                  <Link to="/admin/messaging" className="inline-block mt-3 text-sm font-semibold text-emerald-700 hover:underline">
                    {t('admin.teamMessageLink')} →
                  </Link>
                </section>
              </>
            )}
          </div>
        </div>
      </div>
    </Layout>
  )
}

/** A bordered card whose body collapses behind a clickable header. Counts/status shown in the
 *  header stay visible when collapsed, so the team detail stays scannable without scrolling. */
function CollapsibleSection({
  title, subtitle, defaultOpen = false, children,
}: {
  title: string
  subtitle?: string
  defaultOpen?: boolean
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <section className="bg-white border border-slate-200 rounded-lg">
      <button type="button" onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between gap-3 px-5 py-4 text-left">
        <span>
          <span className="font-bold text-emerald-800">{title}</span>
          {subtitle && <span className="block text-xs text-slate-500 font-normal mt-0.5">{subtitle}</span>}
        </span>
        <span className={`text-slate-400 text-xs transition-transform ${open ? '' : '-rotate-90'}`} aria-hidden>▼</span>
      </button>
      {open && <div className="px-5 pb-5 -mt-1">{children}</div>}
    </section>
  )
}
