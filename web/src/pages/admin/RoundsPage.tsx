import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, CalendarDays, Trophy } from 'lucide-react';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { useRounds } from '../../hooks/useRounds';
import { useSortableTable } from '../../hooks/useSortableTable';
import {
  useFinalizeRound,
  useDeleteRound,
  useCancelRound,
  useReopenRound,
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
import { CreateTournamentRoundForm } from '../../components/admin/CreateTournamentRoundForm';
import { CreateHalfForm } from '../../components/admin/CreateHalfForm';
import { ManageTournamentPlayersModal } from '../../components/admin/ManageTournamentPlayersModal';
import {
  normalizeRoundStatus,
  normalizeRoundType,
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
  const prefix = useLeaguePrefix();
  const { sort, cycle } = useSortableTable('adminRounds');
  const { data: roundsPage, isLoading, error } = useRounds(1, sort);
  const rounds = roundsPage?.data ?? [];

  const [createOpen, setCreateOpen] = useState(false);
  const [createTournamentOpen, setCreateTournamentOpen] = useState(false);
  const [createHalfOpen, setCreateHalfOpen] = useState(false);
  const [finalizeTarget, setFinalizeTarget] = useState<Round | null>(null);
  const [cancelTarget, setCancelTarget] = useState<Round | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Round | null>(null);
  const [reopenTarget, setReopenTarget] = useState<Round | null>(null);
  const [managePlayersTarget, setManagePlayersTarget] = useState<Round | null>(null);

  const finalize = useFinalizeRound(String(finalizeTarget?.id ?? ''));
  const cancelRound = useCancelRound();
  const deleteRound = useDeleteRound();
  const reopenRound = useReopenRound();

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

  async function handleReopen() {
    if (!reopenTarget) return;
    await reopenRound.mutateAsync(String(reopenTarget.id));
    setReopenTarget(null);
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
      render: (r: Round) => new Date(r.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' }),
    },
    { key: 'course', header: 'Course', sortable: true, render: (r: Round) => r.courseName ?? '—' },
    {
      key: 'type',
      header: 'Type',
      render: (r: Round) =>
        normalizeRoundType(r.roundType) === 'Tournament' ? (
          <Badge variant="warning">Tournament</Badge>
        ) : (
          <Badge variant={r.nineHoleSide === 'Front' ? 'info' : 'success'}>
            {r.nineHoleSide === 'NotApplicable' ? '18-Hole' : r.nineHoleSide}
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
          {normalizeRoundType(r.roundType) === 'Tournament' ? (
            <>
              {isRoundScheduled(r.status) && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setManagePlayersTarget(r)}
                >
                  Manage Tournament
                </Button>
              )}
              <Button
                variant="ghost"
                size="sm"
                onClick={() => navigate(`${prefix}/admin/rounds/${r.id}/tournament-scores`)}
              >
                {isRoundFinalized(r.status) ? 'View Results' : 'Enter Scores'}
              </Button>
            </>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigate(`/admin/rounds/${r.id}/scores`)}
            >
              {isRoundFinalized(r.status) ? 'View Scorecard' : 'Enter Scores'}
            </Button>
          )}
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
          {isRoundFinalized(r.status) && (
            <Button
              variant="ghost"
              size="sm"
              className="text-amber-600 hover:bg-amber-50"
              onClick={() => setReopenTarget(r)}
            >
              Re-open
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
          <Button variant="secondary" onClick={() => setCreateTournamentOpen(true)}>
            <Trophy className="mr-1 h-4 w-4" />
            Tournament Round
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

      <Modal open={createTournamentOpen} title="Create Tournament Round" onClose={() => setCreateTournamentOpen(false)}>
        <CreateTournamentRoundForm
          onSuccess={(roundId) => {
            setCreateTournamentOpen(false);
            navigate(`/rounds/${roundId}/tournament-results`);
          }}
          onCancel={() => setCreateTournamentOpen(false)}
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
        description={`Finalize the round on ${finalizeTarget ? new Date(finalizeTarget.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : ''}? This will lock scores and recalculate standings.`}
        confirmLabel="Finalize"
        onConfirm={handleFinalize}
        onCancel={() => setFinalizeTarget(null)}
      />

      <ConfirmDialog
        open={!!cancelTarget}
        title="Cancel Round"
        description={`Cancel the round on ${cancelTarget ? new Date(cancelTarget.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : ''}? All later rounds in this half will shift forward by one week to keep the schedule sequential.`}
        confirmLabel="Cancel Round"
        onConfirm={handleCancel}
        onCancel={() => setCancelTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Round"
        description={`Permanently delete the round on ${deleteTarget ? new Date(deleteTarget.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : ''}? This cannot be undone.`}
        confirmLabel="Delete"
        destructive
        isLoading={deleteRound.isPending}
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />

      <ManageTournamentPlayersModal
        round={managePlayersTarget}
        onClose={() => setManagePlayersTarget(null)}
      />

      <ConfirmDialog
        open={!!reopenTarget}
        title="Re-open Round"
        description={`Re-open the finalized round on ${reopenTarget ? new Date(reopenTarget.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : ''}? Scores can be edited again. Calculated handicaps from this round will be removed and recalculated when you re-finalize.`}
        confirmLabel="Re-open"
        isLoading={reopenRound.isPending}
        onConfirm={handleReopen}
        onCancel={() => setReopenTarget(null)}
      />
    </div>
  );
}
