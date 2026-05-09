import { useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { useSeasons } from '../../hooks/useSeasons';
import { useCreateRound } from '../../hooks/admin/useRoundMutations';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { Course } from '../../types/api';

const schema = z.object({
  halfId: z.string().min(1, 'Half is required'),
  scheduledDate: z.string().min(1, 'Date is required'),
  courseId: z.string().min(1, 'Course is required'),
  nineHoleSide: z.enum(['Front', 'Back', 'auto']).default('auto'),
  notes: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

interface CreateRoundFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

export function CreateRoundForm({ onSuccess, onCancel }: CreateRoundFormProps) {
  const createRound = useCreateRound();
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);

  const halves = activeSeason?.halves ?? [];

  const { data: coursesPage } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { nineHoleSide: 'auto' },
  });

  useEffect(() => {
    if (halves.length > 0) {
      setValue('halfId', String(halves[0].id), { shouldValidate: true });
    }
  }, [halves, setValue]);

  async function onSubmit(values: FormValues) {
    await createRound.mutateAsync({
      halfId: Number(values.halfId),
      courseId: Number(values.courseId),
      scheduledDate: values.scheduledDate,
      nineHoleSide: values.nineHoleSide === 'auto' ? undefined : values.nineHoleSide,
      notes: values.notes,
    });
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Half" error={errors.halfId} required>
        <select {...register('halfId')} className={selectClass}>
          {halves.map((h) => (
            <option key={h.id} value={h.id}>
              {h.name} ({h.startDate} → {h.endDate})
            </option>
          ))}
          {halves.length === 0 && <option value="">— No active season —</option>}
        </select>
      </FormField>

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

      <FormField label="Nine" error={errors.nineHoleSide}>
        <select {...register('nineHoleSide')} className={selectClass}>
          <option value="auto">Auto-alternate from previous</option>
          <option value="Front">Front 9</option>
          <option value="Back">Back 9</option>
        </select>
      </FormField>

      <FormField label="Notes" error={errors.notes}>
        <input {...register('notes')} className={inputClass} placeholder="Optional" />
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
