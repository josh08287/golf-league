import { Link } from 'react-router-dom';
import { Modal } from '@/components/admin/Modal';
import { usePlayerStatistics } from '@/hooks/useStatistics';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { ScoringDistributionBar } from '@/pages/PlayerProfilePage';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { formatShortDate } from '@/lib/utils';
import type { PlayerStatistics, BestWorstRound } from '@/types/api';

interface PlayerCompareModalProps {
  open: boolean;
  onClose: () => void;
  currentPlayerId: string;
  otherPlayerId: string;
}

function scoreToParLabel(val: number | null) {
  if (val == null) return '—';
  if (val > 0) return `+${val.toFixed(1)}`;
  if (val === 0) return 'E';
  return val.toFixed(1);
}

interface MetricRow {
  label: string;
  value: (s: PlayerStatistics) => string;
  /** Lower is better (e.g. scoring average); undefined when neither direction is "better". */
  lowerIsBetter?: boolean;
  raw: (s: PlayerStatistics) => number | null;
}

const METRICS: MetricRow[] = [
  {
    label: 'Rounds Played',
    value: (s) => `${s.totalRoundsPlayed}`,
    raw: (s) => s.totalRoundsPlayed,
  },
  {
    label: 'Rounds Finalized',
    value: (s) => `${s.totalRoundsFinalized}`,
    raw: (s) => s.totalRoundsFinalized,
  },
  {
    label: 'Avg Score to Par',
    value: (s) => scoreToParLabel(s.averageScoreToPar),
    lowerIsBetter: true,
    raw: (s) => s.averageScoreToPar,
  },
  {
    label: 'Avg Gross Strokes',
    value: (s) => s.averageGrossStrokes?.toFixed(1) ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.averageGrossStrokes,
  },
  {
    label: 'Avg Net Strokes',
    value: (s) => s.averageNetStrokes?.toFixed(1) ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.averageNetStrokes,
  },
  {
    label: 'Avg Gross Stableford Pts',
    value: (s) => s.averageGrossStablefordPoints?.toFixed(1) ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.averageGrossStablefordPoints,
  },
  {
    label: 'Avg Net Stableford Pts',
    value: (s) => s.averageNetStablefordPoints?.toFixed(1) ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.averageNetStablefordPoints,
  },
  {
    label: 'Best Gross Strokes',
    value: (s) => s.bestGrossStrokes?.toString() ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.bestGrossStrokes,
  },
  {
    label: 'Worst Gross Strokes',
    value: (s) => s.worstGrossStrokes?.toString() ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.worstGrossStrokes,
  },
  {
    label: 'Best Net Stableford Pts',
    value: (s) => s.bestNetStablefordPoints?.toString() ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.bestNetStablefordPoints,
  },
  {
    label: 'Worst Net Stableford Pts',
    value: (s) => s.worstNetStablefordPoints?.toString() ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.worstNetStablefordPoints,
  },
  {
    label: 'Handicap Trend',
    value: (s) =>
      s.handicapTrend != null ? `${s.handicapTrend > 0 ? '+' : ''}${s.handicapTrend.toFixed(1)}` : '—',
    lowerIsBetter: true,
    raw: (s) => s.handicapTrend,
  },
  {
    label: 'Birdies or Better',
    value: (s) => `${s.totalBirdiesOrBetter}`,
    lowerIsBetter: false,
    raw: (s) => s.totalBirdiesOrBetter,
  },
  {
    label: 'Pars',
    value: (s) => `${s.totalPars}`,
    lowerIsBetter: false,
    raw: (s) => s.totalPars,
  },
  {
    label: 'Par or Better %',
    value: (s) => (s.parOrBetterPercentage != null ? `${s.parOrBetterPercentage}%` : '—'),
    lowerIsBetter: false,
    raw: (s) => s.parOrBetterPercentage,
  },
  {
    label: 'Strokes Gained Putting (total)',
    value: (s) =>
      s.strokesGainedPutting
        ? `${s.strokesGainedPutting.totalStrokesGained > 0 ? '+' : ''}${s.strokesGainedPutting.totalStrokesGained.toFixed(2)}`
        : '—',
    lowerIsBetter: false,
    raw: (s) => s.strokesGainedPutting?.totalStrokesGained ?? null,
  },
  {
    label: 'Strokes Gained Putting (per hole)',
    value: (s) =>
      s.strokesGainedPutting
        ? `${s.strokesGainedPutting.perHoleAverage > 0 ? '+' : ''}${s.strokesGainedPutting.perHoleAverage.toFixed(3)}`
        : '—',
    lowerIsBetter: false,
    raw: (s) => s.strokesGainedPutting?.perHoleAverage ?? null,
  },
  {
    label: 'Avg Putts per Hole',
    value: (s) => s.strokesGainedPutting?.averagePuttsPerHole?.toFixed(2) ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.strokesGainedPutting?.averagePuttsPerHole ?? null,
  },
];

function betterSide(
  metric: MetricRow,
  currentValue: number | null,
  otherValue: number | null,
): 'current' | 'other' | null {
  if (metric.lowerIsBetter == null) return null;
  if (currentValue == null || otherValue == null || currentValue === otherValue) return null;
  const currentWins = metric.lowerIsBetter ? currentValue < otherValue : currentValue > otherValue;
  return currentWins ? 'current' : 'other';
}

function RoundLink({ round, prefix }: { round: BestWorstRound | null; prefix: string }) {
  if (!round) return <span className="text-gray-400">—</span>;
  return (
    <Link to={`${prefix}/rounds/${round.roundId}`} className="text-primary-700 hover:underline">
      {round.courseName} — {formatShortDate(round.roundDate)}
    </Link>
  );
}

function ComparePlayerHeader({ name, accent }: { name: string; accent: boolean }) {
  return (
    <th className={`py-2 text-right font-semibold ${accent ? 'text-primary-900' : 'text-gray-900'}`}>
      {name}
    </th>
  );
}

export function PlayerCompareModal({ open, onClose, currentPlayerId, otherPlayerId }: PlayerCompareModalProps) {
  const prefix = useLeaguePrefix();
  const currentStats = usePlayerStatistics(open ? currentPlayerId : '');
  const otherStats = usePlayerStatistics(open ? otherPlayerId : '');

  const isPending = currentStats.isPending || otherStats.isPending;
  const isError = currentStats.isError || otherStats.isError;

  const current = currentStats.data;
  const other = otherStats.data;
  const canCompare = current && other && current.totalRoundsFinalized > 0 && other.totalRoundsFinalized > 0;

  // Union of hole numbers either player has data for, so a hole either of
  // them hasn't played on yet still gets a row (with a "—" for the other).
  const holeNumbers = canCompare
    ? Array.from(
        new Set([
          ...current!.holeAverages.map((h) => h.holeNumber),
          ...other!.holeAverages.map((h) => h.holeNumber),
        ]),
      ).sort((a, b) => a - b)
    : [];

  return (
    <Modal open={open} onClose={onClose} title="Compare Statistics" maxWidth="xl">
      {isPending && <FullPageSpinner />}
      {isError && <ErrorMessage message="Could not load statistics for comparison. Please try again." />}

      {current && other && (
        <div className="space-y-6">
          {!canCompare ? (
            <p className="text-sm text-gray-500">
              At least one player has no finalized rounds yet, so a comparison isn&apos;t available.
            </p>
          ) : (
            <>
              {/* Core metrics */}
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-200">
                      <th className="py-2 text-left font-medium text-gray-500">Metric</th>
                      <ComparePlayerHeader name={current.playerName} accent />
                      <ComparePlayerHeader name={other.playerName} accent={false} />
                    </tr>
                  </thead>
                  <tbody>
                    {METRICS.map((metric) => {
                      const currentRaw = metric.raw(current);
                      const otherRaw = metric.raw(other);
                      const winner = betterSide(metric, currentRaw, otherRaw);

                      return (
                        <tr key={metric.label} className="border-b border-gray-100 last:border-0">
                          <td className="py-2 text-gray-500">{metric.label}</td>
                          <td
                            className={`py-2 text-right tabular-nums ${
                              winner === 'current' ? 'font-semibold text-green-600' : 'text-gray-900'
                            }`}
                          >
                            {metric.value(current)}
                          </td>
                          <td
                            className={`py-2 text-right tabular-nums ${
                              winner === 'other' ? 'font-semibold text-green-600' : 'text-gray-900'
                            }`}
                          >
                            {metric.value(other)}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Best rounds */}
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-200">
                      <th className="py-2 text-left font-medium text-gray-500">Round</th>
                      <ComparePlayerHeader name={current.playerName} accent />
                      <ComparePlayerHeader name={other.playerName} accent={false} />
                    </tr>
                  </thead>
                  <tbody>
                    <tr className="border-b border-gray-100">
                      <td className="py-2 text-gray-500">Best Gross Round</td>
                      <td className="py-2 text-right"><RoundLink round={current.bestGrossRound} prefix={prefix} /></td>
                      <td className="py-2 text-right"><RoundLink round={other.bestGrossRound} prefix={prefix} /></td>
                    </tr>
                    <tr>
                      <td className="py-2 text-gray-500">Best Net Points Round</td>
                      <td className="py-2 text-right"><RoundLink round={current.bestNetPointsRound} prefix={prefix} /></td>
                      <td className="py-2 text-right"><RoundLink round={other.bestNetPointsRound} prefix={prefix} /></td>
                    </tr>
                  </tbody>
                </table>
              </div>

              {/* Scoring distribution, side by side */}
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <h3 className="mb-2 text-sm font-semibold text-gray-700">{current.playerName} — Scoring Distribution</h3>
                  <ScoringDistributionBar stats={current} />
                </div>
                <div>
                  <h3 className="mb-2 text-sm font-semibold text-gray-700">{other.playerName} — Scoring Distribution</h3>
                  <ScoringDistributionBar stats={other} />
                </div>
              </div>

              {/* Per-hole averages */}
              {holeNumbers.length > 0 && (
                <div className="overflow-x-auto rounded-lg border border-gray-200">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-gray-200 bg-gray-50">
                        <th className="py-2 px-3 text-center font-medium text-gray-500">Hole</th>
                        <th className="py-2 px-3 text-center font-medium text-gray-500">Par</th>
                        <th className="py-2 px-3 text-right font-medium text-primary-900">
                          {current.playerName} Avg
                        </th>
                        <th className="py-2 px-3 text-right font-medium text-gray-900">
                          {other.playerName} Avg
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {holeNumbers.map((holeNumber) => {
                        const currentHole = current.holeAverages.find((h) => h.holeNumber === holeNumber);
                        const otherHole = other.holeAverages.find((h) => h.holeNumber === holeNumber);
                        const par = currentHole?.par ?? otherHole?.par;
                        const currentAvg = currentHole?.averageGrossStrokes ?? null;
                        const otherAvg = otherHole?.averageGrossStrokes ?? null;
                        const winner =
                          currentAvg != null && otherAvg != null && currentAvg !== otherAvg
                            ? currentAvg < otherAvg
                              ? 'current'
                              : 'other'
                            : null;

                        return (
                          <tr key={holeNumber} className="border-b border-gray-100 last:border-0">
                            <td className="py-1.5 px-3 text-center font-semibold">{holeNumber}</td>
                            <td className="py-1.5 px-3 text-center text-gray-500">{par ?? '—'}</td>
                            <td
                              className={`py-1.5 px-3 text-right tabular-nums ${
                                winner === 'current' ? 'font-semibold text-green-600' : 'text-gray-900'
                              }`}
                            >
                              {currentAvg?.toFixed(1) ?? '—'}
                            </td>
                            <td
                              className={`py-1.5 px-3 text-right tabular-nums ${
                                winner === 'other' ? 'font-semibold text-green-600' : 'text-gray-900'
                              }`}
                            >
                              {otherAvg?.toFixed(1) ?? '—'}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </Modal>
  );
}
