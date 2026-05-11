import { useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '@/lib/utils';
import { usePlayers } from '@/hooks/usePlayers';
import { useSortableTable } from '@/hooks/useSortableTable';
import {
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableRow,
} from '@/components/ui/Table';
import { SortableTableHead } from '@/components/ui/SortableTableHead';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';

export function PlayersPage() {
  const navigate = useNavigate();
  const { sort, cycle } = useSortableTable('players');
  // Public directory: fetch the full roster so client-side search/filter
  // sees everyone, not just the first 20.
  const players = usePlayers(1, sort, 1000);
  const [query, setQuery] = useState('');

  const filtered = useMemo(() => {
    const data = players.data?.data ?? [];
    if (!query.trim()) return data;
    const q = query.toLowerCase();
    return data.filter(
      (p) =>
        p.fullName.toLowerCase().includes(q) ||
        (p.flightName?.toLowerCase().includes(q) ?? false),
    );
  }, [players.data, query]);

  return (
    <div className="space-y-6">
      <PageHeader title="Players" description="League roster and current handicaps" />

      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
        <input
          type="search"
          placeholder="Search by name or flight…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          className="w-full rounded-lg border border-gray-200 bg-white py-2 pl-9 pr-3 text-sm shadow-sm focus:border-primary-500 focus:outline-none focus:ring-1 focus:ring-primary-500"
        />
      </div>

      {players.isPending && <FullPageSpinner />}
      {players.isError && (
        <ErrorMessage message="Could not load players. Please try again." />
      )}

      {players.data && (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-gray-50">
                <SortableTableHead column="name" sort={sort} onSort={cycle}>
                  Player
                </SortableTableHead>
                <SortableTableHead column="flight" sort={sort} onSort={cycle}>
                  Flight
                </SortableTableHead>
                <SortableTableHead column="handicap" sort={sort} onSort={cycle} className="text-right">
                  Hcp <span className="text-xs font-normal text-gray-400">18 / 9</span>
                </SortableTableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} className="text-center text-sm text-gray-500 py-8">
                    {query ? 'No players match your search.' : 'No players yet.'}
                  </TableCell>
                </TableRow>
              )}
              {filtered.map((p) => (
                <TableRow
                  key={p.id}
                  onClick={() => navigate(`/players/${p.id}`)}
                  className="cursor-pointer"
                >
                  <TableCell className="font-medium text-primary-900">{p.fullName}</TableCell>
                  <TableCell className="text-gray-600">{p.flightName ?? '—'}</TableCell>
                  <TableCell className="text-right font-semibold tabular-nums" title={HANDICAP_PAIR_TOOLTIP}>
                    {formatHandicapPair(p.currentHandicap)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
