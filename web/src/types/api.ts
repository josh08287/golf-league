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
}

/** Lightweight row used by the unlinked-players pickers. */
export interface UnlinkedPlayer {
  id: number;
  fullName: string;
  email: string | null;
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
}

export type RoundStatus = 'Scheduled' | 'InProgress' | 'PendingFinalization' | 'Finalized' | 'Cancelled';
export type NineHoleSide = 'Front' | 'Back';

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
  nineHoleScoreDifferential: number | null;
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

export interface CourseDetail extends Course {
  holeDetails: CourseHole[];
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
}

export interface CreateInvitesResult {
  created: Invite[];
  skipped: string[];
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
  slots: TeeTimeSlot[];
  currentUserPreferredSlots: TeeTimeSlotPreference;
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
  bestGrossScore: number;
  roundDate: string;
  courseName: string;
}

export interface PlayerNetLeaderboardEntry {
  playerId: number;
  playerName: string;
  bestNetScore: number;
  roundDate: string;
  courseName: string;
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

export interface LeagueLeaderboards {
  lowGross: PlayerGrossLeaderboardEntry[];
  lowNet: PlayerNetLeaderboardEntry[];
  birdiesEagles: PlayerBirdiesEagles[];
  par3Skins: PlayerPar3Skins[];
}
