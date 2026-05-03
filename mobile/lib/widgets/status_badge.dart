import 'package:flutter/material.dart';

import '../models/models.dart';

class StatusBadge extends StatelessWidget {
  const StatusBadge(this.status, {super.key});

  final RoundStatus status;

  @override
  Widget build(BuildContext context) {
    final (color, bg) = switch (status) {
      RoundStatus.finalized => (const Color(0xFF166534), const Color(0xFFdcfce7)),
      RoundStatus.inProgress => (const Color(0xFF92400e), const Color(0xFFfef3c7)),
      RoundStatus.pendingFinalization => (const Color(0xFF92400e), const Color(0xFFfef3c7)),
      RoundStatus.scheduled => (const Color(0xFF1e40af), const Color(0xFFdbeafe)),
      RoundStatus.cancelled => (const Color(0xFF374151), const Color(0xFFf3f4f6)),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(
        status.label,
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
