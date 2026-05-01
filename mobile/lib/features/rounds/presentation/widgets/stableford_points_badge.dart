import 'package:flutter/material.dart';

/// Colored badge displaying a Stableford point value.
///
/// Color coding:
///   0  — grey (double bogey or worse)
///   1  — white with border (bogey)
///   2  — course green (par)
///   3  — lime green (birdie)
///   4  — amber (eagle)
///   5+ — purple (albatross / condor)
class StablefordPointsBadge extends StatelessWidget {
  const StablefordPointsBadge({
    super.key,
    required this.points,
    this.size = 28.0,
  });

  final int points;
  final double size;

  @override
  Widget build(BuildContext context) {
    final (bg, fg, border) = _colors(points);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(6),
        border: border != null ? Border.all(color: border) : null,
      ),
      alignment: Alignment.center,
      child: Text(
        '$points',
        style: TextStyle(
          fontSize: size * 0.46,
          fontWeight: FontWeight.bold,
          color: fg,
        ),
      ),
    );
  }

  static (Color bg, Color fg, Color? border) _colors(int points) {
    return switch (points) {
      0 => (Colors.grey.shade300, Colors.grey.shade700, null),
      1 => (Colors.white, Colors.black87, Colors.grey.shade400),
      2 => (const Color(0xFF1B5E20), Colors.white, null),
      3 => (const Color(0xFF76FF03), const Color(0xFF1B5E20), null),
      4 => (const Color(0xFFFFD600), Colors.black87, null),
      _ => (Colors.purple, Colors.white, null),
    };
  }
}
