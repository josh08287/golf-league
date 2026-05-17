import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { CourseDetail } from '@/types/api';

export function useCourseDetail(courseId: number | string | undefined) {
  return useQuery({
    queryKey: courseKeys.detail(String(courseId ?? '')),
    queryFn: async () => {
      const res = await apiClient.get<{ data: CourseDetail }>(`/courses/${courseId}`);
      return res.data.data;
    },
    enabled: Boolean(courseId),
  });
}

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

export interface AddTeeBoxPayload {
  name: string;
  courseRating: number;
  slopeRating: number;
  totalYardage: number;
  par: number;
}

export interface HoleTeeBoxInput {
  courseHoleId: number;
  yardage: number;
  par: number;
}

export interface UpdateHoleTeeBoxesPayload {
  holes: HoleTeeBoxInput[];
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

export function useDeleteCourse() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (courseId: string) =>
      apiClient.delete(`/courses/${courseId}`).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.all });
    },
  });
}

export function useAddTeeBox(courseId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: AddTeeBoxPayload) =>
      apiClient.post(`/courses/${courseId}/teeboxes`, payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.detail(courseId) });
    },
  });
}

export function useUpdateHoleTeeBoxes(courseId: string, teeBoxId: string) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdateHoleTeeBoxesPayload) =>
      apiClient.put(`/courses/${courseId}/teeboxes/${teeBoxId}/holes`, payload).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: courseKeys.detail(courseId) });
    },
  });
}
