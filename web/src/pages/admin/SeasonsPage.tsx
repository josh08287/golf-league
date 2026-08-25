import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, CheckCircle, Trash2, Pencil } from 'lucide-react';
import { useSeasons, useCreateSeason, useSetActiveSeason, useDeleteSeason, useUpdateSeasonHalf } from '../../hooks/useSeasons';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Badge } from '../../components/ui/Badge';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { Modal } from '../../components/admin/Modal';
import { FormField, inputClass } from '../../components/admin/FormField';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { SCORING_FORMATS, MATCH_PLAY_FORMULA_VARIABLES } from '../../types/api';
import type { Season, SeasonHalf } from '../../types/api';

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  year: z.number({ invalid_type_error: 'Enter a year' }).int().min(2000).max(2100),
  startDate: z.string().min(1, 'Start date is required'),
  endDate: z.string().min(1, 'End date is required'),
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

const halfSchema = z
  .object({
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
  })
  .refine((v) => v.endDate > v.startDate, {
    message: 'End date must be after start date',
    path: ['endDate'],
  });

type HalfFormValues = z.infer<typeof halfSchema>;

function EditHalfForm({ half, onSuccess, onCancel }: { half: SeasonHalf; onSuccess: () => void; onCancel: () => void }) {
  const update = useUpdateSeasonHalf();

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<HalfFormValues>({
    resolver: zodResolver(halfSchema),
    defaultValues: { startDate: half.startDate, endDate: half.endDate },
  });

  const [scoringFormat, setScoringFormat] = useState<'stableford' | 'matchPlay'>(half.scoringFormat);
  const [useCustomFormula, setUseCustomFormula] = useState(Boolean(half.matchPlayCustomFormula));
  const [formulaInput, setFormulaInput] = useState(half.matchPlayCustomFormula ?? '');

  async function onSubmit(values: HalfFormValues) {
    await update.mutateAsync({
      halfId: half.id,
      startDate: values.startDate,
      endDate: values.endDate,
      scoringFormat,
      matchPlayCustomFormula: scoringFormat === SCORING_FORMATS.matchPlay && useCustomFormula ? formulaInput.trim() : null,
    });
    onSuccess();
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <FormField label="Start Date" error={errors.startDate} required>
          <input {...register('startDate')} type="date" className={inputClass} />
        </FormField>
        <FormField label="End Date" error={errors.endDate} required>
          <input {...register('endDate')} type="date" className={inputClass} />
        </FormField>
      </div>

      <div>
        <p className="mb-2 text-sm font-medium text-gray-900">Scoring format</p>
        <div className="space-y-2">
          <label className="flex items-start gap-2 text-sm">
            <input
              type="radio"
              name="scoring-format"
              className="mt-0.5"
              checked={scoringFormat === SCORING_FORMATS.stableford}
              onChange={() => setScoringFormat('stableford')}
            />
            <span>
              <span className="font-medium text-gray-900">Stableford</span>{' '}
              <span className="text-gray-500">points per hole based on net score vs. par</span>
            </span>
          </label>
          <label className="flex items-start gap-2 text-sm">
            <input
              type="radio"
              name="scoring-format"
              className="mt-0.5"
              checked={scoringFormat === SCORING_FORMATS.matchPlay}
              onChange={() => setScoringFormat('matchPlay')}
            />
            <span>
              <span className="font-medium text-gray-900">Match play</span>{' '}
              <span className="text-gray-500">round-robin head-to-head within each flight</span>
            </span>
          </label>
        </div>

        {scoringFormat === SCORING_FORMATS.matchPlay && (
          <div className="mt-3 space-y-2 pl-6">
            <label className="flex items-start gap-2 text-sm">
              <input
                type="radio"
                name="match-play-scoring"
                className="mt-0.5"
                checked={!useCustomFormula}
                onChange={() => setUseCustomFormula(false)}
              />
              <span>
                <span className="font-medium text-gray-900">Standard scoring</span>{' '}
                <span className="text-gray-500">2 pts per hole won, 1 pt each for a halve, plus a 4-pt bonus for winning the match</span>
              </span>
            </label>
            <label className="flex items-start gap-2 text-sm">
              <input
                type="radio"
                name="match-play-scoring"
                className="mt-0.5"
                checked={useCustomFormula}
                onChange={() => setUseCustomFormula(true)}
              />
              <span>
                <span className="font-medium text-gray-900">Custom formula</span>{' '}
                <span className="text-gray-500">enter your own per-hole formula below</span>
              </span>
            </label>

            {useCustomFormula && (
              <div className="pl-6">
                <textarea
                  value={formulaInput}
                  onChange={(e) => setFormulaInput(e.target.value)}
                  placeholder="netStrokes < opponentNetStrokes ? 2 : (netStrokes > opponentNetStrokes ? 0 : 1)"
                  rows={2}
                  disabled={update.isPending}
                  className="w-full rounded-md border border-gray-300 px-3 py-1.5 font-mono text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Available variables: {MATCH_PLAY_FORMULA_VARIABLES.map((v, i) => (
                    <span key={v}>
                      <code>{v}</code>{i < MATCH_PLAY_FORMULA_VARIABLES.length - 1 ? ', ' : ''}
                    </span>
                  ))}. Evaluated once per player per hole; must evaluate to a number.
                </p>
              </div>
            )}
          </div>
        )}
      </div>

      {update.isError && (
        <p className="text-sm text-red-600">Failed to update half. Try again.</p>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>Cancel</Button>
        <Button type="submit" variant="primary" disabled={isSubmitting || update.isPending}>
          Save Changes
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
  const [editHalfTarget, setEditHalfTarget] = useState<SeasonHalf | null>(null);

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
                </p>
                {s.halves && s.halves.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {s.halves.map((h) => (
                      <button
                        key={h.id}
                        type="button"
                        onClick={() => setEditHalfTarget(h)}
                        className="group inline-flex items-center gap-1 rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600 hover:bg-gray-200 hover:text-gray-900"
                        title="Edit date range"
                      >
                        {h.name}: {h.startDate} → {h.endDate}
                        <Pencil className="h-3 w-3 opacity-0 group-hover:opacity-100" />
                      </button>
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

      <Modal open={!!editHalfTarget} title={`Edit ${editHalfTarget?.name ?? 'Half'}`} onClose={() => setEditHalfTarget(null)}>
        {editHalfTarget && (
          <EditHalfForm
            half={editHalfTarget}
            onSuccess={() => setEditHalfTarget(null)}
            onCancel={() => setEditHalfTarget(null)}
          />
        )}
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
