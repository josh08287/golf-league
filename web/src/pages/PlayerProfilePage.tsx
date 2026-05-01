import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { usePlayer, useHandicapHistory } from '@/hooks/usePlayers';
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
import { formatShortDate } from '@/lib/utils';

export function PlayerProfilePage() {
  const { playerId } = useParams<{ playerId: string }>();
  const player = usePlayer(playerId ?? '');
  const handicapHistory = useHandicapHistory(playerId ?? '');

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to="/">
          <ArrowLeft className="h-4 w-4 mr-1" />
          Home
        </Link>
      </Button>

      {player.isPending && <FullPageSpinner />}
      {player.isError && (
        <ErrorMessage message="Could not load player profile. Please try again." />
      )}

      {player.data?.data && (
        <>
          <PageHeader
            title={player.data.data.name}
            description={player.data.data.flightName ?? undefined}
          >
            <Badge variant={player.data.data.isActive ? 'green' : 'secondary'}>
              {player.data.data.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </PageHeader>

          {/* Stats cards */}
          <div className="grid gap-4 sm:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  Current Handicap
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold text-primary-900">
                  {player.data.data.currentHandicap.toFixed(1)}
                </p>
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
                  {player.data.data.flightName ?? '—'}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  Member Since
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-lg font-semibold text-gray-900">
                  {formatShortDate(player.data.data.createdAt)}
                </p>
              </CardContent>
            </Card>
          </div>
        </>
      )}

      {/* Handicap history */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Handicap History
        </h2>

        {handicapHistory.isPending && <FullPageSpinner />}
        {handicapHistory.isError && (
          <ErrorMessage message="Could not load handicap history." />
        )}

        {handicapHistory.data && (
          <>
            {handicapHistory.data.data.length === 0 ? (
              <p className="text-gray-500 text-sm">
                No handicap history recorded yet.
              </p>
            ) : (
              <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-gray-50">
                      <TableHead>Effective Date</TableHead>
                      <TableHead>Calculated At</TableHead>
                      <TableHead className="text-right">Handicap Index</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {handicapHistory.data.data.map((h) => (
                      <TableRow key={h.id}>
                        <TableCell>{formatShortDate(h.effectiveDate)}</TableCell>
                        <TableCell className="text-gray-500">
                          {formatShortDate(h.calculatedAt)}
                        </TableCell>
                        <TableCell className="text-right font-semibold">
                          {h.handicapIndex.toFixed(1)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}
