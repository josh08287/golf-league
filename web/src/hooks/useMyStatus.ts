import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { isAuthenticated } from '@/lib/auth';
import type { MyStatusResponse } from '@/types/api';

export const myStatusKeys = {
  all: ['auth', 'me'] as const,
};

export function useMyStatus() {
  return useQuery({
    queryKey: myStatusKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<MyStatusResponse>('/auth/me');
      return res.data;
    },
    enabled: isAuthenticated(),
    // No staleTime - uses global default (Infinity) to prevent auto-refetch
  });
}
