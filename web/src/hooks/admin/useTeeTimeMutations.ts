import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { teeTimeKeys } from '@/hooks/useTeeTimes';
import type { RoundTeeTimeSchedule } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export interface AdminParticipant {
  id: number;
  playerId: number;
  fullName: string;
  flightId: number | null;
  name: string | null;
  handicapIndex: number;
  courseHandicap: number;
  teeTimeId: number | null;
  teeTimeNumber: number | null;
  isWithdrawn: boolean;
  skippedWeek: boolean;
}

export function useAdminMoveParticipantToTeeTime(roundId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ participantId, teeTimeId }: { participantId: number; teeTimeId: number }) => {
      const res = await apiClient.post(
        `/admin/rounds/${roundId}/tee-times/${teeTimeId}/participants/${participantId}`,
      );
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeKeys.forRound(roundId) });
      qc.invalidateQueries({ queryKey: ['admin', 'rounds', roundId, 'participants'] });
    },
  });
}

export function useAdminRemoveParticipantFromTeeTime(roundId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (participantId: number) => {
      const res = await apiClient.delete(
        `/admin/rounds/${roundId}/tee-times/participants/${participantId}`,
      );
      return unwrap<RoundTeeTimeSchedule>(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeKeys.forRound(roundId) });
      qc.invalidateQueries({ queryKey: ['admin', 'rounds', roundId, 'participants'] });
    },
  });
}

export function useAdminRoundParticipants(roundId: number | null) {
  return useQuery({
    queryKey: ['admin', 'rounds', roundId, 'participants'],
    queryFn: async () => {
      if (roundId == null) throw new Error('roundId required');
      const res = await apiClient.get(`/admin/rounds/${roundId}/participants`);
      return unwrap<AdminParticipant[]>(res.data);
    },
    enabled: roundId != null,
  });
}

export function useAdminSetParticipantSkipped(roundId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ playerId, skipped }: { playerId: number; skipped: boolean }) => {
      const res = await apiClient.post(`/rounds/${roundId}/participants/${playerId}/skip`, { skipped });
      return res.data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeKeys.forRound(roundId) });
      qc.invalidateQueries({ queryKey: ['admin', 'rounds', roundId, 'participants'] });
    },
  });
}

/**
 * Re-send the weekly tee time schedule email for a round. Returns the number
 * of emails actually sent (0 when the league has tee time emails disabled).
 */
export function useResendTeeTimeEmails() {
  return useMutation({
    mutationFn: async (roundId: number) => {
      const res = await apiClient.post(`/rounds/${roundId}/tee-times/send-emails`);
      return unwrap<{ sent: number }>(res.data);
    },
  });
}
