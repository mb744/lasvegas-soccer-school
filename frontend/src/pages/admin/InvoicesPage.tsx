import { Fragment, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  InvoiceDto,
  InvoiceStatus,
  InvoiceSummaryDto,
  InvoiceType,
  InboxParent,
  ChargeTypeDto,
  AdminPlayerSummary,
  EmailTemplate,
} from '../../api/types'
import { InvoiceStatusValue, InvoiceTypeValue, TemplateContextValue } from '../../api/types'
import { EmailTemplatePreviewModal } from './MessagingPage'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

const STATUS_LABEL: Record<InvoiceStatus, string> = {
  0: 'New',
  1: 'Sent',
  2: 'Paid',
  3: 'Closed',
}
const STATUS_BADGE_CLS: Record<InvoiceStatus, string> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-amber-200 text-amber-900',
  2: 'bg-emerald-200 text-emerald-900',
  3: 'bg-slate-300 text-slate-600',
}
const RECURRENCE_LABEL: Record<number, string> = {
  0: 'One-time', 1: 'Hourly', 2: 'Daily', 3: 'Weekly', 4: 'Monthly', 5: 'Yearly',
}

function formatUsd(amount: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount)
}

/** Admin Invoices hub. Lists every invoice in the system with parent contact, amount, due
 *  date, lifecycle status, and per-row state-machine transition + edit / delete actions.
 *  Search bar filters by description or parent name; status pills filter by lifecycle state.
 *  Add form binds a parent via the existing parents-search picker. */
export function AdminInvoicesPage() {
  const { t } = useTranslation()
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [summary, setSummary] = useState<InvoiceSummaryDto | null>(null)
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | null>(null)
  const [query, setQuery] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [showAdd, setShowAdd] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [statusEditingId, setStatusEditingId] = useState<number | null>(null)
  const [emailingId, setEmailingId] = useState<number | null>(null)

  const refresh = async () => {
    try {
      const [rows, sum] = await Promise.all([
        Api.listInvoices({ status: statusFilter ?? undefined, q: query.trim() || undefined }),
        Api.getInvoiceSummary(),
      ])
      setInvoices(rows); setSummary(sum)
    } catch (e: any) { setError(errMsg(e)) }
  }

  useEffect(() => {
    const id = setTimeout(refresh, 200)
    return () => clearTimeout(id)
  }, [statusFilter, query])

  const removeRow = async (inv: InvoiceDto) => {
    if (!confirm(t('admin.invoicesDeleteConfirm', { desc: inv.description }))) return
    try {
      await Api.deleteInvoice(inv.id)
      await refresh()
      setNotice(t('admin.invoicesDeletedNotice'))
    } catch (e: any) { setError(errMsg(e)) }
  }

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-4">
        <div className="flex items-start justify-between flex-wrap gap-2">
          <div>
            <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
            <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.invoicesTitle')}</h1>
            <p className="text-sm text-slate-600 mt-1">{t('admin.invoicesSubtitle')}</p>
          </div>
          <button onClick={() => setShowAdd(s => !s)}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
            {showAdd ? t('admin.cancel') : '+ ' + t('admin.invoicesAddNew')}
          </button>
        </div>

        {error && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>
        )}
        {notice && (
          <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>
        )}

        {summary && (
          <div className="grid sm:grid-cols-3 lg:grid-cols-5 gap-2">
            <SummaryCard label={t('admin.invoicesSumNew')} value={summary.newCount} accent="slate" />
            <SummaryCard label={t('admin.invoicesSumSent')} value={summary.sentCount} accent="amber" />
            <SummaryCard label={t('admin.invoicesSumPaid')} value={summary.paidCount} accent="emerald" />
            <SummaryCard label={t('admin.invoicesSumOutstanding')} value={formatUsd(summary.outstandingAmount)} accent="amber" />
            <SummaryCard label={t('admin.invoicesSumPaidTotal')} value={formatUsd(summary.paidAmount)} accent="emerald" />
          </div>
        )}

        {showAdd && (
          <AddInvoiceForm
            onCreated={async () => { await refresh(); setShowAdd(false); setNotice(t('admin.invoicesAddedNotice')) }}
            onError={(e) => { setError(e); setNotice(null) }}
            onCancel={() => setShowAdd(false)} />
        )}

        <div className="flex flex-wrap items-center gap-2">
          <div className="flex gap-1">
            <button onClick={() => setStatusFilter(null)}
              className={`text-xs px-2 py-1 rounded border ${statusFilter === null ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
              {t('admin.invoicesFilterAll')}
            </button>
            {([0, 1, 2, 3] as InvoiceStatus[]).map(s => (
              <button key={s} onClick={() => setStatusFilter(s)}
                className={`text-xs px-2 py-1 rounded border ${statusFilter === s ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
                {STATUS_LABEL[s]}
              </button>
            ))}
          </div>
          <input type="text" value={query} onChange={e => setQuery(e.target.value)}
            placeholder={t('admin.invoicesSearchPlaceholder')}
            className="flex-1 min-w-[200px] border border-slate-300 rounded-md px-3 py-1.5 text-sm" />
          <span className="text-xs text-slate-400 ml-auto">
            {t('admin.invoicesCount', { count: invoices.length })}
          </span>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-500 border-b">
                <th className="py-2 px-3">{t('admin.invoicesColParent')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColDescription')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColAmount')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColType')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColIssued')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColDue')}</th>
                <th className="py-2 px-3">{t('admin.invoicesColStatus')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {invoices.map(inv => (
                <Fragment key={inv.id}>
                  <tr className="border-b last:border-0 align-top">
                    <td className="py-2 px-3">
                      <div className="font-medium text-slate-800">{inv.parentName ?? <span className="text-slate-400">—</span>}</div>
                      <div className="text-[11px] text-slate-500">{inv.parentCellPhone ?? ''}</div>
                      <div className="text-[11px] text-slate-500">{inv.parentEmail ?? ''}</div>
                    </td>
                    <td className="py-2 px-3">
                      <div>{inv.description}</div>
                      <div className="flex flex-wrap gap-1 mt-0.5">
                        {inv.playerName && (
                          <span className="inline-block text-[10px] uppercase tracking-wide bg-sky-100 text-sky-800 px-1.5 py-0.5 rounded">
                            {inv.playerName}
                          </span>
                        )}
                        {inv.chargeTypeName && (
                          <span className="inline-block text-[10px] uppercase tracking-wide bg-indigo-100 text-indigo-800 px-1.5 py-0.5 rounded">
                            {inv.chargeTypeName}
                          </span>
                        )}
                      </div>
                      {inv.notes && <div className="text-[11px] text-slate-500 mt-0.5">{inv.notes}</div>}
                    </td>
                    <td className="py-2 px-3 font-mono whitespace-nowrap">{formatUsd(inv.amount)}</td>
                    <td className="py-2 px-3 text-xs">
                      {inv.type === InvoiceTypeValue.Subscription
                        ? <span className="text-indigo-700">{t('admin.invoicesTypeSubscription')}</span>
                        : <span className="text-slate-600">{t('admin.invoicesTypeOneTime')}</span>}
                    </td>
                    <td className="py-2 px-3 whitespace-nowrap text-xs">{inv.issuedAt.slice(0, 10)}</td>
                    <td className="py-2 px-3 whitespace-nowrap text-xs">{inv.dueDate ?? <span className="text-slate-400">—</span>}</td>
                    <td className="py-2 px-3 whitespace-nowrap text-xs">
                      <span className={`text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded ${STATUS_BADGE_CLS[inv.status]}`}>
                        {STATUS_LABEL[inv.status]}
                      </span>
                      {inv.paidAt && (
                        <div className="text-[10px] text-emerald-700 mt-0.5">
                          {inv.paymentMethod ? `${inv.paymentMethod} · ` : ''}{inv.paidAt.slice(0, 10)}
                        </div>
                      )}
                    </td>
                    <td className="py-2 px-3 whitespace-nowrap text-right text-xs space-x-2">
                      <button onClick={() => { setEmailingId(emailingId === inv.id ? null : inv.id); setStatusEditingId(null); setEditingId(null) }}
                        disabled={!inv.parentEmail}
                        title={inv.parentEmail ? '' : t('admin.invoicesEmailNoAddress')}
                        className="text-emerald-700 hover:underline disabled:text-slate-400 disabled:no-underline disabled:cursor-not-allowed">
                        {emailingId === inv.id ? t('admin.cancel') : t('admin.invoicesEmailSend')}
                      </button>
                      <button onClick={() => { setStatusEditingId(statusEditingId === inv.id ? null : inv.id); setEditingId(null); setEmailingId(null) }}
                        className="text-emerald-700 hover:underline">
                        {statusEditingId === inv.id ? t('admin.cancel') : t('admin.invoicesStatusChange')}
                      </button>
                      <button onClick={() => { setEditingId(editingId === inv.id ? null : inv.id); setStatusEditingId(null); setEmailingId(null) }}
                        className="text-emerald-700 hover:underline">
                        {editingId === inv.id ? t('admin.cancel') : t('admin.edit')}
                      </button>
                      <button onClick={() => removeRow(inv)} className="text-rose-700 hover:underline">
                        {t('admin.delete')}
                      </button>
                    </td>
                  </tr>
                  {emailingId === inv.id && (
                    <tr><td colSpan={8} className="py-2 px-3 bg-sky-50/50">
                      <SendInvoiceEmailForm invoice={inv}
                        onSent={async (templateName) => {
                          await refresh()
                          setEmailingId(null)
                          setNotice(t('admin.invoicesEmailSentNotice', { template: templateName }))
                        }}
                        onError={(e) => { setError(e); setNotice(null) }}
                        onCancel={() => setEmailingId(null)} />
                    </td></tr>
                  )}
                  {statusEditingId === inv.id && (
                    <tr><td colSpan={8} className="py-2 px-3 bg-emerald-50/50">
                      <StatusForm invoice={inv}
                        onSaved={async () => { await refresh(); setStatusEditingId(null); setNotice(t('admin.invoicesStatusChangedNotice')) }}
                        onError={(e) => { setError(e); setNotice(null) }}
                        onCancel={() => setStatusEditingId(null)} />
                    </td></tr>
                  )}
                  {editingId === inv.id && (
                    <tr><td colSpan={8} className="py-2 px-3 bg-emerald-50">
                      <EditInvoiceForm invoice={inv}
                        onSaved={async () => { await refresh(); setEditingId(null); setNotice(t('admin.invoicesEditedNotice')) }}
                        onError={(e) => { setError(e); setNotice(null) }}
                        onCancel={() => setEditingId(null)} />
                    </td></tr>
                  )}
                </Fragment>
              ))}
              {invoices.length === 0 && (
                <tr><td colSpan={8} className="py-6 text-center text-sm text-slate-400">{t('admin.invoicesEmpty')}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </Layout>
  )
}

function SummaryCard({ label, value, accent }: { label: string; value: string | number; accent: 'slate' | 'amber' | 'emerald' }) {
  const cls = accent === 'amber'
    ? 'border-amber-200 bg-amber-50 text-amber-900'
    : accent === 'emerald'
      ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
      : 'border-slate-200 bg-slate-50 text-slate-800'
  return (
    <div className={`border rounded-md px-3 py-2 ${cls}`}>
      <div className="text-[10px] uppercase tracking-wide opacity-70">{label}</div>
      <div className="text-base font-semibold">{value}</div>
    </div>
  )
}

/** Parent-picker + invoice details form. Searches existing ParentAccounts via the inbox
 *  parents-search endpoint so admins don't have to look up ids. */
function AddInvoiceForm({ onCreated, onError, onCancel }: {
  onCreated: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [parentQuery, setParentQuery] = useState('')
  const [parentResults, setParentResults] = useState<InboxParent[]>([])
  const [parentLoading, setParentLoading] = useState(false)
  const [picked, setPicked] = useState<InboxParent | null>(null)
  // Optional player picker. When the admin picks a player, we infer + lock the parent
  // from the player's account so the invoice ties to the right family automatically.
  const [playerQuery, setPlayerQuery] = useState('')
  const [playerResults, setPlayerResults] = useState<AdminPlayerSummary[]>([])
  const [playerLoading, setPlayerLoading] = useState(false)
  const [pickedPlayer, setPickedPlayer] = useState<AdminPlayerSummary | null>(null)
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [type, setType] = useState<InvoiceType>(InvoiceTypeValue.OneTime)
  const [dueDate, setDueDate] = useState('')
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  // Optional ChargeType pre-fill. Loads active-only since admin shouldn't bill from a
  // retired type. Picking one writes description / amount / type and remembers the id so
  // the saved invoice carries the link for reporting.
  const [chargeTypes, setChargeTypes] = useState<ChargeTypeDto[]>([])
  const [chargeTypeId, setChargeTypeId] = useState<number | ''>('')
  useEffect(() => {
    Api.listChargeTypes(true).then(setChargeTypes).catch((e: any) => onError(errMsg(e)))
  }, [])
  const onPickChargeType = (id: number | '') => {
    setChargeTypeId(id)
    if (id === '') return
    const ct = chargeTypes.find(c => c.id === id)
    if (!ct) return
    // Pre-fill from the type — admin can still edit the fields before submit.
    setDescription(ct.name)
    setAmount(String(ct.amount))
    // Recurrence drives whether this looks like a subscription or one-off line.
    setType(ct.recurrence === 0 ? InvoiceTypeValue.OneTime : InvoiceTypeValue.Subscription)
  }

  useEffect(() => {
    if (picked) return // don't keep searching once a parent is chosen
    let stale = false
    setParentLoading(true)
    const t = setTimeout(async () => {
      try {
        const rows = await Api.searchInboxParents(parentQuery.trim(), { unrepliedOnly: false, limit: 20 })
        if (!stale) setParentResults(rows)
      } catch (e: any) {
        if (!stale) onError(errMsg(e))
      } finally {
        if (!stale) setParentLoading(false)
      }
    }, 200)
    return () => { stale = true; clearTimeout(t) }
  }, [parentQuery, picked])

  // Debounced player search. Skipped once a player is locked in to avoid extra queries
  // while the admin types into description / amount fields.
  useEffect(() => {
    if (pickedPlayer) return
    let stale = false
    setPlayerLoading(true)
    const t = setTimeout(async () => {
      try {
        const rows = await Api.listAdminPlayers(playerQuery.trim() || undefined)
        if (!stale) setPlayerResults(rows.slice(0, 20))
      } catch (e: any) {
        if (!stale) onError(errMsg(e))
      } finally {
        if (!stale) setPlayerLoading(false)
      }
    }, 200)
    return () => { stale = true; clearTimeout(t) }
  }, [playerQuery, pickedPlayer])

  // When the admin picks a player, fill in the parent from the player's account so they
  // don't have to search twice. If a different parent was picked first, the player's
  // parent wins — keeps the invoice consistent with the player it's tied to.
  const onPickPlayer = (p: AdminPlayerSummary) => {
    setPickedPlayer(p)
    if (p.parentAccountId && p.parentName) {
      setPicked({
        parentAccountId: p.parentAccountId,
        name: p.parentName,
        phone: p.parentCellPhone ?? '',
        language: 0,
        hasReplied: false,
      })
    }
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!picked) { onError(t('admin.invoicesAddPickParent')); return }
    const amt = Number(amount)
    if (!description.trim() || !amount || !isFinite(amt) || amt <= 0) {
      onError(t('admin.invoicesAddRequired'))
      return
    }
    setBusy(true)
    try {
      await Api.createInvoice({
        parentAccountId: picked.parentAccountId,
        description: description.trim(),
        amount: amt,
        type,
        dueDate: dueDate || null,
        notes: notes.trim() || null,
        chargeTypeId: chargeTypeId === '' ? null : chargeTypeId,
        playerId: pickedPlayer?.id ?? null,
      })
      await onCreated()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="bg-white border border-emerald-200 rounded-lg p-4 space-y-3">
      <h3 className="font-semibold text-emerald-800">{t('admin.invoicesAddTitle')}</h3>
      <div>
        <div className="text-xs text-slate-700 mb-1 font-medium">{t('admin.invoicesAddPlayer')}</div>
        {pickedPlayer ? (
          <div className="flex items-center gap-2 text-xs bg-emerald-50 border border-emerald-200 rounded p-2">
            <span className="font-medium">{pickedPlayer.firstName} {pickedPlayer.lastName}</span>
            {pickedPlayer.ageBracket && <span className="text-slate-500">{pickedPlayer.ageBracket}</span>}
            {pickedPlayer.parentName && <span className="text-slate-500">· {pickedPlayer.parentName}</span>}
            <button type="button" onClick={() => { setPickedPlayer(null); setPlayerQuery('') }}
              className="ml-auto text-emerald-700 hover:underline">{t('admin.invoicesAddPlayerChange')}</button>
          </div>
        ) : (
          <>
            <input type="text" value={playerQuery} onChange={e => setPlayerQuery(e.target.value)}
              placeholder={t('admin.invoicesAddPlayerSearch')}
              className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            <ul className="max-h-40 overflow-y-auto bg-white border border-slate-200 rounded divide-y divide-slate-100 mt-1">
              {playerLoading && <li className="text-xs text-slate-400 p-2 text-center">{t('common.loading')}</li>}
              {!playerLoading && playerResults.length === 0 && (
                <li className="text-xs text-slate-400 p-2 text-center">{t('admin.invoicesAddPlayerNone')}</li>
              )}
              {!playerLoading && playerResults.map(p => (
                <li key={p.id}>
                  <button type="button" onClick={() => onPickPlayer(p)}
                    className="w-full text-left px-2 py-1.5 text-xs hover:bg-emerald-50">
                    <span className="font-medium">{p.firstName} {p.lastName}</span>
                    {p.ageBracket && <span className="text-[10px] text-slate-500 ml-2">{p.ageBracket}</span>}
                    {p.parentName && <span className="text-[10px] text-slate-500 ml-2">· {p.parentName}</span>}
                  </button>
                </li>
              ))}
            </ul>
            <span className="text-[10px] text-slate-500 mt-0.5 block">{t('admin.invoicesAddPlayerHelp')}</span>
          </>
        )}
      </div>
      <div>
        <div className="text-xs text-slate-700 mb-1">{t('admin.invoicesAddParent')}{pickedPlayer && <span className="text-emerald-700 ml-1">— {t('admin.invoicesAddParentAuto')}</span>}</div>
        {picked ? (
          <div className="flex items-center gap-2 text-xs bg-emerald-50 border border-emerald-200 rounded p-2">
            <span className="font-medium">{picked.name}</span>
            <span className="text-slate-500 font-mono">{picked.phone}</span>
            <button type="button" onClick={() => { setPicked(null); setParentQuery('') }}
              className="ml-auto text-emerald-700 hover:underline">{t('admin.invoicesAddParentChange')}</button>
          </div>
        ) : (
          <>
            <input type="text" value={parentQuery} onChange={e => setParentQuery(e.target.value)}
              placeholder={t('admin.invoicesAddParentSearch')}
              className="w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            <ul className="max-h-40 overflow-y-auto bg-white border border-slate-200 rounded divide-y divide-slate-100 mt-1">
              {parentLoading && <li className="text-xs text-slate-400 p-2 text-center">{t('common.loading')}</li>}
              {!parentLoading && parentResults.length === 0 && (
                <li className="text-xs text-slate-400 p-2 text-center">{t('admin.msgInboxParentNone')}</li>
              )}
              {!parentLoading && parentResults.map(p => (
                <li key={p.parentAccountId}>
                  <button type="button" onClick={() => setPicked(p)}
                    className="w-full text-left px-2 py-1.5 text-xs hover:bg-emerald-50">
                    <span className="font-medium">{p.name}</span>
                    <span className="text-[10px] text-slate-500 font-mono ml-2">{p.phone}</span>
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
      {chargeTypes.length > 0 && (
        <label className="text-xs block">
          <span className="text-slate-700">{t('admin.invoicesAddChargeType')}</span>
          <select value={chargeTypeId} onChange={e => onPickChargeType(e.target.value === '' ? '' : Number(e.target.value))}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
            <option value="">{t('admin.invoicesAddChargeTypeNone')}</option>
            {chargeTypes.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} — {formatUsd(c.amount)}{c.recurrence !== 0 ? ' · ' + RECURRENCE_LABEL[c.recurrence] : ''}
              </option>
            ))}
          </select>
          <span className="text-[10px] text-slate-500 mt-0.5 block">{t('admin.invoicesAddChargeTypeHelp')}</span>
        </label>
      )}
      <div className="grid sm:grid-cols-2 gap-2">
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.invoicesAddDescription')}</span>
          <input type="text" value={description} onChange={e => setDescription(e.target.value)}
            maxLength={256}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.invoicesAddAmount')}</span>
          <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
            step="0.01" min="0.01"
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.invoicesAddType')}</span>
          <select value={type} onChange={e => setType(Number(e.target.value) as InvoiceType)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
            <option value={InvoiceTypeValue.OneTime}>{t('admin.invoicesTypeOneTime')}</option>
            <option value={InvoiceTypeValue.Subscription}>{t('admin.invoicesTypeSubscription')}</option>
          </select>
        </label>
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.invoicesAddDueDate')}</span>
          <input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
        <label className="text-xs sm:col-span-2">
          <span className="text-slate-700">{t('admin.invoicesAddNotes')}</span>
          <input type="text" value={notes} onChange={e => setNotes(e.target.value)}
            maxLength={2000}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
        </label>
      </div>
      <div className="flex gap-2">
        <button type="submit" disabled={busy || !picked}
          className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.invoicesAddSubmit')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-sm text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}

function EditInvoiceForm({ invoice, onSaved, onError, onCancel }: {
  invoice: InvoiceDto
  onSaved: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [description, setDescription] = useState(invoice.description)
  const [amount, setAmount] = useState(String(invoice.amount))
  const [type, setType] = useState<InvoiceType>(invoice.type)
  const [dueDate, setDueDate] = useState(invoice.dueDate ?? '')
  const [notes, setNotes] = useState(invoice.notes ?? '')
  const [playerId, setPlayerId] = useState<number | ''>(invoice.playerId ?? '')
  // Players for this invoice's parent — populate the dropdown so the admin can re-assign
  // or attach the invoice to a player without leaving the row.
  const [roster, setRoster] = useState<AdminPlayerSummary[]>([])
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    Api.listAdminPlayers().then(all => {
      setRoster(all.filter(p => p.parentAccountId === invoice.parentAccountId))
    }).catch((e: any) => onError(errMsg(e)))
  }, [invoice.parentAccountId])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const amt = Number(amount)
    if (!description.trim() || !isFinite(amt) || amt <= 0) {
      onError(t('admin.invoicesAddRequired'))
      return
    }
    setBusy(true)
    try {
      await Api.updateInvoice(invoice.id, {
        description: description.trim(),
        amount: amt,
        type,
        dueDate: dueDate || null,
        notes: notes.trim() || null,
        playerId: playerId === '' ? null : playerId,
      })
      await onSaved()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="grid sm:grid-cols-[2fr_1fr_1fr_1fr_auto] gap-2 items-end">
      <label className="text-xs">
        <span className="text-slate-700">{t('admin.invoicesAddDescription')}</span>
        <input type="text" value={description} onChange={e => setDescription(e.target.value)}
          maxLength={256}
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
      </label>
      <label className="text-xs">
        <span className="text-slate-700">{t('admin.invoicesAddAmount')}</span>
        <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
          step="0.01" min="0.01"
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
      </label>
      <label className="text-xs">
        <span className="text-slate-700">{t('admin.invoicesAddType')}</span>
        <select value={type} onChange={e => setType(Number(e.target.value) as InvoiceType)}
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
          <option value={InvoiceTypeValue.OneTime}>{t('admin.invoicesTypeOneTime')}</option>
          <option value={InvoiceTypeValue.Subscription}>{t('admin.invoicesTypeSubscription')}</option>
        </select>
      </label>
      <label className="text-xs">
        <span className="text-slate-700">{t('admin.invoicesAddDueDate')}</span>
        <input type="date" value={dueDate} onChange={e => setDueDate(e.target.value)}
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
      </label>
      <div className="flex gap-2">
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.save')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
      <label className="text-xs sm:col-span-5">
        <span className="text-slate-700">{t('admin.invoicesAddPlayer')}</span>
        <select value={playerId} onChange={e => setPlayerId(e.target.value === '' ? '' : Number(e.target.value))}
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
          <option value="">{t('admin.invoicesAddPlayerNoneOpt')}</option>
          {roster.map(p => (
            <option key={p.id} value={p.id}>{p.firstName} {p.lastName}{p.ageBracket ? ` (${p.ageBracket})` : ''}</option>
          ))}
        </select>
      </label>
      <label className="text-xs sm:col-span-5">
        <span className="text-slate-700">{t('admin.invoicesAddNotes')}</span>
        <input type="text" value={notes} onChange={e => setNotes(e.target.value)}
          maxLength={2000}
          className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
      </label>
    </form>
  )
}

function StatusForm({ invoice, onSaved, onError, onCancel }: {
  invoice: InvoiceDto
  onSaved: () => void | Promise<void>
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [status, setStatus] = useState<InvoiceStatus>(invoice.status)
  const [paymentMethod, setPaymentMethod] = useState(invoice.paymentMethod ?? '')
  const [paymentReference, setPaymentReference] = useState(invoice.paymentReference ?? '')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try {
      await Api.changeInvoiceStatus(invoice.id, {
        status,
        paymentMethod: status === InvoiceStatusValue.Paid ? (paymentMethod.trim() || null) : null,
        paymentReference: status === InvoiceStatusValue.Paid ? (paymentReference.trim() || null) : null,
      })
      await onSaved()
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  const showPayment = status === InvoiceStatusValue.Paid

  return (
    <form onSubmit={submit} className="space-y-2">
      <div className="flex flex-wrap items-end gap-2">
        <label className="text-xs">
          <span className="text-slate-700">{t('admin.invoicesStatusNew')}</span>
          <select value={status} onChange={e => setStatus(Number(e.target.value) as InvoiceStatus)}
            className="mt-1 border border-slate-300 rounded-md px-2 py-1 text-sm">
            {([0, 1, 2, 3] as InvoiceStatus[]).map(s => (
              <option key={s} value={s}>{STATUS_LABEL[s]}</option>
            ))}
          </select>
        </label>
        {showPayment && (
          <>
            <label className="text-xs flex-1 min-w-[160px]">
              <span className="text-slate-700">{t('admin.invoicesStatusPaymentMethod')}</span>
              <input type="text" value={paymentMethod} onChange={e => setPaymentMethod(e.target.value)}
                maxLength={120}
                placeholder="Zelle, Cash, Check, Card…"
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
            </label>
            <label className="text-xs flex-1 min-w-[160px]">
              <span className="text-slate-700">{t('admin.invoicesStatusPaymentReference')}</span>
              <input type="text" value={paymentReference} onChange={e => setPaymentReference(e.target.value)}
                maxLength={120}
                placeholder="confirmation / check #"
                className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm font-mono" />
            </label>
          </>
        )}
        <button type="submit" disabled={busy}
          className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.invoicesStatusSave')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}

/** Per-invoice email send: pick an Invoice-context email template, fire one broadcast to the
 *  invoice's parent. The send pipeline auto-fills invoice.* / chargeType.* / player.* / parent.*
 *  template variables from the invoice + its joined entities, so the admin doesn't need to type
 *  any per-row values. Falls back to admin-typed values for any unmapped variable. */
function SendInvoiceEmailForm({ invoice, onSent, onError, onCancel }: {
  invoice: InvoiceDto
  onSent: (templateName: string) => Promise<void> | void
  onError: (e: string) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [templates, setTemplates] = useState<EmailTemplate[]>([])
  const [templateId, setTemplateId] = useState<number | ''>('')
  const [values, setValues] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  useEffect(() => {
    Api.listEmailTemplates()
      .then(all => setTemplates(all.filter(t => t.context === TemplateContextValue.InvoiceNotification)))
      .catch((e: any) => onError(errMsg(e)))
  }, [])
  const picked = templates.find(t => t.id === templateId) ?? null
  // Only show inputs for variables that aren't auto-mapped — mapped ones are filled server-side.
  const unmapped = picked?.variables.filter(v => !v.propertyKey) ?? []

  const openPreview = (e: React.FormEvent) => {
    e.preventDefault()
    if (!picked) { onError(t('admin.invoicesEmailPickTemplate')); return }
    if (!invoice.parentEmail) { onError(t('admin.invoicesEmailNoAddress')); return }
    const missing = unmapped.filter(v => !values[v.position.toString()]?.trim()).map(v => v.label)
    if (missing.length) { onError(`Fill in: ${missing.join(', ')}.`); return }
    setPreviewOpen(true)
  }

  const send = async () => {
    if (!picked) return
    setBusy(true)
    try {
      await Api.createBroadcast({
        channel: 2, // Email
        emailTemplateId: picked.id,
        invoiceId: invoice.id,
        templateVariables: values,
        target: {
          kind: 3, // AdHocList
          recipients: [{
            phone: invoice.parentCellPhone ?? '',
            name: invoice.parentName ?? '',
            email: invoice.parentEmail!,
          }],
        },
      })
      setPreviewOpen(false)
      await onSent(picked.name)
    } catch (e: any) { onError(errMsg(e)) }
    finally { setBusy(false) }
  }

  return (
    <form onSubmit={openPreview} className="space-y-2">
      {previewOpen && picked && (
        <EmailTemplatePreviewModal
          template={picked}
          values={values}
          recipientLabel={`${invoice.parentName ?? ''} <${invoice.parentEmail}>`}
          invoiceId={invoice.id}
          sending={busy}
          onCancel={() => setPreviewOpen(false)}
          onConfirm={send}
        />
      )}
      <div className="text-xs text-slate-600">
        {t('admin.invoicesEmailTo')}: <span className="font-medium">{invoice.parentName ?? invoice.parentEmail}</span>
        <span className="text-slate-500 ml-1">&lt;{invoice.parentEmail}&gt;</span>
      </div>
      <div className="flex flex-wrap items-end gap-2">
        <label className="text-xs flex-1 min-w-[240px]">
          <span className="text-slate-700">{t('admin.invoicesEmailTemplate')}</span>
          <select value={templateId} onChange={e => setTemplateId(e.target.value === '' ? '' : Number(e.target.value))}
            className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm">
            <option value="">— {t('admin.invoicesEmailPickTemplate')} —</option>
            {templates.map(tpl => (
              <option key={tpl.id} value={tpl.id}>{tpl.name} ({tpl.language === 1 ? 'ES' : 'EN'})</option>
            ))}
          </select>
          {templates.length === 0 && (
            <span className="text-[10px] text-slate-500 mt-0.5 block">{t('admin.invoicesEmailNoTemplates')}</span>
          )}
        </label>
        <button type="submit" disabled={busy || !picked}
          className="bg-emerald-700 text-white text-xs font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {busy ? t('admin.sending') : t('admin.invoicesEmailSubmit')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}
          className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
      {unmapped.length > 0 && (
        <div className="space-y-1 pt-2 border-t border-slate-100">
          <div className="text-[10px] uppercase tracking-wide text-slate-500">{t('admin.invoicesEmailManualVars')}</div>
          {unmapped.map(v => {
            const key = v.position.toString()
            return (
              <div key={v.id} className="grid grid-cols-[8rem_1fr] items-center gap-2">
                <label className="text-xs text-slate-700">{v.label}</label>
                <input type="text" value={values[key] ?? ''}
                  placeholder={v.example ?? ''}
                  onChange={e => setValues(prev => ({ ...prev, [key]: e.target.value }))}
                  className="border border-slate-300 rounded-md px-2 py-1 text-xs" />
              </div>
            )
          })}
        </div>
      )}
    </form>
  )
}
