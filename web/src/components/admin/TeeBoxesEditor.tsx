import { useState } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, ChevronDown, ChevronUp } from 'lucide-react';
import { Button } from '../ui/Button';
import { FormField, inputClass } from './FormField';
import { useAddTeeBox, useUpdateHoleTeeBoxes } from '../../hooks/admin/useCourseMutations';
import type { TeeBox, CourseHole } from '../../types/api';

// ── Add TeeBox Form ────────────────────────────────────────────────────────

const addTeeBoxSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  courseRating: z.number({ invalid_type_error: 'Required' }).min(60).max(80),
  slopeRating: z.number({ invalid_type_error: 'Required' }).int().min(55).max(155),
  totalYardage: z.number({ invalid_type_error: 'Required' }).int().min(1000).max(8000),
  par: z.number({ invalid_type_error: 'Required' }).int().min(60).max(80),
});

type AddTeeBoxFormValues = z.infer<typeof addTeeBoxSchema>;

interface AddTeeBoxProps {
  courseId: string;
  onSuccess: () => void;
  onCancel: () => void;
}

function AddTeeBoxForm({ courseId, onSuccess, onCancel }: AddTeeBoxProps) {
  const addTeeBox = useAddTeeBox(courseId);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<AddTeeBoxFormValues>({ resolver: zodResolver(addTeeBoxSchema) });

  async function onSubmit(values: AddTeeBoxFormValues) {
    await addTeeBox.mutateAsync(values);
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 rounded-lg border border-gray-200 bg-gray-50 p-4">
      <h4 className="font-medium text-gray-900">Add New Tee Box</h4>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
        <FormField label="Name" error={errors.name} required>
          <input {...register('name')} className={inputClass} placeholder="Blue" />
        </FormField>
        <FormField label="Rating" error={errors.courseRating} required>
          <input {...register('courseRating', { valueAsNumber: true })} type="number" step="0.1" className={inputClass} placeholder="71.2" />
        </FormField>
        <FormField label="Slope" error={errors.slopeRating} required>
          <input {...register('slopeRating', { valueAsNumber: true })} type="number" className={inputClass} placeholder="125" />
        </FormField>
        <FormField label="Yardage" error={errors.totalYardage} required>
          <input {...register('totalYardage', { valueAsNumber: true })} type="number" className={inputClass} placeholder="6400" />
        </FormField>
        <FormField label="Par" error={errors.par} required>
          <input {...register('par', { valueAsNumber: true })} type="number" className={inputClass} placeholder="72" />
        </FormField>
      </div>
      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" size="sm" disabled={isSubmitting || addTeeBox.isPending}>
          Add
        </Button>
      </div>
    </form>
  );
}

// ── Edit HoleTeeBoxes Form ────────────────────────────────────────────────

const holeSchema = z.object({
  courseHoleId: z.number(),
  holeNumber: z.number(),
  yardage: z.number({ invalid_type_error: 'Required' }).int().min(0),
  par: z.number({ invalid_type_error: 'Required' }).int().min(3).max(5),
});

const updateHolesSchema = z.object({
  holes: z.array(holeSchema),
});

type UpdateHolesFormValues = z.infer<typeof updateHolesSchema>;

interface EditHoleTeeBoxesProps {
  courseId: string;
  teeBox: TeeBox;
  courseHoles: CourseHole[];
}

function EditHoleTeeBoxes({ courseId, teeBox, courseHoles }: EditHoleTeeBoxesProps) {
  const updateHoles = useUpdateHoleTeeBoxes(courseId, String(teeBox.id));

  // Merge courseHoles (which drive the 1-18 layout) with teeBox.holes (yardage/par data)
  const defaultHoles = courseHoles.map(ch => {
    const existing = teeBox.holes.find(h => h.courseHoleId === ch.id);
    return {
      courseHoleId: ch.id,
      holeNumber: ch.holeNumber,
      yardage: existing?.yardage ?? 0,
      par: existing?.par ?? ch.par,
    };
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<UpdateHolesFormValues>({
    resolver: zodResolver(updateHolesSchema),
    defaultValues: { holes: defaultHoles },
  });

  async function onSubmit(values: UpdateHolesFormValues) {
    await updateHoles.mutateAsync(values);
    reset(values); // marks form as pristine again
  }

  const cellClassSmall = 'w-16 rounded border border-gray-300 px-1.5 py-1 text-center text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mt-4">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50 text-xs font-semibold uppercase tracking-wider text-gray-500">
              <th className="px-3 py-2 text-left">Hole</th>
              <th className="px-3 py-2 text-center">Yardage</th>
              <th className="px-3 py-2 text-center">Par</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {defaultHoles.map((_, i) => (
              <tr key={i} className="hover:bg-gray-50/50">
                <td className="px-3 py-1.5 font-medium text-gray-700">
                  {i + 1}
                  <input type="hidden" {...register(`holes.${i}.courseHoleId`, { valueAsNumber: true })} />
                  <input type="hidden" {...register(`holes.${i}.holeNumber`, { valueAsNumber: true })} />
                </td>
                <td className="px-3 py-1.5 text-center">
                  <input
                    {...register(`holes.${i}.yardage`, { valueAsNumber: true })}
                    type="number"
                    min={0}
                    className={cellClassSmall}
                  />
                </td>
                <td className="px-3 py-1.5 text-center">
                  <select
                    {...register(`holes.${i}.par`, { valueAsNumber: true })}
                    className={cellClassSmall}
                  >
                    {[3, 4, 5].map((p) => (
                      <option key={p} value={p}>
                        {p}
                      </option>
                    ))}
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mt-4 flex items-center gap-3">
        <Button type="submit" variant="primary" disabled={!isDirty || isSubmitting || updateHoles.isPending}>
          Save Hole Yardages
        </Button>
        {updateHoles.isSuccess && (
          <span className="text-sm text-green-700">Saved!</span>
        )}
      </div>
    </form>
  );
}

export function TeeBoxesEditor({ courseId, courseHoles, teeBoxes }: { courseId: string; courseHoles: CourseHole[]; teeBoxes: TeeBox[] }) {
  const [adding, setAdding] = useState(false);
  const [expandedTeeBox, setExpandedTeeBox] = useState<number | null>(null);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold text-gray-900">Tee Boxes</h3>
        {!adding && (
          <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
            <Plus className="mr-1 h-4 w-4" />
            Add Tee Box
          </Button>
        )}
      </div>

      {adding && (
        <AddTeeBoxForm courseId={courseId} onSuccess={() => setAdding(false)} onCancel={() => setAdding(false)} />
      )}

      {teeBoxes.length === 0 && !adding && (
        <p className="text-sm text-gray-500">No tee boxes configured.</p>
      )}

      <div className="space-y-3">
        {teeBoxes.map(tb => {
          const isExpanded = expandedTeeBox === tb.id;
          return (
            <div key={tb.id} className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
              <div
                className="flex cursor-pointer items-center justify-between px-5 py-4 hover:bg-gray-50"
                onClick={() => setExpandedTeeBox(isExpanded ? null : tb.id)}
              >
                <div>
                  <h4 className="font-semibold text-gray-900">{tb.name}</h4>
                  <p className="text-xs text-gray-500">
                    Rating: {tb.courseRating} | Slope: {tb.slopeRating} | Yardage: {tb.totalYardage} | Par: {tb.par}
                  </p>
                </div>
                <div className="text-gray-400">
                  {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                </div>
              </div>
              {isExpanded && (
                <div className="border-t border-gray-100 px-5 py-4 bg-gray-50/30">
                  <EditHoleTeeBoxes courseId={courseId} teeBox={tb} courseHoles={courseHoles} />
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
