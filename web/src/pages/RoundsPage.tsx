import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft, ChevronRight, ArrowRight } from 'lucide-react';
import { useRounds } from '@/hooks/useRounds';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus, normalizeRoundType, formatNineHoleSide } from '@/lib/enumUtils';
import type { Round, RoundStatus } from '@/types/api';
import type { TableSort } from '@/hooks/useSortableTable';

/** Next round first, then past rounds newest to oldest, then the rest of the upcoming rounds. */
const SCHEDULE_SORT: TableSort = { sortBy: 'schedule', sortDir: 'asc' };

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Mirrors the server's schedule ordering: dated today or later and not finalized or cancelled. */
function isUpcoming(round: Round, today: string): boolean {
  const status = normalizeRoundStatus(round.status);
  return round.scheduledDate.slice(0, 10) >= today && status !== 'Finalized' && status !== 'Cancelled';
}

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

function RoundRow({ round, highlight = false }: { round: Round; highlight?: boolean }) {
  const isTournament = normalizeRoundType(round.roundType) === 'Tournament';
  return (
    <Link
      to={isTournament ? `/rounds/${round.id}/tournament-results` : `/rounds/${round.id}`}
      className={`flex items-center justify-between rounded-lg border bg-white px-5 py-4 hover:shadow-sm hover:border-primary-300 transition-all ${
        highlight ? 'border-primary-300 ring-1 ring-primary-200' : 'border-gray-200'
      }`}
    >
      <div className="min-w-0">
        <p className="font-semibold text-gray-900 truncate">
          {round.courseName}
        </p>
        <p className="text-sm text-gray-500">
          {isTournament ? 'Tournament' : formatNineHoleSide(round.nineHoleSide)} &middot; Week {round.weekNumber} &middot;{' '}
          {formatShortDate(round.scheduledDate)}
        </p>
      </div>
      <div className="flex items-center gap-3 shrink-0 ml-4">
        <Badge variant={statusVariant(round.status)}>{round.status}</Badge>
        <ArrowRight className="h-4 w-4 text-gray-400" />
      </div>
    </Link>
  );
}

export function RoundsPage() {
  const [page, setPage] = useState(1);
  const { data, isPending, isError, isFetching } = useRounds(page, SCHEDULE_SORT);
  const today = todayIso();

  const meta = data?.meta;
  const totalPages = meta ? Math.ceil(meta.totalCount / meta.pageSize) : 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Rounds"
        description="All scheduled and completed rounds this season."
      />

      {isPending && <FullPageSpinner />}
      {isError && <ErrorMessage message="Could not load rounds. Please try again." />}

      {data && (
        <>
          <div className="space-y-3">
            {data.data.length === 0 && (
              <p className="text-gray-500 text-sm">No rounds found.</p>
            )}
            {data.data.map((round, index) => {
              const upcoming = isUpcoming(round, today);
              const isNextRound = page === 1 && index === 0 && upcoming;
              // Upcoming rounds after the next one come last; mark where they start.
              const startsUpcoming = upcoming && !isNextRound && (index === 0 || !isUpcoming(data.data[index - 1], today) || (page === 1 && index === 1));
              return (
                <div key={round.id} className="space-y-3">
                  {isNextRound && (
                    <h2 className="text-sm font-semibold uppercase tracking-wide text-primary-700">Next Round</h2>
                  )}
                  {startsUpcoming && (
                    <h2 className="pt-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Upcoming</h2>
                  )}
                  {page === 1 && index === 1 && !upcoming && data.data[0] && isUpcoming(data.data[0], today) && (
                    <h2 className="pt-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Past Rounds</h2>
                  )}
                  <RoundRow round={round} highlight={isNextRound} />
                </div>
              );
            })}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between pt-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1 || isFetching}
              >
                <ChevronLeft className="h-4 w-4 mr-1" />
                Previous
              </Button>
              <span className="text-sm text-gray-500">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages || isFetching}
              >
                Next
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
