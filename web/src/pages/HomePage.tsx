import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useLeagueName, useLeaguePrefix } from '@/context/LeagueContext';
import { ArrowRight, Edit3 } from 'lucide-react';
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
import { GolfLogo } from '@/components/ui/GolfLogo';
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus, formatNineHoleSide } from '@/lib/enumUtils';
import type { Flight, Round, RoundScorecard } from '@/types/api';

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
  const isToday = teeTime.roundDate.slice(0, 10) === new Date().toLocaleDateString('en-CA');

  return (
    <Card className="border-l-4 border-l-[#C89B3C] border-[#E6C775] bg-[#FAF6EC]">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2.5">
            <Edit3 className="h-4 w-4 text-[#7A520B]" />
            <div>
              <CardTitle className="text-base text-[#3D2904]">
                {isToday ? "Today's Round" : 'Previous Round'}: Enter Scores
              </CardTitle>
              <CardDescription className="text-[#7A520B]">
                {teeTime.courseName} &middot; {formatNineHoleSide(teeTime.nineHoleSide)} &middot; Tee Time: {teeTime.scheduledTimeFormatted}
              </CardDescription>
            </div>
          </div>
          <Badge variant="gold">{formattedDate}</Badge>
        </div>
      </CardHeader>
      <CardContent>
        {teeTime.canEnterScores ? (
          <Button
            variant="primary"
            asChild
            className="w-full bg-[#133A25] hover:bg-[#18452C] text-white"
          >
            <Link to={`/tee-times/${teeTime.teeTimeId}/enter-scores`}>
              Enter Scores for Your Group
            </Link>
          </Button>
        ) : (
          <div className="rounded-sm bg-[#F3E1B0]/60 px-3.5 py-2 text-sm text-[#6B4806]">
            This round has been finalized. Scores can no longer be edited.
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
    <Card className="overflow-hidden">
      <CardHeader className="py-4 px-5 bg-gray-50/70 border-b border-gray-200/80">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">{flightName}</CardTitle>
          <span className="font-mono text-xs text-gray-500">{sorted.length} players</span>
        </div>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        <Table>
          <TableHeader>
            <TableRow className="bg-gray-50/50">
              <TableHead className="w-9 text-center" rowSpan={2}>Pos</TableHead>
              <TableHead rowSpan={2}>Player</TableHead>
              <TableHead className="text-right" rowSpan={2}>HCP</TableHead>
              <TableHead className="text-center border-l border-gray-200/70" colSpan={2}>Strokes</TableHead>
              <TableHead className="text-center border-l border-gray-200/70" colSpan={2}>Points</TableHead>
            </TableRow>
            <TableRow className="bg-gray-50/50">
              <TableHead className="text-right border-l border-gray-200/70">Gross</TableHead>
              <TableHead className="text-right">Net</TableHead>
              <TableHead className="text-right border-l border-gray-200/70">Gross</TableHead>
              <TableHead className="text-right">Net</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.map((sc, i) => (
              <TableRow
                key={sc.playerId}
                className={i === 0 ? 'bg-[#FAF6EC]/45' : undefined}
              >
                <TableCell className="text-center font-mono text-xs font-semibold text-gray-500">
                  {i + 1}
                </TableCell>
                <TableCell>
                  <Link
                    to={`${prefix}/players/${sc.playerId}`}
                    className="font-semibold text-gray-900 hover:text-primary-900 hover:underline"
                  >
                    {sc.playerName}
                  </Link>
                </TableCell>
                <TableCell className="text-right text-gray-500 tabular-nums whitespace-nowrap" title="Course Handicap for this round">
                  {sc.courseHandicap}
                </TableCell>
                <TableCell className="text-right tabular-nums border-l border-gray-100 text-gray-700">{sc.grossScore ?? '—'}</TableCell>
                <TableCell className="text-right tabular-nums text-gray-700">{sc.netScore ?? '—'}</TableCell>
                <TableCell className="text-right tabular-nums border-l border-gray-100 text-gray-600">{sc.grossPoints ?? '—'}</TableCell>
                <TableCell className="text-right font-bold tabular-nums text-primary-900">
                  {sc.netPoints ?? '—'}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <div className="border-t border-gray-100 bg-gray-50/30 px-5 py-2.5 flex items-center justify-between">
          <span className="text-xs text-gray-400">Net Stableford Standings</span>
          <Link
            to={`${prefix}/flights/${flightId}`}
            className="inline-flex items-center gap-1 text-xs font-semibold text-primary-900 hover:underline"
          >
            Season standings <ArrowRight className="h-3.5 w-3.5" />
          </Link>
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

interface BirdiesAndEaglesCardProps {
  entries: BirdieEagleEntry[];
  seasonId: number;
  halfId: number;
}

function BirdiesAndEaglesCard({ entries, seasonId, halfId }: BirdiesAndEaglesCardProps) {
  const prefix = useLeaguePrefix();

  if (entries.length === 0) return null;

  return (
    <Card className="h-full flex flex-col justify-between">
      <CardHeader className="pb-4 border-b border-gray-100">
        <div className="flex items-start justify-between gap-2">
          <div>
            <CardTitle className="text-lg">Birdies &amp; Eagles</CardTitle>
            <CardDescription className="text-xs mt-0.5">
              Under-par hole scores recorded this round
            </CardDescription>
          </div>
          <Link
            to={`${prefix}/statistics?seasonId=${seasonId}&halfId=${halfId}`}
            className="inline-flex items-center gap-1 text-xs font-semibold text-primary-900 hover:underline shrink-0"
          >
            Half Stats <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        <ul className="divide-y divide-gray-100 max-h-[260px] overflow-y-auto">
          {entries.map((e, i) => (
            <li
              key={`${e.playerId}-${e.holeNumber}-${i}`}
              className="flex items-center justify-between gap-3 py-2.5 text-sm"
            >
              <div className="flex items-center gap-2.5 min-w-0">
                <Badge variant={e.isEagle ? 'gold' : 'green'}>
                  {e.isEagle ? 'Eagle' : 'Birdie'}
                </Badge>
                <Link
                  to={`${prefix}/players/${e.playerId}`}
                  className="font-semibold text-gray-900 hover:text-primary-900 hover:underline truncate"
                >
                  {e.playerName}
                </Link>
              </div>
              <span className="font-mono text-xs text-gray-500 shrink-0">
                Hole {e.holeNumber} (Par {e.par}) &middot;{' '}
                <strong className="text-gray-900 font-bold">{e.strokes}</strong>
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

interface FeaturedRoundWorkspaceProps {
  round: Round;
  leagueName: string;
  showLeaderboardLink: boolean;
  hasTodaysTeeTime: boolean;
  todaysTeeTimeData?: import('@/types/api').MyTodaysTeeTime | null;
}

function FeaturedRoundWorkspace({
  round,
  leagueName,
  showLeaderboardLink,
  hasTodaysTeeTime,
  todaysTeeTimeData,
}: FeaturedRoundWorkspaceProps) {
  const prefix = useLeaguePrefix();
  const scorecards = useRoundScorecards(String(round.id));
  const skins = useRoundSkins(String(round.id));
  const flights = useFlights();

  const scorecardList = useMemo(() => scorecards.data?.data ?? [], [scorecards.data]);

  const lowGross = useMemo(() => lowEntries(scorecardList, (sc) => sc.grossScore), [scorecardList]);
  const lowNet = useMemo(() => lowEntries(scorecardList, (sc) => sc.netScore), [scorecardList]);
  const highGrossPoints = useMemo(() => highEntries(scorecardList, (sc) => sc.grossPoints), [scorecardList]);
  const highNetPoints = useMemo(() => highEntries(scorecardList, (sc) => sc.netPoints), [scorecardList]);
  const birdiesAndEagles = useMemo(() => findBirdiesAndEagles(scorecardList), [scorecardList]);

  const hasNotableStats =
    lowGross.length > 0 ||
    lowNet.length > 0 ||
    highGrossPoints.length > 0 ||
    highNetPoints.length > 0;

  const cards = scorecardList.reduce<Map<number, RoundScorecard[]>>(
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

  const hasSkins = !!skins.data?.grossPar3Skins;
  const hasBirdies = birdiesAndEagles.length > 0;

  return (
    <div className="space-y-6">
      {/* Architectural Clubhouse Round Spotlight + Flush Notable Stat Ledger */}
      <section className="overflow-hidden rounded-md border border-[#0B2316] bg-[#133A25] text-white shadow-sm">
        <div className="p-6 sm:p-8 flex flex-col lg:flex-row lg:items-end lg:justify-between gap-6">
          <div className="space-y-2.5 max-w-2xl">
            <div className="border-l-2 border-[#E5B84B] pl-3 font-mono text-xs font-semibold uppercase tracking-widest text-[#F3D078]">
              {leagueName} &bull; Week {round.weekNumber} {round.status} &bull; {formatNineHoleSide(round.nineHoleSide)} &bull; {formatShortDate(round.scheduledDate)}
            </div>

            <div>
              <Link
                to={`${prefix}/rounds/${round.id}`}
                className="group inline-flex items-center gap-3 hover:opacity-95 transition-opacity"
              >
                <h1 className="font-sans text-3xl sm:text-4xl font-extrabold tracking-tight text-white leading-tight">
                  {round.courseName}
                </h1>
                <ArrowRight className="h-5 w-5 text-[#E5B84B] transition-transform group-hover:translate-x-1 shrink-0" />
              </Link>
              <p className="mt-1 text-sm text-emerald-100/80">
                Official round results, Par 3 Skins payouts, notables, and flight scorecards.
              </p>
            </div>
          </div>

          {/* Action Bar */}
          <div className="flex flex-wrap items-center gap-2 shrink-0">
            {showLeaderboardLink && (
              <Button
                className="bg-[#E5B84B] text-[#112A1B] hover:bg-[#ebb129] font-bold"
                asChild
              >
                <Link to={`${prefix}/leaderboard`} className="flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-[#112A1B] animate-pulse" />
                  Live Leaderboard
                </Link>
              </Button>
            )}
            <Button
              className="bg-white text-[#133A25] hover:bg-[#F7F6F2] font-semibold"
              asChild
            >
              <Link to={`${prefix}/rounds/${round.id}`}>
                Round Scorecard &rarr;
              </Link>
            </Button>
            <Button
              className="bg-transparent hover:bg-white/10 text-white border border-white/25"
              asChild
            >
              <Link to={`${prefix}/flights`}>Standings</Link>
            </Button>
            <Button
              className="bg-transparent hover:bg-white/10 text-white border border-white/25"
              asChild
            >
              <Link to={`${prefix}/rounds`}>All Rounds</Link>
            </Button>
          </div>
        </div>

        {/* Flush Architectural 4-Column Stat Ledger */}
        {hasNotableStats && (
          <div className="bg-[#0E2C1B] border-t border-white/15 grid grid-cols-2 lg:grid-cols-4 divide-x divide-y lg:divide-y-0 divide-white/10">
            {lowGross.length > 0 && (
              <div className="px-6 py-4">
                <p className="text-[10px] font-semibold uppercase tracking-widest text-emerald-200/70">
                  Low Gross
                </p>
                <div className="mt-1 flex items-baseline justify-between gap-2">
                  <span className="font-mono text-2xl sm:text-3xl font-bold text-white tabular-nums">
                    {lowGross[0].value}
                  </span>
                  <span className="text-xs font-semibold text-[#F3D078] text-right truncate">
                    {formatEntries(lowGross)}
                  </span>
                </div>
              </div>
            )}
            {lowNet.length > 0 && (
              <div className="px-6 py-4">
                <p className="text-[10px] font-semibold uppercase tracking-widest text-emerald-200/70">
                  Low Net
                </p>
                <div className="mt-1 flex items-baseline justify-between gap-2">
                  <span className="font-mono text-2xl sm:text-3xl font-bold text-white tabular-nums">
                    {lowNet[0].value}
                  </span>
                  <span className="text-xs font-medium text-emerald-100 text-right truncate">
                    {formatEntries(lowNet)}
                  </span>
                </div>
              </div>
            )}
            {highGrossPoints.length > 0 && (
              <div className="px-6 py-4">
                <p className="text-[10px] font-semibold uppercase tracking-widest text-emerald-200/70">
                  High Gross Points
                </p>
                <div className="mt-1 flex items-baseline justify-between gap-2">
                  <span className="font-mono text-2xl sm:text-3xl font-bold text-white tabular-nums">
                    {highGrossPoints[0].value}
                  </span>
                  <span className="text-xs font-semibold text-[#F3D078] text-right truncate">
                    {formatEntries(highGrossPoints)}
                  </span>
                </div>
              </div>
            )}
            {highNetPoints.length > 0 && (
              <div className="px-6 py-4">
                <p className="text-[10px] font-semibold uppercase tracking-widest text-emerald-200/70">
                  High Net Points
                </p>
                <div className="mt-1 flex items-baseline justify-between gap-2">
                  <span className="font-mono text-2xl sm:text-3xl font-bold text-white tabular-nums">
                    {highNetPoints[0].value}
                  </span>
                  <span className="text-xs font-medium text-emerald-100 text-right truncate">
                    {formatEntries(highNetPoints)}
                  </span>
                </div>
              </div>
            )}
          </div>
        )}
      </section>

      {/* Today's Tee Time Score Entry Card */}
      {hasTodaysTeeTime && todaysTeeTimeData && (
        <TodaysTeeTimeCard teeTime={todaysTeeTimeData} />
      )}

      {/* 2-Column Highlight Row: Par 3 Gross Skins + Birdies & Eagles */}
      {(hasSkins || hasBirdies) && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
          {hasSkins && (
            <div className={hasBirdies ? 'lg:col-span-7' : 'lg:col-span-12'}>
              <GrossPar3SkinsDisplay
                grossPar3Skins={skins.data!.grossPar3Skins!}
                roundId={round.id}
              />
            </div>
          )}
          {hasBirdies && (
            <div className={hasSkins ? 'lg:col-span-5' : 'lg:col-span-12'}>
              <BirdiesAndEaglesCard
                entries={birdiesAndEagles}
                seasonId={round.seasonId}
                halfId={round.halfId}
              />
            </div>
          )}
        </div>
      )}

      {/* Flight Scorecards Section */}
      <section className="space-y-4">
        <div className="flex items-end justify-between border-b border-gray-200 pb-3">
          <div>
            <h2 className="font-sans text-2xl font-bold tracking-tight text-gray-900">
              Flight Scorecards
            </h2>
            <p className="text-xs text-gray-500 mt-0.5">
              Week {round.weekNumber} standings by flight at {round.courseName}
            </p>
          </div>
          <Link
            to={`${prefix}/flights`}
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary-900 hover:underline"
          >
            All Flights <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>

        {scorecards.isPending && (
          <div className="flex justify-center py-8">
            <Spinner />
          </div>
        )}
        {scorecards.isError && (
          <ErrorMessage message="Could not load scorecards for this round." />
        )}
        {scorecards.data && orderedFlightIds.length === 0 && (
          <Card>
            <CardContent className="py-8 text-center text-gray-500 text-sm">
              No scores recorded for this round yet.
            </CardContent>
          </Card>
        )}
        {scorecards.data && orderedFlightIds.length > 0 && (
          <div className="grid gap-6 lg:grid-cols-2">
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
      </section>
    </div>
  );
}

export function HomePage() {
  const leagueName = useLeagueName();
  const prefix = useLeaguePrefix();
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

  if (rounds.isPending) {
    return (
      <div className="flex justify-center py-16">
        <Spinner />
      </div>
    );
  }

  if (rounds.isError) {
    return <ErrorMessage message="Could not load rounds. Please try again." />;
  }

  if (featured) {
    return (
      <FeaturedRoundWorkspace
        round={featured}
        leagueName={leagueName}
        showLeaderboardLink={showLeaderboardLink}
        hasTodaysTeeTime={hasTodaysTeeTime}
        todaysTeeTimeData={todaysTeeTime.data}
      />
    );
  }

  // Fallback when no rounds have been created yet
  return (
    <div className="space-y-8">
      <section className="rounded-md border border-[#0B2316] bg-[#133A25] px-8 py-12 text-white text-center shadow-sm">
        <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded bg-white/10 border border-white/15">
          <GolfLogo size="md" />
        </div>
        <h1 className="font-sans text-4xl font-extrabold tracking-tight sm:text-5xl">
          {leagueName}
        </h1>
        <p className="mt-4 text-emerald-100/80 text-lg max-w-xl mx-auto">
          Track standings, scores, and handicaps all season long.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          {showLeaderboardLink && (
            <Button
              className="bg-[#E5B84B] text-[#112A1B] hover:bg-[#ebb129] font-bold"
              asChild
            >
              <Link to={`${prefix}/leaderboard`}>Live Leaderboard</Link>
            </Button>
          )}
          <Button
            className="bg-white text-[#133A25] hover:bg-[#F7F6F2]"
            asChild
          >
            <Link to={`${prefix}/flights`}>View Flights</Link>
          </Button>
          <Button
            className="bg-transparent hover:bg-white/10 text-white border border-white/25"
            asChild
          >
            <Link to={`${prefix}/rounds`}>Latest Rounds</Link>
          </Button>
        </div>
      </section>

      {hasTodaysTeeTime && (
        <TodaysTeeTimeCard teeTime={todaysTeeTime.data!} />
      )}

      <Card>
        <CardContent className="py-10 text-center text-gray-500 text-sm">
          No rounds played yet this season.
        </CardContent>
      </Card>
    </div>
  );
}
