import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';

// ── Types ──────────────────────────────────────────────────────────────────

export interface CreateRoundPayload {
  scheduledDate: string; // ISO date string — sent as scheduledDate, backend accepts it
  courseId: number | string;
  flightId?: number | string; // Legacy single flight
  flightIds?: number[]; // Multiple flights
  seasonId?: number | string;
  playerIds?: number[];
  notes?: string;
  roundType?: 'NineHole' | 'EighteenHole';
  nineHoleSide?: 'Front' | 'Back';
}

export interface HoleScoreInput {
  holeNumber: number;
  grossScore: number;
}

export interface SubmitHoleScoresPayload {
  playerId: number | string;
  scores: HoleScoreInput[];
}

// ── Hooks ──────────────────────────────────────────────────────────────────

export function useCreateRound() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateRoundPayload) =>
      apiClient.post('/rounds', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

export function useSubmitHoleScores(roundId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ playerId, scores }: SubmitHoleScoresPayload) =>
      apiClient
        .put(`/rounds/${roundId}/scores/${playerId}/holes`, { scores })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.scorecards(roundId) });
    },
  });
}

export function useFinalizeRound(roundId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: () =>
      apiClient.post(`/rounds/${roundId}/finalize`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

export function useDeleteRound() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (roundId: string) =>
      apiClient.delete(`/rounds/${roundId}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}
