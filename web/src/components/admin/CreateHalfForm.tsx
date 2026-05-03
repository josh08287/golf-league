import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useFlights } from '../../hooks/useFlights';
import { useCreateHalf } from '../../hooks/admin/useCreateHalf';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass, checkboxClass } from './FormField';
import type { Course } from '../../types/api';
import { Calendar, X, ChevronDown, ChevronUp } from 'lucide-react';

const schema = z.object({
  startDate: z.string().min(1, 'Start date is required'),
  numberOfRounds: z.coerce.number().min(1, 'Must create at least 1 round').max(52, 'Maximum 52 rounds'),
  frequency: z.enum(['weekly', 'biweekly', 'daily']).default('weekly'),
  dayOfWeek: z.coerce.number().min(0).max(6).default(1), // 1 = Monday
  courseId: z.string().min(1, 'Course is required'),
  roundType: z.enum(['NineHole', 'EighteenHole']).default('NineHole'),
  nineHolePattern: z.enum(['Front', 'Back', 'Alternate']).default('Alternate'),
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

const NINE_HOLE_PATTERNS = [
  { value: 'Front', label: 'Always Front 9' },
  { value: 'Back', label: 'Always Back 9' },
  { value: 'Alternate', label: 'Alternate Front/Back' },
];

export function CreateHalfForm({ onSuccess, onCancel }: CreateHalfFormProps) {
  const createHalf = useCreateHalf();
  const { data: flightsPage } = useFlights();
  const flights = flightsPage?.data ?? [];

  const { data: coursesPage } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  const [selectedFlightIds, setSelectedFlightIds] = useState<Set<number>>(new Set());
  const [skipDates, setSkipDates] = useState<Set<string>>(new Set());
  const [showPreview, setShowPreview] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      frequency: 'weekly',
      dayOfWeek: 1, // Monday
      roundType: 'NineHole',
      nineHolePattern: 'Alternate',
      numberOfRounds: 8,
    },
  });

  const watchedValues = useWatch({ control });

  // Select all flights by default on first load
  if (selectedFlightIds.size === 0 && flights.length > 0) {
    setSelectedFlightIds(new Set(flights.map((f) => f.id)));
  }

  const generatedSchedule = useMemo(() => {
    if (!watchedValues.startDate || !watchedValues.numberOfRounds) return [];

    const dates: Array<{ date: string; nineHoleSide: string | null }> = [];
    const start = new Date(watchedValues.startDate);
    const skipSet = skipDates;

    let currentDate = new Date(start);
    let roundCount = 0;
    let alternateSide: 'Front' | 'Back' = 'Front';

    // Adjust start date to match selected day of week if frequency is weekly/biweekly
    if (watchedValues.frequency !== 'daily' && watchedValues.dayOfWeek !== undefined) {
      const currentDay = currentDate.getDay();
      const targetDay = watchedValues.dayOfWeek;
      const diff = (targetDay - currentDay + 7) % 7;
      currentDate.setDate(currentDate.getDate() + diff);
    }

    while (roundCount < (watchedValues.numberOfRounds || 0)) {
      const dateStr = currentDate.toISOString().split('T')[0];

      if (!skipSet.has(dateStr)) {
        let nineHoleSide: string | null = null;

        if (watchedValues.roundType === 'NineHole') {
          if (watchedValues.nineHolePattern === 'Front') {
            nineHoleSide = 'Front';
          } else if (watchedValues.nineHolePattern === 'Back') {
            nineHoleSide = 'Back';
          } else {
            nineHoleSide = alternateSide;
            alternateSide = alternateSide === 'Front' ? 'Back' : 'Front';
          }
        }

        dates.push({ date: dateStr, nineHoleSide });
        roundCount++;
      }

      if (watchedValues.frequency === 'weekly') {
        currentDate.setDate(currentDate.getDate() + 7);
      } else if (watchedValues.frequency === 'biweekly') {
        currentDate.setDate(currentDate.getDate() + 14);
      } else {
        currentDate.setDate(currentDate.getDate() + 1);
      }
    }

    return dates;
  }, [watchedValues, skipDates]);

  function toggleFlight(id: number) {
    setSelectedFlightIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function selectAllFlights() {
    setSelectedFlightIds(new Set(flights.map((f) => f.id)));
  }

  function selectNoFlights() {
    setSelectedFlightIds(new Set());
  }

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
    await createHalf.mutateAsync({
      startDate: values.startDate,
      numberOfRounds: values.numberOfRounds,
      frequency: values.frequency,
      courseId: Number(values.courseId),
      flightIds: Array.from(selectedFlightIds),
      roundType: values.roundType,
      nineHolePattern: values.nineHolePattern,
      skipDates: Array.from(skipDates),
      dayOfWeek: values.dayOfWeek,
    });
    onSuccess();
  }

  const roundType = watch('roundType');
  const frequency = watch('frequency');

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 max-h-[80vh] overflow-y-auto">
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
        <FormField label="Frequency" error={errors.frequency}>
          <select {...register('frequency')} className={selectClass}>
            <option value="weekly">Weekly</option>
            <option value="biweekly">Bi-weekly</option>
            <option value="daily">Daily</option>
          </select>
        </FormField>

        {frequency !== 'daily' && (
          <FormField label="Day of Week" error={errors.dayOfWeek}>
            <select {...register('dayOfWeek')} className={selectClass}>
              {DAYS_OF_WEEK.map((d) => (
                <option key={d.value} value={d.value}>
                  {d.label}
                </option>
              ))}
            </select>
          </FormField>
        )}
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

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Round Type" error={errors.roundType}>
          <select {...register('roundType')} className={selectClass}>
            <option value="NineHole">9 Holes</option>
            <option value="EighteenHole">18 Holes</option>
          </select>
        </FormField>

        {roundType === 'NineHole' && (
          <FormField label="9-Hole Pattern" error={errors.nineHolePattern}>
            <select {...register('nineHolePattern')} className={selectClass}>
              {NINE_HOLE_PATTERNS.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
          </FormField>
        )}
      </div>

      {/* Flights Selection */}
      <div>
        <div className="mb-1.5 flex items-center justify-between">
          <label className="text-sm font-medium text-gray-700">
            Flights{' '}
            <span className="font-normal text-gray-400">({selectedFlightIds.size} selected)</span>
          </label>
          <div className="flex gap-2 text-xs">
            <button type="button" onClick={selectAllFlights} className="text-[#1B5E20] hover:underline">
              All
            </button>
            <button type="button" onClick={selectNoFlights} className="text-gray-400 hover:underline">
              None
            </button>
          </div>
        </div>
        {flights.length === 0 ? (
          <p className="text-sm text-gray-400">No flights available.</p>
        ) : (
          <div className="max-h-32 overflow-y-auto rounded-lg border border-gray-200 divide-y divide-gray-100">
            {flights.map((f) => (
              <label
                key={f.id}
                className="flex cursor-pointer items-center gap-3 px-3 py-2 hover:bg-gray-50"
              >
                <input
                  type="checkbox"
                  checked={selectedFlightIds.has(f.id)}
                  onChange={() => toggleFlight(f.id)}
                  className={checkboxClass}
                />
                <span className="text-sm text-gray-800">{f.name}</span>
              </label>
            ))}
          </div>
        )}
      </div>

      {/* Skip Dates */}
      <div>
        <label className="text-sm font-medium text-gray-700 mb-1.5 block">Skip Dates (optional)</label>
        <div className="flex gap-2 mb-2">
          <input
            type="date"
            id="skipDatePicker"
            className={inputClass}
            onChange={(e) => {
              if (e.target.value) {
                addSkipDate(e.target.value);
                e.target.value = '';
              }
            }}
          />
        </div>
        {skipDates.size > 0 && (
          <div className="flex flex-wrap gap-2">
            {Array.from(skipDates).map((date) => (
              <span
                key={date}
                className="inline-flex items-center gap-1 px-2 py-1 rounded bg-red-50 text-red-700 text-xs"
              >
                <Calendar className="h-3 w-3" />
                {new Date(date).toLocaleDateString()}
                <button
                  type="button"
                  onClick={() => removeSkipDate(date)}
                  className="hover:text-red-900"
                >
                  <X className="h-3 w-3" />
                </button>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Schedule Preview */}
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
                  <th className="px-3 py-2 text-left font-medium text-gray-600">#</th>
                  <th className="px-3 py-2 text-left font-medium text-gray-600">Date</th>
                  {roundType === 'NineHole' && (
                    <th className="px-3 py-2 text-left font-medium text-gray-600">Side</th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {generatedSchedule.map((item, idx) => (
                  <tr key={item.date} className="hover:bg-gray-50">
                    <td className="px-3 py-2 text-gray-500">{idx + 1}</td>
                    <td className="px-3 py-2">{new Date(item.date).toLocaleDateString()}</td>
                    {roundType === 'NineHole' && (
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
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {createHalf.isError && (
        <p className="text-sm text-red-600">
          Failed to create rounds. Try again.
        </p>
      )}

      <div className="flex justify-end gap-3 pt-2 border-t border-gray-200">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          type="submit"
          variant="primary"
          disabled={isSubmitting || createHalf.isPending || selectedFlightIds.size === 0}
        >
          Create {generatedSchedule.length * selectedFlightIds.size} Rounds
        </Button>
      </div>
    </form>
  );
}
