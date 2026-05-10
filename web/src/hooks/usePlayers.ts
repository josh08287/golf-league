import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { TableSort } from '@/hooks/useSortableTable';
import type {
  Player,
  HandicapHistoryEntry,
  PagedResponse,
  PlayerRoundSummary,
} from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const playerKeys = {
  all: ['players'] as const,
  lists: () => [...playerKeys.all, 'list'] as const,
  list: (page: number, sort?: TableSort) =>
    [...playerKeys.lists(), { page, sort: sort ?? null }] as const,
  details: () => [...playerKeys.all, 'detail'] as const,
  detail: (id: string) => [...playerKeys.details(), id] as const,
  handicapHistory: (playerId: string) =>
    [...playerKeys.all, 'handicapHistory', playerId] as const,
  rounds: (playerId: string) =>
    [...playerKeys.all, 'rounds', playerId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function usePlayers(page = 1, sort?: TableSort) {
  return useQuery({
    queryKey: playerKeys.list(page, sort),
    queryFn: async () => {
      const params: Record<string, string | number> = { page, pageSize: 20 };
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
