import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Clock, ArrowLeft, LogIn, LogOut, RefreshCw, SkipForward } from 'lucide-react';
import { useAuthStore } from '@/store/authStore';
import {
  useNextRoundTeeTimes,
  useRoundTeeTimes,
  useJoinTeeTime,
  useLeaveTeeTime,
  useSkipMyWeek,
  useSetTeeTimePreference,
} from '@/hooks/useTeeTimes';
import { useRound } from '@/hooks/useRounds';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardContent } from '@/components/ui/Card';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { formatShortDate } from '@/lib/utils';
import type { RoundTeeTimeSchedule, TeeTimeSlot, TeeTimeSlotName } from '@/types/api';
import { TEE_TIME_SLOTS, TEE_TIME_SLOT_FLAG, FEATURE_FLAG_KEYS } from '@/types/api';

// ── Countdown helper ──────────────────────────────────────────────────────────

function useCutoffCountdown(cutoffUtc: string | undefined): string {
  const [label, setLabel] = useState('');

  useEffect(() => {
    if (!cutoffUtc) return;
    function tick() {
      const diff = new Date(cutoffUtc!).getTime() - Date.now();
      if (diff <= 0) {
        setLabel('Auto-fill has run');
        return;
      }
      const totalSeconds = Math.floor(diff / 1000);
      const days = Math.floor(totalSeconds / 86400);
      const hours = Math.floor((totalSeconds % 86400) / 3600);
      const minutes = Math.floor((totalSeconds % 3600) / 60);
      const parts: string[] = [];
      if (days > 0) parts.push(`${days}d`);
      if (hours > 0) parts.push(`${hours}h`);
      parts.push(`${minutes}m`);
      setLabel(`Auto-fill in ${parts.join(' ')}`);
    }
    tick();
    const id = setInterval(tick, 30_000);
    return () => clearInterval(id);
  }, [cutoffUtc]);

  return label;
}

// ── Slot card ─────────────────────────────────────────────────────────────────

const CAPACITY = 4;

interface SlotCardProps {
  slot: TeeTimeSlot;
  schedule: RoundTeeTimeSchedule;
  isParticipant: boolean;
  roundDaySwitchEnabled: boolean;
  onJoin: (teeTimeId: number) => void;
  onSwitch: (teeTimeId: number) => void;
  onLeave: () => void;
  mutating: boolean;
}

function SlotCard({
  slot,
  schedule,
  isParticipant,
  roundDaySwitchEnabled,
  onJoin,
  onSwitch,
  onLeave,
  mutating,
}: SlotCardProps) {
  const isMine = schedule.currentUserTeeTimeId === slot.id;
  const isFull = slot.players.length >= CAPACITY;
  const canJoin = isParticipant && !schedule.isLocked && !isMine && !isFull;
  const canLeave = isParticipant && !schedule.isLocked && isMine;
  const canSwitch =
    roundDaySwitchEnabled &&
    isParticipant &&
    schedule.isLocked &&
    schedule.isRoundDay &&
    schedule.currentUserTeeTimeId != null &&
    !isMine &&
    !isFull;
  const isEmpty = slot.players.length === 0;

  return (
    <Card className={isMine ? 'border-[#1B5E20] ring-1 ring-[#1B5E20]' : ''}>
      <CardContent className="p-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-gray-400" />
            <span className="font-semibold text-gray-900">{slot.scheduledTime}</span>
            {isMine && (
              <Badge variant="green">Your slot</Badge>
            )}
            {slot.autoFilled && (
              <Badge variant="neutral" className="text-[10px]">
                <RefreshCw className="h-3 w-3 mr-1 inline" />
                Auto-filled
              </Badge>
            )}
          </div>
          <span className="text-xs text-gray-400">
            {slot.players.length}/{CAPACITY}
          </span>
        </div>

        <ul className="space-y-1 min-h-[5rem]">
          {slot.players.map((p) => (
            <li key={p.participantId} className="flex items-center gap-2 text-sm">
              <span
                className={
                  p.playerId === (schedule.currentUserParticipantId ?? -1)
                    ? 'font-semibold text-[#1B5E20]'
                    : 'text-gray-800'
                }
              >
                {p.playerName}
              </span>
              <span className="text-gray-400 text-xs">{p.flightName}</span>
            </li>
          ))}
          {isEmpty && (
            <li className="text-sm text-gray-400 italic">No players yet</li>
          )}
        </ul>

        {(canJoin || canLeave || canSwitch) && (
          <div className="mt-3 pt-3 border-t border-gray-100">
            {canJoin && (
              <Button
                size="sm"
                className="w-full"
                disabled={mutating}
                onClick={() => onJoin(slot.id)}
              >
                <LogIn className="h-4 w-4 mr-1" />
                {isFull ? 'Full' : 'Join this slot'}
              </Button>
            )}
            {canLeave && (
              <Button
                size="sm"
                variant="outline"
                className="w-full text-red-600 border-red-200 hover:bg-red-50"
                disabled={mutating}
                onClick={onLeave}
              >
                <LogOut className="h-4 w-4 mr-1" />
                Leave slot
              </Button>
            )}
            {canSwitch && (
              <Button
                size="sm"
                className="w-full"
                disabled={mutating}
                onClick={() => onSwitch(slot.id)}
              >
                <LogIn className="h-4 w-4 mr-1" />
                Move to this group
              </Button>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ── Preference selector ───────────────────────────────────────────────────────

interface PreferenceSelectorProps {
  playerId: number;
  currentMask: number;
}

function TeeTimePreferenceSelector({ playerId, currentMask }: PreferenceSelectorProps) {
  const setPreference = useSetTeeTimePreference();

  const [selected, setSelected] = useState<Set<TeeTimeSlotName>>(
    () => new Set(TEE_TIME_SLOTS.filter((s) => (currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0)),
  );

  function toggle(slot: TeeTimeSlotName) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(slot)) next.delete(slot); else next.add(slot);
      return next;
    });
  }

  function save() {
    setPreference.mutate({ playerId, preferredSlots: [...selected] });
  }

  const isDirty = TEE_TIME_SLOTS.some(
    (s) => selected.has(s) !== ((currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0),
  );

  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 space-y-3">
      <p className="text-sm font-medium text-gray-700">
        Preferred tee-time slots for auto-fill
        <span className="ml-1 text-xs font-normal text-gray-400">(select none, any, or all)</span>
      </p>
      <div className="flex flex-wrap gap-2">
        {TEE_TIME_SLOTS.map((slot) => {
          const active = selected.has(slot);
          return (
            <button
              key={slot}
              type="button"
              onClick={() => toggle(slot)}
              className={[
                'px-3 py-1 rounded-full text-sm font-medium border transition-colors',
                active
                  ? 'bg-[#1B5E20] text-white border-[#1B5E20]'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-[#1B5E20]',
              ].join(' ')}
            >
              {slot}
            </button>
          );
        })}
      </div>
      {isDirty && (
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            onClick={save}
            disabled={setPreference.isPending}
          >
            Save preference
          </Button>
          {setPreference.isError && (
            <span className="text-xs text-red-600">Failed to save.</span>
          )}
          {setPreference.isSuccess && !setPreference.isPending && (
            <span className="text-xs text-green-700">Saved!</span>
          )}
        </div>
      )}
    </div>
  );
}

// ── Inner view (shared between next-round and specific-round routes) ──────────

interface TeeTimeViewProps {
  schedule: RoundTeeTimeSchedule;
  roundCourseName?: string;
  roundDate?: string;
}

function TeeTimeView({ schedule, roundCourseName, roundDate }: TeeTimeViewProps) {
  const user = useAuthStore((s) => s.user);
  const countdown = useCutoffCountdown(schedule.cutoffUtc);
  const join = useJoinTeeTime();
  const leave = useLeaveTeeTime();
  const skipMyWeek = useSkipMyWeek();
  const mutating = join.isPending || leave.isPending || skipMyWeek.isPending;
  const featureFlags = useFeatureFlagStates();
  const roundDaySwitchEnabled =
    featureFlags.data?.[FEATURE_FLAG_KEYS.roundDayTeeTimeSwitchEnabled] ?? false;

  const isParticipant = schedule.currentUserParticipantId != null;
  const hasPlayerId = user?.playerId != null;
  const isSkipped = schedule.currentUserSkippedWeek;

  const [actionError, setActionError] = useState<string | null>(null);

  function handleJoin(teeTimeId: number) {
    setActionError(null);
    join.mutate(
      { roundId: schedule.roundId, teeTimeId },
      {
        onError: (err: unknown) => {
          const msg =
            (err as { response?: { data?: { error?: string } } })?.response?.data?.error
            ?? 'Could not join that slot.';
          setActionError(msg);
        },
      },
    );
  }

  function handleSwitch(teeTimeId: number) {
    const proceed = window.confirm(
      "Move to this group? If you've already entered scores today, they'll move with you — " +
      "your current group's scorecard will no longer include you, and this group's scorecard will.",
    );
    if (!proceed) return;
    handleJoin(teeTimeId);
  }

  function handleLeave() {
    setActionError(null);
    leave.mutate(schedule.roundId, {
      onError: (err: unknown) => {
        const msg =
          (err as { response?: { data?: { error?: string } } })?.response?.data?.error
          ?? 'Could not leave slot.';
        setActionError(msg);
      },
    });
  }

  function handleSkipToggle() {
    setActionError(null);
    skipMyWeek.mutate(
      { roundId: schedule.roundId, skipped: !isSkipped },
      {
        onError: (err: unknown) => {
          const msg =
            (err as { response?: { data?: { error?: string } } })?.response?.data?.error
            ?? 'Could not update skip status.';
          setActionError(msg);
        },
      },
    );
  }

  const title = roundCourseName ?? schedule.courseName ?? 'Tee Times';
  const subtitle = useMemo(() => {
    const parts: string[] = [];
    if (schedule.weekNumber) parts.push(`Week ${schedule.weekNumber}`);
    const date = roundDate ?? schedule.roundDate;
    if (date) parts.push(formatShortDate(date));
    parts.push(`${schedule.participantCount} players`);
    return parts.join(' · ');
  }, [roundDate, schedule.roundDate, schedule.weekNumber, schedule.participantCount]);

  return (
    <div className="space-y-6">
      <PageHeader title={title} description={subtitle}>
        {schedule.isLocked ? (
          <Badge variant="amber">Sign-ups closed</Badge>
        ) : (
          <Badge variant="blue">{countdown}</Badge>
        )}
      </PageHeader>

      {!user && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <Link to="/login" className="font-medium underline">Sign in</Link> to join a tee time.
        </div>
      )}

      {user && !hasPlayerId && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Your account isn&apos;t linked to a player profile yet. Contact an admin.
        </div>
      )}

      {user && hasPlayerId && !isParticipant && (
        <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-800">
          You&apos;re not registered as a participant in this round — viewing only.
        </div>
      )}

      {actionError && (
        <ErrorMessage message={actionError} />
      )}

      {isParticipant && isSkipped && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
          <div className="flex items-center justify-between gap-4">
            <p className="text-sm text-amber-800">
              You&apos;ve marked yourself as skipping this round. You won&apos;t be assigned to a tee time.
            </p>
            {!schedule.isLocked && (
              <Button
                size="sm"
                variant="outline"
                disabled={mutating}
                onClick={handleSkipToggle}
                className="shrink-0"
              >
                {skipMyWeek.isPending ? 'Updating…' : "I'm playing"}
              </Button>
            )}
          </div>
        </div>
      )}

      {isParticipant && !isSkipped && !schedule.isLocked && (
        <div className="flex justify-end">
          <Button
            size="sm"
            variant="outline"
            disabled={mutating}
            onClick={handleSkipToggle}
            className="text-amber-700 border-amber-300 hover:bg-amber-50"
          >
            <SkipForward className="h-4 w-4 mr-1" />
            {skipMyWeek.isPending ? 'Updating…' : 'Skip this week'}
          </Button>
        </div>
      )}

      {schedule.isLocked && isParticipant && schedule.currentUserTeeTimeId == null && !isSkipped && (
        <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 text-sm text-gray-600">
          Sign-ups are closed. You weren&apos;t assigned to a slot — you may have been placed by auto-fill or
          weren&apos;t registered in time.
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {schedule.slots.map((slot) => (
          <SlotCard
            key={slot.id}
            slot={slot}
            schedule={schedule}
            isParticipant={isParticipant}
            roundDaySwitchEnabled={roundDaySwitchEnabled}
            onJoin={handleJoin}
            onSwitch={handleSwitch}
            onLeave={handleLeave}
            mutating={mutating}
          />
        ))}
        {schedule.slots.length === 0 && (
          <p className="col-span-full text-sm text-gray-500">
            No tee-time slots have been generated yet.
          </p>
        )}
      </div>

      {user && hasPlayerId && (
        <TeeTimePreferenceSelector
          playerId={parseInt(user.playerId!, 10)}
          currentMask={schedule.currentUserPreferredSlots ?? 0}
        />
      )}
    </div>
  );
}

// ── Next round page ───────────────────────────────────────────────────────────

export function TeeTimesNextPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const { data, isPending, isError, error } = useNextRoundTeeTimes();

  useEffect(() => {
    if (!user) navigate('/login', { replace: true });
  }, [user, navigate]);

  if (!user) return null;

  if (isPending) return <FullPageSpinner />;

  if (isError) {
    const msg =
      (error as { response?: { data?: { error?: string } } })?.response?.data?.error;
    if (msg?.toLowerCase().includes('no upcoming')) {
      return (
        <div className="space-y-6">
          <PageHeader title="Tee Times" description="No upcoming rounds scheduled." />
          <p className="text-sm text-gray-500">Check back once the next round is set up.</p>
        </div>
      );
    }
    return <ErrorMessage message="Could not load tee times." />;
  }

  if (!data) return null;

  return (
    <div className="space-y-4">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to={`/rounds/${data.roundId}/tee-times`}>
          View as specific round →
        </Link>
      </Button>
      <TeeTimeView schedule={data} />
    </div>
  );
}

// ── Specific round page ───────────────────────────────────────────────────────

export function TeeTimesRoundPage() {
  const { roundId } = useParams<{ roundId: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const parsedRoundId = roundId ? parseInt(roundId, 10) : null;

  const round = useRound(roundId ?? '');
  const teeTimes = useRoundTeeTimes(parsedRoundId);

  useEffect(() => {
    if (!user) navigate('/login', { replace: true });
  }, [user, navigate]);

  if (!user) return null;

  const isPending = round.isPending || teeTimes.isPending;
  const isError = round.isError || teeTimes.isError;

  if (isPending) return <FullPageSpinner />;
  if (isError) return <ErrorMessage message="Could not load tee-time schedule." />;
  if (!teeTimes.data) return null;

  return (
    <div className="space-y-4">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to={`/rounds/${roundId}`}>
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back to round
        </Link>
      </Button>
      <TeeTimeView
        schedule={teeTimes.data}
        roundCourseName={round.data?.courseName}
        roundDate={round.data?.scheduledDate}
      />
    </div>
  );
}
