import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminRoundsScreen extends ConsumerStatefulWidget {
  const AdminRoundsScreen({super.key});

  @override
  ConsumerState<AdminRoundsScreen> createState() => _AdminRoundsScreenState();
}

class _AdminRoundsScreenState extends ConsumerState<AdminRoundsScreen> {
  late Future<List<Map<String, dynamic>>> _rounds;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    _rounds = ref.read(adminLeagueServiceProvider).listRounds();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Rounds'),
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
              builder: (ctx) => const _CreateRoundDialog(),
            );
            if (ok == true && mounted) setState(_reload);
          },
          icon: const Icon(Icons.add),
          label: const Text('Round'),
        ),
        body: FutureBuilder<List<Map<String, dynamic>>>(
          future: _rounds,
          builder: (context, snapshot) {
            if (!snapshot.hasData) {
              return const Center(child: CircularProgressIndicator());
            }
            final rows = snapshot.data!;
            if (rows.isEmpty) {
              return const Center(child: Text('No rounds.'));
            }
            return ListView.builder(
              itemCount: rows.length,
              itemBuilder: (context, i) {
                final r = rows[i];
                final id = (r['id'] as num).toInt();
                final status = r['status']?.toString() ?? '';
                final dateStr =
                    r['scheduledDate']?.toString() ?? r['roundDate']?.toString() ?? '';
                return Card(
                  margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  child: ListTile(
                    title: Text(r['courseName']?.toString() ?? 'Course'),
                    subtitle: Text('$dateStr · $status · ${r['flightName'] ?? ''}'),
                    isThreeLine: true,
                    trailing: Wrap(
                      spacing: 4,
                      children: [
                        TextButton(
                          onPressed: () =>
                              context.push('/score-entry/$id'),
                          child: Text(
                            status.contains('Finalized') ? 'Scores' : 'Scores',
                          ),
                        ),
                        if (status == 'InProgress')
                          TextButton(
                            onPressed: () async {
                              await ref
                                  .read(adminLeagueServiceProvider)
                                  .finalizeRound(id);
                              if (mounted) setState(_reload);
                            },
                            child: const Text('Finalize'),
                          ),
                        if (!status.contains('Finalized'))
                          IconButton(
                            icon: const Icon(Icons.delete_outline),
                            onPressed: () async {
                              final ok = await showDialog<bool>(
                                context: context,
                                builder: (ctx) => AlertDialog(
                                  title: const Text('Delete round'),
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
                                    .deleteRound(id);
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

class _CreateRoundDialog extends ConsumerStatefulWidget {
  const _CreateRoundDialog();

  @override
  ConsumerState<_CreateRoundDialog> createState() => _CreateRoundDialogState();
}

class _CreateRoundDialogState extends ConsumerState<_CreateRoundDialog> {
  String _scheduled = '';
  String _courseId = '';
  String _flightId = '';
  final Set<int> _selected = {};
  List<Map<String, dynamic>> _courses = [];
  List<Map<String, dynamic>> _flights = [];
  List<Map<String, dynamic>> _players = [];
  String? _err;

  @override
  void initState() {
    super.initState();
    final api = ref.read(adminLeagueServiceProvider);
    Future.wait([
      api.listCourses(),
      api.listFlights(),
      api.listPlayers(),
    ]).then((parts) {
      if (!mounted) return;
      setState(() {
        _courses = parts[0];
        _flights = parts[1];
        _players = parts[2];
      });
    });
    final t = DateTime.now();
    _scheduled =
        '${t.year.toString().padLeft(4, '0')}-${t.month.toString().padLeft(2, '0')}-${t.day.toString().padLeft(2, '0')}';
  }

  List<Map<String, dynamic>> get _flightPlayers {
    if (_flightId.isEmpty) return [];
    final fid = int.tryParse(_flightId);
    if (fid == null) return [];
    return _players.where((p) {
      if (!(p['isActive'] as bool? ?? true)) return false;
      final pf = p['flightId'];
      if (pf == null) return false;
      return (pf as num).toInt() == fid;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Create round'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ListTile(
              title: Text('Date: $_scheduled'),
              trailing: const Icon(Icons.calendar_today),
              onTap: () async {
                final d = await showDatePicker(
                  context: context,
                  initialDate: DateTime.now(),
                  firstDate: DateTime(2000),
                  lastDate: DateTime(2100),
                );
                if (d != null) {
                  setState(() {
                    _scheduled =
                        '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
                  });
                }
              },
            ),
            DropdownButtonFormField<String>(
              decoration: const InputDecoration(labelText: 'Course'),
              initialValue: _courseId.isEmpty ? null : _courseId,
              items: _courses
                  .map(
                    (c) => DropdownMenuItem(
                      value: '${(c['id'] as num).toInt()}',
                      child: Text(c['name'] as String? ?? ''),
                    ),
                  )
                  .toList(),
              onChanged: (v) => setState(() => _courseId = v ?? ''),
            ),
            DropdownButtonFormField<String>(
              decoration: const InputDecoration(labelText: 'Flight'),
              initialValue: _flightId.isEmpty ? null : _flightId,
              items: _flights
                  .map(
                    (f) => DropdownMenuItem(
                      value: '${(f['id'] as num).toInt()}',
                      child: Text(f['name'] as String? ?? ''),
                    ),
                  )
                  .toList(),
              onChanged: (v) {
                setState(() {
                  _flightId = v ?? '';
                  _selected
                    ..clear()
                    ..addAll(
                      _flightPlayers
                          .map((p) => (p['id'] as num).toInt()),
                    );
                });
              },
            ),
            const SizedBox(height: 8),
            Text('Players', style: Theme.of(context).textTheme.titleSmall),
            ..._flightPlayers.map((p) {
              final pid = (p['id'] as num).toInt();
              return CheckboxListTile(
                value: _selected.contains(pid),
                title: Text(p['fullName'] as String? ?? ''),
                onChanged: (checked) {
                  setState(() {
                    if (checked == true) {
                      _selected.add(pid);
                    } else {
                      _selected.remove(pid);
                    }
                  });
                },
              );
            }),
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
            if (_courseId.isEmpty || _flightId.isEmpty) {
              setState(() => _err = 'Select course and flight.');
              return;
            }
            if (_selected.isEmpty) {
              setState(() => _err = 'Pick at least one player.');
              return;
            }
            try {
              await ref.read(adminLeagueServiceProvider).createRound(
                    flightId: int.parse(_flightId),
                    courseId: int.parse(_courseId),
                    scheduledDate: _scheduled,
                    playerIds: _selected.toList(),
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
