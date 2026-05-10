import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { TableSort } from '@/hooks/useSortableTable';
import type { Flight, Standing, PagedResponse } from '@/types/api';

export const flightKeys = {
  all: ['flights'] as const,
  lists: () => [...flightKeys.all, 'list'] as const,
  list: (halfId?: number | string, sort?: TableSort) =>
    [...flightKeys.lists(), { halfId: halfId ?? null, sort: sort ?? null }] as const,
  details: () => [...flightKeys.all, 'detail'] as const,
  detail: (id: string) => [...flightKeys.details(), id] as const,
  standings: (flightId: string, halfId: string) =>
    [...flightKeys.all, 'standings', flightId, halfId] as const,
};

export function useFlights(halfId?: number | string, sort?: TableSort) {
  return useQuery({
    queryKey: flightKeys.list(halfId, sort),
    queryFn: async () => {
      const params: Record<string, string | number> = {};
      if (halfId) params.halfId = String(halfId);
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
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

export function useFlightStandings(
  flightId: string,
  halfId: string,
  useGrossPoints = false,
  sort?: TableSort,
) {
  return useQuery({
    queryKey: [...flightKeys.standings(flightId, halfId), { useGrossPoints, sort: sort ?? null }],
    queryFn: async () => {
      const params: Record<string, string> = {
        halfId,
        useGrossPoints: String(useGrossPoints),
      };
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const response = await apiClient.get<Standing[]>(
        `/flights/${flightId}/standings`,
        { params },
      );
      return response.data;
    },
    enabled: Boolean(flightId) && Boolean(halfId),
  });
}
