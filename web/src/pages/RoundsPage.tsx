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
import { normalizeRoundStatus } from '@/lib/enumUtils';
import type { RoundStatus } from '@/types/api';

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

export function RoundsPage() {
  const [page, setPage] = useState(1);
  const { data, isPending, isError, isFetching } = useRounds(page);

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
            {data.data.map((round) => (
              <Link
                key={round.id}
                to={`/rounds/${round.id}`}
                className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-5 py-4 hover:shadow-sm hover:border-primary-300 transition-all"
              >
                <div className="min-w-0">
                  <p className="font-semibold text-gray-900 truncate">
                    {round.courseName}
                  </p>
                  <p className="text-sm text-gray-500">
                    Week {round.weekNumber} &middot; {round.nineHoleSide} 9 &middot;{' '}
                    {formatShortDate(round.scheduledDate)}
                  </p>
                </div>
                <div className="flex items-center gap-3 shrink-0 ml-4">
                  <Badge variant={statusVariant(round.status)}>{round.status}</Badge>
                  <ArrowRight className="h-4 w-4 text-gray-400" />
                </div>
              </Link>
            ))}
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
