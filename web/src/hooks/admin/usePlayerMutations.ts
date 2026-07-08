import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { playerKeys } from '@/hooks/usePlayers';

// ── Types ──────────────────────────────────────────────────────────────────

export interface CreatePlayerPayload {
  name: string;
  email?: string;
  initialHandicap: number;
  flightId?: string;
}

export interface UpdatePlayerPayload {
  name?: string;
  email?: string | null;
  flightId?: string | null;
  roles?: string[];
}

export interface SetHandicapPayload {
  newIndex: number;
  notes?: string;
}

// ── Hooks ──────────────────────────────────────────────────────────────────

/**
 * Manually link an existing unlinked Player to an existing AppUser. Admin-only.
 * Backend refuses if either side is already linked; surfaces a 409 with a
 * clear error message.
 */
export function useLinkPlayerToUser(playerId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) =>
      apiClient.post(`/players/${playerId}/link-user`, { userId }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
      qc.invalidateQueries({ queryKey: ['admin', 'users'] });
    },
  });
}

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

/**
 * Add, move, or remove a player for a single season half. `flightId: null`
 * removes the player from that half. The backend rejects changes to a half
 * whose rounds have already started (locked).
 */
export function useSetHalfMembership(playerId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ halfId, flightId }: { halfId: number; flightId: number | null }) =>
      apiClient
        .put(`/players/${playerId}/half-membership`, { halfId, flightId })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: playerKeys.all });
      qc.invalidateQueries({ queryKey: playerKeys.detail(playerId) });
    },
  });
}

/**
 * Sets whether a player is opted in to par-3 gross skins for a single half.
 * Independent of flight assignment, so it survives flight reassignment.
 */
export function useSetPar3GrossSkinsOptIn(playerId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: ({ halfId, optIn }: { halfId: number; optIn: boolean }) =>
      apiClient
        .put(`/players/${playerId}/half-settings/${halfId}/par3-gross-skins-opt-in`, { optIn })
        .then((r) => r.data),
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
