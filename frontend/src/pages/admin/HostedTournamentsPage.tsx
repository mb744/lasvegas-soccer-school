import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  HostedTournament,
  InvitedTeam,
  RosterTeamSummary,
  TournamentKind,
  Venue,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

function fmtUsd(v: number | null | undefined): string {
  if (v == null) return '—'
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(v)
}

const KIND_LABEL: Record<TournamentKind, string> = {
  0: 'Tournament',
  1: 'League',
}

/** Admin hub for LVSS-hosted tournaments/leagues. Two columns: the events list on the left,
 *  the picked event's detail + rosters on the right; a separate "Invited teams" catalog panel
 *  at the bottom lets admin manage the external-teams pool shared across all events. */
export function AdminHostedTournamentsPage() {
  const { t } = useTranslation()
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [events, setEvents] = useState<HostedTournament[]>([])
  const [venues, setVenues] = useState<Venue[]>([])
  const [lvssTeams, setLvssTeams] = useState<RosterTeamSummary[]>([])
  const [invited, setInvited] = useState<InvitedTeam[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showAdd, setShowAdd] = useState(false)

  const selected = useMemo(() => events.find(e => e.id === selectedId) ?? null, [events, selectedId])

  const refresh = async () => {
    try {
      const [ev, vn, tm, iv] = await Promise.all([
        Api.listHostedTournaments(),
        Api.listVenues(),
        Api.listRosterTeams(),
        Api.listInvitedTeams(),
      ])
      setEvents(ev); setVenues(vn); setLvssTeams(tm); setInvited(iv)
      if (selectedId == null && ev.length > 0) setSelectedId(ev[0].id)
    } catch (e: any) { setError(errMsg(e)) }
  }
  useEffect(() => { refresh() }, [])

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-4">
        <div className="flex items-start justify-between flex-wrap gap-2">
          <div>
            <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
            <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.hostedTitle')}</h1>
            <p className="text-sm text-slate-600 mt-1">{t('admin.hostedSubtitle')}</p>
          </div>
          <button onClick={() => setShowAdd(s => !s)}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
            {showAdd ? t('admin.cancel') : '+ ' + t('admin.hostedAddNew')}
          </button>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        {showAdd && (
          <SaveEventForm venues={venues}
            onSaved={async () => { await refresh(); setShowAdd(false); setNotice(t('admin.hostedCreatedNotice')) }}
            onError={(e) => { setError(e); setNotice(null) }}
            onCancel={() => setShowAdd(false)} />
        )}

        <div className="grid lg:grid-cols-3 gap-4">
          <section className="bg-white border border-slate-200 rounded-lg p-4 space-y-2">
            <h2 className="font-bold text-emerald-800">{t('admin.hostedListHeader')}</h2>
            <ul className="space-y-1">
              {events.map(e => (
                <li key={e.id}>
                  <button onClick={() => setSelectedId(e.id)}
                    className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${selectedId === e.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                    <div>{e.name} <span className="text-[10px] uppercase tracking-wide text-slate-500 ml-1">{KIND_LABEL[e.kind]}</span></div>
                    <div className="text-xs text-slate-500">{e.startDate}{e.endDate ? ` → ${e.endDate}` : ''} · {e.teams.length} {t('admin.hostedTeamCount')}</div>
                  </button>
                </li>
              ))}
              {events.length === 0 && <li className="text-sm text-slate-400">{t('admin.hostedEmpty')}</li>}
            </ul>
          </section>

          <section className="lg:col-span-2">
            {selected ? (
              <EventDetailPanel event={selected} venues={venues} lvssTeams={lvssTeams} invitedTeams={invited}
                onChanged={refresh}
                onDeleted={async () => { setSelectedId(null); await refresh(); setNotice(t('admin.hostedDeletedNotice')) }}
                onError={(e) => { setError(e); setNotice(null) }}
                onNotice={(n) => { setNotice(n); setError(null) }} />
            ) : (
              <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
                {t('admin.hostedPickOne')}
              </div>
            )}
          </section>
        </div>

        <InvitedTeamsPanel teams={invited}
          onChanged={refresh}
          onError={(e) => { setError(e); setNotice(null) }}
          onNotice={(n) => { setNotice(n); setError(null) }} />
      </div>
    </Layout>
  )
}

// ------------------------------------------------------------
// Event detail: fields, roster, delete
// ------------------------------------------------------------

function EventDetailPanel({ event, venues, lvssTeams, invitedTeams, onChanged, onDeleted, onError, onNotice }: {
  event: HostedTournament
  venues: Venue[]
  lvssTeams: RosterTeamSummary[]
  invitedTeams: InvitedTeam[]
  onChanged: () => Promise<void> | void
  onDeleted: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState(false)
  const [addingKind, setAddingKind] = useState<'lvss' | 'invited' | null>(null)
  const [pickTeamId, setPickTeamId] = useState<number | ''>('')

  const remove = async () => {
    if (!confirm(t('admin.hostedDeleteConfirm', { name: event.name }))) return
    try { await Api.deleteHostedTournament(event.id); await onDeleted() }
    catch (e: any) { onError(errMsg(e)) }
  }
  const addTeam = async () => {
    if (pickTeamId === '') return
    try {
      if (addingKind === 'lvss') await Api.addHostedTournamentTeam(event.id, { lvssTeamId: Number(pickTeamId) })
      else await Api.addHostedTournamentTeam(event.id, { invitedTeamId: Number(pickTeamId) })
      setAddingKind(null); setPickTeamId('')
      await onChanged()
      onNotice(t('admin.hostedTeamAddedNotice'))
    } catch (e: any) { onError(errMsg(e)) }
  }
  const removeTeam = async (rowId: number) => {
    if (!confirm(t('admin.hostedRemoveTeamConfirm'))) return
    try { await Api.removeHostedTournamentTeam(event.id, rowId); await onChanged() }
    catch (e: any) { onError(errMsg(e)) }
  }

  const availableLvss = lvssTeams.filter(t => !event.teams.some(r => r.lvssTeamId === t.id))
  const availableInvited = invitedTeams.filter(t => !event.teams.some(r => r.invitedTeamId === t.id))

  if (editing) {
    return <SaveEventForm venues={venues} initial={event}
      onSaved={async () => { setEditing(false); await onChanged(); onNotice(t('admin.hostedUpdatedNotice')) }}
      onError={onError}
      onCancel={() => setEditing(false)} />
  }

  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <div>
          <h2 className="text-xl font-bold text-emerald-800">{event.name}</h2>
          <div className="text-xs text-slate-500 mt-0.5">
            <span className="uppercase tracking-wide mr-2">{KIND_LABEL[event.kind]}</span>
            {event.startDate}{event.endDate ? ` → ${event.endDate}` : ''}
          </div>
          <div className="text-xs text-slate-600 mt-1">
            {event.venueName && <span className="mr-3">{event.venueName}{event.venueAddress ? ` · ${event.venueAddress}` : ''}</span>}
            {event.location && <span className="mr-3">{event.location}</span>}
            {event.costPerTeam != null && <span className="mr-3">{t('admin.hostedCostLabel')}: {fmtUsd(event.costPerTeam)}</span>}
          </div>
          {event.notes && <div className="text-xs text-slate-500 mt-1">{event.notes}</div>}
        </div>
        <div className="flex gap-2">
          <button onClick={() => setEditing(true)} className="text-sm text-emerald-700 hover:underline">{t('admin.edit')}</button>
          <button onClick={remove} className="text-sm text-rose-700 hover:underline">{t('admin.delete')}</button>
        </div>
      </div>

      <div className="border-t border-slate-100 pt-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-slate-700">{t('admin.hostedTeamsHeader')}</h3>
          <div className="flex gap-2">
            <button onClick={() => { setAddingKind('lvss'); setPickTeamId('') }}
              className="text-xs text-emerald-700 hover:underline">+ {t('admin.hostedAddLvssTeam')}</button>
            <button onClick={() => { setAddingKind('invited'); setPickTeamId('') }}
              className="text-xs text-emerald-700 hover:underline">+ {t('admin.hostedAddInvitedTeam')}</button>
          </div>
        </div>

        {addingKind && (
          <div className="mt-2 flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded p-2">
            <select value={pickTeamId} onChange={e => setPickTeamId(e.target.value === '' ? '' : Number(e.target.value))}
              className="border border-slate-300 rounded-md px-2 py-1 text-sm flex-1">
              <option value="">— {addingKind === 'lvss' ? t('admin.hostedPickLvss') : t('admin.hostedPickInvited')} —</option>
              {addingKind === 'lvss'
                ? availableLvss.map(t => <option key={t.id} value={t.id}>{t.name}</option>)
                : availableInvited.map(t => <option key={t.id} value={t.id}>{t.name}{t.ageGroup ? ` (${t.ageGroup})` : ''}</option>)}
            </select>
            <button onClick={addTeam} disabled={pickTeamId === ''}
              className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {t('admin.hostedAddTeamSubmit')}
            </button>
            <button onClick={() => setAddingKind(null)} className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
          </div>
        )}

        <table className="w-full text-sm mt-2">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-1 px-2">{t('admin.hostedTeamCol')}</th>
              <th className="py-1 px-2">{t('admin.hostedTeamSourceCol')}</th>
              <th className="py-1 px-2">{t('admin.hostedAgeCol')}</th>
              <th className="py-1 px-2">{t('admin.hostedTierCol')}</th>
              <th className="py-1 px-2">{t('admin.hostedPaidCol')}</th>
              <th className="py-1 px-2">{t('admin.hostedContactCol')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {event.teams.map(r => (
              <TeamRow key={r.id} row={r} event={event}
                onChanged={onChanged}
                onError={onError}
                onRemove={() => removeTeam(r.id)} />
            ))}
            {event.teams.length === 0 && (
              <tr><td colSpan={7} className="py-4 text-center text-xs text-slate-400">{t('admin.hostedNoTeams')}</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <TiersPanel event={event} onChanged={onChanged} onError={onError} onNotice={onNotice} />
      <DaysPanel event={event} onChanged={onChanged} onError={onError} onNotice={onNotice} />
    </div>
  )
}

function TeamRow({ row, event, onChanged, onError, onRemove }: {
  row: HostedTournament['teams'][number]
  event: HostedTournament
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onRemove: () => void
}) {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  const [editingPay, setEditingPay] = useState(false)
  const [payMethod, setPayMethod] = useState(row.paymentMethod ?? '')
  const [payRef, setPayRef] = useState(row.paymentReference ?? '')

  const changeTier = async (tierId: number | null) => {
    setBusy(true)
    try { await Api.assignHostedTournamentTeamTier(event.id, row.id, { tierId }); await onChanged() }
    catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }
  const togglePaid = async () => {
    if (row.paid) {
      // Un-pay clears details.
      setBusy(true)
      try { await Api.setHostedTournamentTeamPaid(event.id, row.id, { paid: false }); await onChanged() }
      catch (e: any) { onError(errMsg(e)) }
      finally { setBusy(false) }
    } else {
      setEditingPay(true)
    }
  }
  const savePayment = async () => {
    setBusy(true)
    try {
      await Api.setHostedTournamentTeamPaid(event.id, row.id, {
        paid: true,
        paymentMethod: payMethod.trim() || null,
        paymentReference: payRef.trim() || null,
      })
      setEditingPay(false)
      await onChanged()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <>
      <tr className="border-b last:border-0 align-top">
        <td className="py-1 px-2 font-medium">{row.lvssTeamName ?? row.invitedTeamName ?? <span className="text-slate-400">—</span>}</td>
        <td className="py-1 px-2 text-xs">
          {row.lvssTeamId != null
            ? <span className="inline-block bg-emerald-100 text-emerald-800 px-1.5 py-0.5 rounded uppercase tracking-wide text-[10px]">{t('admin.hostedSourceLvss')}</span>
            : <span className="inline-block bg-sky-100 text-sky-800 px-1.5 py-0.5 rounded uppercase tracking-wide text-[10px]">{t('admin.hostedSourceInvited')}</span>}
        </td>
        <td className="py-1 px-2 text-xs">{row.ageGroup ?? <span className="text-slate-400">—</span>}</td>
        <td className="py-1 px-2 text-xs">
          <select value={row.tierId ?? ''}
            onChange={e => changeTier(e.target.value === '' ? null : Number(e.target.value))}
            disabled={busy || event.tiers.length === 0}
            className="border border-slate-300 rounded px-1 py-0.5 text-xs disabled:bg-slate-50 disabled:text-slate-400">
            <option value="">— {t('admin.hostedNoTier')} —</option>
            {event.tiers.map(tr => <option key={tr.id} value={tr.id}>{tr.name}</option>)}
          </select>
        </td>
        <td className="py-1 px-2 text-xs">
          <button onClick={togglePaid} disabled={busy}
            className={`px-1.5 py-0.5 rounded text-[10px] uppercase tracking-wide ${row.paid ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-600 hover:bg-emerald-50'} disabled:opacity-60`}>
            {row.paid ? t('admin.hostedPaidYes') : t('admin.hostedPaidNo')}
          </button>
          {row.paid && row.paymentMethod && (
            <div className="text-[10px] text-emerald-700 mt-0.5">{row.paymentMethod}{row.paymentReference ? ` · ${row.paymentReference}` : ''}</div>
          )}
        </td>
        <td className="py-1 px-2 text-xs">
          {row.headCoachName && <div>{row.headCoachName}</div>}
          {row.headCoachPhone && <div className="text-slate-500 font-mono">{row.headCoachPhone}</div>}
          {row.headCoachEmail && <div className="text-slate-500 truncate max-w-[16rem]">{row.headCoachEmail}</div>}
        </td>
        <td className="py-1 px-2 text-right">
          <button onClick={onRemove} className="text-xs text-rose-700 hover:underline">{t('common.remove')}</button>
        </td>
      </tr>
      {editingPay && (
        <tr><td colSpan={7} className="py-2 px-3 bg-emerald-50/50">
          <div className="flex items-end gap-2 flex-wrap">
            <label className="text-xs flex-1 min-w-[140px]">
              <span className="text-slate-700">{t('admin.hostedPayMethod')}</span>
              <input type="text" value={payMethod} onChange={e => setPayMethod(e.target.value)} maxLength={120}
                placeholder="Zelle, Cash, Check…"
                className="mt-1 w-full border border-slate-300 rounded px-2 py-1 text-xs" />
            </label>
            <label className="text-xs flex-1 min-w-[140px]">
              <span className="text-slate-700">{t('admin.hostedPayRef')}</span>
              <input type="text" value={payRef} onChange={e => setPayRef(e.target.value)} maxLength={120}
                className="mt-1 w-full border border-slate-300 rounded px-2 py-1 text-xs font-mono" />
            </label>
            <button onClick={savePayment} disabled={busy}
              className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {t('admin.hostedMarkPaid')}
            </button>
            <button onClick={() => setEditingPay(false)} disabled={busy}
              className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
          </div>
        </td></tr>
      )}
    </>
  )
}

function TiersPanel({ event, onChanged, onError, onNotice }: {
  event: HostedTournament
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [showAdd, setShowAdd] = useState(false)
  const [name, setName] = useState('')
  const [notes, setNotes] = useState('')

  const add = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError(t('admin.hostedTierNameRequired')); return }
    try {
      await Api.addHostedTournamentTier(event.id, {
        name: name.trim(),
        sortOrder: event.tiers.length,
        notes: notes.trim() || null,
      })
      setName(''); setNotes(''); setShowAdd(false)
      await onChanged()
      onNotice(t('admin.hostedTierSavedNotice'))
    } catch (err: any) { onError(errMsg(err)) }
  }
  const remove = async (tierId: number, tierName: string) => {
    if (!confirm(t('admin.hostedTierDeleteConfirm', { name: tierName }))) return
    try { await Api.deleteHostedTournamentTier(event.id, tierId); await onChanged() }
    catch (err: any) { onError(errMsg(err)) }
  }

  return (
    <div className="border-t border-slate-100 pt-3">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-slate-700">{t('admin.hostedTiersHeader')}</h3>
        <button onClick={() => setShowAdd(s => !s)}
          className="text-xs text-emerald-700 hover:underline">
          {showAdd ? t('admin.cancel') : '+ ' + t('admin.hostedTierAddNew')}
        </button>
      </div>
      {showAdd && (
        <form onSubmit={add} className="mt-2 flex items-end gap-2 bg-emerald-50 border border-emerald-200 rounded p-2">
          <label className="text-xs flex-1">
            <span className="text-slate-700">{t('admin.hostedTierName')}</span>
            <input type="text" value={name} onChange={e => setName(e.target.value)} maxLength={80}
              placeholder="U10 Gold, Boys 12U A…"
              className="mt-1 w-full border border-slate-300 rounded px-2 py-1 text-sm" />
          </label>
          <label className="text-xs flex-1">
            <span className="text-slate-700">{t('admin.hostedTierNotes')}</span>
            <input type="text" value={notes} onChange={e => setNotes(e.target.value)} maxLength={500}
              className="mt-1 w-full border border-slate-300 rounded px-2 py-1 text-sm" />
          </label>
          <button type="submit"
            className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800">
            {t('admin.save')}
          </button>
        </form>
      )}
      {event.tiers.length === 0 ? (
        <p className="text-xs text-slate-400 mt-1">{t('admin.hostedTiersEmpty')}</p>
      ) : (
        <ul className="mt-2 space-y-1">
          {event.tiers.map(tier => {
            const teamCount = event.teams.filter(t => t.tierId === tier.id).length
            return (
              <li key={tier.id} className="flex items-center justify-between text-sm border border-slate-100 rounded px-2 py-1">
                <div>
                  <span className="font-medium">{tier.name}</span>
                  <span className="text-xs text-slate-500 ml-2">{teamCount} {t('admin.hostedTeamCount')}</span>
                  {tier.notes && <span className="text-xs text-slate-500 ml-2">· {tier.notes}</span>}
                </div>
                <button onClick={() => remove(tier.id, tier.name)}
                  className="text-xs text-rose-700 hover:underline">{t('admin.delete')}</button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

function DaysPanel({ event, onChanged, onError, onNotice }: {
  event: HostedTournament
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [showAdd, setShowAdd] = useState(false)
  const [date, setDate] = useState('')
  const [startTime, setStartTime] = useState('')
  const [endTime, setEndTime] = useState('')

  const add = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!date) { onError(t('admin.hostedDayDateRequired')); return }
    if (startTime && endTime && endTime < startTime) { onError(t('admin.hostedDayTimeRange')); return }
    try {
      await Api.addHostedTournamentDay(event.id, {
        date,
        startTime: startTime || null,
        endTime: endTime || null,
      })
      setDate(''); setStartTime(''); setEndTime(''); setShowAdd(false)
      await onChanged()
      onNotice(t('admin.hostedDaySavedNotice'))
    } catch (err: any) { onError(errMsg(err)) }
  }
  const remove = async (dayId: number, dayDate: string) => {
    if (!confirm(t('admin.hostedDayDeleteConfirm', { date: dayDate }))) return
    try { await Api.deleteHostedTournamentDay(event.id, dayId); await onChanged() }
    catch (err: any) { onError(errMsg(err)) }
  }

  return (
    <div className="border-t border-slate-100 pt-3">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-slate-700">{t('admin.hostedDaysHeader')}</h3>
        <button onClick={() => setShowAdd(s => !s)}
          className="text-xs text-emerald-700 hover:underline">
          {showAdd ? t('admin.cancel') : '+ ' + t('admin.hostedDayAddNew')}
        </button>
      </div>
      {showAdd && (
        <form onSubmit={add} className="mt-2 flex items-end gap-2 bg-emerald-50 border border-emerald-200 rounded p-2 flex-wrap">
          <label className="text-xs">
            <span className="text-slate-700">{t('admin.hostedDayDate')}</span>
            <input type="date" value={date} onChange={e => setDate(e.target.value)}
              className="mt-1 border border-slate-300 rounded px-2 py-1 text-sm" />
          </label>
          <label className="text-xs">
            <span className="text-slate-700">{t('admin.hostedDayStart')}</span>
            <input type="time" value={startTime} onChange={e => setStartTime(e.target.value)}
              className="mt-1 border border-slate-300 rounded px-2 py-1 text-sm" />
          </label>
          <label className="text-xs">
            <span className="text-slate-700">{t('admin.hostedDayEnd')}</span>
            <input type="time" value={endTime} onChange={e => setEndTime(e.target.value)}
              className="mt-1 border border-slate-300 rounded px-2 py-1 text-sm" />
          </label>
          <button type="submit"
            className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800">
            {t('admin.save')}
          </button>
        </form>
      )}
      {event.days.length === 0 ? (
        <p className="text-xs text-slate-400 mt-1">{t('admin.hostedDaysEmpty')}</p>
      ) : (
        <ul className="mt-2 space-y-1">
          {event.days.map(day => (
            <li key={day.id} className="flex items-center justify-between text-sm border border-slate-100 rounded px-2 py-1">
              <div>
                <span className="font-medium">{day.date}</span>
                <span className="text-xs text-slate-500 ml-2">
                  {day.startTime && day.endTime
                    ? `${day.startTime.slice(0, 5)} – ${day.endTime.slice(0, 5)}`
                    : day.startTime
                      ? `from ${day.startTime.slice(0, 5)}`
                      : t('admin.hostedDayNoTimes')}
                </span>
                {day.notes && <span className="text-xs text-slate-500 ml-2">· {day.notes}</span>}
              </div>
              <button onClick={() => remove(day.id, day.date)}
                className="text-xs text-rose-700 hover:underline">{t('admin.delete')}</button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ------------------------------------------------------------
// Save (create + edit) event form
// ------------------------------------------------------------

function SaveEventForm({ venues, initial, onSaved, onError, onCancel }: {
  venues: Venue[]
  initial?: HostedTournament
  onSaved: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(initial?.name ?? '')
  const [kind, setKind] = useState<TournamentKind>(initial?.kind ?? 0)
  const [startDate, setStartDate] = useState(initial?.startDate ?? '')
  const [endDate, setEndDate] = useState(initial?.endDate ?? '')
  const [venueId, setVenueId] = useState<number | ''>(initial?.venueId ?? '')
  const [location, setLocation] = useState(initial?.location ?? '')
  const [costPerTeam, setCostPerTeam] = useState(initial?.costPerTeam != null ? String(initial.costPerTeam) : '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError(t('admin.hostedNameRequired')); return }
    if (!startDate) { onError(t('admin.hostedStartRequired')); return }
    const cost = costPerTeam.trim() === '' ? null : Number(costPerTeam)
    if (cost != null && (!isFinite(cost) || cost < 0)) { onError(t('admin.hostedCostInvalid')); return }
    setBusy(true)
    try {
      const payload = {
        name: name.trim(),
        kind,
        startDate,
        endDate: endDate || null,
        venueId: venueId === '' ? null : venueId,
        location: location.trim() || null,
        costPerTeam: cost,
        notes: notes.trim() || null,
      }
      if (initial) await Api.updateHostedTournament(initial.id, payload)
      else await Api.createHostedTournament(payload)
      await onSaved()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="bg-white border border-emerald-200 rounded-lg p-4 space-y-3">
      <h3 className="font-semibold text-emerald-800">{initial ? t('admin.hostedEditTitle') : t('admin.hostedAddTitle')}</h3>
      <div className="grid sm:grid-cols-2 gap-2">
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.hostedName')}</span>
          <input type="text" value={name} onChange={e => setName(e.target.value)} maxLength={160}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedKind')}</span>
          <select value={kind} onChange={e => setKind(Number(e.target.value) as TournamentKind)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
            <option value={0}>{t('admin.hostedKindTournament')}</option>
            <option value={1}>{t('admin.hostedKindLeague')}</option>
          </select>
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedVenue')}</span>
          <select value={venueId} onChange={e => setVenueId(e.target.value === '' ? '' : Number(e.target.value))}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
            <option value="">— {t('admin.hostedNoVenue')} —</option>
            {venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
          </select>
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedStart')}</span>
          <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedEnd')}</span>
          <input type="date" value={endDate} onChange={e => setEndDate(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.hostedLocation')}</span>
          <input type="text" value={location} onChange={e => setLocation(e.target.value)} maxLength={400}
            placeholder={t('admin.hostedLocationPh')}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedCost')}</span>
          <input type="number" value={costPerTeam} onChange={e => setCostPerTeam(e.target.value)}
            step="0.01" min="0"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
        </label>
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.hostedNotes')}</span>
          <input type="text" value={notes} onChange={e => setNotes(e.target.value)} maxLength={2000}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
      </div>
      <div className="flex gap-2">
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.save')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}

// ------------------------------------------------------------
// Invited teams catalog
// ------------------------------------------------------------

function InvitedTeamsPanel({ teams, onChanged, onError, onNotice }: {
  teams: InvitedTeam[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [showAdd, setShowAdd] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const editing = editingId ? teams.find(t => t.id === editingId) ?? null : null

  const remove = async (t: InvitedTeam) => {
    if (!confirm(`Delete invited team "${t.name}"?`)) return
    try { await Api.deleteInvitedTeam(t.id); await onChanged(); onNotice('Invited team deleted.') }
    catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-4 space-y-2">
      <div className="flex items-center justify-between">
        <h2 className="font-bold text-emerald-800">{t('admin.hostedInvitedHeader')}</h2>
        <button onClick={() => { setShowAdd(s => !s); setEditingId(null) }}
          className="text-sm text-emerald-700 hover:underline">
          {showAdd ? t('admin.cancel') : '+ ' + t('admin.hostedInvitedAddNew')}
        </button>
      </div>
      <p className="text-xs text-slate-500">{t('admin.hostedInvitedBlurb')}</p>

      {(showAdd || editing) && (
        <SaveInvitedTeamForm initial={editing ?? undefined}
          onSaved={async () => { setShowAdd(false); setEditingId(null); await onChanged(); onNotice(t('admin.hostedInvitedSavedNotice')) }}
          onError={onError}
          onCancel={() => { setShowAdd(false); setEditingId(null) }} />
      )}

      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-slate-500 border-b">
            <th className="py-1 px-2">{t('admin.hostedInvitedName')}</th>
            <th className="py-1 px-2">{t('admin.hostedInvitedCoach')}</th>
            <th className="py-1 px-2">{t('admin.hostedInvitedPhone')}</th>
            <th className="py-1 px-2">{t('admin.hostedInvitedEmail')}</th>
            <th className="py-1 px-2">{t('admin.hostedInvitedAge')}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {teams.map(t => (
            <tr key={t.id} className="border-b last:border-0">
              <td className="py-1 px-2 font-medium">{t.name}</td>
              <td className="py-1 px-2">{t.headCoachName ?? <span className="text-slate-400">—</span>}</td>
              <td className="py-1 px-2 font-mono text-xs">{t.headCoachPhone ?? <span className="text-slate-400">—</span>}</td>
              <td className="py-1 px-2 text-xs">{t.headCoachEmail ?? <span className="text-slate-400">—</span>}</td>
              <td className="py-1 px-2 text-xs">{t.ageGroup ?? <span className="text-slate-400">—</span>}</td>
              <td className="py-1 px-2 text-right text-xs space-x-2">
                <button onClick={() => { setEditingId(t.id); setShowAdd(false) }} className="text-emerald-700 hover:underline">Edit</button>
                <button onClick={() => remove(t)} className="text-rose-700 hover:underline">Delete</button>
              </td>
            </tr>
          ))}
          {teams.length === 0 && (
            <tr><td colSpan={6} className="py-4 text-center text-xs text-slate-400">{t('admin.hostedInvitedEmpty')}</td></tr>
          )}
        </tbody>
      </table>
    </section>
  )
}

function SaveInvitedTeamForm({ initial, onSaved, onError, onCancel }: {
  initial?: InvitedTeam
  onSaved: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(initial?.name ?? '')
  const [headCoachName, setHeadCoachName] = useState(initial?.headCoachName ?? '')
  const [headCoachPhone, setHeadCoachPhone] = useState(initial?.headCoachPhone ?? '')
  const [headCoachEmail, setHeadCoachEmail] = useState(initial?.headCoachEmail ?? '')
  const [ageGroup, setAgeGroup] = useState(initial?.ageGroup ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError(t('admin.hostedInvitedNameRequired')); return }
    setBusy(true)
    try {
      const payload = {
        name: name.trim(),
        headCoachName: headCoachName.trim() || null,
        headCoachPhone: headCoachPhone.trim() || null,
        headCoachEmail: headCoachEmail.trim() || null,
        ageGroup: ageGroup.trim() || null,
        notes: notes.trim() || null,
      }
      if (initial) await Api.updateInvitedTeam(initial.id, payload)
      else await Api.createInvitedTeam(payload)
      await onSaved()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="bg-emerald-50/50 border border-emerald-200 rounded p-3 space-y-2">
      <div className="grid sm:grid-cols-2 gap-2">
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedInvitedName')}</span>
          <input type="text" value={name} onChange={e => setName(e.target.value)} maxLength={160}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedInvitedCoach')}</span>
          <input type="text" value={headCoachName} onChange={e => setHeadCoachName(e.target.value)} maxLength={160}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedInvitedPhone')}</span>
          <input type="text" value={headCoachPhone} onChange={e => setHeadCoachPhone(e.target.value)} maxLength={32}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedInvitedEmail')}</span>
          <input type="email" value={headCoachEmail} onChange={e => setHeadCoachEmail(e.target.value)} maxLength={320}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.hostedInvitedAge')}</span>
          <input type="text" value={ageGroup} onChange={e => setAgeGroup(e.target.value)} maxLength={60}
            placeholder="U10, 2016-2017, …"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.hostedInvitedNotes')}</span>
          <input type="text" value={notes} onChange={e => setNotes(e.target.value)} maxLength={2000}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
      </div>
      <div className="flex gap-2">
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.save')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}
