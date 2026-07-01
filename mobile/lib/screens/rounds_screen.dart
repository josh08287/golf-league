import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../api/providers.dart';
import '../models/models.dart';

class RoundsScreen extends ConsumerWidget {
  const RoundsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final roundsAsync = ref.watch(roundsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Rounds'),
        leading: IconButton(
          icon: const Icon(Icons.menu),
          onPressed: () => Scaffold.of(context).openDrawer(),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(roundsProvider);
        },
        child: roundsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Could not load rounds.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () => ref.invalidate(roundsProvider),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (rounds) {
            if (rounds.isEmpty) {
              return const Center(
                child: Text(
                  'No rounds scheduled yet.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: rounds.length,
              itemBuilder: (context, index) => _RoundCard(round: rounds[index]),
            );
          },
        ),
      ),
    );
  }
}

class _RoundCard extends StatelessWidget {
  const _RoundCard({required this.round});

  final Round round;

  String _getStatusLabel(RoundStatus status) {
    return switch (status) {
      RoundStatus.scheduled => 'Scheduled',
      RoundStatus.inProgress => 'In Progress',
      RoundStatus.pendingFinalization => 'Pending',
      RoundStatus.finalized => 'Finalized',
      RoundStatus.cancelled => 'Cancelled',
    };
  }

  Color _getStatusColor(RoundStatus status) {
    return switch (status) {
      RoundStatus.scheduled => Colors.blue,
      RoundStatus.inProgress => Colors.orange,
      RoundStatus.pendingFinalization => Colors.orange,
      RoundStatus.finalized => Colors.green,
      RoundStatus.cancelled => Colors.red,
    };
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: InkWell(
        onTap: () => context.push('/rounds/${round.id}'),
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      round.courseName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 16,
                        color: Color(0xFF111827),
                      ),
                    ),
                  ),
                  if (round.isTournament)
                    Container(
                      margin: const EdgeInsets.only(right: 6),
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.amber.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: const Text(
                        '🏆 Tournament',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF92400E),
                        ),
                      ),
                    ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: _getStatusColor(round.status).withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      _getStatusLabel(round.status),
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                        color: _getStatusColor(round.status),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                '${DateFormat('MMM d, y').format(round.scheduledDate)} · Week ${round.weekNumber} · ${round.flightName}',
                style: const TextStyle(
                  fontSize: 13,
                  color: Color(0xFF6B7280),
                ),
              ),
              if (round.participantCount > 0) ...[
                const SizedBox(height: 8),
                Text(
                  '${round.participantCount} participants',
                  style: TextStyle(
                    fontSize: 12,
                    color: Colors.grey[600],
                  ),
                ),
              ],
              const SizedBox(height: 12),
              Row(
                children: [
                  const Icon(Icons.chevron_right, size: 16, color: Color(0xFF1a5c38)),
                  Text(
                    'View Details',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: const Color(0xFF1a5c38),
                    ),
                  ),
                  if (round.isTournament) ...[
                    const Spacer(),
                    GestureDetector(
                      onTap: () => context
                          .push('/rounds/${round.id}/tournament-results'),
                      child: const Text(
                        'Tournament Results',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF92400E),
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
