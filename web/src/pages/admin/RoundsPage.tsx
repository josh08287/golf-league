import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, CalendarDays } from 'lucide-react';
import { useRounds } from '../../hooks/useRounds';
import { useSortableTable } from '../../hooks/useSortableTable';
import {
  useFinalizeRound,
  useDeleteRound,
  useCancelRound,
} from '../../hooks/admin/useRoundMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { DataTable } from '../../components/ui/DataTable';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { CreateRoundForm } from '../../components/admin/CreateRoundForm';
import { CreateHalfForm } from '../../components/admin/CreateHalfForm';
import {
  normalizeRoundStatus,
  isRoundFinalized,
  isRoundInProgress,
  isRoundScheduled,
} from '../../lib/enumUtils';
import type { Round, RoundStatus } from '../../types/api';

function RoundStatusBadge({ status }: { status: Round['status'] }) {
  const normalizedStatus = normalizeRoundStatus(status);
  const variantMap: Record<RoundStatus, 'neutral' | 'warning' | 'success' | 'info'> = {
    Scheduled: 'info',
    InProgress: 'warning',
    PendingFinalization: 'warning',
    Finalized: 'success',
    Cancelled: 'neutral',
  };
  return <Badge variant={variantMap[normalizedStatus]}>{normalizedStatus}</Badge>;
}

export function RoundsPage() {
  const navigate = useNavigate();
  const { sort, cycle } = useSortableTable('adminRounds');
  const { data: roundsPage, isLoading, error } = useRounds(1, sort);
  const rounds = roundsPage?.data ?? [];

  const [createOpen, setCreateOpen] = useState(false);
  const [createHalfOpen, setCreateHalfOpen] = useState(false);
  const [finalizeTarget, setFinalizeTarget] = useState<Round | null>(null);
  const [cancelTarget, setCancelTarget] = useState<Round | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Round | null>(null);

  const finalize = useFinalizeRound(String(finalizeTarget?.id ?? ''));
  const cancelRound = useCancelRound();
  const deleteRound = useDeleteRound();

  async function handleFinalize() {
    if (!finalizeTarget) return;
    await finalize.mutateAsync();
    setFinalizeTarget(null);
  }

  async function handleCancel() {
    if (!cancelTarget) return;
    await cancelRound.mutateAsync(String(cancelTarget.id));
    setCancelTarget(null);
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    await deleteRound.mutateAsync(String(deleteTarget.id));
    setDeleteTarget(null);
  }

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <ErrorMessage message="Failed to load rounds." />;
  }

  const columns = [
    {
      key: 'week',
      header: 'Week',
      sortable: true,
      render: (r: Round) => `#${r.weekNumber}`,
    },
    {
      key: 'date',
      header: 'Date',
      sortable: true,
      render: (r: Round) => new Date(r.scheduledDate).toLocaleDateString(),
    },
    { key: 'course', header: 'Course', sortable: true, render: (r: Round) => r.courseName ?? '—' },
    {
      key: 'nineHoleSide',
      header: '9-Hole Side',
      sortable: true,
      render: (r: Round) => (
        <Badge variant={r.nineHoleSide === 'Front' ? 'info' : 'success'}>
          {r.nineHoleSide}
        </Badge>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      sortable: true,
      render: (r: Round) => <RoundStatusBadge status={r.status} />,
    },
    {
      key: 'actions',
      header: '',
      render: (r: Round) => (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/admin/rounds/${r.id}/scores`)}
          >
            {isRoundFinalized(r.status) ? 'View Scorecard' : 'Enter Scores'}
          </Button>
          {isRoundInProgress(r.status) && (
            <Button
              variant="ghost"
              size="sm"
              className="text-[#1B5E20] hover:bg-green-50"
              onClick={() => setFinalizeTarget(r)}
            >
              Finalize
            </Button>
          )}
          {isRoundScheduled(r.status) && (
            <Button
              variant="ghost"
              size="sm"
              className="text-amber-600 hover:bg-amber-50"
              onClick={() => setCancelTarget(r)}
            >
              Cancel
            </Button>
          )}
          {!isRoundFinalized(r.status) && (
            <Button
              variant="ghost"
              size="sm"
              className="text-red-600 hover:bg-red-50 hover:text-red-700"
              onClick={() => setDeleteTarget(r)}
            >
              Delete
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader title="Rounds">
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => setCreateHalfOpen(true)}>
            <CalendarDays className="mr-1 h-4 w-4" />
            Generate Schedule
          </Button>
          <Button variant="primary" onClick={() => setCreateOpen(true)}>
            <Plus className="mr-1 h-4 w-4" />
            Add Round
          </Button>
        </div>
      </PageHeader>

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <DataTable
          columns={columns}
          data={rounds}
          rowKey={(r) => String(r.id)}
          emptyMessage="No rounds yet."
          sort={sort}
          onSort={cycle}
        />
      </div>

      <Modal open={createOpen} title="Add Round" onClose={() => setCreateOpen(false)}>
        <CreateRoundForm
          onSuccess={() => setCreateOpen(false)}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>

      <Modal open={createHalfOpen} title="Generate Half Schedule" onClose={() => setCreateHalfOpen(false)}>
        <CreateHalfForm
          onSuccess={() => setCreateHalfOpen(false)}
          onCancel={() => setCreateHalfOpen(false)}
        />
      </Modal>

      <ConfirmDialog
        open={!!finalizeTarget}
        title="Finalize Round"
        description={`Finalize the round on ${finalizeTarget ? new Date(finalizeTarget.scheduledDate).toLocaleDateString() : ''}? This will lock scores and recalculate standings.`}
        confirmLabel="Finalize"
        onConfirm={handleFinalize}
        onCancel={() => setFinalizeTarget(null)}
      />

      <ConfirmDialog
        open={!!cancelTarget}
        title="Cancel Round"
        description={`Cancel the round on ${cancelTarget ? new Date(cancelTarget.scheduledDate).toLocaleDateString() : ''}? A make-up round will be appended one week later with the same 9-hole side.`}
        confirmLabel="Cancel Round"
        onConfirm={handleCancel}
        onCancel={() => setCancelTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Round"
        description={`Permanently delete the round on ${deleteTarget ? new Date(deleteTarget.scheduledDate).toLocaleDateString() : ''}? This cannot be undone.`}
        confirmLabel="Delete"
        destructive
        isLoading={deleteRound.isPending}
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
