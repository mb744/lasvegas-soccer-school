import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import { RequiredLabel, useRequiredValidation } from '../../components/RequiredField'
import type { MappedField, TemplateProperty, TemplateContext } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

// Event details context exposes the full base catalog (event/tournament/team/player/parent/app).
const EVENT_DETAILS_CONTEXT = 5 as unknown as TemplateContext

/** Settings → Mapped fields. Admin-defined composite properties built by concatenating base
 *  properties + literal text via {base.key} placeholders. They appear in the template "Map to"
 *  dropdown (key starts with "custom.") and resolve at send time. */
export function MappedFieldsSection({ onError, onNotice }: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [items, setItems] = useState<MappedField[]>([])
  const [baseProps, setBaseProps] = useState<TemplateProperty[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [template, setTemplate] = useState('')
  const [saving, setSaving] = useState(false)
  const v = useRequiredValidation(['name', 'template'])

  const load = async () => {
    onError(null)
    try { setItems(await Api.listMappedFields()) }
    catch (e: any) { onError(errMsg(e)) }
  }
  useEffect(() => { load() }, [])
  useEffect(() => {
    // Base catalog (exclude existing custom fields — a mapped field references base props only).
    Api.listTemplateProperties(EVENT_DETAILS_CONTEXT)
      .then(p => setBaseProps(p.filter(x => !x.key.startsWith('custom.'))))
      .catch(() => setBaseProps([]))
  }, [])

  const resetForm = () => { setEditingId(null); setName(''); setTemplate(''); v.reset() }
  const startAdd = () => { resetForm(); setEditingId('new') }
  const startEdit = (m: MappedField) => {
    onError(null); onNotice(null)
    setEditingId(m.id); setName(m.name); setTemplate(m.template); v.reset()
  }

  const insertProperty = (key: string) => {
    if (!key) return
    // Append the placeholder; a leading space if the template doesn't already end with one.
    setTemplate(prev => prev + (prev && !prev.endsWith(' ') ? ' ' : '') + `{${key}}`)
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    onError(null); onNotice(null)
    if (!v.checkSubmit({ name, template })) { onError(t('common.required')); return }
    setSaving(true)
    try {
      const payload = { name: name.trim(), template: template.trim() }
      if (editingId === 'new') await Api.createMappedField(payload)
      else if (editingId != null) await Api.updateMappedField(editingId, payload)
      onNotice(t('admin.mappedFieldSaved'))
      resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setSaving(false) }
  }

  const remove = async (m: MappedField) => {
    if (!confirm(t('admin.mappedFieldDeleteConfirm', { name: m.name }))) return
    onError(null); onNotice(null)
    try {
      await Api.deleteMappedField(m.id)
      if (editingId === m.id) resetForm()
      await load()
    } catch (e: any) { onError(errMsg(e)) }
  }

  return (
    <div>
      <p className="text-xs text-slate-500 mb-3">{t('admin.settingsMappedFieldsBlurb')}</p>

      <div className="flex justify-between items-center mb-2">
        <h3 className="font-semibold text-emerald-800 text-sm">{t('admin.settingsTabMappedFields')}</h3>
        {editingId == null && (
          <button onClick={startAdd} className="text-sm text-emerald-700 hover:underline">
            + {t('admin.mappedFieldAdd')}
          </button>
        )}
      </div>

      {editingId != null && (
        <form onSubmit={save} noValidate className="mb-3 border border-slate-200 rounded p-3 bg-slate-50/50 space-y-2">
          <label className="block text-xs">
            <RequiredLabel className="text-slate-600">{t('admin.mappedFieldName')}</RequiredLabel>
            <input ref={v.register('name')} type="text" value={name}
              onChange={e => setName(e.target.value)}
              onBlur={e => v.onFieldBlur('name', e.target.value)}
              placeholder={t('admin.mappedFieldNamePlaceholder')}
              className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm ${v.fieldCls('name')}`} />
          </label>
          <label className="block text-xs">
            <RequiredLabel className="text-slate-600">{t('admin.mappedFieldDefinition')}</RequiredLabel>
            <textarea ref={v.register('template')} rows={2} value={template}
              onChange={e => setTemplate(e.target.value)}
              onBlur={e => v.onFieldBlur('template', e.target.value)}
              placeholder="{event.venue}, {event.address} — {event.field}"
              className={`mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono ${v.fieldCls('template')}`} />
          </label>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs text-slate-500">{t('admin.mappedFieldInsert')}:</span>
            <select value="" onChange={e => { insertProperty(e.target.value); e.target.value = '' }}
              className="border border-slate-300 rounded-md px-2 py-1 text-xs max-w-xs">
              <option value="">— {t('admin.mappedFieldPickProperty')} —</option>
              {baseProps.map(p => <option key={p.key} value={p.key}>{p.label} ({`{${p.key}}`})</option>)}
            </select>
          </div>
          <div className="flex gap-2 pt-1">
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
              <th className="py-1 pr-2">{t('admin.mappedFieldName')}</th>
              <th className="py-1 pr-2">{t('admin.mappedFieldDefinition')}</th>
              <th className="py-1 pr-2 text-right"></th>
            </tr>
          </thead>
          <tbody>
            {items.map(m => (
              <tr key={m.id} className="border-b last:border-0">
                <td className="py-1 pr-2 font-medium text-slate-700 align-top">
                  {m.name}
                  <div className="text-[10px] text-slate-400 font-mono">{m.key}</div>
                </td>
                <td className="py-1 pr-2 font-mono text-slate-600 align-top">{m.template}</td>
                <td className="py-1 pr-2 text-right whitespace-nowrap align-top">
                  <button type="button" onClick={() => startEdit(m)}
                    className="text-emerald-700 hover:underline">{t('admin.edit')}</button>
                  <span className="mx-1.5 text-slate-300">|</span>
                  <button type="button" onClick={() => remove(m)}
                    className="text-red-600 hover:underline">{t('admin.delete')}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-slate-400">{t('admin.mappedFieldNone')}</p>
      )}
    </div>
  )
}
