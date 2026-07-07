import { apiClient } from '@/lib/api';
import { useMutation } from '@tanstack/react-query';

interface RecalculateRoundsResult {
  roundsProcessed: number;
  participantsProcessed: number;
  holeScoresUpdated: number;
  flightAssignmentsUpdated: number;
}

async function recalculateAllRounds(): Promise<RecalculateRoundsResult> {
  const response = await apiClient.post<RecalculateRoundsResult>('/admin/rounds/recalculate');
  return response.data;
}

export function useRecalculateRounds() {
  return useMutation({
    mutationFn: recalculateAllRounds,
  });
}
