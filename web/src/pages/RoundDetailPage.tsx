import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, ChevronDown } from 'lucide-react';
import * as Accordion from '@radix-ui/react-accordion';
import { useRound, useRoundScorecards } from '@/hooks/useRounds';
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
import type { HoleScore, RoundStatus, Scorecard } from '@/types/api';

function statusVariant(status: RoundStatus) {
  switch (status) {
    case 'Finalized':  return 'green' as const;
    case 'InProgress': return 'amber' as const;
    case 'Scheduled':  return 'blue' as const;
  }
}

/**
 * Score relative to par: eagle or better, birdie, par, bogey, double bogey+
 */
function holeScoreClass(hole: HoleScore): string {
  const diff = hole.strokes - hole.par;
  if (diff <= -2) return 'bg-yellow-400 text-yellow-900 font-bold'; // eagle+
  if (diff === -1) return 'bg-green-500 text-white font-semibold';  // birdie
  if (diff === 0)  return 'bg-white text-gray-800';                 // par
  if (diff === 1)  return 'bg-gray-200 text-gray-700';              // bogey
  return 'bg-red-500 text-white font-semibold';                      // double+
}

function HoleScoreCell({ hole }: { hole: HoleScore }) {
  return (
    <td
      className={cn(
        'px-2 py-2 text-center text-xs rounded',
        holeScoreClass(hole),
      )}
      title={`Hole ${hole.holeNumber}: par ${hole.par}`}
    >
      {hole.strokes}
    </td>
  );
}

function ScorecardTable({ scorecard }: { scorecard: Scorecard }) {
  const front = scorecard.holes.filter((h) => h.holeNumber <= 9);
  const back  = scorecard.holes.filter((h) => h.holeNumber > 9);

  const renderHoles = (holes: HoleScore[]) => (
    <Table>
      <TableHeader>
        <TableRow className="bg-gray-50">
          <TableHead className="w-24">Hole</TableHead>
          {holes.map((h) => (
            <TableHead key={h.holeNumber} className="text-center px-2">
              {h.holeNumber}
            </TableHead>
          ))}
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
            <HoleScoreCell key={h.holeNumber} hole={h} />
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
      </TableBody>
    </Table>
  );

  return (
    <div className="space-y-4 overflow-x-auto">
      {front.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">
            Front 9
          </p>
          {renderHoles(front)}
        </div>
      )}
      {back.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">
            Back 9
          </p>
          {renderHoles(back)}
        </div>
      )}
      <div className="flex gap-6 text-sm text-gray-600 pt-1">
        <span>
          Gross: <strong>{scorecard.grossScore ?? '—'}</strong>
        </span>
        <span>
          Net: <strong>{scorecard.netScore ?? '—'}</strong>
        </span>
        <span>
          Points: <strong>{scorecard.points ?? '—'}</strong>
        </span>
        <span>
          HCP: <strong>{scorecard.handicapAtTime}</strong>
        </span>
      </div>
    </div>
  );
}

export function RoundDetailPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const round = useRound(roundId ?? '');
  const scorecards = useRoundScorecards(roundId ?? '');

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

      {round.data?.data && (
        <PageHeader
          title={round.data.data.courseName}
          description={formatDate(round.data.data.scheduledDate)}
        >
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-500">{round.data.data.flightName}</span>
            <Badge variant={statusVariant(round.data.data.status)}>
              {round.data.data.status}
            </Badge>
          </div>
        </PageHeader>
      )}

      {/* Scorecards accordion */}
      {scorecards.isPending && (
        <div className="flex justify-center py-8">
          <FullPageSpinner />
        </div>
      )}
      {scorecards.isError && (
        <ErrorMessage message="Could not load scorecards." />
      )}

      {scorecards.data && (
        <>
          {scorecards.data.data.length === 0 ? (
            <p className="text-gray-500 text-sm">
              No scorecards have been entered for this round yet.
            </p>
          ) : (
            <Accordion.Root type="multiple" className="space-y-2">
              {scorecards.data.data.map((sc) => (
                <Accordion.Item
                  key={sc.playerId}
                  value={sc.playerId}
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
                        <span className="text-sm text-gray-400">
                          HCP {sc.handicapAtTime}
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
                        {sc.points !== null && (
                          <Badge variant="secondary">{sc.points} pts</Badge>
                        )}
                        <ChevronDown className="h-4 w-4 text-gray-400 transition-transform group-data-[state=open]:rotate-180" />
                      </div>
                    </Accordion.Trigger>
                  </Accordion.Header>
                  <Accordion.Content className="data-[state=open]:animate-accordion-down data-[state=closed]:animate-accordion-up overflow-hidden">
                    <div className="border-t border-gray-100 px-5 py-4">
                      <ScorecardTable scorecard={sc} />
                    </div>
                  </Accordion.Content>
                </Accordion.Item>
              ))}
            </Accordion.Root>
          )}
        </>
      )}
    </div>
  );
}
