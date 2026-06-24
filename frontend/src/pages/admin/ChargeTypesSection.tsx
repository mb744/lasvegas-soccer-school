import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import type { ChargeTypeDto, ChargeRecurrence } from '../../api/types'
import { ChargeRecurrenceValue } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const RECURRENCES: ChargeRecurrence[] = [0, 1, 2, 3, 4, 5]
const RECURRENCE_LABEL_EN: Record<ChargeRecurrence, string> = {
  0: 'One-time', 1: 'Hourly', 2: 'Daily', 3: 'Weekly', 4: 'Monthly', 5: 'Yearly',
}

function formatUsd(amount: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount)
}

/** Settings → Charge types. CRUD for the catalog of billable types the Invoices page picks
 *  from when creating a new invoice. Each type has name, optional description, default
 *  amount, and a recurrence cadence (one-time / hourly / daily / weekly / monthly / yearly).
 *  Active flag soft-disables retired types so they drop out of the invoice picker without
 *  losing historical references. */
export function ChargeTypesSection({ onError, onNotice }: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<ChargeTypeDto[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [recurrence, setRecurrence] = useState<ChargeRecurrence>(ChargeRecurrenceValue.OneTime)
  const [active, setActive] = useState(true)
  const [saving, setSaving] = useState(false)

  const load = async () => {
    onError(null)
    try { setItems(await Api.listChargeTypes(false)) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { load() }, [])

  const startNew = () => {
    setEditingId('new')
    setName(''); setDescription(''); setAmount(''); setRecurrence(ChargeRecurrenceValue.OneTime); setActive(true)
  }
  const startEdit = (c: ChargeTypeDto) => {
    setEditingId(c.id)
    setName(c.name); setDescription(c.description ?? ''); setAmount(String(c.amount))
    setRecurrence(c.recurrence); setActive(c.active)
  }
  const cancel = () => setEditingId(null)

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(null); onNotice(null)
    const amt = Number(amount)
    if (!name.trim() || !amount || !isFinite(amt) || amt <= 0) {
      onError(t('admin.chargeTypesAddRequired'))
      return
    }
    setSaving(true)
    try {
      const payload = {
        name: name.trim(),
        description: description.trim() || null,
        amount: amt,
        recurrence,
        active,
      }
      if (editingId === 'new') await Api.createChargeType(payload)
      else if (typeof editingId === 'number') await Api.updateChargeType(editingId, payload)
      await load()
      setEditingId(null)
      onNotice(t('admin.chargeTypesSavedNotice'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSaving(false) }
  }

  const remove = async (c: ChargeTypeDto) => {
    if (!confirm(t('admin.chargeTypesDeleteConfirm', { name: c.name }))) return
    onError(null); onNotice(null)
    try {
      await Api.deleteChargeType(c.id)
      await load()
      onNotice(t('admin.chargeTypesDeletedNotice'))
    } catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold text-emerald-800">{t('admin.chargeTypesHeader')}</h3>
        {editingId === null && (
          <button onClick={startNew}
            className="text-sm text-emerald-700 hover:underline">+ {t('admin.chargeTypesAddNew')}</button>
        )}
      </div>
      <p className="text-xs text-slate-500">{t('admin.chargeTypesBlurb')}</p>

      {editingId !== null && (
        <form onSubmit={save} className="bg-emerald-50 border border-emerald-200 rounded p-3 space-y-2">
          <div className="grid sm:grid-cols-2 gap-2">
            <label className="text-xs">
              <span className="text-slate-700">{t('admin.chargeTypesName')}</span>
              <input type="text" value={name} onChange={e => setName(e.target.value)}
                maxLength={128}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            </label>
            <label className="text-xs">
              <span className="text-slate-700">{t('admin.chargeTypesAmount')}</span>
              <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
                step="0.01" min="0.01"
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
            </label>
            <label className="text-xs">
              <span className="text-slate-700">{t('admin.chargeTypesRecurrence')}</span>
              <select value={recurrence} onChange={e => setRecurrence(Number(e.target.value) as ChargeRecurrence)}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
                {RECURRENCES.map(r => (
                  <option key={r} value={r}>{RECURRENCE_LABEL_EN[r]}</option>
                ))}
              </select>
            </label>
            <label className="text-xs flex items-center gap-2 pt-5">
              <input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} />
              <span className="text-slate-700">{t('admin.chargeTypesActive')}</span>
            </label>
            <label className="text-xs sm:col-span-2">
              <span className="text-slate-700">{t('admin.chargeTypesDescription')}</span>
              <input type="text" value={description} onChange={e => setDescription(e.target.value)}
                maxLength={1000}
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            </label>
          </div>
          <div className="flex gap-2">
            <button type="submit" disabled={saving}
              className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {saving ? t('admin.sending') : t('admin.save')}
            </button>
            <button type="button" onClick={cancel} disabled={saving}
              className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
          </div>
        </form>
      )}

      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-slate-500 border-b">
            <th className="py-1 px-2">{t('admin.chargeTypesName')}</th>
            <th className="py-1 px-2">{t('admin.chargeTypesAmount')}</th>
            <th className="py-1 px-2">{t('admin.chargeTypesRecurrence')}</th>
            <th className="py-1 px-2">{t('admin.chargeTypesActive')}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {items.map(c => (
            <tr key={c.id} className={`border-b last:border-0 ${c.active ? '' : 'text-slate-400'}`}>
              <td className="py-1 px-2">
                <div className="font-medium">{c.name}</div>
                {c.description && <div className="text-[11px] text-slate-500">{c.description}</div>}
              </td>
              <td className="py-1 px-2 font-mono">{formatUsd(c.amount)}</td>
              <td className="py-1 px-2 text-xs">{RECURRENCE_LABEL_EN[c.recurrence]}</td>
              <td className="py-1 px-2 text-xs">
                {c.active
                  ? <span className="text-emerald-700">✓</span>
                  : <span className="text-slate-400">—</span>}
              </td>
              <td className="py-1 px-2 whitespace-nowrap text-right text-xs space-x-2">
                <button onClick={() => startEdit(c)} className="text-emerald-700 hover:underline">
                  {t('admin.edit')}
                </button>
                <button onClick={() => remove(c)} className="text-rose-700 hover:underline">
                  {t('admin.delete')}
                </button>
              </td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={5} className="py-3 text-center text-slate-400 text-sm">{t('admin.chargeTypesEmpty')}</td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
