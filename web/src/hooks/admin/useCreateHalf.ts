import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';

export interface GenerateSchedulePayload {
  halfId: number;
  courseId: number;
  weekDates: string[];
  startingSide?: 'Front' | 'Back';
}

export function useGenerateHalfSchedule() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async (payload: GenerateSchedulePayload) => {
      const result = await apiClient.post(`/halves/${payload.halfId}/schedule`, {
        courseId: payload.courseId,
        weekDates: payload.weekDates,
        startingSide: payload.startingSide ?? 'Front',
      });
      return result.data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}
