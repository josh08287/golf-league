import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { LeagueSetting } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const settingsKeys = {
  all: ['admin', 'settings'] as const,
};

export function useLeagueSettings() {
  return useQuery({
    queryKey: settingsKeys.all,
    queryFn: async () => {
      const res = await apiClient.get('/admin/settings');
      return unwrap<LeagueSetting[]>(res.data);
    },
  });
}

export function useUpdateLeagueSetting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ key, value }: { key: string; value: string }) => {
      const res = await apiClient.put(`/admin/settings/${key}`, { value });
      return unwrap<LeagueSetting>(res.data);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: settingsKeys.all }),
  });
}

/** Extracts the backend's `{ error: string }` message from a failed settings mutation, if present. */
export function settingErrorMessage(error: unknown): string | undefined {
  if (error && typeof error === 'object' && 'response' in error) {
    const response = (error as { response?: { data?: { error?: string } } }).response;
    return response?.data?.error;
  }
  return undefined;
}
