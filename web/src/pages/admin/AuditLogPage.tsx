import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { api } from '../../lib/api';
import { useSortableTable } from '../../hooks/useSortableTable';
import { PageHeader } from '../../components/ui/PageHeader';
import { DataTable } from '../../components/ui/DataTable';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Button } from '../../components/ui/Button';

// ── Types ──────────────────────────────────────────────────────────────────

interface AuditLogEntry {
  id: string;
  timestamp: string;
  action: string;
  entityType: string;
  entity: string;
  user: string;
  details?: string;
}

interface AuditLogPage {
  items: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ── Action badge colors ────────────────────────────────────────────────────

function actionVariant(action: string): 'success' | 'warning' | 'neutral' | 'info' {
  const a = action.toUpperCase();
  if (a.includes('CREATE') || a.includes('ADD')) return 'success';
  if (a.includes('DELETE') || a.includes('DEACTIVATE') || a.includes('FINALIZE')) return 'warning';
  if (a.includes('UPDATE') || a.includes('EDIT') || a.includes('SET')) return 'info';
  return 'neutral';
}

// ── Page ───────────────────────────────────────────────────────────────────

const PAGE_SIZE = 25;

export function AuditLogPage() {
  const [page, setPage] = useState(1);
  const { sort, cycle } = useSortableTable('auditLog');

  const { data, isLoading, error } = useQuery<AuditLogPage>({
    queryKey: ['audit-log', page, sort],
    queryFn: () => {
      const params: Record<string, string | number> = { page, pageSize: PAGE_SIZE };
      params.sortBy = sort.sortBy;
      params.sortDir = sort.sortDir;
      return api.get('/admin/audit-log', { params }).then((r) => r.data);
    },
    placeholderData: (prev) => prev,
  });

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1;

  const columns = [
    {
      key: 'timestamp',
      header: 'Timestamp',
      sortable: true,
      render: (e: AuditLogEntry) =>
        new Date(e.timestamp).toLocaleString(undefined, {
          dateStyle: 'short',
          timeStyle: 'medium',
        }),
    },
    {
      key: 'action',
      header: 'Action',
      sortable: true,
      render: (e: AuditLogEntry) => (
        <Badge variant={actionVariant(e.action)}>{e.action}</Badge>
      ),
    },
    {
      key: 'entityType',
      header: 'Entity Type',
      sortable: true,
      render: (e: AuditLogEntry) => (
        <span className="font-mono text-xs text-gray-600">{e.entityType}</span>
      ),
    },
    {
      key: 'entity',
      header: 'Entity',
      sortable: true,
      render: (e: AuditLogEntry) => (
        <span className="text-gray-700">{e.entity}</span>
      ),
    },
    {
      key: 'user',
      header: 'User',
      sortable: true,
      render: (e: AuditLogEntry) => (
        <span className="text-gray-700">{e.user}</span>
      ),
    },
    {
      key: 'details',
      header: 'Details',
      render: (e: AuditLogEntry) => (
        <span className="text-gray-500">{e.details ?? '—'}</span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Log"
        subtitle="Read-only record of all administrative actions"
      />

      {isLoading && (
        <div className="flex h-64 items-center justify-center">
          <Spinner />
        </div>
      )}

      {error && <ErrorMessage message="Failed to load audit log." />}

      {!isLoading && !error && (
        <>
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <DataTable
              columns={columns}
              data={data?.items ?? []}
              rowKey={(e) => e.id}
              emptyMessage="No audit log entries."
              sort={sort}
              onSort={cycle}
            />
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm text-gray-600">
              <span>
                Page {page} of {totalPages} — {data?.totalCount ?? 0} total entries
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
        </>
      )}
    </div>
  );
}
