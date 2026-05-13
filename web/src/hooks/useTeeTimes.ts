import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { useAuthStore } from '@/store/authStore';
import type { RoundTeeTimeSchedule, TeeTimeSlotName } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const teeTimeKeys = {
  all: ['teeTimes'] as const,
  next: () => [...teeTimeKeys.all, 'next'] as const,
  forRound: (roundId: number) => [...teeTimeKeys.all, 'round', roundId] as const,
};

/**
 * Fetch the tee-time schedule for the next scheduled round. Gated on
 * authentication — anonymous visitors don't trigger the request.
 */
export function useNextRoundTeeTimes() {
  const isAuthed = useAuthStore((s) => !!s.user);
  return useQuery({
    queryKey: teeTimeKeys.next(),
    queryFn: async () => {
      const res = await apiClient.get('/tee-times/next');
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    enabled: isAuthed,
  });
}

export function useRoundTeeTimes(roundId: number | null) {
  const isAuthed = useAuthStore((s) => !!s.user);
  return useQuery({
    queryKey: roundId != null ? teeTimeKeys.forRound(roundId) : teeTimeKeys.all,
    queryFn: async () => {
      if (roundId == null) throw new Error('roundId required');
      const res = await apiClient.get(`/rounds/${roundId}/tee-times`);
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    enabled: isAuthed && roundId != null,
  });
}

export function useJoinTeeTime() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { roundId: number; teeTimeId: number }) => {
      const res = await apiClient.post(
        `/rounds/${payload.roundId}/tee-times/${payload.teeTimeId}/join`,
      );
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: teeTimeKeys.all }),
  });
}

export function useLeaveTeeTime() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (roundId: number) => {
      const res = await apiClient.post(`/rounds/${roundId}/tee-times/leave`);
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: teeTimeKeys.all }),
  });
}

export function useSetTeeTimePreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { playerId: number; preferredSlots: TeeTimeSlotName[] }) => {
      await apiClient.put(`/players/${payload.playerId}/tee-time-preference`, {
        preferredSlots: payload.preferredSlots,
      });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: teeTimeKeys.all }),
  });
}
