import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { ApiResponse, CreateInvitesResult, Invite } from '@/types/api';

export const inviteKeys = {
  all: ['admin', 'invites'] as const,
};

export function useInvites() {
  return useQuery({
    queryKey: inviteKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<ApiResponse<Invite[]>>('/admin/invites');
      return res.data.data;
    },
  });
}

export function useCreateInvites() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: { emails: string[]; expiryDays?: number }) =>
      apiClient
        .post<ApiResponse<CreateInvitesResult>>('/admin/invites', {
          emails: payload.emails,
          expiryDays: payload.expiryDays ?? 7,
        })
        .then((r) => r.data.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inviteKeys.all });
    },
  });
}

export function useRevokeInvite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiClient.post(`/admin/invites/${id}/revoke`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inviteKeys.all });
    },
  });
}
