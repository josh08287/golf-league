import { Modal } from '@/components/admin/Modal';
import { usePlayerStatistics } from '@/hooks/useStatistics';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import type { PlayerStatistics } from '@/types/api';

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
  /** Lower is better (e.g. scoring average); null when neither direction is "better". */
  lowerIsBetter?: boolean;
  raw: (s: PlayerStatistics) => number | null;
}

const METRICS: MetricRow[] = [
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
    label: 'Avg Net Stableford Pts',
    value: (s) => s.averageNetStablefordPoints?.toFixed(1) ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.averageNetStablefordPoints,
  },
  {
    label: 'Par or Better %',
    value: (s) => (s.parOrBetterPercentage != null ? `${s.parOrBetterPercentage}%` : '—'),
    lowerIsBetter: false,
    raw: (s) => s.parOrBetterPercentage,
  },
  {
    label: 'Best Gross Round',
    value: (s) => s.bestGrossStrokes?.toString() ?? '—',
    lowerIsBetter: true,
    raw: (s) => s.bestGrossStrokes,
  },
  {
    label: 'Best Net Stableford Pts',
    value: (s) => s.bestNetStablefordPoints?.toString() ?? '—',
    lowerIsBetter: false,
    raw: (s) => s.bestNetStablefordPoints,
  },
  {
    label: 'Handicap Trend',
    value: (s) =>
      s.handicapTrend != null ? `${s.handicapTrend > 0 ? '+' : ''}${s.handicapTrend.toFixed(1)}` : '—',
    lowerIsBetter: true,
    raw: (s) => s.handicapTrend,
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

export function PlayerCompareModal({ open, onClose, currentPlayerId, otherPlayerId }: PlayerCompareModalProps) {
  const currentStats = usePlayerStatistics(open ? currentPlayerId : '');
  const otherStats = usePlayerStatistics(open ? otherPlayerId : '');

  const isPending = currentStats.isPending || otherStats.isPending;
  const isError = currentStats.isError || otherStats.isError;

  return (
    <Modal open={open} onClose={onClose} title="Compare Statistics" maxWidth="lg">
      {isPending && <FullPageSpinner />}
      {isError && <ErrorMessage message="Could not load statistics for comparison. Please try again." />}

      {currentStats.data && otherStats.data && (
        <div className="overflow-x-auto">
          {currentStats.data.totalRoundsFinalized === 0 || otherStats.data.totalRoundsFinalized === 0 ? (
            <p className="text-sm text-gray-500">
              At least one player has no finalized rounds yet, so a comparison isn&apos;t available.
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="py-2 text-left font-medium text-gray-500">Metric</th>
                  <th className="py-2 text-right font-semibold text-primary-900">
                    {currentStats.data.playerName}
                  </th>
                  <th className="py-2 text-right font-semibold text-gray-900">
                    {otherStats.data.playerName}
                  </th>
                </tr>
              </thead>
              <tbody>
                {METRICS.map((metric) => {
                  const currentRaw = metric.raw(currentStats.data!);
                  const otherRaw = metric.raw(otherStats.data!);
                  const winner = betterSide(metric, currentRaw, otherRaw);

                  return (
                    <tr key={metric.label} className="border-b border-gray-100 last:border-0">
                      <td className="py-2 text-gray-500">{metric.label}</td>
                      <td
                        className={`py-2 text-right tabular-nums ${
                          winner === 'current' ? 'font-semibold text-green-600' : 'text-gray-900'
                        }`}
                      >
                        {metric.value(currentStats.data!)}
                      </td>
                      <td
                        className={`py-2 text-right tabular-nums ${
                          winner === 'other' ? 'font-semibold text-green-600' : 'text-gray-900'
                        }`}
                      >
                        {metric.value(otherStats.data!)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}
    </Modal>
  );
}
