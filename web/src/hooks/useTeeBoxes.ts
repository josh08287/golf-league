import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';
import { teeTimeScoreEntryKeys } from '@/hooks/useTeeTimeScoreEntry';
import type { ParticipantTee, PlayerTeeBoxSettings, TeeBoxOption } from '@/types/api';

export const teeBoxKeys = {
  playerSettings: (playerId: string) => ['players', 'tee-boxes', playerId] as const,
};

/** Formats a tee for a picker: "Blue (72.1 / 131)". */
export function teeBoxLabel(tee: Pick<TeeBoxOption, 'name' | 'courseRating' | 'slopeRating'>): string {
  return `${tee.name} (${tee.courseRating} / ${tee.slopeRating})`;
}

// ── Per-half defaults (admin player page) ────────────────────────────────────

export function usePlayerTeeBoxSettings(playerId: string, enabled = true) {
  return useQuery({
    queryKey: teeBoxKeys.playerSettings(playerId),
    queryFn: async () => {
      const res = await apiClient.get<PlayerTeeBoxSettings>(`/players/${playerId}/tee-boxes`);
      return res.data;
    },
    enabled: Boolean(playerId) && enabled,
  });
}

export function useSetPlayerHalfTeeBox(playerId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ halfId, courseId, teeBoxId }: { halfId: number; courseId: number; teeBoxId: number | null }) =>
      apiClient
        .put<PlayerTeeBoxSettings>(`/players/${playerId}/half-settings/${halfId}/tee-box`, { courseId, teeBoxId })
        .then((r) => r.data),
    onSuccess: (data) => {
      qc.setQueryData(teeBoxKeys.playerSettings(playerId), data);
      // Unstarted rounds may have moved to the new tee.
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

// ── Per-round override (score entry) ─────────────────────────────────────────

/** Admin/scorer score entry: change one player's tee for a round. `teeBoxId: null` reverts to their default. */
export function useSetRoundParticipantTeeBox(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ playerId, teeBoxId }: { playerId: number; teeBoxId: number | null }) =>
      apiClient
        .put<ParticipantTee>(`/rounds/${roundId}/participants/${playerId}/tee-box`, { teeBoxId })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

/** Tee-time group score entry: change a group member's tee for the round. */
export function useSetTeeTimeParticipantTeeBox(teeTimeId: number | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ playerId, teeBoxId }: { playerId: number; teeBoxId: number | null }) => {
      if (teeTimeId == null) throw new Error('teeTimeId required');
      return apiClient
        .put<ParticipantTee>(`/tee-times/${teeTimeId}/participants/${playerId}/tee-box`, { teeBoxId })
        .then((r) => r.data);
    },
    onSuccess: () => {
      if (teeTimeId != null)
        qc.invalidateQueries({ queryKey: teeTimeScoreEntryKeys.groupScorecard(teeTimeId) });
    },
  });
}
