import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../data/dashboard_repository_impl.dart';
import '../domain/dashboard_repository.dart';
import '../domain/models.dart';

final dashboardRepositoryProvider = Provider<DashboardRepository>((ref) {
  return DashboardRepositoryImpl(dio: ref.watch(dioClientProvider));
});

final dashboardProvider = FutureProvider<DashboardData>((ref) async {
  final repo = ref.watch(dashboardRepositoryProvider);
  return repo.getDashboardData();
});
