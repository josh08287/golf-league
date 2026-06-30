import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useLeagueName, useLeaguePrefix } from '@/context/LeagueContext';
import { ArrowRight, Trophy, Calendar, Edit3 } from 'lucide-react';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '@/lib/utils';
import { useFlights } from '@/hooks/useFlights';
import { useRounds, useRoundScorecards, useRoundSkins } from '@/hooks/useRounds';
import { GrossPar3SkinsDisplay } from '@/components/GrossPar3SkinsDisplay';
import { useMyTodaysTeeTime } from '@/hooks/useTeeTimeScoreEntry';
import { useAuthStore } from '@/store/authStore';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
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
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus } from '@/lib/enumUtils';
import type { Flight, Round, RoundScorecard, RoundStatus } from '@/types/api';

function statusVariant(status: RoundStatus) {
  const normalized = normalizeRoundStatus(status);
  switch (normalized) {
    case 'Finalized':           return 'green' as const;
    case 'InProgress':          return 'amber' as const;
    case 'PendingFinalization': return 'amber' as const;
    case 'Scheduled':           return 'blue' as const;
    case 'Cancelled':           return 'neutral' as const;
  }
}

interface TodaysTeeTimeCardProps {
  teeTime: import('@/types/api').MyTodaysTeeTime;
}

function TodaysTeeTimeCard({ teeTime }: TodaysTeeTimeCardProps) {
  const roundDate = new Date(teeTime.roundDate);
  const formattedDate = roundDate.toLocaleDateString(undefined, {
    weekday: 'long',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });

  return (
    <Card className="border-amber-200 bg-amber-50">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Edit3 className="h-5 w-5 text-amber-600" />
            <CardTitle className="text-base text-amber-900">
              Today's Round: Enter Scores
            </CardTitle>
          </div>
          <Badge variant="amber">
            {formattedDate}
          </Badge>
        </div>
        <CardDescription className="text-amber-700">
          {teeTime.courseName} · {teeTime.nineHoleSide} 9 · Tee Time: {teeTime.scheduledTimeFormatted}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {teeTime.canEnterScores ? (
          <Button variant="primary" asChild className="w-full bg-amber-600 hover:bg-amber-700">
            <Link to={`/tee-times/${teeTime.teeTimeId}/enter-scores`}>
              Enter Scores for Your Group
            </Link>
          </Button>
        ) : (
          <div className="rounded-md bg-amber-100 px-3 py-2 text-sm text-amber-800">
            Scores have already been submitted for this round. An admin will finalize shortly.
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Pick the most recently played round to feature on the homepage. We
 * prefer the latest Finalized round (its scores are stable); if none
 * are finalized yet we fall back to the most recent round of any
 * status so admins still see something useful right after entry.
 */
function pickFeaturedRound(rounds: Round[]): Round | null {
  if (rounds.length === 0) return null;
  const sorted = [...rounds].sort(
    (a, b) => new Date(b.scheduledDate).getTime() - new Date(a.scheduledDate).getTime(),
  );
  const finalized = sorted.find((r) => normalizeRoundStatus(r.status) === 'Finalized');
  return finalized ?? sorted[0];
}

interface FlightScorecardCardProps {
  flightId: number;
  flightName: string;
  scorecards: RoundScorecard[];
}

function FlightScorecardCard({ flightId, flightName, scorecards }: FlightScorecardCardProps) {
  const prefix = useLeaguePrefix();
  // Players sorted by net points (the league's primary metric), then net
  // strokes ascending (lower is better) as a tiebreaker.
  const sorted = useMemo(() => {
    return [...scorecards].sort((a, b) => {
      const ap = a.netPoints ?? -1;
      const bp = b.netPoints ?? -1;
      if (bp !== ap) return bp - ap;
      const an = a.netScore ?? Number.POSITIVE_INFINITY;
      const bn = b.netScore ?? Number.POSITIVE_INFINITY;
      return an - bn;
    });
  }, [scorecards]);

  if (sorted.length === 0) return null;

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">{flightName}</CardTitle>
          <Badge variant="secondary">{sorted.length} players</Badge>
        </div>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        <Table>
          <TableHeader>
            <TableRow className="bg-gray-50">
              <TableHead className="w-7 text-center">#</TableHead>
              <TableHead>Player</TableHead>
              <TableHead className="text-right">HCP</TableHead>
              <TableHead className="text-right">Gross</TableHead>
              <TableHead className="text-right">Net</TableHead>
              <TableHead className="text-right">Pts</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.map((sc, i) => (
              <TableRow key={sc.playerId}>
                <TableCell className="text-center text-xs text-gray-500">{i + 1}</TableCell>
                <TableCell>
                  <Link
                    to={`${prefix}/players/${sc.playerId}`}
                    className="font-medium text-primary-900 hover:underline"
                  >
                    {sc.playerName}
                  </Link>
                </TableCell>
                <TableCell className="text-right text-gray-600 tabular-nums whitespace-nowrap" title={HANDICAP_PAIR_TOOLTIP}>
                  {formatHandicapPair(sc.handicapAtTime)}
                </TableCell>
                <TableCell className="text-right tabular-nums">{sc.grossScore ?? '—'}</TableCell>
                <TableCell className="text-right tabular-nums">{sc.netScore ?? '—'}</TableCell>
                <TableCell className="text-right font-semibold tabular-nums">
                  {sc.netPoints ?? '—'}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <div className="border-t border-gray-100 px-5 py-3">
          <Button variant="ghost" size="sm" asChild className="-ml-2">
            <Link to={`${prefix}/flights/${flightId}`} className="flex items-center gap-1 text-xs">
              Season standings <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

interface FeaturedRoundProps {
  round: Round;
}

function FeaturedRound({ round }: FeaturedRoundProps) {
  const prefix = useLeaguePrefix();
  const scorecards = useRoundScorecards(String(round.id));
  const skins = useRoundSkins(String(round.id));
  const flights = useFlights();

  const cards = (scorecards.data?.data ?? []).reduce<Map<number, RoundScorecard[]>>(
    (acc, sc) => {
      const list = acc.get(sc.flightId) ?? [];
      list.push(sc);
      acc.set(sc.flightId, list);
      return acc;
    },
    new Map<number, RoundScorecard[]>(),
  );

  const flightLookup = new Map<number, Flight>(
    (flights.data?.data ?? []).map((f) => [f.id, f]),
  );

  const orderedFlightIds = Array.from(cards.keys()).sort((a, b) => {
    const aOrder = flightLookup.get(a)?.displayOrder ?? Number.MAX_SAFE_INTEGER;
    const bOrder = flightLookup.get(b)?.displayOrder ?? Number.MAX_SAFE_INTEGER;
    if (aOrder !== bOrder) return aOrder - bOrder;
    return a - b;
  });

  return (
    <div className="space-y-4">
      <Link
        to={`${prefix}/rounds/${round.id}`}
        className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-5 py-4 hover:shadow-sm hover:border-primary-300 transition-all"
      >
        <div>
          <p className="font-semibold text-gray-900">{round.courseName}</p>
          <p className="text-sm text-gray-500">
            Week {round.weekNumber} &middot; {round.nineHoleSide} 9 &middot;{' '}
            {formatShortDate(round.scheduledDate)}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant={statusVariant(round.status)}>{round.status}</Badge>
          <ArrowRight className="h-4 w-4 text-gray-400" />
        </div>
      </Link>

      {skins.data?.grossPar3Skins && (
        <GrossPar3SkinsDisplay grossPar3Skins={skins.data.grossPar3Skins} />
      )}

      {scorecards.isPending && (
        <div className="flex justify-center py-8">
          <Spinner />
        </div>
      )}
      {scorecards.isError && (
        <ErrorMessage message="Could not load scorecards for this round." />
      )}
      {scorecards.data && orderedFlightIds.length === 0 && (
        <p className="text-gray-500 text-sm">No scores recorded for this round yet.</p>
      )}
      {scorecards.data && orderedFlightIds.length > 0 && (
        <div className="grid gap-4 lg:grid-cols-2">
          {orderedFlightIds.map((flightId) => (
            <FlightScorecardCard
              key={flightId}
              flightId={flightId}
              flightName={flightLookup.get(flightId)?.name ?? `Flight ${flightId}`}
              scorecards={cards.get(flightId) ?? []}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export function HomePage() {
  const leagueName = useLeagueName();
  const prefix = useLeaguePrefix();
  const flights = useFlights();
  // Most recent first so page 1 holds the latest rounds across all halves —
  // otherwise (default ascending) page 1 is the earliest rounds and the
  // featured round is stuck in the first half once the season grows past a page.
  const rounds = useRounds(1, { sortBy: 'date', sortDir: 'desc' });
  const isAuthed = useAuthStore((s) => !!s.user);
  const todaysTeeTime = useMyTodaysTeeTime(isAuthed);

  const allRounds = rounds.data?.data ?? [];
  const featured = pickFeaturedRound(allRounds);
  const olderRounds = featured
    ? allRounds.filter((r) => r.id !== featured.id).slice(0, 3)
    : [];

  const hasTodaysTeeTime = todaysTeeTime.data != null;

  return (
    <div className="space-y-10">
      {/* Hero */}
      <section className="rounded-2xl bg-primary-900 px-8 py-12 text-white text-center">
        <h1 className="text-4xl font-extrabold tracking-tight sm:text-5xl">
          ⛳ {leagueName}
        </h1>
        <p className="mt-4 text-primary-100 text-lg max-w-xl mx-auto">
          Track standings, scores, and handicaps all season long.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <Button variant="secondary" asChild>
            <Link to={`${prefix}/flights`}>View Flights</Link>
          </Button>
          <Button
            className="bg-white text-primary-900 hover:bg-primary-50"
            asChild
          >
            <Link to={`${prefix}/rounds`}>Latest Rounds</Link>
          </Button>
        </div>
      </section>

      {/* Today's Tee Time Score Entry Card */}
      {hasTodaysTeeTime && (
        <TodaysTeeTimeCard teeTime={todaysTeeTime.data!} />
      )}

      {/* Latest round — expanded by flight */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-xl font-bold text-gray-900">
            <Calendar className="h-5 w-5 text-primary-700" />
            Latest Round
          </h2>
          <Button variant="ghost" size="sm" asChild>
            <Link to={`${prefix}/rounds`} className="flex items-center gap-1">
              All rounds <ArrowRight className="h-4 w-4" />
            </Link>
          </Button>
        </div>

        {rounds.isPending && (
          <div className="flex justify-center py-12">
            <Spinner />
          </div>
        )}
        {rounds.isError && (
          <ErrorMessage message="Could not load rounds. Please try again." />
        )}
        {rounds.data && !featured && (
          <p className="text-gray-500 text-sm">No rounds played yet.</p>
        )}
        {featured && <FeaturedRound round={featured} />}
      </section>

      {/* Flights overview */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-xl font-bold text-gray-900">
            <Trophy className="h-5 w-5 text-primary-700" />
            Flights &amp; Standings
          </h2>
          <Button variant="ghost" size="sm" asChild>
            <Link to={`${prefix}/flights`} className="flex items-center gap-1">
              All flights <ArrowRight className="h-4 w-4" />
            </Link>
          </Button>
        </div>

        {flights.isPending && (
          <div className="flex justify-center py-12">
            <Spinner />
          </div>
        )}
        {flights.isError && (
          <ErrorMessage message="Could not load flights. Please try again." />
        )}
        {flights.data && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {flights.data.data.map((flight) => (
              <Card key={flight.id} className="hover:shadow-md transition-shadow">
                <CardHeader className="pb-3">
                  <div className="flex items-start justify-between">
                    <CardTitle className="text-base">{flight.name}</CardTitle>
                    <Badge variant="secondary">
                      {flight.playerCount} players
                    </Badge>
                  </div>
                  <CardDescription>
                    {flight.playerCount} players
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <Button variant="outline" size="sm" asChild className="w-full">
                    <Link to={`${prefix}/flights/${flight.id}`}>
                      View Leaderboard
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </section>

      {/* Older rounds (compact) */}
      {olderRounds.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="flex items-center gap-2 text-xl font-bold text-gray-900">
              <Calendar className="h-5 w-5 text-primary-700" />
              Recent Rounds
            </h2>
          </div>
          <div className="space-y-3">
            {olderRounds.map((round) => (
              <Link
                key={round.id}
                to={`${prefix}/rounds/${round.id}`}
                className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-5 py-4 hover:shadow-sm hover:border-primary-300 transition-all"
              >
                <div>
                  <p className="font-medium text-gray-900">{round.courseName}</p>
                  <p className="text-sm text-gray-500">
                    Week {round.weekNumber} &middot; {round.nineHoleSide} 9 &middot;{' '}
                    {formatShortDate(round.scheduledDate)}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <Badge variant={statusVariant(round.status)}>{round.status}</Badge>
                  <ArrowRight className="h-4 w-4 text-gray-400" />
                </div>
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
