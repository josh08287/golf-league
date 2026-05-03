import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminFlightsScreen extends ConsumerStatefulWidget {
  const AdminFlightsScreen({super.key});

  @override
  ConsumerState<AdminFlightsScreen> createState() => _AdminFlightsScreenState();
}

class _AdminFlightsScreenState extends ConsumerState<AdminFlightsScreen> {
  late Future<List<Map<String, dynamic>>> _flights;
  late Future<List<Map<String, dynamic>>> _players;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    final api = ref.read(adminLeagueServiceProvider);
    _flights = api.listFlights();
    _players = api.listPlayers();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Flights'),
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
              builder: (ctx) => _CreateFlightDialog(
                onCreate: (name, minH, maxH, order) async {
                  await ref.read(adminLeagueServiceProvider).createFlight(
                        name: name,
                        minHandicap: minH,
                        maxHandicap: maxH,
                        displayOrder: order,
                      );
                },
              ),
            );
            if (ok == true && mounted) setState(_reload);
          },
          icon: const Icon(Icons.add),
          label: const Text('Flight'),
        ),
        body: FutureBuilder<List<Map<String, dynamic>>>(
          future: _flights,
          builder: (context, snap) {
            if (!snap.hasData) {
              return const Center(child: CircularProgressIndicator());
            }
            final flights = snap.data!;
            return ListView(
              padding: const EdgeInsets.only(bottom: 120),
              children: [
                ...flights.map((f) {
                  final id = (f['id'] as num).toInt();
                  final name = f['name'] as String? ?? '';
                  return Card(
                    margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                    child: ListTile(
                      title: Text(name),
                      subtitle: Text(
                        'Order ${f['displayOrder'] ?? 0} · '
                        '${f['minHandicap'] ?? '—'}–${f['maxHandicap'] ?? '—'}',
                      ),
                      trailing: IconButton(
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () async {
                          final ok = await showDialog<bool>(
                            context: context,
                            builder: (ctx) => AlertDialog(
                              title: const Text('Delete flight'),
                              content: Text('Remove $name?'),
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
                            await ref.read(adminLeagueServiceProvider).deleteFlight(id);
                            setState(_reload);
                          }
                        },
                      ),
                    ),
                  );
                }),
                const Divider(),
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Text(
                    'Assign players',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                ),
                FutureBuilder<List<Map<String, dynamic>>>(
                  future: _players,
                  builder: (context, pSnap) {
                    if (!pSnap.hasData) {
                      return const SizedBox.shrink();
                    }
                    final players = pSnap.data!;
                    return FutureBuilder<List<Map<String, dynamic>>>(
                      future: _flights,
                      builder: (context, fSnap) {
                        if (!fSnap.hasData) {
                          return const SizedBox.shrink();
                        }
                        final fs = fSnap.data!;
                        return Column(
                          children: players.map((p) {
                            final pid = (p['id'] as num).toInt();
                            final name = p['fullName'] as String? ?? '';
                            final cur = p['flightId'];
                            var choice = cur == null ? '' : '${(cur as num).toInt()}';
                            return Padding(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 16,
                                vertical: 4,
                              ),
                              child: Row(
                                children: [
                                  Expanded(child: Text(name)),
                                  SizedBox(
                                    width: 160,
                                    child: DropdownButtonFormField<String>(
                                      value: choice.isEmpty ? '' : choice,
                                      items: [
                                        const DropdownMenuItem(
                                          value: '',
                                          child: Text('—'),
                                        ),
                                        ...fs.map(
                                          (fl) => DropdownMenuItem(
                                            value: '${(fl['id'] as num).toInt()}',
                                            child: Text(
                                              fl['name'] as String? ?? '',
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                          ),
                                        ),
                                      ],
                                      onChanged: (v) async {
                                        choice = v ?? '';
                                        try {
                                          await ref
                                              .read(adminLeagueServiceProvider)
                                              .patchPlayer(
                                                pid,
                                                flightId: choice,
                                              );
                                          if (mounted) setState(_reload);
                                        } catch (_) {}
                                      },
                                    ),
                                  ),
                                ],
                              ),
                            );
                          }).toList(),
                        );
                      },
                    );
                  },
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _CreateFlightDialog extends StatefulWidget {
  const _CreateFlightDialog({required this.onCreate});

  final Future<void> Function(
    String name,
    double? minH,
    double? maxH,
    int order,
  ) onCreate;

  @override
  State<_CreateFlightDialog> createState() => _CreateFlightDialogState();
}

class _CreateFlightDialogState extends State<_CreateFlightDialog> {
  final _name = TextEditingController();
  final _min = TextEditingController();
  final _max = TextEditingController();
  final _order = TextEditingController(text: '0');
  String? _err;

  @override
  void dispose() {
    _name.dispose();
    _min.dispose();
    _max.dispose();
    _order.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Create flight'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: _name,
            decoration: const InputDecoration(labelText: 'Name'),
          ),
          TextField(
            controller: _min,
            decoration: const InputDecoration(labelText: 'Min HCP'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
          ),
          TextField(
            controller: _max,
            decoration: const InputDecoration(labelText: 'Max HCP'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
          ),
          TextField(
            controller: _order,
            decoration: const InputDecoration(labelText: 'Display order'),
            keyboardType: TextInputType.number,
          ),
          if (_err != null)
            Text(_err!, style: TextStyle(color: Colors.red.shade800)),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () async {
            if (_name.text.trim().isEmpty) {
              setState(() => _err = 'Name required');
              return;
            }
            try {
              await widget.onCreate(
                _name.text.trim(),
                double.tryParse(_min.text),
                double.tryParse(_max.text),
                int.tryParse(_order.text) ?? 0,
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
