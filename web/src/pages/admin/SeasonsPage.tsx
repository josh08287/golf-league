import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, CheckCircle, Trash2 } from 'lucide-react';
import { useSeasons, useCreateSeason, useSetActiveSeason, useDeleteSeason } from '../../hooks/useSeasons';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { FormField, inputClass } from '../../components/admin/FormField';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import type { Season } from '../../types/api';

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  year: z.number({ invalid_type_error: 'Enter a year' }).int().min(2000).max(2100),
  startDate: z.string().min(1, 'Start date is required'),
  endDate: z.string().min(1, 'End date is required'),
  bestNRounds: z.number({ invalid_type_error: 'Enter a number' }).int().min(1).optional(),
});

type FormValues = z.infer<typeof schema>;

function CreateSeasonForm({ onSuccess, onCancel }: { onSuccess: () => void; onCancel: () => void }) {
  const create = useCreateSeason();
  const year = new Date().getFullYear();

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { year, startDate: `${year}-01-01`, endDate: `${year}-12-31` },
  });

  async function onSubmit(values: FormValues) {
    await create.mutateAsync(values);
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <FormField label="Season Name" error={errors.name} required>
        <input {...register('name')} className={inputClass} placeholder={`${year} Season`} />
      </FormField>

      <FormField label="Year" error={errors.year} required>
        <input {...register('year', { valueAsNumber: true })} type="number" className={inputClass} />
      </FormField>

      <div className="grid grid-cols-2 gap-4">
        <FormField label="Start Date" error={errors.startDate} required>
          <input {...register('startDate')} type="date" className={inputClass} />
        </FormField>
        <FormField label="End Date" error={errors.endDate} required>
          <input {...register('endDate')} type="date" className={inputClass} />
        </FormField>
      </div>

      <FormField label="Best N Rounds" error={errors.bestNRounds} hint="Leave blank to count all rounds">
        <input {...register('bestNRounds', { valueAsNumber: true })} type="number" className={inputClass} placeholder="e.g. 10" />
      </FormField>

      {create.isError && (
        <p className="text-sm text-red-600">Failed to create season. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>Cancel</Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || create.isPending}>
          Create Season
        </Button>
      </div>
    </form>
  );
}

export function SeasonsPage() {
  const { data: seasons, isLoading, error } = useSeasons();
  const setActive = useSetActiveSeason();
  const deleteSeason = useDeleteSeason();
  const [createOpen, setCreateOpen] = useState(false);
  const [activateTarget, setActivateTarget] = useState<Season | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Season | null>(null);

  async function handleDelete() {
    if (!deleteTarget) return;
    await deleteSeason.mutateAsync(String(deleteTarget.id));
    setDeleteTarget(null);
  }

  if (isLoading) {
    return <div className="flex h-64 items-center justify-center"><Spinner /></div>;
  }

  if (error) {
    return <ErrorMessage message="Failed to load seasons." />;
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Seasons">
        <Button variant="primary" onClick={() => setCreateOpen(true)}>
          <Plus className="mr-1 h-4 w-4" />
          Create Season
        </Button>
      </PageHeader>

      <div className="space-y-3">
        {seasons?.map((s) => (
          <Card key={s.id} className="p-5">
            <div className="flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-gray-900">{s.name}</h3>
                  {s.isActive && (
                    <Badge variant="success">Active</Badge>
                  )}
                </div>
                <p className="mt-0.5 text-xs text-gray-500">
                  {s.startDate} — {s.endDate}
                  {s.bestNRounds != null && ` · Best ${s.bestNRounds} rounds`}
                </p>
                {s.halves && s.halves.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {s.halves.map((h) => (
                      <span
                        key={h.id}
                        className="inline-flex items-center rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600"
                      >
                        {h.name}: {h.startDate} → {h.endDate}
                      </span>
                    ))}
                  </div>
                )}
              </div>
              <div className="flex items-center gap-2">
                {!s.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setActivateTarget(s)}
                  >
                    <CheckCircle className="mr-1 h-3.5 w-3.5" />
                    Set Active
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-red-600 hover:bg-red-50 hover:text-red-700"
                  onClick={() => setDeleteTarget(s)}
                  title="Delete season"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </Card>
        ))}
        {seasons?.length === 0 && (
          <p className="text-sm text-gray-500">No seasons yet.</p>
        )}
      </div>

      <Modal open={createOpen} title="Create Season" onClose={() => setCreateOpen(false)}>
        <CreateSeasonForm
          onSuccess={() => setCreateOpen(false)}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>

      <ConfirmDialog
        open={!!activateTarget}
        title="Set Active Season"
        description={`Make "${activateTarget?.name}" the active season? This will deactivate the current active season.`}
        confirmLabel="Set Active"
        onConfirm={async () => {
          if (!activateTarget) return;
          await setActive.mutateAsync(activateTarget.id);
          setActivateTarget(null);
        }}
        onCancel={() => setActivateTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Season"
        description={`Permanently delete ${deleteTarget?.name}? This will remove all associated rounds and cannot be undone.`}
        confirmLabel="Delete"
        destructive
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
