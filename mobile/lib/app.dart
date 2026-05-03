import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'core/network/dio_client.dart';
import 'core/theme/app_theme.dart';
import 'features/admin/presentation/admin_audit_screen.dart';
import 'features/admin/presentation/admin_courses_screen.dart';
import 'features/admin/presentation/admin_dashboard_screen.dart';
import 'features/admin/presentation/admin_flights_screen.dart';
import 'features/admin/presentation/admin_player_detail_screen.dart';
import 'features/admin/presentation/admin_players_screen.dart';
import 'features/admin/presentation/admin_rounds_screen.dart';
import 'features/admin/presentation/admin_seasons_screen.dart';
import 'features/admin/presentation/admin_settings_screen.dart';
import 'features/dashboard/presentation/dashboard_screen.dart';
import 'features/leaderboard/presentation/flight_leaderboard_screen.dart';
import 'features/leaderboard/presentation/flight_list_screen.dart';
import 'features/player_profile/presentation/player_profile_screen.dart';
import 'features/rounds/presentation/player_scorecard_screen.dart';
import 'features/rounds/presentation/round_detail_screen.dart';
import 'features/rounds/presentation/round_history_screen.dart';
import 'features/score_entry/presentation/score_entry_screen.dart';

final _rootNavigatorKey = GlobalKey<NavigatorState>();
final _shellNavigatorKey = GlobalKey<NavigatorState>();

final routerProvider = Provider<GoRouter>((ref) {
  final tokenService = ref.watch(tokenServiceProvider);

  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: '/home',
    redirect: (context, state) {
      // Score entry requires scorer or admin role
      if (state.matchedLocation.startsWith('/score-entry')) {
        final roles = tokenService.getRoles();
        final canEnterScores =
            roles.contains('admin') || roles.contains('scorer');
        if (!canEnterScores) return '/home';
      }
      return null;
    },
    routes: [
      ShellRoute(
        navigatorKey: _shellNavigatorKey,
        builder: (context, state, child) => AppShell(child: child),
        routes: [
          GoRoute(
            path: '/home',
            builder: (context, state) => const DashboardScreen(),
          ),
          GoRoute(
            path: '/leaderboard',
            builder: (context, state) => const FlightListScreen(),
            routes: [
              GoRoute(
                path: ':flightId',
                builder: (context, state) => FlightLeaderboardScreen(
                  flightId: int.parse(state.pathParameters['flightId']!),
                ),
              ),
            ],
          ),
          GoRoute(
            path: '/rounds',
            builder: (context, state) => const RoundHistoryScreen(),
            routes: [
              GoRoute(
                path: ':roundId',
                builder: (context, state) => RoundDetailScreen(
                  roundId: int.parse(state.pathParameters['roundId']!),
                ),
                routes: [
                  GoRoute(
                    path: 'player/:playerId',
                    builder: (context, state) => PlayerScorecardScreen(
                      roundId: int.parse(state.pathParameters['roundId']!),
                      playerId: int.parse(state.pathParameters['playerId']!),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/profile',
            builder: (context, state) {
              final playerIdStr = state.uri.queryParameters['playerId'];
              return PlayerProfileScreen(
                playerId: playerIdStr != null ? int.parse(playerIdStr) : null,
              );
            },
          ),
        ],
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/score-entry/:roundId',
        builder: (context, state) => ScoreEntryScreen(
          roundId: int.parse(state.pathParameters['roundId']!),
        ),
      ),
      // League admin — flat routes (full-screen stacks like web admin).
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin',
        builder: (context, state) => const AdminDashboardScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/players',
        builder: (context, state) => const AdminPlayersScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/players/:playerId',
        builder: (context, state) => AdminPlayerDetailScreen(
          playerId: int.parse(state.pathParameters['playerId']!),
        ),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/flights',
        builder: (context, state) => const AdminFlightsScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/rounds',
        builder: (context, state) => const AdminRoundsScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/courses',
        builder: (context, state) => const AdminCoursesScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/seasons',
        builder: (context, state) => const AdminSeasonsScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/audit-log',
        builder: (context, state) => const AdminAuditScreen(),
      ),
      GoRoute(
        parentNavigatorKey: _rootNavigatorKey,
        path: '/admin/settings',
        builder: (context, state) => const AdminSettingsScreen(),
      ),
    ],
  );
});

class GolfLeagueApp extends ConsumerWidget {
  const GolfLeagueApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'Golf League',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.system,
      routerConfig: router,
    );
  }
}

class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.child});

  final Widget child;

  static const _tabs = ['/home', '/leaderboard', '/rounds', '/profile'];

  @override
  Widget build(BuildContext context) {
    final location = GoRouterState.of(context).matchedLocation;
    int selectedIndex = _tabs.indexWhere((t) => location.startsWith(t));
    if (selectedIndex < 0) selectedIndex = 0;

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: selectedIndex,
        onDestinationSelected: (i) => context.go(_tabs[i]),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.leaderboard_outlined),
            selectedIcon: Icon(Icons.leaderboard),
            label: 'Standings',
          ),
          NavigationDestination(
            icon: Icon(Icons.history_outlined),
            selectedIcon: Icon(Icons.history),
            label: 'Rounds',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outlined),
            selectedIcon: Icon(Icons.person),
            label: 'Profile',
          ),
        ],
      ),
    );
  }
}
