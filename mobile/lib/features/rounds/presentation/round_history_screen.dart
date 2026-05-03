import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../domain/models.dart';
import 'providers.dart';

class RoundHistoryScreen extends ConsumerWidget {
  const RoundHistoryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final roundsAsync = ref.watch(roundsListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Rounds')),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(roundsListProvider),
        child: roundsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(child: Text('Error: $e')),
          data: (rounds) => rounds.isEmpty
              ? const Center(child: Text('No rounds yet'))
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: rounds.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (context, i) => _RoundCard(round: rounds[i]),
                ),
        ),
      ),
    );
  }
}

class _RoundCard extends StatelessWidget {
  const _RoundCard({required this.round});

  final Round round;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isFuture = round.status == 'scheduled';
    final dateStr = DateFormat('EEE d MMM y').format(round.scheduledDate);

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: isFuture
            ? null
            : () => context.go('/rounds/${round.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              if (isFuture)
                const Icon(Icons.lock_outline, size: 32, color: Colors.grey)
              else
                const Icon(Icons.golf_course, size: 32),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Round ${round.roundNumber}',
                      style: theme.textTheme.titleSmall,
                    ),
                    Text(round.courseName, style: theme.textTheme.bodyMedium),
                    Text(
                      dateStr,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              _StatusChip(status: round.status),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      'finalized' => ('Final', Colors.green),
      'in_progress' => ('Live', Colors.blue),
      'scheduled' => ('Upcoming', Colors.grey),
      'cancelled' => ('Cancelled', Colors.red),
      _ => (status, Colors.grey),
    };

    return Chip(
      label: Text(label),
      labelStyle: TextStyle(color: color, fontSize: 11),
      backgroundColor: color.withOpacity(0.12),
      side: BorderSide(color: color.withOpacity(0.4)),
      padding: EdgeInsets.zero,
      visualDensity: VisualDensity.compact,
    );
  }
}
