import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { usePlayers } from '../../hooks/usePlayers';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Table } from '../../components/ui/Table';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { AddPlayerForm } from '../../components/admin/AddPlayerForm';
import { useDeactivatePlayer } from '../../hooks/admin/usePlayerMutations';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import type { Player } from '../../types/api';

export function PlayersPage() {
  const navigate = useNavigate();
  const { data: players, isLoading, error } = usePlayers();

  const [addOpen, setAddOpen] = useState(false);
  const [deactivateTarget, setDeactivateTarget] = useState<Player | null>(null);

  const deactivate = useDeactivatePlayer(deactivateTarget?.id ?? '');

  async function handleDeactivate() {
    if (!deactivateTarget) return;
    await deactivate.mutateAsync();
    setDeactivateTarget(null);
  }

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <ErrorMessage message="Failed to load players." />;
  }

  const columns = [
    {
      key: 'name',
      header: 'Name',
      render: (p: Player) => (
        <button
          className="font-medium text-[#1B5E20] hover:underline"
          onClick={() => navigate(`/admin/players/${p.id}`)}
        >
          {p.firstName} {p.lastName}
        </button>
      ),
    },
    { key: 'email', header: 'Email', render: (p: Player) => p.email },
    {
      key: 'handicap',
      header: 'Handicap',
      render: (p: Player) => p.handicapIndex?.toFixed(1) ?? '—',
    },
    {
      key: 'flight',
      header: 'Flight',
      render: (p: Player) => p.flightName ?? <span className="text-gray-400">Unassigned</span>,
    },
    {
      key: 'status',
      header: 'Status',
      render: (p: Player) => (
        <Badge variant={p.isActive ? 'success' : 'neutral'}>
          {p.isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (p: Player) => (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              navigate(`/admin/players/${p.id}`);
            }}
          >
            Edit
          </Button>
          {p.isActive && (
            <Button
              variant="ghost"
              size="sm"
              className="text-red-600 hover:bg-red-50 hover:text-red-700"
              onClick={(e) => {
                e.stopPropagation();
                setDeactivateTarget(p);
              }}
            >
              Deactivate
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader title="Players">
        <Button variant="primary" onClick={() => setAddOpen(true)}>
          <Plus className="mr-1 h-4 w-4" />
          Add Player
        </Button>
      </PageHeader>

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <Table
          columns={columns}
          data={players ?? []}
          rowKey={(p) => p.id}
          emptyMessage="No players found."
        />
      </div>

      {/* Add Player Modal */}
      <Modal open={addOpen} title="Add Player" onClose={() => setAddOpen(false)}>
        <AddPlayerForm
          onSuccess={() => setAddOpen(false)}
          onCancel={() => setAddOpen(false)}
        />
      </Modal>

      {/* Deactivate Confirmation */}
      <ConfirmDialog
        open={!!deactivateTarget}
        title="Deactivate Player"
        description={`Are you sure you want to deactivate ${deactivateTarget?.firstName} ${deactivateTarget?.lastName}? They will no longer appear in active rounds.`}
        confirmLabel="Deactivate"
        destructive
        onConfirm={handleDeactivate}
        onCancel={() => setDeactivateTarget(null)}
      />
    </div>
  );
}
