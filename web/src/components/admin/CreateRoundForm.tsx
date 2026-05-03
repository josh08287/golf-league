import { useForm } from 'react-hook-form';
import { useEffect } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useFlights } from '../../hooks/useFlights';
import { usePlayers } from '../../hooks/usePlayers';
import { useCreateRound } from '../../hooks/admin/useRoundMutations';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass, checkboxClass } from './FormField';
import type { Course } from '../../types/api';

const schema = z.object({
  scheduledDate: z.string().min(1, 'Date is required'),
  courseId: z.string().min(1, 'Course is required'),
  flightIds: z.array(z.string()).min(1, 'At least one flight is required'),
  roundType: z.enum(['NineHole', 'EighteenHole']).default('NineHole'),
  nineHoleSide: z.enum(['Front', 'Back']).default('Front'),
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

  const [selectedFlightIds, setSelectedFlightIds] = useState<Set<number>>(new Set());
  const [selectedPlayerIds, setSelectedPlayerIds] = useState<Set<number>>(new Set());

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const selectedFlightIdsArray = watch('flightIds') || [];
  
  // Get players from all selected flights
  const flightPlayers = allPlayers.filter(
    (p) => p.isActive && selectedFlightIdsArray.includes(String(p.flightId)),
  );

  // Auto-select all players when flights change
  useEffect(() => {
    if (flightPlayers.length > 0) {
      setSelectedPlayerIds(new Set(flightPlayers.map((p) => p.id)));
    }
  }, [selectedFlightIdsArray.join(',')]);

  function toggleFlight(id: number) {
    setSelectedFlightIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      // Update form value
      setValue('flightIds', Array.from(next).map(String), { shouldValidate: true });
      return next;
    });
  }

  function selectAllFlights() {
    const allIds = new Set(flights.map((f) => f.id));
    setSelectedFlightIds(allIds);
    setValue('flightIds', Array.from(allIds).map(String), { shouldValidate: true });
  }

  function selectNoFlights() {
    setSelectedFlightIds(new Set());
    setValue('flightIds', [], { shouldValidate: true });
  }

  function togglePlayer(id: number) {
    setSelectedPlayerIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function selectAllPlayers() {
    setSelectedPlayerIds(new Set(flightPlayers.map((p) => p.id)));
  }

  function selectNoPlayers() {
    setSelectedPlayerIds(new Set());
  }

  async function onSubmit(values: FormValues) {
    await createRound.mutateAsync({
      scheduledDate: values.scheduledDate,
      courseId: Number(values.courseId),
      flightIds: values.flightIds.map(Number),
      playerIds: Array.from(selectedPlayerIds),
      roundType: values.roundType,
      nineHoleSide: values.roundType === 'NineHole' ? values.nineHoleSide : undefined,
    });
    onSuccess();
  }

  // Initialize with all flights selected
  useState(() => {
    if (flights.length > 0 && selectedFlightIds.size === 0) {
      selectAllFlights();
    }
  });

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
        {errors.flightIds && <p className="text-xs text-red-600 mt-1">{errors.flightIds.message}</p>}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Round Type" error={errors.roundType}>
          <select {...register('roundType')} className={selectClass}>
            <option value="NineHole">9 Holes</option>
            <option value="EighteenHole">18 Holes</option>
          </select>
        </FormField>

        {watch('roundType') === 'NineHole' && (
          <FormField label="Nine" error={errors.nineHoleSide}>
            <select {...register('nineHoleSide')} className={selectClass}>
              <option value="Front">Front 9</option>
              <option value="Back">Back 9</option>
            </select>
          </FormField>
        )}
      </div>

      {selectedFlightIdsArray.length > 0 && (
        <div>
          <div className="mb-1.5 flex items-center justify-between">
            <label className="text-sm font-medium text-gray-700">
              Players{' '}
              <span className="font-normal text-gray-400">
                ({selectedPlayerIds.size} selected from {flightPlayers.length} available)
              </span>
            </label>
            <div className="flex gap-2 text-xs">
              <button type="button" onClick={selectAllPlayers} className="text-[#1B5E20] hover:underline">
                All
              </button>
              <button type="button" onClick={selectNoPlayers} className="text-gray-400 hover:underline">
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
