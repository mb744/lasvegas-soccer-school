import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type { ChatGroupAdmin, InboxParent, TeamSummary } from '../../api/types'

/**
 * Admin management of the native in-app chat groups parents use in the mobile app. Create a group
 * (optionally seeded from a team's roster), add/remove parent members, post a message into it (fans
 * out over SignalR + push), and delete it. The parent side is the mobile app's chat tab.
 */
export function AdminChatGroupsPage() {
  const [groups, setGroups] = useState<ChatGroupAdmin[]>([])
  const [teams, setTeams] = useState<TeamSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  // Create form
  const [title, setTitle] = useState('')
  const [seedTeamId, setSeedTeamId] = useState<number | ''>('')
  const [creating, setCreating] = useState(false)

  const [expandedId, setExpandedId] = useState<number | null>(null)

  const refresh = async () => {
    setError(null)
    try {
      setGroups(await Api.listChatGroups())
    } catch (e: any) {
      setError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    refresh()
    Api.listTeams().then(setTeams).catch(() => {})
  }, [])

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null); setNotice(null)
    if (!title.trim()) { setError('Title is required.'); return }
    setCreating(true)
    try {
      await Api.createChatGroup({ title: title.trim(), seedFromTeamId: seedTeamId === '' ? null : seedTeamId })
      setTitle(''); setSeedTeamId('')
      setNotice('Chat group created.')
      await refresh()
    } catch (e: any) {
      setError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setCreating(false)
    }
  }

  const remove = async (id: number) => {
    if (!confirm('Delete this chat group and all its messages?')) return
    setError(null)
    try {
      await Api.deleteChatGroup(id)
      await refresh()
    } catch (e: any) {
      setError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    }
  }

  return (
    <Layout>
      <div className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <nav className="text-sm text-slate-500">
          <Link to="/admin" className="hover:underline">Admin</Link> <span>/</span> <span>Chat groups</span>
        </nav>

        <header>
          <h1 className="text-2xl font-bold text-emerald-800">Chat groups</h1>
          <p className="mt-1 text-slate-600 text-sm">
            In-app group chats for the parent mobile app. Create a group, add the parents who should be
            in it (or seed it from a team roster), and they'll see it in the app.
          </p>
        </header>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded px-4 py-2 text-sm">{error}</div>}
        {notice && <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 rounded px-4 py-2 text-sm">{notice}</div>}

        <form onSubmit={create} className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
          <h2 className="font-semibold text-slate-800">New group</h2>
          <div className="grid sm:grid-cols-2 gap-3">
            <input
              className="border border-slate-300 rounded px-3 py-2 text-sm"
              placeholder="Group title (e.g. B2014 Team Chat)"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
            <select
              className="border border-slate-300 rounded px-3 py-2 text-sm"
              value={seedTeamId}
              onChange={(e) => setSeedTeamId(e.target.value === '' ? '' : Number(e.target.value))}
            >
              <option value="">Seed from team (optional)…</option>
              {teams.map((tm) => (
                <option key={tm.id} value={tm.id}>{tm.name}</option>
              ))}
            </select>
          </div>
          <button
            type="submit"
            disabled={creating}
            className="bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white font-semibold rounded px-4 py-2 text-sm"
          >
            {creating ? 'Creating…' : 'Create group'}
          </button>
        </form>

        {loading ? (
          <p className="text-slate-500">Loading…</p>
        ) : groups.length === 0 ? (
          <p className="text-slate-500">No chat groups yet.</p>
        ) : (
          <div className="space-y-3">
            {groups.map((g) => (
              <GroupCard
                key={g.id}
                group={g}
                expanded={expandedId === g.id}
                onToggle={() => setExpandedId(expandedId === g.id ? null : g.id)}
                onChanged={refresh}
                onDelete={() => remove(g.id)}
                onError={setError}
              />
            ))}
          </div>
        )}
      </div>
    </Layout>
  )
}

function GroupCard({
  group, expanded, onToggle, onChanged, onDelete, onError,
}: {
  group: ChatGroupAdmin
  expanded: boolean
  onToggle: () => void
  onChanged: () => Promise<void>
  onDelete: () => void
  onError: (e: string | null) => void
}) {
  const [search, setSearch] = useState('')
  const [results, setResults] = useState<InboxParent[]>([])
  const [searching, setSearching] = useState(false)
  const [message, setMessage] = useState('')
  const [posting, setPosting] = useState(false)

  const doSearch = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!search.trim()) return
    setSearching(true)
    try {
      setResults(await Api.searchInboxParents(search.trim(), { limit: 10 }))
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.message || 'Error')
    } finally {
      setSearching(false)
    }
  }

  const addMember = async (parentAccountId: number) => {
    onError(null)
    try {
      await Api.addChatGroupMember(group.id, { parentAccountId })
      setSearch(''); setResults([])
      await onChanged()
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.message || 'Error')
    }
  }

  const removeMember = async (memberId: number) => {
    onError(null)
    try {
      await Api.removeChatGroupMember(group.id, memberId)
      await onChanged()
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.message || 'Error')
    }
  }

  const post = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!message.trim()) return
    setPosting(true)
    onError(null)
    try {
      await Api.postChatGroupMessage(group.id, message.trim())
      setMessage('')
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.message || 'Error')
    } finally {
      setPosting(false)
    }
  }

  return (
    <div className="bg-white border border-slate-200 rounded-lg">
      <button onClick={onToggle} className="w-full flex items-center justify-between px-4 py-3 text-left">
        <div>
          <span className="font-semibold text-slate-800">{group.title}</span>
          {group.teamName && <span className="ml-2 text-xs text-slate-500">· {group.teamName}</span>}
        </div>
        <span className="text-sm text-slate-500">
          {group.memberCount} member{group.memberCount === 1 ? '' : 's'} · {group.messageCount} msg
        </span>
      </button>

      {expanded && (
        <div className="border-t border-slate-200 p-4 space-y-4">
          {/* Members */}
          <div>
            <h3 className="text-sm font-semibold text-slate-700 mb-2">Members</h3>
            {group.members.length === 0 ? (
              <p className="text-sm text-slate-500">No members yet.</p>
            ) : (
              <ul className="divide-y divide-slate-100">
                {group.members.map((m) => (
                  <li key={m.id} className="flex items-center justify-between py-1.5">
                    <span className="text-sm text-slate-700">
                      {m.displayName}
                      {m.role === 1 && <span className="ml-1 text-xs text-amber-600">(admin)</span>}
                    </span>
                    <button onClick={() => removeMember(m.id)} className="text-xs text-red-600 hover:underline">
                      Remove
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Add member */}
          <div>
            <form onSubmit={doSearch} className="flex gap-2">
              <input
                className="flex-1 border border-slate-300 rounded px-3 py-1.5 text-sm"
                placeholder="Search parents by name or phone…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <button type="submit" disabled={searching} className="text-sm border border-slate-300 rounded px-3 py-1.5 hover:bg-slate-50">
                {searching ? '…' : 'Search'}
              </button>
            </form>
            {results.length > 0 && (
              <ul className="mt-2 border border-slate-200 rounded divide-y divide-slate-100">
                {results.map((p) => (
                  <li key={p.parentAccountId} className="flex items-center justify-between px-3 py-1.5">
                    <span className="text-sm text-slate-700">{p.name} <span className="text-slate-400">{p.phone}</span></span>
                    <button onClick={() => addMember(p.parentAccountId)} className="text-xs text-emerald-700 hover:underline">
                      Add
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Post a message */}
          <form onSubmit={post} className="flex gap-2">
            <input
              className="flex-1 border border-slate-300 rounded px-3 py-1.5 text-sm"
              placeholder="Post a message to the group…"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
            <button type="submit" disabled={posting || !message.trim()} className="text-sm bg-emerald-600 disabled:opacity-50 text-white rounded px-3 py-1.5">
              {posting ? '…' : 'Post'}
            </button>
          </form>

          <div className="pt-2 border-t border-slate-100">
            <button onClick={onDelete} className="text-xs text-red-600 hover:underline">Delete group</button>
          </div>
        </div>
      )}
    </div>
  )
}
