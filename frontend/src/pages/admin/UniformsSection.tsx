import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import type { Uniform, UniformDesignation } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const DESIGNATIONS: UniformDesignation[] = [0, 1, 2, 3]

/** Settings → Uniforms. Club-wide CRUD: name + shirt/shorts/sock colors + a Home/Away/Practice
 *  designation. At most one uniform per non-None designation (the backend reassigns on save).
 *  Games default to the uniform whose designation matches their home/away; admins can override
 *  per game from the games list. */
export function UniformsSection({ onError, onNotice }: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<Uniform[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [shirt, setShirt] = useState('')
  const [shorts, setShorts] = useState('')
  const [sock, setSock] = useState('')
  const [designation, setDesignation] = useState<UniformDesignation>(0)
  const [saving, setSaving] = useState(false)
  const v = useRequiredValidation(['name'])

  const load = async () => {
    onError(null)
    try { setItems(await Api.listUniforms()) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { load() }, [])

  const designationLabel = (d: UniformDesignation) =>
    t(['admin.uniformDesignationNone', 'admin.uniformDesignationHome',
       'admin.uniformDesignationAway', 'admin.uniformDesignationPractice'][d])

  const resetForm = () => {
    setEditingId(null); setName(''); setShirt(''); setShorts(''); setSock(''); setDesignation(0)
    v.reset()
  }

  const startAdd = () => { resetForm(); setEditingId('new') }

  const startEdit = (u: Uniform) => {
    onError(null); onNotice(null)
    setEditingId(u.id)
    setName(u.name)
    setShirt(u.shirtColor ?? '')
    setShorts(u.shortsColor ?? '')
    setSock(u.sockColor ?? '')
    setDesignation(u.designation)
    v.reset()
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(null); onNotice(null)
    if (!v.checkSubmit({ name })) { onError(t('common.required')); return }
    setSaving(true)
    try {
      const payload = {
        name: name.trim(),
        shirtColor: shirt.trim() || null,
        shortsColor: shorts.trim() || null,
        sockColor: sock.trim() || null,
        designation,
      }
      if (editingId === 'new') await Api.createUniform(payload)
      else if (editingId != null) await Api.updateUniform(editingId, payload)
      onNotice(t('admin.uniformSaved'))
      resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSaving(false) }
  }

  const remove = async (u: Uniform) => {
    if (!confirm(t('admin.uniformDeleteConfirm', { name: u.name }))) return
    onError(null); onNotice(null)
    try {
      await Api.deleteUniform(u.id)
      if (editingId === u.id) resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
  }

  const colorText = (u: Uniform) => {
    const parts = [
      u.shirtColor && `${u.shirtColor} ${t('admin.uniformShirt').toLowerCase()}`,
      u.shortsColor && `${u.shortsColor} ${t('admin.uniformShorts').toLowerCase()}`,
      u.sockColor && `${u.sockColor} ${t('admin.uniformSock').toLowerCase()}`,
    ].filter(Boolean)
    return parts.length ? parts.join(', ') : '—'
  }

  return (
    <div>
      <p className="text-xs text-slate-500 mb-3">{t('admin.settingsUniformsBlurb')}</p>

      <div className="flex justify-between items-center mb-2">
        <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.settingsTabUniforms')}</h3>
        {editingId == null && (
          <button onClick={startAdd} className="text-sm text-emerald-700 hover:underline">
            + {t('admin.uniformAdd')}
          </button>
        )}
      </div>

      {editingId != null && (
        <form onSubmit={save} noValidate className="grid sm:grid-cols-2 gap-2 mb-3 border border-slate-200 rounded p-3 bg-slate-50/50">
          <label className="block text-xs">
            <RequiredLabel className="text-slate-600">{t('admin.uniformName')}</RequiredLabel>
            <input ref={v.register('name')} type="text" value={name}
              onChange={e => setName(e.target.value)}
              onBlur={e => v.onFieldBlur('name', e.target.value)}
              className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm ${v.fieldCls('name')}`} />
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.uniformDesignation')}</span>
            <select value={designation} onChange={e => setDesignation(Number(e.target.value) as UniformDesignation)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
              {DESIGNATIONS.map(d => <option key={d} value={d}>{designationLabel(d)}</option>)}
            </select>
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.uniformShirt')}</span>
            <input type="text" value={shirt} onChange={e => setShirt(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.uniformShorts')}</span>
            <input type="text" value={shorts} onChange={e => setShorts(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
          </label>
          <label className="block text-xs">
            <span className="text-slate-600">{t('admin.uniformSock')}</span>
            <input type="text" value={sock} onChange={e => setSock(e.target.value)}
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
              <th className="py-1 pr-2">{t('admin.uniformName')}</th>
              <th className="py-1 pr-2">{t('admin.uniformDesignation')}</th>
              <th className="py-1 pr-2">{t('admin.uniformColors')}</th>
              <th className="py-1 pr-2 text-right"></th>
            </tr>
          </thead>
          <tbody>
            {items.map(u => (
              <tr key={u.id} className="border-b last:border-0">
                <td className="py-1 pr-2 font-medium text-slate-700">{u.name}</td>
                <td className="py-1 pr-2">
                  {u.designation === 0
                    ? <span className="text-slate-400">{designationLabel(0)}</span>
                    : <span className="inline-block px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-800">{designationLabel(u.designation)}</span>}
                </td>
                <td className="py-1 pr-2 text-slate-600">{colorText(u)}</td>
                <td className="py-1 pr-2 text-right whitespace-nowrap">
                  <button type="button" onClick={() => startEdit(u)}
                    className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                  <span className="mx-1.5 text-slate-300">|</span>
                  <button type="button" onClick={() => remove(u)}
                    className="text-red-600 hover:underline">{t('admin.delete')}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-slate-400">{t('admin.uniformNone')}</p>
      )}
    </div>
  )
}
