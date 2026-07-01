import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../api/providers.dart';
import '../models/models.dart';

class TournamentResultsScreen extends ConsumerWidget {
  const TournamentResultsScreen({super.key, required this.roundId});

  final int roundId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resultsAsync = ref.watch(tournamentResultsProvider(roundId));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Tournament Results'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(tournamentResultsProvider(roundId));
        },
        child: resultsAsync.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 16),
                const Text('Failed to load tournament results.'),
                const SizedBox(height: 8),
                ElevatedButton(
                  onPressed: () =>
                      ref.invalidate(tournamentResultsProvider(roundId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
          data: (results) {
            if (results == null) {
              return const Center(
                child: Text(
                  'No tournament results available.',
                  style: TextStyle(color: Color(0xFF6B7280)),
                ),
              );
            }
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                // Header
                Row(
                  children: [
                    const Icon(Icons.emoji_events,
                        color: Colors.amber, size: 28),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        '${results.courseName} · ${DateFormat('MMM d, y').format(results.roundDate)}',
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFF111827),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),

                _SectionTitle(icon: Icons.emoji_events, label: 'Skins'),
                _SkinsPanel(skins: results.grossSkins),
                const SizedBox(height: 12),
                _SkinsPanel(skins: results.netSkins),
                const SizedBox(height: 24),

                _SectionTitle(
                  icon: Icons.gps_fixed,
                  label: 'Closest to Pin & Longest Drive',
                ),
                _HoleExtrasPanel(
                  extras: results.holeExtras,
                  ldWinners: results.longestDriveWinners,
                ),
                const SizedBox(height: 24),

                if (results.matchupResults.isNotEmpty) ...[
                  _SectionTitle(icon: Icons.people, label: 'Matchup Results'),
                  ...results.matchupResults
                      .map((m) => _MatchupCard(matchup: m)),
                  const SizedBox(height: 24),
                ],

                _SectionTitle(icon: Icons.bar_chart, label: 'Rankings'),
                _RankingCard(
                  title: 'Gross Stroke Play',
                  entries: results.grossStrokeRanking,
                  scoreLabel: 'Gross',
                ),
                _RankingCard(
                  title: 'Net Stroke Play',
                  entries: results.netStrokeRanking,
                  scoreLabel: 'Net',
                ),
                _RankingCard(
                  title: 'Gross Stableford',
                  entries: results.grossStablefordRanking,
                  scoreLabel: 'Pts',
                ),
                _RankingCard(
                  title: 'Net Stableford',
                  entries: results.netStablefordRanking,
                  scoreLabel: 'Pts',
                ),
                const SizedBox(height: 16),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: [
          Icon(icon, size: 20, color: const Color(0xFF1a5c38)),
          const SizedBox(width: 8),
          Text(
            label,
            style: const TextStyle(
              fontSize: 17,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            ),
          ),
        ],
      ),
    );
  }
}

class _SkinsPanel extends StatelessWidget {
  const _SkinsPanel({required this.skins});

  final TournamentSkinsResult skins;

  @override
  Widget build(BuildContext context) {
    final label = skins.skinType == 'Gross' ? 'Gross Skins' : 'Net Skins';
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
            Text(
              label,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
            ),
            const SizedBox(height: 8),
            if (skins.holeResults.isEmpty)
              const Text(
                'No scores submitted yet.',
                style: TextStyle(
                  color: Color(0xFF9CA3AF),
                  fontStyle: FontStyle.italic,
                ),
              )
            else ...[
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: DataTable(
                  headingRowHeight: 32,
                  dataRowMinHeight: 32,
                  dataRowMaxHeight: 40,
                  columnSpacing: 18,
                  columns: const [
                    DataColumn(label: Text('Hole')),
                    DataColumn(label: Text('Par')),
                    DataColumn(label: Text('Winner')),
                    DataColumn(label: Text('Score')),
                    DataColumn(label: Text('Value')),
                  ],
                  rows: skins.holeResults.map((h) {
                    return DataRow(
                      cells: [
                        DataCell(Text('${h.holeNumber}')),
                        DataCell(Text('${h.par}')),
                        DataCell(
                          h.isTie
                              ? const Text(
                                  'Tie (carry +1)',
                                  style: TextStyle(
                                    color: Color(0xFF9CA3AF),
                                    fontStyle: FontStyle.italic,
                                  ),
                                )
                              : Text(
                                  h.winnerPlayerName ?? '—',
                                  style: const TextStyle(
                                    color: Color(0xFF1a5c38),
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                        ),
                        DataCell(Text(
                            h.winningScore != null ? '${h.winningScore}' : '—')),
                        DataCell(
                          h.isTie
                              ? const Text('—')
                              : Text(
                                  h.skinValue > 0
                                      ? '${h.skinValue}${h.wasCarryover ? ' ★' : ''}'
                                      : '—',
                                  style: TextStyle(
                                    fontWeight: FontWeight.w600,
                                    color: h.wasCarryover
                                        ? Colors.amber.shade800
                                        : const Color(0xFF374151),
                                  ),
                                ),
                        ),
                      ],
                    );
                  }).toList(),
                ),
              ),
              if (skins.playerSummaries.isNotEmpty) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: skins.playerSummaries.map((ps) {
                    return Chip(
                      avatar: const Icon(Icons.emoji_events,
                          size: 16, color: Colors.amber),
                      label: Text(
                        '${ps.playerName}: ${ps.totalSkinsWon} skin${ps.totalSkinsWon != 1 ? 's' : ''} (${ps.totalSkinValue} pts)',
                        style: const TextStyle(fontSize: 12),
                      ),
                      backgroundColor: const Color(0xFFFFF8E1),
                      side: BorderSide(color: Colors.amber.shade200),
                    );
                  }).toList(),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _HoleExtrasPanel extends StatelessWidget {
  const _HoleExtrasPanel({required this.extras, required this.ldWinners});

  final List<TournamentHoleExtra> extras;
  final List<LongestDriveWinner> ldWinners;

  @override
  Widget build(BuildContext context) {
    final ctp =
        extras.where((e) => e.closestToPinPlayerId != null).toList();

    if (ctp.isEmpty && ldWinners.isEmpty) {
      return const Card(
        elevation: 0,
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text(
            'Closest to pin and longest drive not yet recorded.',
            style: TextStyle(
              color: Color(0xFF9CA3AF),
              fontStyle: FontStyle.italic,
            ),
          ),
        ),
      );
    }

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
              'Closest to the Pin (Par 3s)',
              style: TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
            ),
            const SizedBox(height: 8),
            if (ctp.isEmpty)
              const Text(
                'Not recorded.',
                style: TextStyle(
                  color: Color(0xFF9CA3AF),
                  fontStyle: FontStyle.italic,
                ),
              )
            else
              ...ctp.map(
                (e) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Row(
                    children: [
                      Text(
                        '#${e.holeNumber}',
                        style: const TextStyle(
                          color: Color(0xFF6B7280),
                          fontFamily: 'monospace',
                        ),
                      ),
                      const SizedBox(width: 12),
                      Text(
                        e.closestToPinPlayerName ?? '',
                        style: const TextStyle(fontWeight: FontWeight.w500),
                      ),
                    ],
                  ),
                ),
              ),
            const SizedBox(height: 16),
            const Text(
              'Longest Drive',
              style: TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
            ),
            const SizedBox(height: 8),
            if (ldWinners.isEmpty)
              const Text(
                'Not recorded.',
                style: TextStyle(
                  color: Color(0xFF9CA3AF),
                  fontStyle: FontStyle.italic,
                ),
              )
            else
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: ldWinners
                    .map(
                      (w) => Chip(
                        label: Text(w.playerName,
                            style: const TextStyle(fontSize: 12)),
                        backgroundColor: const Color(0xFFFFF8E1),
                        side: BorderSide(color: Colors.amber.shade200),
                      ),
                    )
                    .toList(),
              ),
          ],
        ),
      ),
    );
  }
}

class _MatchupCard extends StatelessWidget {
  const _MatchupCard({required this.matchup});

  final TournamentMatchupResult matchup;

  @override
  Widget build(BuildContext context) {
    final m = matchup;
    final p1Wins = m.winnerPlayerId == m.player1Id;
    final p2Wins = m.winnerPlayerId == m.player2Id;
    final pending = m.winnerPlayerId == null && !m.isHalved;

    Widget playerBox(
      String name,
      double hcp,
      int ch,
      int? net,
      bool isWinner,
    ) {
      return Expanded(
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: isWinner ? const Color(0xFFE8F5E9) : const Color(0xFFF9FAFB),
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: isWinner ? const Color(0xFF4CAF50) : Colors.transparent,
              width: isWinner ? 2 : 0,
            ),
          ),
          child: Column(
            children: [
              Text(
                name,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  color: isWinner
                      ? const Color(0xFF1a5c38)
                      : const Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 2),
              Text(
                'HCP ${hcp.toStringAsFixed(1)} / CH $ch',
                style: const TextStyle(fontSize: 11, color: Color(0xFF6B7280)),
              ),
              if (net != null) ...[
                const SizedBox(height: 4),
                Text(
                  '$net',
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
              if (isWinner)
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    '🏆 Winner',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF1a5c38),
                    ),
                  ),
                ),
            ],
          ),
        ),
      );
    }

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
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
            Text(
              'MATCHUP ${m.matchupNumber}',
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: Color(0xFF9CA3AF),
                letterSpacing: 0.8,
              ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                playerBox(
                  m.player1Name,
                  m.player1HandicapIndex,
                  m.player1CourseHandicap,
                  m.player1NetStrokes,
                  p1Wins,
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 8),
                  child: Text(
                    'vs',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      color: Color(0xFF9CA3AF),
                    ),
                  ),
                ),
                playerBox(
                  m.player2Name,
                  m.player2HandicapIndex,
                  m.player2CourseHandicap,
                  m.player2NetStrokes,
                  p2Wins,
                ),
              ],
            ),
            if (m.isHalved || pending) ...[
              const SizedBox(height: 8),
              Center(
                child: Text(
                  m.isHalved ? 'Halved (Tie)' : 'Awaiting scores',
                  style: TextStyle(
                    fontSize: 12,
                    color: m.isHalved
                        ? Colors.blue.shade700
                        : const Color(0xFF9CA3AF),
                    fontStyle: m.isHalved ? null : FontStyle.italic,
                    fontWeight: m.isHalved ? FontWeight.w500 : null,
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _RankingCard extends StatelessWidget {
  const _RankingCard({
    required this.title,
    required this.entries,
    required this.scoreLabel,
  });

  final String title;
  final List<TournamentRankingEntry> entries;
  final String scoreLabel;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
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
            Text(
              title,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
            ),
            const SizedBox(height: 8),
            if (entries.isEmpty)
              const Text(
                'No scores yet.',
                style: TextStyle(
                  color: Color(0xFF9CA3AF),
                  fontStyle: FontStyle.italic,
                  fontSize: 12,
                ),
              )
            else
              ...entries.map(
                (e) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Row(
                    children: [
                      Container(
                        width: 28,
                        height: 28,
                        decoration: BoxDecoration(
                          color: e.rank == 1
                              ? Colors.amber
                              : const Color(0xFFF3F4F6),
                          shape: BoxShape.circle,
                        ),
                        child: Center(
                          child: Text(
                            e.isTied ? 'T${e.rank}' : '${e.rank}',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: e.rank == 1
                                  ? Colors.white
                                  : const Color(0xFF6B7280),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          e.playerName,
                          style: const TextStyle(fontWeight: FontWeight.w500),
                        ),
                      ),
                      Text(
                        'HCP ${e.handicapIndex.toStringAsFixed(1)}',
                        style: const TextStyle(
                          fontSize: 11,
                          color: Color(0xFF9CA3AF),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Text(
                        e.score != null ? '${e.score} $scoreLabel' : '—',
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
