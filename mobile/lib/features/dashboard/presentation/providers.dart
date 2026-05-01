import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/network/dio_client.dart';
import '../data/dashboard_repository_impl.dart';
import '../domain/dashboard_repository.dart';
import '../domain/models.dart';

part 'providers.g.dart';

@riverpod
DashboardRepository dashboardRepository(DashboardRepositoryRef ref) {
  return DashboardRepositoryImpl(dio: ref.watch(dioClientProvider));
}

@riverpod
Future<DashboardData> dashboard(DashboardRef ref) async {
  final repo = ref.watch(dashboardRepositoryProvider);
  return repo.getDashboardData();
}
