import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { PublicLeagueSettings } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

/** Public, anonymous-safe league settings (e.g. footer WhatsApp link). */
export function usePublicLeagueSettings() {
  return useQuery({
    queryKey: ['settings', 'public'],
    queryFn: async () => {
      const res = await apiClient.get('/settings/public');
      return unwrap<PublicLeagueSettings>(res.data);
    },
  });
}
