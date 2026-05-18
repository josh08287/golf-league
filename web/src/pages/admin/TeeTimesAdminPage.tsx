import { useState, useMemo, useCallback } from 'react';
import { useRounds } from '@/hooks/useRounds';
import { useRoundTeeTimes } from '@/hooks/useTeeTimes';
import {
  useAdminRoundParticipants,
  useAdminMoveParticipantToTeeTime,
  useAdminRemoveParticipantFromTeeTime,
  type AdminParticipant,
} from '@/hooks/admin/useTeeTimeMutations';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card } from '@/components/ui/Card';
import { Spinner } from '@/components/ui/Spinner';
import { Badge } from '@/components/ui/Badge';
import { Clock, Users, GripVertical, X, Calendar, ArrowRightLeft } from 'lucide-react';
import type { TeeTimeSlot, TeeTimeParticipant } from '@/types/api';

const CAPACITY = 4;

interface DraggablePlayerProps {
  participant: AdminParticipant;
  currentTeeTimeId: number | null;
  onRemove?: () => void;
  isDragging?: boolean;
}

function DraggablePlayer({ participant, currentTeeTimeId, onRemove, isDragging }: DraggablePlayerProps) {
  const [dragging, setDragging] = useState(false);

  const handleDragStart = (e: React.DragEvent) => {
    setDragging(true);
    e.dataTransfer.setData('application/json', JSON.stringify({
      participantId: participant.id,
      playerId: participant.playerId,
      fullName: participant.fullName,
      sourceTeeTimeId: currentTeeTimeId,
    }));
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragEnd = () => {
    setDragging(false);
  };

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      className={[
        'flex items-center gap-2 px-3 py-2 rounded-lg border cursor-move transition-all',
        'bg-white border-gray-200 hover:border-[#1B5E20] hover:shadow-sm',
        (dragging || isDragging) && 'opacity-50 border-[#1B5E20]',
      ].filter(Boolean).join(' ')}
    >
      <GripVertical className="h-4 w-4 text-gray-400 flex-shrink-0" />
      <div className="flex-1 min-w-0">
        <div className="font-medium text-sm text-gray-900 truncate">
          {participant.fullName}
        </div>
        <div className="text-xs text-gray-500">
          {participant.name ?? 'No flight'} · HCP {participant.handicapIndex?.toFixed(1) ?? '—'}
        </div>
      </div>
      {onRemove && (
        <button
          onClick={onRemove}
          className="p-1 rounded hover:bg-red-50 text-gray-400 hover:text-red-600 transition-colors"
          title="Remove from tee time"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}

interface TeeTimeSlotCardProps {
  slot: TeeTimeSlot;
  roundId: number;
  onPlayerMoved: () => void;
}

function TeeTimeSlotCard({ slot, roundId, onPlayerMoved }: TeeTimeSlotCardProps) {
  const [isDragOver, setIsDragOver] = useState(false);
  const moveParticipant = useAdminMoveParticipantToTeeTime(roundId);
  const removeParticipant = useAdminRemoveParticipantFromTeeTime(roundId);

  const slotPlayers = useMemo(() => {
    const players: Array<TeeTimeParticipant & { participantId: number }> = slot.players.map(p => ({
      ...p,
      participantId: p.participantId,
    }));
    return players;
  }, [slot.players]);

  const isFull = slotPlayers.length >= CAPACITY;

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    if (!isFull) {
      e.dataTransfer.dropEffect = 'move';
      setIsDragOver(true);
    }
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);

    const data = JSON.parse(e.dataTransfer.getData('application/json'));
    const { participantId, sourceTeeTimeId } = data;

    if (sourceTeeTimeId === slot.id) return; // Dropped in same slot

    // Check capacity
    if (slotPlayers.length >= CAPACITY && !slotPlayers.some(p => p.participantId === participantId)) {
      return;
    }

    try {
      await moveParticipant.mutateAsync({ participantId, teeTimeId: slot.id });
      onPlayerMoved();
    } catch (err) {
      console.error('Failed to move participant:', err);
    }
  };

  const handleRemove = async (participantId: number) => {
    try {
      await removeParticipant.mutateAsync(participantId);
      onPlayerMoved();
    } catch (err) {
      console.error('Failed to remove participant:', err);
    }
  };

  return (
    <Card
      className={[
        'transition-all',
        isDragOver && 'ring-2 ring-[#1B5E20] bg-green-50',
        isFull && !isDragOver && 'opacity-75',
      ].filter(Boolean).join(' ')}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className="p-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-gray-400" />
            <span className="font-semibold text-gray-900">{slot.scheduledTime}</span>
            {slot.autoFilled && (
              <Badge variant="neutral" className="text-[10px]">
                Auto-filled
              </Badge>
            )}
          </div>
          <span className={`text-xs ${isFull ? 'text-red-500 font-medium' : 'text-gray-400'}`}>
            {slotPlayers.length}/{CAPACITY}
          </span>
        </div>

        <div className="space-y-2 min-h-[5rem]">
          {slotPlayers.length === 0 && (
            <div className="text-sm text-gray-400 italic py-4 text-center border-2 border-dashed border-gray-200 rounded-lg">
              Drop players here
            </div>
          )}
          {slotPlayers.map((player) => {
            // Find the full participant data for drag-and-drop
            const fullParticipant: AdminParticipant = {
              id: player.participantId,
              playerId: player.playerId,
              fullName: player.playerName,
              flightId: player.flightId,
              name: player.flightName,
              handicapIndex: 0, // Not available in TeeTimeParticipant
              courseHandicap: 0,
              teeTimeId: slot.id,
              teeTimeNumber: slot.teeTimeNumber,
              isWithdrawn: false,
              skippedWeek: false,
            };

            return (
              <DraggablePlayer
                key={player.participantId}
                participant={fullParticipant}
                currentTeeTimeId={slot.id}
                onRemove={() => handleRemove(player.participantId)}
              />
            );
          })}
        </div>
      </div>
    </Card>
  );
}

interface UnassignedPlayersProps {
  participants: AdminParticipant[];
  roundId: number;
  onPlayerMoved: () => void;
}

function UnassignedPlayers({ participants, roundId, onPlayerMoved }: UnassignedPlayersProps) {
  const [isDragOver, setIsDragOver] = useState(false);
  const removeParticipant = useAdminRemoveParticipantFromTeeTime(roundId);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);

    const data = JSON.parse(e.dataTransfer.getData('application/json'));
    const { participantId, sourceTeeTimeId } = data;

    // Only remove if coming from a tee time
    if (sourceTeeTimeId != null) {
      try {
        await removeParticipant.mutateAsync(participantId);
        onPlayerMoved();
      } catch (err) {
        console.error('Failed to remove participant:', err);
      }
    }
  };

  return (
    <Card
      className={[
        'transition-all',
        isDragOver && 'ring-2 ring-red-500 bg-red-50',
      ].filter(Boolean).join(' ')}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className="p-4">
        <div className="flex items-center gap-2 mb-3">
          <Users className="h-4 w-4 text-gray-400" />
          <span className="font-semibold text-gray-900">Unassigned Players</span>
          <Badge variant="neutral">{participants.length}</Badge>
        </div>

        <div className="space-y-2 min-h-[5rem]">
          {participants.length === 0 && (
            <div className="text-sm text-gray-400 italic py-4 text-center border-2 border-dashed border-gray-200 rounded-lg">
              All players assigned
            </div>
          )}
          {participants.map((participant) => (
            <DraggablePlayer
              key={participant.id}
              participant={participant}
              currentTeeTimeId={null}
            />
          ))}
        </div>

        <p className="text-xs text-gray-400 mt-3">
          Drag players from here to tee times, or drop players here to unassign them.
        </p>
      </div>
    </Card>
  );
}

export function TeeTimesAdminPage() {
  const [selectedRoundId, setSelectedRoundId] = useState<number | null>(null);
  const { data: roundsPage, isLoading: roundsLoading } = useRounds(1, { sortBy: 'scheduledDate', sortDir: 'desc' });
  const { data: schedule, isLoading: scheduleLoading } = useRoundTeeTimes(selectedRoundId);
  const { data: participants, isLoading: participantsLoading, refetch: refetchParticipants } = useAdminRoundParticipants(selectedRoundId);

  const rounds = roundsPage?.data ?? [];

  // Get unassigned participants (those without a teeTimeId)
  const unassignedParticipants = useMemo(() => {
    if (!participants) return [];
    return participants.filter(p => p.teeTimeId == null);
  }, [participants]);

  // Map tee time slots to include their participants
  const teeTimeSlots = useMemo(() => {
    if (!schedule) return [];
    return schedule.slots;
  }, [schedule]);

  const handlePlayerMoved = useCallback(() => {
    refetchParticipants();
  }, [refetchParticipants]);

  const isLoading = roundsLoading || scheduleLoading || participantsLoading;

  return (
    <div className="space-y-6">
      <PageHeader title="Tee Time Management">
        <p className="text-sm text-gray-500">
          Drag and drop players between tee times to manage assignments.
        </p>
      </PageHeader>

      {/* Round Selector */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <Calendar className="h-5 w-5 text-gray-400" />
          <span className="text-sm font-medium text-gray-700">Select Round:</span>
        </div>
        <select
          value={selectedRoundId ?? ''}
          onChange={(e) => setSelectedRoundId(e.target.value ? parseInt(e.target.value, 10) : null)}
          className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-[#1B5E20] focus:border-transparent min-w-[250px]"
        >
          <option value="">Choose a round...</option>
          {rounds.map((round) => (
            <option key={round.id} value={round.id}>
              {new Date(round.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' })} — {round.courseName} (Week {round.weekNumber})
            </option>
          ))}
        </select>
      </div>

      {!selectedRoundId && !roundsLoading && (
        <div className="text-center py-12 text-gray-500">
          <ArrowRightLeft className="h-12 w-12 mx-auto mb-4 text-gray-300" />
          <p>Select a round to manage tee time assignments.</p>
        </div>
      )}

      {isLoading && selectedRoundId && (
        <div className="flex h-64 items-center justify-center">
          <Spinner />
        </div>
      )}

      {selectedRoundId && !isLoading && schedule && (
        <div className="space-y-6">
          {/* Schedule Info */}
          <div className="flex items-center gap-4 text-sm text-gray-600">
            <span>
              <strong>{schedule.participantCount}</strong> participants
            </span>
            <span>·</span>
            <span>
              <strong>{teeTimeSlots.length}</strong> tee times
            </span>
            {schedule.isLocked && (
              <>
                <span>·</span>
                <Badge variant="amber">Sign-ups locked</Badge>
              </>
            )}
          </div>

          {/* Tee Time Slots Grid */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {teeTimeSlots.map((slot) => (
              <TeeTimeSlotCard
                key={slot.id}
                slot={slot}
                roundId={selectedRoundId}
                onPlayerMoved={handlePlayerMoved}
              />
            ))}
          </div>

          {/* Unassigned Players */}
          <div className="pt-4 border-t border-gray-200">
            {participants && (
              <UnassignedPlayers
                participants={unassignedParticipants}
                roundId={selectedRoundId}
                onPlayerMoved={handlePlayerMoved}
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
