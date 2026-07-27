import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import type { HostedTournament } from '../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/**
 * Inline panel that surfaces the reverse flow — starting from a Team, list all hosted-event
 * bracket assignments for that team and give the admin a quick picker to slot the team into
 * a new bracket (creating the event-team row if needed). Same underlying endpoints the event
 * page uses, so paid / bracket state stays consistent across both entry points.
 */
export function TeamBracketAssignmentsPanel({ events, teamKind, teamId, onChanged, onError, onNotice }: {
  events: HostedTournament[]
  teamKind: 'lvss' | 'invited'
  teamId: number
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [pickedEventId, setPickedEventId] = useState<number | ''>('')
  const [pickedTierId, setPickedTierId] = useState<number | ''>('')
  const [pickedBracketId, setPickedBracketId] = useState<number | ''>('')
  const [busy, setBusy] = useState(false)

  // For each event that includes this team as a participant, resolve the (event, teamRow, tier,
  // bracket) tuple so the "current assignments" list can show it with paid + remove actions
  // without another network hop.
  const assignments = useMemo(() => {
    const out: Array<{
      event: HostedTournament
      row: HostedTournament['teams'][number]
    }> = []
    for (const ev of events) {
      const row = ev.teams.find(r => teamKind === 'lvss' ? r.lvssTeamId === teamId : r.invitedTeamId === teamId)
      if (row) out.push({ event: ev, row })
    }
    return out
  }, [events, teamKind, teamId])

  const pickedEvent = useMemo(
    () => (pickedEventId === '' ? null : events.find(e => e.id === Number(pickedEventId)) ?? null),
    [events, pickedEventId])
  const pickedTier = useMemo(
    () => (pickedEvent && pickedTierId !== '' ? pickedEvent.tiers.find(tr => tr.id === Number(pickedTierId)) ?? null : null),
    [pickedEvent, pickedTierId])

  const resetPicker = () => { setPickedEventId(''); setPickedTierId(''); setPickedBracketId('') }

  const submit = async () => {
    if (pickedEventId === '' || pickedBracketId === '') return
    const evId = Number(pickedEventId)
    const brId = Number(pickedBracketId)
    setBusy(true)
    try {
      const existing = assignments.find(a => a.event.id === evId)
      let teamRowId: number
      if (existing) {
        teamRowId = existing.row.id
      } else {
        // Not yet on this event — add first (backend picks the right FK based on kind).
        const updated = await Api.addHostedTournamentTeam(evId,
          teamKind === 'lvss' ? { lvssTeamId: teamId } : { invitedTeamId: teamId })
        const row = updated.teams.find(r => teamKind === 'lvss' ? r.lvssTeamId === teamId : r.invitedTeamId === teamId)
        if (!row) throw new Error('Team add succeeded but the resulting row could not be located.')
        teamRowId = row.id
      }
      await Api.assignHostedTournamentTeamBracket(evId, teamRowId, { bracketId: brId })
      resetPicker()
      await onChanged()
      onNotice(t('admin.hostedBracketTeamAddedNotice'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const removeFromEvent = async (evId: number, teamRowId: number) => {
    if (!confirm(t('admin.hostedRemoveTeamConfirm'))) return
    setBusy(true)
    try { await Api.removeHostedTournamentTeam(evId, teamRowId); await onChanged() }
    catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }
  const clearBracket = async (evId: number, teamRowId: number) => {
    setBusy(true)
    try { await Api.assignHostedTournamentTeamBracket(evId, teamRowId, { bracketId: null }); await onChanged() }
    catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }
  const togglePaid = async (evId: number, row: HostedTournament['teams'][number]) => {
    setBusy(true)
    try { await Api.setHostedTournamentTeamPaid(evId, row.id, { paid: !row.paid }); await onChanged() }
    catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <div className="space-y-2 bg-slate-50/60 border border-slate-200 rounded p-2">
      <div className="text-xs font-semibold text-slate-700">{t('admin.teamBracketsHeader')}</div>

      {assignments.length === 0 ? (
        <p className="text-xs text-slate-400">{t('admin.teamBracketsEmpty')}</p>
      ) : (
        <ul className="space-y-1">
          {assignments.map(({ event, row }) => (
            <li key={event.id} className="flex items-center gap-2 text-xs bg-white border border-slate-200 rounded px-2 py-1">
              <div className="flex-1 min-w-0 truncate">
                <span className="font-medium">{event.name}</span>
                <span className="text-slate-500 ml-2">{event.startDate}{event.endDate ? ` → ${event.endDate}` : ''}</span>
                {row.tierName || row.bracketName
                  ? <span className="text-slate-600 ml-2">· {row.tierName ?? '—'}{row.bracketName ? ` / ${row.bracketName}` : ''}</span>
                  : <span className="text-slate-400 ml-2">· {t('admin.hostedNoTier')}</span>}
              </div>
              <button onClick={() => togglePaid(event.id, row)} disabled={busy}
                className={`px-1.5 py-0.5 rounded text-[10px] uppercase tracking-wide ${row.paid ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-600 hover:bg-emerald-50'} disabled:opacity-60`}>
                {row.paid ? t('admin.hostedPaidYes') : t('admin.hostedPaidNo')}
              </button>
              {row.bracketId != null && (
                <button onClick={() => clearBracket(event.id, row.id)} disabled={busy}
                  className="text-[10px] text-slate-600 hover:underline">{t('admin.teamBracketClear')}</button>
              )}
              <button onClick={() => removeFromEvent(event.id, row.id)} disabled={busy}
                className="text-[10px] text-rose-700 hover:underline">{t('common.remove')}</button>
            </li>
          ))}
        </ul>
      )}

      <div className="border-t border-slate-100 pt-2">
        <div className="text-xs text-slate-600 mb-1">{t('admin.teamBracketsAssign')}</div>
        <div className="grid sm:grid-cols-3 gap-1">
          <select value={pickedEventId} onChange={e => { setPickedEventId(e.target.value === '' ? '' : Number(e.target.value)); setPickedTierId(''); setPickedBracketId('') }}
            className="border border-slate-300 rounded px-2 py-1 text-xs">
            <option value="">{t('admin.teamBracketsPickEvent')}</option>
            {events.map(ev => <option key={ev.id} value={ev.id}>{ev.name}</option>)}
          </select>
          <select value={pickedTierId} onChange={e => { setPickedTierId(e.target.value === '' ? '' : Number(e.target.value)); setPickedBracketId('') }}
            disabled={!pickedEvent || pickedEvent.tiers.length === 0}
            className="border border-slate-300 rounded px-2 py-1 text-xs disabled:bg-slate-50 disabled:text-slate-400">
            <option value="">{t('admin.teamBracketsPickTier')}</option>
            {pickedEvent?.tiers.map(tr => <option key={tr.id} value={tr.id}>{tr.name}</option>)}
          </select>
          <select value={pickedBracketId} onChange={e => setPickedBracketId(e.target.value === '' ? '' : Number(e.target.value))}
            disabled={!pickedTier || pickedTier.brackets.length === 0}
            className="border border-slate-300 rounded px-2 py-1 text-xs disabled:bg-slate-50 disabled:text-slate-400">
            <option value="">{t('admin.teamBracketsPickBracket')}</option>
            {pickedTier?.brackets.map(br => <option key={br.id} value={br.id}>{br.name}</option>)}
          </select>
        </div>
        {pickedEvent && pickedEvent.tiers.length === 0 && (
          <p className="text-[11px] text-amber-700 mt-1">{t('admin.teamBracketsNoTiers')}</p>
        )}
        {pickedTier && pickedTier.brackets.length === 0 && (
          <p className="text-[11px] text-amber-700 mt-1">{t('admin.teamBracketsNoBrackets')}</p>
        )}
        <div className="mt-1 flex items-center gap-2">
          <button onClick={submit} disabled={busy || pickedEventId === '' || pickedBracketId === ''}
            className="text-xs bg-emerald-700 text-white font-semibold px-2 py-1 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {t('admin.teamBracketsSubmit')}
          </button>
          {(pickedEventId !== '' || pickedTierId !== '' || pickedBracketId !== '') && (
            <button onClick={resetPicker} disabled={busy}
              className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
          )}
        </div>
      </div>
    </div>
  )
}
