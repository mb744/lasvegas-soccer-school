import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  AdminPlayerSummary,
  PlayerUniformAssignment,
  Uniform,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const designationLabel = (d: number): string | null => {
  switch (d) {
    case 1: return 'Home'
    case 2: return 'Away'
    case 3: return 'Practice'
    default: return null
  }
}

/** Admin Players hub. Lists every player in the system with parent/team/registration context,
 *  an at-a-glance jersey-number column, and per-row actions for:
 *    - Uniform tracking (assign a uniform from the catalog + jersey number + date; players can
 *      have multiple active assignments).
 *    - Send registration invite email (parent gets a link to /register to sign the waiver +
 *      add player info for the active season).
 *  Top-of-page button to admin-create a new player (with parent picker or new-parent inline). */
export function AdminPlayersPage() {
  const { t } = useTranslation()
  const [players, setPlayers] = useState<AdminPlayerSummary[]>([])
  const [query, setQuery] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showAdd, setShowAdd] = useState(false)

  const refresh = async (q: string) => {
    try {
      setPlayers(await Api.listAdminPlayers(q))
    } catch (e: any) { setError(errMsg(e)) }
  }

  // Debounce the query so a typed search doesn't hammer the API on each keystroke.
  useEffect(() => {
    const id = setTimeout(() => refresh(query), 200)
    return () => clearTimeout(id)
  }, [query])

  const selected = useMemo(
    () => players.find(p => p.id === selectedId) ?? null,
    [players, selectedId])

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-4">
        <div className="flex items-start justify-between flex-wrap gap-2">
          <div>
            <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
            <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.playersTitle')}</h1>
            <p className="text-sm text-slate-600 mt-1">{t('admin.playersSubtitle')}</p>
          </div>
          <button onClick={() => setShowAdd(s => !s)}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
            {showAdd ? t('admin.cancel') : '+ ' + t('admin.playersAddNew')}
          </button>
        </div>

        {error && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>
        )}
        {notice && (
          <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>
        )}

        {showAdd && (
          <AddPlayerForm
            onCreated={async () => { await refresh(query); setShowAdd(false); setNotice(t('admin.playersAddedNotice')) }}
            onError={(e) => { setError(e); setNotice(null) }}
            onCancel={() => setShowAdd(false)} />
        )}

        <div className="flex items-center gap-2">
          <input type="text" value={query} onChange={e => setQuery(e.target.value)}
            placeholder={t('admin.playersSearchPlaceholder')}
            className="w-full max-w-md border border-slate-300 rounded-md px-3 py-2 text-sm" />
          <button onClick={() => refresh(query)} className="text-sm text-emerald-700 hover:underline">↻</button>
          <span className="text-xs text-slate-400 ml-auto">
            {t('admin.playersCount', { count: players.length })}
          </span>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-500 border-b">
                <th className="py-2 px-3">{t('admin.playersColName')}</th>
                <th className="py-2 px-3">{t('admin.playersColDob')}</th>
                <th className="py-2 px-3">{t('admin.playersColBracket')}</th>
                <th className="py-2 px-3">{t('admin.playersColParent')}</th>
                <th className="py-2 px-3">{t('admin.playersColTeam')}</th>
                <th className="py-2 px-3">{t('admin.playersColReg')}</th>
                <th className="py-2 px-3">{t('admin.playersColJersey')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {players.map(p => (
                <tr key={p.id} className="border-b last:border-0 align-top">
                  <td className="py-2 px-3">
                    <div className="font-medium text-slate-800">{p.firstName} {p.lastName}</div>
                    <div className="text-[10px] text-slate-400">#{p.id}</div>
                  </td>
                  <td className="py-2 px-3 whitespace-nowrap">{p.dateOfBirth}</td>
                  <td className="py-2 px-3">{p.ageBracket ?? <span className="text-slate-400">—</span>}</td>
                  <td className="py-2 px-3">
                    <div>{p.parentName ?? <span className="text-slate-400">—</span>}</div>
                    <div className="text-[11px] text-slate-500">{p.parentCellPhone ?? ''}</div>
                    <div className="text-[11px] text-slate-500">{p.parentEmail ?? ''}</div>
                  </td>
                  <td className="py-2 px-3">{p.currentTeamName ?? <span className="text-slate-400">—</span>}</td>
                  <td className="py-2 px-3 whitespace-nowrap text-xs">
                    {p.waiverSigned
                      ? <span className="text-emerald-700">✓ {t('admin.playersRegSigned')}</span>
                      : p.registeredThisSeason
                        ? <span className="text-amber-700">… {t('admin.playersRegPending')}</span>
                        : <span className="text-slate-400">— {t('admin.playersRegNone')}</span>}
                  </td>
                  <td className="py-2 px-3 text-xs">
                    {p.activeJerseyNumbers
                      ? <span className="font-mono">{p.activeJerseyNumbers}</span>
                      : <span className="text-slate-400">—</span>}
                    {p.uniformCount > 0 && (
                      <span className="ml-1 text-[10px] text-slate-400">({p.uniformCount})</span>
                    )}
                  </td>
                  <td className="py-2 px-3 whitespace-nowrap text-right text-xs space-x-2">
                    <button onClick={() => setSelectedId(p.id === selectedId ? null : p.id)}
                      className="text-emerald-700 hover:underline">
                      {selectedId === p.id ? t('admin.hide') : t('admin.playersManageUniforms')}
                    </button>
                    {p.parentEmail && p.parentAccountId !== null && (
                      <button onClick={() => sendInvite(p, setError, setNotice, t)}
                        className="text-emerald-700 hover:underline">
                        {t('admin.playersSendInvite')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {players.length === 0 && (
                <tr><td colSpan={8} className="py-6 text-center text-sm text-slate-400">{t('admin.playersEmpty')}</td></tr>
              )}
            </tbody>
          </table>
        </div>

        {selected && (
          <PlayerUniformPanel player={selected}
            onClose={() => setSelectedId(null)}
            onChanged={() => refresh(query)}
            onError={(e) => { setError(e); setNotice(null) }}
            onNotice={(n) => { setNotice(n); setError(null) }} />
        )}
      </div>
    </Layout>
  )
}

async function sendInvite(
  p: AdminPlayerSummary,
  setError: (e: string) => void,
  setNotice: (n: string) => void,
  t: (key: string, opts?: any) => string,
) {
  if (p.parentAccountId === null) return
  if (!confirm(t('admin.playersSendInviteConfirm', { email: p.parentEmail ?? '' }))) return
  try {
    const r = await Api.sendRegistrationInvite({ parentAccountId: p.parentAccountId })
    setNotice(r.message)
  } catch (e: any) {
    setError(errMsg(e))
  }
}

function AddPlayerForm({ onCreated, onError, onCancel }: {
  onCreated: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [dob, setDob] = useState('')
  const [parentEmail, setParentEmail] = useState('')
  const [parentFirstName, setParentFirstName] = useState('')
  const [parentLastName, setParentLastName] = useState('')
  const [parentPhone, setParentPhone] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!firstName.trim() || !lastName.trim() || !dob) { onError(t('admin.playersAddRequired')); return }
    if (!parentEmail.trim()) { onError(t('admin.playersAddParentEmailRequired')); return }
    setBusy(true)
    try {
      await Api.createAdminPlayer({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        dateOfBirth: dob,
        newParentEmail: parentEmail.trim(),
        newParentFirstName: parentFirstName.trim() || null,
        newParentLastName: parentLastName.trim() || null,
        newParentCellPhone: parentPhone.trim() || null,
      })
      await onCreated()
    } catch (e: any) {
      onError(errMsg(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="bg-white border border-emerald-200 rounded-lg p-4 space-y-3">
      <h3 className="font-semibold text-emerald-800">{t('admin.playersAddTitle')}</h3>
      <p className="text-xs text-slate-500">{t('admin.playersAddBlurb')}</p>
      <div className="grid sm:grid-cols-3 gap-2">
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddFirstName')}</span>
          <input type="text" value={firstName} onChange={e => setFirstName(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddLastName')}</span>
          <input type="text" value={lastName} onChange={e => setLastName(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddDob')}</span>
          <input type="date" value={dob} onChange={e => setDob(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
      </div>
      <div className="grid sm:grid-cols-2 gap-2">
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddParentEmail')}</span>
          <input type="email" value={parentEmail} onChange={e => setParentEmail(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddParentPhone')}</span>
          <input type="tel" value={parentPhone} onChange={e => setParentPhone(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddParentFirstName')}</span>
          <input type="text" value={parentFirstName} onChange={e => setParentFirstName(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.playersAddParentLastName')}</span>
          <input type="text" value={parentLastName} onChange={e => setParentLastName(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
      </div>
      <div className="flex gap-2">
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.playersAddSubmit')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy} className="text-sm text-slate-600 hover:underline">
          {t('admin.cancel')}
        </button>
      </div>
    </form>
  )
}

function PlayerUniformPanel({
  player, onClose, onChanged, onError, onNotice,
}: {
  player: AdminPlayerSummary
  onClose: () => void
  onChanged: () => void | Promise<void>
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [rows, setRows] = useState<PlayerUniformAssignment[]>([])
  const [catalog, setCatalog] = useState<Uniform[]>([])
  const [busy, setBusy] = useState(false)

  // Add-form state.
  const [uniformId, setUniformId] = useState<number | ''>('')
  const [jerseyNumber, setJerseyNumber] = useState('')
  const [assignedAt, setAssignedAt] = useState(() => new Date().toISOString().slice(0, 10))
  const [notes, setNotes] = useState('')

  const refresh = async () => {
    try {
      const [r, c] = await Promise.all([Api.listPlayerUniforms(player.id), Api.listUniforms()])
      setRows(r); setCatalog(c)
    } catch (e: any) { onError(errMsg(e)) }
  }

  useEffect(() => { refresh() }, [player.id])

  const addAssignment = async () => {
    if (uniformId === '' || !jerseyNumber.trim() || !assignedAt) {
      onError(t('admin.playersUniformAddRequired'))
      return
    }
    setBusy(true)
    try {
      await Api.createPlayerUniform(player.id, {
        uniformId: Number(uniformId),
        jerseyNumber: jerseyNumber.trim(),
        assignedAt,
        notes: notes.trim() || null,
      })
      setJerseyNumber(''); setNotes(''); setUniformId('')
      await refresh()
      await onChanged()
      onNotice(t('admin.playersUniformAddedNotice'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const markReturned = async (r: PlayerUniformAssignment) => {
    try {
      await Api.updatePlayerUniform(player.id, r.id, {
        jerseyNumber: r.jerseyNumber,
        assignedAt: r.assignedAt,
        returnedAt: new Date().toISOString().slice(0, 10),
        notes: r.notes ?? null,
      })
      await refresh()
      await onChanged()
    } catch (e: any) { onError(errMsg(e)) }
  }

  const remove = async (r: PlayerUniformAssignment) => {
    if (!confirm(t('admin.playersUniformDeleteConfirm', { jersey: r.jerseyNumber }))) return
    try {
      await Api.deletePlayerUniform(player.id, r.id)
      await refresh()
      await onChanged()
    } catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <section className="bg-white border border-emerald-200 rounded-lg p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold text-emerald-800">
          {t('admin.playersUniformHeader', { name: `${player.firstName} ${player.lastName}` })}
        </h3>
        <button onClick={onClose} className="text-sm text-slate-500 hover:underline">{t('admin.hide')}</button>
      </div>

      <div className="bg-slate-50 border border-slate-200 rounded p-3 space-y-2">
        <div className="text-xs font-medium text-emerald-800">{t('admin.playersUniformAddTitle')}</div>
        <div className="grid sm:grid-cols-4 gap-2">
          <label className="text-xs sm:col-span-2">
            <span className="text-slate-700">{t('admin.playersUniformPickKit')}</span>
            <select value={uniformId} onChange={e => setUniformId(e.target.value === '' ? '' : Number(e.target.value))}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
              <option value="">—</option>
              {catalog.map(u => (
                <option key={u.id} value={u.id}>
                  {u.name}{designationLabel(u.designation) ? ` (${designationLabel(u.designation)})` : ''}
                </option>
              ))}
            </select>
          </label>
          <label className="text-xs">
            <span className="text-slate-700">{t('admin.playersUniformJersey')}</span>
            <input type="text" value={jerseyNumber} onChange={e => setJerseyNumber(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono"
              maxLength={16} />
          </label>
          <label className="text-xs">
            <span className="text-slate-700">{t('admin.playersUniformAssignedAt')}</span>
            <input type="date" value={assignedAt} onChange={e => setAssignedAt(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
          </label>
          <label className="text-xs sm:col-span-4">
            <span className="text-slate-700">{t('admin.playersUniformNotes')}</span>
            <input type="text" value={notes} onChange={e => setNotes(e.target.value)}
              maxLength={500}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
          </label>
        </div>
        <div>
          <button onClick={addAssignment} disabled={busy}
            className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {busy ? t('admin.sending') : t('admin.playersUniformAddSubmit')}
          </button>
        </div>
      </div>

      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-slate-500 border-b">
            <th className="py-1 px-2">{t('admin.playersUniformKit')}</th>
            <th className="py-1 px-2">{t('admin.playersUniformJersey')}</th>
            <th className="py-1 px-2">{t('admin.playersUniformAssignedAt')}</th>
            <th className="py-1 px-2">{t('admin.playersUniformReturnedAt')}</th>
            <th className="py-1 px-2">{t('admin.playersUniformNotes')}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className={`border-b last:border-0 ${r.returnedAt ? 'text-slate-400 line-through' : ''}`}>
              <td className="py-1 px-2">
                {r.uniformName}{r.uniformDesignation ? ` (${r.uniformDesignation})` : ''}
              </td>
              <td className="py-1 px-2 font-mono">{r.jerseyNumber}</td>
              <td className="py-1 px-2 whitespace-nowrap">{r.assignedAt}</td>
              <td className="py-1 px-2 whitespace-nowrap">{r.returnedAt ?? '—'}</td>
              <td className="py-1 px-2">{r.notes ?? ''}</td>
              <td className="py-1 px-2 whitespace-nowrap text-right">
                {!r.returnedAt && (
                  <button onClick={() => markReturned(r)} className="text-amber-700 hover:underline">
                    {t('admin.playersUniformMarkReturned')}
                  </button>
                )}
                <button onClick={() => remove(r)} className="text-rose-700 hover:underline ml-2">
                  {t('admin.delete')}
                </button>
              </td>
            </tr>
          ))}
          {rows.length === 0 && (
            <tr><td colSpan={6} className="py-3 text-center text-slate-400">{t('admin.playersUniformEmpty')}</td></tr>
          )}
        </tbody>
      </table>
    </section>
  )
}
