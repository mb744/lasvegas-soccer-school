// Mirrors the backend's Dtos/MobileDtos.cs. Keep in sync when the API changes.

export enum AttendanceStatus {
  Pending = 0,
  Confirmed = 1,
  Declined = 2,
  Maybe = 3,
}

export enum ScheduledEventKind {
  Game = 0,
  Practice = 1,
  Miscellaneous = 2,
}

export enum Language {
  English = 0,
  Spanish = 1,
}

export enum DevicePlatform {
  Unknown = 0,
  Ios = 1,
  Android = 2,
}

export interface PlayerTeam {
  teamId: number;
  teamName: string;
}

export interface Player {
  id: number;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  teams: PlayerTeam[];
}

export interface Me {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string | null;
  language: Language;
  isAdmin: boolean;
  players: Player[];
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: Me;
}

export interface EventPlayer {
  playerId: number;
  firstName: string;
  lastName: string;
  status: AttendanceStatus;
}

export interface ScheduleEvent {
  id: number;
  teamId: number;
  teamName: string;
  kind: ScheduledEventKind;
  startsAt: string;
  endsAt?: string | null;
  arriveAt?: string | null;
  summary?: string | null;
  location?: string | null;
  venueName?: string | null;
  opponentName?: string | null;
  isHome?: boolean | null;
  isCancelled: boolean;
  uniformName?: string | null;
  shoeType: number;
  players: EventPlayer[];
}

export interface ChatGroup {
  id: number;
  title: string;
  lastMessagePreview?: string | null;
  lastMessageSender?: string | null;
  lastMessageAt?: string | null;
  unreadCount: number;
}

export interface ChatMessage {
  id: number;
  groupId: number;
  senderUserId: string;
  senderName: string;
  isFromAdmin: boolean;
  body: string;
  sentAt: string;
}
