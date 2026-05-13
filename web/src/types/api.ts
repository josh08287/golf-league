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
