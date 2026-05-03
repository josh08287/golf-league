import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'admin_gate.dart';
import 'admin_providers.dart';

class AdminAuditScreen extends ConsumerStatefulWidget {
  const AdminAuditScreen({super.key});

  @override
  ConsumerState<AdminAuditScreen> createState() => _AdminAuditScreenState();
}

class _AdminAuditScreenState extends ConsumerState<AdminAuditScreen> {
  int _page = 1;
  static const _pageSize = 25;

  @override
  Widget build(BuildContext context) {
    return AdminGate(
      child: Scaffold(
        appBar: AppBar(title: const Text('Audit log')),
        body: FutureBuilder<Map<String, dynamic>>(
          key: ValueKey(_page),
          future: ref.read(adminLeagueServiceProvider).auditLog(
                page: _page,
                pageSize: _pageSize,
              ),
          builder: (context, snapshot) {
            if (!snapshot.hasData) {
              return const Center(child: CircularProgressIndicator());
            }
            final data = snapshot.data!;
            final items = (data['items'] as List<dynamic>? ?? [])
                .map((e) => Map<String, dynamic>.from(e as Map))
                .toList();
            final total = (data['totalCount'] as num?)?.toInt() ?? 0;
            final pages = (total / _pageSize).ceil().clamp(1, 9999);

            return Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      IconButton(
                        onPressed: _page > 1
                            ? () => setState(() => _page--)
                            : null,
                        icon: const Icon(Icons.chevron_left),
                      ),
                      Text('Page $_page / $pages'),
                      IconButton(
                        onPressed: _page < pages
                            ? () => setState(() => _page++)
                            : null,
                        icon: const Icon(Icons.chevron_right),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: ListView.builder(
                    itemCount: items.length,
                    itemBuilder: (context, i) {
                      final e = items[i];
                      return ListTile(
                        title: Text(e['action']?.toString() ?? ''),
                        subtitle: Text(
                          '${e['entityType']} ${e['entityId']}\n'
                          '${e['timestamp']}',
                        ),
                        isThreeLine: true,
                      );
                    },
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
