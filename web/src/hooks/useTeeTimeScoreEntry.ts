import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type {
  MyTodaysTeeTime,
  TeeTimeGroupScorecard,
  TeeTimeGroupScoresResult,
  PlayerScoreInput,
  ConfirmedOverwrite,
  ScorecardOcrResult,
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
        const res = await apiClient.get('/me/todays-tee-time');
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
 * Mark a player in the tee time group as skipped (or un-skip them).
 * Any authenticated player in the group can call this.
 */
export function useSetTeeTimeParticipantSkipped(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ playerId, skipped }: { playerId: number; skipped: boolean }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      await apiClient.post(`/tee-times/${teeTimeId}/participants/${playerId}/skip`, { skipped });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeId != null ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId) : teeTimeScoreEntryKeys.all });
    },
  });
}

/**
 * Shotgun-start tournaments only: set which hole (1-18) the group is teeing
 * off on. Any authenticated player in the group can call this.
 */
export function useSetTeeTimeStartingHole(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (startingHoleNumber: number) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      await apiClient.post(`/tee-times/${teeTimeId}/starting-hole`, { startingHoleNumber });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeId != null ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId) : teeTimeScoreEntryKeys.all });
    },
  });
}

/**
 * Save scores for a single hole for all players in a tee time group.
 * Called when the user presses Next on each hole for incremental persistence.
 * Throws (with a 409 response) if a conflicting score was entered by another
 * player and not included in confirmedOverwrites.
 */
export function useSaveTeeTimeHoleScores(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      holeNumber,
      playerScores,
      confirmedOverwrites,
    }: {
      holeNumber: number;
      playerScores: PlayerScoreInput[];
      confirmedOverwrites?: ConfirmedOverwrite[];
    }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      await apiClient.put(`/tee-times/${teeTimeId}/holes/${holeNumber}/scores`, {
        playerScores,
        confirmedOverwrites,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeId != null ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId) : teeTimeScoreEntryKeys.all });
    },
  });
}

/**
 * Records (or clears) the closest-to-pin winner for a par-3 hole of a
 * tournament round, on behalf of the caller's tee-time group. Saved
 * immediately — call directly from the picker's onChange, not behind a
 * separate Save button, so the leaderboard stays live.
 */
export function useSetTeeTimeTournamentCtp(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ holeNumber, winnerPlayerId }: { holeNumber: number; winnerPlayerId: number | null }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      await apiClient.put(`/tee-times/${teeTimeId}/tournament-ctp/${holeNumber}`, { winnerPlayerId });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeId != null ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId) : teeTimeScoreEntryKeys.all });
    },
  });
}

/**
 * Records (or clears) the longest-drive winner for a tournament flight, on
 * the round's configured hole, on behalf of the caller's tee-time group.
 * Saved immediately, same as CTP.
 */
export function useSetTeeTimeTournamentLongestDrive(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ tournamentFlightId, winnerPlayerId }: { tournamentFlightId: number; winnerPlayerId: number | null }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      await apiClient.put(`/tee-times/${teeTimeId}/tournament-longest-drive/${tournamentFlightId}`, { winnerPlayerId });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeId != null ? teeTimeScoreEntryKeys.groupScorecard(teeTimeId) : teeTimeScoreEntryKeys.all });
    },
  });
}

/**
 * Uploads a scorecard photo and returns OCR'd per-player hole scores for the
 * user to confirm/edit. The image is processed in memory server-side and
 * never persisted — nothing to clean up here once the request completes.
 */
export function useParseScorecardImage(teeTimeId: number | null) {
  return useMutation({
    mutationFn: async (image: File) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      const formData = new FormData();
      formData.append('image', image);
      // apiClient defaults every request to Content-Type: application/json;
      // clear it here so axios can set its own multipart boundary instead
      // of sending the FormData body under the wrong content type.
      const res = await apiClient.post(`/tee-times/${teeTimeId}/scorecard-ocr`, formData, {
        headers: { 'Content-Type': undefined },
      });
      return unwrap<ScorecardOcrResult>(res.data);
    },
  });
}

/**
 * Submit scores for all players in a tee time group.
 * Throws (with a 409 response) if a conflicting score was entered by another
 * player and not included in confirmedOverwrites.
 */
export function useSubmitTeeTimeGroupScores(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      playerScores,
      confirmedOverwrites,
    }: {
      playerScores: PlayerScoreInput[];
      confirmedOverwrites?: ConfirmedOverwrite[];
    }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      const res = await apiClient.post(`/tee-times/${teeTimeId}/submit-scores`, {
        playerScores,
        confirmedOverwrites,
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
