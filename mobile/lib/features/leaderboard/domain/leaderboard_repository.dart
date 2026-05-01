import 'models.dart';

abstract class LeaderboardRepository {
  Future<List<Flight>> getFlights();
  Future<List<LeaderboardEntry>> getStandings(int flightId);
}
