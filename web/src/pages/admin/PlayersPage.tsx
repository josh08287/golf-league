import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, ChevronLeft, ChevronRight } from 'lucide-react';
import { usePlayers } from '../../hooks/usePlayers';
import { useSortableTable } from '../../hooks/useSortableTable';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { DataTable } from '../../components/ui/DataTable';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { AddPlayerForm } from '../../components/admin/AddPlayerForm';
import { useDeactivatePlayer, useDeletePlayer } from '../../hooks/admin/usePlayerMutations';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { AdministratorsTable } from '../../components/admin/AdministratorsTable';
import type { Player } from '../../types/api';

// Default to a large page so a typical league roster (< 100 players) fits in
// one fetch. Prev/Next controls below the table cover the case where it
// grows past that.
const PAGE_SIZE = 100;

export function PlayersPage() {
  const navigate = useNavigate();
  const { sort, cycle } = useSortableTable('adminPlayers');
  const [page, setPage] = useState(1);
  const { data: playersPage, isLoading, error } = usePlayers(page, sort, PAGE_SIZE);
  const players = playersPage?.data ?? [];
  const meta = playersPage?.meta;
  const totalPages = meta ? Math.max(1, Math.ceil(meta.totalCount / meta.pageSize)) : 1;

  const [addOpen, setAddOpen] = useState(false);
  const [deactivateTarget, setDeactivateTarget] = useState<Player | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Player | null>(null);

  const deactivate = useDeactivatePlayer(String(deactivateTarget?.id ?? ''));
  const deletePlayer = useDeletePlayer();

  async function handleDeactivate() {
    if (!deactivateTarget) return;
    await deactivate.mutateAsync();
    setDeactivateTarget(null);
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    await deletePlayer.mutateAsync(String(deleteTarget.id));
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
    return <ErrorMessage message="Failed to load players." />;
  }

  const columns = [
    {
      key: 'name',
      header: 'Name',
      sortable: true,
      render: (p: Player) => (
        <button
          className="font-medium text-[#1B5E20] hover:underline"
          onClick={() => navigate(`/admin/players/${p.id}`)}
        >
          {p.fullName}
        </button>
      ),
    },
    {
      key: 'email',
      header: 'Email',
      sortable: true,
      render: (p: Player) => p.email ?? <span className="text-gray-400">&mdash;</span>,
    },
    {
      key: 'handicap',
      header: 'Handicap',
      sortable: true,
      render: (p: Player) => p.currentHandicap?.toFixed(1) ?? '—',
    },
    {
      key: 'role',
      header: 'Roles',
      sortable: false,
      render: (p: Player) => (
        <span className="capitalize text-sm text-gray-700">
          {(p.roles ?? []).length === 0 ? <span className="text-gray-400">none</span> : (p.roles ?? []).join(', ')}
        </span>
      ),
    },
    {
      key: 'flight',
      header: 'Flight',
      sortable: true,
      render: (p: Player) => p.flightName ?? <span className="text-gray-400">Unassigned</span>,
    },
    {
      key: 'isActive',
      header: 'Status',
      sortable: true,
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
          <Button
            variant="ghost"
            size="sm"
            className="text-red-700 hover:bg-red-50 hover:text-red-800"
            onClick={(e) => {
              e.stopPropagation();
              setDeleteTarget(p);
            }}
          >
            Delete
          </Button>
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
        <DataTable
          columns={columns}
          data={players}
          rowKey={(p) => String(p.id)}
          emptyMessage="No players found."
          sort={sort}
          onSort={cycle}
        />
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-gray-600">
          <span>
            Page {page} of {totalPages} &mdash; {meta?.totalCount ?? 0} total players
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
            >
              <ChevronLeft className="h-4 w-4" />
              Previous
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
            >
              Next
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

      <div className="space-y-3 pt-8">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">Administrators</h2>
          <p className="text-sm text-gray-500">
            Accounts with no player profile &mdash; typically league admins who don&apos;t compete.
          </p>
        </div>
        <AdministratorsTable />
      </div>

      <Modal open={addOpen} title="Add Player" onClose={() => setAddOpen(false)}>
        <AddPlayerForm
          onSuccess={() => setAddOpen(false)}
          onCancel={() => setAddOpen(false)}
        />
      </Modal>

      <ConfirmDialog
        open={!!deactivateTarget}
        title="Deactivate Player"
        description={`Are you sure you want to deactivate ${deactivateTarget?.fullName}? They will no longer appear in active rounds.`}
        confirmLabel="Deactivate"
        destructive
        onConfirm={handleDeactivate}
        onCancel={() => setDeactivateTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Player"
        description={`Permanently delete ${deleteTarget?.fullName}? This cannot be undone and will remove all their data.`}
        confirmLabel="Delete"
        destructive
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
