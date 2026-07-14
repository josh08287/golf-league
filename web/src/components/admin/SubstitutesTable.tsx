import { Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '@/lib/utils';
import { useSubstitutes, useSetPlayerSubstitute } from '@/hooks/usePlayers';
import { DataTable } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import type { Player } from '@/types/api';

export function SubstitutesTable() {
  const prefix = useLeaguePrefix();
  const { data: substitutes = [], isLoading, error } = useSubstitutes();
  const setSubstitute = useSetPlayerSubstitute();

  if (isLoading) {
    return (
      <div className="flex h-32 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-red-600">Failed to load substitutes.</p>;
  }

  const columns = [
    {
      key: 'name',
      header: 'Name',
      render: (p: Player) => (
        <Link
          to={`${prefix}/admin/players/${p.id}`}
          className="font-medium text-[#1B5E20] hover:underline"
        >
          {p.fullName}
        </Link>
      ),
    },
    {
      key: 'email',
      header: 'Email',
      render: (p: Player) => p.email ?? <span className="text-gray-400">&mdash;</span>,
    },
    {
      key: 'handicap',
      header: 'Handicap',
      render: (p: Player) => (
        <span title={HANDICAP_PAIR_TOOLTIP}>{formatHandicapPair(p.currentHandicap)}</span>
      ),
    },
    {
      key: 'account',
      header: 'Account',
      render: (p: Player) =>
        p.appUserId ? (
          <Badge variant="success">Linked</Badge>
        ) : (
          <Badge variant="neutral">Not invited</Badge>
        ),
    },
    {
      key: 'isActive',
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
          {!p.appUserId && (
            <Link to={`${prefix}/admin/invites?preLinkPlayerId=${p.id}`}>
              <Button variant="ghost" size="sm">
                Invite
              </Button>
            </Link>
          )}
          <Button
            variant="ghost"
            size="sm"
            className="text-red-600 hover:bg-red-50 hover:text-red-700"
            disabled={setSubstitute.isPending}
            onClick={() => setSubstitute.mutate({ playerId: p.id, isSubstitute: false })}
          >
            Remove from pool
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
      <DataTable
        columns={columns}
        data={substitutes}
        rowKey={(p) => String(p.id)}
        emptyMessage="No substitutes yet."
      />
    </div>
  );
}
