import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../data/admin_league_service.dart';
import 'admin_gate.dart';
import 'admin_providers.dart';

/// Mirrors the web admin home: quick links to management areas.
class AdminDashboardScreen extends ConsumerWidget {
  const AdminDashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(title: const Text('League admin')),
        body: ListView(
          children: [
            _StatsHeader(service: ref.watch(adminLeagueServiceProvider)),
            const Divider(),
            _tile(context, 'Players', 'Add, edit, handicaps', '/admin/players'),
            _tile(context, 'Flights', 'Create flights & assign players', '/admin/flights'),
            _tile(context, 'Rounds', 'Schedule, finalize, scores', '/admin/rounds'),
            _tile(context, 'Courses', 'Courses & hole layouts', '/admin/courses'),
            _tile(context, 'Seasons', 'Season calendar & active season', '/admin/seasons'),
            _tile(context, 'Audit log', 'Admin actions', '/admin/audit-log'),
            _tile(context, 'Settings', 'Placeholder', '/admin/settings'),
          ],
        ),
      ),
    );
  }

  Widget _tile(
    BuildContext context,
    String title,
    String subtitle,
    String path,
  ) {
    return ListTile(
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => context.push(path),
    );
  }
}

class _StatsHeader extends StatelessWidget {
  const _StatsHeader({required this.service});

  final AdminLeagueService service;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder(
      future: Future.wait([
        service.listPlayers(),
        service.listRounds(),
      ]),
      builder: (context, snapshot) {
        final players =
            snapshot.hasData ? (snapshot.data![0] as List).length : null;
        final rounds =
            snapshot.hasData ? (snapshot.data![1] as List).length : null;
        return Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
          child: Row(
            children: [
              Expanded(
                child: _StatCard(
                  label: 'Players',
                  value: players?.toString() ?? '—',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _StatCard(
                  label: 'Rounds',
                  value: rounds?.toString() ?? '—',
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: theme.textTheme.labelMedium),
            Text(value, style: theme.textTheme.headlineSmall),
          ],
        ),
      ),
    );
  }
}
