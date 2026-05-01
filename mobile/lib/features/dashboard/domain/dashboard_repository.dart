import 'models.dart';

abstract class DashboardRepository {
  Future<DashboardData> getDashboardData();
}
