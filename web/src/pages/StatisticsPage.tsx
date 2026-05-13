import { useState, useMemo } from 'react';
import { BarChart3, TrendingUp, TrendingDown, Target } from 'lucide-react';
import { useCourses, useCourseStatistics } from '@/hooks/useStatistics';
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
import { Badge } from '@/components/ui/Badge';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import type { HoleStatistics } from '@/types/api';

function scoreToParLabel(val: number | null) {
  if (val == null) return '—';
  if (val > 0) return `+${val.toFixed(1)}`;
  if (val === 0) return 'E';
  return val.toFixed(1);
}

function difficultyBadge(rank: number, total: number) {
  if (rank <= Math.ceil(total / 3)) return <Badge variant="red">Hard</Badge>;
  if (rank <= Math.ceil((2 * total) / 3)) return <Badge variant="amber">Medium</Badge>;
  return <Badge variant="green">Easy</Badge>;
}

function scoringBar(hole: HoleStatistics) {
  const total =
    hole.eagleOrBetterCount +
    hole.birdieCount +
    hole.parCount +
    hole.bogeyCount +
    hole.doubleBogeyOrWorseCount;
  if (total === 0) return null;

  const segments = [
    { label: 'Eagle+', count: hole.eagleOrBetterCount, color: 'bg-blue-500' },
    { label: 'Birdie', count: hole.birdieCount, color: 'bg-green-500' },
    { label: 'Par', count: hole.parCount, color: 'bg-gray-400' },
    { label: 'Bogey', count: hole.bogeyCount, color: 'bg-amber-500' },
    { label: 'Dbl+', count: hole.doubleBogeyOrWorseCount, color: 'bg-red-500' },
  ];

  return (
    <div className="flex h-4 w-full overflow-hidden rounded-full">
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
  );
}

export function StatisticsPage() {
  const courses = useCourses();
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);

  const firstCourseId = courses.data?.[0]?.id ?? null;
  const activeCourseId = selectedCourseId ?? firstCourseId;

  const stats = useCourseStatistics(activeCourseId ?? '');

  const hardestHole = useMemo(
    () =>
      stats.data?.holeStatistics.reduce<HoleStatistics | null>(
        (best, h) =>
          !best || h.averageScoreToPar > best.averageScoreToPar ? h : best,
        null,
      ) ?? null,
    [stats.data],
  );

  const easiestHole = useMemo(
    () =>
      stats.data?.holeStatistics.reduce<HoleStatistics | null>(
        (best, h) =>
          !best || h.averageScoreToPar < best.averageScoreToPar ? h : best,
        null,
      ) ?? null,
    [stats.data],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Course Statistics"
        description="Performance breakdown by hole across all finalized rounds"
      >
        <BarChart3 className="h-6 w-6 text-primary-700" />
      </PageHeader>

      {/* Course picker */}
      {courses.isPending && <FullPageSpinner />}
      {courses.isError && (
        <ErrorMessage message="Could not load courses." />
      )}
      {courses.data && courses.data.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {courses.data.map((c) => (
            <button
              key={c.id}
              onClick={() => setSelectedCourseId(c.id)}
              className={[
                'rounded-full px-4 py-1.5 text-sm font-medium border transition-colors',
                activeCourseId === c.id
                  ? 'bg-primary-900 text-white border-primary-900'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-primary-500',
              ].join(' ')}
            >
              {c.name}
            </button>
          ))}
        </div>
      )}
      {courses.data && courses.data.length === 0 && (
        <p className="text-sm text-gray-500">No courses configured yet.</p>
      )}

      {/* Stats content */}
      {activeCourseId && (
        <>
          {stats.isPending && <FullPageSpinner />}
          {stats.isError && (
            <ErrorMessage message="Could not load course statistics." />
          )}

          {stats.data && (
            <>
              {/* Summary cards */}
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-gray-500">
                      Rounds Played
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p className="text-3xl font-bold text-primary-900">
                      {stats.data.totalRoundsPlayed}
                    </p>
                    <p className="text-xs text-gray-400 mt-1">
                      {stats.data.totalScorecardsRecorded} scorecards
                    </p>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-gray-500">
                      Avg Score to Par
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p className="text-3xl font-bold text-primary-900">
                      {scoreToParLabel(stats.data.averageScoreToPar)}
                    </p>
                    <p className="text-xs text-gray-400 mt-1">
                      Gross: {stats.data.averageTotalGrossStrokes?.toFixed(1) ?? '—'} |
                      Net: {stats.data.averageTotalNetStrokes?.toFixed(1) ?? '—'}
                    </p>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-1 text-sm font-medium text-red-600">
                      <TrendingUp className="h-4 w-4" />
                      Hardest Hole
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    {hardestHole ? (
                      <>
                        <p className="text-3xl font-bold text-primary-900">
                          #{hardestHole.holeNumber}
                        </p>
                        <p className="text-xs text-gray-400 mt-1">
                          Par {hardestHole.par} | Avg {scoreToParLabel(hardestHole.averageScoreToPar)} to par
                        </p>
                      </>
                    ) : (
                      <p className="text-gray-400">—</p>
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-1 text-sm font-medium text-green-600">
                      <TrendingDown className="h-4 w-4" />
                      Easiest Hole
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    {easiestHole ? (
                      <>
                        <p className="text-3xl font-bold text-primary-900">
                          #{easiestHole.holeNumber}
                        </p>
                        <p className="text-xs text-gray-400 mt-1">
                          Par {easiestHole.par} | Avg {scoreToParLabel(easiestHole.averageScoreToPar)} to par
                        </p>
                      </>
                    ) : (
                      <p className="text-gray-400">—</p>
                    )}
                  </CardContent>
                </Card>
              </div>

              {/* Avg Stableford Points */}
              <div className="grid gap-4 sm:grid-cols-2">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-1 text-sm font-medium text-gray-500">
                      <Target className="h-4 w-4" />
                      Avg Gross Stableford Pts
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p className="text-2xl font-bold text-primary-900">
                      {stats.data.averageTotalGrossStablefordPoints?.toFixed(1) ?? '—'}
                    </p>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="flex items-center gap-1 text-sm font-medium text-gray-500">
                      <Target className="h-4 w-4" />
                      Avg Net Stableford Pts
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p className="text-2xl font-bold text-primary-900">
                      {stats.data.averageTotalNetStablefordPoints?.toFixed(1) ?? '—'}
                    </p>
                  </CardContent>
                </Card>
              </div>

              {/* Scoring legend */}
              <div className="flex flex-wrap items-center gap-3 text-xs text-gray-500">
                <span className="font-medium text-gray-700">Scoring distribution:</span>
                <span className="flex items-center gap-1"><span className="inline-block h-3 w-3 rounded-full bg-blue-500" />Eagle+</span>
                <span className="flex items-center gap-1"><span className="inline-block h-3 w-3 rounded-full bg-green-500" />Birdie</span>
                <span className="flex items-center gap-1"><span className="inline-block h-3 w-3 rounded-full bg-gray-400" />Par</span>
                <span className="flex items-center gap-1"><span className="inline-block h-3 w-3 rounded-full bg-amber-500" />Bogey</span>
                <span className="flex items-center gap-1"><span className="inline-block h-3 w-3 rounded-full bg-red-500" />Dbl Bogey+</span>
              </div>

              {/* Hole-by-hole table */}
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-gray-50">
                      <TableHead className="w-16 text-center">Hole</TableHead>
                      <TableHead className="w-16 text-center">Par</TableHead>
                      <TableHead className="w-16 text-center">SI</TableHead>
                      <TableHead className="text-right">Avg Gross</TableHead>
                      <TableHead className="text-right">Avg Net</TableHead>
                      <TableHead className="text-right">Avg to Par</TableHead>
                      <TableHead className="text-right">Avg Net Pts</TableHead>
                      <TableHead className="w-20 text-center">Difficulty</TableHead>
                      <TableHead className="min-w-[180px]">Scoring</TableHead>
                      <TableHead className="text-right w-16">Played</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {stats.data.holeStatistics.map((hole) => (
                      <TableRow key={hole.holeNumber}>
                        <TableCell className="text-center font-semibold">
                          {hole.holeNumber}
                        </TableCell>
                        <TableCell className="text-center">{hole.par}</TableCell>
                        <TableCell className="text-center text-gray-500">
                          {hole.strokeIndex}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {hole.averageGrossStrokes.toFixed(1)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {hole.averageNetStrokes.toFixed(1)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          <span
                            className={
                              hole.averageScoreToPar > 0
                                ? 'text-red-600'
                                : hole.averageScoreToPar < 0
                                  ? 'text-green-600'
                                  : 'text-gray-600'
                            }
                          >
                            {scoreToParLabel(hole.averageScoreToPar)}
                          </span>
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {hole.averageNetStablefordPoints.toFixed(1)}
                        </TableCell>
                        <TableCell className="text-center">
                          {difficultyBadge(
                            hole.difficultyRank,
                            stats.data!.holeStatistics.length,
                          )}
                        </TableCell>
                        <TableCell>{scoringBar(hole)}</TableCell>
                        <TableCell className="text-right text-gray-500 tabular-nums">
                          {hole.totalScoresRecorded}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              {/* Course info footer */}
              <p className="text-xs text-gray-400 text-center">
                {stats.data.courseName} — Rating: {stats.data.courseRating} | Slope: {stats.data.slopeRating}
              </p>
            </>
          )}
        </>
      )}
    </div>
  );
}
