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

/** A child's login for the separate Daily Training app, managed by the parent. */
export interface TrainingLogin {
  hasLogin: boolean;
  username: string | null;
  lastLoginAt: string | null;
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

export interface AdminTeamDetail {
  id: number;
  name: string;
  players: AdminTeamPlayer[];
  coaches: AdminTeamCoach[];
}

export interface AdminTeamPlayer {
  id: number;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  parentName: string | null;
  parentPhone: string | null;
}

export interface AdminTeamCoach {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  role: string;
}

export interface AdminEvent {
  id: number;
  teamId: number;
  teamName: string;
  kind: ScheduledEventKind;
  startsAt: string;
  arriveAt: string | null;
  opponentName: string | null;
  location: string | null;
  venueName: string | null;
  isCancelled: boolean;
}

export interface AdminChatGroup {
  id: number;
  title: string;
  teamId: number | null;
  teamName: string | null;
  memberCount: number;
  messageCount: number;
  createdAt: string;
  members: AdminChatGroupMember[];
}

export interface AdminChatGroupMember {
  id: number;
  parentAccountId: number | null;
  displayName: string;
  role: number;
  addedAt: string;
}

export interface AdminPlayerOption {
  id: number;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  parentName: string | null;
}

export interface AdminUniform {
  id: number;
  name: string;
}

export interface AdminVenue {
  id: number;
  name: string;
  address: string | null;
}

/** Player row from GET /api/teams/{id} — richer than the mobile-admin roster row (has jersey #,
 *  goalie flag, etc.) — matches the web admin TeamPlayer DTO. */
export interface AdminTeamPlayerRow {
  playerId: number;
  playerFirstName: string;
  playerLastName: string;
  jerseyNumber: string | null;
}

export interface SaveTeamRequest {
  name: string;
  messageGroupId?: number | null;
}

export interface SaveTeamCoachRequest {
  name: string;
  email: string | null;
  phone: string | null;
  role: number;
  language?: number;
  hasWhatsApp?: boolean;
  coachId?: number | null;
}

export interface SavePracticeRequest {
  startsAt: string;
  endsAt: string | null;
  location: string | null;
  summary: string | null;
  notes: string | null;
  venueId: number | null;
  shoeType?: number;
}

export interface SaveGameRequest {
  startsAt: string;
  endsAt: string | null;
  arriveAt: string | null;
  opponentName: string | null;
  isHome: boolean | null;
  location: string | null;
  summary: string | null;
  notes: string | null;
  uniformId: number | null;
  venueId: number | null;
  shoeType?: number;
}

export interface SaveChatGroupRequest {
  title: string;
  seedFromTeamId: number | null;
}
