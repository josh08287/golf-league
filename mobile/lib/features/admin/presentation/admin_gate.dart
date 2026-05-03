import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_tick.dart';
import '../../../core/network/dio_client.dart';

/// Restricts [child] to users whose JWT includes the `admin` role claim.
class AdminGate extends ConsumerWidget {
  const AdminGate({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(authTickProvider);
    final ts = ref.read(tokenServiceProvider);
    if (!ts.getRoles().contains('admin')) {
      return Scaffold(
        appBar: AppBar(title: const Text('Admin')),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'League admin tools are available only to accounts with the '
              'admin role in Microsoft Entra.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      );
    }
    return child;
  }
}
