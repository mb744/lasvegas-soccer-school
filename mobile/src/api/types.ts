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
  venueAddress?: string | null;
  opponentName?: string | null;
  isHome?: boolean | null;
  isCancelled: boolean;
  uniformName?: string | null;
  notes?: string | null;
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

export interface BlockedUser {
  userId: string;
  blockedAt: string;
}

export enum InvoiceStatus {
  New = 0,
  Sent = 1,
  Paid = 2,
  Closed = 3,
}

export interface InvoiceSummary {
  id: number;
  description: string;
  amount: number;
  currency: string;
  dueDate: string | null;
  status: InvoiceStatus;
  issuedAt: string;
  paidAt: string | null;
  playerName: string | null;
  chargeTypeName: string | null;
}

export interface InvoiceDetail extends InvoiceSummary {
  sentAt: string | null;
  paymentMethod: string | null;
  paymentReference: string | null;
}

export interface Announcement {
  id: number;
  title: string;
  body: string;
  teamId: number | null;
  teamName: string | null;
  createdAt: string;
}

/** Admin-facing announcement — includes edit fields the parent-facing DTO omits. */
export interface AdminAnnouncement {
  id: number;
  title: string;
  body: string;
  teamId: number | null;
  teamName: string | null;
  endsAt: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SaveAnnouncementRequest {
  title: string;
  body: string;
  teamId: number | null;
  endsAt: string | null;
  isActive: boolean;
}

export interface TeamOption {
  id: number;
  name: string;
}
