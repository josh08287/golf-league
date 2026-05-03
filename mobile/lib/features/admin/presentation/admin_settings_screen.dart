import 'package:flutter/material.dart';

import 'admin_gate.dart';

/// Matches web admin Settings placeholder.
class AdminSettingsScreen extends StatelessWidget {
  const AdminSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(title: const Text('Settings')),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'League configuration settings will appear here. '
              '(Same placeholder as the web admin Settings page.)',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      ),
    );
  }
}
