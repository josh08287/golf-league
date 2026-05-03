import 'package:flutter_riverpod/flutter_riverpod.dart';

class AuthTickNotifier extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

/// Bump after login/logout so listeners refresh auth-derived UI.
final authTickProvider =
    NotifierProvider<AuthTickNotifier, int>(AuthTickNotifier.new);

void bumpAuthTick(WidgetRef ref) {
  ref.read(authTickProvider.notifier).bump();
}
