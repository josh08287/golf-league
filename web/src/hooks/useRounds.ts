import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type {
  Round,
  Scorecard,
  PagedResponse,
  ApiResponse,
} from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const roundKeys = {
  all: ['rounds'] as const,
  lists: () => [...roundKeys.all, 'list'] as const,
  list: (page: number) => [...roundKeys.lists(), { page }] as const,
  details: () => [...roundKeys.all, 'detail'] as const,
  detail: (id: string) => [...roundKeys.details(), id] as const,
  scorecard: (roundId: string, playerId: string) =>
    [...roundKeys.all, 'scorecard', roundId, playerId] as const,
  scorecards: (roundId: string) =>
    [...roundKeys.all, 'scorecards', roundId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function useRounds(page = 1) {
  return useQuery({
    queryKey: roundKeys.list(page),
    queryFn: async () => {
      const response = await apiClient.get<PagedResponse<Round>>(
        '/rounds',
        { params: { page, pageSize: 20 } },
      );
      return response.data;
    },
  });
}

export function useRound(id: string) {
  return useQuery({
    queryKey: roundKeys.detail(id),
    queryFn: async () => {
      const response = await apiClient.get<ApiResponse<Round>>(
        `/rounds/${id}`,
      );
      return response.data;
    },
    enabled: Boolean(id),
  });
}

export function useScorecard(roundId: string, playerId: string) {
  return useQuery({
    queryKey: roundKeys.scorecard(roundId, playerId),
    queryFn: async () => {
      const response = await apiClient.get<ApiResponse<Scorecard>>(
        `/rounds/${roundId}/scorecards/${playerId}`,
      );
      return response.data;
    },
    enabled: Boolean(roundId) && Boolean(playerId),
  });
}

export function useRoundScorecards(roundId: string) {
  return useQuery({
    queryKey: roundKeys.scorecards(roundId),
    queryFn: async () => {
      const response = await apiClient.get<PagedResponse<Scorecard>>(
        `/rounds/${roundId}/scorecards`,
      );
      return response.data;
    },
    enabled: Boolean(roundId),
  });
}
