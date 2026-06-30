import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { TableSort } from '@/hooks/useSortableTable';
import type {
  Round,
  Scorecard,
  RoundScorecard,
  RoundSkins,
  TournamentResults,
  PagedResponse,
} from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const roundKeys = {
  all: ['rounds'] as const,
  lists: () => [...roundKeys.all, 'list'] as const,
  list: (page: number, sort?: TableSort) =>
    [...roundKeys.lists(), { page, sort: sort ?? null }] as const,
  details: () => [...roundKeys.all, 'detail'] as const,
  detail: (id: string) => [...roundKeys.details(), id] as const,
  scorecard: (roundId: string, playerId: string) =>
    [...roundKeys.all, 'scorecard', roundId, playerId] as const,
  scorecards: (roundId: string, sort?: TableSort) =>
    [...roundKeys.all, 'scorecards', roundId, { sort: sort ?? null }] as const,
  skins: (roundId: string) => [...roundKeys.all, 'skins', roundId] as const,
  tournamentResults: (roundId: string) => [...roundKeys.all, 'tournament-results', roundId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function useRounds(page = 1, sort?: TableSort, pageSize = 20) {
  return useQuery({
    queryKey: [...roundKeys.list(page, sort), pageSize] as const,
    queryFn: async () => {
      const params: Record<string, string | number> = { page, pageSize };
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<PagedResponse<Round>>('/rounds', { params });
      return response.data;
    },
  });
}

export function useRound(id: string) {
  return useQuery({
    queryKey: roundKeys.detail(id),
    queryFn: async () => {
      const response = await apiClient.get<Round>(`/rounds/${id}`);
      return response.data;
    },
    enabled: Boolean(id),
  });
}

export function useScorecard(roundId: string, playerId: string) {
  return useQuery({
    queryKey: roundKeys.scorecard(roundId, playerId),
    queryFn: async () => {
      const response = await apiClient.get<Scorecard>(
        `/rounds/${roundId}/scorecards/${playerId}`,
      );
      return response.data;
    },
    enabled: Boolean(roundId) && Boolean(playerId),
  });
}

export function useRoundScorecards(roundId: string, sort?: TableSort) {
  return useQuery({
    queryKey: roundKeys.scorecards(roundId, sort),
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<PagedResponse<RoundScorecard>>(
        `/rounds/${roundId}/scorecards`,
        { params },
      );
      return response.data;
    },
    enabled: Boolean(roundId),
  });
}

export function useRoundSkins(roundId: string) {
  return useQuery({
    queryKey: roundKeys.skins(roundId),
    queryFn: async () => {
      const response = await apiClient.get<RoundSkins>(`/rounds/${roundId}/skins`);
      return response.data;
    },
    enabled: Boolean(roundId),
  });
}

export function useTournamentResults(roundId: string) {
  return useQuery({
    queryKey: roundKeys.tournamentResults(roundId),
    queryFn: async () => {
      const response = await apiClient.get<TournamentResults>(`/tournament-rounds/${roundId}/results`);
      return response.data;
    },
    enabled: Boolean(roundId),
  });
}
