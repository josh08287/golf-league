import { useMemo } from 'react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { ArrowLeft } from 'lucide-react';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '@/lib/utils';
import { useMatchPlayStandings, useFlight } from '@/hooks/useFlights';
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
import type { MatchPlayStanding, MatchPlayMatchResult } from '@/types/api';

type PodiumVariant = 'gold' | 'silver' | 'bronze';

function positionBadge(position: number) {
  if (position === 1) return <Badge variant="gold">1</Badge>;
  if (position === 2) return <Badge variant="silver">2</Badge>;
  if (position === 3) return <Badge variant="bronze">3</Badge>;
  return <span className="text-sm text-gray-600 font-medium">{position}</span>;
}

function podiumVariant(position: number): PodiumVariant | null {
  if (position === 1) return 'gold';
  if (position === 2) return 'silver';
  if (position === 3) return 'bronze';
  return null;
}

interface StandingsRowProps {
  standing: MatchPlayStanding;
  highlight: PodiumVariant | null;
}

function StandingsRow({ standing, highlight }: StandingsRowProps) {
  const prefix = useLeaguePrefix();
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
          to={`${prefix}/players/${standing.playerId}`}
          className="font-medium text-primary-900 hover:underline"
        >
          {standing.playerFullName}
        </Link>
      </TableCell>
      <TableCell className="text-center whitespace-nowrap" title={HANDICAP_PAIR_TOOLTIP}>
        {formatHandicapPair(standing.currentHandicapIndex)}
      </TableCell>
      <TableCell className="text-center">{standing.matchesPlayed}</TableCell>
      <TableCell className="text-center font-semibold">{standing.totalPoints}</TableCell>
      <TableCell className="text-center">{standing.averagePointsPerMatch.toFixed(1)}</TableCell>
      <TableCell className="text-center whitespace-nowrap tabular-nums">
        {standing.wins}-{standing.halves}-{standing.losses}
      </TableCell>
    </TableRow>
  );
}

function allWeeks(standings: MatchPlayStanding[]): number[] {
  const weeks = new Set<number>();
  for (const s of standings) {
    for (const r of s.matchResults) weeks.add(r.weekNumber);
  }
  return Array.from(weeks).sort((a, b) => a - b);
}

interface MatchesGridProps {
  standings: MatchPlayStanding[];
}

function MatchesGrid({ standings }: MatchesGridProps) {
  const weeks = useMemo(() => allWeeks(standings), [standings]);
  const sorted = useMemo(() => [...standings].sort((a, b) => a.position - b.position), [standings]);

  if (weeks.length === 0) return null;

  function matchCell(result: MatchPlayMatchResult | undefined) {
    if (!result) {
      return <td className="px-3 py-2 text-center text-gray-300 text-sm">—</td>;
    }
    if (result.wasBye) {
      return (
        <td className="px-3 py-2 text-center text-sm text-gray-400 italic" title={`Bye — scored against the card: ${result.playerPoints} pts`}>
          BYE ({result.playerPoints})
        </td>
      );
    }
    const opponentLabel = result.opponentFullName ?? 'opponent';
    const title = result.wasAgainstCard
      ? `vs. card (${opponentLabel} absent): ${result.playerPoints}-${result.opponentPoints}`
      : `vs. ${opponentLabel}: ${result.playerPoints}-${result.opponentPoints}`;
    return (
      <td className="px-3 py-2 text-center text-sm" title={title}>
        <span className="font-medium text-gray-900">{result.playerPoints}-{result.opponentPoints}</span>
        {result.wasAgainstCard && <span className="ml-1 text-xs text-amber-600">(card)</span>}
      </td>
    );
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-white overflow-x-auto">
      <div className="px-4 py-3 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-700">
          Match-by-Match Results
          <span className="ml-2 text-xs font-normal text-gray-400">
            (points won-lost per match · "card" = opponent absent/bye, scored vs. par)
          </span>
        </h3>
      </div>
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-gray-50 border-b border-gray-200">
            <th className="px-3 py-2 text-left font-medium text-gray-600 whitespace-nowrap">Player</th>
            {weeks.map((w) => (
              <th key={w} className="px-3 py-2 text-center font-medium text-gray-600 whitespace-nowrap">
                Wk {w}
              </th>
            ))}
            <th className="px-3 py-2 text-center font-medium text-gray-600 whitespace-nowrap">Total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {sorted.map((standing) => {
            const byWeek = new Map<number, MatchPlayMatchResult>(
              standing.matchResults.map((r) => [r.weekNumber, r]),
            );
            return (
              <tr key={standing.playerId} className="hover:bg-gray-50">
                <td className="px-3 py-2 whitespace-nowrap">
                  <span className="font-medium text-gray-900 text-sm">{standing.playerFullName}</span>
                </td>
                {weeks.map((w) => <span key={w}>{matchCell(byWeek.get(w))}</span>)}
                <td className="px-3 py-2 text-center font-semibold text-gray-900 text-sm">
                  {standing.totalPoints}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function MatchPlayLeaderboardPage() {
  const prefix = useLeaguePrefix();
  const { flightId } = useParams<{ flightId: string }>();
  const [searchParams] = useSearchParams();
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);

  const flight = useFlight(flightId ?? '');
  const flightData = flight.data;

  const halfFromQuery = searchParams.get('halfId');
  const halfId = halfFromQuery ?? (flightData ? String(flightData.halfId) : '');

  const { sort, cycle } = useSortableTable('matchPlayStandings');
  const standings = useMatchPlayStandings(flightId ?? '', halfId, sort);

  const halfLabel = useMemo(() => {
    if (!activeSeason) return '';
    const half = activeSeason.halves.find((h) => String(h.id) === halfId);
    return half?.name ?? '';
  }, [activeSeason, halfId]);

  const title = flightData?.name
    ? `${flightData.name} Match Play Leaderboard${halfLabel ? ` — ${halfLabel}` : ''}`
    : 'Match Play Leaderboard';

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="mb-4 -ml-2">
          <Link to={`${prefix}/flights`}>
            <ArrowLeft className="h-4 w-4 mr-1" />
            Back to Flights
          </Link>
        </Button>
        <PageHeader
          title={title}
          description={flightData ? `${flightData.playerCount} players` : undefined}
        />
      </div>

      {(standings.isPending || flight.isPending) && <FullPageSpinner />}
      {(standings.isError || flight.isError) && (
        <ErrorMessage message="Could not load standings. Please try again." />
      )}

      {standings.data && (
        <>
          {standings.data.length === 0 ? (
            <p className="text-gray-500 text-sm">No match play results available for this half yet.</p>
          ) : (
            <>
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
                        Hcp <span className="text-xs font-normal text-gray-400">18 / 9</span>
                      </SortableTableHead>
                      <SortableTableHead column="matches" sort={sort} onSort={cycle} className="text-center">
                        Matches
                      </SortableTableHead>
                      <SortableTableHead column="points" sort={sort} onSort={cycle} className="text-center">
                        Points
                      </SortableTableHead>
                      <SortableTableHead column="avg" sort={sort} onSort={cycle} className="text-center">
                        Avg Pts
                      </SortableTableHead>
                      <TableCell className="text-center font-medium text-gray-600">W-H-L</TableCell>
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

              <MatchesGrid standings={standings.data} />
            </>
          )}
        </>
      )}
    </div>
  );
}
