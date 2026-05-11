import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { useAuthStore } from '@/store/authStore';
import type { TableSort } from '@/hooks/useSortableTable';
import type { CreateInvitesResult, Invite } from '@/types/api';

export const inviteKeys = {
  all: ['admin', 'invites'] as const,
  list: (sort?: TableSort) => ['admin', 'invites', { sort: sort ?? null }] as const,
};

export function useInvites(sort?: TableSort) {
  // Only admins can read /admin/invites. Firing it for everyone (the NavBar
  // calls this on every page) causes a 401 on public pages and triggers
  // the api.ts redirect-to-login interceptor.
  const isAdmin = useAuthStore((s) => s.user?.roles?.includes('admin') ?? false);
  return useQuery({
    queryKey: inviteKeys.list(sort),
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (sort) {
        params.sortBy = sort.sortBy;
        params.sortDir = sort.sortDir;
      }
      const res = await apiClient.get<Invite[]>('/admin/invites', { params });
      return res.data;
    },
    enabled: isAdmin,
  });
}

export function useCreateInvites() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: { emails: string[]; expiryDays?: number; role?: string }) =>
      apiClient
        .post<CreateInvitesResult>('/admin/invites', {
          emails: payload.emails,
          expiryDays: payload.expiryDays ?? 7,
          role: payload.role ?? 'player',
        })
        .then((r) => r.data),
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

export function useDeleteInvite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiClient.delete(`/admin/invites/${id}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: inviteKeys.all });
    },
  });
}
