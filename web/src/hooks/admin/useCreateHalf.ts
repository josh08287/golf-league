import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';

// ── Types ──────────────────────────────────────────────────────────────────

export interface CreateHalfPayload {
  startDate: string; // ISO date string
  numberOfRounds: number;
  frequency: 'weekly' | 'biweekly' | 'daily';
  courseId: number | string;
  roundType: 'NineHole' | 'EighteenHole';
  nineHolePattern: 'Front' | 'Back' | 'Alternate'; // Alternate switches each round
  skipDates?: string[]; // ISO date strings to skip
  dayOfWeek?: number; // 0-6 for weekly frequency (0=Sunday)
}

interface RoundSchedule {
  scheduledDate: string;
  nineHoleSide: 'Front' | 'Back' | undefined;
}

// ── Helpers ───────────────────────────────────────────────────────────────

function generateRoundDates(payload: CreateHalfPayload): RoundSchedule[] {
  const dates: RoundSchedule[] = [];
  const start = new Date(payload.startDate);
  const skipSet = new Set(payload.skipDates ?? []);

  let currentDate = new Date(start);
  let roundCount = 0;
  let alternateSide: 'Front' | 'Back' = 'Front';

  while (roundCount < payload.numberOfRounds) {
    const dateStr = currentDate.toISOString().split('T')[0];

    // Skip if in skip list
    if (!skipSet.has(dateStr)) {
      let nineHoleSide: 'Front' | 'Back' | undefined;

      if (payload.roundType === 'NineHole') {
        if (payload.nineHolePattern === 'Front') {
          nineHoleSide = 'Front';
        } else if (payload.nineHolePattern === 'Back') {
          nineHoleSide = 'Back';
        } else {
          // Alternate
          nineHoleSide = alternateSide;
          alternateSide = alternateSide === 'Front' ? 'Back' : 'Front';
        }
      }

      dates.push({ scheduledDate: dateStr, nineHoleSide });
      roundCount++;
    }

    // Move to next date based on frequency
    if (payload.frequency === 'weekly') {
      currentDate.setDate(currentDate.getDate() + 7);
    } else if (payload.frequency === 'biweekly') {
      currentDate.setDate(currentDate.getDate() + 14);
    } else if (payload.frequency === 'daily') {
      currentDate.setDate(currentDate.getDate() + 1);
    }
  }

  return dates;
}

// ── Hook ───────────────────────────────────────────────────────────────────

export function useCreateHalf() {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async (payload: CreateHalfPayload) => {
      const schedules = generateRoundDates(payload);
      const result = await apiClient.post('/rounds/half', {
        startDate: payload.startDate,
        courseId: payload.courseId,
        roundDates: schedules.map((s) => s.scheduledDate),
        roundType: payload.roundType,
        nineHoleSides: schedules.map((s) => s.nineHoleSide).filter(Boolean),
      });
      return result.data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}
