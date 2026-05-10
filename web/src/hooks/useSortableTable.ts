import { useCallback, useEffect, useState } from 'react';

export type SortDirection = 'asc' | 'desc';

export interface TableSort {
  sortBy: string;
  sortDir: SortDirection;
}

const STORAGE_PREFIX = 'gl_sort_';

function readStored(key: string): TableSort | null {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<TableSort>;
    if (!parsed.sortBy || (parsed.sortDir !== 'asc' && parsed.sortDir !== 'desc')) return null;
    return { sortBy: parsed.sortBy, sortDir: parsed.sortDir };
  } catch {
    return null;
  }
}

function writeStored(key: string, value: TableSort | null) {
  try {
    if (value === null) localStorage.removeItem(STORAGE_PREFIX + key);
    else localStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(value));
  } catch {
    // ignore quota / privacy mode
  }
}

/**
 * Sentinel sort that tells the backend to apply its multi-column default
 * (e.g. flight → last → first). When the user cycles a column past `desc`
 * the third click returns the table to this sentinel.
 */
export const DEFAULT_SORT: TableSort = { sortBy: 'default', sortDir: 'asc' };

/**
 * Per-table sort state with localStorage persistence. Each table passes a
 * unique `storageKey`. When no preference is stored or after the user
 * cycles a column to "unsorted", the hook returns DEFAULT_SORT — which
 * the backend treats as "apply your default ordering".
 *
 * Click cycle: column-A asc → column-A desc → DEFAULT_SORT.
 */
export function useSortableTable(storageKey: string) {
  const [sort, setSortState] = useState<TableSort>(() => readStored(storageKey) ?? DEFAULT_SORT);

  // If the storage key changes (e.g. a re-mount with a different table),
  // pick up that table's stored value.
  useEffect(() => {
    setSortState(readStored(storageKey) ?? DEFAULT_SORT);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [storageKey]);

  const cycle = useCallback(
    (column: string) => {
      setSortState((prev) => {
        let next: TableSort;
        if (prev.sortBy !== column) {
          next = { sortBy: column, sortDir: 'asc' };
        } else if (prev.sortDir === 'asc') {
          next = { sortBy: column, sortDir: 'desc' };
        } else {
          next = DEFAULT_SORT;
        }
        if (next.sortBy === DEFAULT_SORT.sortBy && next.sortDir === DEFAULT_SORT.sortDir) {
          writeStored(storageKey, null);
        } else {
          writeStored(storageKey, next);
        }
        return next;
      });
    },
    [storageKey],
  );

  return { sort, cycle };
}
