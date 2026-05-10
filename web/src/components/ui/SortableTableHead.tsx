import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import * as React from 'react';
import { TableHead } from '@/components/ui/Table';
import { cn } from '@/lib/utils';
import type { TableSort } from '@/hooks/useSortableTable';

interface SortableTableHeadProps extends React.ThHTMLAttributes<HTMLTableCellElement> {
  /** Server-side column name passed to the API as `sortBy`. */
  column: string;
  /** Current sort state from useSortableTable. */
  sort: TableSort;
  /** Cycle handler returned by useSortableTable. */
  onSort: (column: string) => void;
}

/**
 * Wraps a TableHead with click-to-sort behavior. Renders an arrow icon
 * indicating the current direction when this column is the active sort,
 * or a faint up/down hint when inactive.
 */
export function SortableTableHead({
  column,
  sort,
  onSort,
  children,
  className,
  ...rest
}: SortableTableHeadProps) {
  const active = sort.sortBy === column;
  const dir = active ? sort.sortDir : null;

  return (
    <TableHead className={cn('cursor-pointer select-none', className)} {...rest}>
      <button
        type="button"
        onClick={() => onSort(column)}
        className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-gray-500 hover:text-gray-700"
      >
        {children}
        {dir === 'asc' && <ChevronUp className="h-3.5 w-3.5 text-primary-700" />}
        {dir === 'desc' && <ChevronDown className="h-3.5 w-3.5 text-primary-700" />}
        {dir === null && <ChevronsUpDown className="h-3.5 w-3.5 text-gray-300" />}
      </button>
    </TableHead>
  );
}
