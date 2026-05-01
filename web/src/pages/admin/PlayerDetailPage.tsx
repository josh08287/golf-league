import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft } from 'lucide-react';
import { usePlayer, useHandicapHistory } from '../../hooks/usePlayers';
import {
  useUpdatePlayer,
  useDeactivatePlayer,
  useSetHandicap,
} from '../../hooks/admin/usePlayerMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Table } from '../../components/ui/Table';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { FormField, inputClass } from '../../components/admin/FormField';
import type { HandicapHistoryEntry } from '../../types/api';

// ── Schemas ────────────────────────────────────────────────────────────────

const editSchema = z.object({
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  email: z.string().email('Valid email required'),
});

const handicapSchema = z.object({
  newIndex: z
    .number({ invalid_type_error: 'Enter a number' })
    .min(-10)
    .max(54),
  notes: z.string().optional(),
});

type EditValues = z.infer<typeof editSchema>;
type HandicapValues = z.infer<typeof handicapSchema>;

// ── Edit Form ──────────────────────────────────────────────────────────────

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
      <div className="grid grid-cols-2 gap-4">
        <FormField label="First Name" error={errors.firstName} required>
          <input {...register('firstName')} className={inputClass} />
        </FormField>
        <FormField label="Last Name" error={errors.lastName} required>
          <input {...register('lastName')} className={inputClass} />
        </FormField>
      </div>
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

// ── Handicap Override Form ─────────────────────────────────────────────────

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

// ── Page ───────────────────────────────────────────────────────────────────

export function PlayerDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: player, isLoading, error } = usePlayer(id);
  const { data: history } = useHandicapHistory(id);
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
      render: (h: HandicapHistoryEntry) => new Date(h.date).toLocaleDateString(),
    },
    {
      key: 'index',
      header: 'Index',
      render: (h: HandicapHistoryEntry) => h.index.toFixed(1),
    },
    {
      key: 'source',
      header: 'Source',
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
          title={`${player.firstName} ${player.lastName}`}
          subtitle={
            <Badge variant={player.isActive ? 'success' : 'neutral'}>
              {player.isActive ? 'Active' : 'Inactive'}
            </Badge>
          }
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Edit form */}
        <Card className="p-6">
          <h2 className="mb-4 text-base font-semibold text-gray-900">Player Info</h2>
          <EditPlayerForm
            playerId={id}
            defaultValues={{
              firstName: player.firstName,
              lastName: player.lastName,
              email: player.email,
            }}
          />
        </Card>

        {/* Handicap override */}
        <Card className="p-6">
          <h2 className="mb-1 text-base font-semibold text-gray-900">Manual Handicap Override</h2>
          <p className="mb-4 text-sm text-gray-500">
            Current index: <strong>{player.handicapIndex?.toFixed(1) ?? '—'}</strong>
          </p>
          <HandicapOverrideForm playerId={id} currentIndex={player.handicapIndex ?? 18} />
        </Card>
      </div>

      {/* Handicap history */}
      <Card className="p-6">
        <h2 className="mb-4 text-base font-semibold text-gray-900">Handicap History</h2>
        <Table
          columns={handicapColumns}
          data={history ?? []}
          rowKey={(h) => `${h.date}-${h.index}`}
          emptyMessage="No handicap history yet."
        />
      </Card>

      {/* Danger zone */}
      {player.isActive && (
        <Card className="border-red-200 p-6">
          <h2 className="mb-1 text-base font-semibold text-red-700">Danger Zone</h2>
          <p className="mb-4 text-sm text-gray-500">
            Deactivating a player removes them from future rounds and standings calculations.
          </p>
          <Button
            variant="destructive"
            onClick={() => setConfirmDeactivate(true)}
          >
            Deactivate Player
          </Button>
        </Card>
      )}

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate Player"
        description={`Deactivate ${player.firstName} ${player.lastName}? This action can be reversed by support.`}
        confirmLabel="Deactivate"
        destructive
        onConfirm={handleDeactivate}
        onCancel={() => setConfirmDeactivate(false)}
      />
    </div>
  );
}
