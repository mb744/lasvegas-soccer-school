import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Api } from '../api/client'
import type { PublicScheduleDto } from '../api/types'

/** Shareable read-only tournament schedule. Rendered without the Layout chrome so parents /
 *  external coaches don't see the LVSS admin nav. Fetched via /api/public/hosted-tournaments/{slug}
 *  — no auth required. */
export function TournamentPublicPage() {
  const { slug } = useParams<{ slug: string }>()
  const [data, setData] = useState<PublicScheduleDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!slug) return
    Api.publicHostedTournament(slug)
      .then(setData)
      .catch(e => setError(e?.response?.status === 404 ? 'Schedule not found.' : (e?.message ?? 'Error')))
  }, [slug])

  if (error) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center p-6">
        <div className="max-w-md text-center">
          <h1 className="text-xl font-bold text-slate-800">Schedule unavailable</h1>
          <p className="mt-2 text-slate-600 text-sm">{error}</p>
        </div>
      </div>
    )
  }
  if (!data) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center p-6">
        <p className="text-slate-500">Loading…</p>
      </div>
    )
  }

  const kindLabel = data.kind === 1 ? 'League' : 'Tournament'
  const dateRange = data.endDate && data.endDate !== data.startDate
    ? `${data.startDate} → ${data.endDate}`
    : data.startDate

  return (
    <div className="min-h-screen bg-slate-50">
      <div className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <header>
          <div className="text-xs uppercase tracking-wide text-emerald-700">{kindLabel}</div>
          <h1 className="text-3xl font-bold text-emerald-800 mt-1">{data.name}</h1>
          <div className="text-sm text-slate-600 mt-1">
            {dateRange}
            {data.venueName && <span> · {data.venueName}</span>}
            {data.venueAddress && <span className="text-slate-500"> ({data.venueAddress})</span>}
            {!data.venueName && data.location && <span> · {data.location}</span>}
          </div>
        </header>

        {data.rulesOfPlay && (
          <section className="bg-white border border-slate-200 rounded-lg p-5 whitespace-pre-wrap text-sm text-slate-700">
            {data.rulesOfPlay}
          </section>
        )}

        <section className="bg-white border border-slate-200 rounded-lg p-5">
          <h2 className="font-bold text-emerald-800 mb-3">Schedule</h2>
          {data.matches.length === 0 ? (
            <p className="text-sm text-slate-500">The schedule hasn't been posted yet — check back soon.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-slate-500 border-b">
                    <th className="py-2 px-2">Day</th>
                    <th className="py-2 px-2">Time</th>
                    <th className="py-2 px-2">Field</th>
                    <th className="py-2 px-2">Group</th>
                    <th className="py-2 px-2">Match</th>
                  </tr>
                </thead>
                <tbody>
                  {data.matches.map(m => (
                    <tr key={m.id} className="border-b last:border-0">
                      <td className="py-1.5 px-2 whitespace-nowrap">{m.dayDate ?? '—'}</td>
                      <td className="py-1.5 px-2 font-mono whitespace-nowrap">{m.startTime?.slice(0, 5) ?? 'TBD'}</td>
                      <td className="py-1.5 px-2">{m.fieldName ?? 'TBD'}</td>
                      <td className="py-1.5 px-2 text-slate-500">{m.tierName ?? ''}</td>
                      <td className="py-1.5 px-2 font-medium">{m.teamALabel ?? 'TBD'} <span className="text-slate-400 font-normal">vs</span> {m.teamBLabel ?? 'TBD'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <footer className="text-xs text-slate-400 pt-4">
          Powered by Las Vegas Soccer School
        </footer>
      </div>
    </div>
  )
}
