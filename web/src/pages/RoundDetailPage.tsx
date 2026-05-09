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
import { normalizeRoundStatus } from '@/lib/enumUtils';
import type { RoundScorecard, RoundScorecardHole, RoundStatus } from '@/types/api';

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

function HoleScoreCell({ hole }: { hole: RoundScorecardHole }) {
  return (
    <td
      className={cn('px-2 py-2 text-center text-xs rounded', holeScoreClass(hole))}
      title={`Hole ${hole.holeNumber}: par ${hole.par}`}
    >
      {hole.strokes}
    </td>
  );
}

function ScorecardTable({ scorecard }: { scorecard: RoundScorecard }) {
  const holes = [...scorecard.holes].sort((a, b) => a.holeNumber - b.holeNumber);

  return (
    <div className="space-y-3 overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="bg-gray-50">
            <TableHead className="w-32">Hole</TableHead>
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
      <div className="flex gap-6 text-sm text-gray-600 pt-1">
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

      {round.data && (
        <PageHeader
          title={round.data.courseName}
          description={`${formatDate(round.data.scheduledDate)} — Week ${round.data.weekNumber} — ${round.data.nineHoleSide} 9`}
        >
          <Badge variant={statusVariant(round.data.status)}>{round.data.status}</Badge>
        </PageHeader>
      )}

      {scorecards.isPending && (
        <div className="flex justify-center py-8">
          <FullPageSpinner />
        </div>
      )}
      {scorecards.isError && <ErrorMessage message="Could not load scorecards." />}

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
                  value={String(sc.playerId)}
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
                        {sc.netPoints !== null && (
                          <Badge variant="secondary">{sc.netPoints} pts</Badge>
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
