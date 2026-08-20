import { useParams, Link } from 'react-router-dom';
import {
  Trophy,
  Target,
  Zap,
  Users,
  BarChart2,
  ArrowLeft,
  Loader2,
  AlertCircle,
} from 'lucide-react';
import { useTournamentResults } from '@/hooks/useRounds';
import { formatDate } from '@/lib/utils';
import { HandicapDots } from '@/components/scoring/HandicapDots';
import type {
  TournamentSkinsResult,
  TournamentSkinHole,
  TournamentMatchupResult,
  TournamentRankingEntry,
  TournamentHoleExtra,
  LongestDriveWinner,
  TournamentFlight,
  TournamentCourseHole,
  TournamentResults,
} from '@/types/api';

// ── Shared helpers ────────────────────────────────────────────────────────────

function SectionTitle({ icon: Icon, label }: { icon: React.ElementType; label: string }) {
  return (
    <div className="mb-3 flex items-center gap-2 border-b border-gray-200 pb-2">
      <Icon className="h-5 w-5 text-green-700" />
      <h2 className="text-lg font-semibold text-gray-800">{label}</h2>
    </div>
  );
}

// ── Skins ─────────────────────────────────────────────────────────────────────

function SkinsHoleRow({ hole }: { hole: TournamentSkinHole }) {
  return (
    <tr className={hole.isTie ? 'bg-gray-50' : ''}>
      <td className="px-3 py-2 text-center font-mono text-sm">{hole.holeNumber}</td>
      <td className="px-3 py-2 text-center text-sm text-gray-500">{hole.par}</td>
      <td className="px-3 py-2 text-center text-sm">
        {hole.isTie ? (
          <span className="text-gray-400 italic">Tie (carry +1)</span>
        ) : (
          <span className="font-medium text-green-700">{hole.winnerPlayerName ?? '—'}</span>
        )}
      </td>
      <td className="px-3 py-2 text-center text-sm">
        {hole.winningScore !== null ? hole.winningScore : '—'}
      </td>
      <td className="px-3 py-2 text-center text-sm">
        {hole.isTie ? (
          <span className="text-gray-400">—</span>
        ) : (
          <span className={hole.wasCarryover ? 'font-bold text-amber-600' : 'font-semibold text-gray-700'}>
            {hole.skinValue > 0 ? `${hole.skinValue}` : '—'}
            {hole.wasCarryover && ' ★'}
          </span>
        )}
      </td>
    </tr>
  );
}

function SkinsPanel({ skins }: { skins: TournamentSkinsResult }) {
  const label = skins.skinType === 'Gross' ? 'Gross Skins' : 'Net Skins';
  return (
    <div>
      <h3 className="mb-2 text-base font-semibold text-gray-700">{label}</h3>
      {skins.holeResults.length === 0 ? (
        <p className="text-sm text-gray-400 italic">No scores submitted yet.</p>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
                <tr>
                  <th className="px-3 py-2 text-center">Hole</th>
                  <th className="px-3 py-2 text-center">Par</th>
                  <th className="px-3 py-2 text-center">Winner</th>
                  <th className="px-3 py-2 text-center">Score</th>
                  <th className="px-3 py-2 text-center">Value</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {skins.holeResults.map((h) => (
                  <SkinsHoleRow key={h.holeNumber} hole={h} />
                ))}
              </tbody>
            </table>
          </div>

          {skins.playerSummaries.length > 0 && (
            <div className="mt-3">
              <h4 className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Player Summary
              </h4>
              <div className="flex flex-wrap gap-2">
                {skins.playerSummaries.map((ps) => (
                  <div
                    key={ps.playerId}
                    className="flex items-center gap-2 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-sm"
                  >
                    <Trophy className="h-3.5 w-3.5 text-amber-500" />
                    <span className="font-medium text-amber-800">{ps.playerName}</span>
                    <span className="text-amber-600">
                      {ps.totalSkinsWon} skin{ps.totalSkinsWon !== 1 ? 's' : ''} ({ps.totalSkinValue} pts)
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ── Hole Extras ───────────────────────────────────────────────────────────────

function HoleExtrasPanel({
  extras,
  ldWinners,
}: {
  extras: TournamentHoleExtra[];
  ldWinners: LongestDriveWinner[];
}) {
  const ctp = extras.filter((e) => e.closestToPinPlayerId !== null);

  if (ctp.length === 0 && ldWinners.length === 0) {
    return (
      <p className="text-sm text-gray-400 italic">
        Closest to pin and longest drive not yet recorded.
      </p>
    );
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <div>
        <h3 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-gray-700">
          <Target className="h-4 w-4 text-green-600" />
          Closest to the Pin (Par 3s)
        </h3>
        {ctp.length === 0 ? (
          <p className="text-sm text-gray-400 italic">Not recorded.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="text-xs uppercase tracking-wide text-gray-400">
              <tr>
                <th className="py-1 text-left">Hole</th>
                <th className="py-1 text-left">Player</th>
              </tr>
            </thead>
            <tbody>
              {ctp.map((e) => (
                <tr key={e.holeNumber} className="border-t border-gray-100">
                  <td className="py-1.5 pr-4 font-mono text-gray-500">#{e.holeNumber}</td>
                  <td className="py-1.5 font-medium text-gray-800">{e.closestToPinPlayerName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div>
        <h3 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-gray-700">
          <Zap className="h-4 w-4 text-amber-500" />
          Longest Drive
        </h3>
        {ldWinners.length === 0 ? (
          <p className="text-sm text-gray-400 italic">Not recorded.</p>
        ) : (
          <ul className="space-y-1.5">
            {ldWinners.map((w) => (
              <li
                key={w.tournamentFlightId}
                className="flex items-center justify-between rounded-md border border-amber-200 bg-amber-50 px-3 py-1.5 text-sm"
              >
                <span className="font-medium text-gray-700">Flight {w.flightName}</span>
                <span className="text-amber-800">{w.playerName ?? '—'}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

// ── Flights ───────────────────────────────────────────────────────────────────

function FlightScorecard({ flight, holes }: { flight: TournamentFlight; holes: TournamentCourseHole[] }) {
  if (holes.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 p-3">
        <h4 className="mb-1.5 text-sm font-semibold text-gray-700">Flight {flight.name}</h4>
        <ul className="space-y-0.5 text-sm text-gray-600">
          {flight.players.map((p) => (
            <li key={p.playerId}>{p.playerName}</li>
          ))}
        </ul>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-gray-200 p-3">
      <h4 className="mb-2 text-sm font-semibold text-gray-700">Flight {flight.name}</h4>
      <div className="overflow-x-auto">
        <table className="w-full min-w-max text-xs">
          <thead className="text-gray-400">
            <tr>
              <th className="sticky left-0 bg-white px-2 py-1 text-left font-medium">Player</th>
              {holes.map((h) => (
                <th key={h.holeNumber} className="px-1.5 py-1 text-center font-medium">
                  {h.holeNumber}
                </th>
              ))}
              <th className="px-2 py-1 text-center font-semibold text-gray-600">Net</th>
            </tr>
            <tr className="text-gray-300">
              <th className="sticky left-0 bg-white px-2 py-0.5 text-left font-normal">Par</th>
              {holes.map((h) => (
                <th key={h.holeNumber} className="px-1.5 py-0.5 text-center font-normal">
                  {h.par}
                </th>
              ))}
              <th />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {flight.players.map((p) => {
              const scoresByHole = new Map(p.holeScores.map((h) => [h.holeNumber, h]));
              return (
                <tr key={p.playerId}>
                  <td className="sticky left-0 whitespace-nowrap bg-white px-2 py-1.5 font-medium text-gray-800">
                    {p.playerName}
                    <span className="ml-1 font-normal text-gray-400">({p.courseHandicap})</span>
                  </td>
                  {holes.map((h) => {
                    const score = scoresByHole.get(h.holeNumber);
                    return (
                      <td key={h.holeNumber} className="relative px-1.5 py-1.5 text-center text-gray-700">
                        {score?.grossStrokes ?? <span className="text-gray-300">—</span>}
                        {score && score.handicapStrokes > 0 && (
                          <span className="absolute inset-x-0 -bottom-0.5">
                            <HandicapDots strokes={score.handicapStrokes} />
                          </span>
                        )}
                      </td>
                    );
                  })}
                  <td className="px-2 py-1.5 text-center font-semibold text-gray-800">
                    {p.totalNetStrokes ?? <span className="text-gray-300">—</span>}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function FlightsPanel({ flights, holes }: { flights: TournamentFlight[]; holes: TournamentCourseHole[] }) {
  if (flights.length === 0) return null;

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      {flights.map((f) => (
        <FlightScorecard key={f.id} flight={f} holes={holes} />
      ))}
    </div>
  );
}

// ── Matchups ──────────────────────────────────────────────────────────────────

function MatchupCard({ m }: { m: TournamentMatchupResult }) {
  const halved = m.isHalved;
  const p1Wins = m.winnerPlayerId === m.player1Id;
  const p2Wins = m.winnerPlayerId === m.player2Id;
  const pending = m.winnerPlayerId === null && !halved;

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">
        Matchup {m.matchupNumber}
      </div>
      <div className="flex items-center justify-between gap-2">
        {/* Player 1 */}
        <div className={`flex-1 rounded-lg p-3 text-center ${p1Wins ? 'bg-green-50 ring-2 ring-green-400' : 'bg-gray-50'}`}>
          <p className={`font-semibold ${p1Wins ? 'text-green-800' : 'text-gray-800'}`}>
            {m.player1Name}
          </p>
          <p className="mt-0.5 text-xs text-gray-500">
            CH {m.player1CourseHandicap}
          </p>
          {m.player1NetStrokes !== null && (
            <p className="mt-1 text-lg font-bold text-gray-700">{m.player1NetStrokes}</p>
          )}
          {p1Wins && (
            <span className="mt-1 inline-flex items-center gap-1 text-xs font-semibold text-green-700">
              <Trophy className="h-3 w-3" /> Winner
            </span>
          )}
        </div>

        <div className="text-sm font-bold text-gray-400">vs</div>

        {/* Player 2 */}
        <div className={`flex-1 rounded-lg p-3 text-center ${p2Wins ? 'bg-green-50 ring-2 ring-green-400' : 'bg-gray-50'}`}>
          <p className={`font-semibold ${p2Wins ? 'text-green-800' : 'text-gray-800'}`}>
            {m.player2Name}
          </p>
          <p className="mt-0.5 text-xs text-gray-500">
            CH {m.player2CourseHandicap}
          </p>
          {m.player2NetStrokes !== null && (
            <p className="mt-1 text-lg font-bold text-gray-700">{m.player2NetStrokes}</p>
          )}
          {p2Wins && (
            <span className="mt-1 inline-flex items-center gap-1 text-xs font-semibold text-green-700">
              <Trophy className="h-3 w-3" /> Winner
            </span>
          )}
        </div>
      </div>

      <div className="mt-2 text-center text-sm">
        {halved && <span className="text-blue-600 font-medium">Halved (Tie)</span>}
        {pending && m.holeByHole.length === 0 && <span className="text-gray-400 italic text-xs">Awaiting scores</span>}
      </div>

      {m.holeByHole.length > 0 && <MatchPlayScorecard m={m} />}
    </div>
  );
}

/// Classic 1-up-style strip: one column per hole showing who won it and the
/// running match status from Player1's perspective (e.g. "2 UP", "AS", "3&2").
function MatchPlayScorecard({ m }: { m: TournamentMatchupResult }) {
  const formatStatus = (status: number | null) => {
    if (status === null) return '—';
    if (status === 0) return 'AS';
    return status > 0 ? `${status}↑` : `${Math.abs(status)}↓`;
  };

  const decidedIndex = m.holeByHole.findIndex((h) => h.isConceded);
  const closingHole = decidedIndex > 0 ? m.holeByHole[decidedIndex - 1] : null;
  const holesRemaining = closingHole ? m.holeByHole.length - decidedIndex : 0;
  const closeoutLabel =
    closingHole && closingHole.statusAfterHole !== null && closingHole.statusAfterHole !== 0
      ? `${Math.abs(closingHole.statusAfterHole)}&${holesRemaining}`
      : null;

  return (
    <div className="mt-3 overflow-x-auto border-t border-gray-100 pt-2">
      <table className="w-full min-w-max text-[11px]">
        <thead className="text-gray-400">
          <tr>
            <th className="px-1 py-0.5 text-left font-medium">Hole</th>
            {m.holeByHole.map((h) => (
              <th key={h.holeNumber} className="px-1 py-0.5 text-center font-medium">
                {h.holeNumber}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          <tr>
            <td className="px-1 py-0.5 text-left text-gray-500">Status</td>
            {m.holeByHole.map((h) => {
              const holeWinner =
                h.player1NetStrokes !== null && h.player2NetStrokes !== null
                  ? h.player1NetStrokes < h.player2NetStrokes
                    ? 'p1'
                    : h.player2NetStrokes < h.player1NetStrokes
                      ? 'p2'
                      : 'halve'
                  : null;
              return (
                <td
                  key={h.holeNumber}
                  className={`px-1 py-0.5 text-center font-semibold ${
                    h.isConceded
                      ? 'text-gray-300'
                      : holeWinner === 'p1'
                        ? 'text-green-700'
                        : holeWinner === 'p2'
                          ? 'text-blue-700'
                          : 'text-gray-500'
                  }`}
                >
                  {h.isConceded ? '·' : formatStatus(h.statusAfterHole)}
                </td>
              );
            })}
          </tr>
        </tbody>
      </table>
      {closeoutLabel && (
        <p className="mt-1 text-center text-xs font-medium text-green-700">
          Closed out {closeoutLabel}
        </p>
      )}
    </div>
  );
}

// ── Rankings ──────────────────────────────────────────────────────────────────

function RankingTable({
  title,
  entries,
  scoreLabel,
  ascending,
}: {
  title: string;
  entries: TournamentRankingEntry[];
  scoreLabel: string;
  ascending: boolean;
}) {
  return (
    <div>
      <h3 className="mb-2 text-sm font-semibold text-gray-700">{title}</h3>
      {entries.length === 0 ? (
        <p className="text-xs text-gray-400 italic">No scores yet.</p>
      ) : (
        <div className="overflow-hidden rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="px-3 py-2 text-center">#</th>
                <th className="px-3 py-2 text-left">Player</th>
                <th className="px-3 py-2 text-center">HCP</th>
                <th className="px-3 py-2 text-center">{scoreLabel}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {entries.map((e) => (
                <tr
                  key={e.playerId}
                  className={e.rank === 1 ? 'bg-amber-50' : ''}
                >
                  <td className="px-3 py-2 text-center">
                    <span
                      className={
                        e.rank === 1
                          ? 'inline-flex h-6 w-6 items-center justify-center rounded-full bg-amber-400 text-xs font-bold text-white'
                          : 'text-gray-500'
                      }
                    >
                      {e.isTied ? `T${e.rank}` : e.rank}
                    </span>
                  </td>
                  <td className="px-3 py-2 font-medium text-gray-800">{e.playerName}</td>
                  <td className="px-3 py-2 text-center text-gray-500">
                    {e.handicapIndex.toFixed(1)}
                  </td>
                  <td className="px-3 py-2 text-center font-semibold">
                    {e.score !== null
                      ? ascending
                        ? e.score
                        : `+${e.score}`
                      : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function TournamentResultsBody({ results }: { results: TournamentResults }) {
  return (
    <div className="space-y-8">
      {/* Skins — two columns */}
      <section>
        <SectionTitle icon={Trophy} label="Skins" />
        <div className="grid gap-6 lg:grid-cols-2">
          <SkinsPanel skins={results.grossSkins} />
          <SkinsPanel skins={results.netSkins} />
        </div>
      </section>

      {/* Hole Extras */}
      <section>
        <SectionTitle icon={Target} label="Closest to Pin & Longest Drive" />
        <HoleExtrasPanel extras={results.holeExtras} ldWinners={results.longestDriveWinners} />
      </section>

      {/* Flights (longest-drive grouping) */}
      {results.flights.length > 0 && (
        <section>
          <SectionTitle icon={Users} label="Flights" />
          <FlightsPanel flights={results.flights} holes={results.holes} />
        </section>
      )}

      {/* Matchups */}
      {results.matchupResults.length > 0 && (
        <section>
          <SectionTitle icon={Users} label="Matchup Results" />
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {results.matchupResults.map((m) => (
              <MatchupCard key={m.matchupNumber} m={m} />
            ))}
          </div>
        </section>
      )}

      {/* Rankings */}
      <section>
        <SectionTitle icon={BarChart2} label="Rankings" />
        <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
          <RankingTable
            title="Gross Stroke Play"
            entries={results.grossStrokeRanking}
            scoreLabel="Gross"
            ascending={true}
          />
          <RankingTable
            title="Net Stroke Play"
            entries={results.netStrokeRanking}
            scoreLabel="Net"
            ascending={true}
          />
          <RankingTable
            title="Gross Stableford"
            entries={results.grossStablefordRanking}
            scoreLabel="Pts"
            ascending={false}
          />
          <RankingTable
            title="Net Stableford"
            entries={results.netStablefordRanking}
            scoreLabel="Pts"
            ascending={false}
          />
        </div>
      </section>
    </div>
  );
}

export function TournamentResultsPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const { data: results, isLoading, error } = useTournamentResults(roundId ?? '');

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-green-600" />
      </div>
    );
  }

  if (error || !results) {
    return (
      <div className="flex h-64 flex-col items-center justify-center gap-2 text-gray-500">
        <AlertCircle className="h-8 w-8 text-red-400" />
        <p>Failed to load tournament results.</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-8 px-4 py-6">
      {/* Header */}
      <div>
        <Link
          to="/rounds"
          className="mb-2 inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Rounds
        </Link>
        <div className="mt-1 flex flex-wrap items-baseline gap-3">
          <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <Trophy className="h-6 w-6 text-amber-500" />
            Tournament Results
          </h1>
          <span className="text-gray-500">
            {results.courseName} · {formatDate(results.roundDate)}
          </span>
        </div>
      </div>

      <TournamentResultsBody results={results} />
    </div>
  );
}
