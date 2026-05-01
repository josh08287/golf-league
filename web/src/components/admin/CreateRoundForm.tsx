import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { useFlights } from '../../hooks/useFlights';
import { useCreateRound } from '../../hooks/admin/useRoundMutations';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { Course } from '../../types/api';

const schema = z.object({
  date: z.string().min(1, 'Date is required'),
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
  const { data: flights } = useFlights();
  const { data: courses } = useQuery<Course[]>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    await createRound.mutateAsync(values);
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Date" error={errors.date} required>
        <input {...register('date')} type="date" className={inputClass} />
      </FormField>

      <FormField label="Course" error={errors.courseId} required>
        <select {...register('courseId')} className={selectClass}>
          <option value="">— Select course —</option>
          {(courses ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </FormField>

      <FormField label="Flight" error={errors.flightId} required>
        <select {...register('flightId')} className={selectClass}>
          <option value="">— Select flight —</option>
          {(flights ?? []).map((f) => (
            <option key={f.id} value={f.id}>
              {f.name}
            </option>
          ))}
        </select>
      </FormField>

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
