import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'screens/dashboard_screen.dart';
import 'screens/round_detail_screen.dart';

final _router = GoRouter(
  initialLocation: '/',
  routes: [
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

class GolfLeagueApp extends StatelessWidget {
  const GolfLeagueApp({super.key});

  @override
  Widget build(BuildContext context) {
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
