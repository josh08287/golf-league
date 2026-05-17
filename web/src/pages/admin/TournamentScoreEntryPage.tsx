import { useState, useEffect } from 'react';
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
  Save,
  X,
} from 'lucide-react';
import { useTournamentResults } from '@/hooks/useRounds';
import { useSaveTournamentExtras, useSetLongestDriveWinners } from '@/hooks/admin/useRoundMutations';
import { useCourseDetail } from '@/hooks/admin/useCourseMutations';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { formatDate } from '@/lib/utils';
import { Button } from '@/components/ui/Button';
import type {
  TournamentSkinsResult,
  TournamentSkinHole,
  TournamentMatchupResult,
  TournamentRankingEntry,
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

// ── Skins (read-only) ─────────────────────────────────────────────────────────

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
            <div className="mt-3 flex flex-wrap gap-2">
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
          )}
        </>
      )}
    </div>
  );
}

// ── Matchups (read-only) ──────────────────────────────────────────────────────

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
        <div className={`flex-1 rounded-lg p-3 text-center ${p1Wins ? 'bg-green-50 ring-2 ring-green-400' : 'bg-gray-50'}`}>
          <p className={`font-semibold ${p1Wins ? 'text-green-800' : 'text-gray-800'}`}>{m.player1Name}</p>
          <p className="mt-0.5 text-xs text-gray-500">
            HCP {m.player1HandicapIndex.toFixed(1)} / CH {m.player1CourseHandicap}
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
        <div className={`flex-1 rounded-lg p-3 text-center ${p2Wins ? 'bg-green-50 ring-2 ring-green-400' : 'bg-gray-50'}`}>
          <p className={`font-semibold ${p2Wins ? 'text-green-800' : 'text-gray-800'}`}>{m.player2Name}</p>
          <p className="mt-0.5 text-xs text-gray-500">
            HCP {m.player2HandicapIndex.toFixed(1)} / CH {m.player2CourseHandicap}
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
        {pending && <span className="text-gray-400 italic text-xs">Awaiting scores</span>}
      </div>
    </div>
  );
}

// ── Rankings (read-only) ──────────────────────────────────────────────────────

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
                <tr key={e.playerId} className={e.rank === 1 ? 'bg-amber-50' : ''}>
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
                  <td className="px-3 py-2 text-center text-gray-500">{e.handicapIndex.toFixed(1)}</td>
                  <td className="px-3 py-2 text-center font-semibold">
                    {e.score !== null ? (ascending ? e.score : `+${e.score}`) : '—'}
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

// ── Prop Awards Editor ────────────────────────────────────────────────────────

function PropAwardsEditor({
  results,
  roundId,
}: {
  results: TournamentResults;
  roundId: string;
}) {
  // All players who have scores — derived from ranking lists
  const playerSet = new Map<number, string>();
  for (const entry of [...results.grossStrokeRanking, ...results.netStrokeRanking]) {
    playerSet.set(entry.playerId, entry.playerName);
  }
  const players = Array.from(playerSet.entries())
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name));

  // ── CTP state (per par-3 hole from course) ────────────────────────────────
  const { data: course } = useCourseDetail(results.courseId);
  const par3Holes = (course?.holeDetails ?? [])
    .filter((h) => h.par === 3)
    .sort((a, b) => a.holeNumber - b.holeNumber);

  const [ctpMap, setCtpMap] = useState<Record<number, number | null>>({});
  const [ctpDirty, setCtpDirty] = useState(false);
  const [ctpError, setCtpError] = useState('');
  const saveCtp = useSaveTournamentExtras(roundId);

  // Initialise CTP from existing holeExtras
  useEffect(() => {
    const init: Record<number, number | null> = {};
    for (const e of results.holeExtras) {
      init[e.holeNumber] = e.closestToPinPlayerId;
    }
    setCtpMap(init);
    setCtpDirty(false);
  }, [results.holeExtras]);

  function setCtpPlayer(holeNumber: number, playerId: number | null) {
    setCtpMap((prev) => ({ ...prev, [holeNumber]: playerId }));
    setCtpDirty(true);
    setCtpError('');
  }

  async function saveCtp_() {
    setCtpError('');
    const holeExtras = par3Holes
      .map((h) => ({
        holeNumber: h.holeNumber,
        closestToPinPlayerId: ctpMap[h.holeNumber] ?? null,
        longestDrivePlayerId: null, // LD handled separately
      }))
      .filter((e) => e.closestToPinPlayerId !== null);
    try {
      await saveCtp.mutateAsync(holeExtras);
      setCtpDirty(false);
    } catch {
      setCtpError('Failed to save. Please try again.');
    }
  }

  // ── LD state (multi-player, round-level) ──────────────────────────────────
  const [ldPlayerIds, setLdPlayerIds] = useState<number[]>([]);
  const [ldDirty, setLdDirty] = useState(false);
  const [ldError, setLdError] = useState('');
  const [ldPickerId, setLdPickerId] = useState<number | ''>('');
  const saveLd = useSetLongestDriveWinners(roundId);

  useEffect(() => {
    setLdPlayerIds(results.longestDriveWinners.map((w) => w.playerId));
    setLdDirty(false);
  }, [results.longestDriveWinners]);

  function addLdPlayer() {
    if (ldPickerId === '' || ldPlayerIds.includes(Number(ldPickerId))) return;
    setLdPlayerIds((prev) => [...prev, Number(ldPickerId)]);
    setLdPickerId('');
    setLdDirty(true);
    setLdError('');
  }

  function removeLdPlayer(playerId: number) {
    setLdPlayerIds((prev) => prev.filter((id) => id !== playerId));
    setLdDirty(true);
    setLdError('');
  }

  async function saveLd_() {
    setLdError('');
    try {
      await saveLd.mutateAsync(ldPlayerIds);
      setLdDirty(false);
    } catch {
      setLdError('Failed to save. Please try again.');
    }
  }

  const ldPlayerMap = new Map(players.map((p) => [p.id, p.name]));
  const ldAvailable = players.filter((p) => !ldPlayerIds.includes(p.id));

  const noScores = players.length === 0;

  return (
    <div className="grid gap-8 lg:grid-cols-2">
      {/* Closest to Pin */}
      <div className="space-y-3">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-gray-700">
          <Target className="h-4 w-4 text-green-600" />
          Closest to the Pin (Par 3s)
        </h3>

        {noScores ? (
          <p className="text-sm text-gray-400 italic">
            No scores yet — enter scores first.
          </p>
        ) : par3Holes.length === 0 && !course ? (
          <p className="text-sm text-gray-400 italic">Loading course holes…</p>
        ) : par3Holes.length === 0 ? (
          <p className="text-sm text-gray-400 italic">No par-3 holes configured for this course.</p>
        ) : (
          <>
            <div className="overflow-hidden rounded-lg border border-gray-200">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
                  <tr>
                    <th className="px-4 py-2 text-left w-20">Hole</th>
                    <th className="px-4 py-2 text-left">Winner</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {par3Holes.map((h) => (
                    <tr key={h.holeNumber}>
                      <td className="px-4 py-2 font-mono text-gray-500">#{h.holeNumber}</td>
                      <td className="px-4 py-2">
                        <select
                          value={ctpMap[h.holeNumber] ?? ''}
                          onChange={(e) =>
                            setCtpPlayer(h.holeNumber, e.target.value === '' ? null : Number(e.target.value))
                          }
                          className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-green-600"
                        >
                          <option value="">— none —</option>
                          {players.map((p) => (
                            <option key={p.id} value={p.id}>{p.name}</option>
                          ))}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center gap-3">
              <Button
                variant="primary"
                onClick={() => void saveCtp_()}
                disabled={!ctpDirty || saveCtp.isPending}
              >
                <Save className="mr-1.5 h-4 w-4" />
                {saveCtp.isPending ? 'Saving…' : 'Save CTP'}
              </Button>
              {saveCtp.isSuccess && !ctpDirty && (
                <span className="text-sm text-green-700">Saved.</span>
              )}
              {ctpError && <span className="text-sm text-red-600">{ctpError}</span>}
            </div>
          </>
        )}
      </div>

      {/* Longest Drive */}
      <div className="space-y-3">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-gray-700">
          <Zap className="h-4 w-4 text-amber-500" />
          Longest Drive
        </h3>

        {noScores ? (
          <p className="text-sm text-gray-400 italic">
            No scores yet — enter scores first.
          </p>
        ) : (
          <>
            {/* Current winners as removable tags */}
            <div className="min-h-[2.5rem] flex flex-wrap gap-2">
              {ldPlayerIds.length === 0 && (
                <span className="text-sm text-gray-400 italic">No winners recorded yet.</span>
              )}
              {ldPlayerIds.map((pid) => (
                <span
                  key={pid}
                  className="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-sm font-medium text-amber-800"
                >
                  {ldPlayerMap.get(pid) ?? `Player ${pid}`}
                  <button
                    onClick={() => removeLdPlayer(pid)}
                    className="ml-0.5 rounded-full p-0.5 hover:bg-amber-200 transition-colors"
                    aria-label="Remove"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </span>
              ))}
            </div>

            {/* Add player picker */}
            {ldAvailable.length > 0 && (
              <div className="flex items-center gap-2">
                <select
                  value={ldPickerId}
                  onChange={(e) => setLdPickerId(e.target.value === '' ? '' : Number(e.target.value))}
                  className="flex-1 rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-green-600"
                >
                  <option value="">Add a player…</option>
                  {ldAvailable.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </select>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={ldPickerId === ''}
                  onClick={addLdPlayer}
                >
                  Add
                </Button>
              </div>
            )}

            <div className="flex items-center gap-3">
              <Button
                variant="primary"
                onClick={() => void saveLd_()}
                disabled={!ldDirty || saveLd.isPending}
              >
                <Save className="mr-1.5 h-4 w-4" />
                {saveLd.isPending ? 'Saving…' : 'Save Longest Drive'}
              </Button>
              {saveLd.isSuccess && !ldDirty && (
                <span className="text-sm text-green-700">Saved.</span>
              )}
              {ldError && <span className="text-sm text-red-600">{ldError}</span>}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function TournamentScoreEntryPage() {
  const { id } = useParams<{ id: string }>();
  const prefix = useLeaguePrefix();
  const { data: results, isLoading, error } = useTournamentResults(id ?? '');

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
    <div className="space-y-8">
      {/* Header */}
      <div>
        <Link
          to={`${prefix}/admin/rounds`}
          className="mb-2 inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Rounds
        </Link>
        <div className="mt-1 flex flex-wrap items-baseline gap-3">
          <h1 className="flex items-center gap-2 text-2xl font-bold text-gray-900">
            <Trophy className="h-6 w-6 text-amber-500" />
            Tournament Scores
          </h1>
          <span className="text-gray-500">
            {results.courseName} · {formatDate(results.roundDate)}
          </span>
        </div>
      </div>

      {/* Prop Awards — editable, shown first */}
      <section>
        <SectionTitle icon={Target} label="Prop Awards" />
        <PropAwardsEditor results={results} roundId={id ?? ''} />
      </section>

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

      {/* Skins */}
      <section>
        <SectionTitle icon={Trophy} label="Skins" />
        <div className="grid gap-6 lg:grid-cols-2">
          <SkinsPanel skins={results.grossSkins} />
          <SkinsPanel skins={results.netSkins} />
        </div>
      </section>
    </div>
  );
}
