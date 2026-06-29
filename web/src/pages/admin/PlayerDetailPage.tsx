import { useMemo, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft } from 'lucide-react';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '../../lib/utils';
import { usePlayer, useHandicapHistory } from '../../hooks/usePlayers';
import { useSortableTable } from '../../hooks/useSortableTable';
import {
  useUpdatePlayer,
  useDeactivatePlayer,
  useSetHandicap,
  useSetHalfMembership,
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
import { LinkUserForm } from '../../components/admin/LinkUserForm';
import { useSetTeeTimePreference } from '../../hooks/useTeeTimes';
import type { Flight, HandicapHistoryEntry, SeasonHalf, TeeTimeSlotName } from '../../types/api';
import { TEE_TIME_SLOTS, TEE_TIME_SLOT_FLAG } from '../../types/api';

function TeeTimePreferenceCard({ playerId, currentMask }: { playerId: string; currentMask: number }) {
  const setPreference = useSetTeeTimePreference();
  const [selected, setSelected] = useState<Set<TeeTimeSlotName>>(
    () => new Set(TEE_TIME_SLOTS.filter((s) => (currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0)),
  );

  function toggle(slot: TeeTimeSlotName) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(slot)) next.delete(slot); else next.add(slot);
      return next;
    });
  }

  function save() {
    setPreference.mutate({ playerId: parseInt(playerId, 10), preferredSlots: [...selected] });
  }

  const isDirty = TEE_TIME_SLOTS.some(
    (s) => selected.has(s) !== ((currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0),
  );

  return (
    <Card className="p-6">
      <h2 className="mb-1 text-base font-semibold text-gray-900">Tee-Time Preference</h2>
      <p className="mb-3 text-sm text-gray-500">
        Slot preference used by auto-fill when assigning this player to tee times.
      </p>
      <div className="flex flex-wrap gap-2 mb-3">
        {TEE_TIME_SLOTS.map((slot) => {
          const active = selected.has(slot);
          return (
            <button
              key={slot}
              type="button"
              onClick={() => toggle(slot)}
              className={[
                'px-3 py-1 rounded-full text-sm font-medium border transition-colors',
                active
                  ? 'bg-[#1B5E20] text-white border-[#1B5E20]'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-[#1B5E20]',
              ].join(' ')}
            >
              {slot}
            </button>
          );
        })}
      </div>
      {isDirty && (
        <div className="flex items-center gap-2">
          <Button size="sm" variant="primary" onClick={save} disabled={setPreference.isPending}>
            Save Preference
          </Button>
          {setPreference.isError && (
            <span className="text-xs text-red-600">Failed to save.</span>
          )}
          {setPreference.isSuccess && !setPreference.isPending && (
            <span className="text-xs text-green-700">Saved!</span>
          )}
        </div>
      )}
      {!isDirty && selected.size === 0 && (
        <p className="text-sm text-gray-400 italic">No preference set — player will be placed anywhere.</p>
      )}
    </Card>
  );
}

const editSchema = z.object({
  name: z.string().min(1, 'Required'),
  email: z
    .string()
    .optional()
    .refine(
      (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
      { message: 'Valid email required' },
    ),
  isAdmin: z.boolean(),
  isScorer: z.boolean(),
  isPlayer: z.boolean(),
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
    const roles: string[] = [];
    if (values.isAdmin) roles.push('admin');
    if (values.isScorer) roles.push('scorer');
    if (values.isPlayer) roles.push('player');
    await updatePlayer.mutateAsync({
      name: values.name,
      // Send null when the field is empty so the API clears the column,
      // rather than passing an empty string.
      email: values.email?.trim() ? values.email.trim() : null,
      roles,
    });
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Full Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} />
      </FormField>
      <FormField label="Email" error={errors.email}>
        <input {...register('email')} type="email" className={inputClass} />
      </FormField>
      <FormField label="Roles">
        <div className="flex flex-col gap-2 mt-1">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" {...register('isPlayer')} /> Player
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" {...register('isScorer')} /> Scorer
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" {...register('isAdmin')} /> Admin
          </label>
        </div>
        <p className="mt-1 text-xs text-gray-400">
          Admins must enroll a second factor (passkey or TOTP) on next sign-in.
        </p>
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

interface HalfMembershipRowProps {
  playerId: string;
  half: SeasonHalf;
  currentFlightId: number | null;
  flights: Flight[];
}

/** One half's flight assignment: a dropdown, or read-only when the half is locked. */
function HalfMembershipRow({ playerId, half, currentFlightId, flights }: HalfMembershipRowProps) {
  const setMembership = useSetHalfMembership(playerId);
  const [value, setValue] = useState(currentFlightId != null ? String(currentFlightId) : '');
  const [saved, setSaved] = useState(false);

  const halfFlights = useMemo(
    () => flights.filter((f) => f.halfId === half.id).sort((a, b) => a.displayOrder - b.displayOrder),
    [flights, half.id],
  );

  const dirty = value !== (currentFlightId != null ? String(currentFlightId) : '');
  const currentFlightName = flights.find((f) => f.id === currentFlightId)?.name;

  async function save() {
    await setMembership.mutateAsync({ halfId: half.id, flightId: value === '' ? null : Number(value) });
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <div className="grid grid-cols-[1fr,2fr] items-center gap-3">
      <div className="text-sm font-medium text-gray-700">{half.name}</div>
      {half.isLocked ? (
        <div className="flex items-center gap-2 text-sm text-gray-600">
          <span>{currentFlightName ?? <span className="text-gray-400">Not in this half</span>}</span>
          <Badge variant="neutral">🔒 Locked</Badge>
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <select
            value={value}
            onChange={(e) => setValue(e.target.value)}
            className={selectClass}
          >
            <option value="">— Not in this half —</option>
            {halfFlights.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
          {dirty && (
            <Button size="sm" variant="primary" onClick={save} disabled={setMembership.isPending}>
              Save
            </Button>
          )}
          {saved && <span className="text-xs text-green-700">Saved!</span>}
        </div>
      )}
      {setMembership.isError && (
        <p className="col-span-2 text-sm text-red-600">
          {(setMembership.error as { response?: { data?: { error?: string } } })?.response?.data?.error
            ?? 'Failed to update. Try again.'}
        </p>
      )}
    </div>
  );
}

interface HalfMembershipsCardProps {
  playerId: string;
  halves: SeasonHalf[];
  flights: Flight[];
  membershipFlightByHalf: Map<number, number>;
}

function HalfMembershipsBody({ playerId, halves, flights, membershipFlightByHalf }: HalfMembershipsCardProps) {
  const ordered = useMemo(
    () => [...halves].sort((a, b) => a.halfNumber - b.halfNumber),
    [halves],
  );

  return (
    <div className="space-y-4">
      {ordered.map((half) => (
        <HalfMembershipRow
          key={half.id}
          playerId={playerId}
          half={half}
          currentFlightId={membershipFlightByHalf.get(half.id) ?? null}
          flights={flights}
        />
      ))}
    </div>
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

  // Player's current flight per half, from their active-season memberships.
  const membershipFlightByHalf = useMemo(() => {
    const map = new Map<number, number>();
    for (const m of player?.flightMemberships ?? []) map.set(m.halfId, m.flightId);
    return map;
  }, [player]);

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
      render: (h: HandicapHistoryEntry) => new Date(h.effectiveDate).toLocaleDateString(undefined, { timeZone: 'UTC' }),
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
            defaultValues={{
              name: player.fullName,
              email: player.email ?? '',
              isAdmin: player.roles?.includes('admin') ?? false,
              isScorer: player.roles?.includes('scorer') ?? false,
              isPlayer: player.roles?.includes('player') ?? false,
            }}
          />
        </Card>

        <Card className="p-6">
          <h2 className="mb-1 text-base font-semibold text-gray-900">Half Assignments</h2>
          <p className="mb-4 text-sm text-gray-500">
            Choose the flight this player competes in for each half of the active season.
            Pick &ldquo;Not in this half&rdquo; to leave them out. A half locks once its rounds start.
          </p>
          {activeSeason ? (
            <HalfMembershipsBody
              playerId={id}
              halves={activeSeason.halves}
              flights={activeSeasonFlights}
              membershipFlightByHalf={membershipFlightByHalf}
            />
          ) : (
            <p className="text-sm text-gray-500">No active season.</p>
          )}
        </Card>

        {player.appUserId === null && (
          <Card className="p-6 lg:col-span-2">
            <h2 className="mb-1 text-base font-semibold text-gray-900">Link to user account</h2>
            <p className="mb-4 text-sm text-gray-500">
              This player has no linked account. Pick an existing unlinked user to attach,
              or invite the player and they&apos;ll be linked on sign-up.
            </p>
            <LinkUserForm playerId={id} playerEmail={player.email} />
          </Card>
        )}

        <Card className="p-6 lg:col-span-2">
          <h2 className="mb-1 text-base font-semibold text-gray-900">Manual Handicap Override</h2>
          <p className="mb-4 text-sm text-gray-500">
            Current index: <strong title={HANDICAP_PAIR_TOOLTIP}>{formatHandicapPair(player.currentHandicap)}</strong>
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

      <TeeTimePreferenceCard playerId={id} currentMask={player.preferredTeeTimeSlots ?? 0} />

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
