import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';

export interface CreateRoundPayload {
  halfId: number;
  courseId: number;
  scheduledDate: string;
  notes?: string;
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

export function useSetParticipantSkipped(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ playerId, skipped }: { playerId: number | string; skipped: boolean }) =>
      apiClient
        .post(`/rounds/${roundId}/participants/${playerId}/skip`, { skipped })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
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
      qc.invalidateQueries({ queryKey: roundKeys.scorecards(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

export function useReopenRound() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (roundId: string) =>
      apiClient.post(`/rounds/${roundId}/reopen`).then((r) => r.data),
    onSuccess: (_data, roundId) => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.scorecards(roundId) });
    },
  });
}

export function useCancelRound() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (roundId: string) =>
      apiClient.post(`/rounds/${roundId}/cancel`).then((r) => r.data),
    onSuccess: () => {
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

export interface MatchupInput {
  player1Id: number;
  player2Id: number;
}

export interface CreateTournamentRoundPayload {
  seasonId: number;
  courseId: number;
  roundDate: string;
  playerIds: number[];
  matchups?: MatchupInput[];
  notes?: string;
  longestDriveHoleNumber?: number;
  grossSkinsPool?: number;
  netSkinsPool?: number;
}

export function useCreateTournamentRound() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateTournamentRoundPayload) =>
      apiClient.post('/tournament-rounds', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}

export interface HoleExtraInput {
  holeNumber: number;
  closestToPinPlayerId?: number | null;
  longestDrivePlayerId?: number | null;
}

export function useSetTournamentMatchups(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (matchups: MatchupInput[]) =>
      apiClient.put(`/tournament-rounds/${roundId}/matchups`, { matchups }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
    },
  });
}

export function useRegenerateTournamentMatchups(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiClient.post(`/tournament-rounds/${roundId}/matchups/regenerate`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
    },
  });
}

export function useSaveTournamentExtras(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (holeExtras: HoleExtraInput[]) =>
      apiClient.put(`/tournament-rounds/${roundId}/extras`, { holeExtras }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
    },
  });
}

export function useSetTournamentLongestDriveHole(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (holeNumber: number | null) =>
      apiClient.put(`/tournament-rounds/${roundId}/longest-drive-hole`, { holeNumber }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
    },
  });
}

export interface SkinsPoolPayload {
  grossSkinsPool: number | null;
  netSkinsPool: number | null;
}

export function useSetTournamentSkinsPool(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SkinsPoolPayload) =>
      apiClient.put(`/tournament-rounds/${roundId}/skins-pool`, payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
    },
  });
}

export function useAddTournamentParticipants(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (playerIds: number[]) =>
      apiClient.post(`/tournament-rounds/${roundId}/participants`, { playerIds }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
    },
  });
}

export function useRemoveTournamentParticipant(roundId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (playerId: number | string) =>
      apiClient.delete(`/tournament-rounds/${roundId}/participants/${playerId}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
      qc.invalidateQueries({ queryKey: roundKeys.tournamentResults(roundId) });
      qc.invalidateQueries({ queryKey: roundKeys.detail(roundId) });
    },
  });
}

