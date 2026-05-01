import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { useRounds } from '../../hooks/useRounds';
import { useFinalizeRound } from '../../hooks/admin/useRoundMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Table } from '../../components/ui/Table';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { CreateRoundForm } from '../../components/admin/CreateRoundForm';
import type { Round } from '../../types/api';

// ── Status badge helper ────────────────────────────────────────────────────

function RoundStatusBadge({ status }: { status: Round['status'] }) {
  const variantMap: Record<Round['status'], 'neutral' | 'warning' | 'success' | 'info'> = {
    Scheduled: 'info',
    InProgress: 'warning',
    Finalized: 'success',
    Cancelled: 'neutral',
  };
  return <Badge variant={variantMap[status]}>{status}</Badge>;
}

// ── Page ───────────────────────────────────────────────────────────────────

export function RoundsPage() {
  const navigate = useNavigate();
  const { data: rounds, isLoading, error } = useRounds();

  const [createOpen, setCreateOpen] = useState(false);
  const [finalizeTarget, setFinalizeTarget] = useState<Round | null>(null);

  const finalize = useFinalizeRound(finalizeTarget?.id ?? '');

  async function handleFinalize() {
    if (!finalizeTarget) return;
    await finalize.mutateAsync();
    setFinalizeTarget(null);
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
      key: 'date',
      header: 'Date',
      render: (r: Round) => new Date(r.date).toLocaleDateString(),
    },
    { key: 'course', header: 'Course', render: (r: Round) => r.courseName ?? '—' },
    { key: 'flight', header: 'Flight', render: (r: Round) => r.flightName ?? '—' },
    {
      key: 'status',
      header: 'Status',
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
            {r.status === 'Finalized' ? 'View Scorecard' : 'Enter Scores'}
          </Button>
          {r.status === 'InProgress' && (
            <Button
              variant="ghost"
              size="sm"
              className="text-[#1B5E20] hover:bg-green-50"
              onClick={() => setFinalizeTarget(r)}
            >
              Finalize
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader title="Rounds">
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Plus className="mr-1 h-4 w-4" />
          Create Round
        </Button>
      </PageHeader>

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <Table
          columns={columns}
          data={rounds ?? []}
          rowKey={(r) => r.id}
          emptyMessage="No rounds yet."
        />
      </div>

      {/* Create Round Modal */}
      <Modal open={createOpen} title="Create Round" onClose={() => setCreateOpen(false)}>
        <CreateRoundForm
          onSuccess={() => setCreateOpen(false)}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>

      {/* Finalize Confirmation */}
      <ConfirmDialog
        open={!!finalizeTarget}
        title="Finalize Round"
        description={`Finalize the round on ${finalizeTarget ? new Date(finalizeTarget.date).toLocaleDateString() : ''}? This will lock scores and recalculate standings.`}
        confirmLabel="Finalize"
        onConfirm={handleFinalize}
        onCancel={() => setFinalizeTarget(null)}
      />
    </div>
  );
}
