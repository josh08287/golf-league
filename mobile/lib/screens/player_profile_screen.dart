import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../api/providers.dart';
import '../models/models.dart';

class PlayerProfileScreen extends ConsumerWidget {
  const PlayerProfileScreen({super.key, required this.playerId});

  final int playerId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final playerAsync = ref.watch(playerDetailProvider(playerId));
    final handicapHistoryAsync = ref.watch(
      playerHandicapHistoryProvider(playerId),
    );
    final roundsAsync = ref.watch(playerRoundsProvider(playerId));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Player Profile'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(playerDetailProvider(playerId));
          ref.invalidate(playerHandicapHistoryProvider(playerId));
          ref.invalidate(playerRoundsProvider(playerId));
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            playerAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) =>
                  const _ErrorCard('Could not load player profile.'),
              data: (player) => _PlayerHeader(player: player),
            ),
            const SizedBox(height: 24),
            const Text(
              'Handicap History',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 12),
            handicapHistoryAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) =>
                  const _ErrorCard('Could not load handicap history.'),
              data: (history) => _HandicapHistoryList(history: history),
            ),
            const SizedBox(height: 24),
            const Text(
              'Past Rounds',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 12),
            roundsAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) => const _ErrorCard('Could not load rounds.'),
              data: (rounds) => _RoundsList(rounds: rounds),
            ),
          ],
        ),
      ),
    );
  }
}

class _PlayerHeader extends StatelessWidget {
  const _PlayerHeader({required this.player});

  final Player player;

  String _formatHandicap(double? handicap) {
    if (handicap == null) return '—';
    return handicap.toStringAsFixed(1);
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            Row(
              children: [
                Container(
                  width: 64,
                  height: 64,
                  decoration: BoxDecoration(
                    color: const Color(0xFF1a5c38),
                    shape: BoxShape.circle,
                  ),
                  child: Center(
                    child: Text(
                      player.fullName
                          .split(' ')
                          .map((n) => n[0])
                          .take(2)
                          .join(),
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 24,
                        fontWeight: FontWeight.bold,
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
                        player.fullName,
                        style: const TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        player.flightName ?? 'No flight assigned',
                        style: TextStyle(
                          fontSize: 14,
                          color: player.flightName != null
                              ? const Color(0xFF6B7280)
                              : const Color(0xFF9CA3AF),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            const Divider(),
            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: [
                _StatBox(
                  label: '18-Hole HCP',
                  value: _formatHandicap(player.currentHandicap),
                ),
                _StatBox(
                  label: '9-Hole HCP',
                  value: player.currentHandicap != null
                      ? _formatHandicap(player.currentHandicap! / 2)
                      : '—',
                ),
                _StatBox(
                  label: 'Status',
                  value: player.isActive ? 'Active' : 'Inactive',
                  valueColor: player.isActive ? Colors.green : Colors.grey,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StatBox extends StatelessWidget {
  const _StatBox({required this.label, required this.value, this.valueColor});

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: TextStyle(
            fontSize: 20,
            fontWeight: FontWeight.bold,
            color: valueColor ?? const Color(0xFF1a5c38),
          ),
        ),
        const SizedBox(height: 4),
        Text(
          label,
          style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
        ),
      ],
    );
  }
}

class _HandicapHistoryList extends StatelessWidget {
  const _HandicapHistoryList({required this.history});

  final List<HandicapHistoryEntry> history;

  @override
  Widget build(BuildContext context) {
    if (history.isEmpty) {
      return const Card(
        elevation: 0,
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'No handicap history recorded yet.',
            style: TextStyle(color: Color(0xFF6B7280)),
          ),
        ),
      );
    }

    return Column(
      children: history
          .map((entry) => _HandicapHistoryRow(entry: entry))
          .toList(),
    );
  }
}

class _HandicapHistoryRow extends StatelessWidget {
  const _HandicapHistoryRow({required this.entry});

  final HandicapHistoryEntry entry;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    DateFormat('MMM d, y').format(entry.effectiveDate),
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 14,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    entry.source,
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
                  entry.handicapIndex.toStringAsFixed(1),
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                    color: Color(0xFF1a5c38),
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  entry.nineHoleHandicapIndex.toStringAsFixed(1),
                  style: TextStyle(fontSize: 12, color: Colors.grey[600]),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _RoundsList extends StatelessWidget {
  const _RoundsList({required this.rounds});

  final List<PlayerRoundSummary> rounds;

  @override
  Widget build(BuildContext context) {
    if (rounds.isEmpty) {
      return const Card(
        elevation: 0,
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'No rounds played yet.',
            style: TextStyle(color: Color(0xFF6B7280)),
          ),
        ),
      );
    }

    return Column(
      children: rounds.map((round) => _RoundRow(round: round)).toList(),
    );
  }
}

class _RoundRow extends StatelessWidget {
  const _RoundRow({required this.round});

  final PlayerRoundSummary round;

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
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: InkWell(
        onTap: () => context.push('/rounds/${round.roundId}'),
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          round.courseName,
                          style: const TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 15,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${DateFormat('MMM d, y').format(round.roundDate)} · Week ${round.weekNumber} · ${round.nineHoleSide} 9',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFF6B7280),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: _getStatusColor(
                        round.status,
                      ).withValues(alpha: 0.1),
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
              if (round.totalNetStablefordPoints != null) ...[
                const SizedBox(height: 12),
                const Divider(),
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceAround,
                  children: [
                    _RoundStat(
                      label: 'Gross',
                      value: '${round.totalGrossStrokes ?? '—'}',
                    ),
                    _RoundStat(
                      label: 'Net',
                      value: '${round.totalNetStrokes ?? '—'}',
                    ),
                    _RoundStat(
                      label: 'Points',
                      value: '${round.totalNetStablefordPoints ?? '—'}',
                      isHighlighted: true,
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _RoundStat extends StatelessWidget {
  const _RoundStat({
    required this.label,
    required this.value,
    this.isHighlighted = false,
  });

  final String label;
  final String value;
  final bool isHighlighted;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.bold,
            color: isHighlighted
                ? const Color(0xFF1a5c38)
                : const Color(0xFF111827),
          ),
        ),
        const SizedBox(height: 2),
        Text(
          label,
          style: const TextStyle(fontSize: 11, color: Color(0xFF6B7280)),
        ),
      ],
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard(this.message);

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      color: const Color(0xFFFEF2F2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFFCA5A5)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            const Icon(Icons.error_outline, color: Color(0xFFDC2626)),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                message,
                style: const TextStyle(color: Color(0xFF991B1B)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
