import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/api_service.dart';
import '../api/providers.dart';
import '../models/models.dart';

class ScoreEntryScreen extends ConsumerWidget {
  const ScoreEntryScreen({super.key, required this.teeTimeId});

  final int teeTimeId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scorecardAsync = ref.watch(teeTimeGroupScorecardProvider(teeTimeId));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Enter Scores'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(teeTimeGroupScorecardProvider(teeTimeId));
        },
        child: scorecardAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load scorecard.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () =>
                      ref.invalidate(teeTimeGroupScorecardProvider(teeTimeId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (scorecard) {
            if (scorecard == null) {
              return const Center(
                child: Text(
                  'Scorecard not available.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return _ScoreEntryView(scorecard: scorecard, teeTimeId: teeTimeId);
          },
        ),
      ),
    );
  }
}

class _ScoreEntryView extends StatefulWidget {
  const _ScoreEntryView({required this.scorecard, required this.teeTimeId});

  final TeeTimeGroupScorecard scorecard;
  final int teeTimeId;

  @override
  State<_ScoreEntryView> createState() => _ScoreEntryViewState();
}

class _ScoreEntryViewState extends State<_ScoreEntryView> {
  late Map<int, Map<int, int>> _scores; // playerId -> holeNumber -> strokes
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    _scores = {};
    for (final player in widget.scorecard.players) {
      _scores[player.playerId] = {};
      for (final holeScore in player.holeScores) {
        if (holeScore.grossStrokes != null) {
          _scores[player.playerId]![holeScore.holeNumber] =
              holeScore.grossStrokes!;
        }
      }
    }
  }

  Future<void> _submitScores() async {
    setState(() => _isSubmitting = true);

    try {
      final apiService = ProviderScope.containerOf(
        context,
      ).read(apiServiceProvider);

      final playerScores = widget.scorecard.players.map((player) {
        final holeScores = <Map<String, dynamic>>[];
        for (final hole in widget.scorecard.holes) {
          final strokes = _scores[player.playerId]?[hole.holeNumber];
          if (strokes != null) {
            holeScores.add({
              'holeNumber': hole.holeNumber,
              'grossStrokes': strokes,
            });
          }
        }
        return {'playerId': player.playerId, 'holeScores': holeScores};
      }).toList();

      await apiService.submitTeeTimeScores(widget.teeTimeId, playerScores);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Scores submitted successfully!')),
        );
        context.pop();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Failed to submit scores: $e')));
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scorecard = widget.scorecard;

    return Column(
      children: [
        // Header
        Container(
          color: Colors.white,
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                scorecard.courseName,
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                '${scorecard.nineHoleSide} 9 · Tee Time: ${scorecard.scheduledTimeFormatted}',
                style: const TextStyle(fontSize: 14, color: Color(0xFF6B7280)),
              ),
            ],
          ),
        ),

        // Score entry table
        Expanded(
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: _buildScoreTable(),
            ),
          ),
        ),

        // Submit button
        Container(
          color: Colors.white,
          padding: const EdgeInsets.all(16),
          child: SafeArea(
            child: SizedBox(
              width: double.infinity,
              height: 48,
              child: ElevatedButton(
                onPressed: _isSubmitting ? null : _submitScores,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF1a5c38),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                child: _isSubmitting
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          valueColor: AlwaysStoppedAnimation(Colors.white),
                        ),
                      )
                    : const Text(
                        'Submit Scores',
                        style: TextStyle(fontSize: 16),
                      ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildScoreTable() {
    final scorecard = widget.scorecard;
    final holes = scorecard.holes;
    final players = scorecard.players;

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header row with hole numbers
          Container(
            color: const Color(0xFFF9FAFB),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
            child: Row(
              children: [
                const SizedBox(
                  width: 120,
                  child: Text(
                    'Player',
                    style: TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
                ...holes.map(
                  (hole) => SizedBox(
                    width: 50,
                    child: Column(
                      children: [
                        Text(
                          '${hole.holeNumber}',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        Text(
                          'Par ${hole.par}',
                          style: const TextStyle(
                            fontSize: 10,
                            color: Color(0xFF6B7280),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),

          const Divider(height: 1),

          // Player rows
          ...players.map((player) {
            return Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                border: Border(bottom: BorderSide(color: Colors.grey.shade200)),
              ),
              child: Row(
                children: [
                  SizedBox(
                    width: 120,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          player.playerName,
                          style: const TextStyle(fontWeight: FontWeight.w500),
                          overflow: TextOverflow.ellipsis,
                        ),
                        Text(
                          'HCP: ${player.handicapIndex.toStringAsFixed(1)}',
                          style: const TextStyle(
                            fontSize: 11,
                            color: Color(0xFF6B7280),
                          ),
                        ),
                      ],
                    ),
                  ),
                  ...holes.map((hole) {
                    final currentScore =
                        _scores[player.playerId]?[hole.holeNumber];
                    return SizedBox(
                      width: 50,
                      child: TextField(
                        controller: TextEditingController(
                          text: currentScore?.toString() ?? '',
                        ),
                        keyboardType: TextInputType.number,
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontSize: 14),
                        decoration: InputDecoration(
                          isDense: true,
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 4,
                            vertical: 8,
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(6),
                            borderSide: BorderSide(color: Colors.grey.shade300),
                          ),
                          hintText: '-',
                          hintStyle: TextStyle(color: Colors.grey.shade400),
                        ),
                        onChanged: (value) {
                          final strokes = int.tryParse(value);
                          setState(() {
                            if (strokes != null && strokes > 0) {
                              _scores[player.playerId] ??= {};
                              _scores[player.playerId]![hole.holeNumber] =
                                  strokes;
                            } else {
                              _scores[player.playerId]?.remove(hole.holeNumber);
                            }
                          });
                        },
                      ),
                    );
                  }),
                ],
              ),
            );
          }),
        ],
      ),
    );
  }
}
