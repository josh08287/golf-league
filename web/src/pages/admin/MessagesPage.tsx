import { useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { PageHeader } from '../../components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { usePlayers } from '../../hooks/usePlayers';
import { useSeasons } from '../../hooks/useSeasons';
import { useFlights } from '../../hooks/useFlights';
import { useSendBroadcast } from '../../hooks/admin/useSendBroadcast';
import type { Player } from '../../types/api';

// ── Recipient group selector ──────────────────────────────────────────────────

type RecipientGroup = 'all' | 'flight' | 'custom';

// ── Ad-hoc email tag input ────────────────────────────────────────────────────

function AdHocEmailInput({
  emails,
  onChange,
}: {
  emails: string[];
  onChange: (emails: string[]) => void;
}) {
  const [input, setInput] = useState('');

  function addEmail(raw: string) {
    const trimmed = raw.trim().toLowerCase();
    if (!trimmed) return;
    // Basic format check
    if (!trimmed.includes('@')) return;
    if (!emails.includes(trimmed)) onChange([...emails, trimmed]);
    setInput('');
  }

  function handleKey(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      addEmail(input);
    } else if (e.key === 'Backspace' && input === '' && emails.length > 0) {
      onChange(emails.slice(0, -1));
    }
  }

  function handleBlur() {
    addEmail(input);
  }

  return (
    <div className="flex flex-wrap gap-1.5 rounded-md border border-gray-300 px-2 py-1.5 focus-within:border-[#1B5E20] focus-within:ring-1 focus-within:ring-[#1B5E20] min-h-[38px]">
      {emails.map((email) => (
        <span
          key={email}
          className="inline-flex items-center gap-1 rounded bg-green-50 border border-green-200 px-2 py-0.5 text-xs text-green-800"
        >
          {email}
          <button
            type="button"
            onClick={() => onChange(emails.filter((e) => e !== email))}
            className="text-green-600 hover:text-green-900"
          >
            <X className="h-3 w-3" />
          </button>
        </span>
      ))}
      <input
        type="text"
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={handleKey}
        onBlur={handleBlur}
        placeholder={emails.length === 0 ? 'Type an email and press Enter...' : ''}
        className="flex-1 min-w-32 text-sm outline-none bg-transparent py-0.5"
      />
    </div>
  );
}

// ── Player checkbox list ──────────────────────────────────────────────────────

function PlayerCheckboxList({
  players,
  selected,
  onChange,
}: {
  players: Player[];
  selected: Set<number>;
  onChange: (selected: Set<number>) => void;
}) {
  function toggle(id: number) {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onChange(next);
  }

  function toggleAll() {
    if (selected.size === players.length) onChange(new Set());
    else onChange(new Set(players.map((p) => p.id)));
  }

  const allChecked = players.length > 0 && selected.size === players.length;
  const someChecked = selected.size > 0 && !allChecked;

  return (
    <div className="rounded-md border border-gray-200 overflow-hidden">
      <div className="flex items-center gap-2 px-3 py-2 bg-gray-50 border-b border-gray-200">
        <input
          type="checkbox"
          checked={allChecked}
          ref={(el) => { if (el) el.indeterminate = someChecked; }}
          onChange={toggleAll}
          className="h-4 w-4 rounded border-gray-300 text-[#1B5E20] cursor-pointer"
        />
        <span className="text-xs font-medium text-gray-600">
          {selected.size} of {players.length} selected
        </span>
      </div>
      <ul className="max-h-56 overflow-y-auto divide-y divide-gray-100">
        {players.map((p) => (
          <li key={p.id}>
            <label className="flex items-center gap-3 px-3 py-2 hover:bg-gray-50 cursor-pointer">
              <input
                type="checkbox"
                checked={selected.has(p.id)}
                onChange={() => toggle(p.id)}
                className="h-4 w-4 rounded border-gray-300 text-[#1B5E20] cursor-pointer"
              />
              <span className="text-sm text-gray-900">{p.fullName}</span>
              {!p.email && (
                <span className="ml-auto text-xs text-amber-600">no email</span>
              )}
              {p.email && (
                <span className="ml-auto text-xs text-gray-400 truncate max-w-40">{p.email}</span>
              )}
            </label>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export function MessagesPage() {
  const { data: playersPage, isLoading: playersLoading } = usePlayers(1, undefined, 500);
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);

  // Default to the first half of the active season.
  const defaultHalfId = activeSeason?.halves[0]?.id;
  const [selectedHalfId, setSelectedHalfId] = useState<number | undefined>(undefined);
  const halfId = selectedHalfId ?? defaultHalfId;

  const { data: flightsPage } = useFlights(halfId);
  const flights = flightsPage?.data ?? [];

  const allPlayers = useMemo(() => playersPage?.data ?? [], [playersPage]);

  // Players in the current half (have a flight membership for this half).
  const halfPlayers = useMemo(() => {
    if (!halfId) return allPlayers.filter((p) => p.isActive);
    return allPlayers.filter((p) =>
      p.isActive && p.flightMemberships.some((m) => m.halfId === halfId),
    );
  }, [allPlayers, halfId]);

  const [group, setGroup] = useState<RecipientGroup>('all');
  const [selectedFlightId, setSelectedFlightId] = useState<number | undefined>(undefined);
  const [customSelected, setCustomSelected] = useState<Set<number>>(() => new Set());
  const [adHocEmails, setAdHocEmails] = useState<string[]>([]);

  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  const send = useSendBroadcast();
  const [result, setResult] = useState<{ sent: number; skipped: number; skippedNames: string[] } | null>(null);

  // Players displayed in the flight selector.
  const flightPlayers = useMemo(() => {
    if (!selectedFlightId) return [];
    return halfPlayers.filter((p) =>
      p.flightMemberships.some(
        (m) => m.flightId === selectedFlightId && m.halfId === halfId,
      ),
    );
  }, [halfPlayers, selectedFlightId, halfId]);

  // Resolved player IDs to send to (null = send to all active half players).
  const resolvedPlayerIds = useMemo((): number[] | null => {
    if (group === 'all') return null;
    if (group === 'flight') return flightPlayers.map((p) => p.id);
    return Array.from(customSelected);
  }, [group, flightPlayers, customSelected]);

  // Recipient preview label.
  const recipientLabel = useMemo(() => {
    if (group === 'all') {
      const withEmail = halfPlayers.filter((p) => p.email).length;
      return `All active players in current half (${withEmail} with email)`;
    }
    if (group === 'flight') {
      if (!selectedFlightId) return 'Select a flight';
      const withEmail = flightPlayers.filter((p) => p.email).length;
      return `${flights.find((f) => f.id === selectedFlightId)?.name ?? 'Flight'} — ${withEmail} players with email`;
    }
    const withEmail = Array.from(customSelected).filter((id) => {
      const p = allPlayers.find((p) => p.id === id);
      return p?.email != null;
    }).length;
    return `${customSelected.size} selected (${withEmail} with email)`;
  }, [group, halfPlayers, flightPlayers, selectedFlightId, flights, customSelected, allPlayers]);

  const totalRecipientCount =
    (resolvedPlayerIds === null
      ? halfPlayers.filter((p) => p.email).length
      : resolvedPlayerIds.filter((id) => allPlayers.find((p) => p.id === id)?.email).length) +
    adHocEmails.length;

  async function handleSend() {
    if (!subject.trim() || !body.trim()) return;
    setResult(null);
    try {
      const res = await send.mutateAsync({
        subject: subject.trim(),
        body: body.trim(),
        playerIds: resolvedPlayerIds,
        adHocEmails: adHocEmails.length > 0 ? adHocEmails : null,
      });
      setResult(res);
    } catch {
      // error shown via send.isError
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Send Message" subtitle="Email players in your league" />

      {playersLoading && (
        <div className="flex justify-center py-8">
          <Spinner />
        </div>
      )}

      {!playersLoading && (
        <div className="grid gap-6 lg:grid-cols-2">
          {/* Left: Recipients */}
          <div className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Recipients</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Half selector */}
                {activeSeason && activeSeason.halves.length > 1 && (
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Half</label>
                    <select
                      value={halfId ?? ''}
                      onChange={(e) => setSelectedHalfId(Number(e.target.value) || undefined)}
                      className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
                    >
                      {activeSeason.halves.map((h) => (
                        <option key={h.id} value={h.id}>{h.name}</option>
                      ))}
                    </select>
                  </div>
                )}

                {/* Group tabs */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Group</label>
                  <div className="flex rounded-md border border-gray-200 overflow-hidden">
                    {([
                      ['all', 'All active'],
                      ['flight', 'By flight'],
                      ['custom', 'Custom'],
                    ] as [RecipientGroup, string][]).map(([val, label]) => (
                      <button
                        key={val}
                        type="button"
                        onClick={() => setGroup(val)}
                        className={[
                          'flex-1 px-3 py-1.5 text-sm font-medium border-r last:border-r-0 border-gray-200 transition-colors',
                          group === val
                            ? 'bg-[#1B5E20] text-white'
                            : 'bg-white text-gray-700 hover:bg-gray-50',
                        ].join(' ')}
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Flight selector */}
                {group === 'flight' && (
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Flight</label>
                    {flights.length === 0 ? (
                      <p className="text-sm text-gray-400">No flights for this half.</p>
                    ) : (
                      <select
                        value={selectedFlightId ?? ''}
                        onChange={(e) => setSelectedFlightId(Number(e.target.value) || undefined)}
                        className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
                      >
                        <option value="">Select a flight...</option>
                        {flights.map((f) => (
                          <option key={f.id} value={f.id}>{f.name}</option>
                        ))}
                      </select>
                    )}
                  </div>
                )}

                {/* Custom player picker */}
                {group === 'custom' && (
                  <PlayerCheckboxList
                    players={halfPlayers}
                    selected={customSelected}
                    onChange={setCustomSelected}
                  />
                )}

                {/* Recipient summary */}
                <p className="text-xs text-gray-500">{recipientLabel}</p>
              </CardContent>
            </Card>

            {/* Ad-hoc emails */}
            <Card>
              <CardHeader>
                <CardTitle>Additional recipients</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="text-xs text-gray-500">
                  Add extra email addresses not in the player list. Press Enter or comma to add each one.
                </p>
                <AdHocEmailInput emails={adHocEmails} onChange={setAdHocEmails} />
              </CardContent>
            </Card>
          </div>

          {/* Right: Message */}
          <div className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Message</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Subject</label>
                  <input
                    type="text"
                    value={subject}
                    onChange={(e) => setSubject(e.target.value)}
                    placeholder="e.g. Important update for this week"
                    className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Body</label>
                  <textarea
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                    rows={10}
                    placeholder="Write your message here..."
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] resize-y"
                  />
                </div>

                <Button
                  onClick={() => void handleSend()}
                  disabled={
                    send.isPending ||
                    !subject.trim() ||
                    !body.trim() ||
                    totalRecipientCount === 0
                  }
                  className="w-full"
                >
                  {send.isPending ? (
                    <span className="flex items-center justify-center gap-2">
                      <Spinner className="h-4 w-4" /> Sending…
                    </span>
                  ) : (
                    `Send to ${totalRecipientCount} recipient${totalRecipientCount === 1 ? '' : 's'}`
                  )}
                </Button>

                {send.isError && (
                  <ErrorMessage message="Failed to send messages. Please try again." />
                )}

                {result && (
                  <div className="rounded-md border border-green-200 bg-green-50 px-4 py-3 space-y-1">
                    <p className="text-sm font-medium text-green-800">
                      ✓ Sent to {result.sent} recipient{result.sent === 1 ? '' : 's'}
                    </p>
                    {result.skipped > 0 && (
                      <p className="text-sm text-amber-700">
                        {result.skipped} failed: {result.skippedNames.join(', ')}
                      </p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </div>
  );
}
