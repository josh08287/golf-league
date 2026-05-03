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

export interface Player {
  id: number;
  fullName: string;
  email: string;
  isActive: boolean;
  currentHandicap: number | null;
  flightId: number | null;
  flightName: string | null;
}

export interface HandicapHistoryEntry {
  id: number;
  playerId: number;
  handicapIndex: number;
  effectiveDate: string;
  source: 'Manual' | 'Calculated' | 'Initial';
  notes: string | null;
}

export interface Flight {
  id: number;
  name: string;
  seasonId: number;
  minHandicap: number | null;
  maxHandicap: number | null;
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
export type RoundType = 'NineHole' | 'EighteenHole';
export type NineHoleSide = 'NotApplicable' | 'Front' | 'Back';

export interface Round {
  id: number;
  courseId: number;
  courseName: string;
  flightId: number;
  flightName: string;
  scheduledDate: string;
  status: RoundStatus;
  roundType: RoundType;
  nineHoleSide: NineHoleSide;
  seasonId: number;
  participantCount: number;
}

export interface Participant {
  id: number;
  roundId: number;
  playerId: number;
  playerName: string;
  handicapAtTime: number;
  courseHandicap: number;
  isWithdrawn: boolean;
}

export interface HoleScore {
  holeNumber: number;
  par: number;
  strokes: number;
  netStrokes: number;
  strokeIndex: number;
}

export interface Scorecard {
  roundId: number;
  playerId: number;
  playerName: string;
  courseName: string;
  scheduledDate: string;
  handicapAtTime: number;
  grossScore: number | null;
  netScore: number | null;
  points: number | null;
  holes: HoleScore[];
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

export interface Season {
  id: number;
  name: string;
  year: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  bestNRounds: number | null;
}
