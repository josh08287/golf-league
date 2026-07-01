import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/providers.dart';
import '../models/models.dart';

class FlightLeaderboardScreen extends ConsumerStatefulWidget {
  const FlightLeaderboardScreen({
    super.key,
    required this.flightId,
    required this.halfId,
  });

  final int flightId;
  final String halfId;

  @override
  ConsumerState<FlightLeaderboardScreen> createState() =>
      _FlightLeaderboardScreenState();
}

class _FlightLeaderboardScreenState
    extends ConsumerState<FlightLeaderboardScreen> {
  bool _useGross = false;

  FlightStandingsParams get _params => FlightStandingsParams(
    flightId: widget.flightId.toString(),
    halfId: widget.halfId.isEmpty
        ? widget.flightId.toString()
        : widget.halfId,
    useGrossPoints: _useGross,
  );

  @override
  Widget build(BuildContext context) {
    final params = _params;
    final standingsAsync = ref.watch(flightStandingsProvider(params));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Leaderboard'),
        leading: const BackButton(),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: Center(
              child: SegmentedButton<bool>(
                style: const ButtonStyle(
                  visualDensity: VisualDensity.compact,
                ),
                segments: const [
                  ButtonSegment(value: false, label: Text('Net')),
                  ButtonSegment(value: true, label: Text('Gross')),
                ],
                selected: {_useGross},
                onSelectionChanged: (s) =>
                    setState(() => _useGross = s.first),
              ),
            ),
          ),
        ],
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
            final sorted = standings.toList()
              ..sort((a, b) => a.position.compareTo(b.position));
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                ...sorted.map(
                  (s) => _StandingRow(standing: s, useGross: _useGross),
                ),
                const SizedBox(height: 16),
                _RoundScoresGrid(standings: sorted, useGross: _useGross),
                const SizedBox(height: 16),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _StandingRow extends StatelessWidget {
  const _StandingRow({required this.standing, required this.useGross});

  final Standing standing;
  final bool useGross;

  Color? get _positionColor {
    return switch (standing.position) {
      1 => const Color(0xFFFFD700), // Gold
      2 => const Color(0xFFC0C0C0), // Silver
      3 => const Color(0xFFCD7F32), // Bronze
      _ => null,
    };
  }

  @override
  Widget build(BuildContext context) {
    final position = standing.position;
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
                      '${standing.roundsPlayed} rounds · HCP: ${standing.currentHandicapIndex.toStringAsFixed(1)}'
                      '${standing.averageScore != null ? ' · Avg ${useGross ? 'gross' : 'net'}: ${standing.averageScore!.toStringAsFixed(1)}' : ''}',
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
                    'pts · avg ${standing.averagePoints.toStringAsFixed(1)}',
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

/// Week-by-week points per player, mirroring the web round-scores grid:
/// dropped scores struck through, skipped weeks shown as "skip".
class _RoundScoresGrid extends StatelessWidget {
  const _RoundScoresGrid({required this.standings, required this.useGross});

  final List<Standing> standings;
  final bool useGross;

  @override
  Widget build(BuildContext context) {
    final weeks = <int>{};
    for (final s in standings) {
      for (final r in s.roundScores) {
        weeks.add(r.weekNumber);
      }
    }
    if (weeks.isEmpty) return const SizedBox.shrink();
    final sortedWeeks = weeks.toList()..sort();

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Round-by-Round Scores',
              style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
            ),
            const SizedBox(height: 4),
            Text(
              '${useGross ? 'Gross' : 'Net'} Stableford pts · dropped scores struck through',
              style: const TextStyle(fontSize: 11, color: Color(0xFF9CA3AF)),
            ),
            const SizedBox(height: 12),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Table(
                defaultColumnWidth: const IntrinsicColumnWidth(),
                children: [
                  TableRow(
                    decoration:
                        const BoxDecoration(color: Color(0xFFF9FAFB)),
                    children: [
                      const _GridHeaderCell('Player'),
                      for (final w in sortedWeeks) _GridHeaderCell('Wk $w'),
                      const _GridHeaderCell('Total'),
                    ],
                  ),
                  for (final s in standings)
                    TableRow(
                      children: [
                        Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 6,
                          ),
                          child: Text(
                            s.playerFullName,
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                        for (final w in sortedWeeks)
                          _scoreCell(
                            s.roundScores
                                .where((r) => r.weekNumber == w)
                                .firstOrNull,
                          ),
                        Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 6,
                          ),
                          child: Text(
                            '${s.totalPoints}',
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _scoreCell(RoundScore? round) {
    Widget child;
    if (round == null) {
      child = const Text(
        '—',
        style: TextStyle(fontSize: 12, color: Color(0xFFD1D5DB)),
      );
    } else if (round.isSkipped) {
      child = const Text(
        'skip',
        style: TextStyle(
          fontSize: 12,
          color: Color(0xFF9CA3AF),
          fontStyle: FontStyle.italic,
        ),
      );
    } else {
      final label = round.points != null ? '${round.points}' : '—';
      child = Text(
        label,
        style: TextStyle(
          fontSize: 12,
          color: round.isDropped
              ? const Color(0xFF9CA3AF)
              : const Color(0xFF111827),
          fontWeight: round.isDropped ? FontWeight.w400 : FontWeight.w500,
          decoration: round.isDropped ? TextDecoration.lineThrough : null,
        ),
      );
    }
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      child: Center(child: child),
    );
  }
}

class _GridHeaderCell extends StatelessWidget {
  const _GridHeaderCell(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: const TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: Color(0xFF6B7280),
        ),
      ),
    );
  }
}
