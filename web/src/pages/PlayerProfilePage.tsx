import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, Trophy, TrendingDown, TrendingUp, Target } from 'lucide-react';
import { useState } from 'react';
import { usePlayer, useHandicapHistory, usePlayerRounds } from '@/hooks/usePlayers';
import { usePlayerStatistics } from '@/hooks/useStatistics';
import { useSetTeeTimePreference } from '@/hooks/useTeeTimes';
import { useAuthStore } from '@/store/authStore';
import { TEE_TIME_SLOTS, TEE_TIME_SLOT_FLAG } from '@/types/api';
import type { TeeTimeSlotName, PlayerStatistics } from '@/types/api';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/Card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/Table';
import { SortableTableHead } from '@/components/ui/SortableTableHead';
import { useSortableTable } from '@/hooks/useSortableTable';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { HandicapChart } from '@/components/HandicapChart';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus } from '@/lib/enumUtils';

function TeeTimePreferenceSelector({ playerId, currentMask }: { playerId: number; currentMask: number }) {
  const setPreference = useSetTeeTimePreference();
  const [selected, setSelected] = useState<Set<TeeTimeSlotName>>(
    () => new Set(TEE_TIME_SLOTS.filter((s) => (currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0)),
  );

  function toggle(slot: TeeTimeSlotName) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(slot)) next.delete(slot); else next.add(slot);
      return next;
    });
  }

  function save() {
    setPreference.mutate({ playerId, preferredSlots: [...selected] });
  }

  const isDirty = TEE_TIME_SLOTS.some(
    (s) => selected.has(s) !== ((currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0),
  );

  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 space-y-3">
      <p className="text-sm font-medium text-gray-700">
        Preferred tee-time slots for auto-fill
        <span className="ml-1 text-xs font-normal text-gray-400">(select none, any, or all)</span>
      </p>
      <div className="flex flex-wrap gap-2">
        {TEE_TIME_SLOTS.map((slot) => {
          const active = selected.has(slot);
          return (
            <button
              key={slot}
              type="button"
              onClick={() => toggle(slot)}
              className={[
                'px-3 py-1 rounded-full text-sm font-medium border transition-colors',
                active
                  ? 'bg-[#1B5E20] text-white border-[#1B5E20]'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-[#1B5E20]',
              ].join(' ')}
            >
              {slot}
            </button>
          );
        })}
      </div>
      {isDirty && (
        <div className="flex items-center gap-2">
          <Button size="sm" onClick={save} disabled={setPreference.isPending}>
            Save preference
          </Button>
          {setPreference.isError && (
            <span className="text-xs text-red-600">Failed to save.</span>
          )}
          {setPreference.isSuccess && !setPreference.isPending && (
            <span className="text-xs text-green-700">Saved!</span>
          )}
        </div>
      )}
    </div>
  );
}

function ScoringDistributionBar({ stats }: { stats: PlayerStatistics }) {
  const { scoringDistribution: dist } = stats;
  const total = dist.totalHolesPlayed;
  if (total === 0) return null;

  const segments = [
    { label: 'Eagle+', count: dist.eagleOrBetterCount, color: 'bg-blue-500' },
    { label: 'Birdie', count: dist.birdieCount, color: 'bg-green-500' },
    { label: 'Par', count: dist.parCount, color: 'bg-gray-400' },
    { label: 'Bogey', count: dist.bogeyCount, color: 'bg-amber-500' },
    { label: 'Dbl+', count: dist.doubleBogeyOrWorseCount, color: 'bg-red-500' },
  ];

  return (
    <div className="space-y-2">
      <div className="flex h-6 w-full overflow-hidden rounded-full">
        {segments.map((s) =>
          s.count > 0 ? (
            <div
              key={s.label}
              className={`${s.color} transition-all`}
              style={{ width: `${(s.count / total) * 100}%` }}
              title={`${s.label}: ${s.count} (${((s.count / total) * 100).toFixed(0)}%)`}
            />
          ) : null,
        )}
      </div>
      <div className="flex flex-wrap gap-3 text-xs text-gray-500">
        {segments.map((s) => (
          <span key={s.label} className="flex items-center gap-1">
            <span className={`inline-block h-2.5 w-2.5 rounded-full ${s.color}`} />
            {s.label}: {s.count} ({total > 0 ? ((s.count / total) * 100).toFixed(0) : 0}%)
          </span>
        ))}
      </div>
    </div>
  );
}

function PlayerStatsSection({ playerId }: { playerId: string }) {
  const playerStats = usePlayerStatistics(playerId);

  if (!playerId) return null;
  if (playerStats.isPending) return null;
  if (playerStats.isError) return null;

  const stats = playerStats.data;
  if (!stats || stats.totalRoundsFinalized === 0) return null;

  const scoreToParLabel = (val: number | null) => {
    if (val == null) return '—';
    if (val > 0) return `+${val.toFixed(1)}`;
    if (val === 0) return 'E';
    return val.toFixed(1);
  };

  return (
    <section className="space-y-4">
      <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
        <Target className="h-5 w-5 text-primary-700" />
        Performance Statistics
      </h2>

      {/* Key metrics row */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Rounds Finalized
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-primary-900">{stats.totalRoundsFinalized}</p>
            <p className="text-xs text-gray-400 mt-1">of {stats.totalRoundsPlayed} total</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Avg Score to Par
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-primary-900">
              {scoreToParLabel(stats.averageScoreToPar)}
            </p>
            <p className="text-xs text-gray-400 mt-1">
              Gross: {stats.averageGrossStrokes?.toFixed(1) ?? '—'} |
              Net: {stats.averageNetStrokes?.toFixed(1) ?? '—'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Avg Net Stableford Pts
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-primary-900">
              {stats.averageNetStablefordPoints?.toFixed(1) ?? '—'}
            </p>
            <p className="text-xs text-gray-400 mt-1">
              Gross: {stats.averageGrossStablefordPoints?.toFixed(1) ?? '—'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">
              Par or Better %
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-primary-900">
              {stats.parOrBetterPercentage != null
                ? `${stats.parOrBetterPercentage}%`
                : '—'}
            </p>
            <p className="text-xs text-gray-400 mt-1">
              {stats.totalBirdiesOrBetter} birdies+ | {stats.totalPars} pars
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Best/worst and handicap trend */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-1 text-sm font-medium text-green-600">
              <TrendingDown className="h-4 w-4" />
              Best Gross Round
            </CardTitle>
          </CardHeader>
          <CardContent>
            {stats.bestGrossRound ? (
              <>
                <p className="text-2xl font-bold text-primary-900">
                  {stats.bestGrossRound.grossStrokes}
                </p>
                <Link
                  to={`/rounds/${stats.bestGrossRound.roundId}`}
                  className="text-xs text-primary-700 hover:underline"
                >
                  {stats.bestGrossRound.courseName} — {formatShortDate(stats.bestGrossRound.roundDate)}
                </Link>
              </>
            ) : (
              <p className="text-gray-400">—</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-1 text-sm font-medium text-blue-600">
              <Trophy className="h-4 w-4" />
              Best Net Points Round
            </CardTitle>
          </CardHeader>
          <CardContent>
            {stats.bestNetPointsRound ? (
              <>
                <p className="text-2xl font-bold text-primary-900">
                  {stats.bestNetPointsRound.netStablefordPoints} pts
                </p>
                <Link
                  to={`/rounds/${stats.bestNetPointsRound.roundId}`}
                  className="text-xs text-primary-700 hover:underline"
                >
                  {stats.bestNetPointsRound.courseName} — {formatShortDate(stats.bestNetPointsRound.roundDate)}
                </Link>
              </>
            ) : (
              <p className="text-gray-400">—</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-1 text-sm font-medium text-gray-500">
              {stats.handicapTrend != null && stats.handicapTrend < 0 ? (
                <TrendingDown className="h-4 w-4 text-green-600" />
              ) : (
                <TrendingUp className="h-4 w-4 text-red-600" />
              )}
              Handicap Trend
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-primary-900">
              {stats.handicapTrend != null
                ? `${stats.handicapTrend > 0 ? '+' : ''}${stats.handicapTrend.toFixed(1)}`
                : '—'}
            </p>
            <p className="text-xs text-gray-400 mt-1">change since first record</p>
          </CardContent>
        </Card>
      </div>

      {/* Stroke range */}
      {stats.bestGrossStrokes != null && stats.worstGrossStrokes != null && (
        <div className="rounded-lg border border-gray-200 bg-white px-5 py-3">
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">Gross stroke range</span>
            <span className="font-semibold tabular-nums text-primary-900">
              {stats.bestGrossStrokes} – {stats.worstGrossStrokes}
            </span>
          </div>
          {stats.bestNetStablefordPoints != null && stats.worstNetStablefordPoints != null && (
            <div className="flex items-center justify-between text-sm mt-1">
              <span className="text-gray-500">Net Stableford pts range</span>
              <span className="font-semibold tabular-nums text-primary-900">
                {stats.worstNetStablefordPoints} – {stats.bestNetStablefordPoints}
              </span>
            </div>
          )}
        </div>
      )}

      {/* Strokes Gained: Putting */}
      {stats.strokesGainedPutting && (
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-3">
            Strokes Gained: Putting (vs Flight)
          </h3>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="text-center">
              <p
                className={`text-2xl font-bold ${
                  stats.strokesGainedPutting.perHoleAverage > 0
                    ? 'text-green-600'
                    : stats.strokesGainedPutting.perHoleAverage < 0
                      ? 'text-red-600'
                      : 'text-gray-600'
                }`}
              >
                {stats.strokesGainedPutting.perHoleAverage > 0 ? '+' : ''}
                {stats.strokesGainedPutting.perHoleAverage.toFixed(3)}
              </p>
              <p className="text-xs text-gray-500 mt-1">SG per hole</p>
            </div>
            <div className="text-center">
              <p
                className={`text-2xl font-bold ${
                  stats.strokesGainedPutting.totalStrokesGained > 0
                    ? 'text-green-600'
                    : stats.strokesGainedPutting.totalStrokesGained < 0
                      ? 'text-red-600'
                      : 'text-gray-600'
                }`}
              >
                {stats.strokesGainedPutting.totalStrokesGained > 0 ? '+' : ''}
                {stats.strokesGainedPutting.totalStrokesGained.toFixed(2)}
              </p>
              <p className="text-xs text-gray-500 mt-1">Total SG Putting</p>
            </div>
            <div className="text-center">
              <p className="text-2xl font-bold text-primary-900">
                {stats.strokesGainedPutting.averagePuttsPerHole?.toFixed(2) ?? '—'}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                Avg putts/hole
                {stats.strokesGainedPutting.flightAveragePuttsPerHole != null && (
                  <span className="text-gray-400">
                    {' '}(flight: {stats.strokesGainedPutting.flightAveragePuttsPerHole.toFixed(2)})
                  </span>
                )}
              </p>
            </div>
          </div>
          <p className="text-[10px] text-gray-400 mt-3 text-center">
            Based on {stats.strokesGainedPutting.holesWithPuttData} holes with putt data recorded
          </p>
        </div>
      )}

      {/* Scoring distribution */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">
          Scoring Distribution (Gross)
        </h3>
        <ScoringDistributionBar stats={stats} />
      </div>

      {/* Per-hole averages */}
      {stats.holeAverages.length > 0 && (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="px-5 py-3 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-700">
              Hole-by-Hole Averages
            </h3>
          </div>
          <Table>
            <TableHeader>
              <TableRow className="bg-gray-50">
                <TableHead className="w-16 text-center">Hole</TableHead>
                <TableHead className="w-16 text-center">Par</TableHead>
                <TableHead className="text-right">Avg Gross</TableHead>
                <TableHead className="text-right">Avg Net</TableHead>
                <TableHead className="text-right">Avg to Par</TableHead>
                <TableHead className="text-right w-16">Played</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {stats.holeAverages.map((h) => (
                <TableRow key={h.holeNumber}>
                  <TableCell className="text-center font-semibold">{h.holeNumber}</TableCell>
                  <TableCell className="text-center">{h.par}</TableCell>
                  <TableCell className="text-right tabular-nums">
                    {h.averageGrossStrokes.toFixed(1)}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {h.averageNetStrokes.toFixed(1)}
                  </TableCell>
                  <TableCell className="text-right tabular-nums font-medium">
                    <span
                      className={
                        h.averageScoreToPar > 0
                          ? 'text-red-600'
                          : h.averageScoreToPar < 0
                            ? 'text-green-600'
                            : 'text-gray-600'
                      }
                    >
                      {scoreToParLabel(h.averageScoreToPar)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right text-gray-500 tabular-nums">
                    {h.timesPlayed}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </section>
  );
}

export function PlayerProfilePage() {
  const { playerId } = useParams<{ playerId: string }>();
  const user = useAuthStore((s) => s.user);
  const player = usePlayer(playerId ?? '');
  const handicapSort = useSortableTable('playerHandicapHistory');
  const roundsSort = useSortableTable('playerPastRounds');
  const handicapHistory = useHandicapHistory(playerId ?? '', handicapSort.sort);
  const playerRounds = usePlayerRounds(playerId ?? '', roundsSort.sort);

  const playerData = player.data;
  const isOwnProfile = user?.playerId != null && user.playerId === playerId;
  const history = handicapHistory.data ?? [];
  const rounds = playerRounds.data ?? [];

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to="/players">
          <ArrowLeft className="h-4 w-4 mr-1" />
          Players
        </Link>
      </Button>

      {player.isPending && <FullPageSpinner />}
      {player.isError && (
        <ErrorMessage message="Could not load player profile. Please try again." />
      )}

      {playerData && (
        <>
          <PageHeader
            title={playerData.fullName}
            description={playerData.flightName ?? undefined}
          >
            <Badge variant={playerData.isActive ? 'green' : 'secondary'}>
              {playerData.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </PageHeader>

          {isOwnProfile && (
            <TeeTimePreferenceSelector
              playerId={parseInt(playerId!, 10)}
              currentMask={playerData.preferredTeeTimeSlots ?? 0}
            />
          )}

          {/* Stats cards */}
          <div className="grid gap-4 sm:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  18-Hole Handicap
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold text-primary-900">
                  {playerData.currentHandicap?.toFixed(1) ?? '—'}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  9-Hole Handicap
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold text-primary-900">
                  {playerData.currentHandicap !== null && playerData.currentHandicap !== undefined
                    ? (playerData.currentHandicap / 2).toFixed(1)
                    : '—'}
                </p>
                <p className="text-xs text-gray-400 mt-1">applied for league rounds</p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  Flight
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-lg font-semibold text-gray-900">
                  {playerData.flightName ?? '—'}
                </p>
              </CardContent>
            </Card>
          </div>
        </>
      )}

      {/* Player Statistics Section */}
      <PlayerStatsSection playerId={playerId ?? ''} />

      {/* Handicap chart */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Handicap Trend
        </h2>
        {handicapHistory.isPending && <FullPageSpinner />}
        {handicapHistory.isError && (
          <ErrorMessage message="Could not load handicap history." />
        )}
        {!handicapHistory.isPending && !handicapHistory.isError && (
          <HandicapChart history={history} />
        )}
      </section>

      {/* Handicap history table */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Handicap History
        </h2>

        {history.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50">
                  <SortableTableHead column="date" sort={handicapSort.sort} onSort={handicapSort.cycle}>
                    Date
                  </SortableTableHead>
                  <SortableTableHead column="source" sort={handicapSort.sort} onSort={handicapSort.cycle}>
                    Source
                  </SortableTableHead>
                  <SortableTableHead column="index" sort={handicapSort.sort} onSort={handicapSort.cycle} className="text-right">
                    18-Hole
                  </SortableTableHead>
                  <SortableTableHead column="nineHole" sort={handicapSort.sort} onSort={handicapSort.cycle} className="text-right">
                    9-Hole
                  </SortableTableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.map((h, i) => (
                  <TableRow key={`${h.effectiveDate}-${i}`}>
                    <TableCell>{formatShortDate(h.effectiveDate)}</TableCell>
                    <TableCell className="text-gray-500">{h.source}</TableCell>
                    <TableCell className="text-right font-semibold tabular-nums">
                      {h.handicapIndex.toFixed(1)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums text-gray-600">
                      {h.nineHoleHandicapIndex.toFixed(1)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
        {!handicapHistory.isPending && history.length === 0 && (
          <p className="text-gray-500 text-sm">No handicap history recorded yet.</p>
        )}
      </section>

      {/* Past rounds */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Past Rounds
        </h2>

        {playerRounds.isPending && <FullPageSpinner />}
        {playerRounds.isError && (
          <ErrorMessage message="Could not load round history." />
        )}

        {playerRounds.data && rounds.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50">
                  <SortableTableHead column="date" sort={roundsSort.sort} onSort={roundsSort.cycle}>
                    Date
                  </SortableTableHead>
                  <SortableTableHead column="course" sort={roundsSort.sort} onSort={roundsSort.cycle}>
                    Course
                  </SortableTableHead>
                  <SortableTableHead column="week" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-center">
                    Wk
                  </SortableTableHead>
                  <SortableTableHead column="status" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-center">
                    Status
                  </SortableTableHead>
                  <SortableTableHead column="gross" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Gross
                  </SortableTableHead>
                  <SortableTableHead column="net" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Net
                  </SortableTableHead>
                  <SortableTableHead column="differential" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Diff
                  </SortableTableHead>
                  <SortableTableHead column="grossPts" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Gross Pts
                  </SortableTableHead>
                  <SortableTableHead column="netPts" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Net Pts
                  </SortableTableHead>
                  
                </TableRow>
              </TableHeader>
              <TableBody>
                {rounds.map((r) => {
                  const statusNormalized = normalizeRoundStatus(r.status);
                  const statusVariant =
                    statusNormalized === 'Finalized'
                      ? 'green'
                      : statusNormalized === 'InProgress'
                        ? 'amber'
                        : statusNormalized === 'Scheduled'
                          ? 'blue'
                          : 'secondary';
                  return (
                    <TableRow key={r.roundId}>
                      <TableCell>
                        <Link
                          to={`/rounds/${r.roundId}`}
                          className="text-primary-900 hover:underline"
                        >
                          {formatShortDate(r.roundDate)}
                        </Link>
                      </TableCell>
                      <TableCell className="text-gray-700">
                        {r.courseName}{' '}
                        <span className="text-gray-400 text-xs">({r.nineHoleSide})</span>
                      </TableCell>
                      <TableCell className="text-center text-gray-500">{r.weekNumber}</TableCell>
                      <TableCell className="text-center">
                        <Badge variant={statusVariant}>{statusNormalized}</Badge>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalGrossStrokes ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalNetStrokes ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums text-gray-600">
                        {r.scoreDifferential != null ? r.scoreDifferential.toFixed(1) : '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalGrossStablefordPoints ?? '—'}
                      </TableCell>
                      <TableCell className="text-right font-semibold tabular-nums">
                        {r.totalNetStablefordPoints ?? '—'}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
        {!playerRounds.isPending && rounds.length === 0 && (
          <p className="text-gray-500 text-sm">No rounds played yet.</p>
        )}
      </section>
    </div>
  );
}
