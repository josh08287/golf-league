import { useMemo, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft } from 'lucide-react';
import { usePlayer, useHandicapHistory } from '../../hooks/usePlayers';
import { useSortableTable } from '../../hooks/useSortableTable';
import {
  useUpdatePlayer,
  useDeactivatePlayer,
  useSetHandicap,
} from '../../hooks/admin/usePlayerMutations';
import { useFlights } from '../../hooks/useFlights';
import { useSeasons } from '../../hooks/useSeasons';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { FormField, inputClass, selectClass } from '../../components/admin/FormField';
import type { Flight, HandicapHistoryEntry, SeasonHalf } from '../../types/api';

const editSchema = z.object({
  name: z.string().min(1, 'Required'),
  email: z.string().email('Valid email required'),
});

const handicapSchema = z.object({
  newIndex: z.number({ invalid_type_error: 'Enter a number' }).min(-10).max(54),
  notes: z.string().optional(),
});

type EditValues = z.infer<typeof editSchema>;
type HandicapValues = z.infer<typeof handicapSchema>;

interface EditFormProps {
  playerId: string;
  defaultValues: EditValues;
}

function EditPlayerForm({ playerId, defaultValues }: EditFormProps) {
  const updatePlayer = useUpdatePlayer(playerId);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<EditValues>({ resolver: zodResolver(editSchema), defaultValues });

  async function onSubmit(values: EditValues) {
    await updatePlayer.mutateAsync(values);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Full Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} />
      </FormField>
      <FormField label="Email" error={errors.email} required>
        <input {...register('email')} type="email" className={inputClass} />
      </FormField>

      {updatePlayer.isError && (
        <p className="text-sm text-red-600">Save failed. Please try again.</p>
      )}

      <div className="flex items-center gap-3">
        <Button
          type="submit"
          variant="primary"
          disabled={!isDirty || isSubmitting || updatePlayer.isPending}
        >
          Save Changes
        </Button>
        {saved && <span className="text-sm text-green-700">Saved!</span>}
      </div>
    </form>
  );
}

const flightSchema = z.object({
  flightId: z.string(),
});

type FlightAssignmentValues = z.infer<typeof flightSchema>;

interface FlightAssignmentFormProps {
  playerId: string;
  currentFlightId: number | null;
  flights: Flight[];
  halvesById: Map<number, SeasonHalf>;
}

function FlightAssignmentForm({
  playerId,
  currentFlightId,
  flights,
  halvesById,
}: FlightAssignmentFormProps) {
  const updatePlayer = useUpdatePlayer(playerId);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { isDirty, isSubmitting },
  } = useForm<FlightAssignmentValues>({
    resolver: zodResolver(flightSchema),
    defaultValues: { flightId: currentFlightId != null ? String(currentFlightId) : '' },
  });

  // Group flights by half for the optgroup label.
  const grouped = useMemo(() => {
    const map = new Map<number, Flight[]>();
    for (const f of flights) {
      const list = map.get(f.halfId) ?? [];
      list.push(f);
      map.set(f.halfId, list);
    }
    return [...map.entries()]
      .map(([halfId, list]) => ({
        half: halvesById.get(halfId),
        flights: list.sort((a, b) => a.displayOrder - b.displayOrder),
      }))
      .sort((a, b) => (a.half?.halfNumber ?? 99) - (b.half?.halfNumber ?? 99));
  }, [flights, halvesById]);

  async function onSubmit(values: FlightAssignmentValues) {
    await updatePlayer.mutateAsync({
      flightId: values.flightId === '' ? null : values.flightId,
    });
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Flight">
        <select {...register('flightId')} className={selectClass}>
          <option value="">— Unassigned —</option>
          {grouped.map(({ half, flights: halfFlights }) => (
            <optgroup
              key={half?.id ?? 'orphan'}
              label={half?.name ?? 'Unknown half'}
            >
              {halfFlights.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
      </FormField>

      {updatePlayer.isError && (
        <p className="text-sm text-red-600">Failed to update flight. Try again.</p>
      )}

      <div className="flex items-center gap-3">
        <Button
          type="submit"
          variant="primary"
          disabled={!isDirty || isSubmitting || updatePlayer.isPending}
        >
          Save Flight
        </Button>
        {saved && <span className="text-sm text-green-700">Saved!</span>}
      </div>
    </form>
  );
}

interface HandicapFormProps {
  playerId: string;
  currentIndex: number;
}

function HandicapOverrideForm({ playerId, currentIndex }: HandicapFormProps) {
  const setHandicap = useSetHandicap(playerId);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<HandicapValues>({
    resolver: zodResolver(handicapSchema),
    defaultValues: { newIndex: currentIndex },
  });

  async function onSubmit(values: HandicapValues) {
    await setHandicap.mutateAsync({ newIndex: values.newIndex, notes: values.notes });
    reset({ newIndex: values.newIndex, notes: '' });
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="New Handicap Index" error={errors.newIndex} required>
        <input
          {...register('newIndex', { valueAsNumber: true })}
          type="number"
          step="0.1"
          className={inputClass}
        />
      </FormField>
      <FormField label="Notes" error={errors.notes}>
        <input
          {...register('notes')}
          className={inputClass}
          placeholder="Reason for manual override"
        />
      </FormField>

      {setHandicap.isError && (
        <p className="text-sm text-red-600">Failed to update handicap.</p>
      )}

      <div className="flex items-center gap-3">
        <Button type="submit" variant="primary" disabled={isSubmitting || setHandicap.isPending}>
          Set Handicap
        </Button>
        {saved && <span className="text-sm text-green-700">Updated!</span>}
      </div>
    </form>
  );
}

export function PlayerDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: player, isLoading, error } = usePlayer(id);

  const handicapSort = useSortableTable('adminHandicapHistory');
  const { data: history = [] } = useHandicapHistory(id, handicapSort.sort);

  const { data: flightsPage } = useFlights();
  const allFlights = flightsPage?.data ?? [];

  const { data: seasons } = useSeasons();
  const activeSeason = seasons?.find((s) => s.isActive) ?? null;

  // Only show flights from the active season's halves so the dropdown isn't
  // cluttered with prior years.
  const activeSeasonFlights = useMemo(() => {
    if (!activeSeason) return [];
    const halfIds = new Set(activeSeason.halves.map((h) => h.id));
    return allFlights.filter((f) => halfIds.has(f.halfId));
  }, [allFlights, activeSeason]);

  const halvesById = useMemo(() => {
    const map = new Map<number, SeasonHalf>();
    for (const h of activeSeason?.halves ?? []) map.set(h.id, h);
    return map;
  }, [activeSeason]);

  const deactivate = useDeactivatePlayer(id);
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);

  async function handleDeactivate() {
    await deactivate.mutateAsync();
    setConfirmDeactivate(false);
    navigate('/admin/players');
  }

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error || !player) {
    return <ErrorMessage message="Player not found." />;
  }

  const handicapColumns = [
    {
      key: 'date',
      header: 'Date',
      sortable: true,
      render: (h: HandicapHistoryEntry) => new Date(h.effectiveDate).toLocaleDateString(),
    },
    {
      key: 'index',
      header: '18-Hole',
      sortable: true,
      render: (h: HandicapHistoryEntry) => h.handicapIndex.toFixed(1),
    },
    {
      key: 'nineHole',
      header: '9-Hole',
      sortable: true,
      render: (h: HandicapHistoryEntry) => h.nineHoleHandicapIndex.toFixed(1),
    },
    {
      key: 'source',
      header: 'Source',
      sortable: true,
      render: (h: HandicapHistoryEntry) => (
        <Badge
          variant={
            h.source === 'Manual' ? 'warning' : h.source === 'Initial' ? 'neutral' : 'success'
          }
        >
          {h.source}
        </Badge>
      ),
    },
    {
      key: 'notes',
      header: 'Notes',
      sortable: true,
      render: (h: HandicapHistoryEntry) => h.notes ?? '—',
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <button
          onClick={() => navigate('/admin/players')}
          className="text-gray-400 hover:text-gray-600"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <PageHeader
          title={player.fullName}
          subtitle={
            <Badge variant={player.isActive ? 'success' : 'neutral'}>
              {player.isActive ? 'Active' : 'Inactive'}
            </Badge>
          }
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="p-6">
          <h2 className="mb-4 text-base font-semibold text-gray-900">Player Info</h2>
          <EditPlayerForm
            playerId={id}
            defaultValues={{ name: player.fullName, email: player.email }}
          />
        </Card>

        <Card className="p-6">
          <h2 className="mb-1 text-base font-semibold text-gray-900">Flight Assignment</h2>
          <p className="mb-4 text-sm text-gray-500">
            Currently in: <strong>{player.flightName ?? 'Unassigned'}</strong>
          </p>
          {activeSeason ? (
            <FlightAssignmentForm
              playerId={id}
              currentFlightId={player.flightId}
              flights={activeSeasonFlights}
              halvesById={halvesById}
            />
          ) : (
            <p className="text-sm text-gray-500">No active season.</p>
          )}
        </Card>

        <Card className="p-6 lg:col-span-2">
          <h2 className="mb-1 text-base font-semibold text-gray-900">Manual Handicap Override</h2>
          <p className="mb-4 text-sm text-gray-500">
            Current index: <strong>{player.currentHandicap?.toFixed(1) ?? '—'}</strong>
          </p>
          <HandicapOverrideForm playerId={id} currentIndex={player.currentHandicap ?? 18} />
        </Card>
      </div>

      <Card className="p-6">
        <h2 className="mb-4 text-base font-semibold text-gray-900">Handicap History</h2>
        <DataTable
          columns={handicapColumns}
          data={history}
          rowKey={(h) => `${h.effectiveDate}-${h.handicapIndex}`}
          emptyMessage="No handicap history yet."
          sort={handicapSort.sort}
          onSort={handicapSort.cycle}
        />
      </Card>

      {player.isActive && (
        <Card className="border-red-200 p-6">
          <h2 className="mb-1 text-base font-semibold text-red-700">Danger Zone</h2>
          <p className="mb-4 text-sm text-gray-500">
            Deactivating a player removes them from future rounds and standings calculations.
          </p>
          <Button variant="destructive" onClick={() => setConfirmDeactivate(true)}>
            Deactivate Player
          </Button>
        </Card>
      )}

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate Player"
        description={`Deactivate ${player.fullName}? This action can be reversed by support.`}
        confirmLabel="Deactivate"
        destructive
        onConfirm={handleDeactivate}
        onCancel={() => setConfirmDeactivate(false)}
      />
    </div>
  );
}
