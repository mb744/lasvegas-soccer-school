import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type { Language, ParentContactInput, RegistrationDetail, RegistrationPlayerDetail, RegistrationSummary, UpdateRegistrationRequest } from '../../api/types'

export function AdminRegistrationsPage() {
  const { t } = useTranslation()
  const [registrations, setRegistrations] = useState<RegistrationSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [details, setDetails] = useState<Record<number, RegistrationDetail>>({})
  const [loadingDetail, setLoadingDetail] = useState<number | null>(null)

  const load = async () => {
    setError(null)
    try { setRegistrations(await Api.listRegistrations()) }
    catch (e: any) { setError(e?.message ?? 'Error') }
  }

  useEffect(() => { load() }, [])

  const toggleDetail = async (id: number) => {
    if (expandedId === id) { setExpandedId(null); return }
    setExpandedId(id)
    if (!details[id]) {
      setLoadingDetail(id)
      try {
        const d = await Api.getRegistration(id)
        setDetails(prev => ({ ...prev, [id]: d }))
      } catch {
        setError('Could not load registration details.')
      } finally {
        setLoadingDetail(null)
      }
    }
  }

  const deleteRegistration = async (id: number) => {
    if (!confirm(t('admin.confirmDelete'))) return
    try {
      await Api.deleteRegistration(id)
      setRegistrations(prev => prev.filter(r => r.id !== id))
      if (expandedId === id) setExpandedId(null)
    } catch (e: any) { setError(e?.message ?? 'Error') }
  }

  const onSaved = (updated: RegistrationDetail) => {
    setDetails(prev => ({ ...prev, [updated.id]: updated }))
    setRegistrations(prev => prev.map(r => r.id === updated.id
      ? {
          ...r,
          parentFirstName: updated.parentFirstName,
          parentLastName: updated.parentLastName,
          email: updated.email,
          cellPhone: updated.cellPhone,
          language: updated.language,
          hasWhatsApp: updated.hasWhatsApp,
        }
      : r))
  }

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-8">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.registrationsTitle')}</h1>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <div className="flex items-center justify-between">
            <h2 className="font-bold text-emerald-800">{t('admin.registrations')}</h2>
            <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
          </div>
          <div className="mt-4 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">Season</th>
                  <th className="py-2 pr-4">Parent</th>
                  <th className="py-2 pr-4">Email</th>
                  <th className="py-2 pr-4">Phone</th>
                  <th className="py-2 pr-4">Players</th>
                  <th className="py-2 pr-4">Lang</th>
                  <th className="py-2 pr-4">Created</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {registrations.map(r => (
                  <RegistrationRow
                    key={r.id}
                    r={r}
                    expanded={expandedId === r.id}
                    detail={details[r.id]}
                    loading={loadingDetail === r.id}
                    onToggle={() => toggleDetail(r.id)}
                    onDelete={() => deleteRegistration(r.id)}
                    onSaved={onSaved}
                    onError={(e) => setError(e)}
                  />
                ))}
                {registrations.length === 0 && (
                  <tr><td colSpan={8} className="py-4 text-center text-slate-400">—</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </Layout>
  )
}

function RegistrationRow({
  r, expanded, detail, loading, onToggle, onDelete, onSaved, onError,
}: {
  r: RegistrationSummary
  expanded: boolean
  detail: RegistrationDetail | undefined
  loading: boolean
  onToggle: () => void
  onDelete: () => void
  onSaved: (updated: RegistrationDetail) => void
  onError: (msg: string) => void
}) {
  const { t } = useTranslation()
  return (
    <>
      <tr className="border-b last:border-0">
        <td className="py-2 pr-4 font-mono text-xs">{r.season}</td>
        <td className="py-2 pr-4 font-medium">{r.parentFirstName} {r.parentLastName}</td>
        <td className="py-2 pr-4">{r.email}</td>
        <td className="py-2 pr-4">{r.cellPhone}</td>
        <td className="py-2 pr-4">{r.playerCount}</td>
        <td className="py-2 pr-4">{r.language === 1 ? 'ES' : 'EN'}</td>
        <td className="py-2 pr-4 text-slate-500 whitespace-nowrap">{new Date(r.createdAt).toLocaleString()}</td>
        <td className="py-2 pr-4 whitespace-nowrap">
          <button onClick={onToggle} className="text-emerald-700 hover:underline">
            {expanded ? t('admin.hide') : t('admin.details')}
          </button>
          <span className="mx-2 text-slate-300">|</span>
          <button onClick={onDelete} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
        </td>
      </tr>
      {expanded && (
        <tr className="border-b last:border-0 bg-slate-50">
          <td colSpan={8} className="p-4">
            {loading && <span className="text-slate-500">{t('common.loading')}</span>}
            {!loading && detail && <RegistrationDetailPanel detail={detail} onSaved={onSaved} onError={onError} />}
          </td>
        </tr>
      )}
    </>
  )
}

function RegistrationDetailPanel({
  detail, onSaved, onError,
}: {
  detail: RegistrationDetail
  onSaved: (updated: RegistrationDetail) => void
  onError: (msg: string) => void
}) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState(false)
  const r = detail
  const fullAddress = [
    r.addressLine1,
    r.addressLine2,
    `${r.city}, ${r.state} ${r.postalCode}`,
  ].filter(Boolean).join(' • ')

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-md border border-slate-200 p-4">
        <div className="flex items-start justify-between mb-2">
          <h3 className="font-bold text-emerald-800">{t('admin.parentInfo')}</h3>
          {!editing && (
            <button onClick={() => setEditing(true)} className="text-sm text-emerald-700 hover:underline">
              {t('admin.edit')}
            </button>
          )}
        </div>
        {editing ? (
          <EditRegistrationForm
            detail={r}
            onCancel={() => setEditing(false)}
            onSaved={(updated) => { onSaved(updated); setEditing(false) }}
            onError={onError}
          />
        ) : (
          <dl className="grid sm:grid-cols-2 gap-x-6 gap-y-1 text-sm">
            <Definition label={t('admin.parentInfo')} value={`${r.parentFirstName} ${r.parentLastName}`} />
            <Definition label={t('admin.address')} value={fullAddress} />
            <Definition label={t('admin.phone')} value={r.cellPhone} />
            <Definition label={t('admin.emailLbl')} value={r.email} />
            <Definition label={t('admin.language')} value={r.language === 1 ? 'Español' : 'English'} />
            <Definition label={t('admin.hasWhatsApp')} value={r.hasWhatsApp ? t('common.yes') : t('common.no')} />
            <Definition label="Season" value={r.season} />
            <Definition label={t('admin.submitted')} value={new Date(r.createdAt).toLocaleString()} />
            <Definition
              label={t('admin.waiverSignedAt')}
              value={r.waiverSignedAt ? new Date(r.waiverSignedAt).toLocaleString() : '—'}
            />
          </dl>
        )}
        {!editing && r.additionalParents.length > 0 && (
          <div className="mt-3 pt-3 border-t border-slate-100">
            <h4 className="text-sm font-semibold text-slate-700 mb-1">{t('admin.additionalParents')}</h4>
            <ul className="text-sm text-slate-600 space-y-0.5">
              {r.additionalParents.map(c => (
                <li key={c.id}>
                  {c.firstName} {c.lastName}
                  {c.cellPhone ? ` · ${c.cellPhone}` : ''}
                  {c.email ? ` · ${c.email}` : ''}
                  {c.hasWhatsApp ? ` · ${t('admin.hasWhatsApp')}` : ''}
                </li>
              ))}
            </ul>
          </div>
        )}
        <div className="mt-3">
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => Api.viewWaivers(r.id)}
              className="text-sm bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800"
            >
              {t('admin.viewAllWaivers')}
            </button>
            <button
              onClick={() => Api.downloadWaivers(r.id)}
              className="text-sm bg-white border border-emerald-700 text-emerald-700 px-3 py-1.5 rounded-md hover:bg-emerald-50"
            >
              {t('admin.downloadAllWaivers')}
            </button>
          </div>
          <p className="text-xs text-slate-500 mt-1.5">{t('admin.packetHelp')}</p>
        </div>
      </div>

      <div>
        <h3 className="font-bold text-emerald-800 mb-2">{t('admin.playersHeading')}</h3>
        <div className="grid sm:grid-cols-2 gap-3">
          {detail.players.map((p, idx) => (
            <PlayerCard key={p.id} regId={detail.id} idx={idx + 1} p={p} />
          ))}
        </div>
      </div>
    </div>
  )
}

function EditRegistrationForm({
  detail, onCancel, onSaved, onError,
}: {
  detail: RegistrationDetail
  onCancel: () => void
  onSaved: (updated: RegistrationDetail) => void
  onError: (msg: string) => void
}) {
  const { t } = useTranslation()
  const [form, setForm] = useState<UpdateRegistrationRequest>({
    parentFirstName: detail.parentFirstName,
    parentLastName: detail.parentLastName,
    addressLine1: detail.addressLine1 ?? '',
    addressLine2: detail.addressLine2 ?? '',
    city: detail.city ?? '',
    state: detail.state ?? '',
    postalCode: detail.postalCode ?? '',
    cellPhone: detail.cellPhone,
    email: detail.email,
    language: detail.language,
    hasWhatsApp: detail.hasWhatsApp,
  })
  const [contacts, setContacts] = useState<ParentContactInput[]>(
    detail.additionalParents.map(c => ({
      firstName: c.firstName, lastName: c.lastName,
      email: c.email ?? '', cellPhone: c.cellPhone ?? '',
      hasWhatsApp: c.hasWhatsApp, language: c.language,
    })))
  const [saving, setSaving] = useState(false)

  const set = <K extends keyof UpdateRegistrationRequest>(key: K, value: UpdateRegistrationRequest[K]) =>
    setForm(prev => ({ ...prev, [key]: value }))

  const setContact = (idx: number, patch: Partial<ParentContactInput>) =>
    setContacts(prev => prev.map((c, i) => i === idx ? { ...c, ...patch } : c))
  const addContact = () =>
    setContacts(prev => [...prev, { firstName: '', lastName: '', email: '', cellPhone: '', hasWhatsApp: false, language: form.language }])
  const removeContact = (idx: number) =>
    setContacts(prev => prev.filter((_, i) => i !== idx))

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.parentFirstName.trim() || !form.parentLastName.trim()) {
      onError(t('common.required')); return
    }
    if (!form.cellPhone.trim() || !form.email.trim()) {
      onError(t('common.required')); return
    }
    setSaving(true)
    try {
      const updated = await Api.updateRegistration(detail.id, { ...form, additionalParents: contacts })
      onSaved(updated)
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setSaving(false)
    }
  }

  const inputCls = 'border border-slate-300 rounded-md px-2 py-1 text-sm w-full'

  return (
    <form onSubmit={submit} className="grid sm:grid-cols-2 gap-3 text-sm">
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.firstName')}</span>
        <input className={inputCls} value={form.parentFirstName}
          onChange={e => set('parentFirstName', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.lastName')}</span>
        <input className={inputCls} value={form.parentLastName}
          onChange={e => set('parentLastName', e.target.value)} />
      </label>
      <label className="block sm:col-span-2">
        <span className="text-slate-600 text-xs">{t('register.parent.address1')}</span>
        <input className={inputCls} value={form.addressLine1 ?? ''}
          onChange={e => set('addressLine1', e.target.value)} />
      </label>
      <label className="block sm:col-span-2">
        <span className="text-slate-600 text-xs">{t('register.parent.address2')}</span>
        <input className={inputCls} value={form.addressLine2 ?? ''}
          onChange={e => set('addressLine2', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.city')}</span>
        <input className={inputCls} value={form.city ?? ''}
          onChange={e => set('city', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.state')}</span>
        <input className={inputCls} value={form.state ?? ''}
          onChange={e => set('state', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.postal')}</span>
        <input className={inputCls} value={form.postalCode ?? ''}
          onChange={e => set('postalCode', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('register.parent.cell')}</span>
        <input type="tel" className={inputCls} value={form.cellPhone}
          onChange={e => set('cellPhone', e.target.value)} />
      </label>
      <label className="block sm:col-span-2">
        <span className="text-slate-600 text-xs">{t('register.parent.email')}</span>
        <input type="email" className={inputCls} value={form.email}
          onChange={e => set('email', e.target.value)} />
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('admin.language')}</span>
        <select className={inputCls} value={form.language}
          onChange={e => set('language', Number(e.target.value) as Language)}>
          <option value={0}>English</option>
          <option value={1}>Español</option>
        </select>
      </label>
      <label className="block">
        <span className="text-slate-600 text-xs">{t('admin.hasWhatsApp')}</span>
        <select className={inputCls} value={form.hasWhatsApp ? 'yes' : 'no'}
          onChange={e => set('hasWhatsApp', e.target.value === 'yes')}>
          <option value="yes">{t('common.yes')}</option>
          <option value="no">{t('common.no')}</option>
        </select>
      </label>
      <div className="sm:col-span-2 pt-2 border-t border-slate-100">
        <div className="flex items-center justify-between">
          <span className="text-slate-600 text-xs font-semibold">{t('admin.additionalParents')}</span>
          <button type="button" onClick={addContact} className="text-xs text-emerald-700 hover:underline">
            + {t('admin.addParent')}
          </button>
        </div>
        <div className="space-y-2 mt-2">
          {contacts.map((c, idx) => (
            <div key={idx} className="grid sm:grid-cols-2 gap-2 border border-slate-200 rounded-md p-2">
              <input className={inputCls} placeholder={t('register.parent.firstName')}
                value={c.firstName} onChange={e => setContact(idx, { firstName: e.target.value })} />
              <input className={inputCls} placeholder={t('register.parent.lastName')}
                value={c.lastName} onChange={e => setContact(idx, { lastName: e.target.value })} />
              <input type="tel" className={inputCls} placeholder={t('register.parent.cell')}
                value={c.cellPhone ?? ''} onChange={e => setContact(idx, { cellPhone: e.target.value })} />
              <input type="email" className={inputCls} placeholder={t('register.parent.email')}
                value={c.email ?? ''} onChange={e => setContact(idx, { email: e.target.value })} />
              <select className={inputCls} value={c.hasWhatsApp ? 'yes' : 'no'}
                onChange={e => setContact(idx, { hasWhatsApp: e.target.value === 'yes' })}>
                <option value="no">{t('admin.hasWhatsApp')}: {t('common.no')}</option>
                <option value="yes">{t('admin.hasWhatsApp')}: {t('common.yes')}</option>
              </select>
              <div className="flex items-center justify-between gap-2">
                <select className={inputCls} value={c.language ?? 0}
                  onChange={e => setContact(idx, { language: Number(e.target.value) as Language })}>
                  <option value={0}>English</option>
                  <option value={1}>Español</option>
                </select>
                <button type="button" onClick={() => removeContact(idx)} className="text-xs text-rose-600 hover:underline whitespace-nowrap">
                  {t('admin.delete')}
                </button>
              </div>
            </div>
          ))}
          {contacts.length === 0 && <p className="text-xs text-slate-400">—</p>}
        </div>
      </div>

      <div className="sm:col-span-2 flex gap-2 pt-2 border-t border-slate-100">
        <button type="submit" disabled={saving}
          className="bg-emerald-700 text-white text-sm font-semibold px-4 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {saving ? t('register.submitting') : t('admin.save')}
        </button>
        <button type="button" onClick={onCancel}
          className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}

function PlayerCard({ regId, idx, p }: { regId: number; idx: number; p: RegistrationPlayerDetail }) {
  const { t } = useTranslation()
  const stem = `${regId}-${p.lastName}-${p.firstName}`.replace(/[^a-zA-Z0-9-_]/g, '')
  const [trialOver, setTrialOver] = useState(p.freeTrialOver)
  const [saving, setSaving] = useState(false)

  const toggleTrial = async () => {
    const next = !trialOver
    setSaving(true)
    try {
      const updated = await Api.updatePlayerTrial(regId, p.id, next)
      setTrialOver(updated.freeTrialOver)
    } catch {
      // Revert on failure — no toast infra here.
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="bg-white rounded-md border border-slate-200 p-4 text-sm">
      <div className="flex items-center justify-between mb-2">
        <h4 className="font-semibold text-slate-700">
          {t('admin.playerLabel')} {idx}: {p.firstName} {p.lastName}
        </h4>
        {p.hasSignature ? (
          <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5 font-semibold">✓ Signed</span>
        ) : (
          <span className="text-xs bg-amber-100 text-amber-800 rounded px-2 py-0.5">{t('admin.notSigned')}</span>
        )}
      </div>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
        <Definition label={t('admin.dob')} value={p.dateOfBirth} />
        <Definition label={t('admin.grade')} value={p.schoolGrade} />
        <Definition label={t('admin.sizes')} value={`${p.uniformSize} / ${p.shoeSize}`} />
        <Definition label={t('admin.ageClassification')} value={p.ageClassificationName ?? '—'} />
        {p.waiverTeamName && <Definition label={t('admin.teamLbl')} value={p.waiverTeamName} />}
        {p.heardFrom && <Definition label={t('admin.heardFromLbl')} value={p.heardFrom} />}
      </dl>
      <label className="mt-3 flex items-center gap-2 text-xs text-slate-700">
        <input type="checkbox" className="w-4 h-4"
          checked={trialOver}
          disabled={saving}
          onChange={toggleTrial} />
        <span>{t('admin.freeTrialOver')}</span>
        {saving && <span className="text-slate-400">…</span>}
      </label>
      <div className="mt-3 flex flex-wrap gap-2">
        <button
          onClick={() => Api.viewPlayerWaiver(regId, p.id)}
          disabled={!p.hasSignature}
          className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-50"
        >
          {t('admin.view')}
        </button>
        <button
          onClick={() => Api.downloadPlayerWaiver(regId, p.id, stem)}
          disabled={!p.hasSignature}
          className="text-xs bg-white border border-emerald-700 text-emerald-700 px-3 py-1.5 rounded-md hover:bg-emerald-50 disabled:opacity-50"
        >
          {t('admin.download')}
        </button>
      </div>
    </div>
  )
}

function Definition({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-slate-900">{value || '—'}</dd>
    </>
  )
}
