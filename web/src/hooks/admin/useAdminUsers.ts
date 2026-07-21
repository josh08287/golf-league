import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { useAuthStore } from '@/store/authStore';

export type AdminUserRole = 'admin' | 'scorer' | 'player';

export interface AdminUser {
  id: string;
  email: string;
  roles: AdminUserRole[];
  createdAt: string;
  lastLoginAt: string | null;
  hasPassword: boolean;
  hasTotp: boolean;
  hasPasskey: boolean;
  isLockedOut: boolean;
}

export const adminUserKeys = {
  all: ['admin', 'users'] as const,
};

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export function useAdminUsers() {
  // Listing /admin/users is admin-only; gate the query the same way useInvites
  // is gated so unauthenticated visitors don't trigger 401s.
  const isAdmin = useAuthStore((s) => s.user?.roles?.includes('admin') ?? false);
  return useQuery({
    queryKey: adminUserKeys.all,
    queryFn: async () => {
      const res = await apiClient.get('/admin/users');
      return unwrap<AdminUser[]>(res.data);
    },
    enabled: isAdmin,
  });
}

export function useUpdateAdminUserRoles() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { id: string; roles: AdminUserRole[] }) => {
      const res = await apiClient.patch(`/admin/users/${payload.id}`, { roles: payload.roles });
      return unwrap<AdminUser>(res.data);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: adminUserKeys.all }),
  });
}

export function useResetAdminUserMfa() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/admin/users/${id}/reset-mfa`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminUserKeys.all }),
  });
}

export function useDeleteAdminUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/admin/users/${id}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminUserKeys.all });
      // The deleted account may have been linked to a Player (SetNull FK
      // clears Player.AppUserId server-side), so refresh player data too.
      qc.invalidateQueries({ queryKey: ['players'] });
    },
  });
}

export interface AttachPlayerPayload {
  firstName: string;
  lastName: string;
  initialHandicap: number;
  flightId?: number | null;
}

/**
 * Attach a Player profile to an existing admin-only AppUser. On success the
 * user moves out of the Administrators table and into the Players table.
 */
export function useAttachPlayerProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { userId: string } & AttachPlayerPayload) => {
      const { userId, ...body } = payload;
      const res = await apiClient.post(`/admin/users/${userId}/attach-player`, body);
      return unwrap(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminUserKeys.all });
      qc.invalidateQueries({ queryKey: ['players'] });
    },
  });
}
