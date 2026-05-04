import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { ApiResponse, Registration } from '@/types/api';
import { myStatusKeys } from '@/hooks/useMyStatus';

export const registrationKeys = {
  pending: ['admin', 'registrations', 'pending'] as const,
};

export function usePendingRegistrations() {
  return useQuery({
    queryKey: registrationKeys.pending,
    queryFn: async () => {
      const res = await apiClient.get<ApiResponse<Registration[]>>('/admin/registrations');
      return res.data.data;
    },
  });
}

export function useApproveRegistration() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiClient.post(`/admin/registrations/${id}/approve`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: registrationKeys.pending });
      qc.invalidateQueries({ queryKey: myStatusKeys.all });
    },
  });
}

export function useRejectRegistration() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: number; reason?: string }) =>
      apiClient
        .post(`/admin/registrations/${id}/reject`, { reason })
        .then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: registrationKeys.pending });
    },
  });
}

export function useSubmitRegistration() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: {
      firstName: string;
      lastName: string;
      email: string;
      phone?: string;
    }) => apiClient.post('/auth/register', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: myStatusKeys.all });
    },
  });
}
