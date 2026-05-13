import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/providers.dart';
import '../models/models.dart';

class LeaderboardTab extends ConsumerWidget {
  const LeaderboardTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flightsAsync = ref.watch(flightsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Leaderboard'),
        leading: IconButton(
          icon: const Icon(Icons.menu),
          onPressed: () => Scaffold.of(context).openDrawer(),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(flightsProvider);
        },
        child: flightsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load flights.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () => ref.invalidate(flightsProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (flights) {
            if (flights.isEmpty) {
              return const Center(
                child: Text(
                  'No flights have been created for this season yet.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: flights.length,
              itemBuilder: (context, index) =>
                  _FlightLeaderboardPreview(flight: flights[index]),
            );
          },
        ),
      ),
    );
  }
}

class _FlightLeaderboardPreview extends ConsumerWidget {
  const _FlightLeaderboardPreview({required this.flight});

  final Flight flight;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = FlightStandingsParams(
      flightId: flight.id.toString(),
      halfId: flight.seasonId.toString(),
    );
    final standingsAsync = ref.watch(flightStandingsProvider(params));

    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          InkWell(
            onTap: () => context.push(
              '/flights/${flight.id}/leaderboard?halfId=${flight.seasonId}',
            ),
            borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          flight.name,
                          style: const TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 16,
                            color: Color(0xFF111827),
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${flight.playerCount} players',
                          style: const TextStyle(
                            fontSize: 13,
                            color: Color(0xFF6B7280),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: Color(0xFF9CA3AF)),
                ],
              ),
            ),
          ),
          const Divider(height: 1),
          standingsAsync.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
            ),
            error: (_, __) => const SizedBox.shrink(),
            data: (standings) {
              if (standings.isEmpty) {
                return const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text(
                    'No standings available yet.',
                    style: TextStyle(color: Color(0xFF6B7280), fontSize: 13),
                  ),
                );
              }
              final topPlayers = standings.take(5).toList();
              return Column(
                children: [
                  ...topPlayers.asMap().entries.map((entry) {
                    final index = entry.key;
                    final standing = entry.value;
                    return _StandingPreviewRow(
                      standing: standing,
                      position: index + 1,
                    );
                  }),
                  if (standings.length > 5)
                    Padding(
                      padding: const EdgeInsets.all(12),
                      child: Text(
                        '+${standings.length - 5} more players',
                        style: const TextStyle(
                          fontSize: 12,
                          color: Color(0xFF6B7280),
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _StandingPreviewRow extends StatelessWidget {
  const _StandingPreviewRow({required this.standing, required this.position});

  final Standing standing;
  final int position;

  Color? get _positionColor {
    return switch (position) {
      1 => const Color(0xFFFFD700),
      2 => const Color(0xFFC0C0C0),
      3 => const Color(0xFFCD7F32),
      _ => null,
    };
  }

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => context.push('/players/${standing.playerId}'),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          border: Border(bottom: BorderSide(color: Colors.grey.shade200)),
        ),
        child: Row(
          children: [
            Container(
              width: 28,
              height: 28,
              decoration: BoxDecoration(
                color: _positionColor ?? const Color(0xFFF3F4F6),
                shape: BoxShape.circle,
              ),
              child: Center(
                child: Text(
                  '$position',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.bold,
                    color: position <= 3
                        ? const Color(0xFF111827)
                        : const Color(0xFF6B7280),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                standing.playerFullName,
                style: const TextStyle(
                  fontWeight: FontWeight.w500,
                  fontSize: 14,
                ),
              ),
            ),
            Text(
              '${standing.totalPoints} pts',
              style: const TextStyle(
                fontWeight: FontWeight.w600,
                color: Color(0xFF1a5c38),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
