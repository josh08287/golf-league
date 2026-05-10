import { useQuery } from '@tanstack/react-query';
import { useIsAuthenticated } from '@azure/msal-react';
import { apiClient } from '@/lib/api';
import type { MyStatusResponse } from '@/types/api';

export const myStatusKeys = {
  all: ['auth', 'me'] as const,
};

export function useMyStatus() {
  const isAuthenticated = useIsAuthenticated();
  return useQuery({
    queryKey: myStatusKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<MyStatusResponse>('/auth/me');
      return res.data;
    },
    enabled: isAuthenticated,
    // No staleTime - uses global default (Infinity) to prevent auto-refetch
  });
}
