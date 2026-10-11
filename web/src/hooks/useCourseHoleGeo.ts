import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { CourseHoleGeo, CourseHoleGeoHole, OsmHoleGeoPreview } from '@/types/api';

function unwrap<T>(data: unknown): T {
  if (data && typeof data === 'object' && 'data' in (data as object)) {
    return (data as { data: T }).data;
  }
  return data as T;
}

export const courseHoleGeoKeys = {
  course: (courseId: number) => ['courseHoleGeo', courseId] as const,
};

/**
 * A course's hole map (tee/green coordinates). Pass enabled=false while the
 * hole_map_enabled flag is off. Geometry rarely changes, so it never goes
 * stale on its own — saving from the admin editor refreshes it.
 */
export function useCourseHoleGeo(courseId: number | undefined, enabled: boolean = true) {
  return useQuery({
    queryKey: courseHoleGeoKeys.course(courseId ?? 0),
    queryFn: async () => {
      const res = await apiClient.get(`/courses/${courseId}/hole-geo`);
      return unwrap<CourseHoleGeo>(res.data);
    },
    enabled: enabled && !!courseId,
    staleTime: Infinity,
  });
}

/** Admin: proposed hole coordinates from OpenStreetMap around a point. Nothing is saved. */
export function usePreviewOsmHoleGeo(courseId: number) {
  return useMutation({
    mutationFn: async (centre: { lat: number; lng: number }) => {
      const res = await apiClient.post(`/courses/${courseId}/hole-geo/osm-preview`, centre);
      return unwrap<OsmHoleGeoPreview>(res.data);
    },
  });
}

export function useSaveCourseHoleGeo(courseId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (body: { latitude: number | null; longitude: number | null; holes: CourseHoleGeoHole[] }) => {
      const res = await apiClient.put(`/courses/${courseId}/hole-geo`, body);
      return unwrap<CourseHoleGeo>(res.data);
    },
    onSuccess: (data) => {
      qc.setQueryData(courseHoleGeoKeys.course(courseId), data);
    },
  });
}
