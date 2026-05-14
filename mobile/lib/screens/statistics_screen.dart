import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/providers.dart';
import '../models/models.dart';

class StatisticsScreen extends ConsumerWidget {
  const StatisticsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final coursesAsync = ref.watch(coursesProvider);
    final mostImprovedAsync = ref.watch(mostImprovedProvider);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Statistics'),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(coursesProvider);
          ref.invalidate(mostImprovedProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Most Improved Section
            mostImprovedAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, _) => const SizedBox.shrink(),
              data: (result) => result != null && result.winner != null
                  ? _MostImprovedCard(result: result)
                  : const SizedBox.shrink(),
            ),
            const SizedBox(height: 24),
            // Course Statistics Section
            const Text(
              'Course Statistics',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 12),
            coursesAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => const _ErrorCard('Could not load courses.'),
              data: (courses) => _CourseStatisticsList(courses: courses),
            ),
          ],
        ),
      ),
    );
  }
}

class _MostImprovedCard extends StatelessWidget {
  const _MostImprovedCard({required this.result});

  final MostImprovedResult result;

  @override
  Widget build(BuildContext context) {
    final winner = result.winner!;
    return Card(
      elevation: 0,
      color: const Color(0xFFFFF8E1),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: Colors.amber.shade200),
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.emoji_events,
                  color: Colors.amber.shade700,
                  size: 28,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'Most Improved - ${result.seasonHalfName}',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Colors.amber.shade900,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        winner.playerName,
                        style: const TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF111827),
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${winner.roundsPlayedInHalf} rounds played',
                        style: const TextStyle(
                          fontSize: 13,
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
                      winner.improvementFactor.toStringAsFixed(3),
                      style: TextStyle(
                        fontSize: 24,
                        fontWeight: FontWeight.bold,
                        color: Colors.green.shade700,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${winner.startingHandicapIndex.toStringAsFixed(1)} → ${winner.currentHandicapIndex.toStringAsFixed(1)}',
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xFF6B7280),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _CourseStatisticsList extends StatefulWidget {
  const _CourseStatisticsList({required this.courses});

  final List<Course> courses;

  @override
  State<_CourseStatisticsList> createState() => _CourseStatisticsListState();
}

class _CourseStatisticsListState extends State<_CourseStatisticsList> {
  int? _selectedCourseId;

  @override
  void initState() {
    super.initState();
    if (widget.courses.isNotEmpty) {
      _selectedCourseId = widget.courses.first.id;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.courses.isEmpty) {
      return const Card(
        elevation: 0,
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text('No courses configured yet.'),
        ),
      );
    }

    return Column(
      children: [
        Wrap(
          spacing: 8,
          children: widget.courses.map((course) {
            final isSelected = _selectedCourseId == course.id;
            return ChoiceChip(
              label: Text(course.name),
              selected: isSelected,
              onSelected: (selected) {
                if (selected) {
                  setState(() => _selectedCourseId = course.id);
                }
              },
              backgroundColor: Colors.white,
              selectedColor: const Color(0xFF1a5c38),
              labelStyle: TextStyle(
                color: isSelected ? Colors.white : const Color(0xFF374151),
              ),
            );
          }).toList(),
        ),
        const SizedBox(height: 16),
        if (_selectedCourseId != null)
          Consumer(
            builder: (context, ref, _) {
              final statsAsync = ref.watch(
                courseStatisticsProvider(_selectedCourseId!),
              );
              return statsAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (_, _) => const _ErrorCard('Could not load statistics.'),
                data: (stats) => stats != null
                    ? _CourseStatsDetail(stats: stats)
                    : const SizedBox.shrink(),
              );
            },
          ),
      ],
    );
  }
}

class _CourseStatsDetail extends StatelessWidget {
  const _CourseStatsDetail({required this.stats});

  final CourseStatistics stats;

  String _scoreToParLabel(double? val) {
    if (val == null) return '—';
    if (val > 0) return '+${val.toStringAsFixed(1)}';
    if (val == 0) return 'E';
    return val.toStringAsFixed(1);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        // Summary Cards
        Row(
          children: [
            Expanded(
              child: _StatCard(
                label: 'Rounds',
                value: '${stats.totalRoundsPlayed}',
                subtitle: '${stats.totalScorecardsRecorded} scorecards',
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _StatCard(
                label: 'Avg to Par',
                value: _scoreToParLabel(stats.averageScoreToPar),
                subtitle: stats.averageTotalNetStrokes != null
                    ? '${stats.averageTotalNetStrokes!.toStringAsFixed(1)} net'
                    : null,
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        // Hole Statistics
        Card(
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
                  'Hole Statistics',
                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                ),
                const SizedBox(height: 12),
                if (stats.holeStatistics.isEmpty)
                  const Text(
                    'No hole statistics available.',
                    style: TextStyle(color: Color(0xFF6B7280)),
                  )
                else
                  ...stats.holeStatistics.map(
                    (hole) => _HoleStatRow(hole: hole),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value, this.subtitle});

  final String label;
  final String value;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Text(
              value,
              style: const TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1a5c38),
              ),
            ),
            const SizedBox(height: 4),
            Text(
              label,
              style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
            ),
            if (subtitle != null) ...[
              const SizedBox(height: 2),
              Text(
                subtitle!,
                style: TextStyle(fontSize: 11, color: Colors.grey[500]),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _HoleStatRow extends StatelessWidget {
  const _HoleStatRow({required this.hole});

  final HoleStatistics hole;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Container(
            width: 32,
            height: 32,
            decoration: BoxDecoration(
              color: const Color(0xFFF3F4F6),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Center(
              child: Text(
                '${hole.holeNumber}',
                style: const TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 14,
                ),
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Par ${hole.par} · SI ${hole.strokeIndex}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: Color(0xFF6B7280),
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Avg: ${hole.averageGrossStrokes.toStringAsFixed(1)} gross · ${hole.averageNetStablefordPoints.toStringAsFixed(1)} pts',
                  style: const TextStyle(fontSize: 13),
                ),
              ],
            ),
          ),
        ],
      ),
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
