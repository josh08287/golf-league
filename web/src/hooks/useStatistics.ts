import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { Course, CourseStatistics, PlayerStatistics, MostImprovedResult, LeagueLeaderboards } from '@/types/api';

// ── Query key factory ─────────────────────────────────────────────────────────
export const statisticsKeys = {
  all: ['statistics'] as const,
  courses: () => ['courses'] as const,
  course: (courseId: number | string) =>
    [...statisticsKeys.all, 'course', String(courseId)] as const,
  player: (playerId: number | string) =>
    [...statisticsKeys.all, 'player', String(playerId)] as const,
  mostImproved: () => [...statisticsKeys.all, 'most-improved'] as const,
  leaderboards: () => [...statisticsKeys.all, 'leaderboards'] as const,
};

export function useCourses() {
  return useQuery({
    queryKey: statisticsKeys.courses(),
    queryFn: async () => {
      const response = await apiClient.get<{ data: Course[] }>('/courses');
      return response.data.data;
    },
  });
}

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function useCourseStatistics(courseId: number | string) {
  return useQuery({
    queryKey: statisticsKeys.course(courseId),
    queryFn: async () => {
      const response = await apiClient.get<CourseStatistics>(
        `/courses/${courseId}/statistics`,
      );
      return response.data;
    },
    enabled: Boolean(courseId),
  });
}

export function usePlayerStatistics(playerId: number | string) {
  return useQuery({
    queryKey: statisticsKeys.player(playerId),
    queryFn: async () => {
      const response = await apiClient.get<PlayerStatistics>(
        `/players/${playerId}/statistics`,
      );
      return response.data;
    },
    enabled: Boolean(playerId),
  });
}

export function useMostImproved() {
  return useQuery({
    queryKey: statisticsKeys.mostImproved(),
    queryFn: async () => {
      const response = await apiClient.get<MostImprovedResult>(
        '/statistics/most-improved',
      );
      return response.data;
    },
  });
}

export function useLeagueLeaderboards() {
  return useQuery({
    queryKey: statisticsKeys.leaderboards(),
    queryFn: async () => {
      const response = await apiClient.get<LeagueLeaderboards>(
        '/statistics/leaderboards',
      );
      return response.data;
    },
  });
}
