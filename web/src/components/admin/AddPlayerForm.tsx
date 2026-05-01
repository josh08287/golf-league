import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useFlights } from '@/hooks/useFlights';
import { useCreatePlayer } from '@/hooks/admin/usePlayerMutations';
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
}

export function AddPlayerForm({ onSuccess, onCancel }: AddPlayerFormProps) {
  const { data: flightsPage } = useFlights();
  const flights = (flightsPage as PagedResponse<{ id: string; name: string }> | undefined)?.data ?? [];
  const createPlayer = useCreatePlayer();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { initialHandicap: 18 },
  });

  async function onSubmit(values: FormValues) {
    await createPlayer.mutateAsync({
      name: values.name,
      email: values.email,
      initialHandicap: values.initialHandicap,
      flightId: values.flightId || undefined,
    });
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
        />
      </FormField>

      <FormField label="Initial Handicap Index" error={errors.initialHandicap} required>
        <input
          {...register('initialHandicap', { valueAsNumber: true })}
          type="number"
          step="0.1"
          className={inputClass}
        />
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

      {createPlayer.isError && (
        <p className="text-sm text-red-600">Failed to create player. Please try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || createPlayer.isPending}>
          Add Player
        </Button>
      </div>
    </form>
  );
}
