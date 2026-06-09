import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { surfaceKey } from '../../components/VenuePicker'
import type { Venue, SurfaceType } from '../../api/types'

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
              <tr key={item.id} className="border-b last:border-0">
                <td className="py-1 pr-2 font-medium text-slate-700">{item.name}</td>
                <td className="py-1 pr-2 text-slate-600">{item.address ?? '—'}</td>
                <td className="py-1 pr-2 text-slate-600">{t(surfaceKey(item.surface))}</td>
                <td className="py-1 pr-2 text-right whitespace-nowrap">
                  <button type="button" onClick={() => startEdit(item)}
                    className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                  <span className="mx-1.5 text-slate-300">|</span>
                  <button type="button" onClick={() => remove(item)}
                    className="text-red-600 hover:underline">{t('admin.delete')}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-slate-400">{t('admin.venueNonePlaceholder')}</p>
      )}
    </div>
  )
}
