import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/models.dart';
import 'providers.dart';
import 'widgets/scorecard_table.dart';

class PlayerScorecardScreen extends ConsumerWidget {
  const PlayerScorecardScreen({
    super.key,
    required this.roundId,
    required this.playerId,
  });

  final int roundId;
  final int playerId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final scorecardAsync = ref.watch(
      playerScorecardProvider((roundId: roundId, playerId: playerId)),
    );

    return Scaffold(
      appBar: AppBar(title: const Text('Scorecard')),
      body: scorecardAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('Error: $e')),
        data: (scorecard) => SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                scorecard.playerName,
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              Text(
                'Course HC: ${scorecard.courseHandicap.toStringAsFixed(1)}',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 16),
              ScorecardTable(holes: scorecard.holes),
              const SizedBox(height: 16),
              _TotalsRow(scorecard: scorecard),
            ],
          ),
        ),
      ),
    );
  }
}

class _TotalsRow extends StatelessWidget {
  const _TotalsRow({required this.scorecard});

  final PlayerScorecard scorecard;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            _StatCell(
              label: 'Gross',
              value: scorecard.totalGross?.toString() ?? '-',
              theme: theme,
            ),
            _StatCell(
              label: 'Net',
              value: scorecard.totalNet?.toString() ?? '-',
              theme: theme,
            ),
            _StatCell(
              label: 'Stableford',
              value: scorecard.totalStableford?.toString() ?? '-',
              theme: theme,
              highlight: true,
            ),
          ],
        ),
      ),
    );
  }
}

class _StatCell extends StatelessWidget {
  const _StatCell({
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
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.bold,
            color: highlight ? theme.colorScheme.primary : null,
          ),
        ),
        Text(label, style: theme.textTheme.labelSmall),
      ],
    );
  }
}
