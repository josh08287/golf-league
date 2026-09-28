import { useState, useEffect, useRef, useCallback, Fragment } from 'react';
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
  ClipboardList,
} from 'lucide-react';
import { useTournamentResults, useRound } from '@/hooks/useRounds';
import {
  useSaveTournamentExtras,
  useSubmitHoleScores,
  useSetTournamentLongestDriveWinner,
} from '@/hooks/admin/useRoundMutations';
import { useCourseDetail } from '@/hooks/admin/useCourseMutations';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { formatDate } from '@/lib/utils';
import { isRoundFinalized } from '@/lib/enumUtils';
import { Button } from '@/components/ui/Button';
import type {
  TournamentSkinsResult,
  TournamentSkinHole,
  TournamentMatchupResult,
  TournamentRankingEntry,
  TournamentResults,
  TournamentFlight,
  TournamentCourseHole,
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
  const isBye = m.player2Id === null;
  const halved = m.isHalved;
  const p1Wins = m.winnerPlayerId === m.player1Id;
  const p2Wins = !isBye && m.winnerPlayerId === m.player2Id;
  const pending = !isBye && m.winnerPlayerId === null && !halved;

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
          {p1Wins && !isBye && (
            <span className="mt-1 inline-flex items-center gap-1 text-xs font-semibold text-green-700">
              <Trophy className="h-3 w-3" /> Winner
            </span>
          )}
        </div>
        {isBye ? (
          <div className="flex-1 rounded-lg border border-dashed border-gray-200 p-3 text-center">
            <p className="text-sm font-medium text-gray-400 italic">Bye — no opponent</p>
          </div>
        ) : (
          <>
            <div className="text-sm font-bold text-gray-400">vs</div>
            <div className={`flex-1 rounded-lg p-3 text-center ${p2Wins ? 'bg-green-50 ring-2 ring-green-400' : 'bg-gray-50'}`}>
              <p className={`font-semibold ${p2Wins ? 'text-green-800' : 'text-gray-800'}`}>{m.player2Name}</p>
              <p className="mt-0.5 text-xs text-gray-500">
                HCP {m.player2HandicapIndex?.toFixed(1)} / CH {m.player2CourseHandicap}
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
          </>
        )}
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

// ── Score Entry (admin review/edit of every player's scorecard) ─────────────────

function handicapStrokesForHoleTournament(courseHandicap: number, strokeIndex: number): number {
  const base = Math.floor(courseHandicap / 18);
  const extra = courseHandicap % 18;
  return base + (strokeIndex <= extra ? 1 : 0);
}

function clampNetScore(gross: number, par: number, handicapStrokes: number) {
  const maxGross = par + 2 + handicapStrokes;
  return Math.min(gross, maxGross);
}

function netStableford(gross: number, par: number, handicapStrokes: number): number {
  const cappedGross = clampNetScore(gross, par, handicapStrokes);
  const net = cappedGross - handicapStrokes;
  return Math.max(0, Math.min(6, par + 2 - net));
}

function grossStableford(gross: number, par: number): number {
  return Math.max(0, Math.min(6, par + 2 - gross));
}

type ScoreGrid = Record<number, Record<number, number | ''>>;

interface ScoreCellProps {
  value: number | '';
  onChange: (value: number | '') => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLInputElement>) => void;
  inputRef: (el: HTMLInputElement | null) => void;
  readonly: boolean;
}

function ScoreCell({ value, onChange, onKeyDown, inputRef, readonly }: ScoreCellProps) {
  return (
    <input
      ref={inputRef}
      type="number"
      min={1}
      value={value === '' ? '' : value}
      readOnly={readonly}
      onChange={(e) => {
        const v = e.target.value;
        if (v === '') {
          onChange('');
        } else {
          const n = parseInt(v, 10);
          if (!isNaN(n) && n >= 1) onChange(n);
        }
      }}
      onKeyDown={onKeyDown}
      className={[
        'h-9 w-14 rounded border text-center text-sm transition-colors',
        readonly
          ? 'cursor-default bg-gray-50 text-gray-400'
          : 'border-gray-300 bg-white focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]',
      ].join(' ')}
    />
  );
}

function FlightScoreEntryTable({
  flight,
  holes,
  scores,
  onScoreChange,
  readonly,
  cellRefs,
}: {
  flight: TournamentFlight;
  holes: TournamentCourseHole[];
  scores: ScoreGrid;
  onScoreChange: (playerId: number, hole: number, value: number | '') => void;
  readonly: boolean;
  cellRefs: React.MutableRefObject<Record<number, Array<HTMLInputElement | null>>>;
}) {
  const pars: Record<number, number> = {};
  const strokeIndexes: Record<number, number> = {};
  for (const h of holes) {
    pars[h.holeNumber] = h.par;
    strokeIndexes[h.holeNumber] = h.strokeIndex;
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>, playerId: number, holeIdx: number, playerIdx: number) {
    if (e.key !== 'Tab') return;
    e.preventDefault();
    const nextHole = holeIdx + 1;
    if (nextHole < holes.length) {
      cellRefs.current[playerId]?.[nextHole]?.focus();
    } else {
      const nextPlayer = flight.players[playerIdx + 1];
      if (nextPlayer) cellRefs.current[nextPlayer.playerId]?.[0]?.focus();
    }
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
      <table className="min-w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-gray-200 bg-gray-50">
            <th className="sticky left-0 z-10 bg-gray-50 px-3 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
              Player
            </th>
            {holes.map((h) => (
              <th key={h.holeNumber} className="px-1 py-3 text-center text-xs font-semibold text-gray-500">
                {h.holeNumber}
              </th>
            ))}
            <th className="px-3 py-3 text-center text-xs font-semibold text-gray-500">Total</th>
          </tr>
          <tr className="border-b border-gray-100 bg-gray-50/50">
            <td className="sticky left-0 z-10 bg-gray-50/50 px-3 py-1.5 text-xs font-medium text-gray-400">Par</td>
            {holes.map((h) => (
              <td key={h.holeNumber} className="px-1 py-1.5 text-center text-xs text-gray-400">
                {h.par}
              </td>
            ))}
            <td className="px-3 py-1.5 text-center text-xs text-gray-400">
              {holes.reduce((sum, h) => sum + h.par, 0)}
            </td>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {flight.players.map((player, pi) => {
            const playerScores = scores[player.playerId] ?? {};
            const grossTotal = holes.reduce<number>((sum, h) => {
              const v = playerScores[h.holeNumber];
              return sum + (typeof v === 'number' ? v : 0);
            }, 0);

            return (
              <Fragment key={player.playerId}>
                <tr className="hover:bg-gray-50/50">
                  <td className="sticky left-0 z-10 bg-white px-3 py-2">
                    <div className="font-medium text-gray-900">{player.playerName}</div>
                    <div className="text-xs text-gray-400">CH {player.courseHandicap}</div>
                  </td>
                  {holes.map((h, hi) => (
                    <td key={h.holeNumber} className="px-1 py-2">
                      <ScoreCell
                        value={playerScores[h.holeNumber] ?? ''}
                        readonly={readonly}
                        onChange={(v) => onScoreChange(player.playerId, h.holeNumber, v)}
                        onKeyDown={(e) => handleKeyDown(e, player.playerId, hi, pi)}
                        inputRef={(el) => {
                          if (!cellRefs.current[player.playerId]) cellRefs.current[player.playerId] = [];
                          cellRefs.current[player.playerId][hi] = el;
                        }}
                      />
                    </td>
                  ))}
                  <td className="px-3 py-2 text-center font-semibold text-gray-700">
                    {grossTotal > 0 ? grossTotal : '—'}
                  </td>
                </tr>
                <tr className="bg-green-50 text-[#1B5E20]">
                  <td className="sticky left-0 z-10 px-3 py-1.5 text-xs font-medium" style={{ background: 'inherit' }}>
                    Net Stableford
                  </td>
                  {holes.map((h) => {
                    const gross = playerScores[h.holeNumber];
                    const points =
                      gross === '' || gross === undefined
                        ? null
                        : netStableford(
                            gross,
                            pars[h.holeNumber] ?? 4,
                            handicapStrokesForHoleTournament(player.courseHandicap, strokeIndexes[h.holeNumber] ?? h.holeNumber),
                          );
                    return (
                      <td key={h.holeNumber} className="px-1 py-1.5 text-center text-xs font-semibold">
                        {points ?? '—'}
                      </td>
                    );
                  })}
                  <td className="px-3 py-1.5 text-center text-xs font-bold">
                    {holes.reduce<number>((sum, h) => {
                      const gross = playerScores[h.holeNumber];
                      if (gross === '' || gross === undefined) return sum;
                      return (
                        sum +
                        netStableford(
                          gross,
                          pars[h.holeNumber] ?? 4,
                          handicapStrokesForHoleTournament(player.courseHandicap, strokeIndexes[h.holeNumber] ?? h.holeNumber),
                        )
                      );
                    }, 0)}
                  </td>
                </tr>
                <tr className="bg-blue-50/60 text-blue-800">
                  <td className="sticky left-0 z-10 px-3 py-1.5 text-xs font-medium" style={{ background: 'inherit' }}>
                    Gross Stableford
                  </td>
                  {holes.map((h) => {
                    const gross = playerScores[h.holeNumber];
                    const points = gross === '' || gross === undefined ? null : grossStableford(gross, pars[h.holeNumber] ?? 4);
                    return (
                      <td key={h.holeNumber} className="px-1 py-1.5 text-center text-xs font-semibold">
                        {points ?? '—'}
                      </td>
                    );
                  })}
                  <td className="px-3 py-1.5 text-center text-xs font-bold">
                    {holes.reduce<number>((sum, h) => {
                      const gross = playerScores[h.holeNumber];
                      if (gross === '' || gross === undefined) return sum;
                      return sum + grossStableford(gross, pars[h.holeNumber] ?? 4);
                    }, 0)}
                  </td>
                </tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function LongestDriveFlightSelector({
  flight,
  currentWinnerId,
  roundId,
}: {
  flight: TournamentFlight;
  currentWinnerId: number | null;
  roundId: string;
}) {
  const setWinner = useSetTournamentLongestDriveWinner(roundId);
  const [localValue, setLocalValue] = useState<number | null>(currentWinnerId);

  useEffect(() => {
    setLocalValue(currentWinnerId);
  }, [currentWinnerId]);

  function handleChange(value: string) {
    const winnerPlayerId = value === '' ? null : Number(value);
    setLocalValue(winnerPlayerId);
    setWinner.mutate({ tournamentFlightId: flight.id, winnerPlayerId });
  }

  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-gray-200 px-3 py-2">
      <span className="text-sm font-medium text-gray-700">Flight {flight.name}</span>
      <select
        value={localValue ?? ''}
        onChange={(e) => handleChange(e.target.value)}
        disabled={setWinner.isPending}
        className="w-56 rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 disabled:opacity-50"
      >
        <option value="">— none —</option>
        {flight.players.map((p) => (
          <option key={p.playerId} value={p.playerId}>{p.playerName}</option>
        ))}
      </select>
    </div>
  );
}

function ScoreEntrySection({
  results,
  roundId,
  isFinalized,
}: {
  results: TournamentResults;
  roundId: string;
  isFinalized: boolean;
}) {
  const [scores, setScores] = useState<ScoreGrid>({});
  const [dirtyPlayerIds, setDirtyPlayerIds] = useState<Set<number>>(new Set());
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const submitScores = useSubmitHoleScores(roundId);
  const cellRefs = useRef<Record<number, Array<HTMLInputElement | null>>>({});

  // Seed the grid from server-side flight data. Only fills blank cells so it
  // never clobbers an admin's in-progress, unsaved edits.
  useEffect(() => {
    setScores((prev) => {
      const next = { ...prev };
      let changed = false;
      for (const flight of results.flights) {
        for (const player of flight.players) {
          const existing = next[player.playerId] ?? {};
          const merged: Record<number, number | ''> = { ...existing };
          for (const h of player.holeScores) {
            if (merged[h.holeNumber] === undefined || merged[h.holeNumber] === '') {
              if (h.grossStrokes !== null) {
                merged[h.holeNumber] = h.grossStrokes;
                changed = true;
              }
            }
          }
          next[player.playerId] = merged;
        }
      }
      return changed ? next : prev;
    });
  }, [results.flights]);

  const handleScoreChange = useCallback((playerId: number, hole: number, value: number | '') => {
    setScores((prev) => ({
      ...prev,
      [playerId]: { ...(prev[playerId] ?? {}), [hole]: value },
    }));
    setDirtyPlayerIds((prev) => new Set(prev).add(playerId));
    setSaveSuccess(false);
  }, []);

  async function handleSaveAll() {
    setSaving(true);
    setSaveError(null);
    try {
      const submissions = Array.from(dirtyPlayerIds)
        .map((playerId) => {
          const playerScores = scores[playerId] ?? {};
          const enteredHoles = results.holes.filter((h) => {
            const v = playerScores[h.holeNumber];
            return v !== '' && v !== undefined;
          });
          return { playerId, enteredHoles };
        })
        .filter(({ enteredHoles }) => enteredHoles.length > 0);

      await Promise.all(
        submissions.map(({ playerId, enteredHoles }) =>
          submitScores.mutateAsync({
            playerId,
            scores: enteredHoles.map((h) => ({
              holeNumber: h.holeNumber,
              grossScore: scores[playerId][h.holeNumber] as number,
            })),
          }),
        ),
      );

      setDirtyPlayerIds(new Set());
      setSaveSuccess(true);
    } catch {
      setSaveError('Failed to save one or more scorecards. Please try again.');
    } finally {
      setSaving(false);
    }
  }

  if (results.flights.length === 0) {
    return <p className="text-sm text-gray-400 italic">No players in this round yet.</p>;
  }

  return (
    <div className="space-y-6">
      {isFinalized && (
        <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 text-sm text-gray-600">
          This round is finalized — scores are locked and shown read-only below.
        </div>
      )}
      {results.flights.map((flight) => (
        <div key={flight.id} className="space-y-2">
          <h3 className="text-base font-semibold text-gray-700">
            {flight.name === 'Substitutes' ? 'Substitutes' : `Flight ${flight.name}`}
          </h3>
          <FlightScoreEntryTable
            flight={flight}
            holes={results.holes}
            scores={scores}
            onScoreChange={handleScoreChange}
            readonly={isFinalized}
            cellRefs={cellRefs}
          />
        </div>
      ))}

      {!isFinalized && (
        <div className="flex items-center justify-end gap-3">
          {saveSuccess && !dirtyPlayerIds.size && (
            <span className="text-sm text-green-700">Saved.</span>
          )}
          {saveError && <span className="text-sm text-red-600">{saveError}</span>}
          <Button variant="primary" onClick={() => void handleSaveAll()} disabled={saving || dirtyPlayerIds.size === 0}>
            <Save className="mr-1.5 h-4 w-4" />
            {saving ? 'Saving…' : 'Save Scores'}
          </Button>
        </div>
      )}
    </div>
  );
}

// ── Prop Awards Editor ────────────────────────────────────────────────────────

function PropAwardsEditor({
  results,
  roundId,
  isFinalized,
}: {
  results: TournamentResults;
  roundId: string;
  isFinalized: boolean;
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
                          disabled={isFinalized}
                          className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 disabled:opacity-50"
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
                disabled={!ctpDirty || saveCtp.isPending || isFinalized}
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

      {/* Longest Drive — players can also record this live from their
          tee-time score entry page; the admin can review and correct any
          flight's winner here before finalizing. */}
      <div className="space-y-3">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-gray-700">
          <Zap className="h-4 w-4 text-amber-500" />
          Longest Drive
        </h3>
        {results.longestDriveHoleNumber === null ? (
          <p className="text-sm text-gray-400 italic">
            No longest-drive hole configured for this round.
          </p>
        ) : results.flights.filter((f) => f.name !== 'Substitutes').length === 0 ? (
          <p className="text-sm text-gray-400 italic">No flights to record longest drive for.</p>
        ) : isFinalized ? (
          <ul className="space-y-1.5">
            {results.longestDriveWinners.map((w) => (
              <li
                key={w.tournamentFlightId}
                className="flex items-center justify-between rounded-md border border-amber-200 bg-amber-50 px-3 py-1.5 text-sm"
              >
                <span className="font-medium text-gray-700">Flight {w.flightName}</span>
                <span className="text-amber-800">{w.playerName ?? '—'}</span>
              </li>
            ))}
          </ul>
        ) : (
          <div className="space-y-2">
            {results.flights
              .filter((f) => f.name !== 'Substitutes')
              .map((f) => (
                <LongestDriveFlightSelector
                  key={f.id}
                  flight={f}
                  currentWinnerId={
                    results.longestDriveWinners.find((w) => w.tournamentFlightId === f.id)?.playerId ?? null
                  }
                  roundId={roundId}
                />
              ))}
          </div>
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
  const { data: round } = useRound(id ?? '');
  const isFinalized = isRoundFinalized(round?.status);

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

      {/* Score Entry — admin review/edit of every player's scorecard,
          shown first so scores exist before prop awards depend on them. */}
      <section>
        <SectionTitle icon={ClipboardList} label="Score Entry" />
        <ScoreEntrySection results={results} roundId={id ?? ''} isFinalized={isFinalized} />
      </section>

      {/* Prop Awards — editable, until the round is finalized */}
      <section>
        <SectionTitle icon={Target} label="Prop Awards" />
        <PropAwardsEditor results={results} roundId={id ?? ''} isFinalized={isFinalized} />
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
