import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, ChevronDown, Clock, Trophy } from 'lucide-react';
import * as Accordion from '@radix-ui/react-accordion';
import { useRound, useRoundScorecards, useRoundSkins } from '@/hooks/useRounds';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
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
import { normalizeRoundStatus } from '@/lib/enumUtils';
import type { RoundScorecard, RoundScorecardHole, RoundStatus, FlightSkins, HoleSkin } from '@/types/api';

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

function holeScoreClass(hole: RoundScorecardHole): string {
  const diff = hole.strokes - hole.par;
  if (diff <= -2) return 'bg-yellow-400 text-yellow-900 font-bold';
  if (diff === -1) return 'bg-green-500 text-white font-semibold';
  if (diff === 0)  return 'bg-white text-gray-800';
  if (diff === 1)  return 'bg-gray-200 text-gray-700';
  return 'bg-red-500 text-white font-semibold';
}

function HoleScoreCell({ hole, skin }: { hole: RoundScorecardHole; skin?: { skinValue: number; wasCarryover: boolean } }) {
  return (
    <td
      className={cn('px-2 py-2 text-center text-xs rounded relative', holeScoreClass(hole))}
      title={`Hole ${hole.holeNumber}: par ${hole.par}${skin ? ` — Skin worth ${skin.skinValue}` : ''}`}
    >
      <span className="flex items-center justify-center gap-0.5">
        {hole.strokes}
        {skin && skin.skinValue > 0 && (
          <Trophy className={cn('h-3 w-3', skin.wasCarryover ? 'text-amber-600' : 'text-amber-500')} />
        )}
      </span>
    </td>
  );
}

interface ScorecardTableProps {
  scorecard: RoundScorecard;
  flightSkins?: FlightSkins;
}

function ScorecardTable({ scorecard, flightSkins }: ScorecardTableProps) {
  const holes = [...scorecard.holes].sort((a, b) => a.holeNumber - b.holeNumber);

  // Build a lookup of skins won by this player per hole
  const playerSkinsByHole = new Map<number, { skinValue: number; wasCarryover: boolean }>();
  if (flightSkins) {
    const playerSummary = flightSkins.playerSummaries.find(p => p.playerId === scorecard.playerId);
    if (playerSummary) {
      for (const holeWon of playerSummary.holesWon) {
        playerSkinsByHole.set(holeWon.holeNumber, {
          skinValue: holeWon.skinValue,
          wasCarryover: holeWon.wasCarryover,
        });
      }
    }
  }

  // Build a lookup of all hole results to show carryover indicators
  const allHoleResults = new Map<number, HoleSkin>();
  if (flightSkins) {
    for (const holeResult of flightSkins.allHoleResults) {
      allHoleResults.set(holeResult.holeNumber, holeResult);
    }
  }

  return (
    <div className="space-y-3 overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="bg-gray-50">
            <TableHead className="w-32">Hole</TableHead>
            {holes.map((h) => {
              const holeResult = allHoleResults.get(h.holeNumber);
              const isCarryover = holeResult && holeResult.skinValue === 0;
              return (
                <TableHead key={h.holeNumber} className={cn('text-center px-2', isCarryover && 'text-amber-600')}>
                  {h.holeNumber}
                  {isCarryover && <span className="text-xs ml-0.5">↻</span>}
                </TableHead>
              );
            })}
            <TableHead className="text-center px-2">Total</TableHead>
          </TableRow>
          <TableRow className="bg-gray-50 text-xs text-gray-400">
            <TableHead>Par</TableHead>
            {holes.map((h) => (
              <TableHead key={h.holeNumber} className="text-center px-2 font-normal">
                {h.par}
              </TableHead>
            ))}
            <TableHead className="text-center px-2 font-normal">
              {holes.reduce((s, h) => s + h.par, 0)}
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell className="text-xs font-medium text-gray-600">Gross</TableCell>
            {holes.map((h) => (
              <HoleScoreCell
                key={h.holeNumber}
                hole={h}
                skin={playerSkinsByHole.get(h.holeNumber)}
              />
            ))}
            <TableCell className="text-center font-semibold">
              {holes.reduce((s, h) => s + h.strokes, 0)}
            </TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="text-xs font-medium text-gray-600">Net</TableCell>
            {holes.map((h) => (
              <td key={h.holeNumber} className="px-2 py-2 text-center text-xs text-gray-500">
                {h.netStrokes}
              </td>
            ))}
            <TableCell className="text-center text-sm text-gray-500">
              {holes.reduce((s, h) => s + h.netStrokes, 0)}
            </TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="text-xs font-medium text-blue-700">Gross Pts</TableCell>
            {holes.map((h) => (
              <td key={h.holeNumber} className="px-2 py-2 text-center text-xs text-blue-700">
                {h.grossPoints}
              </td>
            ))}
            <TableCell className="text-center text-sm font-semibold text-blue-700">
              {holes.reduce((s, h) => s + h.grossPoints, 0)}
            </TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="text-xs font-medium text-[#1B5E20]">Net Pts</TableCell>
            {holes.map((h) => (
              <td key={h.holeNumber} className="px-2 py-2 text-center text-xs text-[#1B5E20]">
                {h.netPoints}
              </td>
            ))}
            <TableCell className="text-center text-sm font-semibold text-[#1B5E20]">
              {holes.reduce((s, h) => s + h.netPoints, 0)}
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
      <div className="flex flex-wrap gap-4 text-sm text-gray-600 pt-1">
        <span>
          Gross: <strong>{scorecard.grossScore ?? '—'}</strong>
        </span>
        <span>
          Net: <strong>{scorecard.netScore ?? '—'}</strong>
        </span>
        <span>
          Gross Pts: <strong>{scorecard.grossPoints ?? '—'}</strong>
        </span>
        <span>
          Net Pts: <strong>{scorecard.netPoints ?? '—'}</strong>
        </span>
        <span>
          9H CH: <strong>{scorecard.courseHandicap}</strong>
        </span>
        {flightSkins && playerSkinsByHole.size > 0 && (
          <span className="text-amber-700">
            <Trophy className="inline h-4 w-4 mr-1" />
            Skins: <strong>{playerSkinsByHole.size}</strong> (value: {Array.from(playerSkinsByHole.values()).reduce((s, h) => s + h.skinValue, 0)})
          </span>
        )}
      </div>
    </div>
  );
}

interface FlightSkinsSummaryProps {
  flightSkins: FlightSkins;
}

function FlightSkinsSummary({ flightSkins }: FlightSkinsSummaryProps) {
  if (flightSkins.playerSummaries.length === 0) {
    return (
      <div className="text-sm text-gray-500 italic">
        No skins awarded — all holes were tied.
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Trophy className="h-5 w-5 text-amber-500" />
          <span className="font-semibold text-gray-900">Skins</span>
        </div>
        <span className="text-sm text-gray-500">
          {flightSkins.totalHolesWithSkins} holes awarded • {flightSkins.totalSkinValueAwarded} total value
        </span>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {flightSkins.playerSummaries.map((player) => (
          <div
            key={player.playerId}
            className="flex items-center justify-between bg-amber-50 border border-amber-200 rounded-lg px-3 py-2"
          >
            <div className="flex items-center gap-2">
              <Trophy className="h-4 w-4 text-amber-500" />
              <Link
                to={`/players/${player.playerId}`}
                className="font-medium text-gray-900 hover:underline"
                onClick={(e) => e.stopPropagation()}
              >
                {player.playerName}
              </Link>
            </div>
            <div className="text-sm">
              <span className="font-semibold text-amber-700">{player.totalSkinsWon}</span>
              <span className="text-gray-500 ml-1">({player.totalSkinValue})</span>
            </div>
          </div>
        ))}
      </div>
      <div className="text-xs text-gray-500">
        {flightSkins.allHoleResults.filter(h => h.skinValue === 0).length > 0 && (
          <span>
            ↻ indicates holes with ties (skins carried over)
          </span>
        )}
      </div>
    </div>
  );
}

export function RoundDetailPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const navigate = useNavigate();
  const round = useRound(roundId ?? '');
  const scorecards = useRoundScorecards(roundId ?? '');
  const skins = useRoundSkins(roundId ?? '');

  // Group scorecards by flight
  const scorecardsByFlight = scorecards.data?.data.reduce((acc, sc) => {
    const flightKey = sc.flightId;
    if (!acc.has(flightKey)) {
      acc.set(flightKey, {
        flightId: sc.flightId,
        flightName: sc.flightName || `Flight ${sc.flightId}`,
        scorecards: [],
      });
    }
    acc.get(flightKey)!.scorecards.push(sc);
    return acc;
  }, new Map<number, { flightId: number; flightName: string; scorecards: RoundScorecard[] }>());

  // Build skins lookup by flight
  const skinsByFlight = skins.data?.flightSkins.reduce((acc, fs) => {
    acc.set(fs.flightId, fs);
    return acc;
  }, new Map<number, FlightSkins>());

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to="/rounds">
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back to Rounds
        </Link>
      </Button>

      {round.isPending && <FullPageSpinner />}
      {round.isError && (
        <ErrorMessage message="Could not load round details. Please try again." />
      )}

      {round.data && (
        <PageHeader
          title={round.data.courseName}
          description={`${formatDate(round.data.scheduledDate)} — Week ${round.data.weekNumber} — ${round.data.nineHoleSide} 9`}
        >
          <div className="flex items-center gap-2">
            <Badge variant={statusVariant(round.data.status)}>{round.data.status}</Badge>
            {round.data.status === 'Scheduled' && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => navigate(`/rounds/${roundId}/tee-times`)}
              >
                <Clock className="h-4 w-4 mr-1" />
                Tee Times
              </Button>
            )}
          </div>
        </PageHeader>
      )}

      {scorecards.isPending && (
        <div className="flex justify-center py-8">
          <FullPageSpinner />
        </div>
      )}
      {scorecards.isError && <ErrorMessage message="Could not load scorecards." />}
      {skins.isError && <ErrorMessage message="Could not load skins data." />}

      {scorecards.data && (
        <>
          {scorecards.data.data.length === 0 ? (
            <p className="text-gray-500 text-sm">
              No scorecards have been entered for this round yet.
            </p>
          ) : (
            <div className="space-y-8">
              {Array.from(scorecardsByFlight?.values() ?? []).map((flightGroup) => {
                const flightSkins = skinsByFlight?.get(flightGroup.flightId);

                return (
                  <div key={flightGroup.flightId} className="space-y-4">
                    {/* Flight Header */}
                    <div className="flex items-center justify-between border-b border-gray-200 pb-2">
                      <h2 className="text-lg font-bold text-gray-900">
                        {flightGroup.flightName}
                      </h2>
                      <span className="text-sm text-gray-500">
                        {flightGroup.scorecards.length} player{flightGroup.scorecards.length !== 1 ? 's' : ''}
                      </span>
                    </div>

                    {/* Skins Summary (if data available) */}
                    {skins.data && flightSkins && (
                      <div className="bg-gray-50 rounded-lg p-4">
                        <FlightSkinsSummary flightSkins={flightSkins} />
                      </div>
                    )}

                    {/* Scorecards for this flight */}
                    <Accordion.Root type="multiple" className="space-y-2">
                      {flightGroup.scorecards.map((sc) => (
                        <Accordion.Item
                          key={sc.playerId}
                          value={`${flightGroup.flightId}-${sc.playerId}`}
                          className="rounded-lg border border-gray-200 bg-white overflow-hidden"
                        >
                          <Accordion.Header>
                            <Accordion.Trigger className="flex w-full items-center justify-between px-5 py-4 text-left hover:bg-gray-50 transition-colors group">
                              <div className="flex items-center gap-3">
                                <Link
                                  to={`/players/${sc.playerId}`}
                                  className="font-semibold text-primary-900 hover:underline"
                                  onClick={(e) => e.stopPropagation()}
                                >
                                  {sc.playerName}
                                </Link>
                                <span className="text-sm text-gray-500 whitespace-nowrap">
                                  9H CH {sc.courseHandicap}
                                </span>
                              </div>
                              <div className="flex items-center gap-4">
                                {sc.grossScore !== null && (
                                  <span className="text-sm text-gray-600">
                                    Gross <strong>{sc.grossScore}</strong>
                                  </span>
                                )}
                                {sc.netScore !== null && (
                                  <span className="text-sm text-gray-600">
                                    Net <strong>{sc.netScore}</strong>
                                  </span>
                                )}
                                {sc.netPoints !== null && (
                                  <Badge variant="secondary">{sc.netPoints} pts</Badge>
                                )}
                                <ChevronDown className="h-4 w-4 text-gray-400 transition-transform group-data-[state=open]:rotate-180" />
                              </div>
                            </Accordion.Trigger>
                          </Accordion.Header>
                          <Accordion.Content className="data-[state=open]:animate-accordion-down data-[state=closed]:animate-accordion-up overflow-hidden">
                            <div className="border-t border-gray-100 px-5 py-4">
                              <ScorecardTable
                                scorecard={sc}
                                flightSkins={flightSkins}
                              />
                            </div>
                          </Accordion.Content>
                        </Accordion.Item>
                      ))}
                    </Accordion.Root>
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}
    </div>
  );
}
