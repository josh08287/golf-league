import { Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { Users, ArrowRight } from 'lucide-react';
import { useFlights, useFlightStandings } from '@/hooks/useFlights';
import { useSeasons } from '@/hooks/useSeasons';
import { useRounds } from '@/hooks/useRounds';
import { normalizeRoundStatus } from '@/lib/enumUtils';
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
import type { Flight, Season, SeasonHalf, Standing } from '@/types/api';

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
  // Pull a generous window of rounds (most recent first) so we can find the
  // last finalized round per half across every season for ordering.
  const { data: roundsPage } = useRounds(1, { sortBy: 'date', sortDir: 'desc' }, 500);
  const [useGross, setUseGross] = useState(false);

  // halfId → { season, half } across ALL seasons (not just the active one), so
  // every group can be labelled with its season year and half name.
  const halfInfoById = useMemo(() => {
    const map = new Map<number, { season: Season; half: SeasonHalf }>();
    for (const season of seasons ?? []) {
      for (const half of season.halves) map.set(half.id, { season, half });
    }
    return map;
  }, [seasons]);

  // halfId → most recent finalized round date. Rounds arrive newest-first, so
  // the first finalized round we see for a half is its latest.
  const lastFinalizedByHalf = useMemo(() => {
    const map = new Map<number, string>();
    for (const r of roundsPage?.data ?? []) {
      if (normalizeRoundStatus(r.status) !== 'Finalized') continue;
      if (!map.has(r.halfId)) map.set(r.halfId, r.scheduledDate);
    }
    return map;
  }, [roundsPage]);

  // Group flights by half, label with "{year} — {half name}", and order so the
  // half whose most recent round was finalized most recently comes first. Halves
  // with no finalized round yet sort last, newest season/half first.
  const groupedByHalf = useMemo(() => {
    const flights = data?.data ?? [];
    const byHalf = new Map<number, Flight[]>();
    for (const f of flights) {
      const list = byHalf.get(f.halfId) ?? [];
      list.push(f);
      byHalf.set(f.halfId, list);
    }
    return [...byHalf.entries()]
      .map(([halfId, halfFlights]) => {
        const info = halfInfoById.get(halfId) ?? null;
        const year = info?.season.year ?? 0;
        const halfName = info?.half.name ?? 'Other';
        const lastFinalized = lastFinalizedByHalf.get(halfId) ?? null;
        return {
          halfId,
          half: info?.half ?? null,
          year,
          name: info ? `${year} — ${halfName}` : 'Other',
          halfNumber: info?.half.halfNumber ?? Number.MAX_SAFE_INTEGER,
          lastFinalized,
          flights: halfFlights
            .slice()
            .sort((a, b) => a.displayOrder - b.displayOrder),
        };
      })
      .sort((a, b) => {
        // Primary: most recent finalized round first; halves with none go last.
        if (a.lastFinalized && b.lastFinalized) {
          if (a.lastFinalized !== b.lastFinalized)
            return a.lastFinalized < b.lastFinalized ? 1 : -1;
        } else if (a.lastFinalized) {
          return -1;
        } else if (b.lastFinalized) {
          return 1;
        }
        // Tiebreak: newest season first, then later half first.
        if (a.year !== b.year) return b.year - a.year;
        return b.halfNumber - a.halfNumber;
      });
  }, [data, halfInfoById, lastFinalizedByHalf]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Standings"
        description="Competition groups created for each half based on starting handicaps."
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

      {data && groupedByHalf.length === 0 && (
        <p className="text-gray-500 text-sm">
          No flights have been created yet.
        </p>
      )}

      {data &&
        groupedByHalf.map((group) => (
          <section key={group.halfId} className="space-y-4">
            <div className="flex items-baseline justify-between border-b border-gray-200 pb-2">
              <h2 className="text-lg font-semibold text-gray-900">{group.name}</h2>
              {group.half && (
                <span className="text-sm text-gray-500">
                  {group.half.startDate} – {group.half.endDate}
                </span>
              )}
            </div>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {group.flights.map((flight) => (
                <FlightCard key={flight.id} flight={flight} useGross={useGross} />
              ))}
            </div>
          </section>
        ))}
    </div>
  );
}
