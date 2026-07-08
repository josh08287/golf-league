import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import type { Course, CourseStatistics, PlayerStatistics, MostImprovedResult, LeagueLeaderboards } from '@/types/api';

export interface StatisticsPeriod {
  seasonId?: number | null;
  halfId?: number | null;
}

function periodParams({ seasonId, halfId }: StatisticsPeriod = {}) {
  const params: Record<string, string> = {};
  if (halfId != null) params.halfId = String(halfId);
  else if (seasonId != null) params.seasonId = String(seasonId);
  else params.all = 'true';
  return params;
}

// ── Query key factory ─────────────────────────────────────────────────────────
export const statisticsKeys = {
  all: ['statistics'] as const,
  courses: () => ['courses'] as const,
  course: (courseId: number | string, period?: StatisticsPeriod) =>
    [...statisticsKeys.all, 'course', String(courseId), period ?? null] as const,
  player: (playerId: number | string) =>
    [...statisticsKeys.all, 'player', String(playerId)] as const,
  mostImproved: (period?: StatisticsPeriod) =>
    [...statisticsKeys.all, 'most-improved', period ?? null] as const,
  leaderboards: (period?: StatisticsPeriod) =>
    [...statisticsKeys.all, 'leaderboards', period ?? null] as const,
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

export function useCourseStatistics(courseId: number | string, period?: StatisticsPeriod) {
  return useQuery({
    queryKey: statisticsKeys.course(courseId, period),
    queryFn: async () => {
      const response = await apiClient.get<CourseStatistics>(
        `/courses/${courseId}/statistics`,
        { params: periodParams(period) },
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

export function useMostImproved(period?: StatisticsPeriod) {
  return useQuery({
    queryKey: statisticsKeys.mostImproved(period),
    queryFn: async () => {
      const response = await apiClient.get<MostImprovedResult>(
        '/statistics/most-improved',
        { params: periodParams(period) },
      );
      return response.data;
    },
  });
}

export function useLeagueLeaderboards(period?: StatisticsPeriod) {
  return useQuery({
    queryKey: statisticsKeys.leaderboards(period),
    queryFn: async () => {
      const response = await apiClient.get<LeagueLeaderboards>(
        '/statistics/leaderboards',
        { params: periodParams(period) },
      );
      return response.data;
    },
  });
}
