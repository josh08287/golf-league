import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useUpdateCourseHoles } from '../../hooks/admin/useCourseMutations';
import { Button } from '../ui/Button';

// ── Schema ─────────────────────────────────────────────────────────────────

const holeSchema = z.object({
  holeNumber: z.number().int().min(1).max(18),
  par: z.number({ invalid_type_error: 'Required' }).int().min(3).max(5),
  strokeIndex: z.number({ invalid_type_error: 'Required' }).int().min(1).max(18),
});

const schema = z.object({
  holes: z.array(holeSchema).length(18),
});

type FormValues = z.infer<typeof schema>;

// ── Component ──────────────────────────────────────────────────────────────

interface HoleData {
  holeNumber: number;
  par: number;
  strokeIndex: number;
}

interface CourseHolesEditorProps {
  courseId: string;
  initialHoles: HoleData[];
}

const cellClass =
  'w-16 rounded border border-gray-300 px-1.5 py-1 text-center text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

export function CourseHolesEditor({ courseId, initialHoles }: CourseHolesEditorProps) {
  const updateHoles = useUpdateCourseHoles(courseId);

  const defaultHoles: HoleData[] = Array.from({ length: 18 }, (_, i) => {
    const existing = initialHoles.find((h) => h.holeNumber === i + 1);
    return existing ?? { holeNumber: i + 1, par: 4, strokeIndex: i + 1 };
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { holes: defaultHoles },
  });

  // Reset when initialHoles changes (e.g. after load)
  useEffect(() => {
    reset({ holes: defaultHoles });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [courseId]);

  async function onSubmit(values: FormValues) {
    await updateHoles.mutateAsync({ holes: values.holes });
    reset(values); // mark form as pristine after save
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50 text-xs font-semibold uppercase tracking-wider text-gray-500">
              <th className="px-3 py-2 text-left">Hole</th>
              <th className="px-3 py-2 text-center">Par</th>
              <th className="px-3 py-2 text-center">Stroke Index</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {defaultHoles.map((_, i) => (
              <tr key={i} className="hover:bg-gray-50/50">
                <td className="px-3 py-1.5 font-medium text-gray-700">
                  {i + 1}
                  <input type="hidden" {...register(`holes.${i}.holeNumber`, { valueAsNumber: true })} value={i + 1} />
                </td>
                <td className="px-3 py-1.5 text-center">
                  <select
                    {...register(`holes.${i}.par`, { valueAsNumber: true })}
                    className={cellClass}
                  >
                    {[3, 4, 5].map((p) => (
                      <option key={p} value={p}>
                        {p}
                      </option>
                    ))}
                  </select>
                  {errors.holes?.[i]?.par && (
                    <span className="block text-xs text-red-500">
                      {errors.holes[i]?.par?.message}
                    </span>
                  )}
                </td>
                <td className="px-3 py-1.5 text-center">
                  <input
                    {...register(`holes.${i}.strokeIndex`, { valueAsNumber: true })}
                    type="number"
                    min={1}
                    max={18}
                    className={cellClass}
                  />
                  {errors.holes?.[i]?.strokeIndex && (
                    <span className="block text-xs text-red-500">
                      {errors.holes[i]?.strokeIndex?.message}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {updateHoles.isError && (
        <p className="mt-3 text-sm text-red-600">Failed to save. Please try again.</p>
      )}

      <div className="mt-4 flex items-center gap-3">
        <Button type="submit" variant="primary" disabled={!isDirty || isSubmitting || updateHoles.isPending}>
          Save Holes
        </Button>
        {updateHoles.isSuccess && (
          <span className="text-sm text-green-700">Saved!</span>
        )}
      </div>
    </form>
  );
}
