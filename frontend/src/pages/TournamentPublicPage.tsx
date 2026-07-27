import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Api } from '../api/client'
import type { BracketStandings, PublicScheduleDto } from '../api/types'

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
          // Collapsed by default — the rules block can be long, and visitors mostly come here for
          // the schedule + standings. Native <details> so no JS needed and keyboard/screen-reader
          // support is free.
          <details className="bg-white border border-slate-200 rounded-lg group">
            <summary className="cursor-pointer select-none px-5 py-3 font-medium text-slate-800 flex items-center justify-between">
              <span>Rules of play</span>
              <span className="text-xs text-slate-400 group-open:hidden">Show</span>
              <span className="text-xs text-slate-400 hidden group-open:inline">Hide</span>
            </summary>
            <div className="px-5 pb-5 whitespace-pre-wrap text-sm text-slate-700 border-t border-slate-100 pt-3">
              {data.rulesOfPlay}
            </div>
          </details>
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
                    <th className="py-2 px-2">Score</th>
                  </tr>
                </thead>
                <tbody>
                  {data.matches.map(m => {
                    const played = m.teamAScore != null && m.teamBScore != null
                    return (
                      <tr key={m.id} className="border-b last:border-0">
                        <td className="py-1.5 px-2 whitespace-nowrap">{m.dayDate ?? '—'}</td>
                        <td className="py-1.5 px-2 font-mono whitespace-nowrap">{m.startTime?.slice(0, 5) ?? 'TBD'}</td>
                        <td className="py-1.5 px-2">{m.fieldName ?? 'TBD'}</td>
                        <td className="py-1.5 px-2 text-slate-500">{m.tierName ?? ''}</td>
                        <td className="py-1.5 px-2 font-medium">{m.teamALabel ?? 'TBD'} <span className="text-slate-400 font-normal">vs</span> {m.teamBLabel ?? 'TBD'}</td>
                        <td className="py-1.5 px-2 font-mono whitespace-nowrap">
                          {played
                            ? <span className="text-emerald-800">{m.teamAScore} – {m.teamBScore}</span>
                            : <span className="text-slate-400">—</span>}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        {data.standings.length > 0 && (
          <section className="space-y-4">
            <h2 className="font-bold text-emerald-800">Standings</h2>
            {data.standings.map(s => (
              <StandingsCard key={`${s.tierName ?? 'notier'}-${s.bracketId ?? s.bracketName}`} standings={s} />
            ))}
          </section>
        )}

        <footer className="text-xs text-slate-400 pt-4">
          Powered by Las Vegas Soccer School
        </footer>
      </div>
    </div>
  )
}

/** One bracket's standings table — same columns the admin requested: G / W / L / D / GF / GA
 *  / GD / Points. Rows already come sorted from the backend. */
function StandingsCard({ standings }: { standings: BracketStandings }) {
  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4">
      <div className="flex items-baseline justify-between mb-2 flex-wrap gap-x-3">
        <h3 className="font-semibold text-slate-800">{standings.bracketName}</h3>
        {standings.tierName && standings.tierName !== standings.bracketName && (
          <span className="text-xs uppercase tracking-wide text-slate-500">{standings.tierName}</span>
        )}
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b text-xs uppercase tracking-wide">
              <th className="py-1.5 px-2">Team</th>
              <th className="py-1.5 px-2 text-center" title="Games played">G</th>
              <th className="py-1.5 px-2 text-center" title="Wins">W</th>
              <th className="py-1.5 px-2 text-center" title="Losses">L</th>
              <th className="py-1.5 px-2 text-center" title="Draws">D</th>
              <th className="py-1.5 px-2 text-center" title="Goals for">GF</th>
              <th className="py-1.5 px-2 text-center" title="Goals against">GA</th>
              <th className="py-1.5 px-2 text-center" title="Goal differential">GD</th>
              <th className="py-1.5 px-2 text-center" title="Points (3 win / 1 draw)">Pts</th>
            </tr>
          </thead>
          <tbody>
            {standings.rows.map(r => (
              <tr key={r.teamId} className="border-b last:border-0">
                <td className="py-1.5 px-2 font-medium">{r.teamName}</td>
                <td className="py-1.5 px-2 text-center">{r.gamesPlayed}</td>
                <td className="py-1.5 px-2 text-center">{r.wins}</td>
                <td className="py-1.5 px-2 text-center">{r.losses}</td>
                <td className="py-1.5 px-2 text-center">{r.draws}</td>
                <td className="py-1.5 px-2 text-center">{r.goalsFor}</td>
                <td className="py-1.5 px-2 text-center">{r.goalsAgainst}</td>
                <td className={`py-1.5 px-2 text-center font-mono ${r.goalDifferential > 0 ? 'text-emerald-700' : r.goalDifferential < 0 ? 'text-rose-700' : 'text-slate-500'}`}>
                  {r.goalDifferential > 0 ? `+${r.goalDifferential}` : r.goalDifferential}
                </td>
                <td className="py-1.5 px-2 text-center font-bold text-emerald-800">{r.points}</td>
              </tr>
            ))}
            {standings.rows.length === 0 && (
              <tr><td colSpan={9} className="py-3 text-center text-xs text-slate-400">No teams in this bracket yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
