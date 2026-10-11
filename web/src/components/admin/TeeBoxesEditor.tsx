import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, ChevronDown, ChevronUp, Pencil, Trash2 } from 'lucide-react';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { FormField, inputClass } from './FormField';
import {
  useAddTeeBox,
  useDeleteTeeBox,
  useUpdateHoleTeeBoxes,
  useUpdateTeeBox,
} from '../../hooks/admin/useCourseMutations';
import type { TeeBox, CourseHole } from '../../types/api';

function apiError(err: unknown): string | null {
  if (!err) return null;
  return (
    (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
    (err as Error).message ??
    'Something went wrong.'
  );
}

// ── Add / Edit TeeBox Form ─────────────────────────────────────────────────

const teeBoxSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  courseRating: z.number({ invalid_type_error: 'Required' }).min(55).max(80),
  slopeRating: z.number({ invalid_type_error: 'Required' }).int().min(55).max(155),
  // 0 is allowed so a migrated "Default" tee (no yardage/par on record) can be
  // edited without having to fill those in first.
  totalYardage: z.number({ invalid_type_error: 'Required' }).int().min(0).max(8000),
  par: z.number({ invalid_type_error: 'Required' }).int().min(0).max(80),
  isDefault: z.boolean(),
});

type TeeBoxFormValues = z.infer<typeof teeBoxSchema>;

interface TeeBoxFormProps {
  courseId: string;
  /** The tee being edited; omitted when adding a new one. */
  teeBox?: TeeBox;
  onSuccess: () => void;
  onCancel: () => void;
}

function TeeBoxForm({ courseId, teeBox, onSuccess, onCancel }: TeeBoxFormProps) {
  const addTeeBox = useAddTeeBox(courseId);
  const updateTeeBox = useUpdateTeeBox(courseId, teeBox?.id ?? 0);
  const mutation = teeBox ? updateTeeBox : addTeeBox;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<TeeBoxFormValues>({
    resolver: zodResolver(teeBoxSchema),
    defaultValues: teeBox
      ? {
          name: teeBox.name,
          courseRating: teeBox.courseRating,
          slopeRating: teeBox.slopeRating,
          totalYardage: teeBox.totalYardage,
          par: teeBox.par,
          isDefault: teeBox.isDefault,
        }
      : { isDefault: false },
  });

  async function onSubmit(values: TeeBoxFormValues) {
    if (teeBox) await updateTeeBox.mutateAsync(values);
    else await addTeeBox.mutateAsync(values);
    onSuccess();
  }

  const error = apiError(mutation.error);

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 rounded-lg border border-gray-200 bg-gray-50 p-4">
      <h4 className="font-medium text-gray-900">{teeBox ? `Edit ${teeBox.name}` : 'Add New Tee Box'}</h4>
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
      {/* The default can't be unset here — another tee has to be made the default instead. */}
      {!teeBox?.isDefault && (
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" {...register('isDefault')} className="h-4 w-4 rounded border-gray-300" />
          Make this the course&apos;s default tee
        </label>
      )}
      {teeBox && (
        <p className="text-xs text-gray-500">
          Changing the rating or slope updates rounds that haven&apos;t been finalized yet. Finalized rounds keep the
          rating they were played at.
        </p>
      )}
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" variant="primary" size="sm" disabled={isSubmitting || mutation.isPending}>
          {teeBox ? 'Save' : 'Add'}
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
    formState: { isDirty, isSubmitting },
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
  const [editingTeeBox, setEditingTeeBox] = useState<number | null>(null);
  const [expandedTeeBox, setExpandedTeeBox] = useState<number | null>(null);
  const deleteTeeBox = useDeleteTeeBox(courseId);
  const deleteError = apiError(deleteTeeBox.error);

  async function handleDelete(tb: TeeBox) {
    if (!window.confirm(`Delete the ${tb.name} tee box?`)) return;
    await deleteTeeBox.mutateAsync(tb.id).catch(() => undefined);
  }

  const sorted = [...teeBoxes].sort((a, b) => Number(b.isDefault) - Number(a.isDefault));

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

      <p className="text-xs text-gray-500">
        Players with no tee assigned play from the default tee. Each round records the rating and slope of the tee it
        was played from.
      </p>

      {adding && (
        <TeeBoxForm courseId={courseId} onSuccess={() => setAdding(false)} onCancel={() => setAdding(false)} />
      )}

      {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}

      {teeBoxes.length === 0 && !adding && (
        <p className="text-sm text-gray-500">No tee boxes configured.</p>
      )}

      <div className="space-y-3">
        {sorted.map(tb => {
          const isExpanded = expandedTeeBox === tb.id;
          if (editingTeeBox === tb.id) {
            return (
              <TeeBoxForm
                key={tb.id}
                courseId={courseId}
                teeBox={tb}
                onSuccess={() => setEditingTeeBox(null)}
                onCancel={() => setEditingTeeBox(null)}
              />
            );
          }
          return (
            <div key={tb.id} className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
              <div
                className="flex cursor-pointer items-center justify-between gap-3 px-5 py-4 hover:bg-gray-50"
                onClick={() => setExpandedTeeBox(isExpanded ? null : tb.id)}
              >
                <div>
                  <h4 className="flex items-center gap-2 font-semibold text-gray-900">
                    {tb.name}
                    {tb.isDefault && <Badge variant="green">Default</Badge>}
                  </h4>
                  <p className="text-xs text-gray-500">
                    Rating: {tb.courseRating} | Slope: {tb.slopeRating} | Yardage: {tb.totalYardage} | Par: {tb.par}
                  </p>
                </div>
                <div className="flex items-center gap-1 text-gray-400">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Edit ${tb.name}`}
                    onClick={(e) => {
                      e.stopPropagation();
                      setEditingTeeBox(tb.id);
                    }}
                  >
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Delete ${tb.name}`}
                    title={tb.isDefault ? "The default tee can't be deleted" : undefined}
                    disabled={tb.isDefault || deleteTeeBox.isPending}
                    onClick={(e) => {
                      e.stopPropagation();
                      void handleDelete(tb);
                    }}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
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
