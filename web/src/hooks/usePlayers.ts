import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type {
  Player,
  HandicapHistoryEntry,
  PagedResponse,
} from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const playerKeys = {
  all: ['players'] as const,
  lists: () => [...playerKeys.all, 'list'] as const,
  list: (page: number) => [...playerKeys.lists(), { page }] as const,
  details: () => [...playerKeys.all, 'detail'] as const,
  detail: (id: string) => [...playerKeys.details(), id] as const,
  handicapHistory: (playerId: string) =>
    [...playerKeys.all, 'handicapHistory', playerId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function usePlayers(page = 1) {
  return useQuery({
    queryKey: playerKeys.list(page),
    queryFn: async () => {
      const response = await apiClient.get<PagedResponse<Player>>(
        '/players',
        { params: { page, pageSize: 20 } },
      );
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

export function useHandicapHistory(playerId: string) {
  return useQuery({
    queryKey: playerKeys.handicapHistory(playerId),
    queryFn: async () => {
      const response = await apiClient.get<HandicapHistoryEntry[]>(
        `/players/${playerId}/handicap-history`,
      );
      return response.data;
    },
    enabled: Boolean(playerId),
  });
}
