import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  Coach, CoachSummary, CoachCertification,
  SaveCoachRecordRequest, SaveCoachCertificationRequest, Language,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const emptyCoachForm: SaveCoachRecordRequest = {
  firstName: '', lastName: '',
  cellPhone: '', hasWhatsApp: false, email: '',
  addressLine1: '', addressLine2: '', city: '', state: '', postalCode: '',
  monthlyPayment: null, notes: '', language: 0,
}

const emptyCertForm: SaveCoachCertificationRequest = {
  name: '', issuingBody: '', issuedOn: null, expiresOn: null,
  certificateNumber: '', notes: '',
}

export function AdminCoachesPage() {
  const { t } = useTranslation()
  const [coaches, setCoaches] = useState<CoachSummary[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detail, setDetail] = useState<Coach | null>(null)
  const [editing, setEditing] = useState(false)
  const [creating, setCreating] = useState(false)
  const [form, setForm] = useState<SaveCoachRecordRequest>(emptyCoachForm)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const refresh = async () => {
    try { setCoaches(await Api.listCoaches()) }
    catch (e: any) { setError(errMsg(e)) }
  }
  useEffect(() => { refresh() }, [])

  const select = async (id: number) => {
    setSelectedId(id); setEditing(false); setCreating(false)
    try { setDetail(await Api.getCoach(id)) }
    catch (e: any) { setError(errMsg(e)) }
  }

  const startCreate = () => {
    setCreating(true); setEditing(false); setSelectedId(null); setDetail(null)
    setForm(emptyCoachForm); setError(null); setNotice(null)
  }
  const startEdit = () => {
    if (!detail) return
    setForm({
      firstName: detail.firstName, lastName: detail.lastName,
      cellPhone: detail.cellPhone, hasWhatsApp: detail.hasWhatsApp, email: detail.email,
      addressLine1: detail.addressLine1, addressLine2: detail.addressLine2,
      city: detail.city, state: detail.state, postalCode: detail.postalCode,
      monthlyPayment: detail.monthlyPayment, notes: detail.notes,
      language: detail.language,
    })
    setEditing(true); setError(null); setNotice(null)
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null); setNotice(null)
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setError(t('admin.coachNameRequired')); return
    }
    setBusy(true)
    try {
      if (creating) {
        const created = await Api.createCoach(form)
        await refresh()
        setSelectedId(created.id); setDetail(created)
        setCreating(false); setNotice(t('admin.coachCreated'))
      } else if (editing && detail) {
        const updated = await Api.updateCoach(detail.id, form)
        await refresh()
        setDetail(updated); setEditing(false); setNotice(t('admin.coachUpdated'))
      }
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async () => {
    if (!detail) return
    if (!confirm(t('admin.coachDeleteConfirm', { name: `${detail.firstName} ${detail.lastName}`.trim() }))) return
    setError(null); setNotice(null)
    try {
      await Api.deleteCoach(detail.id)
      await refresh()
      setSelectedId(null); setDetail(null)
      setNotice(t('admin.coachDeleted'))
    } catch (e: any) { setError(errMsg(e)) }
  }

  const sortedCoaches = useMemo(
    () => [...coaches].sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`)),
    [coaches])

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-8 space-y-4">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-2xl font-bold text-emerald-800 mt-2">{t('admin.coachesTitle')}</h1>
          <p className="mt-1 text-sm text-slate-500">{t('admin.coachesSubtitle')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        <div className="grid lg:grid-cols-3 gap-4">
          {/* Coach list */}
          <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1">
            <div className="flex items-center justify-between mb-2">
              <h2 className="font-bold text-emerald-800">{t('admin.coachesList')}</h2>
              <button onClick={startCreate} className="text-sm text-emerald-700 hover:underline">+ {t('admin.coachAdd')}</button>
            </div>
            <ul className="space-y-1 max-h-[65vh] overflow-y-auto">
              {sortedCoaches.map(c => (
                <li key={c.id}>
                  <button onClick={() => select(c.id)}
                    className={`w-full text-left px-2 py-2 rounded text-sm hover:bg-emerald-50 ${selectedId === c.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                    <div className="font-medium">{c.lastName}, {c.firstName}</div>
                    <div className="text-xs text-slate-500 font-mono">{c.cellPhone ?? '—'}</div>
                    <div className="text-[11px] text-slate-400 mt-0.5">
                      {c.certificationCount > 0 && <>{t('admin.coachCertCount', { count: c.certificationCount })} · </>}
                      {c.monthlyPayment !== null && <>${c.monthlyPayment.toFixed(2)}/mo</>}
                    </div>
                  </button>
                </li>
              ))}
              {sortedCoaches.length === 0 && (
                <li className="text-sm text-slate-400 py-4 text-center">{t('admin.coachesEmpty')}</li>
              )}
            </ul>
          </section>

          {/* Detail / edit pane */}
          <section className="lg:col-span-2 space-y-4">
            {!creating && !editing && !detail && (
              <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
                {t('admin.coachPickPrompt')}
              </div>
            )}

            {(creating || editing) && (
              <CoachForm form={form} setForm={setForm} busy={busy} onSave={save}
                onCancel={() => { setCreating(false); setEditing(false); if (selectedId) select(selectedId) }} />
            )}

            {!creating && !editing && detail && (
              <CoachDetail detail={detail} onEdit={startEdit} onDelete={remove}
                onCertChanged={(updated) => { setDetail(updated); refresh() }}
                onError={setError} onNotice={setNotice} />
            )}
          </section>
        </div>
      </div>
    </Layout>
  )
}

function CoachForm({
  form, setForm, busy, onSave, onCancel,
}: {
  form: SaveCoachRecordRequest
  setForm: (f: SaveCoachRecordRequest) => void
  busy: boolean
  onSave: (e: React.FormEvent) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const u = (patch: Partial<SaveCoachRecordRequest>) => setForm({ ...form, ...patch })

  return (
    <form onSubmit={onSave} noValidate className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
      <div className="grid sm:grid-cols-2 gap-3">
        <label className="block text-sm">
          <span className="font-medium text-slate-700">{t('admin.coachFirstName')}</span>
          <input type="text" required value={form.firstName} onChange={e => u({ firstName: e.target.value })}
            className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm">
          <span className="font-medium text-slate-700">{t('admin.coachLastName')}</span>
          <input type="text" required value={form.lastName} onChange={e => u({ lastName: e.target.value })}
            className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm">
          <span className="font-medium text-slate-700">{t('admin.coachCellPhone')}</span>
          <input type="tel" value={form.cellPhone ?? ''} onChange={e => u({ cellPhone: e.target.value })}
            placeholder="+1 (702) 555-0100"
            className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm">
          <span className="font-medium text-slate-700">{t('admin.coachEmail')}</span>
          <input type="email" value={form.email ?? ''} onChange={e => u({ email: e.target.value })}
            className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm flex items-center gap-2 pt-6">
          <input type="checkbox" checked={form.hasWhatsApp} onChange={e => u({ hasWhatsApp: e.target.checked })} />
          <span>{t('admin.coachHasWhatsApp')}</span>
        </label>
        <label className="block text-sm">
          <span className="font-medium text-slate-700">{t('admin.language')}</span>
          <select value={form.language} onChange={e => u({ language: Number(e.target.value) as Language })}
            className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
            <option value={0}>English</option>
            <option value={1}>Español</option>
          </select>
        </label>
      </div>

      <fieldset className="border border-slate-200 rounded-md p-3">
        <legend className="text-xs font-medium text-slate-600 px-1">{t('admin.coachAddress')}</legend>
        <div className="grid sm:grid-cols-2 gap-3 mt-1">
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.coachAddressLine1')}</span>
            <input type="text" value={form.addressLine1 ?? ''} onChange={e => u({ addressLine1: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.coachAddressLine2')}</span>
            <input type="text" value={form.addressLine2 ?? ''} onChange={e => u({ addressLine2: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachCity')}</span>
            <input type="text" value={form.city ?? ''} onChange={e => u({ city: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachState')}</span>
            <input type="text" value={form.state ?? ''} onChange={e => u({ state: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachPostalCode')}</span>
            <input type="text" value={form.postalCode ?? ''} onChange={e => u({ postalCode: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachMonthlyPayment')}</span>
            <input type="number" min={0} step="0.01" value={form.monthlyPayment ?? ''}
              onChange={e => u({ monthlyPayment: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder="0.00"
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
        </div>
      </fieldset>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">{t('admin.coachNotes')}</span>
        <textarea rows={3} value={form.notes ?? ''} onChange={e => u({ notes: e.target.value })}
          className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
      </label>

      <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.save')}
        </button>
        <button type="button" onClick={onCancel} className="text-sm text-slate-600 hover:underline">
          {t('admin.cancel')}
        </button>
      </div>
    </form>
  )
}

function CoachDetail({
  detail, onEdit, onDelete, onCertChanged, onError, onNotice,
}: {
  detail: Coach
  onEdit: () => void
  onDelete: () => void
  onCertChanged: (updated: Coach) => void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const fullName = `${detail.firstName} ${detail.lastName}`.trim()
  const addressLines = [detail.addressLine1, detail.addressLine2,
    [detail.city, detail.state, detail.postalCode].filter(Boolean).join(', ')]
    .filter(Boolean) as string[]

  return (
    <>
      <section className="bg-white border border-slate-200 rounded-lg p-4">
        <div className="flex items-start justify-between gap-3 flex-wrap">
          <div>
            <h2 className="text-lg font-bold text-emerald-800">{fullName}</h2>
            <div className="text-xs text-slate-500 mt-1 font-mono">{detail.cellPhone ?? '—'} {detail.hasWhatsApp && <span className="ml-1 text-emerald-700">· WA</span>}</div>
            <div className="text-xs text-slate-500">{detail.email ?? '—'}</div>
            <div className="text-xs text-slate-500 mt-1">{detail.language === 1 ? 'Español' : 'English'}</div>
          </div>
          <div className="text-sm whitespace-nowrap">
            <button onClick={onEdit} className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
            <span className="mx-2 text-slate-300">|</span>
            <button onClick={onDelete} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
          </div>
        </div>

        <div className="grid sm:grid-cols-2 gap-3 mt-3 text-sm">
          <div>
            <div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t('admin.coachAddress')}</div>
            {addressLines.length === 0
              ? <div className="text-slate-400">—</div>
              : addressLines.map((l, i) => <div key={i} className="text-slate-700">{l}</div>)}
          </div>
          <div>
            <div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t('admin.coachMonthlyPayment')}</div>
            <div className="text-slate-700">{detail.monthlyPayment !== null ? `$${detail.monthlyPayment.toFixed(2)}/mo` : '—'}</div>
          </div>
        </div>

        {detail.notes && (
          <div className="mt-3 text-sm">
            <div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t('admin.coachNotes')}</div>
            <div className="text-slate-700 whitespace-pre-wrap">{detail.notes}</div>
          </div>
        )}
      </section>

      <CoachCertsSection detail={detail} onChanged={onCertChanged}
        onError={onError} onNotice={onNotice} />
    </>
  )
}

function CoachCertsSection({
  detail, onChanged, onError, onNotice,
}: {
  detail: Coach
  onChanged: (updated: Coach) => void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [adding, setAdding] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [form, setForm] = useState<SaveCoachCertificationRequest>(emptyCertForm)
  const [busy, setBusy] = useState(false)

  const cancel = () => { setAdding(false); setEditingId(null) }
  const startAdd = () => { setForm(emptyCertForm); setEditingId(null); setAdding(true) }
  const startEdit = (c: CoachCertification) => {
    setForm({
      name: c.name, issuingBody: c.issuingBody,
      issuedOn: c.issuedOn, expiresOn: c.expiresOn,
      certificateNumber: c.certificateNumber, notes: c.notes,
    })
    setEditingId(c.id); setAdding(false)
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.name.trim()) { onError(t('admin.coachCertNameRequired')); return }
    setBusy(true); onError(''); onNotice('')
    try {
      const payload = {
        name: form.name.trim(),
        issuingBody: form.issuingBody?.trim() || null,
        issuedOn: form.issuedOn || null,
        expiresOn: form.expiresOn || null,
        certificateNumber: form.certificateNumber?.trim() || null,
        notes: form.notes?.trim() || null,
      }
      const updated = editingId !== null
        ? await Api.updateCoachCertification(detail.id, editingId, payload)
        : await Api.addCoachCertification(detail.id, payload)
      onChanged(updated)
      onNotice(editingId !== null ? t('admin.coachCertUpdated') : t('admin.coachCertAdded'))
      cancel()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async (c: CoachCertification) => {
    if (!confirm(t('admin.coachCertRemoveConfirm', { name: c.name }))) return
    try {
      const updated = await Api.removeCoachCertification(detail.id, c.id)
      onChanged(updated)
      onNotice(t('admin.coachCertRemoved'))
    } catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-4">
      <div className="flex items-center justify-between mb-2">
        <h2 className="font-bold text-emerald-800">{t('admin.coachCerts')}</h2>
        {!adding && editingId === null && (
          <button onClick={startAdd} className="text-sm text-emerald-700 hover:underline">+ {t('admin.coachCertAdd')}</button>
        )}
      </div>
      {detail.certifications.length === 0 && !adding && (
        <p className="text-xs text-slate-400">{t('admin.coachCertsNone')}</p>
      )}
      {detail.certifications.length > 0 && (
        <ul className="divide-y divide-slate-100 border border-slate-200 rounded">
          {detail.certifications.map(c => (
            <li key={c.id} className="px-3 py-2 flex items-start justify-between gap-3 text-sm">
              <div>
                <div className="font-medium text-slate-800">{c.name}
                  {c.issuingBody && <span className="ml-2 text-xs text-slate-500">· {c.issuingBody}</span>}
                </div>
                <div className="text-xs text-slate-500">
                  {c.issuedOn && <>{t('admin.coachCertIssued')}: {c.issuedOn}</>}
                  {c.expiresOn && <> · {t('admin.coachCertExpires')}: {c.expiresOn}</>}
                  {c.certificateNumber && <> · #{c.certificateNumber}</>}
                </div>
                {c.notes && <div className="text-xs text-slate-500 mt-0.5">{c.notes}</div>}
              </div>
              <div className="flex items-center gap-2 whitespace-nowrap text-xs">
                <button onClick={() => startEdit(c)} className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                <button onClick={() => remove(c)} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {(adding || editingId !== null) && (
        <form onSubmit={submit} className="mt-3 grid sm:grid-cols-2 gap-3 border border-emerald-200 rounded-md p-3 bg-emerald-50/40">
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.coachCertName')}</span>
            <input type="text" required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })}
              placeholder="USSF Grassroots 4v4"
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachCertIssuingBody')}</span>
            <input type="text" value={form.issuingBody ?? ''} onChange={e => setForm({ ...form, issuingBody: e.target.value })}
              placeholder="US Soccer"
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachCertNumber')}</span>
            <input type="text" value={form.certificateNumber ?? ''} onChange={e => setForm({ ...form, certificateNumber: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachCertIssued')}</span>
            <input type="date" value={form.issuedOn ?? ''} onChange={e => setForm({ ...form, issuedOn: e.target.value || null })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.coachCertExpires')}</span>
            <input type="date" value={form.expiresOn ?? ''} onChange={e => setForm({ ...form, expiresOn: e.target.value || null })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium text-slate-700">{t('admin.coachNotes')}</span>
            <textarea rows={2} value={form.notes ?? ''} onChange={e => setForm({ ...form, notes: e.target.value })}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <div className="sm:col-span-2 flex items-center gap-3">
            <button type="submit" disabled={busy}
              className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {busy ? t('admin.sending') : t('admin.save')}
            </button>
            <button type="button" onClick={cancel} className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
          </div>
        </form>
      )}
    </section>
  )
}
