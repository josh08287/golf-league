import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { UserPlus } from 'lucide-react';
import { usePendingRegistrations, useApproveRegistration, useRejectRegistration } from '@/hooks/admin/useRegistrations';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import { ConfirmDialog } from './ConfirmDialog';
import type { Registration } from '@/types/api';

interface RejectTarget {
  reg: Registration;
  reason: string;
}

export function JoinRequestsPanel() {
  const { data: registrations = [], isLoading } = usePendingRegistrations();
  const approve = useApproveRegistration();
  const reject = useRejectRegistration();
  const navigate = useNavigate();

  const [approveTarget, setApproveTarget] = useState<Registration | null>(null);
  const [rejectTarget, setRejectTarget] = useState<RejectTarget | null>(null);

  if (isLoading) return <Spinner />;
  if (registrations.length === 0) return null;

  async function handleApprove() {
    if (!approveTarget) return;
    const result = await approve.mutateAsync(approveTarget.id);
    setApproveTarget(null);
    // Navigate to the new player's detail page so admin can set handicap + flight
    const playerId = (result as { data?: { id?: number } })?.data?.id;
    if (playerId) navigate(`/admin/players/${playerId}`);
  }

  async function handleReject() {
    if (!rejectTarget) return;
    await reject.mutateAsync({ id: rejectTarget.reg.id, reason: rejectTarget.reason || undefined });
    setRejectTarget(null);
  }

  return (
    <>
      <div className="rounded-xl border border-amber-200 bg-amber-50 shadow-sm">
        <div className="flex items-center gap-2 border-b border-amber-200 px-4 py-3">
          <UserPlus className="h-4 w-4 text-amber-700" />
          <h2 className="text-sm font-semibold text-amber-900">
            Join Requests ({registrations.length})
          </h2>
        </div>
        <ul className="divide-y divide-amber-100">
          {registrations.map((reg) => (
            <JoinRequestRow
              key={reg.id}
              reg={reg}
              onApprove={() => setApproveTarget(reg)}
              onReject={() => setRejectTarget({ reg, reason: '' })}
            />
          ))}
        </ul>
      </div>

      <ConfirmDialog
        open={!!approveTarget}
        title="Approve Join Request"
        description={`Approve ${approveTarget?.firstName} ${approveTarget?.lastName} (${approveTarget?.email})? This will create their player profile. You'll be taken to their profile to set handicap and flight assignment.`}
        confirmLabel="Approve"
        onConfirm={handleApprove}
        onCancel={() => setApproveTarget(null)}
      />

      {rejectTarget && (
        <RejectDialog
          reg={rejectTarget.reg}
          reason={rejectTarget.reason}
          onReasonChange={(r) => setRejectTarget({ ...rejectTarget, reason: r })}
          onConfirm={handleReject}
          onCancel={() => setRejectTarget(null)}
          isPending={reject.isPending}
        />
      )}
    </>
  );
}

function JoinRequestRow({
  reg,
  onApprove,
  onReject,
}: {
  reg: Registration;
  onApprove: () => void;
  onReject: () => void;
}) {
  const requested = new Date(reg.requestedAt).toLocaleDateString();

  return (
    <li className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <p className="text-sm font-medium text-gray-900">
          {reg.firstName} {reg.lastName}
        </p>
        <p className="text-xs text-gray-500">{reg.email}</p>
        {reg.phone && <p className="text-xs text-gray-400">{reg.phone}</p>}
        <p className="text-xs text-gray-400">Requested {requested}</p>
      </div>
      <div className="flex shrink-0 gap-2">
        <Button size="sm" variant="primary" onClick={onApprove}>
          Approve
        </Button>
        <Button size="sm" variant="ghost" className="text-red-600 hover:bg-red-50" onClick={onReject}>
          Decline
        </Button>
      </div>
    </li>
  );
}

function RejectDialog({
  reg,
  reason,
  onReasonChange,
  onConfirm,
  onCancel,
  isPending,
}: {
  reg: Registration;
  reason: string;
  onReasonChange: (r: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
  isPending: boolean;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-xl">
        <h2 className="text-base font-semibold text-gray-900">Decline Request</h2>
        <p className="mt-1 text-sm text-gray-500">
          Decline {reg.firstName} {reg.lastName}'s request to join? You can optionally provide a reason.
        </p>
        <textarea
          className="mt-4 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
          rows={3}
          placeholder="Optional reason..."
          value={reason}
          onChange={(e) => onReasonChange(e.target.value)}
        />
        <div className="mt-4 flex justify-end gap-3">
          <Button variant="ghost" onClick={onCancel} disabled={isPending}>
            Cancel
          </Button>
          <Button
            variant="primary"
            className="bg-red-600 hover:bg-red-700"
            onClick={onConfirm}
            disabled={isPending}
          >
            Decline
          </Button>
        </div>
      </div>
    </div>
  );
}
