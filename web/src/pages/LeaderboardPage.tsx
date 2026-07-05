import { Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { useActiveRoundLeaderboard } from '@/hooks/useRounds';
import { Badge } from '@/components/ui/Badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/Table';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { formatDate } from '@/lib/utils';
import { cn } from '@/lib/utils';
import { FEATURE_FLAG_KEYS } from '@/types/api';
import type { ActiveRoundLeaderboardFlight } from '@/types/api';

function FlightLeaderboardTable({ flight }: { flight: ActiveRoundLeaderboardFlight }) {
  const prefix = useLeaguePrefix();

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-bold text-gray-900">{flight.flightName || 'Flight'}</h2>
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <Table>
          <TableHeader>
            <TableRow className="bg-gray-50">
              <TableHead className="w-16 text-center">Rank</TableHead>
              <TableHead>Player</TableHead>
              <TableHead className="text-center">Thru</TableHead>
              <TableHead className="text-center">Gross</TableHead>
              <TableHead className="text-center">Net</TableHead>
              <TableHead className="text-center">Pts</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {flight.entries.map((entry) => (
              <TableRow
                key={entry.playerId}
                className={cn(entry.rank === 1 && 'bg-amber-50')}
              >
                <TableCell className="text-center font-semibold text-gray-700">
                  {entry.rank}
                </TableCell>
                <TableCell>
                  <Link
                    to={`${prefix}/players/${entry.playerId}`}
                    className="font-medium text-primary-900 hover:underline"
                  >
                    {entry.playerName}
                  </Link>
                </TableCell>
                <TableCell className="text-center text-sm text-gray-500">
                  {entry.thruHoles}
                </TableCell>
                <TableCell className="text-center">{entry.grossScore ?? '—'}</TableCell>
                <TableCell className="text-center">{entry.netScore ?? '—'}</TableCell>
                <TableCell className="text-center font-semibold text-[#1B5E20]">
                  {entry.netPoints ?? '—'}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

export function LeaderboardPage() {
  const featureFlags = useFeatureFlagStates();
  const flagEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.activeRoundLeaderboardEnabled] ?? false;
  const leaderboard = useActiveRoundLeaderboard(flagEnabled);

  if (featureFlags.isPending || (flagEnabled && leaderboard.isPending)) {
    return <FullPageSpinner />;
  }

  if (!flagEnabled) {
    return (
      <div className="space-y-6">
        <PageHeader title="Leaderboard" />
        <p className="text-gray-500 text-sm">The active round leaderboard is not currently enabled.</p>
      </div>
    );
  }

  if (leaderboard.isError) {
    return <ErrorMessage message="Could not load the leaderboard. Please try again." />;
  }

  const data = leaderboard.data;

  return (
    <div className="space-y-6">
      {data ? (
        <PageHeader
          title={data.courseName}
          description={`${formatDate(data.scheduledDate)} — ${data.nineHoleSide} 9`}
        >
          <Badge variant="amber">Live</Badge>
        </PageHeader>
      ) : (
        <PageHeader title="Leaderboard" />
      )}

      {!data && (
        <p className="text-gray-500 text-sm">No round is currently in progress.</p>
      )}

      {data && data.flights.length === 0 && (
        <p className="text-gray-500 text-sm">No scores have been entered for this round yet.</p>
      )}

      {data && data.flights.length > 0 && (
        <div className="space-y-8">
          {data.flights.map((flight) => (
            <FlightLeaderboardTable key={flight.flightId ?? 'none'} flight={flight} />
          ))}
        </div>
      )}
    </div>
  );
}
