import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { FeatureFlag } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const featureFlagKeys = {
  all: ['admin', 'featureFlags'] as const,
  states: ['featureFlags', 'states'] as const,
};

/** Super-admin only: full list of flags for the admin toggle UI. */
export function useFeatureFlags(enabled: boolean = true) {
  return useQuery({
    queryKey: featureFlagKeys.all,
    queryFn: async () => {
      const res = await apiClient.get('/admin/feature-flags');
      return unwrap<FeatureFlag[]>(res.data);
    },
    enabled,
  });
}

export function useUpdateFeatureFlag() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ key, enabled }: { key: string; enabled: boolean }) => {
      const res = await apiClient.put(`/admin/feature-flags/${key}`, { enabled });
      return unwrap<FeatureFlag>(res.data);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: featureFlagKeys.all });
      qc.invalidateQueries({ queryKey: featureFlagKeys.states });
    },
  });
}

/**
 * Any authenticated user: key -> enabled map, used to decide whether to
 * render a flagged feature's UI. Never exposes anything beyond booleans.
 */
export function useFeatureFlagStates() {
  return useQuery({
    queryKey: featureFlagKeys.states,
    queryFn: async () => {
      const res = await apiClient.get('/feature-flags');
      return unwrap<Record<string, boolean>>(res.data);
    },
    staleTime: 5 * 60 * 1000,
  });
}
