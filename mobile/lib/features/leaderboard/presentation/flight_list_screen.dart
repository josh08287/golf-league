import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../domain/models.dart';
import 'providers.dart';

class FlightListScreen extends ConsumerWidget {
  const FlightListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flightsAsync = ref.watch(flightsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Flights')),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(flightsProvider),
        child: flightsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(child: Text('Error: $e')),
          data: (flights) => flights.isEmpty
              ? const Center(child: Text('No flights found'))
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: flights.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) => _FlightCard(flight: flights[i]),
                ),
        ),
      ),
    );
  }
}

class _FlightCard extends StatelessWidget {
  const _FlightCard({required this.flight});

  final Flight flight;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    String handicapRange = '';
    if (flight.minHandicap != null && flight.maxHandicap != null) {
      handicapRange =
          'Handicap ${flight.minHandicap!.toStringAsFixed(1)} – ${flight.maxHandicap!.toStringAsFixed(1)}';
    } else if (flight.maxHandicap != null) {
      handicapRange = 'Up to ${flight.maxHandicap!.toStringAsFixed(1)} HC';
    } else if (flight.minHandicap != null) {
      handicapRange = '${flight.minHandicap!.toStringAsFixed(1)}+ HC';
    }

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.go('/leaderboard/${flight.id}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(
                  flight.name.substring(0, 1),
                  style: theme.textTheme.titleLarge?.copyWith(
                    color: theme.colorScheme.onPrimaryContainer,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(flight.name, style: theme.textTheme.titleMedium),
                    if (handicapRange.isNotEmpty)
                      Text(
                        handicapRange,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    if (flight.description != null)
                      Text(
                        flight.description!,
                        style: theme.textTheme.bodySmall,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}
