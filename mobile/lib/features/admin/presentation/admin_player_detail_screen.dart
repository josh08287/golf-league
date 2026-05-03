import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminPlayerDetailScreen extends ConsumerStatefulWidget {
  const AdminPlayerDetailScreen({super.key, required this.playerId});

  final int playerId;

  @override
  ConsumerState<AdminPlayerDetailScreen> createState() =>
      _AdminPlayerDetailScreenState();
}

class _AdminPlayerDetailScreenState extends ConsumerState<AdminPlayerDetailScreen> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _hcIndex = TextEditingController();
  final _hcNotes = TextEditingController();
  String _flightChoice = '';
  List<Map<String, dynamic>> _flights = [];
  bool _loading = true;
  String? _error;
  Map<String, dynamic>? _player;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final api = ref.read(adminLeagueServiceProvider);
    try {
      final p = await api.getPlayer(widget.playerId);
      final flights = await api.listFlights();
      if (!mounted) return;
      setState(() {
        _player = p;
        _flights = flights;
        _name.text = p['fullName'] as String? ?? '';
        _email.text = p['email'] as String? ?? '';
        final fid = p['flightId'];
        _flightChoice = fid == null ? '' : '$fid';
        _loading = false;
      });
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = '$e';
          _loading = false;
        });
      }
    }
  }

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _hcIndex.dispose();
    _hcNotes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Edit player'),
          actions: [
            if (_player != null && (_player!['isActive'] as bool? ?? false))
              TextButton(
                onPressed: _confirmDeactivate,
                child: const Text('Deactivate'),
              ),
            TextButton(
              onPressed: _confirmDelete,
              child: Text('Delete', style: TextStyle(color: Colors.red.shade800)),
            ),
          ],
        ),
        body: _buildBody(),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(child: Text(_error!));
    }
    if (_player == null) {
      return const Center(child: Text('Not found'));
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _name,
            decoration: const InputDecoration(labelText: 'Full name'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _email,
            decoration: const InputDecoration(labelText: 'Email'),
            keyboardType: TextInputType.emailAddress,
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            decoration: const InputDecoration(labelText: 'Flight'),
            initialValue: _flightChoice.isEmpty ? '' : _flightChoice,
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
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _saveProfile,
            child: const Text('Save'),
          ),
          const Divider(height: 32),
          Text('Manual handicap', style: Theme.of(context).textTheme.titleSmall),
          TextField(
            controller: _hcIndex,
            decoration: const InputDecoration(labelText: 'New index'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
          ),
          TextField(
            controller: _hcNotes,
            decoration: const InputDecoration(labelText: 'Notes (optional)'),
          ),
          const SizedBox(height: 8),
          FilledButton.tonal(
            onPressed: _setHandicap,
            child: const Text('Set handicap'),
          ),
          const Divider(height: 32),
          Text('Handicap history', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: 8),
          _HandicapHistoryList(playerId: widget.playerId),
        ],
      ),
    );
  }

  Future<void> _saveProfile() async {
    try {
      await ref.read(adminLeagueServiceProvider).patchPlayer(
            widget.playerId,
            name: _name.text.trim(),
            email: _email.text.trim(),
            flightId: _flightChoice.isEmpty ? '' : _flightChoice,
          );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Saved')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e')),
        );
      }
    }
  }

  Future<void> _setHandicap() async {
    final v = double.tryParse(_hcIndex.text);
    if (v == null) return;
    try {
      await ref.read(adminLeagueServiceProvider).setHandicap(
            widget.playerId,
            newIndex: v,
            notes: _hcNotes.text.trim().isEmpty ? null : _hcNotes.text.trim(),
          );
      if (mounted) {
        _hcNotes.clear();
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Handicap updated')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e')),
        );
      }
    }
  }

  Future<void> _confirmDeactivate() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Deactivate player'),
        content: const Text(
          'They will no longer appear in active lists; history is kept.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Deactivate'),
          ),
        ],
      ),
    );
    if (ok == true && mounted) {
      try {
        await ref.read(adminLeagueServiceProvider).deactivatePlayer(widget.playerId);
        if (mounted) context.pop();
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
        }
      }
    }
  }

  Future<void> _confirmDelete() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete player'),
        content: const Text(
          'Permanent removal when allowed by the server. Continue?',
        ),
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
      try {
        await ref.read(adminLeagueServiceProvider).deletePlayer(widget.playerId);
        if (mounted) context.pop();
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
        }
      }
    }
  }
}

class _HandicapHistoryList extends ConsumerStatefulWidget {
  const _HandicapHistoryList({required this.playerId});

  final int playerId;

  @override
  ConsumerState<_HandicapHistoryList> createState() =>
      _HandicapHistoryListState();
}

class _HandicapHistoryListState extends ConsumerState<_HandicapHistoryList> {
  late Future<List<Map<String, dynamic>>> _f;

  @override
  void initState() {
    super.initState();
    _f = ref.read(adminLeagueServiceProvider).handicapHistory(widget.playerId);
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<Map<String, dynamic>>>(
      future: _f,
      builder: (context, snapshot) {
        if (!snapshot.hasData) {
          return const SizedBox(height: 40);
        }
        final rows = snapshot.data!;
        if (rows.isEmpty) {
          return const Text('No history yet.');
        }
        return Column(
          children: rows.map((h) {
            final date = h['effectiveDate'] as String? ?? '';
            final idx = h['handicapIndex'];
            final src = h['source']?.toString() ?? '';
            return ListTile(
              dense: true,
              title: Text('$idx  ·  $src'),
              subtitle: Text(date),
            );
          }).toList(),
        );
      },
    );
  }
}
