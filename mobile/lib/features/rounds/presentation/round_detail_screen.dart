import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../domain/models.dart';
import 'providers.dart';
import 'widgets/scorecard_table.dart';

class RoundDetailScreen extends ConsumerWidget {
  const RoundDetailScreen({super.key, required this.roundId});

  final int roundId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final roundAsync = ref.watch(roundDetailProvider(roundId));

    return roundAsync.when(
      loading: () => Scaffold(
        appBar: AppBar(),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (e, _) => Scaffold(
        appBar: AppBar(),
        body: Center(child: Text('Error: $e')),
      ),
      data: (round) => _RoundDetailView(round: round),
    );
  }
}

class _RoundDetailView extends StatelessWidget {
  const _RoundDetailView({required this.round});

  final RoundDetail round;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dateStr = round.playedDate != null
        ? DateFormat('EEEE d MMMM y').format(round.playedDate!)
        : DateFormat('EEEE d MMMM y').format(round.scheduledDate);

    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Round ${round.roundNumber}'),
              Text(
                round.courseName,
                style: theme.textTheme.bodySmall,
              ),
            ],
          ),
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Leaderboard'),
              Tab(text: 'Scorecard'),
            ],
          ),
        ),
        body: Column(
          children: [
            Container(
              color: theme.colorScheme.surfaceContainerLow,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  const Icon(Icons.calendar_today, size: 14),
                  const SizedBox(width: 6),
                  Text(dateStr, style: theme.textTheme.bodySmall),
                  if (round.weatherConditions != null) ...[
                    const SizedBox(width: 12),
                    const Icon(Icons.cloud, size: 14),
                    const SizedBox(width: 4),
                    Text(
                      round.weatherConditions!,
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ],
              ),
            ),
            Expanded(
              child: TabBarView(
                children: [
                  _LeaderboardTab(round: round),
                  _ScorecardTab(roundId: round.id, participants: round.participants),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LeaderboardTab extends StatelessWidget {
  const _LeaderboardTab({required this.round});

  final RoundDetail round;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final sorted = [...round.participants]
      ..sort((a, b) => (a.rank ?? 999).compareTo(b.rank ?? 999));

    if (sorted.isEmpty) {
      return const Center(child: Text('No scores recorded yet'));
    }

    return ListView.builder(
      itemCount: sorted.length,
      itemBuilder: (context, i) {
        final p = sorted[i];
        return ListTile(
          leading: CircleAvatar(child: Text('${p.rank ?? '-'}')),
          title: Text(p.playerName),
          subtitle: p.courseHandicap != null
              ? Text('HC ${p.courseHandicap!.toStringAsFixed(1)}')
              : null,
          trailing: p.totalStablefordPoints != null
              ? Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      '${p.totalStablefordPoints}',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text('pts', style: theme.textTheme.labelSmall),
                  ],
                )
              : const Text('-'),
          onTap: () =>
              context.go('/rounds/${round.id}/player/${p.playerId}'),
        );
      },
    );
  }
}

class _ScorecardTab extends ConsumerWidget {
  const _ScorecardTab({
    required this.roundId,
    required this.participants,
  });

  final int roundId;
  final List<RoundParticipantSummary> participants;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (participants.isEmpty) {
      return const Center(child: Text('No scorecards available'));
    }

    // Show first participant's scorecard by default
    final first = participants.first;
    final scorecardAsync = ref.watch(
      playerScorecardProvider((roundId: roundId, playerId: first.playerId)),
    );

    return scorecardAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(child: Text('Error: $e')),
      data: (scorecard) => SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              scorecard.playerName,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            ScorecardTable(holes: scorecard.holes),
          ],
        ),
      ),
    );
  }
}
