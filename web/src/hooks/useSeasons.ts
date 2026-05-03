import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { Season } from '@/types/api';

export const seasonKeys = {
  all: ['seasons'] as const,
};

export function useSeasons() {
  return useQuery<Season[]>({
    queryKey: seasonKeys.all,
    queryFn: () => apiClient.get('/seasons').then((r) => r.data),
  });
}

export function useCreateSeason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: {
      name: string;
      year: number;
      startDate: string;
      endDate: string;
      bestNRounds?: number;
    }) => apiClient.post('/seasons', payload).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}

export function useSetActiveSeason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (seasonId: number) =>
      apiClient.post(`/seasons/${seasonId}/activate`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}
