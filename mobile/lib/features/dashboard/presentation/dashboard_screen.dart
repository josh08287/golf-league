import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../leaderboard/presentation/providers.dart' show connectivityProvider;
import '../domain/models.dart';
import 'providers.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(dashboardProvider);
    final connectivity = ref.watch(connectivityProvider);

    final isOffline = connectivity.whenOrNull(
          data: (results) => results.every(
            (r) => r == ConnectivityResult.none,
          ),
        ) ??
        false;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Golf League'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(dashboardProvider),
          ),
        ],
      ),
      body: Column(
        children: [
          if (isOffline)
            MaterialBanner(
              content: const Text('You are offline — showing cached data'),
              leading: const Icon(Icons.wifi_off, color: Colors.amber),
              backgroundColor: Colors.amber.shade100,
              actions: [
                TextButton(
                  onPressed: () => ref.invalidate(dashboardProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async => ref.invalidate(dashboardProvider),
              child: dashboardAsync.when(
                loading: () => const _DashboardSkeleton(),
                error: (e, _) => _ErrorView(
                  message: e.toString(),
                  onRetry: () => ref.invalidate(dashboardProvider),
                ),
                data: (data) => _DashboardContent(data: data),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DashboardContent extends StatelessWidget {
  const _DashboardContent({required this.data});

  final DashboardData data;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (data.latestRound != null) ...[
          _LatestRoundCard(round: data.latestRound!),
          const SizedBox(height: 16),
        ],
        ...data.flightSummaries.map(
          (f) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: _FlightSummaryCard(summary: f),
          ),
        ),
      ],
    );
  }
}

class _LatestRoundCard extends StatelessWidget {
  const _LatestRoundCard({required this.round});

  final LatestRoundSummary round;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dateStr = DateFormat('EEE d MMM y').format(round.playedDate);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.flag_outlined, size: 18),
                const SizedBox(width: 6),
                Text('Latest Round', style: theme.textTheme.labelMedium),
              ],
            ),
            const SizedBox(height: 8),
            Text(round.courseName, style: theme.textTheme.titleMedium),
            Text(dateStr, style: theme.textTheme.bodySmall),
            if (round.roundWinnerName != null) ...[
              const Divider(height: 20),
              Row(
                children: [
                  const Icon(Icons.emoji_events, size: 16, color: Colors.amber),
                  const SizedBox(width: 6),
                  Text(
                    '${round.roundWinnerName} — ${round.roundWinnerPoints} pts',
                    style: theme.textTheme.bodyMedium,
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _FlightSummaryCard extends StatelessWidget {
  const _FlightSummaryCard({required this.summary});

  final FlightSummary summary;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(summary.flightName, style: theme.textTheme.titleMedium),
                TextButton(
                  onPressed: () =>
                      context.go('/leaderboard/${summary.flightId}'),
                  child: const Text('View all'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (summary.topThree.isEmpty)
              Text('No scores yet', style: theme.textTheme.bodySmall)
            else
              ...summary.topThree.map(
                (entry) => _TopEntryRow(entry: entry),
              ),
          ],
        ),
      ),
    );
  }
}

class _TopEntryRow extends StatelessWidget {
  const _TopEntryRow({required this.entry});

  final FlightTopEntry entry;

  static const _medals = ['🥇', '🥈', '🥉'];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final medal = entry.rank <= 3 ? _medals[entry.rank - 1] : '${entry.rank}.';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          SizedBox(width: 28, child: Text(medal)),
          Expanded(
            child: Text(entry.playerName, style: theme.textTheme.bodyMedium),
          ),
          Text(
            '${entry.totalPoints} pts',
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    );
  }
}

class _DashboardSkeleton extends StatelessWidget {
  const _DashboardSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _SkeletonCard(height: 100),
        const SizedBox(height: 12),
        _SkeletonCard(height: 140),
        const SizedBox(height: 12),
        _SkeletonCard(height: 140),
      ],
    );
  }
}

class _SkeletonCard extends StatefulWidget {
  const _SkeletonCard({required this.height});

  final double height;

  @override
  State<_SkeletonCard> createState() => _SkeletonCardState();
}

class _SkeletonCardState extends State<_SkeletonCard>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _animation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      duration: const Duration(milliseconds: 1200),
      vsync: this,
    )..repeat(reverse: true);
    _animation = Tween<double>(begin: 0.4, end: 0.8).animate(_controller);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _animation,
      builder: (_, _) => Container(
        height: widget.height,
        decoration: BoxDecoration(
          color: Colors.grey.withOpacity(_animation.value),
          borderRadius: BorderRadius.circular(12),
        ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.red),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}
