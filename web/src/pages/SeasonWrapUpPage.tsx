import { useEffect } from 'react';
import { Trophy, Award, Medal } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { useSeasons } from '@/hooks/useSeasons';
import { useSeasonWrapUp } from '@/hooks/useStatistics';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
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
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import type { PlayerScore, SingleRoundPoints } from '@/types/api';

function PositionBadge({ position }: { position: number }) {
  if (position === 1) return <Badge variant="gold">1st</Badge>;
  if (position === 2) return <Badge variant="silver">2nd</Badge>;
  return <Badge variant="neutral">{position}</Badge>;
}

function SeasonPositionsTable({
  title,
  entries,
  prefix,
}: {
  title: string;
  entries: PlayerScore[];
  prefix: string;
}) {
  return (
    <div>
      <h3 className="mb-2 text-sm font-semibold text-gray-700">{title}</h3>
      <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-gray-50">
              <TableHead className="w-16 text-center">Pos</TableHead>
              <TableHead>Player</TableHead>
              <TableHead className="text-right">Avg</TableHead>
              <TableHead className="text-right">Rounds</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="text-center text-gray-400">No data yet</TableCell>
              </TableRow>
            ) : (
              entries.map((entry, i) => (
                <TableRow key={entry.playerId}>
                  <TableCell className="text-center"><PositionBadge position={i + 1} /></TableCell>
                  <TableCell>
                    <Link to={`${prefix}/players/${entry.playerId}`} className="font-medium text-primary-900 hover:underline">
                      {entry.playerName}
                    </Link>
                  </TableCell>
                  <TableCell className="text-right tabular-nums font-semibold">{entry.value.toFixed(1)}</TableCell>
                  <TableCell className="text-right tabular-nums text-gray-500">{entry.roundsPlayed}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

function HighPointsCard({
  title,
  entry,
  prefix,
}: {
  title: string;
  entry: SingleRoundPoints | null;
  prefix: string;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-gray-500">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        {entry ? (
          <div className="flex items-center justify-between">
            <div>
              <Link
                to={`${prefix}/players/${entry.playerId}`}
                className="font-medium text-primary-900 hover:underline"
              >
                {entry.playerName}
              </Link>
              <p className="text-xs text-gray-400 mt-0.5">
                Week {entry.weekNumber} · {entry.roundDate}
              </p>
            </div>
            <span className="text-2xl font-bold text-primary-900 tabular-nums">{entry.points}</span>
          </div>
        ) : (
          <span className="text-sm text-gray-400">No data yet</span>
        )}
      </CardContent>
    </Card>
  );
}

export function SeasonWrapUpPage() {
  const prefix = useLeaguePrefix();
  const seasons = useSeasons();
  const [searchParams, setSearchParams] = useSearchParams();

  const orderedSeasons = seasons.data ? [...seasons.data].sort((a, b) => b.year - a.year) : [];

  const seasonIdParam = searchParams.get('seasonId');
  const selectedSeasonId = seasonIdParam ? Number(seasonIdParam) : null;

  useEffect(() => {
    if (selectedSeasonId == null && orderedSeasons.length > 0) {
      const params = new URLSearchParams(searchParams);
      params.set('seasonId', String(orderedSeasons[0].id));
      setSearchParams(params, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderedSeasons.length, selectedSeasonId]);

  const setSelectedSeasonId = (seasonId: number) => {
    const params = new URLSearchParams(searchParams);
    params.set('seasonId', String(seasonId));
    setSearchParams(params, { replace: true });
  };

  const wrapUp = useSeasonWrapUp(selectedSeasonId);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Season Wrap-Up"
        description="Half and season awards across every flight"
      >
        <Trophy className="h-6 w-6 text-primary-700" />
      </PageHeader>

      {seasons.isPending && <FullPageSpinner />}
      {seasons.isError && <ErrorMessage message="Could not load seasons." />}

      {orderedSeasons.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {orderedSeasons.map((s) => (
            <button
              key={s.id}
              onClick={() => setSelectedSeasonId(s.id)}
              className={[
                'rounded-full px-4 py-1.5 text-sm font-medium border transition-colors',
                selectedSeasonId === s.id
                  ? 'bg-primary-900 text-white border-primary-900'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-primary-500',
              ].join(' ')}
            >
              {s.name}
            </button>
          ))}
        </div>
      )}

      {selectedSeasonId != null && (
        <>
          {wrapUp.isPending && <FullPageSpinner />}
          {wrapUp.isError && <ErrorMessage message="Could not load the season wrap-up." />}

          {wrapUp.data && (
            <>
              {/* Most Improved */}
              <Card className="border-amber-200 bg-gradient-to-br from-amber-50 to-white">
                <CardHeader className="pb-2">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <Award className="h-5 w-5 text-amber-500" />
                    Season-Long Most Improved
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {wrapUp.data.mostImproved ? (
                    <div className="flex items-center justify-between">
                      <div>
                        <Link
                          to={`${prefix}/players/${wrapUp.data.mostImproved.playerId}`}
                          className="text-xl font-bold text-primary-900 hover:underline"
                        >
                          {wrapUp.data.mostImproved.playerName}
                        </Link>
                        <p className="text-sm text-gray-500 mt-0.5">
                          {wrapUp.data.mostImproved.roundsPlayedInHalf} rounds played
                        </p>
                      </div>
                      <div className="text-right">
                        <span className="text-2xl font-bold text-green-700 tabular-nums">
                          {wrapUp.data.mostImproved.improvementFactor.toFixed(3)}
                        </span>
                        <p className="text-xs text-gray-400 mt-1">
                          HI: {wrapUp.data.mostImproved.startingHandicapIndex.toFixed(1)} → {wrapUp.data.mostImproved.currentHandicapIndex.toFixed(1)}
                        </p>
                      </div>
                    </div>
                  ) : (
                    <p className="text-sm text-gray-500">Not enough data yet for Most Improved this season.</p>
                  )}
                </CardContent>
              </Card>

              {/* Season-wide low gross / low net, positions 1 and 2 */}
              <div>
                <h2 className="mb-3 flex items-center gap-2 text-lg font-semibold text-gray-900">
                  <Medal className="h-5 w-5 text-primary-700" />
                  Season Overall — Positions 1 &amp; 2
                </h2>
                <div className="grid gap-6 lg:grid-cols-2">
                  <SeasonPositionsTable title="Low Gross" entries={wrapUp.data.seasonLowGross} prefix={prefix} />
                  <SeasonPositionsTable title="Low Net" entries={wrapUp.data.seasonLowNet} prefix={prefix} />
                </div>
                <div className="mt-4 grid gap-4 sm:grid-cols-2">
                  <HighPointsCard
                    title="High Gross Stableford Points (Single Round)"
                    entry={wrapUp.data.seasonHighGrossPoints}
                    prefix={prefix}
                  />
                  <HighPointsCard
                    title="High Net Stableford Points (Single Round)"
                    entry={wrapUp.data.seasonHighNetPoints}
                    prefix={prefix}
                  />
                </div>
              </div>

              {/* Per-half breakdown */}
              {wrapUp.data.halves.map((half) => (
                <div key={half.halfId} className="space-y-4">
                  <h2 className="text-lg font-semibold text-gray-900">{half.halfName}</h2>

                  {/* Per-flight net/gross winners */}
                  <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-gray-50">
                          <TableHead>Flight</TableHead>
                          <TableHead>Net Winner</TableHead>
                          <TableHead>Gross Winner</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {half.flightWinners.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={3} className="text-center text-gray-400">No flights configured</TableCell>
                          </TableRow>
                        ) : (
                          half.flightWinners.map((fw) => (
                            <TableRow key={fw.flightId}>
                              <TableCell className="font-medium text-gray-700">{fw.flightName}</TableCell>
                              <TableCell>
                                {fw.netWinner ? (
                                  <Link to={`${prefix}/players/${fw.netWinner.playerId}`} className="font-medium text-primary-900 hover:underline">
                                    {fw.netWinner.playerName}
                                  </Link>
                                ) : (
                                  <span className="text-sm text-gray-400">—</span>
                                )}
                              </TableCell>
                              <TableCell>
                                {fw.grossWinner ? (
                                  <Link to={`${prefix}/players/${fw.grossWinner.playerId}`} className="font-medium text-primary-900 hover:underline">
                                    {fw.grossWinner.playerName}
                                  </Link>
                                ) : (
                                  <span className="text-sm text-gray-400">—</span>
                                )}
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                    </Table>
                  </div>
                </div>
              ))}
            </>
          )}
        </>
      )}
    </div>
  );
}
