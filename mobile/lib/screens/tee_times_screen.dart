import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_service.dart';
import '../api/providers.dart';
import '../auth/auth_providers.dart';
import '../models/models.dart';

class TeeTimesScreen extends ConsumerWidget {
  const TeeTimesScreen({super.key, required this.roundId});

  final int roundId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheduleAsync = ref.watch(roundTeeTimesProvider(roundId));
    final myStatus = ref.watch(myStatusProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Tee Times'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(roundTeeTimesProvider(roundId));
        },
        child: scheduleAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load tee times.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () =>
                      ref.invalidate(roundTeeTimesProvider(roundId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (schedule) {
            if (schedule == null) {
              return const Center(
                child: Text(
                  'No tee times available for this round.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return _TeeTimesView(
              schedule: schedule,
              myPlayerId: myStatus.playerId,
            );
          },
        ),
      ),
    );
  }
}

class _TeeTimesView extends ConsumerWidget {
  const _TeeTimesView({required this.schedule, this.myPlayerId});

  final RoundTeeTimeSchedule schedule;
  final int? myPlayerId;

  String _formatCutoff(String cutoffUtc) {
    final cutoff = DateTime.parse(cutoffUtc);
    final now = DateTime.now();
    final diff = cutoff.difference(now);

    if (diff.isNegative) return 'Sign-ups closed';

    final days = diff.inDays;
    final hours = diff.inHours % 24;
    final minutes = diff.inMinutes % 60;

    if (days > 0) return 'Auto-fill in ${days}d ${hours}h';
    if (hours > 0) return 'Auto-fill in ${hours}h ${minutes}m';
    return 'Auto-fill in ${minutes}m';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isParticipant = schedule.currentUserParticipantId != null;
    final hasPlayerId = myPlayerId != null;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Header info
        Card(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: BorderSide(
              color: schedule.isLocked
                  ? Colors.orange.shade200
                  : Colors.blue.shade200,
            ),
          ),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Icon(
                  schedule.isLocked ? Icons.lock : Icons.access_time,
                  color: schedule.isLocked ? Colors.orange : Colors.blue,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        schedule.isLocked
                            ? 'Sign-ups Closed'
                            : _formatCutoff(schedule.cutoffUtc),
                        style: const TextStyle(
                          fontWeight: FontWeight.w600,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${schedule.participantCount} players registered',
                        style: const TextStyle(
                          fontSize: 12,
                          color: Color(0xFF6B7280),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),

        if (!hasPlayerId) ...[
          const SizedBox(height: 16),
          _InfoCard(
            icon: Icons.info,
            message:
                'Your account isn\'t linked to a player profile yet. Contact an admin.',
            color: Colors.orange,
          ),
        ],

        if (hasPlayerId && !isParticipant) ...[
          const SizedBox(height: 16),
          _InfoCard(
            icon: Icons.info,
            message:
                'You\'re not registered as a participant in this round — viewing only.',
            color: Colors.blue,
          ),
        ],

        const SizedBox(height: 24),

        // Tee Time Slots
        const Text(
          'Tee Time Slots',
          style: TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: Color(0xFF111827),
          ),
        ),
        const SizedBox(height: 12),

        ...schedule.slots.map(
          (slot) => _TeeTimeSlotCard(
            slot: slot,
            schedule: schedule,
            isParticipant: isParticipant,
          ),
        ),

        const SizedBox(height: 24),

        // Preference selector (only for participants)
        if (isParticipant && myPlayerId != null)
          _TeeTimePreferenceSection(
            playerId: myPlayerId!,
            currentMask: schedule.currentUserPreferredSlots,
          ),
      ],
    );
  }
}

class _TeeTimePreferenceSection extends ConsumerWidget {
  const _TeeTimePreferenceSection({
    required this.playerId,
    required this.currentMask,
  });

  final int playerId;
  final int currentMask;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Column(
      children: [
        const Text(
          'Preferred Tee Time Slots',
          style: TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: Color(0xFF111827),
          ),
        ),
        const SizedBox(height: 8),
        const Text(
          'Select your preferred slots for auto-fill',
          style: TextStyle(fontSize: 13, color: Color(0xFF6B7280)),
        ),
        const SizedBox(height: 12),
        _TeeTimePreferenceSelector(
          playerId: playerId,
          currentMask: currentMask,
        ),
      ],
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({
    required this.icon,
    required this.message,
    required this.color,
  });

  final IconData icon;
  final String message;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                fontSize: 13,
                color: color.withValues(alpha: 0.8),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _TeeTimeSlotCard extends ConsumerStatefulWidget {
  const _TeeTimeSlotCard({
    required this.slot,
    required this.schedule,
    required this.isParticipant,
  });

  final TeeTimeSlot slot;
  final RoundTeeTimeSchedule schedule;
  final bool isParticipant;

  @override
  ConsumerState<_TeeTimeSlotCard> createState() => _TeeTimeSlotCardState();
}

class _TeeTimeSlotCardState extends ConsumerState<_TeeTimeSlotCard> {
  bool _isLoading = false;

  Future<void> _joinSlot() async {
    setState(() => _isLoading = true);
    try {
      final api = ref.read(apiServiceProvider);
      await api.joinTeeTime(widget.schedule.roundId, widget.slot.id);
      ref.invalidate(roundTeeTimesProvider(widget.schedule.roundId));
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Could not join slot: $e')));
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _leaveSlot() async {
    setState(() => _isLoading = true);
    try {
      final api = ref.read(apiServiceProvider);
      await api.leaveTeeTime(widget.schedule.roundId);
      ref.invalidate(roundTeeTimesProvider(widget.schedule.roundId));
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Could not leave slot: $e')));
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final slot = widget.slot;
    final schedule = widget.schedule;
    const capacity = 4;

    final isMine = schedule.currentUserTeeTimeId == slot.id;
    final isFull = slot.players.length >= capacity;
    final canJoin =
        widget.isParticipant && !schedule.isLocked && !isMine && !isFull;
    final canLeave = widget.isParticipant && !schedule.isLocked && isMine;

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      color: isMine ? const Color(0xFFE8F5E9) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: isMine ? const Color(0xFF4CAF50) : const Color(0xFFE5E7EB),
          width: 1,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.schedule,
                  size: 20,
                  color: isMine ? const Color(0xFF1a5c38) : Colors.grey,
                ),
                const SizedBox(width: 8),
                Text(
                  slot.scheduledTime,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w600,
                    color: isMine
                        ? const Color(0xFF1a5c38)
                        : const Color(0xFF111827),
                  ),
                ),
                if (isMine) ...[
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFF4CAF50),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Text(
                      'Your slot',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ),
                ],
                if (slot.autoFilled) ...[
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.grey.shade200,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Text(
                      'Auto-filled',
                      style: TextStyle(color: Color(0xFF6B7280), fontSize: 11),
                    ),
                  ),
                ],
                const Spacer(),
                Text(
                  '${slot.players.length}/$capacity',
                  style: TextStyle(
                    fontSize: 14,
                    color: isFull ? Colors.red : const Color(0xFF6B7280),
                    fontWeight: isFull ? FontWeight.w600 : null,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            if (slot.players.isEmpty)
              const Text(
                'No players yet',
                style: TextStyle(
                  fontSize: 14,
                  color: Color(0xFF9CA3AF),
                  fontStyle: FontStyle.italic,
                ),
              )
            else
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: slot.players.map((player) {
                  final isCurrentUser =
                      player.playerId == ref.watch(myStatusProvider).playerId;
                  return Chip(
                    avatar: isCurrentUser
                        ? const Icon(
                            Icons.person,
                            size: 16,
                            color: Color(0xFF1a5c38),
                          )
                        : null,
                    label: Text(
                      player.playerName,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: isCurrentUser ? FontWeight.w600 : null,
                        color: isCurrentUser ? const Color(0xFF1a5c38) : null,
                      ),
                    ),
                    backgroundColor: isCurrentUser
                        ? const Color(0xFFE8F5E9)
                        : const Color(0xFFF3F4F6),
                    side: BorderSide(
                      color: isCurrentUser
                          ? const Color(0xFF4CAF50)
                          : Colors.transparent,
                    ),
                    padding: EdgeInsets.zero,
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  );
                }).toList(),
              ),
            if ((canJoin || canLeave) && !_isLoading) ...[
              const SizedBox(height: 12),
              const Divider(),
              const SizedBox(height: 8),
              if (canJoin)
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: _joinSlot,
                    icon: const Icon(Icons.login, size: 18),
                    label: Text(isFull ? 'Full' : 'Join this slot'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1a5c38),
                      foregroundColor: Colors.white,
                    ),
                  ),
                ),
              if (canLeave)
                SizedBox(
                  width: double.infinity,
                  child: OutlinedButton.icon(
                    onPressed: _leaveSlot,
                    icon: const Icon(Icons.logout, size: 18, color: Colors.red),
                    label: const Text(
                      'Leave slot',
                      style: TextStyle(color: Colors.red),
                    ),
                    style: OutlinedButton.styleFrom(
                      side: const BorderSide(color: Colors.red),
                    ),
                  ),
                ),
            ],
            if (_isLoading)
              const Center(
                child: Padding(
                  padding: EdgeInsets.all(8),
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _TeeTimePreferenceSelector extends ConsumerStatefulWidget {
  const _TeeTimePreferenceSelector({
    required this.playerId,
    required this.currentMask,
  });

  final int playerId;
  final int currentMask;

  @override
  ConsumerState<_TeeTimePreferenceSelector> createState() =>
      _TeeTimePreferenceSelectorState();
}

class _TeeTimePreferenceSelectorState
    extends ConsumerState<_TeeTimePreferenceSelector> {
  late Set<String> _selected;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    _selected = {};
    for (final slot in teeTimeSlots) {
      final flag = teeTimeSlotFlags[slot] ?? 0;
      if ((widget.currentMask & flag) != 0) {
        _selected.add(slot);
      }
    }
  }

  Future<void> _save() async {
    setState(() => _isSaving = true);
    try {
      final api = ref.read(apiServiceProvider);
      await api.setTeeTimePreference(widget.playerId, _selected.toList());
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Preferences saved!')));
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Failed to save: $e')));
      }
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  bool get _isDirty {
    var mask = 0;
    for (final slot in _selected) {
      mask |= teeTimeSlotFlags[slot] ?? 0;
    }
    return mask != widget.currentMask;
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 8,
          children: teeTimeSlots.map((slot) {
            final isSelected = _selected.contains(slot);
            return ChoiceChip(
              label: Text(slot),
              selected: isSelected,
              onSelected: (selected) {
                setState(() {
                  if (selected) {
                    _selected.add(slot);
                  } else {
                    _selected.remove(slot);
                  }
                });
              },
              backgroundColor: Colors.white,
              selectedColor: const Color(0xFF1a5c38),
              labelStyle: TextStyle(
                color: isSelected ? Colors.white : const Color(0xFF374151),
              ),
            );
          }).toList(),
        ),
        if (_isDirty) ...[
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: _isSaving ? null : _save,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF1a5c38),
                foregroundColor: Colors.white,
              ),
              child: _isSaving
                  ? const SizedBox(
                      height: 16,
                      width: 16,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        valueColor: AlwaysStoppedAnimation(Colors.white),
                      ),
                    )
                  : const Text('Save Preferences'),
            ),
          ),
        ],
      ],
    );
  }
}
