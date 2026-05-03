import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminPlayersScreen extends ConsumerStatefulWidget {
  const AdminPlayersScreen({super.key});

  @override
  ConsumerState<AdminPlayersScreen> createState() => _AdminPlayersScreenState();
}

class _AdminPlayersScreenState extends ConsumerState<AdminPlayersScreen> {
  late Future<List<Map<String, dynamic>>> _future;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    _future = ref.read(adminLeagueServiceProvider).listPlayers();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Players'),
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
              builder: (ctx) => const _AddPlayerDialog(),
            );
            if (ok == true && mounted) setState(_reload);
          },
          icon: const Icon(Icons.person_add),
          label: const Text('Add'),
        ),
        body: FutureBuilder<List<Map<String, dynamic>>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(child: Text('${snapshot.error}'));
            }
            final rows = snapshot.data ?? [];
            if (rows.isEmpty) {
              return const Center(child: Text('No players yet.'));
            }
            return ListView.separated(
              itemCount: rows.length,
              separatorBuilder: (_, __) => const Divider(height: 1),
              itemBuilder: (context, i) {
                final p = rows[i];
                final id = (p['id'] as num).toInt();
                final name = p['fullName'] as String? ?? '';
                final email = p['email'] as String? ?? '';
                final h = p['currentHandicap'];
                return ListTile(
                  title: Text(name),
                  subtitle: Text(email),
                  trailing: Text(
                    h != null ? (h as num).toStringAsFixed(1) : '—',
                  ),
                  onTap: () => context.push('/admin/players/$id'),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _AddPlayerDialog extends ConsumerStatefulWidget {
  const _AddPlayerDialog();

  @override
  ConsumerState<_AddPlayerDialog> createState() => _AddPlayerDialogState();
}

class _AddPlayerDialogState extends ConsumerState<_AddPlayerDialog> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _hc = TextEditingController(text: '18');
  String _flightChoice = '';
  List<Map<String, dynamic>> _flights = [];
  String? _error;

  @override
  void initState() {
    super.initState();
    ref.read(adminLeagueServiceProvider).listFlights().then((f) {
      if (mounted) setState(() => _flights = f);
    });
  }

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _hc.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Add player'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: _name,
              decoration: const InputDecoration(labelText: 'Full name'),
            ),
            TextField(
              controller: _email,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
            ),
            TextField(
              controller: _hc,
              decoration: const InputDecoration(labelText: 'Initial handicap'),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
            ),
            DropdownButtonFormField<String>(
              decoration: const InputDecoration(labelText: 'Flight'),
              value: _flightChoice.isEmpty ? '' : _flightChoice,
              items: [
                const DropdownMenuItem(value: '', child: Text('Unassigned')),
                ..._flights.map(
                  (f) => DropdownMenuItem(
                    value: '${(f['id'] as num).toInt()}',
                    child: Text(f['name'] as String? ?? ''),
                  ),
                ),
              ],
              onChanged: (v) => setState(() => _flightChoice = v ?? ''),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(_error!, style: TextStyle(color: Colors.red.shade800)),
              ),
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
            final hc = double.tryParse(_hc.text);
            if (_name.text.trim().isEmpty || _email.text.trim().isEmpty || hc == null) {
              setState(() => _error = 'Fill all fields.');
              return;
            }
            try {
              await ref.read(adminLeagueServiceProvider).createPlayer(
                    name: _name.text.trim(),
                    email: _email.text.trim(),
                    initialHandicap: hc,
                    flightId:
                        _flightChoice.isEmpty ? null : _flightChoice,
                  );
              if (context.mounted) Navigator.pop(context, true);
            } catch (e) {
              setState(() => _error = '$e');
            }
          },
          child: const Text('Create'),
        ),
      ],
    );
  }
}
