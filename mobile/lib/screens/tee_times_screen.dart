import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_service.dart';
import '../api/providers.dart';
import '../auth/auth_providers.dart';
import '../models/models.dart';

/// Shows the tee-time schedule for a specific round, or — when [roundId] is
/// null — for the next scheduled round (matching the web "/tee-times" page).
class TeeTimesScreen extends ConsumerWidget {
  const TeeTimesScreen({super.key, this.roundId});

  final int? roundId;

  void _invalidate(WidgetRef ref) {
    if (roundId != null) {
      ref.invalidate(roundTeeTimesProvider(roundId!));
    } else {
      ref.invalidate(nextRoundTeeTimesProvider);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scheduleAsync = roundId != null
        ? ref.watch(roundTeeTimesProvider(roundId!))
        : ref.watch(nextRoundTeeTimesProvider);
    final myStatus = ref.watch(myStatusProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: Text(roundId != null ? 'Tee Times' : 'Next Round Tee Times'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async => _invalidate(ref),
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
                  onPressed: () => _invalidate(ref),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (schedule) {
            if (schedule == null) {
              return Center(
                child: Text(
                  roundId != null
                      ? 'No tee times available for this round.'
                      : 'No upcoming round with tee times.',
                  style: const TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return _TeeTimesView(
              schedule: schedule,
              myPlayerId: myStatus.playerId,
              isAdmin:
                  myStatus.role == 'admin' || myStatus.role == 'scorer',
              onChanged: () => _invalidate(ref),
            );
          },
        ),
      ),
    );
  }
}

class _TeeTimesView extends ConsumerWidget {
  const _TeeTimesView({
    required this.schedule,
    this.myPlayerId,
    this.isAdmin = false,
    required this.onChanged,
  });

  final RoundTeeTimeSchedule schedule;
  final int? myPlayerId;
  final bool isAdmin;
  final VoidCallback onChanged;

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

        if (isParticipant && !schedule.isLocked) ...[
          const SizedBox(height: 16),
          _SkipWeekCard(schedule: schedule, onChanged: onChanged),
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
            isAdmin: isAdmin,
            onChanged: onChanged,
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

class _SkipWeekCard extends ConsumerStatefulWidget {
  const _SkipWeekCard({required this.schedule, required this.onChanged});

  final RoundTeeTimeSchedule schedule;
  final VoidCallback onChanged;

  @override
  ConsumerState<_SkipWeekCard> createState() => _SkipWeekCardState();
}

class _SkipWeekCardState extends ConsumerState<_SkipWeekCard> {
  bool _saving = false;

  Future<void> _toggle(bool skipped) async {
    setState(() => _saving = true);
    try {
      await ref
          .read(apiServiceProvider)
          .skipMyWeek(widget.schedule.roundId, skipped);
      widget.onChanged();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not update: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final skipped = widget.schedule.currentUserSkippedWeek;
    return Card(
      elevation: 0,
      color: skipped ? const Color(0xFFFFF7ED) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: skipped ? Colors.orange.shade200 : const Color(0xFFE5E7EB),
        ),
      ),
      child: SwitchListTile(
        title: const Text(
          'Skip this week',
          style: TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
        ),
        subtitle: Text(
          skipped
              ? 'You\'re marked as skipping this round.'
              : 'Sitting this one out? Flip the switch so auto-fill skips you.',
          style: const TextStyle(fontSize: 12),
        ),
        value: skipped,
        onChanged: _saving ? null : _toggle,
      ),
    );
  }
}

class _TeeTimeSlotCard extends ConsumerStatefulWidget {
  const _TeeTimeSlotCard({
    required this.slot,
    required this.schedule,
    required this.isParticipant,
    this.isAdmin = false,
    required this.onChanged,
  });

  final TeeTimeSlot slot;
  final RoundTeeTimeSchedule schedule;
  final bool isParticipant;
  final bool isAdmin;
  final VoidCallback onChanged;

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
      widget.onChanged();
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
      widget.onChanged();
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

  Future<void> _adminRemove(TeeTimeParticipant player) async {
    setState(() => _isLoading = true);
    try {
      final api = ref.read(apiServiceProvider);
      await api.adminRemoveParticipantFromTeeTime(
        widget.schedule.roundId,
        player.participantId,
      );
      ref.invalidate(adminRoundParticipantsProvider(widget.schedule.roundId));
      widget.onChanged();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not remove player: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _adminAddPlayer() async {
    final participants = await ref.read(
      adminRoundParticipantsProvider(widget.schedule.roundId).future,
    );
    if (!mounted) return;
    // Players not in this slot already (those in other slots get moved).
    final inThisSlot = widget.slot.players.map((p) => p.participantId).toSet();
    final candidates = participants
        .where((p) => !inThisSlot.contains(p.id) && !p.skippedWeek)
        .toList();
    if (candidates.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No players available to add.')),
      );
      return;
    }
    final selected = await showModalBottomSheet<AdminTeeTimeParticipant>(
      context: context,
      builder: (ctx) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'Add player to this tee time',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
              ),
            ),
            ...candidates.map(
              (p) => ListTile(
                title: Text(p.fullName),
                subtitle: p.teeTimeNumber != null
                    ? Text('Currently in tee time #${p.teeTimeNumber}')
                    : const Text('Unassigned'),
                onTap: () => Navigator.of(ctx).pop(p),
              ),
            ),
          ],
        ),
      ),
    );
    if (selected == null || !mounted) return;
    setState(() => _isLoading = true);
    try {
      final api = ref.read(apiServiceProvider);
      await api.adminMoveParticipantToTeeTime(
        widget.schedule.roundId,
        widget.slot.id,
        selected.id,
      );
      ref.invalidate(adminRoundParticipantsProvider(widget.schedule.roundId));
      widget.onChanged();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not move player: $e')),
        );
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
                    onDeleted: widget.isAdmin && !_isLoading
                        ? () => _adminRemove(player)
                        : null,
                    deleteIcon: widget.isAdmin
                        ? const Icon(Icons.close, size: 14)
                        : null,
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
            if (widget.isAdmin && !isFull && !_isLoading) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: _adminAddPlayer,
                  icon: const Icon(Icons.person_add, size: 16),
                  label: const Text('Add player (admin)'),
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
