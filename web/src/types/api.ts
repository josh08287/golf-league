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
  id: string;
  name: string;
  email: string;
  currentHandicap: number;
  flightId: string | null;
  flightName: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface Handicap {
  id: string;
  playerId: string;
  handicapIndex: number;
  effectiveDate: string;
  calculatedAt: string;
}

export interface Flight {
  id: string;
  name: string;
  seasonId: string;
  minHandicap: number;
  maxHandicap: number;
  playerCount: number;
  createdAt: string;
}

export interface Standing {
  position: number;
  playerId: string;
  playerName: string;
  flightId: string;
  flightName: string;
  currentHandicap: number;
  roundsPlayed: number;
  totalPoints: number;
  averagePoints: number;
}

export type RoundStatus = 'Scheduled' | 'InProgress' | 'Finalized';

export interface Round {
  id: string;
  courseId: string;
  courseName: string;
  flightId: string;
  flightName: string;
  scheduledDate: string;
  status: RoundStatus;
  seasonId: string;
  participantCount: number;
  createdAt: string;
}

export interface Participant {
  id: string;
  roundId: string;
  playerId: string;
  playerName: string;
  handicapAtTime: number;
  grossScore: number | null;
  netScore: number | null;
  points: number | null;
  didNotFinish: boolean;
}

export interface HoleScore {
  holeNumber: number;
  par: number;
  strokes: number;
  netStrokes: number;
  strokeIndex: number;
}

export interface Scorecard {
  roundId: string;
  playerId: string;
  playerName: string;
  courseName: string;
  scheduledDate: string;
  handicapAtTime: number;
  grossScore: number | null;
  netScore: number | null;
  points: number | null;
  holes: HoleScore[];
}

export interface Course {
  id: string;
  name: string;
  par: number;
  holes: number;
  rating: number;
  slope: number;
  address: string | null;
  createdAt: string;
}

export interface Season {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}
