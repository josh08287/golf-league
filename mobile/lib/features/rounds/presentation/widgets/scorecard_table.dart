import 'package:flutter/material.dart';

import '../../../../features/rounds/domain/models.dart';
import 'stableford_points_badge.dart';

/// Horizontally scrollable scorecard table showing holes 1-9, subtotal,
/// holes 10-18, subtotal, and total.
class ScorecardTable extends StatelessWidget {
  const ScorecardTable({
    super.key,
    required this.holes,
  });

  final List<HoleScore> holes;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;

    final front = holes.where((h) => h.holeNumber <= 9).toList()
      ..sort((a, b) => a.holeNumber.compareTo(b.holeNumber));
    final back = holes.where((h) => h.holeNumber >= 10).toList()
      ..sort((a, b) => a.holeNumber.compareTo(b.holeNumber));

    int _sum(List<HoleScore> hs, int? Function(HoleScore) fn) =>
        hs.fold(0, (s, h) => s + (fn(h) ?? 0));

    final frontGross = _sum(front, (h) => h.grossStrokes);
    final frontNet = _sum(front, (h) => h.netStrokes);
    final frontPts = _sum(front, (h) => h.stablefordPoints);

    final backGross = _sum(back, (h) => h.grossStrokes);
    final backNet = _sum(back, (h) => h.netStrokes);
    final backPts = _sum(back, (h) => h.stablefordPoints);

    const colWidths = <double>[
      36, // Hole
      32, // Par
      32, // SI
      48, // Gross
      48, // Net
      42, // Pts
    ];

    Widget headerCell(String text) => Container(
          alignment: Alignment.center,
          color: theme.colorScheme.primaryContainer,
          padding: const EdgeInsets.symmetric(vertical: 6),
          child: Text(
            text,
            style: textTheme.labelSmall?.copyWith(
              fontWeight: FontWeight.bold,
              color: theme.colorScheme.onPrimaryContainer,
            ),
          ),
        );

    Widget cell(
      String text, {
      bool bold = false,
      Color? bgColor,
      Color? textColor,
    }) =>
        Container(
          alignment: Alignment.center,
          color: bgColor,
          padding: const EdgeInsets.symmetric(vertical: 5),
          child: Text(
            text,
            style: textTheme.bodySmall?.copyWith(
              fontWeight: bold ? FontWeight.bold : FontWeight.normal,
              color: textColor,
            ),
          ),
        );

    Widget holeRow(HoleScore h, bool shaded) {
      final bg = shaded ? theme.colorScheme.surfaceContainerHighest : null;
      return TableRow(
        decoration:
            bg != null ? BoxDecoration(color: bg) : const BoxDecoration(),
        children: [
          cell('${h.holeNumber}'),
          cell('${h.par}'),
          cell('${h.strokeIndex}'),
          cell(h.grossStrokes != null ? '${h.grossStrokes}' : '-'),
          cell(h.netStrokes != null ? '${h.netStrokes}' : '-'),
          h.stablefordPoints != null
              ? Container(
                  alignment: Alignment.center,
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  child: StablefordPointsBadge(
                    points: h.stablefordPoints!,
                    size: 26,
                  ),
                )
              : cell('-'),
        ],
      );
    }

    Widget subtotalRow(
      String label,
      int gross,
      int net,
      int pts,
    ) =>
        TableRow(
          decoration: BoxDecoration(
            color: theme.colorScheme.secondaryContainer,
          ),
          children: [
            cell(label, bold: true),
            cell(''),
            cell(''),
            cell('$gross', bold: true),
            cell('$net', bold: true),
            Container(
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: StablefordPointsBadge(points: pts, size: 26),
            ),
          ],
        );

    final tableColumnWidths = <int, TableColumnWidth>{
      for (var i = 0; i < colWidths.length; i++)
        i: FixedColumnWidth(colWidths[i]),
    };

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Table(
        columnWidths: tableColumnWidths,
        border: TableBorder.all(
          color: theme.colorScheme.outlineVariant,
          width: 0.5,
        ),
        children: [
          // Header
          TableRow(
            children: [
              headerCell('Hole'),
              headerCell('Par'),
              headerCell('SI'),
              headerCell('Gross'),
              headerCell('Net'),
              headerCell('Pts'),
            ],
          ),
          // Front 9
          for (var i = 0; i < front.length; i++)
            holeRow(front[i], i.isEven),
          subtotalRow('OUT', frontGross, frontNet, frontPts),
          // Back 9
          for (var i = 0; i < back.length; i++)
            holeRow(back[i], i.isEven),
          subtotalRow('IN', backGross, backNet, backPts),
          // Total
          subtotalRow(
            'TOT',
            frontGross + backGross,
            frontNet + backNet,
            frontPts + backPts,
          ),
        ],
      ),
    );
  }
}
