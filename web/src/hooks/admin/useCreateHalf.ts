import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';
import { roundKeys } from '@/hooks/useRounds';
import type { CreateRoundPayload } from '@/hooks/admin/useRoundMutations';

// ── Types ──────────────────────────────────────────────────────────────────

export interface CreateHalfPayload {
  startDate: string; // ISO date string
  numberOfRounds: number;
  frequency: 'weekly' | 'biweekly' | 'daily';
  courseId: number | string;
  flightIds: number[];
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

      // Create rounds sequentially to avoid overwhelming the API
      // Each round includes all selected flights - backend auto-populates participants
      const results = [];
      for (const schedule of schedules) {
        const roundPayload: CreateRoundPayload = {
          scheduledDate: schedule.scheduledDate,
          courseId: payload.courseId,
          flightIds: payload.flightIds, // All flights in one round
          roundType: payload.roundType,
          nineHoleSide: schedule.nineHoleSide,
          // Players will be auto-selected by the backend based on flights
        };

        const result = await apiClient.post('/rounds', roundPayload);
        results.push(result.data);
      }

      return results;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: roundKeys.all });
    },
  });
}
