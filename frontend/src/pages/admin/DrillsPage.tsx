import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { useAuth } from '../../auth/AuthContext'
import { can } from '../../auth/can'
import { Api } from '../../api/client'
import { DRILL_CATEGORIES } from '../../api/types'
import type {
  AdminDrill,
  AdminDrillAssignment,
  AdminPlayerSummary,
  DrillTargetOptions,
  DrillTargetType,
  SaveDrillRequest,
} from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/** Today as yyyy-mm-dd in the browser's local time (the admin is in Las Vegas, like the club). */
function todayIso(): string {
  const d = new Date()
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10)
}

const EMPTY_DRILL: SaveDrillRequest = {
  category: 'ball-mastery',
  titleEn: '', titleEs: '',
  descriptionEn: '', descriptionEs: '',
  stepsEn: '', stepsEs: '',
  durationMinutes: 5,
  reps: null,
  videoUrl: null,
  isActive: true,
}

const inputCls = 'w-full border border-slate-300 rounded-md px-3 py-2 text-sm'
const labelCls = 'text-xs font-medium text-slate-600 block mb-1'

/** Shared drill library for admins and team coaches. Anyone on staff can write a drill; the editor
 *  opens for drills the caller may change (admins: all; coaches: their own — `drill.canEdit`),
 *  everything else is read-only. Coaches assign only to their own teams/players. The server
 *  enforces the same rules. */
export function AdminDrillsPage() {
  const { t } = useTranslation()
  const { me } = useAuth()
  const isAdmin = can(me, 'admin.access')
  const canCreate = can(me, 'drills.create')
  const canEditAny = can(me, 'drills.edit')
  const canAssign = can(me, 'drills.assign')
  const [drills, setDrills] = useState<AdminDrill[]>([])
  const [showArchived, setShowArchived] = useState(false)
  // null = nothing selected; 'new' = creating; number = editing that drill.
  const [selected, setSelected] = useState<number | 'new' | null>(null)
  const [form, setForm] = useState<SaveDrillRequest>(EMPTY_DRILL)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const refresh = async () => {
    try { setDrills(await Api.listDrills(showArchived)) }
    catch (e: any) { setError(errMsg(e)) }
  }

  useEffect(() => { refresh() }, [showArchived])

  const select = (d: AdminDrill | 'new') => {
    setError(null); setNotice(null)
    if (d === 'new') {
      setSelected('new')
      setForm(EMPTY_DRILL)
      return
    }
    setSelected(d.id)
    setForm({
      category: d.category,
      titleEn: d.titleEn, titleEs: d.titleEs,
      descriptionEn: d.descriptionEn, descriptionEs: d.descriptionEs,
      stepsEn: d.stepsEn, stepsEs: d.stepsEs,
      durationMinutes: d.durationMinutes,
      reps: d.reps,
      videoUrl: d.videoUrl,
      isActive: d.isActive,
    })
  }

  const set = <K extends keyof SaveDrillRequest>(key: K, value: SaveDrillRequest[K]) =>
    setForm(f => ({ ...f, [key]: value }))

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null); setNotice(null)
    if (!form.titleEn.trim() || !form.titleEs.trim()) { setError(t('common.required')); return }
    setBusy(true)
    try {
      const payload = { ...form, videoUrl: form.videoUrl?.trim() || null }
      const saved = selected === 'new' || selected === null
        ? await Api.createDrill(payload)
        : await Api.updateDrill(selected, payload)
      await refresh()
      setSelected(saved.id)
      setNotice(t('drills.saved'))
    } catch (e: any) { setError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async () => {
    if (typeof selected !== 'number') return
    if (!confirm(t('drills.deleteConfirm'))) return
    setError(null); setNotice(null)
    try {
      await Api.deleteDrill(selected)
      setSelected(null)
      await refresh()
      setNotice(t('drills.deleted'))
    } catch (e: any) { setError(errMsg(e)) }
  }

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          {isAdmin
            ? <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
            : <Link to="/" className="text-sm text-emerald-700 hover:underline">← {t('drills.backToSite')}</Link>}
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('drills.title')}</h1>
          <p className="text-sm text-slate-600 mt-1">{isAdmin ? t('drills.blurb') : t('drills.coachBlurb')}</p>
        </div>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        <div className="grid lg:grid-cols-3 gap-6">
          {/* Drill list */}
          <section className="lg:col-span-1 bg-white border border-slate-200 rounded-lg p-5 space-y-4 h-fit">
            <div className="flex items-center justify-between gap-2">
              <h2 className="font-bold text-emerald-800">{t('drills.listHeading')}</h2>
              {canCreate && (
                <button onClick={() => select('new')}
                  className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800">
                  {t('drills.newDrill')}
                </button>
              )}
            </div>
            {(canCreate || canEditAny) && (
              <label className="flex items-center gap-2 text-xs text-slate-600">
                <input type="checkbox" checked={showArchived} onChange={e => setShowArchived(e.target.checked)} />
                {canEditAny ? t('drills.showArchived') : t('drills.showMyArchived')}
              </label>
            )}
            <ul className="divide-y divide-slate-100">
              {drills.map(d => (
                <li key={d.id} className="py-2">
                  <button onClick={() => select(d)}
                    className={`text-left w-full ${selected === d.id ? 'font-bold text-emerald-800' : 'text-slate-700 hover:text-emerald-700'}`}>
                    {d.titleEn}
                    {!d.isActive && <span className="ml-2 text-xs font-normal text-amber-700">{t('drills.archived')}</span>}
                    <span className="block text-xs text-slate-400 font-normal">
                      {t(`drills.categories.${d.category}`)} · {d.durationMinutes} min · {t('drills.assignedCount', { count: d.assignmentCount })}
                    </span>
                    {d.createdByName && (
                      <span className="block text-xs text-slate-400 font-normal">
                        {t('drills.byAuthor', { name: d.createdByName })}
                        {!canEditAny && d.canEdit && <span className="ml-1 text-emerald-700">· {t('drills.yours')}</span>}
                      </span>
                    )}
                  </button>
                </li>
              ))}
              {drills.length === 0 && <li className="py-3 text-sm text-slate-400">{t('drills.none')}</li>}
            </ul>
          </section>

          {/* Editor + assignments */}
          <div className="lg:col-span-2 space-y-6">
            {selected === null ? (
              <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-slate-400 text-sm">
                {t('drills.selectPrompt')}
              </div>
            ) : (
              <>
                {selected !== 'new' && !drills.find(d => d.id === selected)?.canEdit ? (
                  <DrillView drill={drills.find(d => d.id === selected)} />
                ) : (
                <form onSubmit={save} noValidate className="bg-white border border-slate-200 rounded-lg p-5 space-y-4">
                  <h2 className="font-bold text-emerald-800">
                    {selected === 'new' ? t('drills.createHeading') : t('drills.editHeading')}
                  </h2>

                  <div className="grid sm:grid-cols-3 gap-4">
                    <div>
                      <label className={labelCls}>{t('drills.category')}</label>
                      <select value={form.category} onChange={e => set('category', e.target.value as SaveDrillRequest['category'])} className={inputCls}>
                        {DRILL_CATEGORIES.map(c => <option key={c} value={c}>{t(`drills.categories.${c}`)}</option>)}
                      </select>
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.duration')}</label>
                      <input type="number" min={1} max={120} value={form.durationMinutes}
                        onChange={e => set('durationMinutes', Number(e.target.value))} className={inputCls} />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.reps')}</label>
                      <input type="number" min={1} max={1000} value={form.reps ?? ''}
                        onChange={e => set('reps', e.target.value === '' ? null : Number(e.target.value))} className={inputCls} />
                    </div>
                  </div>

                  {/* English and Spanish side by side so translators can line them up. */}
                  <div className="grid sm:grid-cols-2 gap-4">
                    <div>
                      <label className={labelCls}>{t('drills.titleEn')} *</label>
                      <input value={form.titleEn} maxLength={120} onChange={e => set('titleEn', e.target.value)} className={inputCls} aria-required="true" />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.titleEs')} *</label>
                      <input value={form.titleEs} maxLength={120} onChange={e => set('titleEs', e.target.value)} className={inputCls} aria-required="true" />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.descriptionEn')}</label>
                      <textarea rows={2} maxLength={1000} value={form.descriptionEn} onChange={e => set('descriptionEn', e.target.value)} className={inputCls} />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.descriptionEs')}</label>
                      <textarea rows={2} maxLength={1000} value={form.descriptionEs} onChange={e => set('descriptionEs', e.target.value)} className={inputCls} />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.stepsEn')}</label>
                      <textarea rows={6} maxLength={4000} value={form.stepsEn} onChange={e => set('stepsEn', e.target.value)} className={inputCls} />
                    </div>
                    <div>
                      <label className={labelCls}>{t('drills.stepsEs')}</label>
                      <textarea rows={6} maxLength={4000} value={form.stepsEs} onChange={e => set('stepsEs', e.target.value)} className={inputCls} />
                    </div>
                  </div>
                  <p className="text-xs text-slate-500 -mt-2">{t('drills.stepsHelp')}</p>

                  <div>
                    <label className={labelCls}>{t('drills.videoUrl')}</label>
                    <input type="url" placeholder="https://" value={form.videoUrl ?? ''} maxLength={512}
                      onChange={e => set('videoUrl', e.target.value)} className={inputCls} />
                  </div>

                  <label className="flex items-center gap-2 text-sm text-slate-700">
                    <input type="checkbox" checked={form.isActive} onChange={e => set('isActive', e.target.checked)} />
                    {t('drills.active')}
                  </label>

                  <div className="flex items-center justify-between gap-2 pt-2">
                    <button type="submit" disabled={busy}
                      className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                      {t('drills.save')}
                    </button>
                    {typeof selected === 'number' && (
                      <button type="button" onClick={remove} className="text-sm text-rose-700 hover:underline">
                        {t('drills.delete')}
                      </button>
                    )}
                  </div>
                </form>
                )}

                {!canAssign ? null : typeof selected === 'number' ? (
                  <AssignmentsPanel drillId={selected} onChanged={refresh} onError={setError} onNotice={setNotice} />
                ) : (
                  <div className="bg-white border border-dashed border-slate-300 rounded-lg p-5 text-sm text-slate-400">
                    {t('drills.saveFirst')}
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </Layout>
  )
}

/** Read-only drill card for coaches (admins see the editor instead). */
function DrillView({ drill }: { drill: AdminDrill | undefined }) {
  const { t, i18n } = useTranslation()
  if (!drill) return null
  const es = i18n.language.startsWith('es')
  const steps = (es && drill.stepsEs ? drill.stepsEs : drill.stepsEn).split('\n').filter(Boolean)
  return (
    <section className="bg-white border border-slate-200 rounded-lg p-5 space-y-3">
      <p className="text-xs font-semibold uppercase text-emerald-700">{t(`drills.categories.${drill.category}`)}</p>
      <h2 className="text-xl font-bold text-emerald-800">{es ? drill.titleEs : drill.titleEn}</h2>
      <p className="text-sm text-slate-600">{es ? drill.descriptionEs : drill.descriptionEn}</p>
      <p className="text-sm text-slate-500">
        {drill.durationMinutes} min{drill.reps ? ` · ${drill.reps} reps` : ''}
      </p>
      {steps.length > 0 && (
        <ol className="list-decimal pl-5 text-sm text-slate-700 space-y-1">
          {steps.map((s, i) => <li key={i}>{s}</li>)}
        </ol>
      )}
      {drill.videoUrl && (
        <a href={drill.videoUrl} target="_blank" rel="noreferrer" className="text-sm text-emerald-700 hover:underline">▶ Video</a>
      )}
      <p className="text-xs text-slate-400">
        {drill.createdByName ? t('drills.byAuthor', { name: drill.createdByName }) : ''}
        {' '}{t('drills.readOnlyNote')}
      </p>
    </section>
  )
}

/** Lists a drill's assignments and adds new ones (player / team / age group + date range). */
function AssignmentsPanel({
  drillId, onChanged, onError, onNotice,
}: {
  drillId: number
  onChanged: () => void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [rows, setRows] = useState<AdminDrillAssignment[]>([])
  const [targets, setTargets] = useState<DrillTargetOptions>({ isAdmin: false, teams: [], ageGroups: [], players: [] })
  const [targetType, setTargetType] = useState<DrillTargetType>('team')
  const [targetId, setTargetId] = useState<number | ''>('')
  const [startDate, setStartDate] = useState(todayIso())
  const [endDate, setEndDate] = useState('')
  const [playerQuery, setPlayerQuery] = useState('')
  const [playerResults, setPlayerResults] = useState<AdminPlayerSummary[]>([])
  const [busy, setBusy] = useState(false)

  const load = async () => {
    try { setRows(await Api.listDrillAssignments(drillId)) }
    catch (e: any) { onError(errMsg(e)) }
  }

  useEffect(() => { load() }, [drillId])
  useEffect(() => { Api.drillTargetOptions().then(setTargets).catch(e => onError(errMsg(e))) }, [])

  // Admins: debounced search over every player (same 200ms cadence as the admin players page).
  // Coaches get their own roster inline from drillTargetOptions instead.
  useEffect(() => {
    if (!targets.isAdmin || targetType !== 'player' || playerQuery.trim().length < 2) { setPlayerResults([]); return }
    const h = setTimeout(() => {
      Api.listAdminPlayers(playerQuery).then(r => setPlayerResults(r.slice(0, 20))).catch(() => setPlayerResults([]))
    }, 200)
    return () => clearTimeout(h)
  }, [playerQuery, targetType, targets.isAdmin])

  const changeType = (type: DrillTargetType) => {
    setTargetType(type)
    setTargetId('')
    setPlayerQuery('')
  }

  const add = async (e: React.FormEvent) => {
    e.preventDefault()
    if (targetId === '') { onError(t('common.required')); return }
    setBusy(true)
    try {
      await Api.createDrillAssignment({ drillId, targetType, targetId, startDate, endDate: endDate || null })
      setTargetId(''); setPlayerQuery(''); setEndDate('')
      await load()
      onChanged()
      onNotice(t('drills.assigned'))
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const remove = async (id: number) => {
    if (!confirm(t('drills.removeConfirm'))) return
    try {
      await Api.deleteDrillAssignment(id)
      await load()
      onChanged()
    } catch (e: any) { onError(errMsg(e)) }
  }

  const options = targetType === 'team' ? targets.teams : targets.ageGroups

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-5 space-y-4">
      <div>
        <h2 className="font-bold text-emerald-800">{t('drills.assignHeading')}</h2>
        <p className="text-xs text-slate-500 mt-1">{t('drills.assignHelp')}</p>
      </div>

      <form onSubmit={add} noValidate className="grid sm:grid-cols-2 lg:grid-cols-4 gap-3 items-end">
        <div>
          <label className={labelCls}>{t('drills.targetType')}</label>
          <select value={targetType} onChange={e => changeType(e.target.value as DrillTargetType)} className={inputCls}>
            <option value="team">{t('drills.targetTeam')}</option>
            {targets.isAdmin && <option value="age-group">{t('drills.targetAgeGroup')}</option>}
            <option value="player">{t('drills.targetPlayer')}</option>
          </select>
        </div>

        <div className="sm:col-span-1 lg:col-span-1">
          {targetType === 'player' && !targets.isAdmin ? (
            <select value={targetId} onChange={e => setTargetId(e.target.value === '' ? '' : Number(e.target.value))} className={inputCls}>
              <option value="">{t('drills.pickPlayer')}</option>
              {targets.players.map(p => (
                <option key={`${p.id}-${p.teamName}`} value={p.id}>{p.name} — {p.teamName}</option>
              ))}
            </select>
          ) : targetType === 'player' ? (
            <>
              <input value={playerQuery} onChange={e => { setPlayerQuery(e.target.value); setTargetId('') }}
                placeholder={t('drills.searchPlayer')} className={`${inputCls} mb-1`} />
              <select value={targetId} onChange={e => setTargetId(e.target.value === '' ? '' : Number(e.target.value))} className={inputCls}>
                <option value="">{t('drills.pickPlayer')}</option>
                {playerResults.map(p => (
                  <option key={p.id} value={p.id}>
                    {p.firstName} {p.lastName}{p.currentTeamName ? ` — ${p.currentTeamName}` : ''}
                  </option>
                ))}
              </select>
            </>
          ) : (
            <select value={targetId} onChange={e => setTargetId(e.target.value === '' ? '' : Number(e.target.value))} className={inputCls}>
              <option value="">{targetType === 'team' ? t('drills.pickTeam') : t('drills.pickAgeGroup')}</option>
              {options.map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          )}
        </div>

        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className={labelCls}>{t('drills.startDate')}</label>
            <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>{t('drills.endDate')}</label>
            <input type="date" value={endDate} min={startDate} onChange={e => setEndDate(e.target.value)} className={inputCls} />
          </div>
        </div>

        <button type="submit" disabled={busy || targetId === ''}
          className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-50">
          {t('drills.assign')}
        </button>
      </form>

      <table className="w-full text-sm">
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className="border-b last:border-0">
              <td className="py-2 pr-4 text-slate-500 w-28">{t(`drills.targetKind.${r.targetType}`)}</td>
              <td className="py-2 pr-4 font-medium">{r.targetName}</td>
              <td className="py-2 pr-4 text-slate-600 whitespace-nowrap">
                {r.startDate} → {r.endDate ?? t('drills.ongoing')}
              </td>
              <td className="py-2 text-right">
                <button onClick={() => remove(r.id)} className="text-rose-700 hover:underline">{t('drills.remove')}</button>
              </td>
            </tr>
          ))}
          {rows.length === 0 && (
            <tr><td colSpan={4} className="py-4 text-center text-slate-400">{t('drills.noAssignments')}</td></tr>
          )}
        </tbody>
      </table>
    </section>
  )
}
