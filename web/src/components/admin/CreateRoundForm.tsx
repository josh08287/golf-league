import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useFlights } from '../../hooks/useFlights';
import { usePlayers } from '../../hooks/usePlayers';
import { useCreateRound } from '../../hooks/admin/useRoundMutations';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { Course } from '../../types/api';

const schema = z.object({
  scheduledDate: z.string().min(1, 'Date is required'),
  courseId: z.string().min(1, 'Course is required'),
  flightId: z.string().min(1, 'Flight is required'),
});

type FormValues = z.infer<typeof schema>;

interface CreateRoundFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

export function CreateRoundForm({ onSuccess, onCancel }: CreateRoundFormProps) {
  const createRound = useCreateRound();
  const { data: flightsPage } = useFlights();
  const flights = flightsPage?.data ?? [];
  const { data: playersPage } = usePlayers();
  const allPlayers = playersPage?.data ?? [];
  const { data: coursesPage } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  const [selectedPlayerIds, setSelectedPlayerIds] = useState<Set<number>>(new Set());
  const [initializedFlightId, setInitializedFlightId] = useState<string>('');

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const selectedFlightId = watch('flightId');
  const flightPlayers = allPlayers.filter(
    (p) => p.isActive && String(p.flightId) === selectedFlightId,
  );

  // Auto-select all players when the flight changes
  if (selectedFlightId && selectedFlightId !== initializedFlightId && flightPlayers.length > 0) {
    setSelectedPlayerIds(new Set(flightPlayers.map((p) => p.id)));
    setInitializedFlightId(selectedFlightId);
  }

  function togglePlayer(id: number) {
    setSelectedPlayerIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function selectAll() {
    setSelectedPlayerIds(new Set(flightPlayers.map((p) => p.id)));
  }

  function selectNone() {
    setSelectedPlayerIds(new Set());
  }

  async function onSubmit(values: FormValues) {
    await createRound.mutateAsync({
      scheduledDate: values.scheduledDate,
      courseId: Number(values.courseId),
      flightId: Number(values.flightId),
      playerIds: Array.from(selectedPlayerIds),
    });
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Date" error={errors.scheduledDate} required>
        <input {...register('scheduledDate')} type="date" className={inputClass} />
      </FormField>

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

      <FormField label="Flight" error={errors.flightId} required>
        <select {...register('flightId')} className={selectClass}>
          <option value="">— Select flight —</option>
          {flights.map((f) => (
            <option key={f.id} value={f.id}>
              {f.name}
            </option>
          ))}
        </select>
      </FormField>

      {selectedFlightId && (
        <div>
          <div className="mb-1.5 flex items-center justify-between">
            <label className="text-sm font-medium text-gray-700">
              Players{' '}
              <span className="font-normal text-gray-400">
                ({selectedPlayerIds.size} selected)
              </span>
            </label>
            <div className="flex gap-2 text-xs">
              <button type="button" onClick={selectAll} className="text-[#1B5E20] hover:underline">
                All
              </button>
              <button type="button" onClick={selectNone} className="text-gray-400 hover:underline">
                None
              </button>
            </div>
          </div>
          {flightPlayers.length === 0 ? (
            <p className="text-sm text-gray-400">No active players in this flight.</p>
          ) : (
            <div className="max-h-48 overflow-y-auto rounded-lg border border-gray-200 divide-y divide-gray-100">
              {flightPlayers.map((p) => (
                <label
                  key={p.id}
                  className="flex cursor-pointer items-center gap-3 px-3 py-2 hover:bg-gray-50"
                >
                  <input
                    type="checkbox"
                    checked={selectedPlayerIds.has(p.id)}
                    onChange={() => togglePlayer(p.id)}
                    className="h-4 w-4 rounded border-gray-300 text-[#1B5E20] focus:ring-[#1B5E20]"
                  />
                  <span className="text-sm text-gray-800">{p.fullName}</span>
                  <span className="ml-auto text-xs text-gray-400">
                    {p.currentHandicap?.toFixed(1) ?? '—'}
                  </span>
                </label>
              ))}
            </div>
          )}
        </div>
      )}

      {createRound.isError && (
        <p className="text-sm text-red-600">Failed to create round. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || createRound.isPending}>
          Create Round
        </Button>
      </div>
    </form>
  );
}
