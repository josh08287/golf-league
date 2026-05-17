import { Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { Users, ArrowRight } from 'lucide-react';
import { useFlights, useFlightStandings } from '@/hooks/useFlights';
import { useSeasons } from '@/hooks/useSeasons';
import { useMemo, useState } from 'react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import {
  Table,
  TableBody,
  TableCell,
  TableRow,
} from '@/components/ui/Table';
import type { Flight, Standing } from '@/types/api';

function positionBadge(position: number) {
  if (position === 1) return <Badge variant="gold">1</Badge>;
  if (position === 2) return <Badge variant="silver">2</Badge>;
  if (position === 3) return <Badge variant="bronze">3</Badge>;
  return <span className="text-sm text-gray-600 font-medium">{position}</span>;
}

interface FlightCardProps {
  flight: Flight;
  useGross: boolean;
}

function FlightCard({ flight, useGross }: FlightCardProps) {
  const prefix = useLeaguePrefix();
  const { data: standings, isPending } = useFlightStandings(
    String(flight.id),
    String(flight.halfId),
    useGross
  );

  return (
    <Card className="flex flex-col hover:shadow-md transition-shadow">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-lg">{flight.name}</CardTitle>
          <Badge variant="secondary" className="shrink-0">
            <Users className="mr-1 h-3 w-3" />
            {flight.playerCount}
          </Badge>
        </div>
        <CardDescription>{flight.playerCount} players</CardDescription>
      </CardHeader>

      <CardContent className="pt-0 flex-1">
        {isPending ? (
          <div className="py-8 flex justify-center">
            <div className="animate-spin h-5 w-5 border-2 border-primary-600 border-t-transparent rounded-full" />
          </div>
        ) : standings && standings.length > 0 ? (
          <>
            <div className="rounded border border-gray-200 overflow-hidden">
              <Table>
                <TableBody>
                  {standings.slice(0, 5).map((standing: Standing) => (
                    <TableRow key={standing.playerId} className="hover:bg-gray-50">
                      <TableCell className="w-10 py-2 text-center">
                        {positionBadge(standing.position)}
                      </TableCell>
                      <TableCell className="py-2">
                        <Link
                          to={`${prefix}/players/${standing.playerId}`}
                          className="font-medium text-primary-900 hover:underline text-sm"
                        >
                          {standing.playerFullName}
                        </Link>
                      </TableCell>
                      <TableCell className="w-16 py-2 text-center text-sm font-semibold">
                        {standing.totalPoints}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {standings.length > 5 && (
              <p className="text-xs text-gray-500 mt-2 text-center">
                +{standings.length - 5} more players
              </p>
            )}
          </>
        ) : (
          <p className="text-sm text-gray-500 py-4 text-center">
            No standings available for this half yet.
          </p>
        )}

        <Button variant="outline" size="sm" className="w-full mt-4" asChild>
          <Link to={`${prefix}/flights/${flight.id}?halfId=${flight.halfId}`}>
            Full Leaderboard
            <ArrowRight className="ml-1 h-3 w-3" />
          </Link>
        </Button>
      </CardContent>
    </Card>
  );
}

export function FlightsPage() {
  const { data, isPending, isError } = useFlights();
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(
    () => seasons?.find((s) => s.isActive) ?? null,
    [seasons]
  );
  const [useGross, setUseGross] = useState(false);

  const halfLabel = useMemo(() => {
    if (!activeSeason || !data?.data[0]) return '';
    const halfId = data.data[0].halfId;
    const half = activeSeason.halves.find((h) => h.id === halfId);
    return half?.name ?? '';
  }, [activeSeason, data]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Flights"
        description={
          halfLabel
            ? `Competition groups — ${halfLabel}`
            : 'Competition groups created for each half based on starting handicaps.'
        }
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

      {isPending && <FullPageSpinner />}
      {isError && (
        <ErrorMessage message="Could not load flights. Please try again." />
      )}

      {data && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {data.data.length === 0 && (
            <p className="col-span-full text-gray-500 text-sm">
              No flights have been created for this season yet.
            </p>
          )}
          {data.data.map((flight) => (
            <FlightCard key={flight.id} flight={flight} useGross={useGross} />
          ))}
        </div>
      )}
    </div>
  );
}
