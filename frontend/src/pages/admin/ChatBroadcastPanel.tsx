import { useEffect, useState } from 'react'
import { Api } from '../../api/client'
import type { ChatGroupAdmin } from '../../api/types'

/**
 * Chat groups → "Message all groups": one message posted into every chat group (or the ones left
 * ticked). Each parent gets a single notification even if they're in several groups.
 */
export function ChatBroadcastPanel({
  groups,
  onSent,
  onError,
}: {
  groups: ChatGroupAdmin[]
  onSent: (notice: string) => void
  onError: (e: string | null) => void
}) {
  const [open, setOpen] = useState(false)
  const [body, setBody] = useState('')
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [sending, setSending] = useState(false)

  // Everything ticked by default, including groups created while the panel is open.
  useEffect(() => { setSelected(new Set(groups.map(g => g.id))) }, [groups])

  const allSelected = selected.size === groups.length
  const toggle = (id: number) => {
    const next = new Set(selected)
    if (next.has(id)) next.delete(id); else next.add(id)
    setSelected(next)
  }

  const send = async () => {
    const text = body.trim()
    if (!text || selected.size === 0) return
    const what = allSelected ? `all ${groups.length} chat groups` : `${selected.size} chat group${selected.size === 1 ? '' : 's'}`
    if (!confirm(`Post this message to ${what}? Everyone in them will be notified.`)) return
    setSending(true); onError(null)
    try {
      // An empty list means "every group", so new groups made meanwhile are included too.
      const r = await Api.broadcastChatMessage(text, allSelected ? [] : [...selected])
      setBody('')
      setOpen(false)
      onSent(`Posted to ${r.groups} chat group${r.groups === 1 ? '' : 's'}; ${r.people} ${r.people === 1 ? 'person' : 'people'} notified.`)
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setSending(false)
    }
  }

  if (groups.length === 0) return null

  if (!open) {
    return (
      <button
        onClick={() => setOpen(true)}
        className="bg-white border border-emerald-600 text-emerald-700 hover:bg-emerald-50 font-semibold rounded px-4 py-2 text-sm"
      >
        Message all groups
      </button>
    )
  }

  return (
    <div className="bg-white border border-emerald-300 rounded-lg p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="font-semibold text-slate-800">Message all groups</h2>
        <button onClick={() => setOpen(false)} className="text-sm text-slate-500 hover:underline">Cancel</button>
      </div>
      <p className="text-sm text-slate-600">
        Posts your message in each group's chat as you. Parents in more than one group get one notification.
      </p>
      <textarea
        className="w-full border border-slate-300 rounded px-3 py-2 text-sm"
        rows={4}
        maxLength={4000}
        placeholder="e.g. All fields are closed today because of the weather."
        value={body}
        onChange={(e) => setBody(e.target.value)}
      />
      <div>
        <div className="flex items-center justify-between text-sm">
          <span className="text-slate-700 font-medium">Send to {selected.size} of {groups.length} groups</span>
          <button
            type="button"
            onClick={() => setSelected(allSelected ? new Set() : new Set(groups.map(g => g.id)))}
            className="text-emerald-700 hover:underline"
          >
            {allSelected ? 'Select none' : 'Select all'}
          </button>
        </div>
        <div className="mt-2 grid sm:grid-cols-2 gap-1 max-h-56 overflow-y-auto border border-slate-200 rounded p-2">
          {groups.map(g => (
            <label key={g.id} className="flex items-center gap-2 text-sm py-0.5">
              <input type="checkbox" checked={selected.has(g.id)} onChange={() => toggle(g.id)} />
              <span className="truncate">{g.title}</span>
              <span className="text-slate-400 text-xs shrink-0">{g.memberCount}</span>
            </label>
          ))}
        </div>
      </div>
      <button
        onClick={send}
        disabled={sending || !body.trim() || selected.size === 0}
        className="bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white font-semibold rounded px-4 py-2 text-sm"
      >
        {sending ? 'Sending…' : 'Send'}
      </button>
    </div>
  )
}
