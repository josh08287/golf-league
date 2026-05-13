import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/providers.dart';
import '../models/models.dart';

class PlayersScreen extends ConsumerWidget {
  const PlayersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final playersAsync = ref.watch(playersProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Players'),
        leading: IconButton(
          icon: const Icon(Icons.menu),
          onPressed: () => Scaffold.of(context).openDrawer(),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(playersProvider);
        },
        child: playersAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load players.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () => ref.invalidate(playersProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (players) {
            if (players.isEmpty) {
              return const Center(
                child: Text(
                  'No players yet.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: players.length,
              itemBuilder: (context, index) => _PlayerCard(player: players[index]),
            );
          },
        ),
      ),
    );
  }
}

class _PlayerCard extends StatelessWidget {
  const _PlayerCard({required this.player});

  final Player player;

  String _formatHandicap(double? handicap) {
    if (handicap == null) return '—';
    final h18 = handicap.toStringAsFixed(1);
    final h9 = (handicap / 2).toStringAsFixed(1);
    return '$h18 / $h9';
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: InkWell(
        onTap: () => context.push('/players/${player.id}'),
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      player.fullName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 15,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      player.flightName ?? 'No flight assigned',
                      style: TextStyle(
                        fontSize: 13,
                        color: player.flightName != null
                            ? const Color(0xFF6B7280)
                            : const Color(0xFF9CA3AF),
                        fontStyle: player.flightName == null ? FontStyle.italic : null,
                      ),
                    ),
                  ],
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    _formatHandicap(player.currentHandicap),
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 14,
                      color: Color(0xFF1a5c38),
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '18 / 9 HCP',
                    style: TextStyle(
                      fontSize: 10,
                      color: Colors.grey[600],
                    ),
                  ),
                ],
              ),
              const SizedBox(width: 8),
              const Icon(Icons.chevron_right, color: Color(0xFF9CA3AF)),
            ],
          ),
        ),
      ),
    );
  }
}
