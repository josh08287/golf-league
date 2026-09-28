import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { BbbHonor, NassauFormat, ScoringBasis, SideGameType, TeeTimeSideGames } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const teeTimeSideGameKeys = {
  teeTime: (teeTimeId: number) => ['teeTimeSideGames', teeTimeId] as const,
};

/**
 * Games this tee-time group is eligible for and has opted into, with
 * live-computed status. Pass enabled=false while the feature flag is off (or
 * the tee time id is unknown) to avoid a pointless request.
 */
export function useTeeTimeSideGames(teeTimeId: number, enabled: boolean = true) {
  return useQuery({
    queryKey: teeTimeSideGameKeys.teeTime(teeTimeId),
    queryFn: async () => {
      const res = await apiClient.get(`/tee-times/${teeTimeId}/side-games`);
      return unwrap<TeeTimeSideGames>(res.data);
    },
    enabled: enabled && teeTimeId > 0,
  });
}

export interface OptInSideGameInput {
  gameType: SideGameType;
  scoringBasis?: ScoringBasis;
  nassauFormat?: NassauFormat;
  teams?: { participantId: number; teamNumber: number }[];
  wolfRotationOrder?: number[];
}

export function useOptInSideGame(teeTimeId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: OptInSideGameInput) => {
      const res = await apiClient.post(`/tee-times/${teeTimeId}/side-games`, input);
      return unwrap(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeSideGameKeys.teeTime(teeTimeId) });
    },
  });
}

export function useOptOutSideGame(teeTimeId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (sideGameId: number) => {
      const res = await apiClient.delete(`/tee-times/${teeTimeId}/side-games/${sideGameId}`);
      return unwrap(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeSideGameKeys.teeTime(teeTimeId) });
    },
  });
}

/** Records (or clears) one Bingo Bango Bongo honor's winner for one hole. */
export function useSetSideGameHolePick(teeTimeId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { sideGameId: number; holeNumber: number; honor: BbbHonor; winnerParticipantId: number | null }) => {
      const res = await apiClient.put(
        `/tee-times/${teeTimeId}/side-games/${input.sideGameId}/bbb-picks/${input.holeNumber}/${input.honor}`,
        { winnerParticipantId: input.winnerParticipantId },
      );
      return unwrap(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeSideGameKeys.teeTime(teeTimeId) });
    },
  });
}

/** Records one hole's Wolf call (partner picked, or lone wolf). */
export function useSetWolfHolePick(teeTimeId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: {
      sideGameId: number;
      holeNumber: number;
      wolfParticipantId: number;
      isLoneWolf: boolean;
      partnerParticipantId: number | null;
    }) => {
      const res = await apiClient.put(
        `/tee-times/${teeTimeId}/side-games/${input.sideGameId}/wolf-picks/${input.holeNumber}`,
        {
          wolfParticipantId: input.wolfParticipantId,
          isLoneWolf: input.isLoneWolf,
          partnerParticipantId: input.partnerParticipantId,
        },
      );
      return unwrap(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: teeTimeSideGameKeys.teeTime(teeTimeId) });
    },
  });
}
