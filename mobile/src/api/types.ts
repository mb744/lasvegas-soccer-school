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
  /** False when this login is only a view-only family member for the child (no attendance
   *  changes, no training login). Missing from older servers: treat as true. */
  canManage?: boolean;
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
  /** True when the login email appears on a TeamCoach card. Same detection rule the web uses. */
  isCoach: boolean;
  /** Team IDs the login is coach of. Empty when isCoach is false. */
  coachTeamIds: number[];
  players: Player[];
  /** False until the login proves it owns its email; email-matched coach/family links wait on it. */
  emailConfirmed: boolean;
  /** Effective permission keys (backend Auth/Permissions.cs), e.g. 'drills.create'. For showing and
   *  hiding UI only — the server enforces every permission itself. Optional so an older server
   *  response doesn't break the app. */
  permissions?: string[];
  /** Role on the family the app shows. 'viewer' = read-only family member (grandparent, friend):
   *  no chat, invoices or attendance changes. Null/missing when not part of a family. */
  familyRole?: FamilyRole | null;
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
  media?: MediaItem | null;
}

export enum MediaKind {
  Image = 0,
  Video = 1,
}

/** `url` is a short-lived read link — re-fetch the parent resource rather than caching it. */
export interface MediaItem {
  mediaId: number;
  kind: MediaKind;
  contentType: string;
  url: string;
}

export interface EventMediaItem {
  id: number;
  eventId: number;
  media: MediaItem;
  uploadedByUserId: string;
  uploaderName: string;
  caption: string | null;
  createdAt: string;
  canDelete: boolean;
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
  // Optional: older servers don't send these. The editor needs them so a save doesn't wipe them.
  endsAt?: string | null;
  summary?: string | null;
  notes?: string | null;
  venueId?: number | null;
  uniformId?: number | null;
  isHome?: boolean | null;
  shoeType?: number;
  tournamentId?: number | null;
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
  /** True when the member's login email matches a TeamCoach row. Lets the UI tag coaches distinctly from admins/parents. */
  isCoach: boolean;
  addedAt: string;
}

/** Team-wide attendance counts on one event, visible only to admins and coaches of that team. */
export interface StaffEventAttendance {
  going: number;
  maybe: number;
  notGoing: number;
  pending: number;
  rosterSize: number;
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

/** One team the user has a coach card on (email match). Used to hydrate the coach-teams picker. */
export interface UserCoachTeam {
  teamCoachId: number;
  teamId: number;
  teamName: string;
}

/** Row from /api/admin/users — one Identity user with derived staff flags and profile. */
export interface AdminUserRow {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  isAdmin: boolean;
  /** Derived: this login's email appears on at least one TeamCoach card. */
  isCoach: boolean;
  isBanned: boolean;
  createdAt: string | null;
  lastLoginAt: string | null;
  registrationCount: number;
  parentAccountId: number | null;
}

// ---- Access control (api/admin/access) ----

export interface PermissionInfo {
  key: string;
  area: string;
  nameEn: string;
  nameEs: string;
  /** Can be given to one person on top of their role (e.g. Drill creator). */
  grantable: boolean;
}

export interface AccessCatalog {
  permissions: PermissionInfo[];
  /** Keys the Coach / Parent role has. Admin always has every key. */
  coach: string[];
  parent: string[];
}

export type EditableRole = 'coach' | 'parent';

export interface UserAccess {
  userId: string;
  email: string;
  isAdmin: boolean;
  isCoach: boolean;
  coachTeamIds: number[];
  roles: string[];
  grants: string[];
  effective: string[];
}

export type PermissionAuditAction =
  | 'RoleGranted' | 'RoleRevoked' | 'UserGranted' | 'UserRevoked' | 'AdminRoleGranted' | 'AdminRoleRevoked';

export interface PermissionAuditEntry {
  id: number;
  action: PermissionAuditAction;
  role: string | null;
  targetUserId: string | null;
  targetUserEmail: string | null;
  permission: string | null;
  actorEmail: string | null;
  at: string;
}

// ---- Family members & invites (api/mobile/family) ----

export type FamilyRole = 'owner' | 'guardian' | 'viewer';

/** Matches the backend FamilyAccessLevel enum. */
export enum FamilyAccessLevel {
  Guardian = 0,
  Viewer = 1,
}

export interface FamilyMember {
  role: FamilyRole;
  name: string;
  email: string | null;
  status: 'joined' | 'invited';
  /** Set for invited/listed people (resend, change access, remove). */
  contactId: number | null;
  /** Set for people an admin linked without an invite (remove only). */
  collaboratorId: number | null;
  inviteSentAt: string | null;
  isYou: boolean;
}

export interface Family {
  familyId: number;
  yourRole: FamilyRole;
  canManage: boolean;
  members: FamilyMember[];
}

export interface FamilyInviteRequest {
  firstName: string;
  lastName: string;
  email: string;
  accessLevel: FamilyAccessLevel;
  language?: Language;
}
