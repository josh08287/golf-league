import { useMemo } from 'react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useFlightStandings, useFlight } from '@/hooks/useFlights';
import { useSeasons } from '@/hooks/useSeasons';
import {
  Table,
  TableBody,
  TableCell,
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
      <TableCell className="w-16 text-center">{positionBadge(standing.position)}</TableCell>
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
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);

  const flight = useFlight(flightId ?? '');
  const flightData = flight.data;

  const halfFromQuery = searchParams.get('halfId');
  const useGross = searchParams.get('useGrossPoints') === 'true';

  // Default to the half this flight belongs to.
  const halfId = halfFromQuery ?? (flightData ? String(flightData.halfId) : '');

  const { sort, cycle } = useSortableTable('flightStandings');
  const standings = useFlightStandings(flightId ?? '', halfId, useGross, sort);

  const halfLabel = useMemo(() => {
    if (!activeSeason) return '';
    const half = activeSeason.halves.find((h) => String(h.id) === halfId);
    return half?.name ?? '';
  }, [activeSeason, halfId]);

  const title = flightData?.name
    ? `${flightData.name} Leaderboard${halfLabel ? ` — ${halfLabel}` : ''}`
    : 'Flight Leaderboard';

  function setUseGross(next: boolean) {
    const params = new URLSearchParams(searchParams);
    params.set('useGrossPoints', String(next));
    setSearchParams(params);
  }

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
          description={flightData ? `${flightData.playerCount} players` : undefined}
        >
          <div className="flex gap-2 text-sm">
            <button
              onClick={() => setUseGross(false)}
              className={`px-3 py-1.5 rounded ${!useGross ? 'bg-[#1B5E20] text-white' : 'bg-gray-100'}`}
            >
              Net
            </button>
            <button
              onClick={() => setUseGross(true)}
              className={`px-3 py-1.5 rounded ${useGross ? 'bg-[#1B5E20] text-white' : 'bg-gray-100'}`}
            >
              Gross
            </button>
          </div>
        </PageHeader>
      </div>

      {(standings.isPending || flight.isPending) && <FullPageSpinner />}
      {(standings.isError || flight.isError) && (
        <ErrorMessage message="Could not load standings. Please try again." />
      )}

      {standings.data && (
        <>
          {standings.data.length === 0 ? (
            <p className="text-gray-500 text-sm">No standings available for this half yet.</p>
          ) : (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50">
                    <SortableTableHead column="position" sort={sort} onSort={cycle} className="text-center w-16">
                      Pos
                    </SortableTableHead>
                    <SortableTableHead column="player" sort={sort} onSort={cycle}>
                      Player
                    </SortableTableHead>
                    <SortableTableHead column="hcp" sort={sort} onSort={cycle} className="text-center">
                      HCP
                    </SortableTableHead>
                    <SortableTableHead column="rounds" sort={sort} onSort={cycle} className="text-center">
                      Rounds
                    </SortableTableHead>
                    <SortableTableHead column="points" sort={sort} onSort={cycle} className="text-center">
                      {useGross ? 'Gross Pts' : 'Net Pts'}
                    </SortableTableHead>
                    <SortableTableHead column="avg" sort={sort} onSort={cycle} className="text-center">
                      Avg
                    </SortableTableHead>
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
