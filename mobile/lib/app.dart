import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth/auth_providers.dart';
import 'screens/dashboard_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/round_detail_screen.dart';

final _router = GoRouter(
  initialLocation: '/splash',
  routes: [
    GoRoute(
      path: '/splash',
      builder: (_, _) => const _SplashScreen(),
    ),
    GoRoute(
      path: '/login',
      builder: (_, _) => const LoginScreen(),
    ),
    GoRoute(
      path: '/register',
      builder: (_, _) => const RegisterScreen(),
    ),
    GoRoute(
      path: '/',
      builder: (context, state) => const DashboardScreen(),
    ),
    GoRoute(
      path: '/rounds/:roundId',
      builder: (_, state) => RoundDetailScreen(
        roundId: int.parse(state.pathParameters['roundId']!),
      ),
    ),
  ],
);

class GolfLeagueApp extends ConsumerWidget {
  const GolfLeagueApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'Golf League',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF1a5c38),
          brightness: Brightness.light,
        ),
        useMaterial3: true,
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF1a5c38),
          foregroundColor: Colors.white,
          elevation: 0,
        ),
      ),
      routerConfig: _router,
    );
  }
}

/// Checks auth state on startup and routes accordingly.
class _SplashScreen extends ConsumerStatefulWidget {
  const _SplashScreen();

  @override
  ConsumerState<_SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<_SplashScreen> {
  @override
  void initState() {
    super.initState();
    _checkAuthAndRoute();
  }

  Future<void> _checkAuthAndRoute() async {
    final auth = ref.read(authServiceProvider);
    final isSignedIn = await auth.isSignedIn();

    if (!isSignedIn) {
      if (mounted) context.go('/login');
      return;
    }

    // Silently refresh to get fresh claims
    final result = await auth.refresh();
    if (result != null) {
      ref.read(authResultProvider.notifier).state = result;
    }

    await ref.read(myStatusProvider.notifier).fetch();

    if (!mounted) return;
    final status = ref.read(myStatusProvider).status;

    switch (status) {
      case 'approved':
        context.go('/');
      case 'none':
      case 'pending':
      case 'rejected':
        context.go('/register');
      default:
        context.go('/login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    );
  }
}
