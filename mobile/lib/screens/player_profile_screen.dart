import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:fl_chart/fl_chart.dart';

import '../api/api_service.dart';
import '../api/providers.dart';
import '../auth/auth_providers.dart';
import '../models/models.dart';

class PlayerProfileScreen extends ConsumerWidget {
  const PlayerProfileScreen({super.key, required this.playerId});

  final int playerId;

  Future<void> _showEditDialog(
    BuildContext context,
    WidgetRef ref,
    Player player,
  ) async {
    final parts = player.fullName.split(' ');
    final firstCtrl = TextEditingController(text: parts.first);
    final lastCtrl = TextEditingController(
      text: parts.length > 1 ? parts.sublist(1).join(' ') : '',
    );
    String? error;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return StatefulBuilder(
          builder: (ctx, setState) {
            return Padding(
              padding: EdgeInsets.fromLTRB(
                24,
                24,
                24,
                MediaQuery.of(ctx).viewInsets.bottom + 24,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Edit Player Name',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 20),
                  TextField(
                    controller: firstCtrl,
                    textCapitalization: TextCapitalization.words,
                    decoration: const InputDecoration(
                      labelText: 'First name',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: lastCtrl,
                    textCapitalization: TextCapitalization.words,
                    decoration: const InputDecoration(
                      labelText: 'Last name',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  if (error != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      error!,
                      style: const TextStyle(color: Colors.red, fontSize: 13),
                    ),
                  ],
                  const SizedBox(height: 20),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      TextButton(
                        onPressed: () => Navigator.of(ctx).pop(),
                        child: const Text('Cancel'),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        onPressed: () async {
                          final first = firstCtrl.text.trim();
                          final last = lastCtrl.text.trim();
                          if (first.isEmpty || last.isEmpty) {
                            setState(() => error = 'First and last name are required.');
                            return;
                          }
                          try {
                            await ref
                                .read(apiServiceProvider)
                                .updatePlayer(
                                  playerId,
                                  firstName: first,
                                  lastName: last,
                                  email: player.email,
                                );
                            ref.invalidate(playerDetailProvider(playerId));
                            if (ctx.mounted) Navigator.of(ctx).pop();
                          } catch (e) {
                            setState(() => error = 'Failed to save. Please try again.');
                          }
                        },
                        child: const Text('Save'),
                      ),
                    ],
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    firstCtrl.dispose();
    lastCtrl.dispose();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final playerAsync = ref.watch(playerDetailProvider(playerId));
    final handicapHistoryAsync = ref.watch(
      playerHandicapHistoryProvider(playerId),
    );
    final roundsAsync = ref.watch(playerRoundsProvider(playerId));
    final statsAsync = ref.watch(playerStatisticsProvider(playerId));
    final myStatus = ref.watch(myStatusProvider);
    final isAdmin = myStatus.role == 'admin' || myStatus.role == 'scorer';

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: const Text('Player Profile'),
        leading: const BackButton(),
        actions: [
          if (isAdmin)
            playerAsync.whenOrNull(
              data: (player) => IconButton(
                icon: const Icon(Icons.edit_outlined),
                tooltip: 'Edit name',
                onPressed: () => _showEditDialog(context, ref, player),
              ),
            ) ?? const SizedBox.shrink(),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(playerDetailProvider(playerId));
          ref.invalidate(playerHandicapHistoryProvider(playerId));
          ref.invalidate(playerRoundsProvider(playerId));
          ref.invalidate(playerStatisticsProvider(playerId));
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            playerAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, _) =>
                  const _ErrorCard('Could not load player profile.'),
              data: (player) => _PlayerHeader(player: player),
            ),
            const SizedBox(height: 24),
            statsAsync.when(
              loading: () => const SizedBox.shrink(),
              error: (_, _) => const SizedBox.shrink(),
              data: (stats) => stats == null || stats.totalRoundsPlayed == 0
                  ? const SizedBox.shrink()
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Season Statistics',
                          style: TextStyle(
                            fontSize: 17,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF111827),
                          ),
                        ),
                        const SizedBox(height: 12),
                        _PlayerStatsSection(stats: stats),
                        const SizedBox(height: 24),
                      ],
                    ),
            ),
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
              error: (_, _) =>
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
              error: (_, _) => const _ErrorCard('Could not load rounds.'),
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

class _PlayerStatsSection extends StatelessWidget {
  const _PlayerStatsSection({required this.stats});

  final PlayerStatistics stats;

  String _fmt(double? v, {int digits = 1}) =>
      v != null ? v.toStringAsFixed(digits) : '—';

  @override
  Widget build(BuildContext context) {
    final dist = stats.scoringDistribution;
    return Column(
      children: [
        // Averages
        Card(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: const BorderSide(color: Color(0xFFE5E7EB)),
          ),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: [
                _StatBox(
                  label: 'Rounds',
                  value: '${stats.totalRoundsPlayed}',
                ),
                _StatBox(
                  label: 'Avg Gross',
                  value: _fmt(stats.averageGrossStrokes),
                ),
                _StatBox(
                  label: 'Avg Net',
                  value: _fmt(stats.averageNetStrokes),
                ),
                _StatBox(
                  label: 'Avg Pts',
                  value: _fmt(stats.averageNetStablefordPoints),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        // Bests
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
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceAround,
                  children: [
                    _StatBox(
                      label: 'Best Gross',
                      value: stats.bestGrossStrokes != null
                          ? '${stats.bestGrossStrokes}'
                          : '—',
                    ),
                    _StatBox(
                      label: 'Best Net Pts',
                      value: stats.bestNetStablefordPoints != null
                          ? '${stats.bestNetStablefordPoints}'
                          : '—',
                    ),
                    _StatBox(
                      label: 'Par or Better',
                      value: stats.parOrBetterPercentage != null
                          ? '${stats.parOrBetterPercentage!.toStringAsFixed(0)}%'
                          : '—',
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        // Scoring distribution
        if (dist.totalHolesPlayed > 0)
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
                    'Scoring Distribution',
                    style:
                        TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
                  ),
                  const SizedBox(height: 10),
                  _DistRow('Eagle or better', dist.eagleOrBetterCount,
                      dist.totalHolesPlayed, const Color(0xFFFACC15)),
                  _DistRow('Birdie', dist.birdieCount, dist.totalHolesPlayed,
                      const Color(0xFF22C55E)),
                  _DistRow('Par', dist.parCount, dist.totalHolesPlayed,
                      const Color(0xFF9CA3AF)),
                  _DistRow('Bogey', dist.bogeyCount, dist.totalHolesPlayed,
                      const Color(0xFFFB923C)),
                  _DistRow('Double bogey+', dist.doubleBogeyOrWorseCount,
                      dist.totalHolesPlayed, const Color(0xFFEF4444)),
                ],
              ),
            ),
          ),
        if (stats.strokesGainedPutting != null &&
            stats.strokesGainedPutting!.holesWithPuttData > 0) ...[
          const SizedBox(height: 12),
          Card(
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
              side: const BorderSide(color: Color(0xFFE5E7EB)),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceAround,
                children: [
                  _StatBox(
                    label: 'SG: Putting',
                    value: _fmt(
                      stats.strokesGainedPutting!.totalStrokesGained,
                      digits: 2,
                    ),
                  ),
                  _StatBox(
                    label: 'Avg Putts/Hole',
                    value: _fmt(
                      stats.strokesGainedPutting!.averagePuttsPerHole,
                      digits: 2,
                    ),
                  ),
                  _StatBox(
                    label: 'Flight Avg',
                    value: _fmt(
                      stats.strokesGainedPutting!.flightAveragePuttsPerHole,
                      digits: 2,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _DistRow extends StatelessWidget {
  const _DistRow(this.label, this.count, this.total, this.color);

  final String label;
  final int count;
  final int total;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final fraction = total > 0 ? count / total : 0.0;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
            ),
          ),
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: fraction,
                minHeight: 8,
                backgroundColor: const Color(0xFFF3F4F6),
                valueColor: AlwaysStoppedAnimation(color),
              ),
            ),
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 64,
            child: Text(
              '$count (${(fraction * 100).toStringAsFixed(0)}%)',
              textAlign: TextAlign.right,
              style: const TextStyle(fontSize: 11, color: Color(0xFF6B7280)),
            ),
          ),
        ],
      ),
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
      children: [
        _HandicapChart(history: history),
        const SizedBox(height: 16),
        ...history.map((entry) => _HandicapHistoryRow(entry: entry)),
      ],
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

class _HandicapChart extends StatelessWidget {
  const _HandicapChart({required this.history});

  final List<HandicapHistoryEntry> history;

  @override
  Widget build(BuildContext context) {
    final sorted = history.toList()
      ..sort((a, b) => a.effectiveDate.compareTo(b.effectiveDate));

    final minDate = sorted.first.effectiveDate;
    final maxDate = sorted.last.effectiveDate;
    final dateRange = maxDate.difference(minDate).inDays.toDouble();
    final dateSpan = dateRange < 1 ? 1.0 : dateRange;

    final allValues = sorted
        .expand((e) => [e.handicapIndex, e.nineHoleHandicapIndex])
        .toList();
    final minVal = allValues.reduce((a, b) => a < b ? a : b);
    final maxVal = allValues.reduce((a, b) => a > b ? a : b);
    final padding = (maxVal - minVal) * 0.1;
    final yMin = ((minVal - padding).clamp(0, double.infinity)).toDouble();
    final yMax = (maxVal + padding).toDouble();

    final eighteenHoleSpots = sorted.map((e) {
      final x = e.effectiveDate.difference(minDate).inDays.toDouble();
      return FlSpot(x, e.handicapIndex);
    }).toList();

    final nineHoleSpots = sorted.map((e) {
      final x = e.effectiveDate.difference(minDate).inDays.toDouble();
      return FlSpot(x, e.nineHoleHandicapIndex);
    }).toList();

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                _LegendItem(
                  color: const Color(0xFF1a5c38),
                  label: '18-Hole HCP',
                ),
                const SizedBox(width: 24),
                _LegendItem(
                  color: const Color(0xFF059669),
                  label: '9-Hole HCP',
                ),
              ],
            ),
            const SizedBox(height: 16),
            SizedBox(
              height: 200,
              child: LineChart(
                LineChartData(
                  gridData: FlGridData(
                    show: true,
                    drawVerticalLine: false,
                    getDrawingHorizontalLine: (value) {
                      return FlLine(
                        color: const Color(0xFFE5E7EB),
                        strokeWidth: 1,
                      );
                    },
                  ),
                  titlesData: FlTitlesData(
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 40,
                        getTitlesWidget: (value, meta) {
                          return Text(
                            value.toStringAsFixed(0),
                            style: const TextStyle(
                              fontSize: 10,
                              color: Color(0xFF6B7280),
                            ),
                          );
                        },
                      ),
                    ),
                    bottomTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        getTitlesWidget: (value, meta) {
                          final date = minDate.add(
                            Duration(days: value.toInt()),
                          );
                          return Text(
                            DateFormat('MMM d').format(date),
                            style: const TextStyle(
                              fontSize: 10,
                              color: Color(0xFF6B7280),
                            ),
                          );
                        },
                      ),
                    ),
                    rightTitles: const AxisTitles(
                      sideTitles: SideTitles(showTitles: false),
                    ),
                    topTitles: const AxisTitles(
                      sideTitles: SideTitles(showTitles: false),
                    ),
                  ),
                  borderData: FlBorderData(show: false),
                  minX: 0,
                  maxX: dateSpan,
                  minY: yMin,
                  maxY: yMax,
                  lineTouchData: LineTouchData(
                    touchTooltipData: LineTouchTooltipData(
                      getTooltipColor: (touchedSpot) => const Color(0xFF1F2937),
                      tooltipRoundedRadius: 8,
                      getTooltipItems: (touchedSpots) {
                        return touchedSpots.map((touchedSpot) {
                          final isEighteen = touchedSpot.barIndex == 0;
                          final date = minDate.add(
                            Duration(days: touchedSpot.x.toInt()),
                          );
                          return LineTooltipItem(
                            '${isEighteen ? '18' : '9'}-Hole: ${touchedSpot.y.toStringAsFixed(1)}\n${DateFormat('MMM d, y').format(date)}',
                            const TextStyle(
                              color: Color(0xFF6EE7B7),
                              fontSize: 12,
                            ),
                          );
                        }).toList();
                      },
                    ),
                  ),
                  lineBarsData: [
                    LineChartBarData(
                      spots: eighteenHoleSpots,
                      isCurved: true,
                      color: const Color(0xFF1a5c38),
                      barWidth: 3,
                      isStrokeCapRound: true,
                      dotData: const FlDotData(show: true),
                      belowBarData: BarAreaData(show: false),
                    ),
                    LineChartBarData(
                      spots: nineHoleSpots,
                      isCurved: true,
                      color: const Color(0xFF059669),
                      barWidth: 3,
                      isStrokeCapRound: true,
                      dotData: const FlDotData(show: true),
                      belowBarData: BarAreaData(show: false),
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

class _LegendItem extends StatelessWidget {
  const _LegendItem({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 8),
        Text(
          label,
          style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
        ),
      ],
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
