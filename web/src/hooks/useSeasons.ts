import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { TableSort } from '@/hooks/useSortableTable';
import type { Season } from '@/types/api';

export const seasonKeys = {
  all: ['seasons'] as const,
  list: (sort?: TableSort) => ['seasons', { sort: sort ?? null }] as const,
};

export function useSeasons(sort?: TableSort) {
  return useQuery<Season[]>({
    queryKey: seasonKeys.list(sort),
    queryFn: () => {
      const params: Record<string, string> = {};
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      return apiClient.get('/seasons', { params }).then((r) => r.data);
    },
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

export function useUpdateSeasonHalf() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      halfId,
      startDate,
      endDate,
      scoringFormat,
      matchPlayCustomFormula,
    }: {
      halfId: number;
      startDate: string;
      endDate: string;
      scoringFormat?: 'stableford' | 'matchPlay';
      matchPlayCustomFormula?: string | null;
    }) =>
      apiClient
        .put(`/seasons/halves/${halfId}`, { startDate, endDate, scoringFormat, matchPlayCustomFormula })
        .then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}

export function useDeleteSeason() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (seasonId: string) =>
      apiClient.delete(`/seasons/${seasonId}`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}
