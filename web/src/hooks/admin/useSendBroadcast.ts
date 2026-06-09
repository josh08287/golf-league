import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';

export interface BroadcastPayload {
  subject: string;
  body: string;
  playerIds: number[] | null;
  adHocEmails: string[] | null;
}

export interface BroadcastResult {
  sent: number;
  skipped: number;
  skippedNames: string[];
}

export function useSendBroadcast() {
  return useMutation({
    mutationFn: (payload: BroadcastPayload) =>
      apiClient
        .post<{ data: BroadcastResult }>('/admin/messages/broadcast', payload)
        .then((r) => r.data.data),
  });
}
