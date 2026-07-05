import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { TableSort } from '@/hooks/useSortableTable';
import type {
  Player,
  HandicapHistoryEntry,
  PagedResponse,
  PlayerRoundSummary,
  UnlinkedPlayer,
} from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

// ── Query key factory ─────────────────────────────────────────────────────────
export const playerKeys = {
  all: ['players'] as const,
  lists: () => [...playerKeys.all, 'list'] as const,
  list: (page: number, pageSize: number, sort?: TableSort) =>
    [...playerKeys.lists(), { page, pageSize, sort: sort ?? null }] as const,
  details: () => [...playerKeys.all, 'detail'] as const,
  detail: (id: string) => [...playerKeys.details(), id] as const,
  handicapHistory: (playerId: string) =>
    [...playerKeys.all, 'handicapHistory', playerId] as const,
  rounds: (playerId: string) =>
    [...playerKeys.all, 'rounds', playerId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function usePlayers(page = 1, sort?: TableSort, pageSize = 20) {
  return useQuery({
    queryKey: playerKeys.list(page, pageSize, sort),
    queryFn: async () => {
      const params: Record<string, string | number> = { page, pageSize };
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<PagedResponse<Player>>('/players', { params });
      return response.data;
    },
  });
}

export function usePlayer(id: string) {
  return useQuery({
    queryKey: playerKeys.detail(id),
    queryFn: async () => {
      const response = await apiClient.get<Player>(`/players/${id}`);
      return response.data;
    },
    enabled: Boolean(id),
  });
}

export function useHandicapHistory(playerId: string, sort?: TableSort) {
  return useQuery({
    queryKey: [...playerKeys.handicapHistory(playerId), { sort: sort ?? null }] as const,
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<HandicapHistoryEntry[]>(
        `/players/${playerId}/handicap-history`,
        { params },
      );
      return response.data;
    },
    enabled: Boolean(playerId),
  });
}

/**
 * Active players that have no AppUser linked. Used by admin to pick which
 * player should be attached to a new user account (link action or invite
 * pre-attach). Admin-only on the server.
 */
export function useUnlinkedPlayers(enabled: boolean = true) {
  return useQuery({
    queryKey: [...playerKeys.all, 'unlinked'] as const,
    queryFn: async () => {
      const res = await apiClient.get('/admin/players/unlinked');
      return unwrap<UnlinkedPlayer[]>(res.data);
    },
    enabled,
  });
}

export function useSetTeeTimeEmailOptOut() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ playerId, optOut }: { playerId: number; optOut: boolean }) => {
      await apiClient.put(`/players/${playerId}/tee-time-email-opt-out`, { optOut });
    },
    onSuccess: (_data, { playerId }) => {
      qc.invalidateQueries({ queryKey: playerKeys.detail(String(playerId)) });
    },
  });
}

/**
 * Skip or unskip one of the current player's own upcoming rounds, from the
 * player profile page. Reuses the same self-skip endpoint as the tee-times
 * page, but also invalidates this player's rounds list so the profile page
 * strikethrough/unskip UI refreshes immediately.
 */
export function usePlayerSkipRound() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ roundId, skipped }: { roundId: number; skipped: boolean }) => {
      await apiClient.post(`/rounds/${roundId}/tee-times/me/skip`, { skipped });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
    },
  });
}

export function usePlayerRounds(playerId: string, sort?: TableSort) {
  return useQuery({
    queryKey: [...playerKeys.rounds(playerId), { sort: sort ?? null }] as const,
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<PlayerRoundSummary[]>(
        `/players/${playerId}/rounds`,
        { params },
      );
      return response.data;
    },
    enabled: Boolean(playerId),
  });
}
