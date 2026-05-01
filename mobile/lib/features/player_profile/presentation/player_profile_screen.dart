import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/models.dart';
import 'providers.dart';

class PlayerProfileScreen extends ConsumerWidget {
  const PlayerProfileScreen({super.key, this.playerId});

  /// If null, shows the currently authenticated player's profile.
  final int? playerId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(playerProfileProvider(playerId));

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('Error: $e')),
        data: (profile) => _ProfileContent(profile: profile),
      ),
    );
  }
}

class _ProfileContent extends ConsumerWidget {
  const _ProfileContent({required this.profile});

  final PlayerProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final historyAsync =
        ref.watch(handicapHistoryProvider(profile.id));

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _AvatarHeader(profile: profile),
        const SizedBox(height: 20),
        _StatsRow(profile: profile),
        const SizedBox(height: 20),
        historyAsync.when(
          loading: () => const SizedBox(
            height: 120,
            child: Center(child: CircularProgressIndicator()),
          ),
          error: (_, __) => const SizedBox.shrink(),
          data: (history) => history.length >= 2
              ? _HandicapChart(history: history)
              : const SizedBox.shrink(),
        ),
      ],
    );
  }
}

class _AvatarHeader extends StatelessWidget {
  const _AvatarHeader({required this.profile});

  final PlayerProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      children: [
        CircleAvatar(
          radius: 36,
          backgroundColor: theme.colorScheme.primaryContainer,
          child: Text(
            profile.initials,
            style: theme.textTheme.headlineMedium?.copyWith(
              color: theme.colorScheme.onPrimaryContainer,
            ),
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(profile.fullName, style: theme.textTheme.headlineSmall),
              if (profile.flightName != null)
                Text(profile.flightName!, style: theme.textTheme.bodyMedium),
              Text(
                'Handicap: ${profile.currentHandicap.toStringAsFixed(1)}',
                style: theme.textTheme.bodyLarge?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: theme.colorScheme.primary,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.profile});

  final PlayerProfile profile;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            _StatCell(label: 'Rounds', value: '${profile.roundsPlayed}'),
            _StatCell(label: 'Total Pts', value: '${profile.totalStablefordPoints}'),
            _StatCell(
              label: 'Avg',
              value: profile.averagePoints != null
                  ? profile.averagePoints!.toStringAsFixed(1)
                  : '-',
            ),
            _StatCell(
              label: 'Best',
              value: profile.bestRoundPoints?.toString() ?? '-',
            ),
          ],
        ),
      ),
    );
  }
}

class _StatCell extends StatelessWidget {
  const _StatCell({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        Text(
          value,
          style: theme.textTheme.titleLarge
              ?.copyWith(fontWeight: FontWeight.bold),
        ),
        Text(label, style: theme.textTheme.labelSmall),
      ],
    );
  }
}

class _HandicapChart extends StatelessWidget {
  const _HandicapChart({required this.history});

  final List<HandicapHistoryEntry> history;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final sorted = [...history]
      ..sort((a, b) => a.effectiveDate.compareTo(b.effectiveDate));

    final first = sorted.first.handicapIndex;
    final last = sorted.last.handicapIndex;
    final isImproving = last < first;
    final lineColor = isImproving ? Colors.green : Colors.amber;

    final spots = sorted
        .asMap()
        .entries
        .map((e) => FlSpot(e.key.toDouble(), e.value.handicapIndex))
        .toList();

    final minY =
        sorted.map((e) => e.handicapIndex).reduce((a, b) => a < b ? a : b) -
            1;
    final maxY =
        sorted.map((e) => e.handicapIndex).reduce((a, b) => a > b ? a : b) +
            1;

    return Card(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 24, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Handicap Trend', style: theme.textTheme.titleSmall),
            const SizedBox(height: 12),
            SizedBox(
              height: 120,
              child: LineChart(
                LineChartData(
                  gridData: const FlGridData(show: false),
                  borderData: FlBorderData(show: false),
                  titlesData: const FlTitlesData(
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(showTitles: true, reservedSize: 30),
                    ),
                    bottomTitles:
                        AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    topTitles:
                        AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    rightTitles:
                        AxisTitles(sideTitles: SideTitles(showTitles: false)),
                  ),
                  minY: minY,
                  maxY: maxY,
                  lineBarsData: [
                    LineChartBarData(
                      spots: spots,
                      isCurved: true,
                      color: lineColor,
                      barWidth: 2.5,
                      dotData: const FlDotData(show: false),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 4),
            Text(
              isImproving ? '▼ Improving' : '▲ Rising',
              style: theme.textTheme.labelSmall?.copyWith(color: lineColor),
            ),
          ],
        ),
      ),
    );
  }
}
