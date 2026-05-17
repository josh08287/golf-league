import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { ApiResponse } from '@/types/api';

export interface UserLeagueDto {
  leagueId: number;
  name: string;
  slug: string;
  role: string;
}

export interface UserLeaguesDto {
  leagues: UserLeagueDto[];
  isSuperAdmin: boolean;
}

export const myLeaguesKeys = { all: ['my-leagues'] as const };

export function useMyLeagues(enabled = true) {
  return useQuery({
    queryKey: myLeaguesKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<ApiResponse<UserLeaguesDto>>('/auth/me/leagues');
      return res.data.data;
    },
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}
