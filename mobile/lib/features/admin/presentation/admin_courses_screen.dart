import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminCoursesScreen extends ConsumerStatefulWidget {
  const AdminCoursesScreen({super.key});

  @override
  ConsumerState<AdminCoursesScreen> createState() => _AdminCoursesScreenState();
}

class _AdminCoursesScreenState extends ConsumerState<AdminCoursesScreen> {
  late Future<List<Map<String, dynamic>>> _courses;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    _courses = ref.read(adminLeagueServiceProvider).listCourses();
  }

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Courses'),
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
              builder: (ctx) => _AddCourseDialog(
                onSubmit: (name, rating, slope) =>
                    ref.read(adminLeagueServiceProvider).createCourse(
                          name: name,
                          rating: rating,
                          slope: slope,
                        ),
              ),
            );
            if (ok == true && mounted) setState(_reload);
          },
          icon: const Icon(Icons.golf_course),
          label: const Text('Course'),
        ),
        body: FutureBuilder<List<Map<String, dynamic>>>(
          future: _courses,
          builder: (context, snapshot) {
            if (!snapshot.hasData) {
              return const Center(child: CircularProgressIndicator());
            }
            final rows = snapshot.data!;
            return ListView.builder(
              itemCount: rows.length,
              itemBuilder: (context, i) {
                final c = rows[i];
                final id = (c['id'] as num).toInt();
                return ExpansionTile(
                  title: Text(c['name'] as String? ?? ''),
                  subtitle: Text(
                    'Rating ${c['rating']} · Slope ${c['slope']} · '
                    '${c['holeCount'] ?? 0} holes',
                  ),
                  children: [
                    _CourseHolesEditor(
                      courseId: id,
                      onSaved: () => setState(_reload),
                    ),
                    ListTile(
                      leading: const Icon(Icons.delete_outline),
                      title: const Text('Delete course'),
                      onTap: () async {
                        final ok = await showDialog<bool>(
                          context: context,
                          builder: (ctx) => AlertDialog(
                            title: const Text('Delete course'),
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
                          await ref.read(adminLeagueServiceProvider).deleteCourse(id);
                          setState(_reload);
                        }
                      },
                    ),
                  ],
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _AddCourseDialog extends StatefulWidget {
  const _AddCourseDialog({required this.onSubmit});

  final Future<void> Function(String name, double rating, int slope) onSubmit;

  @override
  State<_AddCourseDialog> createState() => _AddCourseDialogState();
}

class _AddCourseDialogState extends State<_AddCourseDialog> {
  final _name = TextEditingController();
  final _rating = TextEditingController(text: '72');
  final _slope = TextEditingController(text: '113');
  String? _err;

  @override
  void dispose() {
    _name.dispose();
    _rating.dispose();
    _slope.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Add course'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: _name,
            decoration: const InputDecoration(labelText: 'Name'),
          ),
          TextField(
            controller: _rating,
            decoration: const InputDecoration(labelText: 'Rating'),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
          ),
          TextField(
            controller: _slope,
            decoration: const InputDecoration(labelText: 'Slope'),
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
            final slope = int.tryParse(_slope.text);
            final rating = double.tryParse(_rating.text);
            if (_name.text.trim().isEmpty || slope == null || rating == null) {
              setState(() => _err = 'Invalid input');
              return;
            }
            try {
              await widget.onSubmit(_name.text.trim(), rating, slope);
              if (context.mounted) Navigator.pop(context, true);
            } catch (e) {
              setState(() => _err = '$e');
            }
          },
          child: const Text('Add'),
        ),
      ],
    );
  }
}

class _CourseHolesEditor extends ConsumerStatefulWidget {
  const _CourseHolesEditor({
    required this.courseId,
    required this.onSaved,
  });

  final int courseId;
  final VoidCallback onSaved;

  @override
  ConsumerState<_CourseHolesEditor> createState() =>
      _CourseHolesEditorState();
}

class _CourseHolesEditorState extends ConsumerState<_CourseHolesEditor> {
  late Future<Map<String, dynamic>> _detail;
  List<Map<String, dynamic>> _holes = [];

  @override
  void initState() {
    super.initState();
    _detail = ref.read(adminLeagueServiceProvider).getCourse(widget.courseId);
    _detail.then((m) {
      if (!mounted) return;
      final hd = m['holeDetails'] as List<dynamic>? ??
          m['holes'] as List<dynamic>? ??
          [];
      setState(() {
        _holes = hd
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        if (_holes.isEmpty) {
          final n = (m['holeCount'] as num?)?.toInt() ?? 18;
          _holes = List.generate(
            n,
            (i) => {
              'holeNumber': i + 1,
              'par': 4,
              'strokeIndex': i + 1,
            },
          );
        }
      });
    });
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Map<String, dynamic>>(
      future: _detail,
      builder: (context, snapshot) {
        if (!snapshot.hasData) {
          return const Padding(
            padding: EdgeInsets.all(16),
            child: CircularProgressIndicator(),
          );
        }
        return Column(
          children: [
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(
                onPressed: () async {
                  await ref.read(adminLeagueServiceProvider).updateCourseHoles(
                        widget.courseId,
                        _holes,
                      );
                  widget.onSaved();
                  if (mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Holes saved')),
                    );
                  }
                },
                child: const Text('Save holes'),
              ),
            ),
            ..._holes.asMap().entries.map((e) {
              final i = e.key;
              final row = e.value;
              final hn = (row['holeNumber'] as num?)?.toInt() ?? i + 1;
              final par = (row['par'] as num?)?.toInt() ?? 4;
              final si = (row['strokeIndex'] as num?)?.toInt() ?? hn;
              return ListTile(
                dense: true,
                leading: Text('$hn'),
                title: Row(
                  children: [
                    SizedBox(
                      width: 72,
                      child: TextFormField(
                        initialValue: '$par',
                        decoration: const InputDecoration(labelText: 'Par'),
                        keyboardType: TextInputType.number,
                        onChanged: (v) {
                          final p = int.tryParse(v);
                          if (p != null) {
                            _holes[i]['par'] = p;
                          }
                        },
                      ),
                    ),
                    const SizedBox(width: 12),
                    SizedBox(
                      width: 72,
                      child: TextFormField(
                        initialValue: '$si',
                        decoration: const InputDecoration(labelText: 'SI'),
                        keyboardType: TextInputType.number,
                        onChanged: (v) {
                          final s = int.tryParse(v);
                          if (s != null) {
                            _holes[i]['strokeIndex'] = s;
                          }
                        },
                      ),
                    ),
                  ],
                ),
              );
            }),
          ],
        );
      },
    );
  }
}
