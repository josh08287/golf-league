import { useParams, useSearchParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useFlightStandings, useFlight } from '@/hooks/useFlights';
import { useSeasons } from '@/hooks/useSeasons';
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
import type { Standing } from '@/types/api';

type PodiumVariant = 'gold' | 'silver' | 'bronze';

function positionBadge(position: number) {
  if (position === 1) return <Badge variant="gold">1</Badge>;
  if (position === 2) return <Badge variant="silver">2</Badge>;
  if (position === 3) return <Badge variant="bronze">3</Badge>;
  return <span className="text-sm text-gray-600 font-medium">{position}</span>;
}

interface StandingsRowProps {
  standing: Standing;
  highlight: PodiumVariant | null;
}

function StandingsRow({ standing, highlight }: StandingsRowProps) {
  const rowClass =
    highlight === 'gold'
      ? 'bg-yellow-50'
      : highlight === 'silver'
        ? 'bg-gray-50'
        : highlight === 'bronze'
          ? 'bg-amber-50'
          : '';

  return (
    <TableRow className={rowClass}>
      <TableCell className="w-16 text-center">
        {positionBadge(standing.position)}
      </TableCell>
      <TableCell>
        <Link
          to={`/players/${standing.playerId}`}
          className="font-medium text-primary-900 hover:underline"
        >
          {standing.playerFullName}
        </Link>
      </TableCell>
      <TableCell className="text-center">{standing.currentHandicapIndex.toFixed(1)}</TableCell>
      <TableCell className="text-center">{standing.roundsPlayed}</TableCell>
      <TableCell className="text-center font-semibold">{standing.totalPoints}</TableCell>
      <TableCell className="text-center">{standing.averagePoints.toFixed(1)}</TableCell>
    </TableRow>
  );
}

function podiumVariant(position: number): PodiumVariant | null {
  if (position === 1) return 'gold';
  if (position === 2) return 'silver';
  if (position === 3) return 'bronze';
  return null;
}

export function FlightLeaderboardPage() {
  const { flightId } = useParams<{ flightId: string }>();
  const [searchParams] = useSearchParams();
  const { data: seasons } = useSeasons();
  const activeSeason = seasons?.find((s) => s.isActive);
  const seasonId = searchParams.get('seasonId') ?? String(activeSeason?.id ?? '');

  const flight = useFlight(flightId ?? '');
  const standings = useFlightStandings(flightId ?? '', seasonId);

  const flightData = flight.data;
  const title = flightData?.name ? `${flightData.name} Leaderboard` : 'Flight Leaderboard';

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="mb-4 -ml-2">
          <Link to="/flights">
            <ArrowLeft className="h-4 w-4 mr-1" />
            Back to Flights
          </Link>
        </Button>
        <PageHeader
          title={title}
          description={
            flightData
              ? `${flightData.playerCount} players`
              : undefined
          }
        />
      </div>

      {(standings.isPending || flight.isPending) && <FullPageSpinner />}
      {(standings.isError || flight.isError) && (
        <ErrorMessage message="Could not load standings. Please try again." />
      )}

      {standings.data && (
        <>
          {standings.data.length === 0 ? (
            <p className="text-gray-500 text-sm">
              No standings available.{' '}
              {!seasonId && 'Provide a seasonId query parameter to filter by season.'}
            </p>
          ) : (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50">
                    <TableHead className="text-center w-16">Pos</TableHead>
                    <TableHead>Player</TableHead>
                    <TableHead className="text-center">HCP</TableHead>
                    <TableHead className="text-center">Rounds</TableHead>
                    <TableHead className="text-center">Total Pts</TableHead>
                    <TableHead className="text-center">Avg Pts</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {standings.data.map((standing) => (
                    <StandingsRow
                      key={standing.playerId}
                      standing={standing}
                      highlight={podiumVariant(standing.position)}
                    />
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
