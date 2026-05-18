import { useState, useRef } from 'react';
import { Link2, Send, Copy, Check, UserX, Trash2 } from 'lucide-react';
import { useInvites, useCreateInvites, useRevokeInvite, useDeleteInvite } from '@/hooks/admin/useInvites';
import { useUnlinkedPlayers } from '@/hooks/usePlayers';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { ConfirmDialog } from '@/components/admin/ConfirmDialog';
import type { Invite } from '@/types/api';

// ── Invite form ────────────────────────────────────────────────────────────

function InviteForm({ onDone }: { onDone: () => void }) {
  const [mode, setMode] = useState<'single' | 'bulk'>('single');
  const [singleEmail, setSingleEmail] = useState('');
  const [bulkText, setBulkText] = useState('');
  const [expiryDays, setExpiryDays] = useState(7);
  const [role, setRole] = useState<'player' | 'scorer' | 'admin'>('player');
  const [preLinkedPlayerId, setPreLinkedPlayerId] = useState<string>('');
  const [result, setResult] = useState<{ created: number; skipped: string[] } | null>(null);
  const create = useCreateInvites();

  // Pre-attach only makes sense in single-email mode (one Player can only
  // map to one AppUser). Hook is enabled only when we'll actually show the
  // dropdown so the request doesn't fire for nothing in bulk mode.
  const { data: unlinkedPlayers = [] } = useUnlinkedPlayers(mode === 'single');

  const selectedPlayer = unlinkedPlayers.find((p) => String(p.id) === preLinkedPlayerId) ?? null;
  // When a player is selected, the invite email must match their profile email.
  const effectiveEmail = selectedPlayer?.email ?? singleEmail;

  function handlePlayerSelect(id: string) {
    setPreLinkedPlayerId(id);
    const player = unlinkedPlayers.find((p) => String(p.id) === id);
    if (player?.email) setSingleEmail(player.email);
    else if (id) setSingleEmail(''); // player has no email — admin must supply one
  }

  function parseEmails(raw: string): string[] {
    return raw
      .split(/[\n,;]+/)
      .map((e) => e.trim())
      .filter((e) => e.includes('@'));
  }

  async function handleSend() {
    const emails = mode === 'single' ? [effectiveEmail.trim()] : parseEmails(bulkText);
    if (emails.length === 0 || emails.some((e) => !e.includes('@'))) return;

    const data = await create.mutateAsync({
      emails,
      expiryDays,
      role,
      preLinkedPlayerId:
        mode === 'single' && preLinkedPlayerId ? Number(preLinkedPlayerId) : null,
    });
    setResult({ created: data.created.length, skipped: data.skipped });
  }

  if (result) {
    return (
      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm space-y-3">
        <p className="font-medium text-gray-900">
          {result.created} invite{result.created !== 1 ? 's' : ''} sent.
        </p>
        {result.skipped.length > 0 && (
          <div className="rounded-lg bg-amber-50 border border-amber-200 p-3 text-sm text-amber-800">
            <strong>Skipped</strong> (already invited or existing players):
            <ul className="mt-1 list-disc pl-4">
              {result.skipped.map((e) => <li key={e}>{e}</li>)}
            </ul>
          </div>
        )}
        <Button variant="primary" onClick={() => { setResult(null); setSingleEmail(''); setBulkText(''); onDone(); }}>
          Done
        </Button>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm space-y-4">
      <div className="flex gap-2">
        <button
          onClick={() => setMode('single')}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
            mode === 'single'
              ? 'bg-[#1B5E20] text-white'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
          }`}
        >
          Single
        </button>
        <button
          onClick={() => setMode('bulk')}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
            mode === 'bulk'
              ? 'bg-[#1B5E20] text-white'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
          }`}
        >
          Bulk
        </button>
      </div>

      {mode === 'single' ? (
        <>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Attach to existing player <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <select
              value={preLinkedPlayerId}
              onChange={(e) => handlePlayerSelect(e.target.value)}
              disabled={unlinkedPlayers.length === 0}
              className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:bg-gray-50 disabled:text-gray-400"
            >
              <option value="">
                {unlinkedPlayers.length === 0
                  ? ‘— No unlinked players available —‘
                  : ‘— Don\’t attach (create new) —‘}
              </option>
              {unlinkedPlayers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName}{p.email ? ` — ${p.email}` : ‘’}
                </option>
              ))}
            </select>
            <p className="mt-1 text-xs text-gray-400">
              {unlinkedPlayers.length === 0
                ? ‘No active players without a user account. Create a player first, or leave this blank to create a fresh roster row on sign-up.’
                : ‘When the invitee signs up, their new account will be linked to this player instead of creating a fresh roster row.’}
            </p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email address</label>
            <input
              type="email"
              value={effectiveEmail}
              onChange={(e) => { if (!selectedPlayer?.email) setSingleEmail(e.target.value); }}
              readOnly={!!selectedPlayer?.email}
              placeholder="player@example.com"
              className={`block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] ${selectedPlayer?.email ? ‘bg-gray-50 text-gray-500’ : ‘’}`}
            />
            {selectedPlayer && selectedPlayer.email && (
              <p className="mt-1 text-xs text-gray-400">Email is set from the selected player profile.</p>
            )}
            {selectedPlayer && !selectedPlayer.email && (
              <p className="mt-1 text-xs text-amber-600">This player has no email on file — enter the email address to send the invite to.</p>
            )}
          </div>
        </>
      ) : (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Email addresses <span className="text-gray-400 font-normal">(one per line, or comma/semicolon separated)</span>
          </label>
          <textarea
            value={bulkText}
            onChange={(e) => setBulkText(e.target.value)}
            rows={6}
            placeholder={`alice@example.com\nbob@example.com\ncharlie@example.com`}
            className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] font-mono"
          />
          <p className="mt-1 text-xs text-gray-400">
            {parseEmails(bulkText).length} valid address{parseEmails(bulkText).length !== 1 ? 'es' : ''} detected
          </p>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <label className="text-sm font-medium text-gray-700 shrink-0">Role</label>
        <select
          value={role}
          onChange={(e) => setRole(e.target.value as 'player' | 'scorer' | 'admin')}
          className="rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none"
        >
          <option value="player">Player</option>
          <option value="scorer">Scorer</option>
          <option value="admin">Admin</option>
        </select>
        <label className="text-sm font-medium text-gray-700 shrink-0">Expires after</label>
        <select
          value={expiryDays}
          onChange={(e) => setExpiryDays(Number(e.target.value))}
          className="rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none"
        >
          <option value={3}>3 days</option>
          <option value={7}>7 days</option>
          <option value={14}>14 days</option>
          <option value={30}>30 days</option>
        </select>
      </div>

      {create.isError && (
        <p className="text-sm text-red-600">Failed to send invites. Please try again.</p>
      )}

      <Button
        variant="primary"
        onClick={() => void handleSend()}
        disabled={create.isPending || (mode === 'single' ? !effectiveEmail.includes('@') : parseEmails(bulkText).length === 0)}
      >
        <Send className="mr-1.5 h-4 w-4" />
        {create.isPending ? 'Sending…' : mode === 'bulk' ? `Send ${parseEmails(bulkText).length || ''} Invite${parseEmails(bulkText).length !== 1 ? 's' : ''}`.trim() : 'Send Invite'}
      </Button>
    </div>
  );
}

// ── Copy-link button ───────────────────────────────────────────────────────

function CopyLinkButton({ link }: { link: string }) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    await navigator.clipboard.writeText(link);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <button
      onClick={() => void handleCopy()}
      className="flex items-center gap-1 rounded px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 transition-colors"
      title={link}
    >
      {copied ? <Check className="h-3.5 w-3.5 text-green-600" /> : <Copy className="h-3.5 w-3.5" />}
      {copied ? 'Copied' : 'Copy link'}
    </button>
  );
}

// ── Main page ──────────────────────────────────────────────────────────────

export function InvitesPage() {
  const { data: invites = [], isLoading, error } = useInvites();
  const revoke = useRevokeInvite();
  const deleteInvite = useDeleteInvite();
  const [revokeTarget, setRevokeTarget] = useState<Invite | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Invite | null>(null);
  const [showForm, setShowForm] = useState(false);
  const formRef = useRef<HTMLDivElement>(null);

  const pending = invites.filter((i) => i.status === 'Pending' && new Date(i.expiresAt) >= new Date());
  const expired = invites.filter((i) => i.status === 'Pending' && new Date(i.expiresAt) < new Date());
  const others = invites.filter((i) => i.status !== 'Pending');

  async function handleRevoke() {
    if (!revokeTarget) return;
    await revoke.mutateAsync(revokeTarget.id);
    setRevokeTarget(null);
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    await deleteInvite.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  }

  if (isLoading) return <div className="flex h-64 items-center justify-center"><Spinner /></div>;
  if (error) return <ErrorMessage message="Failed to load invites." />;

  return (
    <div className="space-y-6">
      <PageHeader title="Invites">
        <Button variant="primary" onClick={() => { setShowForm((v) => !v); }}>
          <Link2 className="mr-1 h-4 w-4" />
          {showForm ? 'Hide Form' : 'Send Invite'}
        </Button>
      </PageHeader>

      {showForm && (
        <div ref={formRef}>
          <InviteForm onDone={() => setShowForm(false)} />
        </div>
      )}

      {/* Pending invites */}
      {pending.length > 0 && (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-gray-500 uppercase tracking-wide">Pending ({pending.length})</h2>
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm divide-y divide-gray-100">
            {pending.map((invite) => (
              <InviteRow key={invite.id} invite={invite} onRevoke={() => setRevokeTarget(invite)} />
            ))}
          </div>
        </section>
      )}

      {/* Expired invites */}
      {expired.length > 0 && (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-gray-500 uppercase tracking-wide">Expired ({expired.length})</h2>
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm divide-y divide-gray-100">
            {expired.map((invite) => (
              <InviteRow key={invite.id} invite={invite} onDelete={() => setDeleteTarget(invite)} />
            ))}
          </div>
        </section>
      )}

      {pending.length === 0 && expired.length === 0 && others.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 bg-white p-12 text-center text-gray-400 text-sm">
          No invites yet. Click "Send Invite" to get started.
        </div>
      )}

      {/* Accepted / Revoked history */}
      {others.length > 0 && (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-gray-500 uppercase tracking-wide">History</h2>
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm divide-y divide-gray-100">
            {others.map((invite) => (
              <InviteRow key={invite.id} invite={invite} onDelete={() => setDeleteTarget(invite)} />
            ))}
          </div>
        </section>
      )}

      <ConfirmDialog
        open={!!revokeTarget}
        title="Revoke Invite"
        description={`Revoke the invite sent to ${revokeTarget?.email}? They will no longer be able to use this link.`}
        confirmLabel="Revoke"
        destructive
        onConfirm={() => void handleRevoke()}
        onCancel={() => setRevokeTarget(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Invite"
        description={`Delete the invite record for ${deleteTarget?.email}? This will allow them to be invited again.`}
        confirmLabel="Delete"
        destructive
        onConfirm={() => void handleDelete()}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}

function InviteRow({ invite, onRevoke, onDelete }: { invite: Invite; onRevoke?: () => void; onDelete?: () => void }) {
  const created = new Date(invite.createdAt).toLocaleDateString();
  const expires = new Date(invite.expiresAt).toLocaleDateString();
  const isExpired = invite.status === 'Pending' && new Date(invite.expiresAt) < new Date();

  return (
    <div className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-900 truncate">{invite.email}</p>
        <p className="text-xs text-gray-400">
          Sent {created}
          {invite.status === 'Pending' && (
            <span className={isExpired ? 'text-red-500' : ''}>
              {isExpired ? ' · Expired' : ` · Expires ${expires}`}
            </span>
          )}
          {invite.status === 'Accepted' && invite.acceptedAt && (
            <span> · Accepted {new Date(invite.acceptedAt).toLocaleDateString()}</span>
          )}
        </p>
        {invite.status === 'Pending' && !isExpired && (
          <CopyLinkButton link={invite.inviteLink} />
        )}
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-3">
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700">
          {invite.role}
        </span>
        <InviteStatusBadge status={invite.status} expired={isExpired} />
        {invite.status === 'Pending' && !isExpired && onRevoke && (
          <Button size="sm" variant="ghost" className="text-red-600 hover:bg-red-50" onClick={onRevoke}>
            <UserX className="mr-1 h-3.5 w-3.5" />
            Revoke
          </Button>
        )}
        {(isExpired || invite.status !== 'Pending') && onDelete && (
          <Button size="sm" variant="ghost" className="text-red-600 hover:bg-red-50" onClick={onDelete}>
            <Trash2 className="mr-1 h-3.5 w-3.5" />
            Delete
          </Button>
        )}
      </div>
    </div>
  );
}

function InviteStatusBadge({ status, expired }: { status: string; expired: boolean }) {
  if (status === 'Pending' && expired) return <Badge variant="neutral">Expired</Badge>;
  if (status === 'Pending') return <Badge variant="warning">Pending</Badge>;
  if (status === 'Accepted') return <Badge variant="success">Accepted</Badge>;
  return <Badge variant="neutral">Revoked</Badge>;
}
