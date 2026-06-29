import { useEffect, useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Calendar, X, ChevronDown, ChevronUp } from 'lucide-react';
import { useSeasons } from '../../hooks/useSeasons';
import { useGenerateHalfSchedule } from '../../hooks/admin/useCreateHalf';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { Course } from '../../types/api';

const schema = z.object({
  halfId: z.string().min(1, 'Half is required'),
  startDate: z.string().min(1, 'Start date is required'),
  numberOfRounds: z.coerce.number().min(1).max(52),
  dayOfWeek: z.coerce.number().min(0).max(6).default(1),
  courseId: z.string().min(1, 'Course is required'),
  startingSide: z.enum(['Front', 'Back']).default('Front'),
});

type FormValues = z.infer<typeof schema>;

interface CreateHalfFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

const DAYS_OF_WEEK = [
  { value: 0, label: 'Sunday' },
  { value: 1, label: 'Monday' },
  { value: 2, label: 'Tuesday' },
  { value: 3, label: 'Wednesday' },
  { value: 4, label: 'Thursday' },
  { value: 5, label: 'Friday' },
  { value: 6, label: 'Saturday' },
];

export function CreateHalfForm({ onSuccess, onCancel }: CreateHalfFormProps) {
  const generate = useGenerateHalfSchedule();

  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);
  const halves = activeSeason?.halves ?? [];

  const { data: coursesPage } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  const [skipDates, setSkipDates] = useState<Set<string>>(new Set());
  const [showPreview, setShowPreview] = useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      dayOfWeek: 1,
      numberOfRounds: 8,
      startingSide: 'Front',
    },
  });

  useEffect(() => {
    if (halves.length > 0) {
      setValue('halfId', String(halves[0].id));
      if (halves[0].startDate) setValue('startDate', halves[0].startDate);
    }
  }, [halves, setValue]);

  const watched = useWatch({ control });

  const generatedSchedule = useMemo(() => {
    if (!watched.startDate || !watched.numberOfRounds) return [];
    const dates: Array<{ date: string; nineHoleSide: 'Front' | 'Back' }> = [];
    // Parse as UTC and do all math in UTC so the output matches the yyyy-MM-dd
    // input without shifting by the viewer's local timezone offset.
    const [y, m, d] = watched.startDate.split('-').map(Number);
    const currentDate = new Date(Date.UTC(y, m - 1, d));
    let count = 0;
    let side: 'Front' | 'Back' = (watched.startingSide as 'Front' | 'Back') ?? 'Front';

    if (watched.dayOfWeek !== undefined) {
      const currentDay = currentDate.getUTCDay();
      const target = watched.dayOfWeek;
      const diff = (target - currentDay + 7) % 7;
      currentDate.setUTCDate(currentDate.getUTCDate() + diff);
    }

    while (count < (watched.numberOfRounds || 0)) {
      const dateStr = currentDate.toISOString().split('T')[0];
      if (!skipDates.has(dateStr)) {
        dates.push({ date: dateStr, nineHoleSide: side });
        side = side === 'Front' ? 'Back' : 'Front';
        count++;
      }
      currentDate.setUTCDate(currentDate.getUTCDate() + 7);
    }

    return dates;
  }, [watched, skipDates]);

  function addSkipDate(date: string) {
    setSkipDates((prev) => new Set([...prev, date]));
  }
  function removeSkipDate(date: string) {
    setSkipDates((prev) => {
      const next = new Set(prev);
      next.delete(date);
      return next;
    });
  }

  async function onSubmit(values: FormValues) {
    await generate.mutateAsync({
      halfId: Number(values.halfId),
      courseId: Number(values.courseId),
      weekDates: generatedSchedule.map((s) => s.date),
      startingSide: values.startingSide,
    });
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 max-h-[80vh] overflow-y-auto">
      <FormField label="Half" error={errors.halfId} required>
        <select {...register('halfId')} className={selectClass}>
          {halves.map((h) => (
            <option key={h.id} value={h.id}>
              {h.name} ({h.startDate} → {h.endDate})
            </option>
          ))}
          {halves.length === 0 && <option value="">— No active season —</option>}
        </select>
      </FormField>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Start Date" error={errors.startDate} required>
          <input {...register('startDate')} type="date" className={inputClass} />
        </FormField>

        <FormField label="Number of Rounds" error={errors.numberOfRounds} required>
          <input
            {...register('numberOfRounds')}
            type="number"
            min={1}
            max={52}
            className={inputClass}
          />
        </FormField>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Day of Week" error={errors.dayOfWeek}>
          <select {...register('dayOfWeek')} className={selectClass}>
            {DAYS_OF_WEEK.map((d) => (
              <option key={d.value} value={d.value}>
                {d.label}
              </option>
            ))}
          </select>
        </FormField>

        <FormField label="Starting 9" error={errors.startingSide}>
          <select {...register('startingSide')} className={selectClass}>
            <option value="Front">Front (then alternate)</option>
            <option value="Back">Back (then alternate)</option>
          </select>
        </FormField>
      </div>

      <FormField label="Course" error={errors.courseId} required>
        <select {...register('courseId')} className={selectClass}>
          <option value="">— Select course —</option>
          {courses.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </FormField>

      <div>
        <label className="text-sm font-medium text-gray-700 mb-1.5 block">Skip Dates (optional)</label>
        <input
          type="date"
          className={inputClass}
          onChange={(e) => {
            if (e.target.value) {
              addSkipDate(e.target.value);
              e.target.value = '';
            }
          }}
        />
        {skipDates.size > 0 && (
          <div className="flex flex-wrap gap-2 mt-2">
            {Array.from(skipDates).map((date) => (
              <span
                key={date}
                className="inline-flex items-center gap-1 px-2 py-1 rounded bg-red-50 text-red-700 text-xs"
              >
                <Calendar className="h-3 w-3" />
                {new Date(date).toLocaleDateString(undefined, { timeZone: 'UTC' })}
                <button type="button" onClick={() => removeSkipDate(date)} className="hover:text-red-900">
                  <X className="h-3 w-3" />
                </button>
              </span>
            ))}
          </div>
        )}
      </div>

      <div className="border-t border-gray-200 pt-4">
        <button
          type="button"
          onClick={() => setShowPreview(!showPreview)}
          className="flex items-center gap-2 text-sm font-medium text-gray-700 hover:text-gray-900"
        >
          {showPreview ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
          Schedule Preview ({generatedSchedule.length} rounds)
        </button>

        {showPreview && (
          <div className="mt-2 max-h-48 overflow-y-auto rounded-lg border border-gray-200">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 sticky top-0">
                <tr>
                  <th className="px-3 py-2 text-left font-medium text-gray-600">Week</th>
                  <th className="px-3 py-2 text-left font-medium text-gray-600">Date</th>
                  <th className="px-3 py-2 text-left font-medium text-gray-600">Side</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {generatedSchedule.map((item, idx) => (
                  <tr key={item.date}>
                    <td className="px-3 py-2 text-gray-500">{idx + 1}</td>
                    <td className="px-3 py-2">{new Date(item.date).toLocaleDateString(undefined, { timeZone: 'UTC' })}</td>
                    <td className="px-3 py-2">
                      <span
                        className={`inline-flex px-2 py-0.5 rounded text-xs ${
                          item.nineHoleSide === 'Front'
                            ? 'bg-blue-50 text-blue-700'
                            : 'bg-green-50 text-green-700'
                        }`}
                      >
                        {item.nineHoleSide}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {generate.isError && (
        <p className="text-sm text-red-600">Failed to generate schedule. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2 border-t border-gray-200">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          type="submit"
          variant="primary"
          disabled={isSubmitting || generate.isPending || generatedSchedule.length === 0}
        >
          Generate {generatedSchedule.length} Round{generatedSchedule.length === 1 ? '' : 's'}
        </Button>
      </div>
    </form>
  );
}
