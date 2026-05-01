import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/models.dart';
import 'providers.dart';

class FlightLeaderboardScreen extends ConsumerWidget {
  const FlightLeaderboardScreen({super.key, required this.flightId});

  final int flightId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final standingsAsync = ref.watch(standingsProvider(flightId));

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(standingsProvider(flightId)),
        child: standingsAsync.when(
          loading: () =>
              const CustomScrollView(slivers: [_LoadingSliver()]),
          error: (e, _) => CustomScrollView(
            slivers: [
              SliverFillRemaining(
                child: Center(child: Text('Error: $e')),
              ),
            ],
          ),
          data: (entries) => CustomScrollView(
            slivers: [
              SliverAppBar(
                pinned: true,
                title: const Text('Standings'),
                bottom: PreferredSize(
                  preferredSize: const Size.fromHeight(24),
                  child: Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      '${entries.length} players',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                ),
              ),
              SliverList.builder(
                itemCount: entries.length,
                itemBuilder: (context, i) => _LeaderboardEntryTile(
                  entry: entries[i],
                  index: i,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LoadingSliver extends StatelessWidget {
  const _LoadingSliver();

  @override
  Widget build(BuildContext context) {
    return const SliverFillRemaining(
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _LeaderboardEntryTile extends StatelessWidget {
  const _LeaderboardEntryTile({
    required this.entry,
    required this.index,
  });

  final LeaderboardEntry entry;
  final int index;

  static const _podiumColors = [
    Color(0xFFFFF9C4), // gold tint
    Color(0xFFF5F5F5), // silver tint
    Color(0xFFEFEBE9), // bronze tint
  ];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final bgColor = index < 3 ? _podiumColors[index] : null;

    final trend = _trendWidget(entry);

    return Container(
      color: bgColor,
      child: ListTile(
        leading: _RankBadge(rank: entry.currentRank),
        title: Text(entry.playerName, style: theme.textTheme.bodyLarge),
        subtitle: Text(
          'HC ${entry.currentHandicap.toStringAsFixed(1)} · '
          '${entry.roundsPlayed} rounds',
          style: theme.textTheme.bodySmall,
        ),
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            trend,
            const SizedBox(width: 8),
            Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  '${entry.totalStablefordPoints}',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Text('pts', style: theme.textTheme.labelSmall),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _trendWidget(LeaderboardEntry entry) {
    if (entry.previousRank == null) return const SizedBox(width: 20);
    final diff = entry.previousRank! - entry.currentRank;
    if (diff > 0) {
      return const Icon(Icons.arrow_upward, color: Colors.green, size: 18);
    } else if (diff < 0) {
      return const Icon(Icons.arrow_downward, color: Colors.red, size: 18);
    } else {
      return const Icon(Icons.remove, color: Colors.grey, size: 18);
    }
  }
}

class _RankBadge extends StatelessWidget {
  const _RankBadge({required this.rank});

  final int rank;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      width: 36,
      height: 36,
      decoration: BoxDecoration(
        color: theme.colorScheme.primaryContainer,
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(
        '$rank',
        style: theme.textTheme.labelLarge?.copyWith(
          color: theme.colorScheme.onPrimaryContainer,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }
}
