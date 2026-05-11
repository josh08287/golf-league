import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useFlights } from '@/hooks/useFlights';
import { useCreatePlayer } from '@/hooks/admin/usePlayerMutations';
import { useAttachPlayerProfile } from '@/hooks/admin/useAdminUsers';
import { Button } from '@/components/ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { PagedResponse } from '@/types/api';

// ── Schema ─────────────────────────────────────────────────────────────────

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  email: z.string().email('Valid email required'),
  initialHandicap: z
    .number({ invalid_type_error: 'Enter a number' })
    .min(-10)
    .max(54),
  flightId: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

// ── Component ──────────────────────────────────────────────────────────────

interface AddPlayerFormProps {
  onSuccess: () => void;
  onCancel: () => void;
  /**
   * When set, the form attaches a Player profile to the supplied AppUser
   * instead of creating a brand-new player. Email is taken from the user
   * record and locked.
   */
  attachToUser?: { id: string; email: string };
}

export function AddPlayerForm({ onSuccess, onCancel, attachToUser }: AddPlayerFormProps) {
  const { data: flightsPage } = useFlights();
  const flights = (flightsPage as PagedResponse<{ id: string; name: string }> | undefined)?.data ?? [];
  const createPlayer = useCreatePlayer();
  const attachPlayer = useAttachPlayerProfile();

  const isAttachMode = !!attachToUser;
  const mutation = isAttachMode ? attachPlayer : createPlayer;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      initialHandicap: 18,
      email: attachToUser?.email ?? '',
    },
  });

  async function onSubmit(values: FormValues) {
    if (isAttachMode && attachToUser) {
      // Split the name on the first space; admin can refine later via Edit.
      const parts = values.name.trim().split(/\s+/);
      const firstName = parts[0] ?? '';
      const lastName = parts.slice(1).join(' ');
      await attachPlayer.mutateAsync({
        userId: attachToUser.id,
        firstName,
        lastName,
        initialHandicap: values.initialHandicap,
        flightId: values.flightId ? Number(values.flightId) : undefined,
      });
    } else {
      await createPlayer.mutateAsync({
        name: values.name,
        email: values.email,
        initialHandicap: values.initialHandicap,
        flightId: values.flightId || undefined,
      });
    }
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Full Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} placeholder="Jane Smith" />
      </FormField>

      <FormField label="Email" error={errors.email} required>
        <input
          {...register('email')}
          type="email"
          className={inputClass}
          placeholder="jane@example.com"
          readOnly={isAttachMode}
          // In attach mode the email is the AppUser's email and can't change.
          // Dimmed visually so admins notice it's not editable.
          style={isAttachMode ? { backgroundColor: '#f9fafb', cursor: 'not-allowed' } : undefined}
        />
        {isAttachMode && (
          <p className="mt-1 text-xs text-gray-400">
            Email comes from the account and can't change here.
          </p>
        )}
      </FormField>

      <FormField label="Initial Handicap Index" error={errors.initialHandicap} required>
        <input
          {...register('initialHandicap', { valueAsNumber: true })}
          type="number"
          step="0.1"
          className={inputClass}
        />
        {isAttachMode && (
          <p className="mt-1 text-xs text-gray-400">
            If a Player row with this email already exists, its handicap history is preserved
            and this value is ignored.
          </p>
        )}
      </FormField>

      <FormField label="Flight Assignment" error={errors.flightId}>
        <select {...register('flightId')} className={selectClass}>
          <option value="">— Unassigned —</option>
          {flights.map((f) => (
            <option key={f.id} value={f.id}>
              {f.name}
            </option>
          ))}
        </select>
      </FormField>

      {mutation.isError && (
        <p className="text-sm text-red-600">
          {(() => {
            const err = mutation.error as { response?: { data?: { error?: string } }; message?: string };
            return err?.response?.data?.error ?? err?.message
              ?? (isAttachMode ? 'Failed to attach player profile.' : 'Failed to create player. Please try again.');
          })()}
        </p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || mutation.isPending}>
          {isAttachMode ? 'Attach Profile' : 'Add Player'}
        </Button>
      </div>
    </form>
  );
}
