import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type {
  MyTodaysTeeTime,
  TeeTimeGroupScorecard,
  TeeTimeGroupScoresResult,
  PlayerScoreInput,
} from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

// ── Query key factory ─────────────────────────────────────────────────────────
export const teeTimeScoreEntryKeys = {
  all: ['teeTimeScoreEntry'] as const,
  myTodaysTeeTime: () => [...teeTimeScoreEntryKeys.all, 'myTodays'] as const,
  groupScorecard: (teeTimeId: number) =>
    [...teeTimeScoreEntryKeys.all, 'groupScorecard', teeTimeId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

/**
 * Fetch today's tee time info for the authenticated player.
 * Returns null if there's no round today or player isn't assigned to a tee time.
 */
export function useMyTodaysTeeTime(enabled: boolean = true) {
  return useQuery({
    queryKey: teeTimeScoreEntryKeys.myTodaysTeeTime(),
    queryFn: async () => {
      try {
        const res = await apiClient.get('/players/me/todays-tee-time');
        return unwrap<MyTodaysTeeTime>(res.data);
      } catch (error: unknown) {
        // 404 means no tee time today - return null instead of throwing
        if (error && typeof error === 'object' && 'response' in error) {
          const axiosError = error as { response?: { status?: number } };
          if (axiosError.response?.status === 404) {
            return null;
          }
        }
        throw error;
      }
    },
    enabled,
    // Don't retry on 404 - it's a valid "no data" state
    retry: (failureCount, error) => {
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as { response?: { status?: number } };
        if (axiosError.response?.status === 404) {
          return false;
        }
      }
      return failureCount < 3;
    },
  });
}

/**
 * Fetch the complete scorecard for all players in a tee time group.
 */
export function useTeeTimeGroupScorecard(teeTimeId: number | null) {
  return useQuery({
    queryKey: teeTimeId != null
      ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId)
      : teeTimeScoreEntryKeys.all,
    queryFn: async () => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      const res = await apiClient.get(`/tee-times/${teeTimeId}/group-scorecard`);
      return unwrap<TeeTimeGroupScorecard>(res.data);
    },
    enabled: teeTimeId != null,
  });
}

/**
 * Submit scores for all players in a tee time group.
 */
export function useSubmitTeeTimeGroupScores(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (playerScores: PlayerScoreInput[]) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      const res = await apiClient.post(`/tee-times/${teeTimeId}/submit-scores`, {
        playerScores,
      });
      return unwrap<TeeTimeGroupScoresResult>(res.data);
    },
    onSuccess: () => {
      // Invalidate related queries
      qc.invalidateQueries({ queryKey: teeTimeScoreEntryKeys.all });
      qc.invalidateQueries({ queryKey: ['rounds'] });
    },
  });
}
