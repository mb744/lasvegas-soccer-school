import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../api/client'
import type { Venue, SurfaceType } from '../api/types'

const SURFACES: SurfaceType[] = [0, 1, 2, 3]

/** i18n key for a surface type. */
export function surfaceKey(s: SurfaceType): string {
  return ['admin.surfaceUnspecified', 'admin.surfaceGrass', 'admin.surfaceTurf', 'admin.surfaceHard'][s]
}

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/**
 * Venue selector for event forms: a dropdown of existing venues plus an inline "+ Add new" form
 * that creates a venue (name, address, surface) on the fly and selects it. The inline form uses
 * type="button" controls so it never submits the parent event form.
 */
export function VenuePicker({ venues, value, onChange, onVenuesChanged, onError, selectClassName }: {
  venues: Venue[]
  value: number | ''
  onChange: (v: number | '') => void
  /** Called after a new venue is created so the parent can reload its venue list. */
  onVenuesChanged: () => void | Promise<void>
  onError?: (e: string) => void
  selectClassName?: string
}) {
  const { t } = useTranslation()
  const [adding, setAdding] = useState(false)
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [surface, setSurface] = useState<SurfaceType>(0)
  const [saving, setSaving] = useState(false)

  const cls = selectClassName ?? 'mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm'

  const create = async () => {
    if (!name.trim() || saving) return
    setSaving(true)
    try {
      const v = await Api.createVenue({ name: name.trim(), address: address.trim() || null, surface })
      await onVenuesChanged()
      onChange(v.id)
      setAdding(false); setName(''); setAddress(''); setSurface(0)
    } catch (e: any) { onError?.(errMsg(e)) }
    finally { setSaving(false) }
  }

  if (adding) {
    return (
      <div className="mt-1 border border-slate-200 rounded-md p-2 bg-white space-y-1.5">
        <input type="text" value={name} onChange={e => setName(e.target.value)}
          placeholder={t('admin.venueName')} autoFocus
          className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        <input type="text" value={address} onChange={e => setAddress(e.target.value)}
          placeholder={t('admin.venueAddress')}
          className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        <select value={surface} onChange={e => setSurface(Number(e.target.value) as SurfaceType)}
          className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
          {SURFACES.map(s => <option key={s} value={s}>{t(surfaceKey(s))}</option>)}
        </select>
        <div className="flex gap-2">
          <button type="button" onClick={create} disabled={saving || !name.trim()}
            className="text-xs bg-emerald-700 text-white px-3 py-1 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {t('admin.save')}
          </button>
          <button type="button" onClick={() => { setAdding(false); setName(''); setAddress(''); setSurface(0) }}
            className="text-xs text-slate-600 hover:underline">
            {t('admin.cancel')}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="flex items-center gap-2">
      <select value={value} onChange={e => onChange(e.target.value ? Number(e.target.value) : '')} className={cls}>
        <option value="">{t('admin.venueNone')}</option>
        {venues.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
      </select>
      <button type="button" onClick={() => setAdding(true)}
        className="text-xs text-emerald-700 hover:underline whitespace-nowrap">
        + {t('admin.venueAddNew')}
      </button>
    </div>
  )
}
