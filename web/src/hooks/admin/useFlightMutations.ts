import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';

export const flightKeys = {
  all: ['flights'] as const,
};

export function useDeleteFlight() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (flightId: string) =>
      apiClient.delete(`/flights/${flightId}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: flightKeys.all });
    },
  });
}

export function useInitializeHalfFlights() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: { halfId: number; maxPlayersPerFlight?: number }) =>
      apiClient.post('/flights/initialize-half', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: flightKeys.all });
      qc.invalidateQueries({ queryKey: ['players'] });
    },
  });
}
