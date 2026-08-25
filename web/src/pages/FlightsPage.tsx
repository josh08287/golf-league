import { Link, useSearchParams } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { Users, ArrowRight, Trophy } from 'lucide-react';
import { useFlights, useFlightStandings, useMatchPlayStandings } from '@/hooks/useFlights';
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
  scoringFormat: 'stableford' | 'matchPlay';
}

function StablefordFlightCardBody({ flight, useGross }: { flight: Flight; useGross: boolean }) {
  const { data: standings, isPending } = useFlightStandings(
    String(flight.id),
    String(flight.halfId),
    useGross
  );

  if (isPending) {
    return (
      <div className="py-8 flex justify-center">
        <div className="animate-spin h-5 w-5 border-2 border-primary-600 border-t-transparent rounded-full" />
      </div>
    );
  }

  if (!standings || standings.length === 0) {
    return <p className="text-sm text-gray-500 py-4 text-center">No standings available for this half yet.</p>;
  }

  return (
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
                  <PlayerLink playerId={standing.playerId} name={standing.playerFullName} />
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
  );
}

function MatchPlayFlightCardBody({ flight }: { flight: Flight }) {
  const { data: standings, isPending } = useMatchPlayStandings(String(flight.id), String(flight.halfId));

  if (isPending) {
    return (
      <div className="py-8 flex justify-center">
        <div className="animate-spin h-5 w-5 border-2 border-primary-600 border-t-transparent rounded-full" />
      </div>
    );
  }

  if (!standings || standings.length === 0) {
    return <p className="text-sm text-gray-500 py-4 text-center">No match play results available for this half yet.</p>;
  }

  return (
    <>
      <div className="rounded border border-gray-200 overflow-hidden">
        <Table>
          <TableBody>
            {standings.slice(0, 5).map((standing) => (
              <TableRow key={standing.playerId} className="hover:bg-gray-50">
                <TableCell className="w-10 py-2 text-center">
                  {positionBadge(standing.position)}
                </TableCell>
                <TableCell className="py-2">
                  <PlayerLink playerId={standing.playerId} name={standing.playerFullName} />
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
  );
}

function PlayerLink({ playerId, name }: { playerId: number; name: string }) {
  const prefix = useLeaguePrefix();
  return (
    <Link to={`${prefix}/players/${playerId}`} className="font-medium text-primary-900 hover:underline text-sm">
      {name}
    </Link>
  );
}

function FlightCard({ flight, useGross, scoringFormat }: FlightCardProps) {
  const prefix = useLeaguePrefix();
  const leaderboardHref = scoringFormat === 'matchPlay'
    ? `${prefix}/flights/${flight.id}/match-play?halfId=${flight.halfId}`
    : `${prefix}/flights/${flight.id}?halfId=${flight.halfId}`;

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
        {scoringFormat === 'matchPlay' ? (
          <MatchPlayFlightCardBody flight={flight} />
        ) : (
          <StablefordFlightCardBody flight={flight} useGross={useGross} />
        )}

        <Button variant="outline" size="sm" className="w-full mt-4" asChild>
          <Link to={leaderboardHref}>
            Full Leaderboard
            <ArrowRight className="ml-1 h-3 w-3" />
          </Link>
        </Button>
      </CardContent>
    </Card>
  );
}

function PeriodSelector({
  seasonId,
  halfId,
  onChange,
}: {
  seasonId: number | null;
  halfId: number | null;
  onChange: (next: { seasonId: number; halfId: number }) => void;
}) {
  const seasons = useSeasons();

  if (seasons.isPending || seasons.isError || !seasons.data || seasons.data.length === 0) {
    return null;
  }

  const orderedSeasons = [...seasons.data].sort((a, b) => b.year - a.year);
  const activeSeason = orderedSeasons.find((s) => s.id === seasonId) ?? orderedSeasons[0];

  const pill = (active: boolean) =>
    [
      'rounded-full px-4 py-1.5 text-sm font-medium border transition-colors',
      active
        ? 'bg-primary-900 text-white border-primary-900'
        : 'bg-white text-gray-700 border-gray-300 hover:border-primary-500',
    ].join(' ');

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-2">
        {orderedSeasons.map((s) => (
          <button
            key={s.id}
            className={pill(activeSeason.id === s.id)}
            onClick={() => {
              const firstHalf = [...s.halves].sort((a, b) => a.halfNumber - b.halfNumber)[0];
              if (firstHalf) onChange({ seasonId: s.id, halfId: firstHalf.id });
            }}
          >
            {s.name}
          </button>
        ))}
      </div>

      {activeSeason.halves.length > 0 && (
        <div className="flex flex-wrap gap-2 pl-2">
          {[...activeSeason.halves]
            .sort((a, b) => a.halfNumber - b.halfNumber)
            .map((h) => (
              <button
                key={h.id}
                className={pill(halfId === h.id)}
                onClick={() => onChange({ seasonId: activeSeason.id, halfId: h.id })}
              >
                {h.name}
              </button>
            ))}
        </div>
      )}
    </div>
  );
}

export function FlightsPage() {
  const prefix = useLeaguePrefix();
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: seasons } = useSeasons();
  // Pull a generous window of rounds (most recent first) so we can find the
  // last finalized round per half across every season, to pick a sensible
  // default half when no season/half is selected yet.
  const { data: roundsPage } = useRounds(1, { sortBy: 'date', sortDir: 'desc' }, 500);
  const [useGross, setUseGross] = useState(false);

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

  // halfId → { season, half } across ALL seasons, for default-half resolution.
  const halfInfoById = useMemo(() => {
    const map = new Map<number, { season: Season; half: SeasonHalf }>();
    for (const season of seasons ?? []) {
      for (const half of season.halves) map.set(half.id, { season, half });
    }
    return map;
  }, [seasons]);

  // Pick the half whose most recent round was finalized most recently; if none
  // have finalized rounds yet, fall back to the active season's latest half.
  const defaultHalf = useMemo(() => {
    let best: { seasonId: number; halfId: number; date: string } | null = null;
    for (const [halfId, date] of lastFinalizedByHalf.entries()) {
      const info = halfInfoById.get(halfId);
      if (!info) continue;
      if (!best || date > best.date) {
        best = { seasonId: info.season.id, halfId, date };
      }
    }
    if (best) return { seasonId: best.seasonId, halfId: best.halfId };

    const activeSeason = seasons?.find((s) => s.isActive) ?? seasons?.[0] ?? null;
    const latestHalf = activeSeason
      ? [...activeSeason.halves].sort((a, b) => b.halfNumber - a.halfNumber)[0]
      : null;
    return activeSeason && latestHalf
      ? { seasonId: activeSeason.id, halfId: latestHalf.id }
      : null;
  }, [lastFinalizedByHalf, halfInfoById, seasons]);

  const seasonIdParam = searchParams.get('seasonId');
  const halfIdParam = searchParams.get('halfId');
  const selectedSeasonId = seasonIdParam ? Number(seasonIdParam) : defaultHalf?.seasonId ?? null;
  const selectedHalfId = halfIdParam ? Number(halfIdParam) : defaultHalf?.halfId ?? null;

  const handlePeriodChange = (next: { seasonId: number; halfId: number }) => {
    const params = new URLSearchParams(searchParams);
    params.set('seasonId', String(next.seasonId));
    params.set('halfId', String(next.halfId));
    setSearchParams(params, { replace: true });
  };

  const selectedHalfInfo = selectedHalfId != null ? halfInfoById.get(selectedHalfId) ?? null : null;

  const { data, isPending, isError } = useFlights(selectedHalfId ?? undefined);
  const flightsForHalf = useMemo(
    () => (data?.data ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder),
    [data],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Standings"
        description="Competition groups created for each half based on starting handicaps."
      >
        <div className="flex items-center gap-3">
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
          {selectedSeasonId != null && (
            <Button variant="outline" size="sm" asChild>
              <Link to={`${prefix}/season-wrap-up?seasonId=${selectedSeasonId}`}>
                <Trophy className="mr-1 h-3.5 w-3.5" />
                Season Wrap-Up
              </Link>
            </Button>
          )}
        </div>
      </PageHeader>

      <PeriodSelector
        seasonId={selectedSeasonId}
        halfId={selectedHalfId}
        onChange={handlePeriodChange}
      />

      {isPending && <FullPageSpinner />}
      {isError && (
        <ErrorMessage message="Could not load flights. Please try again." />
      )}

      {data && flightsForHalf.length === 0 && (
        <p className="text-gray-500 text-sm">
          No flights have been created for this half yet.
        </p>
      )}

      {data && flightsForHalf.length > 0 && selectedHalfInfo && (
        <section className="space-y-4">
          <div className="flex items-baseline justify-between border-b border-gray-200 pb-2">
            <h2 className="text-lg font-semibold text-gray-900">
              {selectedHalfInfo.season.year} — {selectedHalfInfo.half.name}
            </h2>
            <span className="text-sm text-gray-500">
              {selectedHalfInfo.half.startDate} – {selectedHalfInfo.half.endDate}
            </span>
          </div>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {flightsForHalf.map((flight) => (
              <FlightCard
                key={flight.id}
                flight={flight}
                useGross={useGross}
                scoringFormat={selectedHalfInfo.half.scoringFormat}
              />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
