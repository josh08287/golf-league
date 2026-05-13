import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, ChevronDown, Clock, Trophy, Grid3X3 } from 'lucide-react';
import * as Accordion from '@radix-ui/react-accordion';
import * as Collapsible from '@radix-ui/react-collapsible';
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
import type { RoundScorecard, RoundScorecardHole, RoundStatus, FlightSkins, HoleSkin, GrossPar3SkinsSummary } from '@/types/api';

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

interface GrossPar3SkinsDisplayProps {
  grossPar3Skins: GrossPar3SkinsSummary;
}

function GrossPar3SkinsDisplay({ grossPar3Skins }: GrossPar3SkinsDisplayProps) {
  if (grossPar3Skins.playerSummaries.length === 0) {
    return (
      <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
        <div className="flex items-center gap-2 mb-2">
          <Trophy className="h-5 w-5 text-amber-600" />
          <span className="font-semibold text-gray-900">Par 3 Gross Skins</span>
          <span className="text-sm text-gray-500">({grossPar3Skins.par3HoleCount} par 3s)</span>
        </div>
        <div className="text-sm text-gray-600 italic">
          No gross skins awarded on par 3s — all holes were tied.
        </div>
      </div>
    );
  }

  return (
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <Trophy className="h-5 w-5 text-amber-600" />
          <span className="font-semibold text-gray-900">Par 3 Gross Skins</span>
          <span className="text-sm text-gray-500">
            ({grossPar3Skins.totalHolesWithSkins}/{grossPar3Skins.par3HoleCount} holes awarded)
            {grossPar3Skins.incomingCarryover > 0 && (
              <span className="ml-2 text-amber-700 font-medium" title={`${grossPar3Skins.incomingCarryover} skin(s) carried over from previous round`}>
                ↻ {grossPar3Skins.incomingCarryover}
              </span>
            )}
          </span>
        </div>
        <span className="text-sm text-amber-700 font-medium">
          Total Value: {grossPar3Skins.totalSkinValueAwarded}
        </span>
      </div>

      {/* Winners Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-3">
        {grossPar3Skins.playerSummaries.map((player) => (
          <div
            key={player.playerId}
            className="flex items-center justify-between bg-white border border-amber-200 rounded-lg px-3 py-2"
          >
            <div className="flex items-center gap-2">
              <Trophy className="h-4 w-4 text-amber-500" />
              <Link
                to={`/players/${player.playerId}`}
                className="font-medium text-gray-900 hover:underline"
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

      {/* Hole-by-hole breakdown */}
      <div className="flex flex-wrap gap-2 text-xs">
        {grossPar3Skins.holeResults.map((hole) => (
          <span
            key={hole.holeNumber}
            className={cn(
              'inline-flex items-center gap-1 px-2 py-1 rounded',
              hole.skinValue > 0
                ? 'bg-amber-100 text-amber-800 font-medium'
                : 'bg-gray-100 text-gray-500'
            )}
            title={
              hole.skinValue > 0
                ? `${hole.winnerPlayerName} won with gross ${hole.winningGrossScore}${hole.wasCarryover ? ' (carryover)' : ''}`
                : 'Tie — skin carried over'
            }
          >
            Hole {hole.holeNumber}
            {hole.skinValue > 0 ? (
              <>
                : <Trophy className={cn('h-3 w-3', hole.wasCarryover ? 'text-amber-600' : 'text-amber-500')} />
                {hole.skinValue}
              </>
            ) : (
              <span className="text-gray-400">↻</span>
            )}
          </span>
        ))}
      </div>
    </div>
  );
}

interface FlightScorecardsGridProps {
  scorecards: RoundScorecard[];
  flightSkins: FlightSkins;
}

function FlightScorecardsGrid({ scorecards, flightSkins }: FlightScorecardsGridProps) {
  // Get all holes sorted
  const allHoles = scorecards
    .flatMap(sc => sc.holes)
    .map(h => h.holeNumber)
    .filter((v, i, a) => a.indexOf(v) === i)
    .sort((a, b) => a - b);

  // Build skins lookup by hole number
  const skinsByHole = new Map<number, { playerId: number; playerName: string; skinValue: number; wasCarryover: boolean }>();
  for (const holeResult of flightSkins.allHoleResults) {
    if (holeResult.skinValue > 0) {
      skinsByHole.set(holeResult.holeNumber, {
        playerId: holeResult.winnerPlayerId,
        playerName: holeResult.winnerPlayerName,
        skinValue: holeResult.skinValue,
        wasCarryover: holeResult.wasCarryover,
      });
    }
  }

  // Sort scorecards by total net points descending (best performers first)
  const sortedScorecards = [...scorecards].sort((a, b) => {
    const netDiff = (b.netPoints ?? 0) - (a.netPoints ?? 0);
    if (netDiff !== 0) return netDiff;
    return (a.grossScore ?? 999) - (b.grossScore ?? 999);
  });

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-gray-100">
            <th className="px-2 py-2 text-left font-semibold text-gray-700 sticky left-0 bg-gray-100 z-10 min-w-[140px]">
              Player
            </th>
            {allHoles.map(holeNum => {
              const skin = skinsByHole.get(holeNum);
              return (
                <th
                  key={holeNum}
                  className={cn(
                    'px-2 py-2 text-center font-semibold min-w-[48px]',
                    skin ? 'bg-amber-100 text-amber-800' : 'text-gray-600'
                  )}
                  title={skin ? `Skin to ${skin.playerName} (value: ${skin.skinValue})` : undefined}
                >
                  <div className="flex flex-col items-center">
                    <span>{holeNum}</span>
                    {skin && (
                      <Trophy className={cn('h-3 w-3', skin.wasCarryover ? 'text-amber-600' : 'text-amber-500')} />
                    )}
                  </div>
                </th>
              );
            })}
            <th className="px-2 py-2 text-center font-semibold text-gray-700 min-w-[50px]">Gross</th>
            <th className="px-2 py-2 text-center font-semibold text-gray-700 min-w-[50px]">Net</th>
            <th className="px-2 py-2 text-center font-semibold text-[#1B5E20] min-w-[50px]">Pts</th>
          </tr>
        </thead>
        <tbody>
          {sortedScorecards.map((sc, idx) => (
            <tr
              key={sc.playerId}
              className={cn('border-b border-gray-100', idx % 2 === 1 && 'bg-gray-50')}
            >
              <td className="px-2 py-2 sticky left-0 bg-white z-10 border-r border-gray-200">
                <Link
                  to={`/players/${sc.playerId}`}
                  className="font-medium text-primary-900 hover:underline"
                >
                  {sc.playerName}
                </Link>
              </td>
              {allHoles.map(holeNum => {
                const hole = sc.holes.find(h => h.holeNumber === holeNum);
                const skin = skinsByHole.get(holeNum);
                const isSkinWinner = skin?.playerId === sc.playerId;

                return (
                  <td
                    key={holeNum}
                    className={cn(
                      'px-1 py-1 text-center',
                      isSkinWinner && 'bg-amber-100 font-semibold'
                    )}
                    title={isSkinWinner ? `Skin winner! Value: ${skin.skinValue}` : undefined}
                  >
                    {hole ? (
                      <span className={cn(
                        'inline-flex items-center justify-center w-7 h-6 rounded text-xs',
                        isSkinWinner
                          ? 'bg-amber-500 text-white'
                          : hole.strokes === hole.par
                            ? 'bg-white text-gray-700'
                            : hole.strokes < hole.par
                              ? 'bg-green-100 text-green-800'
                              : 'bg-gray-100 text-gray-600'
                      )}>
                        {hole.strokes}
                      </span>
                    ) : (
                      <span className="text-gray-300">—</span>
                    )}
                  </td>
                );
              })}
              <td className="px-2 py-2 text-center font-medium text-gray-700">
                {sc.grossScore ?? '—'}
              </td>
              <td className="px-2 py-2 text-center font-medium text-gray-700">
                {sc.netScore ?? '—'}
              </td>
              <td className="px-2 py-2 text-center font-semibold text-[#1B5E20]">
                {sc.netPoints ?? '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
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

      {skins.data?.grossPar3Skins && (
        <GrossPar3SkinsDisplay grossPar3Skins={skins.data.grossPar3Skins} />
      )}

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

                    {/* Flight Scorecards Grid - Collapsed by default */}
                    {flightGroup.scorecards.length > 0 && flightSkins && (
                      <Collapsible.Root defaultOpen={false}>
                        <Collapsible.Trigger asChild>
                          <button className="flex items-center gap-2 text-sm font-medium text-gray-600 hover:text-gray-900 transition-colors py-2">
                            <Grid3X3 className="h-4 w-4" />
                            <span>View Flight Scorecard Grid</span>
                            <ChevronDown className="h-4 w-4 transition-transform data-[state=open]:rotate-180" />
                          </button>
                        </Collapsible.Trigger>
                        <Collapsible.Content>
                          <div className="mt-2 border border-gray-200 rounded-lg overflow-hidden">
                            <FlightScorecardsGrid
                              scorecards={flightGroup.scorecards}
                              flightSkins={flightSkins}
                            />
                          </div>
                        </Collapsible.Content>
                      </Collapsible.Root>
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
