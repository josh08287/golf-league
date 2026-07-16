import { ArrowLeft, Users } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { useJoesVsOthersStatistics, type StatisticsPeriod } from '@/hooks/useStatistics';
import { PeriodSelector } from '@/pages/StatisticsPage';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { PageHeader } from '@/components/ui/PageHeader';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import type { GroupStatistics } from '@/types/api';

function scoreToParLabel(val: number | null) {
  if (val == null) return '—';
  if (val > 0) return `+${val.toFixed(1)}`;
  if (val === 0) return 'E';
  return val.toFixed(1);
}

interface MetricRow {
  label: string;
  value: (g: GroupStatistics) => string;
  lowerIsBetter?: boolean;
  raw: (g: GroupStatistics) => number | null;
}

const METRICS: MetricRow[] = [
  { label: 'Players', value: (g) => `${g.playerCount}`, raw: (g) => g.playerCount },
  { label: 'Rounds Played', value: (g) => `${g.totalRoundsPlayed}`, raw: (g) => g.totalRoundsPlayed },
  {
    label: 'Avg Score to Par',
    value: (g) => scoreToParLabel(g.averageScoreToPar),
    lowerIsBetter: true,
    raw: (g) => g.averageScoreToPar,
  },
  {
    label: 'Avg Gross Strokes',
    value: (g) => g.averageGrossStrokes?.toFixed(1) ?? '—',
    lowerIsBetter: true,
    raw: (g) => g.averageGrossStrokes,
  },
  {
    label: 'Avg Net Strokes',
    value: (g) => g.averageNetStrokes?.toFixed(1) ?? '—',
    lowerIsBetter: true,
    raw: (g) => g.averageNetStrokes,
  },
  {
    label: 'Avg Gross Stableford Pts',
    value: (g) => g.averageGrossStablefordPoints?.toFixed(1) ?? '—',
    lowerIsBetter: false,
    raw: (g) => g.averageGrossStablefordPoints,
  },
  {
    label: 'Avg Net Stableford Pts',
    value: (g) => g.averageNetStablefordPoints?.toFixed(1) ?? '—',
    lowerIsBetter: false,
    raw: (g) => g.averageNetStablefordPoints,
  },
  {
    label: 'Par or Better %',
    value: (g) => (g.parOrBetterPercentage != null ? `${g.parOrBetterPercentage}%` : '—'),
    lowerIsBetter: false,
    raw: (g) => g.parOrBetterPercentage,
  },
  {
    label: 'Eagles or Better',
    value: (g) => `${g.eagleOrBetterCount}`,
    lowerIsBetter: false,
    raw: (g) => g.eagleOrBetterCount,
  },
  { label: 'Birdies', value: (g) => `${g.birdieCount}`, lowerIsBetter: false, raw: (g) => g.birdieCount },
  { label: 'Pars', value: (g) => `${g.parCount}`, lowerIsBetter: false, raw: (g) => g.parCount },
  { label: 'Bogeys', value: (g) => `${g.bogeyCount}`, lowerIsBetter: true, raw: (g) => g.bogeyCount },
  {
    label: 'Double Bogey or Worse',
    value: (g) => `${g.doubleBogeyOrWorseCount}`,
    lowerIsBetter: true,
    raw: (g) => g.doubleBogeyOrWorseCount,
  },
];

function betterSide(metric: MetricRow, joesValue: number | null, othersValue: number | null): 'joes' | 'others' | null {
  if (metric.lowerIsBetter == null) return null;
  if (joesValue == null || othersValue == null || joesValue === othersValue) return null;
  const joesWin = metric.lowerIsBetter ? joesValue < othersValue : joesValue > othersValue;
  return joesWin ? 'joes' : 'others';
}

export function JoesVsOthersPage() {
  const prefix = useLeaguePrefix();
  const [searchParams, setSearchParams] = useSearchParams();

  const seasonIdParam = searchParams.get('seasonId');
  const halfIdParam = searchParams.get('halfId');
  const periodSeasonId = seasonIdParam ? Number(seasonIdParam) : null;
  const periodHalfId = halfIdParam ? Number(halfIdParam) : null;
  const period: StatisticsPeriod = { seasonId: periodSeasonId, halfId: periodHalfId };

  const handlePeriodChange = (next: { seasonId: number | null; halfId: number | null }) => {
    const params = new URLSearchParams(searchParams);
    if (next.seasonId == null) params.delete('seasonId');
    else params.set('seasonId', String(next.seasonId));
    if (next.halfId == null) params.delete('halfId');
    else params.set('halfId', String(next.halfId));
    setSearchParams(params, { replace: true });
  };

  const stats = useJoesVsOthersStatistics(period);

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to={`${prefix}/statistics`}>
          <ArrowLeft className="h-4 w-4 mr-1" />
          Statistics
        </Link>
      </Button>

      <PageHeader
        title="Joes vs Schmoes"
        description="Every player named Joe or Joseph, compared against everyone else"
      >
        <Users className="h-6 w-6 text-primary-700" />
      </PageHeader>

      <PeriodSelector seasonId={periodSeasonId} halfId={periodHalfId} onChange={handlePeriodChange} />

      {stats.isPending && <FullPageSpinner />}
      {stats.isError && <ErrorMessage message="Could not load Joes vs Schmoes statistics. Please try again." />}

      {stats.data && (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  {stats.data.joes.groupName} ({stats.data.joes.playerCount} player
                  {stats.data.joes.playerCount === 1 ? '' : 's'})
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold text-primary-900">
                  {scoreToParLabel(stats.data.joes.averageScoreToPar)}
                </p>
                <p className="text-xs text-gray-400 mt-1">avg score to par</p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  {stats.data.others.groupName} ({stats.data.others.playerCount} player
                  {stats.data.others.playerCount === 1 ? '' : 's'})
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold text-gray-900">
                  {scoreToParLabel(stats.data.others.averageScoreToPar)}
                </p>
                <p className="text-xs text-gray-400 mt-1">avg score to par</p>
              </CardContent>
            </Card>
          </div>

          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="py-2 px-4 text-left font-medium text-gray-500">Metric</th>
                  <th className="py-2 px-4 text-right font-semibold text-primary-900">
                    {stats.data.joes.groupName}
                  </th>
                  <th className="py-2 px-4 text-right font-semibold text-gray-900">
                    {stats.data.others.groupName}
                  </th>
                </tr>
              </thead>
              <tbody>
                {METRICS.map((metric) => {
                  const joesRaw = metric.raw(stats.data!.joes);
                  const othersRaw = metric.raw(stats.data!.others);
                  const winner = betterSide(metric, joesRaw, othersRaw);

                  return (
                    <tr key={metric.label} className="border-b border-gray-100 last:border-0">
                      <td className="py-2 px-4 text-gray-500">{metric.label}</td>
                      <td
                        className={`py-2 px-4 text-right tabular-nums ${
                          winner === 'joes' ? 'font-semibold text-green-600' : 'text-gray-900'
                        }`}
                      >
                        {metric.value(stats.data!.joes)}
                      </td>
                      <td
                        className={`py-2 px-4 text-right tabular-nums ${
                          winner === 'others' ? 'font-semibold text-green-600' : 'text-gray-900'
                        }`}
                      >
                        {metric.value(stats.data!.others)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {(stats.data.joes.totalRoundsPlayed === 0 || stats.data.others.totalRoundsPlayed === 0) && (
            <p className="text-sm text-gray-500 text-center">
              One of the groups has no finalized rounds recorded yet for this period.
            </p>
          )}
        </>
      )}
    </div>
  );
}
