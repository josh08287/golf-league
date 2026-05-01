import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/utils/stableford_calculator.dart';
import '../../rounds/domain/models.dart' as rounds_models;
import '../../rounds/presentation/widgets/scorecard_table.dart';
import '../domain/models.dart';
import 'providers.dart';

class ScoreEntryScreen extends ConsumerStatefulWidget {
  const ScoreEntryScreen({super.key, required this.roundId});

  final int roundId;

  @override
  ConsumerState<ScoreEntryScreen> createState() => _ScoreEntryScreenState();
}

class _ScoreEntryScreenState extends ConsumerState<ScoreEntryScreen> {
  final PageController _pageController = PageController();
  final Map<int, int> _grossScores = {};
  int _currentPage = 0;

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _setScore(int holeNumber, int score) {
    setState(() => _grossScores[holeNumber] = score);
  }

  void _nextPage() {
    _pageController.nextPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  void _previousPage() {
    _pageController.previousPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  @override
  Widget build(BuildContext context) {
    final holesAsync = ref.watch(scoreEntryHolesProvider(widget.roundId));

    return Scaffold(
      appBar: AppBar(
        title: Text('Round ${widget.roundId} — Score Entry'),
        leading: IconButton(
          icon: const Icon(Icons.close),
          onPressed: () => context.go('/home'),
        ),
      ),
      body: holesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('Error: $e')),
        data: (holes) {
          // 18 hole pages + 1 confirmation page
          final totalPages = holes.length + 1;

          return Column(
            children: [
              _ProgressBar(
                current: _currentPage,
                total: holes.length,
              ),
              Expanded(
                child: PageView.builder(
                  controller: _pageController,
                  physics: const NeverScrollableScrollPhysics(),
                  onPageChanged: (i) => setState(() => _currentPage = i),
                  itemCount: totalPages,
                  itemBuilder: (context, i) {
                    if (i < holes.length) {
                      final hole = holes[i];
                      return _HolePage(
                        hole: hole,
                        grossScore: _grossScores[hole.holeNumber],
                        onScoreChanged: (s) =>
                            _setScore(hole.holeNumber, s),
                        onNext: () {
                          ref
                              .read(scoreEntryNotifierProvider(widget.roundId)
                                  .notifier)
                              .saveHole(
                                roundId: widget.roundId,
                                holeNumber: hole.holeNumber,
                                grossStrokes: _grossScores[hole.holeNumber] ??
                                    hole.par + hole.strokesReceived + 2,
                              );
                          _nextPage();
                        },
                        onBack: i > 0 ? _previousPage : null,
                      );
                    } else {
                      return _ConfirmationPage(
                        roundId: widget.roundId,
                        holes: holes,
                        grossScores: _grossScores,
                        onBack: _previousPage,
                        onSubmit: () async {
                          await ref
                              .read(scoreEntryNotifierProvider(widget.roundId)
                                  .notifier)
                              .submitAll(widget.roundId);
                          if (context.mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                  content: Text('Scores submitted!')),
                            );
                            context.go('/rounds/${widget.roundId}');
                          }
                        },
                      );
                    }
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _ProgressBar extends StatelessWidget {
  const _ProgressBar({required this.current, required this.total});

  final int current;
  final int total;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        LinearProgressIndicator(
          value: current < total ? (current + 1) / total : 1.0,
          minHeight: 6,
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                current < total ? 'Hole ${current + 1} of $total' : 'Review',
                style: theme.textTheme.labelSmall,
              ),
              Text(
                '${_completedCount(current)} entered',
                style: theme.textTheme.labelSmall,
              ),
            ],
          ),
        ),
      ],
    );
  }

  int _completedCount(int page) => page < 0 ? 0 : page;
}

class _HolePage extends StatelessWidget {
  const _HolePage({
    required this.hole,
    required this.grossScore,
    required this.onScoreChanged,
    required this.onNext,
    this.onBack,
  });

  final ScoreEntryHole hole;
  final int? grossScore;
  final ValueChanged<int> onScoreChanged;
  final VoidCallback onNext;
  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final score = grossScore ?? hole.par;
    final net = score - hole.strokesReceived;
    final points = StablefordCalculator.points(net, hole.par);

    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // Hole info header
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: [
              _InfoCell(label: 'Hole', value: '${hole.holeNumber}', theme: theme),
              _InfoCell(label: 'Par', value: '${hole.par}', theme: theme),
              _InfoCell(label: 'SI', value: '${hole.strokeIndex}', theme: theme),
              _InfoCell(
                label: 'Strokes',
                value: '+${hole.strokesReceived}',
                theme: theme,
                highlight: hole.strokesReceived > 0,
              ),
            ],
          ),
          const Spacer(),

          // Score stepper
          Text('Gross Score', style: theme.textTheme.titleMedium),
          const SizedBox(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _StepperButton(
                icon: Icons.remove,
                onPressed: score > 1
                    ? () {
                        HapticFeedback.lightImpact();
                        onScoreChanged(score - 1);
                      }
                    : null,
              ),
              const SizedBox(width: 32),
              Text(
                '$score',
                style: theme.textTheme.displayMedium
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              const SizedBox(width: 32),
              _StepperButton(
                icon: Icons.add,
                onPressed: () {
                  HapticFeedback.lightImpact();
                  onScoreChanged(score + 1);
                },
              ),
            ],
          ),

          const SizedBox(height: 24),

          // Live stableford result
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
            decoration: BoxDecoration(
              color: theme.colorScheme.surfaceContainerHighest,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Column(
              children: [
                Text('Net: $net  ·  Stableford', style: theme.textTheme.bodySmall),
                const SizedBox(height: 6),
                Text(
                  '$points ${_pointsLabel(points)}',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: _pointsColor(points),
                  ),
                ),
              ],
            ),
          ),

          const Spacer(),

          // Navigation buttons
          Row(
            children: [
              if (onBack != null)
                Expanded(
                  child: OutlinedButton(
                    onPressed: onBack,
                    child: const Text('Back'),
                  ),
                ),
              if (onBack != null) const SizedBox(width: 12),
              Expanded(
                flex: 2,
                child: FilledButton(
                  onPressed: () {
                    HapticFeedback.heavyImpact();
                    onNext();
                  },
                  child: const Text('Save & Next'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  String _pointsLabel(int pts) => switch (pts) {
        0 => 'pts',
        1 => 'pt — Bogey',
        2 => 'pts — Par',
        3 => 'pts — Birdie',
        4 => 'pts — Eagle',
        5 => 'pts — Albatross',
        _ => 'pts',
      };

  Color _pointsColor(int pts) => switch (pts) {
        0 => Colors.grey,
        1 => Colors.black87,
        2 => const Color(0xFF1B5E20),
        3 => const Color(0xFF558B2F),
        4 => const Color(0xFFF9A825),
        _ => Colors.purple,
      };
}

class _StepperButton extends StatelessWidget {
  const _StepperButton({required this.icon, this.onPressed});

  final IconData icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 56,
      height: 56,
      child: FilledButton.tonal(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          padding: EdgeInsets.zero,
          shape: const CircleBorder(),
        ),
        child: Icon(icon, size: 28),
      ),
    );
  }
}

class _InfoCell extends StatelessWidget {
  const _InfoCell({
    required this.label,
    required this.value,
    required this.theme,
    this.highlight = false,
  });

  final String label;
  final String value;
  final ThemeData theme;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: theme.textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.bold,
            color: highlight ? theme.colorScheme.primary : null,
          ),
        ),
        Text(label, style: theme.textTheme.labelSmall),
      ],
    );
  }
}

class _ConfirmationPage extends StatelessWidget {
  const _ConfirmationPage({
    required this.roundId,
    required this.holes,
    required this.grossScores,
    required this.onBack,
    required this.onSubmit,
  });

  final int roundId;
  final List<ScoreEntryHole> holes;
  final Map<int, int> grossScores;
  final VoidCallback onBack;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final holeScores = holes.map((h) {
      final gross = grossScores[h.holeNumber] ?? h.par;
      final net = gross - h.strokesReceived;
      return _ConfirmHoleRow(
        holeNumber: h.holeNumber,
        par: h.par,
        strokeIndex: h.strokeIndex,
        grossStrokes: gross,
        handicapStrokes: h.strokesReceived,
        netStrokes: net,
        stablefordPoints: StablefordCalculator.points(net, h.par),
        isMaxScore: StablefordCalculator.isNetDoubleBogey(
            gross, h.par, h.strokesReceived),
      );
    }).toList();

    final total = holeScores.fold<int>(
        0, (sum, h) => sum + h.stablefordPoints);

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          Text('Review Scorecard', style: theme.textTheme.titleLarge),
          const SizedBox(height: 4),
          Text(
            'Total: $total Stableford pts',
            style: theme.textTheme.titleMedium?.copyWith(
              color: theme.colorScheme.primary,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: SingleChildScrollView(
              child: ScorecardTable(
                holes: holeScores.map((h) => rounds_models.HoleScore(
                      holeNumber: h.holeNumber,
                      par: h.par,
                      strokeIndex: h.strokeIndex,
                      grossStrokes: h.grossStrokes,
                      handicapStrokes: h.handicapStrokes,
                      netStrokes: h.netStrokes,
                      stablefordPoints: h.stablefordPoints,
                      isMaxScore: h.isMaxScore,
                    )).toList(),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: onBack,
                  child: const Text('Back'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                flex: 2,
                child: FilledButton(
                  onPressed: onSubmit,
                  child: const Text('Submit Scores'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

}

class _ConfirmHoleRow {
  const _ConfirmHoleRow({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
    required this.grossStrokes,
    required this.handicapStrokes,
    required this.netStrokes,
    required this.stablefordPoints,
    required this.isMaxScore,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;
  final int grossStrokes;
  final int handicapStrokes;
  final int netStrokes;
  final int stablefordPoints;
  final bool isMaxScore;
}
