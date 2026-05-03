import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../api/providers.dart';
import '../models/models.dart';
import '../widgets/status_badge.dart';

class RoundDetailScreen extends ConsumerWidget {
  const RoundDetailScreen({super.key, required this.roundId});

  final int roundId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final roundAsync = ref.watch(roundDetailProvider(roundId));
    final scorecardsAsync = ref.watch(scorecardsProvider(roundId));

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        title: roundAsync.maybeWhen(
          data: (r) => Text(r.courseName),
          orElse: () => const Text('Round'),
        ),
        leading: const BackButton(),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(roundDetailProvider(roundId));
          ref.invalidate(scorecardsProvider(roundId));
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Round header
            roundAsync.when(
              loading: () => const _LoadingCard(),
              error: (e, _) => const _ErrorCard(message: 'Could not load round details.'),
              data: (round) => _RoundHeader(round: round),
            ),
            const SizedBox(height: 20),
            // Scorecards
            const Padding(
              padding: EdgeInsets.only(bottom: 12),
              child: Text(
                'Scorecards',
                style: TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
            ),
            scorecardsAsync.when(
              loading: () => const _LoadingCard(),
              error: (e, _) => const _ErrorCard(message: 'Could not load scorecards.'),
              data: (cards) {
                if (cards.isEmpty) {
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                      'No scorecards have been entered for this round yet.',
                      style: TextStyle(color: Color(0xFF6B7280), fontSize: 13),
                    ),
                  );
                }
                return Column(
                  children: cards
                      .map((sc) => Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: _ScorecardCard(scorecard: sc),
                          ))
                      .toList(),
                );
              },
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}

class _RoundHeader extends StatelessWidget {
  const _RoundHeader({required this.round});

  final Round round;

  @override
  Widget build(BuildContext context) {
    final dateStr = DateFormat('MMMM d, y').format(round.scheduledDate);

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  round.courseName,
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
              ),
              StatusBadge(round.status),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            dateStr,
            style: const TextStyle(fontSize: 14, color: Color(0xFF6B7280)),
          ),
          const SizedBox(height: 4),
          Text(
            round.flightName,
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF6B7280),
              fontWeight: FontWeight.w500,
            ),
          ),
          if (round.participantCount > 0) ...[
            const SizedBox(height: 8),
            Text(
              '${round.participantCount} participants',
              style: const TextStyle(fontSize: 12, color: Color(0xFF9CA3AF)),
            ),
          ],
        ],
      ),
    );
  }
}

class _ScorecardCard extends StatefulWidget {
  const _ScorecardCard({required this.scorecard});

  final Scorecard scorecard;

  @override
  State<_ScorecardCard> createState() => _ScorecardCardState();
}

class _ScorecardCardState extends State<_ScorecardCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final sc = widget.scorecard;
    final hcp = sc.handicapAtTime == sc.handicapAtTime.truncateToDouble()
        ? sc.handicapAtTime.toInt().toString()
        : sc.handicapAtTime.toStringAsFixed(1);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        children: [
          // Header row — always visible
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          sc.playerName,
                          style: const TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 15,
                            color: Color(0xFF111827),
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'HCP $hcp',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFF9CA3AF),
                          ),
                        ),
                      ],
                    ),
                  ),
                  // Score summary
                  Row(
                    children: [
                      if (sc.grossScore != null)
                        _ScorePill(label: 'Gross', value: '${sc.grossScore}'),
                      if (sc.netScore != null)
                        _ScorePill(label: 'Net', value: '${sc.netScore}'),
                      if (sc.points != null)
                        Container(
                          margin: const EdgeInsets.only(left: 6),
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                          decoration: BoxDecoration(
                            color: const Color(0xFFF3F4F6),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Text(
                            '${sc.points} pts',
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: Color(0xFF374151),
                            ),
                          ),
                        ),
                      const SizedBox(width: 8),
                      AnimatedRotation(
                        turns: _expanded ? 0.5 : 0,
                        duration: const Duration(milliseconds: 200),
                        child: const Icon(Icons.keyboard_arrow_down,
                            size: 20, color: Color(0xFF9CA3AF)),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          // Expanded hole-by-hole scorecard
          if (_expanded && sc.holes.isNotEmpty) ...[
            const Divider(height: 1, color: Color(0xFFF3F4F6)),
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 16),
              child: _ScorecardTable(scorecard: sc),
            ),
          ],
        ],
      ),
    );
  }
}

class _ScorePill extends StatelessWidget {
  const _ScorePill({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 8),
      child: RichText(
        text: TextSpan(
          style: const TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
          children: [
            TextSpan(text: '$label '),
            TextSpan(
              text: value,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ScorecardTable extends StatelessWidget {
  const _ScorecardTable({required this.scorecard});

  final Scorecard scorecard;

  @override
  Widget build(BuildContext context) {
    final front = scorecard.holes.where((h) => h.holeNumber <= 9).toList()
      ..sort((a, b) => a.holeNumber.compareTo(b.holeNumber));
    final back = scorecard.holes.where((h) => h.holeNumber > 9).toList()
      ..sort((a, b) => a.holeNumber.compareTo(b.holeNumber));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (front.isNotEmpty) ...[
          _HalfLabel('Front 9'),
          const SizedBox(height: 4),
          _HoleTable(holes: front),
          const SizedBox(height: 12),
        ],
        if (back.isNotEmpty) ...[
          _HalfLabel('Back 9'),
          const SizedBox(height: 4),
          _HoleTable(holes: back),
          const SizedBox(height: 12),
        ],
        // Summary line
        Wrap(
          spacing: 20,
          children: [
            if (scorecard.grossScore != null)
              _SummaryItem('Gross', '${scorecard.grossScore}'),
            if (scorecard.netScore != null)
              _SummaryItem('Net', '${scorecard.netScore}'),
            if (scorecard.points != null)
              _SummaryItem('Points', '${scorecard.points}'),
            _SummaryItem('HCP', scorecard.handicapAtTime.toStringAsFixed(1)),
          ],
        ),
      ],
    );
  }
}

class _HalfLabel extends StatelessWidget {
  const _HalfLabel(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text.toUpperCase(),
      style: const TextStyle(
        fontSize: 10,
        fontWeight: FontWeight.w700,
        color: Color(0xFF9CA3AF),
        letterSpacing: 0.8,
      ),
    );
  }
}

class _HoleTable extends StatelessWidget {
  const _HoleTable({required this.holes});

  final List<HoleScore> holes;

  Color _bgColor(HoleScore h) {
    final diff = h.strokes - h.par;
    if (diff <= -2) return const Color(0xFFFACC15); // eagle
    if (diff == -1) return const Color(0xFF22C55E); // birdie
    if (diff == 0) return Colors.white;              // par
    if (diff == 1) return const Color(0xFFE5E7EB);  // bogey
    return const Color(0xFFEF4444);                  // double+
  }

  Color _textColor(HoleScore h) {
    final diff = h.strokes - h.par;
    if (diff <= -2) return const Color(0xFF78350F);
    if (diff == -1) return Colors.white;
    if (diff == 0) return const Color(0xFF111827);
    if (diff == 1) return const Color(0xFF374151);
    return Colors.white;
  }

  @override
  Widget build(BuildContext context) {
    final totalPar = holes.fold(0, (s, h) => s + h.par);
    final totalGross = holes.fold(0, (s, h) => s + h.strokes);
    final totalNet = holes.fold(0, (s, h) => s + h.netStrokes);

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Table(
        defaultColumnWidth: const IntrinsicColumnWidth(),
        children: [
          // Hole numbers
          TableRow(
            decoration: const BoxDecoration(color: Color(0xFFF9FAFB)),
            children: [
              _HeaderCell('Hole'),
              for (final h in holes) _HeaderCell('${h.holeNumber}'),
              _HeaderCell('Tot'),
            ],
          ),
          // Par row
          TableRow(
            decoration: const BoxDecoration(color: Color(0xFFF9FAFB)),
            children: [
              _LabelCell('Par'),
              for (final h in holes) _DataCell('${h.par}', Colors.white, const Color(0xFF6B7280)),
              _DataCell('$totalPar', Colors.white, const Color(0xFF6B7280)),
            ],
          ),
          // Gross row
          TableRow(
            children: [
              _LabelCell('Gross'),
              for (final h in holes)
                _DataCell('${h.strokes}', _bgColor(h), _textColor(h)),
              _DataCell('$totalGross', Colors.white, const Color(0xFF111827),
                  bold: true),
            ],
          ),
          // Net row
          TableRow(
            children: [
              _LabelCell('Net'),
              for (final h in holes)
                _DataCell('${h.netStrokes}', Colors.white, const Color(0xFF6B7280)),
              _DataCell('$totalNet', Colors.white, const Color(0xFF6B7280)),
            ],
          ),
        ],
      ),
    );
  }
}

class _HeaderCell extends StatelessWidget {
  const _HeaderCell(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 5),
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

class _LabelCell extends StatelessWidget {
  const _LabelCell(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 5),
      child: Text(
        text,
        style: const TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w500,
          color: Color(0xFF374151),
        ),
      ),
    );
  }
}

class _DataCell extends StatelessWidget {
  const _DataCell(this.text, this.bg, this.fg, {this.bold = false});

  final String text;
  final Color bg;
  final Color fg;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(2),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(4),
        ),
        child: Text(
          text,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 11,
            color: fg,
            fontWeight: bold ? FontWeight.w700 : FontWeight.w400,
          ),
        ),
      ),
    );
  }
}

class _SummaryItem extends StatelessWidget {
  const _SummaryItem(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return RichText(
      text: TextSpan(
        style: const TextStyle(fontSize: 13, color: Color(0xFF6B7280)),
        children: [
          TextSpan(text: '$label: '),
          TextSpan(
            text: value,
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            ),
          ),
        ],
      ),
    );
  }
}

class _LoadingCard extends StatelessWidget {
  const _LoadingCard();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Padding(
        padding: EdgeInsets.all(32),
        child: CircularProgressIndicator(),
      ),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFFEF2F2),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFFCA5A5)),
      ),
      child: Row(
        children: [
          const Icon(Icons.error_outline, color: Color(0xFFDC2626), size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(color: Color(0xFF991B1B), fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
