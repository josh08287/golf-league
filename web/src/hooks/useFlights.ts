import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { Flight, Standing, PagedResponse } from '@/types/api';

export const flightKeys = {
  all: ['flights'] as const,
  lists: () => [...flightKeys.all, 'list'] as const,
  list: (halfId?: number | string) => [...flightKeys.lists(), { halfId: halfId ?? null }] as const,
  details: () => [...flightKeys.all, 'detail'] as const,
  detail: (id: string) => [...flightKeys.details(), id] as const,
  standings: (flightId: string, halfId: string) =>
    [...flightKeys.all, 'standings', flightId, halfId] as const,
};

export function useFlights(halfId?: number | string) {
  return useQuery({
    queryKey: flightKeys.list(halfId),
    queryFn: async () => {
      const params: Record<string, string | number> = {};
      if (halfId) params.halfId = String(halfId);
      const response = await apiClient.get<PagedResponse<Flight>>('/flights', { params });
      return response.data;
    },
  });
}

export function useFlight(id: string) {
  return useQuery({
    queryKey: flightKeys.detail(id),
    queryFn: async () => {
      const response = await apiClient.get<Flight>(`/flights/${id}`);
      return response.data;
    },
    enabled: Boolean(id),
  });
}

export function useFlightStandings(flightId: string, halfId: string, useGrossPoints = false) {
  return useQuery({
    queryKey: [...flightKeys.standings(flightId, halfId), { useGrossPoints }],
    queryFn: async () => {
      const response = await apiClient.get<Standing[]>(
        `/flights/${flightId}/standings`,
        { params: { halfId, useGrossPoints: String(useGrossPoints) } },
      );
      return response.data;
    },
    enabled: Boolean(flightId) && Boolean(halfId),
  });
}
