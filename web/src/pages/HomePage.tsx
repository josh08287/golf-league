import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useLeagueName, useLeaguePrefix } from '@/context/LeagueContext';
import { ArrowRight, Calendar, Edit3 } from 'lucide-react';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '@/lib/utils';
import { useFlights } from '@/hooks/useFlights';
import { useRounds, useRoundScorecards, useRoundSkins, useActiveRoundLeaderboardPresence } from '@/hooks/useRounds';
import { GrossPar3SkinsDisplay } from '@/components/GrossPar3SkinsDisplay';
import { useMyTodaysTeeTime } from '@/hooks/useTeeTimeScoreEntry';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { useAuthStore } from '@/store/authStore';
import { FEATURE_FLAG_KEYS } from '@/types/api';
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
              <TableHead className="w-7 text-center" rowSpan={2}>#</TableHead>
              <TableHead rowSpan={2}>Player</TableHead>
              <TableHead className="text-right" rowSpan={2}>HCP</TableHead>
              <TableHead className="text-center border-l border-gray-100" colSpan={2}>Strokes</TableHead>
              <TableHead className="text-center border-l border-gray-100" colSpan={2}>Points</TableHead>
            </TableRow>
            <TableRow className="bg-gray-50">
              <TableHead className="text-right border-l border-gray-100">Gross</TableHead>
              <TableHead className="text-right">Net</TableHead>
              <TableHead className="text-right border-l border-gray-100">Gross</TableHead>
              <TableHead className="text-right">Net</TableHead>
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
                <TableCell className="text-right tabular-nums border-l border-gray-100">{sc.grossScore ?? '—'}</TableCell>
                <TableCell className="text-right tabular-nums">{sc.netScore ?? '—'}</TableCell>
                <TableCell className="text-right tabular-nums border-l border-gray-100">{sc.grossPoints ?? '—'}</TableCell>
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

interface NotableEntry {
  playerId: number;
  playerName: string;
  value: number;
}

function lowEntries(scorecards: RoundScorecard[], selector: (sc: RoundScorecard) => number | null): NotableEntry[] {
  const withValue = scorecards
    .map((sc) => ({ sc, value: selector(sc) }))
    // A skipped week records 0 strokes, which would otherwise win "low" categories.
    .filter((x): x is { sc: RoundScorecard; value: number } => x.value != null && x.value > 0);
  if (withValue.length === 0) return [];
  const best = Math.min(...withValue.map((x) => x.value));
  return withValue
    .filter((x) => x.value === best)
    .map((x) => ({ playerId: x.sc.playerId, playerName: x.sc.playerName, value: x.value }));
}

function highEntries(scorecards: RoundScorecard[], selector: (sc: RoundScorecard) => number | null): NotableEntry[] {
  const withValue = scorecards
    .map((sc) => ({ sc, value: selector(sc) }))
    .filter((x): x is { sc: RoundScorecard; value: number } => x.value != null);
  if (withValue.length === 0) return [];
  const best = Math.max(...withValue.map((x) => x.value));
  return withValue
    .filter((x) => x.value === best)
    .map((x) => ({ playerId: x.sc.playerId, playerName: x.sc.playerName, value: x.value }));
}

interface BirdieEagleEntry {
  playerId: number;
  playerName: string;
  holeNumber: number;
  par: number;
  strokes: number;
  isEagle: boolean;
}

function findBirdiesAndEagles(scorecards: RoundScorecard[]): BirdieEagleEntry[] {
  const entries: BirdieEagleEntry[] = [];
  for (const sc of scorecards) {
    for (const hole of sc.holes) {
      const diff = hole.strokes - hole.par;
      if (diff <= -1) {
        entries.push({
          playerId: sc.playerId,
          playerName: sc.playerName,
          holeNumber: hole.holeNumber,
          par: hole.par,
          strokes: hole.strokes,
          isEagle: diff <= -2,
        });
      }
    }
  }
  return entries.sort((a, b) => {
    if (a.isEagle !== b.isEagle) return a.isEagle ? -1 : 1;
    return a.holeNumber - b.holeNumber;
  });
}

function formatEntries(entries: NotableEntry[]): string {
  return entries.map((e) => e.playerName).join(', ');
}

interface NotablesCardProps {
  scorecards: RoundScorecard[];
  seasonId: number;
  halfId: number;
}

function NotablesCard({ scorecards, seasonId, halfId }: NotablesCardProps) {
  const prefix = useLeaguePrefix();

  const lowGross = useMemo(() => lowEntries(scorecards, (sc) => sc.grossScore), [scorecards]);
  const lowNet = useMemo(() => lowEntries(scorecards, (sc) => sc.netScore), [scorecards]);
  const highGrossPoints = useMemo(() => highEntries(scorecards, (sc) => sc.grossPoints), [scorecards]);
  const highNetPoints = useMemo(() => highEntries(scorecards, (sc) => sc.netPoints), [scorecards]);
  const birdiesAndEagles = useMemo(() => findBirdiesAndEagles(scorecards), [scorecards]);

  if (
    lowGross.length === 0 &&
    lowNet.length === 0 &&
    highGrossPoints.length === 0 &&
    highNetPoints.length === 0 &&
    birdiesAndEagles.length === 0
  ) {
    return null;
  }

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Notables</CardTitle>
        <CardDescription>League-wide highlights for this round</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          {lowGross.length > 0 && (
            <div className="rounded-md bg-gray-50 px-3 py-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">Low Gross</p>
              <p className="text-sm font-semibold text-gray-900">{lowGross[0].value}</p>
              <p className="text-sm text-gray-600">{formatEntries(lowGross)}</p>
            </div>
          )}
          {lowNet.length > 0 && (
            <div className="rounded-md bg-gray-50 px-3 py-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">Low Net</p>
              <p className="text-sm font-semibold text-gray-900">{lowNet[0].value}</p>
              <p className="text-sm text-gray-600">{formatEntries(lowNet)}</p>
            </div>
          )}
          {highGrossPoints.length > 0 && (
            <div className="rounded-md bg-gray-50 px-3 py-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">High Gross Points</p>
              <p className="text-sm font-semibold text-gray-900">{highGrossPoints[0].value}</p>
              <p className="text-sm text-gray-600">{formatEntries(highGrossPoints)}</p>
            </div>
          )}
          {highNetPoints.length > 0 && (
            <div className="rounded-md bg-gray-50 px-3 py-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">High Net Points</p>
              <p className="text-sm font-semibold text-gray-900">{highNetPoints[0].value}</p>
              <p className="text-sm text-gray-600">{formatEntries(highNetPoints)}</p>
            </div>
          )}
        </div>

        {birdiesAndEagles.length > 0 && (
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-gray-500">
                Birdies &amp; Eagles
              </p>
              <ul className="space-y-1">
                {birdiesAndEagles.map((e, i) => (
                  <li key={`${e.playerId}-${e.holeNumber}-${i}`} className="flex items-center gap-2 text-sm">
                    <Badge variant={e.isEagle ? 'amber' : 'green'}>
                      {e.isEagle ? 'Eagle' : 'Birdie'}
                    </Badge>
                    <Link
                      to={`${prefix}/players/${e.playerId}`}
                      className="font-medium text-primary-900 hover:underline"
                    >
                      {e.playerName}
                    </Link>
                    <span className="text-gray-500">
                      Hole {e.holeNumber} (Par {e.par}) &middot; {e.strokes}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
            <Link
              to={`${prefix}/statistics?seasonId=${seasonId}&halfId=${halfId}`}
              className="flex shrink-0 items-center gap-1 whitespace-nowrap text-xs font-medium text-primary-900 hover:underline"
            >
              This half's stats <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </div>
        )}
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
        <GrossPar3SkinsDisplay grossPar3Skins={skins.data.grossPar3Skins} roundId={round.id} />
      )}

      {scorecards.data && (scorecards.data.data?.length ?? 0) > 0 && (
        <NotablesCard
          scorecards={scorecards.data.data}
          seasonId={round.seasonId}
          halfId={round.halfId}
        />
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
  // Most recent first so page 1 holds the latest rounds across all halves —
  // otherwise (default ascending) page 1 is the earliest rounds and the
  // featured round is stuck in the first half once the season grows past a page.
  const rounds = useRounds(1, { sortBy: 'date', sortDir: 'desc' });
  const isAuthed = useAuthStore((s) => !!s.user);
  const todaysTeeTime = useMyTodaysTeeTime(isAuthed);
  const featureFlags = useFeatureFlagStates();
  const leaderboardFlagEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.activeRoundLeaderboardEnabled] ?? false;
  const activeRoundLeaderboard = useActiveRoundLeaderboardPresence(leaderboardFlagEnabled);
  const showLeaderboardLink = leaderboardFlagEnabled && activeRoundLeaderboard.data != null;

  const allRounds = rounds.data?.data ?? [];
  const featured = pickFeaturedRound(allRounds);

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
          {showLeaderboardLink && (
            <Button
              className="bg-amber-400 text-primary-900 hover:bg-amber-300"
              asChild
            >
              <Link to={`${prefix}/leaderboard`}>Live Leaderboard</Link>
            </Button>
          )}
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

    </div>
  );
}
