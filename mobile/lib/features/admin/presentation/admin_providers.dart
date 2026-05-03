import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../data/admin_league_service.dart';

final adminLeagueServiceProvider = Provider<AdminLeagueService>((ref) {
  return AdminLeagueService(ref.watch(dioClientProvider));
});
