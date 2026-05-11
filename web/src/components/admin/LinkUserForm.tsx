import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import { FormField, selectClass } from '@/components/admin/FormField';
import { useAdminUsers } from '@/hooks/admin/useAdminUsers';
import { useLinkPlayerToUser } from '@/hooks/admin/usePlayerMutations';

interface LinkUserFormProps {
  playerId: string;
  playerEmail: string | null;
}

/**
 * Card body for the player-detail "Link to user account" affordance. Pulls
 * the list of admin-only AppUsers (rows with no linked Player) and lets the
 * admin pick one. Warns when the chosen user's email differs from the
 * player's, since the link will overwrite the player's email to match.
 */
export function LinkUserForm({ playerId, playerEmail }: LinkUserFormProps) {
  const { data: users = [], isLoading, error } = useAdminUsers();
  const link = useLinkPlayerToUser(playerId);

  const [selectedId, setSelectedId] = useState<string>('');
  const [submitError, setSubmitError] = useState<string | null>(null);

  const selected = useMemo(
    () => users.find((u) => u.id === selectedId) ?? null,
    [users, selectedId],
  );

  const emailMismatch = !!(
    selected && playerEmail && selected.email
    && selected.email.toLowerCase() !== playerEmail.toLowerCase()
  );

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setSubmitError(null);
    try {
      await link.mutateAsync(selectedId);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Could not link account.';
      setSubmitError(message);
    }
  }

  if (isLoading) {
    return (
      <div className="flex h-20 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-red-600">Could not load user accounts.</p>;
  }

  if (users.length === 0) {
    return (
      <p className="text-sm text-gray-500">
        No unlinked user accounts available. Invite the player or create an account first.
      </p>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      <FormField label="User account">
        <select
          className={selectClass}
          value={selectedId}
          onChange={(e) => setSelectedId(e.target.value)}
        >
          <option value="">— Select an account —</option>
          {users.map((u) => (
            <option key={u.id} value={u.id}>
              {u.email}
            </option>
          ))}
        </select>
      </FormField>

      {emailMismatch && (
        <p className="text-xs text-amber-700">
          The user&apos;s email ({selected!.email}) doesn&apos;t match the player&apos;s
          ({playerEmail}). Linking will set the player&apos;s email to {selected!.email}.
        </p>
      )}

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}

      <Button
        type="submit"
        variant="primary"
        disabled={!selectedId || link.isPending}
      >
        {link.isPending ? 'Linking…' : 'Link account'}
      </Button>
    </form>
  );
}
