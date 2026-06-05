import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import { Api } from '../../api/client'
import type { AgeClassification, SaveAgeClassificationRequest } from '../../api/types'

/** Body-only Age Classifications editor — extracted so the Settings page can render it as a
 *  tab without duplicating the form. The standalone <see cref="AdminAgeClassificationsPage"/>
 *  below wraps this in Layout + breadcrumbs for direct URL access. */
export function AgeClassificationsSection({
  onError, onNotice,
}: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<AgeClassification[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [dobStart, setDobStart] = useState('')
  const [dobEnd, setDobEnd] = useState('')
  const [saving, setSaving] = useState(false)
  const v = useRequiredValidation(['name', 'dobStart', 'dobEnd'])

  const refresh = async () => {
    onError(null)
    try { setItems(await Api.listAgeClassifications()) }
    catch (e: any) { onError(e?.message ?? 'Error') }
  }

  useEffect(() => { refresh() }, [])

  const startNew = () => {
    setEditingId('new')
    setName(''); setDescription(''); setDobStart(''); setDobEnd('')
  }
  const startEdit = (c: AgeClassification) => {
    setEditingId(c.id)
    setName(c.name)
    setDescription(c.description ?? '')
    setDobStart(c.dobStart)
    setDobEnd(c.dobEnd)
  }
  const cancel = () => setEditingId(null)

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(null); onNotice(null)
    if (!v.checkSubmit({ name, dobStart, dobEnd })) { onError(t('common.required')); return }
    if (dobEnd < dobStart) { onError(t('admin.ageRangeInvalid')); return }
    const payload: SaveAgeClassificationRequest = {
      name: name.trim(),
      description: description.trim() || null,
      dobStart, dobEnd,
    }
    setSaving(true)
    try {
      if (editingId === 'new' || editingId === null) {
        await Api.createAgeClassification(payload)
        onNotice(t('admin.ageSaved'))
      } else {
        await Api.updateAgeClassification(editingId, payload)
        onNotice(t('admin.ageSaved'))
      }
      setEditingId(null)
      await refresh()
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setSaving(false)
    }
  }

  const remove = async (id: number) => {
    if (!confirm(t('admin.confirmDelete'))) return
    try {
      await Api.deleteAgeClassification(id)
      await refresh()
    } catch (e: any) { onError(e?.message ?? 'Error') }
  }

  const sorted = useMemo(
    () => [...items].sort((a, b) => a.dobStart.localeCompare(b.dobStart)),
    [items])

  return (
    <div className="space-y-4">
      <section className="bg-white border border-slate-200 rounded-lg p-6">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-emerald-800">{t('admin.ageList')}</h2>
          <button onClick={startNew} className="text-sm text-emerald-700 hover:underline">+ {t('admin.ageNew')}</button>
        </div>
        <table className="w-full text-sm mt-3">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-2 pr-4">{t('admin.ageName')}</th>
              <th className="py-2 pr-4">{t('admin.ageDobStart')}</th>
              <th className="py-2 pr-4">{t('admin.ageDobEnd')}</th>
              <th className="py-2 pr-4">{t('admin.ageDescription')}</th>
              <th className="py-2 pr-4"></th>
            </tr>
          </thead>
          <tbody>
            {sorted.map(c => (
              <tr key={c.id} className="border-b last:border-0">
                <td className="py-2 pr-4 font-medium">{c.name}</td>
                <td className="py-2 pr-4">{c.dobStart}</td>
                <td className="py-2 pr-4">{c.dobEnd}</td>
                <td className="py-2 pr-4 text-slate-600">{c.description ?? '—'}</td>
                <td className="py-2 pr-4 text-right whitespace-nowrap">
                  <button onClick={() => startEdit(c)} className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                  <span className="mx-2 text-slate-300">|</span>
                  <button onClick={() => remove(c.id)} className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                </td>
              </tr>
            ))}
            {sorted.length === 0 && (
              <tr><td colSpan={5} className="py-4 text-center text-slate-400">—</td></tr>
            )}
          </tbody>
        </table>
      </section>

      {editingId !== null && (
        <form onSubmit={save} noValidate className="bg-white border border-slate-200 rounded-lg p-6 space-y-3">
          <h2 className="font-bold text-emerald-800">
            {editingId === 'new' ? t('admin.ageNew') : t('admin.ageEditingTitle', { name })}
          </h2>
          <label className="block text-sm">
            <RequiredLabel>{t('admin.ageName')}</RequiredLabel>
            <input ref={v.register('name')} type="text" value={name} onChange={e => setName(e.target.value)}
              onBlur={e => v.onFieldBlur('name', e.target.value)}
              placeholder="U10"
              className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${v.fieldCls('name')}`} />
          </label>
          <label className="block text-sm">
            <span className="font-medium text-slate-700">{t('admin.ageDescription')}</span>
            <input type="text" value={description} onChange={e => setDescription(e.target.value)}
              className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </label>
          <div className="grid sm:grid-cols-2 gap-3">
            <label className="block text-sm">
              <RequiredLabel>{t('admin.ageDobStart')}</RequiredLabel>
              <input ref={v.register('dobStart')} type="date" value={dobStart} onChange={e => setDobStart(e.target.value)}
                onBlur={e => v.onFieldBlur('dobStart', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${v.fieldCls('dobStart')}`} />
            </label>
            <label className="block text-sm">
              <RequiredLabel>{t('admin.ageDobEnd')}</RequiredLabel>
              <input ref={v.register('dobEnd')} type="date" value={dobEnd} onChange={e => setDobEnd(e.target.value)}
                onBlur={e => v.onFieldBlur('dobEnd', e.target.value)}
                className={`mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm ${v.fieldCls('dobEnd')}`} />
            </label>
          </div>
          <p className="text-xs text-slate-500">{t('admin.ageDobHint')}</p>
          <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
            <button type="submit" disabled={saving}
              className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {saving ? t('register.submitting') : t('admin.save')}
            </button>
            <button type="button" onClick={cancel} className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
          </div>
        </form>
      )}
    </div>
  )
}

/** Standalone page kept for any direct URL bookmarks — the primary entry point is now the
 *  Age Classifications tab inside the Settings card. */
export function AdminAgeClassificationsPage() {
  const { t } = useTranslation()
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  return (
    <Layout>
      <div className="max-w-4xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.ageClassificationsTitle')}</h1>
          <p className="text-sm text-slate-600 mt-1">{t('admin.ageClassificationsBlurb')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        <AgeClassificationsSection onError={setError} onNotice={setNotice} />
      </div>
    </Layout>
  )
}
