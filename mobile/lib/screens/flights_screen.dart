import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/providers.dart';
import '../models/models.dart';

class FlightsScreen extends ConsumerWidget {
  const FlightsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flightsAsync = ref.watch(flightsProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Flights'),
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
              itemBuilder: (context, index) => _FlightCard(flight: flights[index]),
            );
          },
        ),
      ),
    );
  }
}

class _FlightCard extends StatelessWidget {
  const _FlightCard({required this.flight});

  final Flight flight;

  String get _handicapRange {
    final min = flight.minHandicap;
    final max = flight.maxHandicap;
    if (min == null && max == null) return 'Open';
    if (min != null && max != null) {
      return '${_fmt(min)} – ${_fmt(max)}';
    }
    if (min != null) return '${_fmt(min)}+';
    return 'Up to ${_fmt(max!)}';
  }

  String _fmt(double v) => v == v.truncateToDouble() ? v.toInt().toString() : v.toStringAsFixed(1);

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
        onTap: () => context.push(
          '/flights/${flight.id}/leaderboard?halfId=${flight.halfId ?? flight.seasonId}',
        ),
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
                      flight.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 16,
                        color: Color(0xFF111827),
                      ),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: const Color(0xFFF3F4F6),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      '${flight.playerCount} players',
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w500,
                        color: Color(0xFF374151),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                'Handicap $_handicapRange',
                style: const TextStyle(
                  fontSize: 13,
                  color: Color(0xFF6B7280),
                ),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  const Icon(Icons.chevron_right, size: 16, color: Color(0xFF1a5c38)),
                  Text(
                    'View Leaderboard',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: const Color(0xFF1a5c38),
                    ),
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
