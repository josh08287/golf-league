import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminSeasonsScreen extends ConsumerStatefulWidget {
  const AdminSeasonsScreen({super.key});

  @override
  ConsumerState<AdminSeasonsScreen> createState() => _AdminSeasonsScreenState();
}

class _AdminSeasonsScreenState extends ConsumerState<AdminSeasonsScreen> {
  late Future<List<Map<String, dynamic>>> _seasons;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    _seasons = ref.read(adminLeagueServiceProvider).listSeasons();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Seasons'),
          actions: [
            IconButton(
              icon: const Icon(Icons.refresh),
              onPressed: () => setState(_reload),
            ),
          ],
        ),
        floatingActionButton: FloatingActionButton.extended(
          onPressed: () async {
            final ok = await showDialog<bool>(
              context: context,
              builder: (ctx) => _CreateSeasonForm(
                onSubmit: (payload) =>
                    ref.read(adminLeagueServiceProvider).createSeason(
                          name: payload.name,
                          year: payload.year,
                          startDate: payload.startDate,
                          endDate: payload.endDate,
                          bestNRounds: payload.bestNRounds,
                        ),
              ),
            );
            if (ok == true && mounted) setState(_reload);
          },
          icon: const Icon(Icons.add),
          label: const Text('Season'),
        ),
        body: FutureBuilder<List<Map<String, dynamic>>>(
          future: _seasons,
          builder: (context, snapshot) {
            if (!snapshot.hasData) {
              return const Center(child: CircularProgressIndicator());
            }
            final rows = snapshot.data!;
            if (rows.isEmpty) {
              return const Center(child: Text('No seasons.'));
            }
            return ListView.builder(
              itemCount: rows.length,
              itemBuilder: (context, i) {
                final s = rows[i];
                final id = (s['id'] as num).toInt();
                final active = s['isActive'] == true;
                return Card(
                  margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  child: ListTile(
                    title: Text(s['name'] as String? ?? ''),
                    subtitle: Text(
                      '${s['year']} · ${s['startDate']} → ${s['endDate']}',
                    ),
                    trailing: Wrap(
                      spacing: 4,
                      children: [
                        if (!active)
                          TextButton(
                            onPressed: () async {
                              await ref
                                  .read(adminLeagueServiceProvider)
                                  .activateSeason(id);
                              if (mounted) setState(_reload);
                            },
                            child: const Text('Activate'),
                          ),
                        IconButton(
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () async {
                            final ok = await showDialog<bool>(
                              context: context,
                              builder: (ctx) => AlertDialog(
                                title: const Text('Delete season'),
                                actions: [
                                  TextButton(
                                    onPressed: () => Navigator.pop(ctx, false),
                                    child: const Text('Cancel'),
                                  ),
                                  FilledButton(
                                    onPressed: () => Navigator.pop(ctx, true),
                                    child: const Text('Delete'),
                                  ),
                                ],
                              ),
                            );
                            if (ok == true && mounted) {
                              await ref
                                  .read(adminLeagueServiceProvider)
                                  .deleteSeason(id);
                              setState(_reload);
                            }
                          },
                        ),
                      ],
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _SeasonPayload {
  const _SeasonPayload({
    required this.name,
    required this.year,
    required this.startDate,
    required this.endDate,
    this.bestNRounds,
  });

  final String name;
  final int year;
  final String startDate;
  final String endDate;
  final int? bestNRounds;
}

class _CreateSeasonForm extends StatefulWidget {
  const _CreateSeasonForm({required this.onSubmit});

  final Future<void> Function(_SeasonPayload payload) onSubmit;

  @override
  State<_CreateSeasonForm> createState() => _CreateSeasonFormState();
}

class _CreateSeasonFormState extends State<_CreateSeasonForm> {
  final _name = TextEditingController();
  final _year = TextEditingController();
  final _start = TextEditingController();
  final _end = TextEditingController();
  final _bestN = TextEditingController();
  String? _err;

  @override
  void initState() {
    super.initState();
    final y = DateTime.now().year;
    _year.text = '$y';
    _start.text = '$y-01-01';
    _end.text = '$y-12-31';
    _name.text = '$y Season';
  }

  @override
  void dispose() {
    _name.dispose();
    _year.dispose();
    _start.dispose();
    _end.dispose();
    _bestN.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Create season'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: _name,
              decoration: const InputDecoration(labelText: 'Name'),
            ),
            TextField(
              controller: _year,
              decoration: const InputDecoration(labelText: 'Year'),
              keyboardType: TextInputType.number,
            ),
            TextField(
              controller: _start,
              decoration: const InputDecoration(labelText: 'Start (yyyy-MM-dd)'),
            ),
            TextField(
              controller: _end,
              decoration: const InputDecoration(labelText: 'End (yyyy-MM-dd)'),
            ),
            TextField(
              controller: _bestN,
              decoration: const InputDecoration(
                labelText: 'Best N rounds (optional)',
              ),
              keyboardType: TextInputType.number,
            ),
            if (_err != null)
              Text(_err!, style: TextStyle(color: Colors.red.shade800)),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () async {
            final year = int.tryParse(_year.text);
            if (year == null || _name.text.trim().isEmpty) {
              setState(() => _err = 'Invalid');
              return;
            }
            try {
              await widget.onSubmit(
                _SeasonPayload(
                  name: _name.text.trim(),
                  year: year,
                  startDate: _start.text.trim(),
                  endDate: _end.text.trim(),
                  bestNRounds: int.tryParse(_bestN.text),
                ),
              );
              if (context.mounted) Navigator.pop(context, true);
            } catch (e) {
              setState(() => _err = '$e');
            }
          },
          child: const Text('Create'),
        ),
      ],
    );
  }
}
