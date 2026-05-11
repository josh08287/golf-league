import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Users, Trash2 } from 'lucide-react';
import { api } from '../../lib/api';
import { useFlights } from '../../hooks/useFlights';
import { usePlayers } from '../../hooks/usePlayers';
import { useSeasons } from '../../hooks/useSeasons';
import { useDeleteFlight } from '../../hooks/admin/useFlightMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { FormField, inputClass, selectClass } from '../../components/admin/FormField';
import { FlightPlayerAssignment } from '../../components/admin/FlightPlayerAssignment';
import type { Flight, SeasonHalf } from '../../types/api';

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  halfId: z.string().min(1, 'Half is required'),
  displayOrder: z.number({ invalid_type_error: 'Enter a number' }).int().min(0).default(0),
});

type FormValues = z.infer<typeof schema>;

interface CreateFlightFormProps {
  halves: SeasonHalf[];
  onSuccess: () => void;
  onCancel: () => void;
}

function CreateFlightForm({ halves, onSuccess, onCancel }: CreateFlightFormProps) {
  const qc = useQueryClient();
  const create = useMutation({
    mutationFn: (payload: { name: string; halfId: number; displayOrder: number }) =>
      api.post('/flights', payload).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['flights'] }),
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { displayOrder: 0, halfId: halves[0] ? String(halves[0].id) : '' },
  });

  async function onSubmit(values: FormValues) {
    await create.mutateAsync({
      name: values.name,
      halfId: Number(values.halfId),
      displayOrder: values.displayOrder,
    });
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Half" error={errors.halfId} required>
        <select {...register('halfId')} className={selectClass}>
          {halves.map((h) => (
            <option key={h.id} value={h.id}>
              {h.name}
            </option>
          ))}
          {halves.length === 0 && <option value="">— No halves available —</option>}
        </select>
      </FormField>

      <FormField label="Flight Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} placeholder="A Flight" />
      </FormField>

      <FormField label="Display Order" error={errors.displayOrder}>
        <input
          {...register('displayOrder', { valueAsNumber: true })}
          type="number"
          className={inputClass}
          placeholder="0"
        />
      </FormField>

      {create.isError && <p className="text-sm text-red-600">Failed to create flight. Try again.</p>}

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

interface FlightCardProps {
  flight: Flight;
  halfLabel: string | null;
  playerCount: number;
  onDelete: (flight: Flight) => void;
}

function FlightCard({ flight, halfLabel, playerCount, onDelete }: FlightCardProps) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <h3 className="font-semibold text-gray-900">{flight.name}</h3>
          {halfLabel && <p className="mt-0.5 text-xs text-gray-500">{halfLabel}</p>}
        </div>
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 rounded-full bg-green-50 px-2.5 py-1 text-xs font-medium text-[#1B5E20]">
            <Users className="h-3.5 w-3.5" />
            {playerCount}
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="text-red-600 hover:bg-red-50 hover:text-red-700"
            onClick={(e) => {
              e.stopPropagation();
              onDelete(flight);
            }}
            title="Delete flight"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </Card>
  );
}

export function FlightsPage() {
  const { data: flightsPage, isLoading, error } = useFlights();
  const flights = flightsPage?.data ?? [];
  // Need the full roster for flight assignment / unassigned counts; bump
  // the page size so we don't silently cap at 20.
  const { data: playersPage } = usePlayers(1, undefined, 1000);
  const players = playersPage?.data ?? [];
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);
  const halves = activeSeason?.halves ?? [];
  const halfNameById = useMemo(() => {
    const m = new Map<number, string>();
    for (const h of halves) m.set(h.id, h.name);
    return m;
  }, [halves]);

  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Flight | null>(null);

  const deleteFlight = useDeleteFlight();

  async function handleDelete() {
    if (!deleteTarget) return;
    await deleteFlight.mutateAsync(String(deleteTarget.id));
    setDeleteTarget(null);
  }

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

  const flightsByHalf = halves.map((h) => ({
    half: h,
    flights: flights.filter((f) => f.halfId === h.id),
  }));

  return (
    <div className="space-y-8">
      <PageHeader title="Flights">
        <Button variant="primary" onClick={() => setCreateOpen(true)} disabled={halves.length === 0}>
          <Plus className="mr-1 h-4 w-4" />
          Create Flight
        </Button>
      </PageHeader>

      {halves.length === 0 && (
        <p className="text-sm text-gray-500">
          No active season with halves. Create a season first.
        </p>
      )}

      {flightsByHalf.map(({ half, flights: halfFlights }) => (
        <section key={half.id} className="space-y-3">
          <h2 className="text-sm font-semibold uppercase tracking-wider text-gray-500">
            {half.name}
          </h2>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {halfFlights.map((f) => (
              <FlightCard
                key={f.id}
                flight={f}
                halfLabel={halfNameById.get(f.halfId) ?? null}
                playerCount={playerCountForFlight(f.id)}
                onDelete={setDeleteTarget}
              />
            ))}
            {halfFlights.length === 0 && (
              <p className="text-sm text-gray-400">No flights in this half yet.</p>
            )}
          </div>
        </section>
      ))}

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

      <Modal open={createOpen} title="Create Flight" onClose={() => setCreateOpen(false)}>
        <CreateFlightForm
          halves={halves}
          onSuccess={() => setCreateOpen(false)}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Flight"
        description={`Permanently delete ${deleteTarget?.name}? This will unassign all players from this flight and cannot be undone.`}
        confirmLabel="Delete"
        destructive
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
