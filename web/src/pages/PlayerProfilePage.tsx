import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { usePlayer, useHandicapHistory, usePlayerRounds } from '@/hooks/usePlayers';
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
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { HandicapChart } from '@/components/HandicapChart';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus } from '@/lib/enumUtils';

export function PlayerProfilePage() {
  const { playerId } = useParams<{ playerId: string }>();
  const player = usePlayer(playerId ?? '');
  const handicapHistory = useHandicapHistory(playerId ?? '');
  const playerRounds = usePlayerRounds(playerId ?? '');

  const playerData = player.data;
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
                  <TableHead>Date</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead className="text-right">18-Hole</TableHead>
                  <TableHead className="text-right">9-Hole</TableHead>
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
                  <TableHead>Date</TableHead>
                  <TableHead>Course</TableHead>
                  <TableHead className="text-center">Wk</TableHead>
                  <TableHead className="text-center">Status</TableHead>
                  <TableHead className="text-right">Gross</TableHead>
                  <TableHead className="text-right">Net</TableHead>
                  <TableHead className="text-right">Pts</TableHead>
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
