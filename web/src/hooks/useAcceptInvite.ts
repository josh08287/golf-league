import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { ApiResponse, Invite } from '@/types/api';
import { myStatusKeys } from './useMyStatus';

export function useInviteByToken(token: string | null) {
  return useQuery({
    queryKey: ['invite', token],
    queryFn: async () => {
      const res = await apiClient.get<ApiResponse<Invite>>(`/invites/${token}`);
      return res.data.data;
    },
    enabled: !!token,
    retry: false,
  });
}

export function useAcceptInvite(token: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: { firstName: string; lastName: string; email: string; phone?: string }) =>
      apiClient
        .post(`/invites/${token}/accept`, payload)
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: myStatusKeys.all });
    },
  });
}
