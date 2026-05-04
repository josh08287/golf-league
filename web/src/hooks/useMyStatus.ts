import { useQuery } from '@tanstack/react-query';
import { useIsAuthenticated } from '@azure/msal-react';
import { apiClient } from '@/lib/api';
import type { ApiResponse, MyStatusResponse } from '@/types/api';

export const myStatusKeys = {
  all: ['auth', 'me'] as const,
};

export function useMyStatus() {
  const isAuthenticated = useIsAuthenticated();
  return useQuery({
    queryKey: myStatusKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<ApiResponse<MyStatusResponse>>('/auth/me');
      return res.data.data;
    },
    enabled: isAuthenticated,
    staleTime: 30_000,
  });
}
