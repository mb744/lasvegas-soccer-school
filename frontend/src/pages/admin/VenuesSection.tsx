import { Fragment, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { surfaceKey } from '../../components/VenuePicker'
import type { Venue, VenueField, SurfaceType } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const SURFACES: SurfaceType[] = [0, 1, 2, 3]

/** Settings → Venues. CRUD for parks/fields: name, address, playing surface. Events reference a
 *  venue from their location picker; deleting a venue leaves those events with their free text. */
export function VenuesSection({ onError, onNotice }: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<Venue[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [surface, setSurface] = useState<SurfaceType>(0)
  const [saving, setSaving] = useState(false)
  const [fieldsForId, setFieldsForId] = useState<number | null>(null)
  const v = useRequiredValidation(['name'])

  const load = async () => {
    onError(null)
    try { setItems(await Api.listVenues()) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { load() }, [])

  const resetForm = () => {
    setEditingId(null); setName(''); setAddress(''); setSurface(0); v.reset()
  }

  const startAdd = () => { resetForm(); setEditingId('new') }

  const startEdit = (item: Venue) => {
    onError(null); onNotice(null)
    setEditingId(item.id)
    setName(item.name)
    setAddress(item.address ?? '')
    setSurface(item.surface)
    v.reset()
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(null); onNotice(null)
    if (!v.checkSubmit({ name })) { onError(t('common.required')); return }
    setSaving(true)
    try {
      const payload = { name: name.trim(), address: address.trim() || null, surface }
      if (editingId === 'new') await Api.createVenue(payload)
      else if (editingId != null) await Api.updateVenue(editingId, payload)
      onNotice(t('admin.venueSaved'))
      resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSaving(false) }
  }

  const remove = async (item: Venue) => {
    if (!confirm(t('admin.venueDeleteConfirm', { name: item.name }))) return
    onError(null); onNotice(null)
    try {
      await Api.deleteVenue(item.id)
      if (editingId === item.id) resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <div>
      <p className="text-xs text-slate-500 mb-3">{t('admin.settingsVenuesBlurb')}</p>

      <div className="flex justify-between items-center mb-2">
        <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.settingsTabVenues')}</h3>
        {editingId == null && (
          <button onClick={startAdd} className="text-sm text-emerald-700 hover:underline">
            + {t('admin.venueAddNew')}
          </button>
        )}
      </div>

      {editingId != null && (
        <form onSubmit={save} noValidate className="grid sm:grid-cols-2 gap-2 mb-3 border border-slate-200 rounded p-3 bg-slate-50/50">
          <label className="block text-xs">
            <RequiredLabel className="text-slate-600">{t('admin.venueName')}</RequiredLabel>
            <input ref={v.register('name')} type="text" value={name}
              onChange={e => setName(e.target.value)}
              onBlur={e => v.onFieldBlur('name', e.target.value)}
              className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm ${v.fieldCls('name')}`} />
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.surface')}</span>
            <select value={surface} onChange={e => setSurface(Number(e.target.value) as SurfaceType)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
              {SURFACES.map(s => <option key={s} value={s}>{t(surfaceKey(s))}</option>)}
            </select>
          </label>
          <label className="block text-xs sm:col-span-2">
            <span className="text-slate-600">{t('admin.venueAddress')}</span>
            <input type="text" value={address} onChange={e => setAddress(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
          </label>
          <div className="sm:col-span-2 flex gap-2">
            <button type="submit" disabled={saving}
              className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {t('admin.save')}
            </button>
            <button type="button" onClick={resetForm} className="text-xs text-slate-600 hover:underline">
              {t('admin.cancel')}
            </button>
          </div>
        </form>
      )}

      {items.length > 0 ? (
        <table className="w-full text-xs">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-1 pr-2">{t('admin.venueName')}</th>
              <th className="py-1 pr-2">{t('admin.venueAddress')}</th>
              <th className="py-1 pr-2">{t('admin.surface')}</th>
              <th className="py-1 pr-2 text-right"></th>
            </tr>
          </thead>
          <tbody>
            {items.map(item => (
              <Fragment key={item.id}>
                <tr className="border-b last:border-0">
                  <td className="py-1 pr-2 font-medium text-slate-700">{item.name}</td>
                  <td className="py-1 pr-2 text-slate-600">{item.address ?? '—'}</td>
                  <td className="py-1 pr-2 text-slate-600">{t(surfaceKey(item.surface))}</td>
                  <td className="py-1 pr-2 text-right whitespace-nowrap">
                    <button type="button" onClick={() => setFieldsForId(fieldsForId === item.id ? null : item.id)}
                      className="text-emerald-700 hover:underline">
                      {fieldsForId === item.id ? t('admin.venueHideFields') : t('admin.venueFields')}
                    </button>
                    <span className="mx-1.5 text-slate-300">|</span>
                    <button type="button" onClick={() => startEdit(item)}
                      className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                    <span className="mx-1.5 text-slate-300">|</span>
                    <button type="button" onClick={() => remove(item)}
                      className="text-red-600 hover:underline">{t('admin.delete')}</button>
                  </td>
                </tr>
                {fieldsForId === item.id && (
                  <tr><td colSpan={4} className="py-2 pr-2 bg-slate-50">
                    <VenueFieldsPanel venueId={item.id} onError={onError} onNotice={onNotice} />
                  </td></tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-slate-400">{t('admin.venueNonePlaceholder')}</p>
      )}
    </div>
  )
}

/** Inline manage-fields drawer under a venue row. Loads its own list so the venues page doesn't
 *  eagerly fetch every venue's fields, and mutations refresh only this drawer. */
function VenueFieldsPanel({ venueId, onError, onNotice }: {
  venueId: number
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [fields, setFields] = useState<VenueField[]>([])
  const [name, setName] = useState('')
  const [notes, setNotes] = useState('')
  const [editingId, setEditingId] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)

  const load = async () => {
    try { setFields(await Api.listVenueFields(venueId)) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { load() }, [venueId])

  const reset = () => { setEditingId(null); setName(''); setNotes('') }
  const startEdit = (f: VenueField) => { setEditingId(f.id); setName(f.name); setNotes(f.notes ?? '') }
  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError(t('admin.venueFieldNameRequired')); return }
    setSaving(true)
    try {
      const payload = { name: name.trim(), notes: notes.trim() || null }
      if (editingId) await Api.updateVenueField(venueId, editingId, payload)
      else await Api.createVenueField(venueId, payload)
      onNotice(t('admin.venueFieldSavedNotice'))
      reset(); await load()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSaving(false) }
  }
  const remove = async (f: VenueField) => {
    if (!confirm(t('admin.venueFieldDeleteConfirm', { name: f.name }))) return
    try { await Api.deleteVenueField(venueId, f.id); if (editingId === f.id) reset(); await load() }
    catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <div className="text-xs font-semibold text-slate-700">{t('admin.venueFieldsHeader')}</div>
      </div>
      <p className="text-[11px] text-slate-500">{t('admin.venueFieldsBlurb')}</p>
      <form onSubmit={save} className="flex items-end gap-2 flex-wrap">
        <label className="text-xs">
          <span className="text-slate-600">{t('admin.venueFieldName')}</span>
          <input type="text" value={name} onChange={e => setName(e.target.value)} maxLength={80}
            placeholder="Field 1, North Field…"
            className="mt-1 border border-slate-300 rounded px-2 py-1 text-xs" />
        </label>
        <label className="text-xs flex-1 min-w-[180px]">
          <span className="text-slate-600">{t('admin.venueFieldNotes')}</span>
          <input type="text" value={notes} onChange={e => setNotes(e.target.value)} maxLength={500}
            className="mt-1 w-full border border-slate-300 rounded px-2 py-1 text-xs" />
        </label>
        <button type="submit" disabled={saving}
          className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {editingId ? t('admin.save') : t('admin.venueFieldAdd')}
        </button>
        {editingId && (
          <button type="button" onClick={reset} className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
        )}
      </form>
      {fields.length === 0 ? (
        <p className="text-xs text-slate-400">{t('admin.venueFieldsEmpty')}</p>
      ) : (
        <ul className="space-y-1">
          {fields.map(f => (
            <li key={f.id} className="flex items-center justify-between text-xs border border-slate-200 rounded px-2 py-1 bg-white">
              <div>
                <span className="font-medium">{f.name}</span>
                {f.notes && <span className="text-slate-500 ml-2">· {f.notes}</span>}
              </div>
              <div className="space-x-2 whitespace-nowrap">
                <button onClick={() => startEdit(f)} className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                <button onClick={() => remove(f)} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
