import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Users } from 'lucide-react';
import { api } from '../../lib/api';
import { useFlights } from '../../hooks/useFlights';
import { usePlayers } from '../../hooks/usePlayers';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { FormField, inputClass } from '../../components/admin/FormField';
import { FlightPlayerAssignment } from '../../components/admin/FlightPlayerAssignment';
import type { Flight } from '../../types/api';

// ── Schema ─────────────────────────────────────────────────────────────────

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  minHandicap: z
    .number({ invalid_type_error: 'Enter a number' })
    .min(-10)
    .max(54)
    .optional(),
  maxHandicap: z
    .number({ invalid_type_error: 'Enter a number' })
    .min(-10)
    .max(54)
    .optional(),
  displayOrder: z.number({ invalid_type_error: 'Enter a number' }).int().min(0).default(0),
});

type FormValues = z.infer<typeof schema>;

// ── Create Flight Form ─────────────────────────────────────────────────────

interface CreateFlightFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

function CreateFlightForm({ onSuccess, onCancel }: CreateFlightFormProps) {
  const qc = useQueryClient();
  const create = useMutation({
    mutationFn: (payload: FormValues) => api.post('/flights', payload).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['flights'] }),
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { displayOrder: 0 },
  });

  async function onSubmit(values: FormValues) {
    await create.mutateAsync(values);
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Flight Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} placeholder="A Flight" />
      </FormField>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Min Handicap" error={errors.minHandicap}>
          <input
            {...register('minHandicap', { valueAsNumber: true })}
            type="number"
            step="0.1"
            className={inputClass}
            placeholder="e.g. 0"
          />
        </FormField>
        <FormField label="Max Handicap" error={errors.maxHandicap}>
          <input
            {...register('maxHandicap', { valueAsNumber: true })}
            type="number"
            step="0.1"
            className={inputClass}
            placeholder="e.g. 18"
          />
        </FormField>
      </div>

      <FormField label="Display Order" error={errors.displayOrder}>
        <input
          {...register('displayOrder', { valueAsNumber: true })}
          type="number"
          className={inputClass}
          placeholder="0"
        />
      </FormField>

      {create.isError && (
        <p className="text-sm text-red-600">Failed to create flight. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || create.isPending}>
          Create Flight
        </Button>
      </div>
    </form>
  );
}

// ── Flight Card ────────────────────────────────────────────────────────────

interface FlightCardProps {
  flight: Flight;
  playerCount: number;
}

function FlightCard({ flight, playerCount }: FlightCardProps) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="font-semibold text-gray-900">{flight.name}</h3>
          {(flight.minHandicap != null || flight.maxHandicap != null) && (
            <p className="mt-0.5 text-xs text-gray-500">
              Handicap:{' '}
              {flight.minHandicap != null ? flight.minHandicap : '—'}
              {' to '}
              {flight.maxHandicap != null ? flight.maxHandicap : '—'}
            </p>
          )}
        </div>
        <div className="flex items-center gap-1 rounded-full bg-green-50 px-2.5 py-1 text-xs font-medium text-[#1B5E20]">
          <Users className="h-3.5 w-3.5" />
          {playerCount}
        </div>
      </div>
    </Card>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────

export function FlightsPage() {
  const { data: flightsPage, isLoading, error } = useFlights();
  const flights = flightsPage?.data ?? [];
  const { data: playersPage } = usePlayers();
  const players = playersPage?.data ?? [];
  const [createOpen, setCreateOpen] = useState(false);

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <ErrorMessage message="Failed to load flights." />;
  }

  function playerCountForFlight(flightId: number) {
    return players.filter((p) => p.flightId === flightId).length;
  }

  return (
    <div className="space-y-8">
      <PageHeader title="Flights">
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Plus className="mr-1 h-4 w-4" />
          Create Flight
        </Button>
      </PageHeader>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {flights.map((f) => (
          <FlightCard key={f.id} flight={f} playerCount={playerCountForFlight(f.id)} />
        ))}
        {flights.length === 0 && (
          <p className="text-sm text-gray-500">No flights created yet.</p>
        )}
      </div>

      {flights.length > 0 && (
        <div>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">
            Player Flight Assignment
          </h2>
          <p className="mb-4 text-sm text-gray-500">
            Drag players between columns to reassign them to a flight.
          </p>
          <FlightPlayerAssignment flights={flights} />
        </div>
      )}

      {/* Create Flight Modal */}
      <Modal open={createOpen} title="Create Flight" onClose={() => setCreateOpen(false)}>
        <CreateFlightForm
          onSuccess={() => setCreateOpen(false)}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>
    </div>
  );
}
