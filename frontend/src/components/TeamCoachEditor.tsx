import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import type {
  TeamCoach, SaveTeamCoachRequest, CoachSummary,
  Language, TeamCoachRole, RosterTeamDetail, TeamDetail,
} from '../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

interface Form {
  name: string
  email: string
  phone: string
  language: Language
  hasWhatsApp: boolean
  coachId: number | null
  role: TeamCoachRole
}

const emptyForm: Form = {
  name: '', email: '', phone: '', language: 0, hasWhatsApp: false, coachId: null, role: 0,
}

/** Shared per-team coach list editor used by the Teams admin card and by the per-team tab
 *  inside a Tournament/League. Supports picking from the admin Coach roster (one-time
 *  fetch, cached for the session), free-text manual entry, and a Head/Assistant role. */
export function TeamCoachEditor({
  coaches, onAdd, onUpdate, onRemove, onChanged, onError, onNotice,
}: {
  coaches: TeamCoach[]
  onAdd: (payload: SaveTeamCoachRequest) => Promise<RosterTeamDetail | TeamDetail>
  onUpdate: (coachId: number, payload: SaveTeamCoachRequest) => Promise<RosterTeamDetail | TeamDetail>
  onRemove: (coachId: number) => Promise<RosterTeamDetail | TeamDetail | void>
  onChanged: (updated: RosterTeamDetail | TeamDetail) => void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [adding, setAdding] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [form, setForm] = useState<Form>(emptyForm)
  const [busy, setBusy] = useState(false)
  const [roster, setRoster] = useState<CoachSummary[] | null>(null)
  const [rosterLoading, setRosterLoading] = useState(false)

  // Lazy-load the Coach roster the first time the admin opens the form.
  useEffect(() => {
    if (!adding && editingId === null) return
    if (roster !== null || rosterLoading) return
    setRosterLoading(true)
    Api.listCoaches()
      .then(setRoster)
      .catch((e) => onError(errMsg(e)))
      .finally(() => setRosterLoading(false))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [adding, editingId])

  const startAdd = () => {
    setEditingId(null)
    setForm(emptyForm)
    setAdding(true)
  }
  const startEdit = (c: TeamCoach) => {
    setAdding(false)
    setEditingId(c.id)
    setForm({
      name: c.name, email: c.email ?? '', phone: c.phone ?? '',
      language: c.language, hasWhatsApp: c.hasWhatsApp,
      coachId: c.coachId, role: c.role,
    })
  }
  const cancel = () => { setAdding(false); setEditingId(null) }

  /** Picking from the roster fills the visible fields too so the admin can see what's
   *  going to be saved. Backend still pulls the canonical values from the Coach record. */
  const pickFromRoster = (coachId: number | null) => {
    if (coachId === null) {
      setForm(f => ({ ...f, coachId: null }))
      return
    }
    const c = roster?.find(x => x.id === coachId)
    setForm(f => ({
      ...f,
      coachId,
      name: c ? `${c.firstName} ${c.lastName}`.trim() : f.name,
      email: c?.email ?? f.email,
      phone: c?.cellPhone ?? f.phone,
    }))
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (form.coachId === null && !form.name.trim()) {
      onError(t('admin.teamCoachNameRequired'))
      return
    }
    setBusy(true); onError(''); onNotice('')
    try {
      const payload: SaveTeamCoachRequest = {
        name: form.name.trim(),
        email: form.email.trim() || null,
        phone: form.phone.trim() || null,
        language: form.language,
        hasWhatsApp: form.hasWhatsApp,
        coachId: form.coachId,
        role: form.role,
      }
      const updated = editingId !== null
        ? await onUpdate(editingId, payload)
        : await onAdd(payload)
      onChanged(updated)
      onNotice(editingId !== null ? t('admin.teamCoachUpdated') : t('admin.teamCoachAdded'))
      cancel()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async (c: TeamCoach) => {
    if (!confirm(t('admin.teamCoachRemoveConfirm', { name: c.name }))) return
    onError(''); onNotice('')
    try {
      const updated = await onRemove(c.id)
      if (updated) onChanged(updated)
      onNotice(t('admin.teamCoachRemoved'))
    } catch (e: any) { onError(errMsg(e)) }
  }

  const roleBadge = (r: TeamCoachRole) =>
    r === 0 ? t('admin.teamCoachRoleHead') : t('admin.teamCoachRoleAssistant')

  return (
    <div className="space-y-3">
      {coaches.length === 0 ? (
        <p className="text-xs text-slate-400">{t('admin.teamCoachNone')}</p>
      ) : (
        <ul className="divide-y divide-slate-100 border border-slate-200 rounded-md bg-white">
          {coaches.map(c => (
            <li key={c.id} className="px-3 py-2 flex items-start justify-between gap-3">
              <div className="text-sm">
                <div className="font-medium text-slate-800 flex items-center gap-2 flex-wrap">
                  <span>{c.name}</span>
                  <span className={`text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded ${c.role === 0
                    ? 'bg-emerald-100 text-emerald-800'
                    : 'bg-slate-100 text-slate-700'}`}>
                    {roleBadge(c.role)}
                  </span>
                  {c.coachId !== null && (
                    <span className="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-sky-100 text-sky-800"
                      title={t('admin.teamCoachLinkedTitle')}>
                      {t('admin.teamCoachLinkedBadge')}
                    </span>
                  )}
                  <span className="text-[10px] uppercase tracking-wide text-slate-500">
                    {c.language === 1 ? 'ES' : 'EN'}{c.hasWhatsApp ? ' · WA' : ''}
                  </span>
                </div>
                <div className="text-xs text-slate-500 font-mono">
                  {c.phone ?? '—'}{c.email ? ` · ${c.email}` : ''}
                </div>
              </div>
              <div className="flex items-center gap-2 whitespace-nowrap">
                <button onClick={() => startEdit(c)} className="text-xs text-emerald-700 hover:underline">{t('admin.edit')}</button>
                <button onClick={() => remove(c)} className="text-xs text-rose-700 hover:underline">{t('admin.delete')}</button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {!adding && editingId === null && (
        <button onClick={startAdd} className="text-sm text-emerald-700 hover:underline">
          + {t('admin.teamCoachAdd')}
        </button>
      )}

      {(adding || editingId !== null) && (
        <form onSubmit={submit} className="grid sm:grid-cols-2 gap-3 border border-emerald-200 rounded-md p-3 bg-emerald-50/40">
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.teamCoachPickFromRoster')}</span>
            <select value={form.coachId ?? ''}
              onChange={e => pickFromRoster(e.target.value === '' ? null : Number(e.target.value))}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
              <option value="">{rosterLoading ? t('common.loading') : t('admin.teamCoachManualEntry')}</option>
              {(roster ?? []).map(c => (
                <option key={c.id} value={c.id}>{c.lastName}, {c.firstName}{c.cellPhone ? ` (${c.cellPhone})` : ''}</option>
              ))}
            </select>
            <span className="block text-[11px] text-slate-500 mt-0.5">{t('admin.teamCoachPickHelp')}</span>
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.teamCoachName')}</span>
            <input type="text" value={form.name} required={form.coachId === null}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              disabled={form.coachId !== null}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm disabled:bg-slate-100" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.teamCoachPhone')}</span>
            <input type="tel" value={form.phone}
              onChange={e => setForm(f => ({ ...f, phone: e.target.value }))}
              disabled={form.coachId !== null}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm disabled:bg-slate-100" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.teamCoachEmail')}</span>
            <input type="email" value={form.email}
              onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
              disabled={form.coachId !== null}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm disabled:bg-slate-100" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.teamCoachRole')}</span>
            <select value={form.role}
              onChange={e => setForm(f => ({ ...f, role: Number(e.target.value) as TeamCoachRole }))}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
              <option value={0}>{t('admin.teamCoachRoleHead')}</option>
              <option value={1}>{t('admin.teamCoachRoleAssistant')}</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.language')}</span>
            <select value={form.language}
              onChange={e => setForm(f => ({ ...f, language: Number(e.target.value) as Language }))}
              disabled={form.coachId !== null}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm disabled:bg-slate-100">
              <option value={0}>English</option>
              <option value={1}>Español</option>
            </select>
          </label>
          <label className="block text-sm flex items-center gap-2 pt-6">
            <input type="checkbox" checked={form.hasWhatsApp} disabled={form.coachId !== null}
              onChange={e => setForm(f => ({ ...f, hasWhatsApp: e.target.checked }))} />
            <span>{t('admin.teamCoachWhatsApp')}</span>
          </label>
          <div className="sm:col-span-2 flex gap-2">
            <button type="submit" disabled={busy}
              className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {busy ? t('admin.sending') : t('admin.save')}
            </button>
            <button type="button" onClick={cancel} className="text-sm text-slate-600 hover:underline">
              {t('admin.cancel')}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
