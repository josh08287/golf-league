import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'auth/auth_providers.dart';
import 'screens/dashboard_screen.dart';
import 'screens/flights_screen.dart';
import 'screens/flight_leaderboard_screen.dart';
import 'screens/leaderboard_tab.dart';
import 'screens/login_screen.dart';
import 'screens/not_invited_screen.dart';
import 'screens/player_profile_screen.dart';
import 'screens/players_screen.dart';
import 'screens/register_screen.dart';
import 'screens/round_detail_screen.dart';
import 'screens/rounds_screen.dart';
import 'screens/score_entry_screen.dart';
import 'screens/statistics_screen.dart';
import 'screens/tee_times_screen.dart';

final _router = GoRouter(
  initialLocation: '/splash',
  routes: [
    GoRoute(path: '/splash', builder: (_, _) => const _SplashScreen()),
    GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
    GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),
    GoRoute(path: '/not-invited', builder: (_, _) => const NotInvitedScreen()),
    GoRoute(
      path: '/',
      builder: (context, state) => const MainNavigationScreen(),
    ),
    GoRoute(path: '/flights', builder: (_, _) => const FlightsScreen()),
    GoRoute(
      path: '/flights/:flightId/leaderboard',
      builder: (_, state) {
        final flightId = int.parse(state.pathParameters['flightId']!);
        final halfId = state.uri.queryParameters['halfId'] ?? '';
        return FlightLeaderboardScreen(flightId: flightId, halfId: halfId);
      },
    ),
    GoRoute(path: '/players', builder: (_, _) => const PlayersScreen()),
    GoRoute(
      path: '/players/:playerId',
      builder: (_, state) => PlayerProfileScreen(
        playerId: int.parse(state.pathParameters['playerId']!),
      ),
    ),
    GoRoute(path: '/rounds', builder: (_, _) => const RoundsScreen()),
    GoRoute(
      path: '/rounds/:roundId',
      builder: (_, state) => RoundDetailScreen(
        roundId: int.parse(state.pathParameters['roundId']!),
      ),
    ),
    GoRoute(
      path: '/rounds/:roundId/tee-times',
      builder: (_, state) =>
          TeeTimesScreen(roundId: int.parse(state.pathParameters['roundId']!)),
    ),
    GoRoute(
      path: '/tee-times/:teeTimeId/enter-scores',
      builder: (_, state) => ScoreEntryScreen(
        teeTimeId: int.parse(state.pathParameters['teeTimeId']!),
      ),
    ),
    GoRoute(path: '/statistics', builder: (_, _) => const StatisticsScreen()),
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
      default:
        context.go('/not-invited');
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}

// ── Main Navigation with Bottom Nav Bar ────────────────────────────────────────

class MainNavigationScreen extends ConsumerStatefulWidget {
  const MainNavigationScreen({super.key});

  @override
  ConsumerState<MainNavigationScreen> createState() =>
      _MainNavigationScreenState();
}

class _MainNavigationScreenState extends ConsumerState<MainNavigationScreen> {
  int _currentIndex = 0;

  final _screens = const [
    DashboardScreen(),
    LeaderboardTab(),
    RoundsScreen(),
    PlayersScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _screens[_currentIndex],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) => setState(() => _currentIndex = index),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.emoji_events_outlined),
            selectedIcon: Icon(Icons.emoji_events),
            label: 'Leaderboard',
          ),
          NavigationDestination(
            icon: Icon(Icons.calendar_today_outlined),
            selectedIcon: Icon(Icons.calendar_today),
            label: 'Rounds',
          ),
          NavigationDestination(
            icon: Icon(Icons.people_outlined),
            selectedIcon: Icon(Icons.people),
            label: 'Players',
          ),
        ],
      ),
      drawer: _buildDrawer(),
    );
  }

  Widget _buildDrawer() {
    final status = ref.watch(myStatusProvider);
    final isAdmin = status.role == 'admin' || status.role == 'scorer';

    return Drawer(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          DrawerHeader(
            decoration: const BoxDecoration(color: Color(0xFF1a5c38)),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(Icons.golf_course, color: Colors.white, size: 48),
                const SizedBox(height: 12),
                const Text(
                  'Golf League',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 20,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  isAdmin ? 'Administrator' : 'Player',
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.8),
                    fontSize: 14,
                  ),
                ),
              ],
            ),
          ),
          ListTile(
            leading: const Icon(Icons.home),
            title: const Text('Home'),
            selected: _currentIndex == 0,
            onTap: () {
              context.pop();
              setState(() => _currentIndex = 0);
            },
          ),
          ListTile(
            leading: const Icon(Icons.emoji_events),
            title: const Text('Flights & Leaderboard'),
            selected: _currentIndex == 1,
            onTap: () {
              context.pop();
              setState(() => _currentIndex = 1);
            },
          ),
          ListTile(
            leading: const Icon(Icons.calendar_today),
            title: const Text('Rounds'),
            selected: _currentIndex == 2,
            onTap: () {
              context.pop();
              setState(() => _currentIndex = 2);
            },
          ),
          ListTile(
            leading: const Icon(Icons.people),
            title: const Text('Players'),
            selected: _currentIndex == 3,
            onTap: () {
              context.pop();
              setState(() => _currentIndex = 3);
            },
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.schedule),
            title: const Text('Tee Times'),
            onTap: () {
              context.pop();
              setState(() => _currentIndex = 0);
            },
          ),
          ListTile(
            leading: const Icon(Icons.bar_chart),
            title: const Text('Statistics'),
            onTap: () {
              context.pop();
              context.push('/statistics');
            },
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout, color: Colors.red),
            title: const Text('Sign Out', style: TextStyle(color: Colors.red)),
            onTap: () async {
              final auth = ref.read(authServiceProvider);
              final router = GoRouter.of(context);
              await auth.signOut();
              router.go('/login');
            },
          ),
        ],
      ),
    );
  }
}
