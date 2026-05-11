import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { DataTable } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/Badge';
import { Spinner } from '@/components/ui/Spinner';
import { ConfirmDialog } from '@/components/admin/ConfirmDialog';
import { Modal } from '@/components/admin/Modal';
import { AddPlayerForm } from '@/components/admin/AddPlayerForm';
import {
  useAdminUsers,
  useUpdateAdminUserRoles,
  useResetAdminUserMfa,
  useDeleteAdminUser,
  type AdminUser,
  type AdminUserRole,
} from '@/hooks/admin/useAdminUsers';

const ALL_ROLES: AdminUserRole[] = ['admin', 'scorer', 'player'];

function rolesEqual(a: AdminUserRole[], b: AdminUserRole[]): boolean {
  if (a.length !== b.length) return false;
  const set = new Set(a);
  return b.every((r) => set.has(r));
}

function RoleCheckboxes({
  user,
  pending,
  onSave,
}: {
  user: AdminUser;
  pending: boolean;
  onSave: (roles: AdminUserRole[]) => void;
}) {
  const [draft, setDraft] = useState<Set<AdminUserRole>>(new Set(user.roles));

  function toggle(role: AdminUserRole) {
    const next = new Set(draft);
    if (next.has(role)) next.delete(role);
    else next.add(role);
    setDraft(next);
  }

  const dirty = !rolesEqual([...draft], user.roles);

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex flex-wrap gap-3">
        {ALL_ROLES.map((role) => (
          <label key={role} className="flex items-center gap-1.5 text-sm capitalize">
            <input
              type="checkbox"
              checked={draft.has(role)}
              onChange={() => toggle(role)}
            />
            {role}
          </label>
        ))}
      </div>
      {dirty && (
        <Button
          variant="primary"
          size="sm"
          className="self-start"
          disabled={pending}
          onClick={() => onSave([...draft])}
        >
          {pending ? 'Saving…' : 'Save roles'}
        </Button>
      )}
    </div>
  );
}

export function AdministratorsTable() {
  const { data: users = [], isLoading, error } = useAdminUsers();
  const updateRoles = useUpdateAdminUserRoles();
  const resetMfa = useResetAdminUserMfa();
  const deleteUser = useDeleteAdminUser();

  const [resetTarget, setResetTarget] = useState<AdminUser | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AdminUser | null>(null);
  const [attachTarget, setAttachTarget] = useState<AdminUser | null>(null);
  const [opError, setOpError] = useState<string | null>(null);

  async function handleSaveRoles(user: AdminUser, roles: AdminUserRole[]) {
    setOpError(null);
    try {
      await updateRoles.mutateAsync({ id: user.id, roles });
    } catch (err) {
      setOpError(extractError(err) ?? 'Failed to update roles.');
    }
  }

  async function handleResetMfa() {
    if (!resetTarget) return;
    setOpError(null);
    try {
      await resetMfa.mutateAsync(resetTarget.id);
    } catch (err) {
      setOpError(extractError(err) ?? 'Failed to reset MFA.');
    } finally {
      setResetTarget(null);
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    setOpError(null);
    try {
      await deleteUser.mutateAsync(deleteTarget.id);
    } catch (err) {
      setOpError(extractError(err) ?? 'Failed to delete user.');
    } finally {
      setDeleteTarget(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex h-32 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <p className="text-sm text-red-600">Failed to load administrators.</p>;
  }

  const columns = [
    {
      key: 'email',
      header: 'Email',
      render: (u: AdminUser) => <span className="font-medium text-gray-900">{u.email}</span>,
    },
    {
      key: 'roles',
      header: 'Roles',
      render: (u: AdminUser) => (
        <RoleCheckboxes
          user={u}
          pending={updateRoles.isPending}
          onSave={(roles) => void handleSaveRoles(u, roles)}
        />
      ),
    },
    {
      key: 'mfa',
      header: 'MFA',
      render: (u: AdminUser) => (
        <div className="flex flex-wrap gap-1.5">
          {u.hasTotp && <Badge variant="success">TOTP</Badge>}
          {u.hasPasskey && <Badge variant="success">Passkey</Badge>}
          {!u.hasTotp && !u.hasPasskey && <span className="text-xs text-gray-400">none</span>}
        </div>
      ),
    },
    {
      key: 'password',
      header: 'Password',
      render: (u: AdminUser) =>
        u.hasPassword
          ? <span className="text-xs text-gray-500">set</span>
          : <span className="text-xs text-amber-600">not set</span>,
    },
    {
      key: 'lastLogin',
      header: 'Last login',
      render: (u: AdminUser) =>
        u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : <span className="text-gray-400">never</span>,
    },
    {
      key: 'actions',
      header: '',
      render: (u: AdminUser) => (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setAttachTarget(u)}
          >
            Attach player profile
          </Button>
          <Button
            variant="ghost"
            size="sm"
            disabled={!u.hasTotp && !u.hasPasskey}
            onClick={() => setResetTarget(u)}
          >
            Reset MFA
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="text-red-700 hover:bg-red-50 hover:text-red-800"
            onClick={() => setDeleteTarget(u)}
          >
            Delete
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-3">
      {opError && (
        <p className="text-sm text-red-600">{opError}</p>
      )}
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <DataTable
          columns={columns}
          data={users}
          rowKey={(u) => u.id}
          emptyMessage="No administrator-only accounts yet."
        />
      </div>

      <ConfirmDialog
        open={!!resetTarget}
        title="Reset MFA"
        description={`Clear ${resetTarget?.email}'s passkeys and TOTP enrollment? They'll have to set up a new second factor on their next sign-in.`}
        confirmLabel="Reset MFA"
        destructive
        onConfirm={handleResetMfa}
        onCancel={() => setResetTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete account"
        description={`Permanently delete ${deleteTarget?.email}? This removes the account, its sessions, passkeys, and role assignments. This cannot be undone.`}
        confirmLabel="Delete account"
        destructive
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />

      <Modal
        open={!!attachTarget}
        title="Attach player profile"
        onClose={() => setAttachTarget(null)}
      >
        {attachTarget && (
          <AddPlayerForm
            attachToUser={{ id: attachTarget.id, email: attachTarget.email }}
            onSuccess={() => setAttachTarget(null)}
            onCancel={() => setAttachTarget(null)}
          />
        )}
      </Modal>
    </div>
  );
}

function extractError(err: unknown): string | null {
  const e = err as { response?: { data?: { error?: string } } } | undefined;
  return e?.response?.data?.error ?? null;
}
