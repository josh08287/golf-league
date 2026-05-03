import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { Flight, Standing, PagedResponse } from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const flightKeys = {
  all: ['flights'] as const,
  lists: () => [...flightKeys.all, 'list'] as const,
  list: () => [...flightKeys.lists()] as const,
  details: () => [...flightKeys.all, 'detail'] as const,
  detail: (id: string) => [...flightKeys.details(), id] as const,
  standings: (flightId: string, seasonId: string) =>
    [...flightKeys.all, 'standings', flightId, seasonId] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function useFlights() {
  return useQuery({
    queryKey: flightKeys.list(),
    queryFn: async () => {
      const response = await apiClient.get<PagedResponse<Flight>>('/flights');
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

export function useFlightStandings(flightId: string, seasonId: string) {
  return useQuery({
    queryKey: flightKeys.standings(flightId, seasonId),
    queryFn: async () => {
      const response = await apiClient.get<Standing[]>(
        `/flights/${flightId}/standings`,
        { params: { seasonId } },
      );
      return response.data;
    },
    enabled: Boolean(flightId) && Boolean(seasonId),
  });
}
