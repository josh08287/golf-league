import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { RoundClosestToPin } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const closestToPinKeys = {
  round: (roundId: number) => ['closestToPin', roundId] as const,
};

/**
 * The round's par-3 holes with recorded closest-to-pin winners plus the
 * round's active participants. Pass enabled=false while the feature flag is
 * off (or the round id is unknown) to avoid a pointless request.
 */
export function useRoundClosestToPin(roundId: number, enabled: boolean = true) {
  return useQuery({
    queryKey: closestToPinKeys.round(roundId),
    queryFn: async () => {
      const res = await apiClient.get(`/rounds/${roundId}/closest-to-pin`);
      return unwrap<RoundClosestToPin>(res.data);
    },
    enabled: enabled && roundId > 0,
  });
}

export function useSetRoundClosestToPin(roundId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (selections: { holeNumber: number; playerId: number | null }[]) => {
      const res = await apiClient.put(`/rounds/${roundId}/closest-to-pin`, { selections });
      return unwrap<RoundClosestToPin>(res.data);
    },
    onSuccess: (data) => {
      qc.setQueryData(closestToPinKeys.round(roundId), data);
    },
  });
}
