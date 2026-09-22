import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type { Announcement, SaveAnnouncementRequest, TeamSummary } from '../../api/types'

/**
 * Admin CRUD for the parent-facing Home-tab announcements the mobile app displays. Announcements
 * can be school-wide (no team selected) or targeted at a specific team's parents. Endsat is
 * optional; when omitted the announcement stays visible until the admin flips IsActive off.
 */
export function AdminAnnouncementsPage() {
  const [rows, setRows] = useState<Announcement[]>([])
  const [teams, setTeams] = useState<TeamSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Announcement | 'new' | null>(null)

  const refresh = async () => {
    setError(null)
    try {
      setRows(await Api.listAnnouncements())
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

  const remove = async (id: number) => {
    if (!confirm('Delete this announcement? Parents will no longer see it.')) return
    setError(null)
    try {
      await Api.deleteAnnouncement(id)
      setNotice('Announcement deleted.')
      await refresh()
    } catch (e: any) {
      setError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    }
  }

  const toggleActive = async (row: Announcement) => {
    setError(null)
    try {
      await Api.updateAnnouncement(row.id, {
        title: row.title,
        body: row.body,
        teamId: row.teamId,
        endsAt: row.endsAt,
        isActive: !row.isActive,
      })
      await refresh()
    } catch (e: any) {
      setError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    }
  }

  return (
    <Layout>
      <div className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <nav className="text-sm text-slate-500">
          <Link to="/admin" className="hover:underline">
            Admin
          </Link>{' '}
          <span>/</span> <span>Announcements</span>
        </nav>

        <header className="flex items-start justify-between gap-3 flex-wrap">
          <div>
            <h1 className="text-2xl font-bold text-emerald-800">Announcements</h1>
            <p className="mt-1 text-slate-600 text-sm">
              Short messages that appear on the parent mobile app's Home tab. School-wide entries
              reach every family; team-targeted entries only reach parents whose kids are on that
              team. Expired or inactive entries are hidden automatically.
            </p>
          </div>
          <button
            onClick={() => setEditing('new')}
            className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold rounded px-4 py-2 text-sm"
          >
            + New announcement
          </button>
        </header>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded px-4 py-2 text-sm">
            {error}
          </div>
        )}
        {notice && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 rounded px-4 py-2 text-sm">
            {notice}
          </div>
        )}

        {editing !== null && (
          <EditorCard
            teams={teams}
            initial={editing === 'new' ? null : editing}
            onCancel={() => setEditing(null)}
            onSaved={async () => {
              setEditing(null)
              setNotice(editing === 'new' ? 'Announcement created.' : 'Announcement updated.')
              await refresh()
            }}
            onError={setError}
          />
        )}

        {loading ? (
          <p className="text-slate-500">Loading…</p>
        ) : rows.length === 0 ? (
          <p className="text-slate-500">No announcements yet. Tap "New announcement" to post the first one.</p>
        ) : (
          <div className="space-y-3">
            {rows.map((r) => (
              <AnnouncementCard
                key={r.id}
                row={r}
                onEdit={() => setEditing(r)}
                onDelete={() => remove(r.id)}
                onToggleActive={() => toggleActive(r)}
              />
            ))}
          </div>
        )}
      </div>
    </Layout>
  )
}

function EditorCard({
  teams,
  initial,
  onCancel,
  onSaved,
  onError,
}: {
  teams: TeamSummary[]
  initial: Announcement | null
  onCancel: () => void
  onSaved: () => Promise<void>
  onError: (e: string) => void
}) {
  const [title, setTitle] = useState(initial?.title ?? '')
  const [body, setBody] = useState(initial?.body ?? '')
  const [teamId, setTeamId] = useState<number | ''>(initial?.teamId ?? '')
  const [endsAt, setEndsAt] = useState(initial?.endsAt ? initial.endsAt.slice(0, 16) : '')
  const [isActive, setIsActive] = useState(initial?.isActive ?? true)
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    onError('')
    if (!title.trim() || !body.trim()) {
      onError('Title and body are required.')
      return
    }
    setBusy(true)
    try {
      const payload: SaveAnnouncementRequest = {
        title: title.trim(),
        body: body.trim(),
        teamId: teamId === '' ? null : teamId,
        endsAt: endsAt ? new Date(endsAt).toISOString() : null,
        isActive,
      }
      if (initial) {
        await Api.updateAnnouncement(initial.id, payload)
      } else {
        await Api.createAnnouncement(payload)
      }
      await onSaved()
    } catch (e: any) {
      onError(e?.response?.data?.title || e?.response?.data || e?.message || 'Error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
      <h2 className="font-semibold text-slate-800">
        {initial ? 'Edit announcement' : 'New announcement'}
      </h2>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">Title</span>
        <input
          className="mt-1 w-full border border-slate-300 rounded px-3 py-2 text-sm"
          placeholder="Field 3 will be closed Saturday"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          maxLength={200}
        />
      </label>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">Message</span>
        <textarea
          className="mt-1 w-full border border-slate-300 rounded px-3 py-2 text-sm"
          placeholder="Practice moved to Field 5. Same time, same coach."
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={4}
          maxLength={2000}
        />
      </label>

      <div className="grid sm:grid-cols-2 gap-3">
        <label className="block text-sm">
          <span className="font-medium text-slate-700">Audience</span>
          <select
            className="mt-1 w-full border border-slate-300 rounded px-3 py-2 text-sm"
            value={teamId}
            onChange={(e) => setTeamId(e.target.value === '' ? '' : Number(e.target.value))}
          >
            <option value="">Everyone (school-wide)</option>
            {teams.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm">
          <span className="font-medium text-slate-700">Auto-expire at (optional)</span>
          <input
            type="datetime-local"
            className="mt-1 w-full border border-slate-300 rounded px-3 py-2 text-sm"
            value={endsAt}
            onChange={(e) => setEndsAt(e.target.value)}
          />
        </label>
      </div>

      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        <span>Visible to parents</span>
      </label>

      <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
        <button
          type="submit"
          disabled={busy}
          className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
        >
          {busy ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={onCancel} className="text-sm text-slate-600 hover:underline">
          Cancel
        </button>
      </div>
    </form>
  )
}

function AnnouncementCard({
  row,
  onEdit,
  onDelete,
  onToggleActive,
}: {
  row: Announcement
  onEdit: () => void
  onDelete: () => void
  onToggleActive: () => void
}) {
  const expired = row.endsAt && new Date(row.endsAt) < new Date()
  return (
    <div
      className={`bg-white border rounded-lg p-4 ${row.isActive && !expired ? 'border-emerald-300' : 'border-slate-200 opacity-70'}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h3 className="font-bold text-slate-800">{row.title}</h3>
            {row.teamName ? (
              <span className="text-xs bg-emerald-100 text-emerald-800 rounded px-2 py-0.5">
                {row.teamName}
              </span>
            ) : (
              <span className="text-xs bg-slate-100 text-slate-700 rounded px-2 py-0.5">
                Everyone
              </span>
            )}
            {!row.isActive && (
              <span className="text-xs bg-slate-200 text-slate-700 rounded px-2 py-0.5">Hidden</span>
            )}
            {expired && (
              <span className="text-xs bg-amber-100 text-amber-800 rounded px-2 py-0.5">Expired</span>
            )}
          </div>
          <p className="mt-2 text-sm text-slate-700 whitespace-pre-wrap">{row.body}</p>
          <p className="mt-2 text-xs text-slate-500">
            Posted {new Date(row.createdAt).toLocaleString()}
            {row.endsAt && ` · Expires ${new Date(row.endsAt).toLocaleString()}`}
          </p>
        </div>
        <div className="text-sm whitespace-nowrap flex flex-col items-end gap-1">
          <button onClick={onEdit} className="text-emerald-700 hover:underline">
            Edit
          </button>
          <button onClick={onToggleActive} className="text-slate-600 hover:underline">
            {row.isActive ? 'Hide' : 'Show'}
          </button>
          <button onClick={onDelete} className="text-rose-700 hover:underline">
            Delete
          </button>
        </div>
      </div>
    </div>
  )
}
