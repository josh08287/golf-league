// ── Generic API shapes ─────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  data: T;
  errors: string[];
}

export interface PageMeta {
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PagedResponse<T> {
  data: T[];
  meta: PageMeta;
  errors: string[];
}

// ── Domain DTOs ───────────────────────────────────────────────────────────────

export type TeeTimeSlotPreference = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;
export const TEE_TIME_SLOTS = ['Early', 'Middle', 'Late'] as const;
export type TeeTimeSlotName = (typeof TEE_TIME_SLOTS)[number];
export const TEE_TIME_SLOT_FLAG: Record<TeeTimeSlotName, number> = {
  Early: 1,
  Middle: 2,
  Late: 4,
};

export interface HalfFlightMembership {
  halfId: number;
  flightId: number;
  flightName: string;
  par3GrossSkinsOptIn: boolean;
}

export interface Player {
  id: number;
  fullName: string;
  email: string | null;
  isActive: boolean;
  currentHandicap: number | null;
  flightId: number | null;
  flightName: string | null;
  roles: ('admin' | 'scorer' | 'player')[];
  // Null when no AppUser is linked yet. Drives the "Link to user account"
  // affordance on the admin player-detail page.
  appUserId: string | null;
  preferredTeeTimeSlots: TeeTimeSlotPreference;
  flightMemberships: HalfFlightMembership[];
  teeTimeEmailOptOut: boolean;
  isSubstitute: boolean;
  // True when the player is in a flight for the half in progress today —
  // blocks adding them to the substitute pool. Completed and not-yet-started
  // halves don't count.
  hasCurrentHalfFlightMembership: boolean;
}

/** Lightweight row used by the unlinked-players pickers. */
export interface UnlinkedPlayer {
  id: number;
  fullName: string;
  email: string | null;
}

export interface UnlinkedSubstitute extends UnlinkedPlayer {
  isActive: boolean;
}

export interface HandicapHistoryEntry {
  id: number;
  playerId: number;
  handicapIndex: number;
  nineHoleHandicapIndex: number;
  effectiveDate: string;
  source: 'Manual' | 'Calculated' | 'Initial';
  notes: string | null;
}

export interface Flight {
  id: number;
  name: string;
  seasonId: number;
  halfId: number;
  displayOrder: number;
  playerCount: number;
  isLocked: boolean;
}

export interface RoundScore {
  roundId: number;
  roundDate: string;
  weekNumber: number;
  points: number | null;
  grossStrokes: number | null;
  netStrokes: number | null;
  isSkipped: boolean;
  isDropped: boolean;
}

export interface Standing {
  position: number;
  playerId: number;
  playerFullName: string;
  playerInitials: string;
  roundsPlayed: number;
  totalPoints: number;
  averagePoints: number;
  currentHandicapIndex: number;
  averageScore: number | null;
  roundScores: RoundScore[];
}

export type RoundStatus = 'Scheduled' | 'InProgress' | 'PendingFinalization' | 'Finalized' | 'Cancelled';
export type NineHoleSide = 'Front' | 'Back' | 'NotApplicable';
export type RoundType = 'NineHole' | 'EighteenHole' | 'Tournament';

export interface PlayerRoundSummary {
  roundId: number;
  roundDate: string;
  weekNumber: number;
  courseName: string;
  nineHoleSide: NineHoleSide;
  status: RoundStatus;
  totalGrossStrokes: number | null;
  totalNetStrokes: number | null;
  totalGrossStablefordPoints: number | null;
  totalNetStablefordPoints: number | null;
  isWithdrawn: boolean;
  skippedWeek: boolean;
  scoreDifferential: number | null;
}

export interface Round {
  id: number;
  seasonId: number;
  halfId: number;
  courseId: number;
  courseName: string;
  weekNumber: number;
  scheduledDate: string;
  status: RoundStatus;
  nineHoleSide: NineHoleSide;
  roundType: RoundType;
  participantCount: number;
}

export interface Participant {
  id: number;
  roundId: number;
  playerId: number;
  playerName: string;
  flightId: number;
  handicapAtTime: number;
  courseHandicap: number;
  isWithdrawn: boolean;
  skippedWeek: boolean;
}

export interface HoleScore {
  id?: number;
  holeNumber: number;
  par: number;
  strokeIndex: number;
  grossStrokes: number;
  handicapStrokes: number;
  netStrokes: number;
  grossStablefordPoints: number;
  netStablefordPoints: number;
  isMaxScore: boolean;
}

export interface ScorecardParticipant {
  id: number;
  roundId: number;
  playerId: number;
  playerFullName: string;
  playerInitials: string;
  flightId: number;
  handicapIndex: number;
  courseHandicap: number;
  totalGrossStrokes: number | null;
  totalNetStrokes: number | null;
  totalGrossStablefordPoints: number | null;
  totalNetStablefordPoints: number | null;
  isWithdrawn: boolean;
  skippedWeek: boolean;
}

export interface Scorecard {
  roundId: number;
  roundDate: string;
  courseName: string;
  courseRating: number;
  slopeRating: number;
  participant: ScorecardParticipant;
  holeScores: HoleScore[];
  totalPar: number;
  totalGross: number;
  totalNet: number;
  totalGrossPoints: number;
  totalNetPoints: number;
}

export interface RoundScorecardHole {
  holeNumber: number;
  par: number;
  strokes: number;
  netStrokes: number;
  strokeIndex: number;
  grossPoints: number;
  netPoints: number;
  handicapStrokes: number;
}

export interface RoundScorecard {
  roundId: number;
  playerId: number;
  playerName: string;
  flightId: number;
  flightName: string;
  courseName: string;
  scheduledDate: string;
  handicapAtTime: number;
  courseHandicap: number;
  grossScore: number | null;
  netScore: number | null;
  grossPoints: number | null;
  netPoints: number | null;
  holes: RoundScorecardHole[];
}

// ── Active Round Leaderboard ─────────────────────────────────────────────────

export interface ActiveRoundLeaderboardHole {
  holeNumber: number;
  par: number;
  strokeIndex: number;
  grossStrokes: number;
  handicapStrokes: number;
}

export interface ActiveRoundLeaderboardEntry {
  playerId: number;
  playerName: string;
  flightId: number | null;
  flightName: string;
  thruHoles: number;
  grossScore: number | null;
  netScore: number | null;
  grossPoints: number | null;
  netPoints: number | null;
  rank: number;
  holes: ActiveRoundLeaderboardHole[];
}

export interface ActiveRoundLeaderboardFlight {
  flightId: number | null;
  flightName: string;
  entries: ActiveRoundLeaderboardEntry[];
}

export interface ActiveRoundLeaderboard {
  roundId: number;
  courseName: string;
  scheduledDate: string;
  nineHoleSide: string;
  flights: ActiveRoundLeaderboardFlight[];
}

// ── Skins ──────────────────────────────────────────────────────────────────────

export interface HoleSkin {
  holeNumber: number;
  skinValue: number;
  winnerPlayerId: number;
  winnerPlayerName: string;
  winningNetScore: number;
  wasCarryover: boolean;
}

export interface PlayerSkinSummary {
  playerId: number;
  playerName: string;
  totalSkinsWon: number;
  totalSkinValue: number;
  holesWon: HoleSkin[];
}

export interface FlightSkins {
  flightId: number;
  flightName: string;
  totalHolesWithSkins: number;
  totalSkinValueAwarded: number;
  playerSummaries: PlayerSkinSummary[];
  allHoleResults: HoleSkin[];
}

export interface GrossPar3Skin {
  holeNumber: number;
  par: number;
  skinValue: number;
  winnerPlayerId: number;
  winnerPlayerName: string;
  winnerFlightId: number;
  winnerFlightName: string;
  winningGrossScore: number;
  wasCarryover: boolean;
  /** Set when a player with a strictly better score wasn't opted in to par-3 gross skins. */
  notOptedInPlayerId: number | null;
  notOptedInPlayerName: string | null;
  notOptedInGrossScore: number | null;
}

export interface GrossPar3SkinsSummary {
  totalHolesWithSkins: number;
  totalSkinValueAwarded: number;
  par3HoleCount: number;
  incomingCarryover: number;
  holeResults: GrossPar3Skin[];
  playerSummaries: PlayerSkinSummary[];
}

export interface RoundSkins {
  roundId: number;
  roundDate: string;
  courseName: string;
  flightSkins: FlightSkins[];
  grossPar3Skins: GrossPar3SkinsSummary | null;
}

export interface CourseHole {
  id: number;
  holeNumber: number;
  par: number;
  strokeIndex: number;
}

export interface Course {
  id: number;
  name: string;
  rating: number;
  slope: number;
  holeCount: number;
}

export interface HoleTeeBox {
  courseHoleId: number;
  yardage: number;
  par: number;
}

export interface TeeBox {
  id: number;
  name: string;
  courseRating: number;
  slopeRating: number;
  totalYardage: number;
  par: number;
  holes: HoleTeeBox[];
}

export interface CourseDetail extends Course {
  holeDetails: CourseHole[];
  teeBoxes: TeeBox[];
}

export type InviteStatus = 'Pending' | 'Accepted' | 'Revoked';

export interface Invite {
  id: number;
  email: string;
  token: string;
  status: InviteStatus;
  createdAt: string;
  expiresAt: string;
  acceptedAt: string | null;
  playerId: number | null;
  inviteLink: string;
  role: 'admin' | 'scorer' | 'player';
  leagueSlug: string;
  preLinkedPlayerId: number | null;
  preLinkedPlayerName: string | null;
}

export interface CreateInvitesResult {
  created: Invite[];
  skipped: string[];
  autoLinked: string[];
}

export type MyStatus = 'approved' | 'none';

export interface MyStatusResponse {
  status: MyStatus;
  playerId: number | null;
  roles: ('admin' | 'scorer' | 'player')[];
}

export interface SeasonHalf {
  id: number;
  seasonId: number;
  halfNumber: number;
  name: string;
  startDate: string;
  endDate: string;
  /** True once the half's rounds have started; its roster is then frozen. */
  isLocked: boolean;
}

export interface Season {
  id: number;
  name: string;
  year: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  bestNRounds: number | null;
  halves: SeasonHalf[];
}

// ── Tee times ─────────────────────────────────────────────────────────────────

export interface TeeTimeParticipant {
  participantId: number;
  playerId: number;
  playerName: string;
  flightId: number;
  flightName: string;
  isSubstitute: boolean;
}

export interface TeeTimeSlot {
  id: number;
  teeTimeNumber: number;
  scheduledTime: string; // "15:28"
  autoFilled: boolean;
  players: TeeTimeParticipant[];
}

export interface RoundTeeTimeSchedule {
  roundId: number;
  cutoffUtc: string; // ISO-8601
  isLocked: boolean;
  participantCount: number;
  currentUserParticipantId: number | null;
  currentUserTeeTimeId: number | null;
  currentUserSkippedWeek: boolean;
  slots: TeeTimeSlot[];
  currentUserPreferredSlots: TeeTimeSlotPreference;
  weekNumber: number;
  roundDate: string; // "yyyy-MM-dd"
  courseName: string;
  isRoundDay: boolean;
  skippedCount: number;
  substituteCount: number;
  substitutesEnabled: boolean;
  /** Caller is in the sub pool but not in this round — can self-join a slot. */
  currentUserIsSubstitutePoolMember: boolean;
  /** Caller's row in this round is a substitute row — "leave" exits the round. */
  currentUserIsSubstitute: boolean;
}

// ── Tee Time Score Entry ───────────────────────────────────────────────────────

export interface MyTodaysTeeTime {
  roundId: number;
  roundDate: string; // ISO-8601 date
  courseName: string;
  courseId: number;
  nineHoleSide: NineHoleSide;
  roundStatus: RoundStatus;
  teeTimeId: number;
  scheduledTime: string; // "HH:mm"
  scheduledTimeFormatted: string; // "15:28"
  teeTimeNumber: number;
  canEnterScores: boolean;
}

export interface TeeTimeHoleInfo {
  holeNumber: number;
  par: number;
  strokeIndex: number;
}

export interface TeeTimePlayerHoleScore {
  holeNumber: number;
  par: number;
  strokeIndex: number;
  grossStrokes: number | null;
  netStrokes: number | null;
  grossStablefordPoints: number | null;
  netStablefordPoints: number | null;
  putts: number | null;
  firstPuttDistanceFeet: number | null;
  fairwayHit: boolean | null;
  gir: boolean | null;
  lastModifiedByPlayerId: number | null;
  lastModifiedByPlayerName: string | null;
}

export interface TeeTimePlayerScore {
  participantId: number;
  playerId: number;
  playerName: string;
  playerInitials: string;
  flightId: number;
  flightName: string;
  handicapIndex: number;
  courseHandicap: number;
  isWithdrawn: boolean;
  skippedWeek: boolean;
  holeScores: TeeTimePlayerHoleScore[];
  totalGrossStrokes: number | null;
  totalNetStrokes: number | null;
  totalGrossStablefordPoints: number | null;
  totalNetStablefordPoints: number | null;
}

export interface TeeTimeGroupScorecard {
  roundId: number;
  roundDate: string; // ISO-8601 date
  courseName: string;
  courseId: number;
  nineHoleSide: NineHoleSide;
  roundStatus: RoundStatus;
  teeTimeId: number;
  scheduledTimeFormatted: string; // "HH:mm"
  teeTimeNumber: number;
  holes: TeeTimeHoleInfo[];
  players: TeeTimePlayerScore[];
}

export interface TeeTimeGroupScoresResult {
  teeTimeId: number;
  roundId: number;
  playersSubmitted: number;
  totalHolesSubmitted: number;
  message: string;
}

export interface PlayerHoleScoreInput {
  holeNumber: number;
  grossStrokes: number;
  putts?: number | null;
  firstPuttDistanceFeet?: number | null;
  fairwayHit?: boolean | null;
}

export interface PlayerScoreInput {
  playerId: number;
  holeScores: PlayerHoleScoreInput[];
}

export interface HoleScoreConflict {
  playerId: number;
  playerName: string;
  holeNumber: number;
  existingGrossStrokes: number;
  existingEnteredByName: string | null;
  newGrossStrokes: number;
}

export interface ConfirmedOverwrite {
  playerId: number;
  holeNumber: number;
}

// ── Scorecard OCR ────────────────────────────────────────────────────────────

export interface ScorecardOcrHole {
  holeNumber: number;
  grossStrokes: number | null;
  confidence: number; // 0–1; low values should be flagged for the user to double-check
}

export interface ScorecardOcrPlayer {
  rawOcrName: string;
  playerId: number | null; // null when no roster match was found with confidence
  playerName: string | null;
  holes: ScorecardOcrHole[];
}

export interface ScorecardOcrResult {
  players: ScorecardOcrPlayer[];
}

// ── Statistics ───────────────────────────────────────────────────────────────

export interface HoleStatistics {
  holeNumber: number;
  par: number;
  strokeIndex: number;
  averageGrossStrokes: number;
  averageNetStrokes: number;
  averageGrossStablefordPoints: number;
  averageNetStablefordPoints: number;
  averageScoreToPar: number;
  totalScoresRecorded: number;
  eagleOrBetterCount: number;
  birdieCount: number;
  parCount: number;
  bogeyCount: number;
  doubleBogeyOrWorseCount: number;
  difficultyRank: number;
  bestGrossAveragePlayerName: string | null;
  bestGrossAverage: number | null;
  bestNetAveragePlayerName: string | null;
  bestNetAverage: number | null;
}

export interface CourseStatistics {
  courseId: number;
  courseName: string;
  courseRating: number;
  slopeRating: number;
  totalRoundsPlayed: number;
  totalScorecardsRecorded: number;
  averageTotalGrossStrokes: number | null;
  averageTotalNetStrokes: number | null;
  averageTotalGrossStablefordPoints: number | null;
  averageTotalNetStablefordPoints: number | null;
  averageScoreToPar: number | null;
  holeStatistics: HoleStatistics[];
}

export interface ScoringDistribution {
  eagleOrBetterCount: number;
  birdieCount: number;
  parCount: number;
  bogeyCount: number;
  doubleBogeyOrWorseCount: number;
  totalHolesPlayed: number;
}

export interface BestWorstRound {
  roundId: number;
  roundDate: string;
  courseName: string;
  grossStrokes: number | null;
  netStrokes: number | null;
  grossStablefordPoints: number | null;
  netStablefordPoints: number | null;
}

export interface PlayerHoleAverage {
  holeNumber: number;
  par: number;
  averageGrossStrokes: number;
  averageNetStrokes: number;
  averageScoreToPar: number;
  timesPlayed: number;
}

export interface StrokesGainedPutting {
  totalStrokesGained: number;
  perHoleAverage: number;
  holesWithPuttData: number;
  averagePuttsPerHole: number | null;
  flightAveragePuttsPerHole: number | null;
}

export interface PlayerStatistics {
  playerId: number;
  playerName: string;
  totalRoundsPlayed: number;
  totalRoundsFinalized: number;
  averageGrossStrokes: number | null;
  averageNetStrokes: number | null;
  averageGrossStablefordPoints: number | null;
  averageNetStablefordPoints: number | null;
  averageScoreToPar: number | null;
  bestGrossStrokes: number | null;
  worstGrossStrokes: number | null;
  bestNetStablefordPoints: number | null;
  worstNetStablefordPoints: number | null;
  bestGrossRound: BestWorstRound | null;
  bestNetPointsRound: BestWorstRound | null;
  scoringDistribution: ScoringDistribution;
  holeAverages: PlayerHoleAverage[];
  handicapTrend: number | null;
  totalBirdiesOrBetter: number;
  totalPars: number;
  parOrBetterPercentage: number | null;
  strokesGainedPutting: StrokesGainedPutting | null;
}

// ── Joes vs Others ───────────────────────────────────────────────────────────

export interface GroupStatistics {
  groupName: string;
  playerCount: number;
  totalRoundsPlayed: number;
  averageGrossStrokes: number | null;
  averageNetStrokes: number | null;
  averageGrossStablefordPoints: number | null;
  averageNetStablefordPoints: number | null;
  averageScoreToPar: number | null;
  eagleOrBetterCount: number;
  birdieCount: number;
  parCount: number;
  bogeyCount: number;
  doubleBogeyOrWorseCount: number;
  parOrBetterPercentage: number | null;
}

export interface JoesVsOthersStatistics {
  joes: GroupStatistics;
  others: GroupStatistics;
}

// ── Most Improved ───────────────────────────────────────────────────────────

export interface MostImprovedPlayer {
  playerId: number;
  playerName: string;
  seasonHalfName: string;
  startingHandicapIndex: number;
  currentHandicapIndex: number;
  improvementFactor: number;
  handicapReduction: number;
  roundsPlayedInHalf: number;
}

export interface MostImprovedResult {
  winner: MostImprovedPlayer | null;
  leaderboard: MostImprovedPlayer[];
  seasonHalfName: string;
  minRoundsRequired: number;
}

// ── League Leaderboards ──────────────────────────────────────────────────────

export interface PlayerGrossLeaderboardEntry {
  playerId: number;
  playerName: string;
  averageGrossScore: number;
  roundsPlayed: number;
}

export interface PlayerNetLeaderboardEntry {
  playerId: number;
  playerName: string;
  averageNetScore: number;
  roundsPlayed: number;
}

export interface PlayerBirdiesEagles {
  playerId: number;
  playerName: string;
  totalBirdies: number;
  totalEaglesOrBetter: number;
  total: number;
}

export interface PlayerPar3Skins {
  playerId: number;
  playerName: string;
  totalSkinsWon: number;
  totalSkinValue: number;
}

export interface PlayerCtpWins {
  playerId: number;
  playerName: string;
  totalCtpWins: number;
}

export interface LeagueLeaderboards {
  lowGross: PlayerGrossLeaderboardEntry[];
  lowNet: PlayerNetLeaderboardEntry[];
  birdiesEagles: PlayerBirdiesEagles[];
  par3Skins: PlayerPar3Skins[];
  ctpWins: PlayerCtpWins[];
}

// ── Closest to the Pin ────────────────────────────────────────────────────────

export interface ClosestToPinHole {
  holeNumber: number;
  par: number;
  playerId: number | null;
  playerName: string | null;
}

export interface ClosestToPinParticipant {
  playerId: number;
  playerName: string;
}

export interface RoundClosestToPin {
  roundId: number;
  par3Holes: ClosestToPinHole[];
  participants: ClosestToPinParticipant[];
}

// ── Tournament Rounds ─────────────────────────────────────────────────────────

export interface TournamentMatchupDto {
  matchupNumber: number;
  player1Id: number;
  player1Name: string;
  player1HandicapIndex: number;
  player1CourseHandicap: number;
  player2Id: number;
  player2Name: string;
  player2HandicapIndex: number;
  player2CourseHandicap: number;
  winnerPlayerId: number | null;
}

export interface TournamentRoundDto {
  round: Round;
  matchups: TournamentMatchupDto[];
}

export interface TournamentSkinHole {
  holeNumber: number;
  par: number;
  skinValue: number;
  winnerPlayerId: number | null;
  winnerPlayerName: string | null;
  winningScore: number | null;
  wasCarryover: boolean;
  isTie: boolean;
}

export interface TournamentPlayerSkin {
  playerId: number;
  playerName: string;
  totalSkinsWon: number;
  totalSkinValue: number;
  holesWon: TournamentSkinHole[];
}

export interface TournamentSkinsResult {
  skinType: string;
  holeResults: TournamentSkinHole[];
  playerSummaries: TournamentPlayerSkin[];
}

export interface TournamentHoleExtra {
  holeNumber: number;
  closestToPinPlayerId: number | null;
  closestToPinPlayerName: string | null;
  longestDrivePlayerId: number | null;
  longestDrivePlayerName: string | null;
}

export interface TournamentMatchupResult {
  matchupNumber: number;
  player1Id: number;
  player1Name: string;
  player1HandicapIndex: number;
  player1CourseHandicap: number;
  player1NetStrokes: number | null;
  player1NetPoints: number | null;
  player2Id: number;
  player2Name: string;
  player2HandicapIndex: number;
  player2CourseHandicap: number;
  player2NetStrokes: number | null;
  player2NetPoints: number | null;
  winnerPlayerId: number | null;
  winnerPlayerName: string | null;
  isHalved: boolean;
}

export interface TournamentRankingEntry {
  rank: number;
  playerId: number;
  playerName: string;
  handicapIndex: number;
  courseHandicap: number;
  score: number | null;
  isTied: boolean;
}

export interface LongestDriveWinner {
  playerId: number;
  playerName: string;
}

export interface TournamentResults {
  roundId: number;
  roundDate: string;
  courseName: string;
  courseId: number;
  grossSkins: TournamentSkinsResult;
  netSkins: TournamentSkinsResult;
  holeExtras: TournamentHoleExtra[];
  longestDriveWinners: LongestDriveWinner[];
  matchupResults: TournamentMatchupResult[];
  grossStrokeRanking: TournamentRankingEntry[];
  netStrokeRanking: TournamentRankingEntry[];
  grossStablefordRanking: TournamentRankingEntry[];
  netStablefordRanking: TournamentRankingEntry[];
}


// ── League Settings ───────────────────────────────────────────────────────────

export interface LeagueSetting {
  key: string;
  value: string;
}

export const SETTING_KEYS = {
  teeTimeEmailEnabled: 'tee_time_email_enabled',
  standingsDropCount: 'standings_drop_count',
  teeTimeCutoffTime: 'tee_time_cutoff_time',
  signUpReminderEmailEnabled: 'sign_up_reminder_email_enabled',
  substitutesEnabled: 'substitutes_enabled',
  roundCost: 'round_cost',
  whatsAppGroupLink: 'whatsapp_group_link',
} as const;

/** Settings safe to expose to anonymous site visitors. See GET /v1/settings/public. */
export interface PublicLeagueSettings {
  whatsAppGroupLink: string;
}

// ── Feature Flags ──────────────────────────────────────────────────────────────
// Global, application-wide toggles (not per-league) used to roll out or
// kill-switch new features. Distinct from LeagueSettings above.

export interface FeatureFlag {
  key: string;
  enabled: boolean;
}

export const FEATURE_FLAG_KEYS = {
  selfSkipRoundsEnabled: 'self_skip_rounds_enabled',
  closestToPinEnabled: 'closest_to_pin_enabled',
  resendTeeTimeEmailEnabled: 'resend_tee_time_email_enabled',
  activeRoundLeaderboardEnabled: 'active_round_leaderboard_enabled',
  roundDayTeeTimeSwitchEnabled: 'round_day_tee_time_switch_enabled',
  scorecardOcrEnabled: 'scorecard_ocr_enabled',
  joesVsOthersEnabled: 'joes_vs_others_enabled',
} as const;
