import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/providers.dart';
import '../models/models.dart';

class FlightLeaderboardScreen extends ConsumerWidget {
  const FlightLeaderboardScreen({
    super.key,
    required this.flightId,
    required this.halfId,
  });

  final int flightId;
  final String halfId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = FlightStandingsParams(
      flightId: flightId.toString(),
      halfId: halfId.isEmpty ? flightId.toString() : halfId,
    );
    final standingsAsync = ref.watch(flightStandingsProvider(params));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Leaderboard'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(flightStandingsProvider(params));
        },
        child: standingsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load standings.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () =>
                      ref.invalidate(flightStandingsProvider(params)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (standings) {
            if (standings.isEmpty) {
              return const Center(
                child: Text(
                  'No standings available for this half yet.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: standings.length,
              itemBuilder: (context, index) =>
                  _StandingRow(standing: standings[index], position: index + 1),
            );
          },
        ),
      ),
    );
  }
}

class _StandingRow extends StatelessWidget {
  const _StandingRow({required this.standing, required this.position});

  final Standing standing;
  final int position;

  Color? get _positionColor {
    return switch (position) {
      1 => const Color(0xFFFFD700), // Gold
      2 => const Color(0xFFC0C0C0), // Silver
      3 => const Color(0xFFCD7F32), // Bronze
      _ => null,
    };
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      color: position <= 3
          ? _positionColor?.withValues(alpha: 0.1)
          : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: position <= 3
              ? _positionColor!.withValues(alpha: 0.3)
              : const Color(0xFFE5E7EB),
        ),
      ),
      child: InkWell(
        onTap: () => context.push('/players/${standing.playerId}'),
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: _positionColor ?? const Color(0xFFF3F4F6),
                  shape: BoxShape.circle,
                ),
                child: Center(
                  child: Text(
                    '$position',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      color: position <= 3
                          ? const Color(0xFF111827)
                          : const Color(0xFF6B7280),
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      standing.playerFullName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 15,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${standing.roundsPlayed} rounds · HCP: ${standing.currentHandicapIndex.toStringAsFixed(1)}',
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xFF6B7280),
                      ),
                    ),
                  ],
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    '${standing.totalPoints}',
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 18,
                      color: Color(0xFF1a5c38),
                    ),
                  ),
                  Text(
                    'pts',
                    style: TextStyle(fontSize: 11, color: Colors.grey[600]),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
