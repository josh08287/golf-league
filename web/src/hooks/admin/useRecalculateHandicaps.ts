import { apiClient } from '@/lib/api';
import { useMutation } from '@tanstack/react-query';

interface RecalculateHandicapsResult {
  playersProcessed: number;
  handicapsCreated: number;
}

async function recalculateAllHandicaps(): Promise<RecalculateHandicapsResult> {
  const response = await apiClient.post<RecalculateHandicapsResult>('/admin/handicaps/recalculate');
  return response.data;
}

export function useRecalculateHandicaps() {
  return useMutation({
    mutationFn: recalculateAllHandicaps,
  });
}
