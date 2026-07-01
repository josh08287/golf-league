import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../api/api_service.dart';
import '../../api/providers.dart';
import '../../models/models.dart';

/// Admin score entry for a standard (nine-hole) round: one expandable card per
/// participant with a stepper per hole, a skip-week toggle, and a single
/// submit that pushes scores for every non-skipped player — the mobile
/// equivalent of the web admin score-entry grid.
class AdminScoreEntryScreen extends ConsumerStatefulWidget {
  const AdminScoreEntryScreen({super.key, required this.roundId});

  final int roundId;

  @override
  ConsumerState<AdminScoreEntryScreen> createState() =>
      _AdminScoreEntryScreenState();
}

class _AdminScoreEntryScreenState
    extends ConsumerState<AdminScoreEntryScreen> {
  /// playerId → holeNumber → gross strokes
  final Map<int, Map<int, int>> _scores = {};

  /// Local skip overrides (playerId → skipped), only when changed.
  final Map<int, bool> _localSkips = {};

  bool _submitting = false;
  bool _seeded = false;
  String? _error;
  String? _success;

  List<int> _holesForSide(String side) => side == 'Back'
      ? List.generate(9, (i) => i + 10)
      : List.generate(9, (i) => i + 1);

  bool _resolveSkipped(RoundParticipant p) =>
      _localSkips[p.playerId] ?? p.skippedWeek;

  void _seedFromScorecards(List<Scorecard> cards) {
    if (_seeded) return;
    for (final sc in cards) {
      final map = _scores.putIfAbsent(sc.playerId, () => {});
      for (final h in sc.holes) {
        map.putIfAbsent(h.holeNumber, () => h.strokes);
      }
    }
    _seeded = true;
  }

  Future<void> _submitAll(
    Round round,
    List<RoundParticipant> participants,
    List<int> holes,
  ) async {
    setState(() {
      _error = null;
      _success = null;
    });
    for (final p in participants) {
      if (_resolveSkipped(p)) continue;
      for (final h in holes) {
        if (_scores[p.playerId]?[h] == null) {
          setState(
            () => _error = 'Missing score for ${p.playerName} on hole $h.',
          );
          return;
        }
      }
    }
    setState(() => _submitting = true);
    final api = ref.read(apiServiceProvider);
    try {
      for (final entry in _localSkips.entries) {
        await api.setParticipantSkipped(round.id, entry.key, entry.value);
      }
      for (final p in participants) {
        if (_resolveSkipped(p)) continue;
        await api.submitHoleScores(
          round.id,
          p.playerId,
          holes
              .map((h) => {
                    'holeNumber': h,
                    'grossScore': _scores[p.playerId]![h]!,
                  })
              .toList(),
        );
      }
      _localSkips.clear();
      ref.invalidate(roundParticipantsProvider(widget.roundId));
      ref.invalidate(scorecardsProvider(widget.roundId));
      if (mounted) {
        setState(() => _success = 'All scores submitted successfully!');
      }
    } catch (e) {
      if (mounted) setState(() => _error = 'Submission failed: $e');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final roundAsync = ref.watch(roundDetailProvider(widget.roundId));
    final participantsAsync =
        ref.watch(roundParticipantsProvider(widget.roundId));
    final scorecardsAsync = ref.watch(scorecardsProvider(widget.roundId));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Score Entry'),
        leading: const BackButton(),
      ),
      body: roundAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => const Center(child: Text('Round not found.')),
        data: (round) {
          final isFinalized = round.status == RoundStatus.finalized;
          final holes = _holesForSide(round.nineHoleSide);
          final courseAsync = ref.watch(courseDetailProvider(round.courseId));
          final pars = <int, int>{};
          for (final h
              in courseAsync.valueOrNull?.holeDetails ?? const <CourseHole>[]) {
            pars[h.holeNumber] = h.par;
          }

          scorecardsAsync.whenData(_seedFromScorecards);

          return participantsAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, _) =>
                const Center(child: Text('Could not load participants.')),
            data: (participants) => Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${round.courseName} · ${DateFormat('MMM d, y').format(round.scheduledDate)} · ${round.nineHoleSide} 9'
                        '${round.weekNumber != null ? ' (Week ${round.weekNumber})' : ''}',
                        style: const TextStyle(
                          fontSize: 13,
                          color: Color(0xFF6B7280),
                        ),
                      ),
                      if (isFinalized)
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text(
                            'This round is finalized — scores are read-only.',
                            style: TextStyle(
                              fontSize: 12,
                              color: Color(0xFF92400E),
                            ),
                          ),
                        ),
                      if (_error != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(
                            _error!,
                            style: const TextStyle(
                              color: Colors.red,
                              fontSize: 13,
                            ),
                          ),
                        ),
                      if (_success != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(
                            _success!,
                            style: const TextStyle(
                              color: Color(0xFF1a5c38),
                              fontSize: 13,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
                Expanded(
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: participants.length,
                    itemBuilder: (context, index) {
                      final p = participants[index];
                      final skipped = _resolveSkipped(p);
                      return _ParticipantScoreCard(
                        participant: p,
                        holes: holes,
                        pars: pars,
                        scores: _scores.putIfAbsent(p.playerId, () => {}),
                        skipped: skipped,
                        readonly: isFinalized || _submitting,
                        onSkipChanged: isFinalized
                            ? null
                            : (v) => setState(() {
                                  _localSkips[p.playerId] = v;
                                  if (v) _scores.remove(p.playerId);
                                }),
                        onScoreChanged: (hole, value) => setState(() {
                          if (value == null) {
                            _scores[p.playerId]?.remove(hole);
                          } else {
                            _scores.putIfAbsent(p.playerId, () => {})[hole] =
                                value;
                          }
                        }),
                      );
                    },
                  ),
                ),
                if (!isFinalized)
                  SafeArea(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                      child: SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: FilledButton.icon(
                          onPressed: _submitting
                              ? null
                              : () => _submitAll(round, participants, holes),
                          icon: _submitting
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.save),
                          label: Text(
                            _submitting
                                ? 'Submitting…'
                                : 'Submit All Scores',
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _ParticipantScoreCard extends StatefulWidget {
  const _ParticipantScoreCard({
    required this.participant,
    required this.holes,
    required this.pars,
    required this.scores,
    required this.skipped,
    required this.readonly,
    required this.onSkipChanged,
    required this.onScoreChanged,
  });

  final RoundParticipant participant;
  final List<int> holes;
  final Map<int, int> pars;
  final Map<int, int> scores;
  final bool skipped;
  final bool readonly;
  final ValueChanged<bool>? onSkipChanged;
  final void Function(int hole, int? value) onScoreChanged;

  @override
  State<_ParticipantScoreCard> createState() => _ParticipantScoreCardState();
}

class _ParticipantScoreCardState extends State<_ParticipantScoreCard> {
  bool _expanded = false;

  int get _entered =>
      widget.holes.where((h) => widget.scores[h] != null).length;

  int get _total => widget.holes.fold(0, (s, h) => s + (widget.scores[h] ?? 0));

  @override
  Widget build(BuildContext context) {
    final p = widget.participant;
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      elevation: 0,
      color: widget.skipped ? const Color(0xFFF9FAFB) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Column(
        children: [
          ListTile(
            onTap: widget.skipped
                ? null
                : () => setState(() => _expanded = !_expanded),
            title: Text(
              p.playerName,
              style: TextStyle(
                fontWeight: FontWeight.w600,
                fontSize: 14,
                decoration: widget.skipped ? TextDecoration.lineThrough : null,
                color: widget.skipped ? const Color(0xFF9CA3AF) : null,
              ),
            ),
            subtitle: Text(
              widget.skipped
                  ? 'Skipped — 0 pts, no handicap impact'
                  : 'HCP ${p.handicapAtTime.toStringAsFixed(1)} · CH ${p.courseHandicap}'
                      ' · $_entered/${widget.holes.length} holes'
                      '${_entered > 0 ? ' · $_total strokes' : ''}',
              style: const TextStyle(fontSize: 12),
            ),
            trailing: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (widget.onSkipChanged != null)
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Text('Skip', style: TextStyle(fontSize: 11)),
                      Checkbox(
                        value: widget.skipped,
                        onChanged: widget.readonly
                            ? null
                            : (v) => widget.onSkipChanged!(v ?? false),
                      ),
                    ],
                  ),
                if (!widget.skipped)
                  Icon(
                    _expanded ? Icons.expand_less : Icons.expand_more,
                    color: const Color(0xFF9CA3AF),
                  ),
              ],
            ),
          ),
          if (_expanded && !widget.skipped)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Column(
                children: widget.holes.map((h) {
                  final par = widget.pars[h] ?? 4;
                  final value = widget.scores[h];
                  return Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 80,
                          child: Text(
                            'Hole $h',
                            style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                        Text(
                          'Par $par',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFF9CA3AF),
                          ),
                        ),
                        const Spacer(),
                        IconButton(
                          visualDensity: VisualDensity.compact,
                          icon: const Icon(Icons.remove_circle_outline),
                          onPressed: widget.readonly || value == null
                              ? null
                              : () => widget.onScoreChanged(
                                    h,
                                    value > 1 ? value - 1 : null,
                                  ),
                        ),
                        SizedBox(
                          width: 32,
                          child: Text(
                            value != null ? '$value' : '—',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                              color: value == null
                                  ? const Color(0xFFD1D5DB)
                                  : value < par
                                      ? const Color(0xFF16A34A)
                                      : value == par
                                          ? const Color(0xFF111827)
                                          : const Color(0xFFDC2626),
                            ),
                          ),
                        ),
                        IconButton(
                          visualDensity: VisualDensity.compact,
                          icon: const Icon(Icons.add_circle_outline),
                          onPressed: widget.readonly
                              ? null
                              : () => widget.onScoreChanged(
                                    h,
                                    (value ?? par) + (value == null ? 0 : 1),
                                  ),
                        ),
                      ],
                    ),
                  );
                }).toList(),
              ),
            ),
        ],
      ),
    );
  }
}
