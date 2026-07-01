import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../api/api_service.dart';
import '../../api/providers.dart';
import '../../models/models.dart';
import '../../widgets/status_badge.dart';

/// Light round management for admins: create 9-hole rounds, enter scores,
/// finalize / re-open / cancel / delete. Tournament creation and schedule
/// generation stay on the web admin.
class AdminRoundsScreen extends ConsumerWidget {
  const AdminRoundsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final roundsAsync = ref.watch(roundsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Manage Rounds'),
        leading: const BackButton(),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _showCreateRoundSheet(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('Add Round'),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(roundsProvider);
        },
        child: roundsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load rounds.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () => ref.invalidate(roundsProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (rounds) {
            if (rounds.isEmpty) {
              return const Center(
                child: Text(
                  'No rounds yet.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView.builder(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 88),
              itemCount: rounds.length,
              itemBuilder: (context, index) =>
                  _AdminRoundCard(round: rounds[index]),
            );
          },
        ),
      ),
    );
  }

  Future<void> _showCreateRoundSheet(BuildContext context, WidgetRef ref) {
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _CreateRoundSheet(),
    );
  }
}

class _AdminRoundCard extends ConsumerStatefulWidget {
  const _AdminRoundCard({required this.round});

  final Round round;

  @override
  ConsumerState<_AdminRoundCard> createState() => _AdminRoundCardState();
}

class _AdminRoundCardState extends ConsumerState<_AdminRoundCard> {
  bool _busy = false;

  Future<void> _run(
    String confirmTitle,
    String confirmMessage,
    Future<void> Function() action, {
    bool destructive = false,
  }) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(confirmTitle),
        content: Text(confirmMessage),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: destructive
                ? FilledButton.styleFrom(backgroundColor: Colors.red)
                : null,
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text(confirmTitle),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() => _busy = true);
    try {
      await action();
      ref.invalidate(roundsProvider);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Action failed: $e')));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final round = widget.round;
    final api = ref.read(apiServiceProvider);
    final dateStr = DateFormat('MMM d, y').format(round.scheduledDate);
    final isFinalized = round.status == RoundStatus.finalized;
    final isScheduled = round.status == RoundStatus.scheduled;
    final isInProgress = round.status == RoundStatus.inProgress ||
        round.status == RoundStatus.pendingFinalization;

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    '${round.weekNumber != null ? 'Wk ${round.weekNumber} · ' : ''}${round.courseName}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 15,
                    ),
                  ),
                ),
                StatusBadge(round.status),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              '$dateStr · ${round.isTournament ? 'Tournament' : '${round.nineHoleSide} 9'}',
              style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
            ),
            const SizedBox(height: 8),
            if (_busy)
              const Center(
                child: Padding(
                  padding: EdgeInsets.all(8),
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              )
            else
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  if (round.isTournament)
                    TextButton(
                      onPressed: () => context
                          .push('/rounds/${round.id}/tournament-results'),
                      child: Text(
                        isFinalized ? 'View Results' : 'Results',
                      ),
                    )
                  else
                    TextButton(
                      onPressed: () =>
                          context.push('/admin/rounds/${round.id}/scores'),
                      child: Text(
                        isFinalized ? 'View Scorecard' : 'Enter Scores',
                      ),
                    ),
                  if (isInProgress)
                    TextButton(
                      onPressed: () => _run(
                        'Finalize',
                        'Finalize the round on $dateStr? This will lock scores and recalculate standings.',
                        () => api.finalizeRound(round.id),
                      ),
                      child: const Text(
                        'Finalize',
                        style: TextStyle(color: Color(0xFF1a5c38)),
                      ),
                    ),
                  if (isFinalized)
                    TextButton(
                      onPressed: () => _run(
                        'Re-open',
                        'Re-open the finalized round on $dateStr? Scores can be edited again, and handicaps from this round will be recalculated when you re-finalize.',
                        () => api.reopenRound(round.id),
                      ),
                      child: const Text(
                        'Re-open',
                        style: TextStyle(color: Color(0xFFD97706)),
                      ),
                    ),
                  if (isScheduled)
                    TextButton(
                      onPressed: () => _run(
                        'Cancel Round',
                        'Cancel the round on $dateStr? Later rounds in this half will shift forward by one week.',
                        () => api.cancelRound(round.id),
                      ),
                      child: const Text(
                        'Cancel',
                        style: TextStyle(color: Color(0xFFD97706)),
                      ),
                    ),
                  if (!isFinalized)
                    TextButton(
                      onPressed: () => _run(
                        'Delete',
                        'Permanently delete the round on $dateStr? This cannot be undone.',
                        () => api.deleteRound(round.id),
                        destructive: true,
                      ),
                      child: const Text(
                        'Delete',
                        style: TextStyle(color: Colors.red),
                      ),
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _CreateRoundSheet extends ConsumerStatefulWidget {
  const _CreateRoundSheet();

  @override
  ConsumerState<_CreateRoundSheet> createState() => _CreateRoundSheetState();
}

class _CreateRoundSheetState extends ConsumerState<_CreateRoundSheet> {
  int? _halfId;
  int? _courseId;
  DateTime _date = DateTime.now();
  String _side = 'Front';
  bool _saving = false;
  String? _error;

  Future<void> _save() async {
    if (_halfId == null || _courseId == null) {
      setState(() => _error = 'Choose a season half and a course.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref.read(apiServiceProvider).createRound(
            halfId: _halfId!,
            courseId: _courseId!,
            scheduledDate: DateFormat('yyyy-MM-dd').format(_date),
            nineHoleSide: _side,
          );
      ref.invalidate(roundsProvider);
      if (mounted) Navigator.of(context).pop();
    } catch (e) {
      if (mounted) {
        setState(() => _error = 'Could not create round: $e');
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final seasonsAsync = ref.watch(seasonsProvider);
    final coursesAsync = ref.watch(coursesProvider);

    return Padding(
      padding: EdgeInsets.only(
        left: 16,
        right: 16,
        top: 16,
        bottom: MediaQuery.of(context).viewInsets.bottom + 16,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Add Round',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 16),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                _error!,
                style: const TextStyle(color: Colors.red),
              ),
            ),
          seasonsAsync.when(
            loading: () => const LinearProgressIndicator(),
            error: (_, _) => const Text('Could not load seasons.'),
            data: (seasons) {
              final active = seasons.where((s) => s.isActive).toList();
              final halves = (active.isNotEmpty ? active.first : null)
                      ?.halves ??
                  const <SeasonHalf>[];
              return DropdownButtonFormField<int>(
                initialValue: _halfId,
                decoration: const InputDecoration(
                  labelText: 'Season half',
                  border: OutlineInputBorder(),
                ),
                items: halves
                    .map(
                      (h) => DropdownMenuItem(
                        value: h.id,
                        child: Text(h.name),
                      ),
                    )
                    .toList(),
                onChanged: (v) => setState(() => _halfId = v),
              );
            },
          ),
          const SizedBox(height: 12),
          coursesAsync.when(
            loading: () => const LinearProgressIndicator(),
            error: (_, _) => const Text('Could not load courses.'),
            data: (courses) => DropdownButtonFormField<int>(
              initialValue: _courseId,
              decoration: const InputDecoration(
                labelText: 'Course',
                border: OutlineInputBorder(),
              ),
              items: courses
                  .map(
                    (c) => DropdownMenuItem(
                      value: c.id,
                      child: Text(c.name),
                    ),
                  )
                  .toList(),
              onChanged: (v) => setState(() => _courseId = v),
            ),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: () async {
              final picked = await showDatePicker(
                context: context,
                initialDate: _date,
                firstDate: DateTime.now().subtract(const Duration(days: 365)),
                lastDate: DateTime.now().add(const Duration(days: 365)),
              );
              if (picked != null) setState(() => _date = picked);
            },
            icon: const Icon(Icons.calendar_today, size: 16),
            label: Text(DateFormat('MMM d, y').format(_date)),
          ),
          const SizedBox(height: 12),
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'Front', label: Text('Front 9')),
              ButtonSegment(value: 'Back', label: Text('Back 9')),
            ],
            selected: {_side},
            onSelectionChanged: (s) => setState(() => _side = s.first),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 48,
            child: FilledButton(
              onPressed: _saving ? null : _save,
              child: _saving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Create Round'),
            ),
          ),
        ],
      ),
    );
  }
}
