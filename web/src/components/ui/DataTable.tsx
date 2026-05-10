import React from 'react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './Table';
import { SortableTableHead } from './SortableTableHead';
import type { TableSort } from '@/hooks/useSortableTable';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => React.ReactNode;
  /**
   * Set true to make this column header click-to-sort. The column key is
   * sent to the backend as `sortBy=<key>`.
   */
  sortable?: boolean;
  /** Optional className applied to the header cell. */
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  rowKey: (row: T) => string | number;
  emptyMessage?: string;
  /**
   * Pass these together when any column has `sortable: true`. The hook
   * `useSortableTable` returns the matching shape.
   */
  sort?: TableSort;
  onSort?: (column: string) => void;
}

export function DataTable<T>({
  columns,
  data,
  rowKey,
  emptyMessage = 'No data.',
  sort,
  onSort,
}: DataTableProps<T>) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          {columns.map((col) => {
            if (col.sortable && sort && onSort) {
              return (
                <SortableTableHead
                  key={col.key}
                  column={col.key}
                  sort={sort}
                  onSort={onSort}
                  className={col.className}
                >
                  {col.header}
                </SortableTableHead>
              );
            }
            return (
              <TableHead key={col.key} className={col.className}>
                {col.header}
              </TableHead>
            );
          })}
        </TableRow>
      </TableHeader>
      <TableBody>
        {data.length === 0 ? (
          <TableRow>
            <TableCell colSpan={columns.length} className="text-center text-gray-500 py-8">
              {emptyMessage}
            </TableCell>
          </TableRow>
        ) : (
          data.map((row) => (
            <TableRow key={rowKey(row)}>
              {columns.map((col) => (
                <TableCell key={col.key}>{col.render(row)}</TableCell>
              ))}
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  );
}
