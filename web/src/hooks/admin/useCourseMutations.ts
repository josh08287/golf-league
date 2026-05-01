import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';

// ── Query key factory (local — courses not defined by other agent yet) ─────

export const courseKeys = {
  all: ['courses'] as const,
  detail: (id: string) => ['courses', 'detail', id] as const,
};

// ── Types ──────────────────────────────────────────────────────────────────

export interface HoleSpec {
  holeNumber: number;
  par: number;
  strokeIndex: number;
}

export interface CreateCoursePayload {
  name: string;
  rating: number;
  slope: number;
}

export interface UpdateCourseHolesPayload {
  holes: HoleSpec[];
}

// ── Hooks ──────────────────────────────────────────────────────────────────

export function useCreateCourse() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateCoursePayload) =>
      apiClient.post('/courses', payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.all });
    },
  });
}

export function useUpdateCourseHoles(courseId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdateCourseHolesPayload) =>
      apiClient.put(`/courses/${courseId}/holes`, payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.all });
      qc.invalidateQueries({ queryKey: courseKeys.detail(courseId) });
    },
  });
}
