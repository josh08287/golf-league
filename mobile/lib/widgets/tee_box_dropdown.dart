import 'package:flutter/material.dart';

import '../models/models.dart';

/// Per-player tee picker for score entry. Starts on the player's default tee;
/// changing it re-rates the player's round and rescores holes already entered.
/// [onChanged] receives `null` when the player is reset to their default tee.
class TeeBoxDropdown extends StatelessWidget {
  const TeeBoxDropdown({
    super.key,
    required this.teeBoxes,
    required this.value,
    required this.overridden,
    required this.onChanged,
    this.enabled = true,
  });

  final List<TeeBoxOption> teeBoxes;
  final int value;
  final bool overridden;
  final ValueChanged<int?> onChanged;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    if (teeBoxes.isEmpty) return const SizedBox.shrink();
    final hasValue = teeBoxes.any((t) => t.id == value);

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        DropdownButton<int>(
          value: hasValue ? value : null,
          isDense: true,
          style: const TextStyle(fontSize: 13, color: Color(0xFF374151)),
          onChanged: enabled
              ? (id) {
                  if (id != null && id != value) onChanged(id);
                }
              : null,
          items: teeBoxes
              .map(
                (t) => DropdownMenuItem<int>(value: t.id, child: Text(t.label)),
              )
              .toList(),
        ),
        if (overridden)
          TextButton(
            onPressed: enabled ? () => onChanged(null) : null,
            style: TextButton.styleFrom(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              minimumSize: const Size(0, 32),
            ),
            child: const Text('Reset', style: TextStyle(fontSize: 12)),
          ),
      ],
    );
  }
}
