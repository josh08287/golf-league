import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { playerKeys } from '@/hooks/usePlayers';

// ── Types ──────────────────────────────────────────────────────────────────

export interface CreatePlayerPayload {
  name: string;
  email: string;
  initialHandicap: number;
  flightId?: string;
}

export interface UpdatePlayerPayload {
  name?: string;
  email?: string;
  flightId?: string | null;
  roles?: string[];
}

export interface SetHandicapPayload {
  newIndex: number;
  notes?: string;
}

// ── Hooks ──────────────────────────────────────────────────────────────────

export function useCreatePlayer() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreatePlayerPayload) =>
      apiClient.post('/players', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
    },
  });
}

export function useUpdatePlayer(playerId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdatePlayerPayload) =>
      apiClient.patch(`/players/${playerId}`, payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
      qc.invalidateQueries({ queryKey: playerKeys.detail(playerId) });
    },
  });
}

export function useDeactivatePlayer(playerId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: () =>
      apiClient.post(`/players/${playerId}/deactivate`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
    },
  });
}

export function useDeletePlayer() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (playerId: string) =>
      apiClient.delete(`/players/${playerId}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
    },
  });
}

export function useSetHandicap(playerId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: SetHandicapPayload) =>
      apiClient
        .post(`/players/${playerId}/handicap`, { newIndex: payload.newIndex, notes: payload.notes })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.detail(playerId) });
      qc.invalidateQueries({ queryKey: playerKeys.handicapHistory(playerId) });
      qc.invalidateQueries({ queryKey: playerKeys.all });
    },
  });
}
