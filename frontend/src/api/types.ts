export type Language = 0 | 1 // 0 = English, 1 = Spanish
export type OutreachStatus = 0 | 1 | 2 | 3 | 4 // Pending Sent AccountCreated Registered Failed

export interface Me {
  userId: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  language: Language
  isAdmin: boolean
}

export interface SignupRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone?: string
  language: Language
}

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

// --- Players (parent's durable roster) ---

export interface PlayerSummary {
  id: number
  firstName: string
  lastName: string
  dateOfBirth: string
}

export interface SavePlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
}

// --- Additional parent/guardian contacts ---

export interface ParentContact {
  id: number
  firstName: string
  lastName: string
  email: string | null
  cellPhone: string | null
  hasWhatsApp: boolean
  language: Language
}

export interface ParentContactInput {
  firstName: string
  lastName: string
  email?: string | null
  cellPhone?: string | null
  hasWhatsApp: boolean
  language?: Language | null
}

// --- Registration ---

export interface RegistrationPlayerInput {
  playerId?: number | null
  firstName?: string
  lastName?: string
  dateOfBirth?: string
  schoolGrade: string
  uniformSize: string
  shoeSize: string
  heardFrom?: string
  waiverParticipantName?: string
  waiverTeamName?: string
  waiverParentGuardianName?: string
  waiverPhone?: string
  waiverEmail?: string
  signatureDataUrl: string
}

export interface SubmitRegistrationRequest {
  parentFirstName: string
  parentLastName: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  postalCode?: string
  cellPhone: string
  email: string
  language: Language
  hasWhatsApp: boolean
  waiverConsent: boolean
  players: RegistrationPlayerInput[]
  additionalParents?: ParentContactInput[]
}

export interface UpdateRegistrationRequest {
  parentFirstName: string
  parentLastName: string
  addressLine1?: string | null
  addressLine2?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  cellPhone: string
  email: string
  language: Language
  hasWhatsApp: boolean
  additionalParents?: ParentContactInput[]
}

export interface RegistrationSummary {
  id: number
  season: string
  parentFirstName: string
  parentLastName: string
  email: string
  cellPhone: string
  language: Language
  hasWhatsApp: boolean
  playerCount: number
  createdAt: string
}

export interface RegistrationPlayerDetail {
  id: number          // RegistrationPlayer.Id (used in the waiver PDF route)
  playerId: number    // underlying durable Player.Id
  firstName: string
  lastName: string
  dateOfBirth: string
  schoolGrade: string
  uniformSize: string
  shoeSize: string
  heardFrom: string | null
  waiverParticipantName: string | null
  waiverTeamName: string | null
  waiverParentGuardianName: string | null
  waiverPhone: string | null
  waiverEmail: string | null
  hasSignature: boolean
  signedAt: string | null
  freeTrialOver: boolean
  ageClassificationId: number | null
  ageClassificationName: string | null
}

export interface AddRegistrationPlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string  // YYYY-MM-DD
  schoolGrade: string
  uniformSize: string
  shoeSize: string
  heardFrom?: string | null
}

export interface UpdateRegistrationPlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
  schoolGrade: string
  uniformSize: string
  shoeSize: string
}

/** Admin creates an empty registration shell for an existing parent. The parent then
 *  logs in to /account, consents, and signs each player's waiver. */
export interface AdminCreateRegistrationRequest {
  parentAccountId: number
  /** Defaults to the active season when null/blank. */
  season?: string | null
}

/** Parent signs a specific player's waiver. The signature is a data:image/png URL. */
export interface SignPlayerWaiverRequest {
  signatureDataUrl: string
}

/** Admin links a second login to an existing family. If the user has their own ParentAccount
 *  with players, the backend merges them into the primary family. */
export interface LinkUserToRegistrationRequest {
  userId: string
}

/** One login (owner or collaborator) with access to a family — surfaced in the admin
 *  "Linked logins" section on the registration detail panel. */
export interface LinkedLogin {
  userId: string
  email: string
  firstName: string | null
  lastName: string | null
  isOwner: boolean
  linkedAt: string | null
}

// --- Age classifications (admin-managed DOB buckets) ---

export interface AgeClassification {
  id: number
  name: string
  description: string | null
  dobStart: string   // YYYY-MM-DD
  dobEnd: string     // YYYY-MM-DD
  createdAt: string
  updatedAt: string
}

// --- Monthly fee one-click broadcast ---

export interface MonthlyFeePreview {
  recipientCount: number
  englishCount: number
  spanishCount: number
  englishTemplateConfigured: boolean
  spanishTemplateConfigured: boolean
  variables: WhatsAppTemplateVariable[]
  suggestedValues: Record<string, string>
  englishTemplateName: string | null
  spanishTemplateName: string | null
  englishPreviewText: string | null
  spanishPreviewText: string | null
}

export interface SendMonthlyFeeRequest {
  templateVariables?: Record<string, string> | null
}

export interface SaveAgeClassificationRequest {
  name: string
  description?: string | null
  dobStart: string
  dobEnd: string
}

export interface RegistrationDetail {
  id: number
  season: string
  parentFirstName: string
  parentLastName: string
  addressLine1: string
  addressLine2: string | null
  city: string
  state: string
  postalCode: string
  cellPhone: string
  email: string
  language: Language
  hasWhatsApp: boolean
  waiverConsent: boolean
  waiverSignedAt: string | null
  createdAt: string
  players: RegistrationPlayerDetail[]
  additionalParents: ParentContact[]
  /** Owner + any admin-linked collaborator logins for this family. Always has at least the
   *  owner row. */
  linkedLogins: LinkedLogin[]
  /** Family-wide opt-out: when true, every bulk recipient path (broadcasts, events,
   *  tournament confirmations, monthly fee, etc.) skips this family. Toggle from the
   *  registration detail panel. */
  noCommunications: boolean
}

// --- Outreach (admin tracking) ---

export interface CreateOutreachRequest {
  email?: string
  phone?: string
  language: Language
}

export interface OutreachResponse {
  id: number
  email: string | null
  phone: string | null
  language: Language
  status: OutreachStatus
  statusMessage: string | null
  link: string
  createdAt: string
  sentAt: string | null
  accountCreatedAt: string | null
  registeredAt: string | null
  parentAccountId: number | null
}

// --- Admin: user management ---

export interface UserSummary {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  isAdmin: boolean
  isBanned: boolean
  createdAt: string | null
  lastLoginAt: string | null
  registrationCount: number
  /** Null when the Identity user hasn't created a parent profile yet (e.g. seed admin accounts). */
  parentAccountId: number | null
}

export const OUTREACH_STATUS_LABELS: Record<OutreachStatus, string> = {
  0: 'Pending',
  1: 'Sent',
  2: 'Account created',
  3: 'Registered',
  4: 'Failed',
}

// --- Messaging (chat/broadcast) ---

export type MessageChannel = 0 | 1 | 2 // 0 = SMS, 1 = WhatsApp, 2 = Email
export type MessageDeliveryStatus = 0 | 1 | 2 | 3 | 4 | 5
// Pending Queued Sent Delivered Failed Undelivered

export interface MessagingConfig {
  sms: boolean
  whatsApp: boolean
  email: boolean
  conversations: boolean
}

export interface MessageGroupMember {
  id: number
  name: string | null
  phone: string
  email: string | null
  language: Language
  parentAccountId: number | null
}

export interface MessageGroupSummary {
  id: number
  name: string
  description: string | null
  language: Language
  memberCount: number
  createdAt: string
}

export interface MessageGroupDetail extends MessageGroupSummary {
  members: MessageGroupMember[]
}

export interface DynamicGroup {
  key: string
  label: string
  count: number
}

export interface SaveMessageGroupRequest {
  name: string
  description?: string | null
  /** 0 = English, 1 = Spanish. Drives which body recipients of this group receive. */
  language: Language
}

export interface AddMessageGroupMemberRequest {
  name?: string | null
  phone: string
  email?: string | null
  /** When omitted, the new member inherits the group's default language. */
  language?: Language | null
  parentAccountId?: number | null
}

export interface ListGroupsResponse {
  curated: MessageGroupSummary[]
  dynamic: DynamicGroup[]
}

export type RecipientTargetKind = 0 | 1 | 2 | 3 // Individual CustomGroup DynamicGroup AdHocList

export interface AdHocRecipient {
  phone: string
  name?: string | null
  email?: string | null
}

export interface BroadcastTarget {
  kind: RecipientTargetKind
  phone?: string | null
  name?: string | null
  customGroupId?: number | null
  dynamicGroupKey?: string | null
  /** Used only when kind = 3 (AdHocList). One-off list pasted at compose time. */
  recipients?: AdHocRecipient[] | null
}

export interface CreateBroadcastRequest {
  channel: MessageChannel
  /** English body. At least one of bodyEn/bodyEs required when not using a template. */
  bodyEn?: string | null
  /** Spanish body. */
  bodyEs?: string | null
  /** Email subject EN (required when channel = Email and not using a template). */
  subjectEn?: string | null
  /** Email subject ES. */
  subjectEs?: string | null
  /** Language to use for recipients without one attached (ad-hoc/individual/dynamic). Default English. */
  defaultLanguage?: Language
  /** Use an approved WhatsApp Content template instead of free-form body. WhatsApp channel only. */
  whatsAppTemplateId?: number | null
  /** Use an admin-managed Email template. Email channel only. */
  emailTemplateId?: number | null
  /** Values for the template's positional variables, keyed by position as string. */
  templateVariables?: Record<string, string> | null
  /** Event this send is about (picked from event picker). Drives the cancellation flow later. */
  scheduledGameId?: number | null
  /** Invoice this send is about. Drives the InvoiceNotification context's property resolver
   *  so the template's invoice.* / chargeType.* / player.* / parent.* variables auto-fill. */
  invoiceId?: number | null
  target: BroadcastTarget
}

export interface SendPerPlayerRequest {
  channel: MessageChannel
  whatsAppTemplateId: number
  defaultLanguage?: Language
  scheduledGameId?: number | null
  templateVariables?: Record<string, string> | null
  target: BroadcastTarget
}

export interface SendPerPlayerResult {
  sent: number
  skipped: number
  total: number
}

export interface PerPlayerPreviewItem {
  playerName: string
  recipients: string[]
  body: string
}

export interface PerPlayerPreviewResult {
  items: PerPlayerPreviewItem[]
  total: number
}

export interface FailedMessage {
  recipientId: number
  broadcastId: number
  channel: MessageChannel
  createdAt: string
  name: string | null
  phone: string
  targetLabel: string | null
  statusMessage: string | null
  errorCode: string | null
}

export interface BroadcastSummary {
  id: number
  channel: MessageChannel
  bodyEn: string | null
  bodyEs: string | null
  subjectEn: string | null
  subjectEs: string | null
  targetLabel: string | null
  createdAt: string
  total: number
  queued: number
  delivered: number
  failed: number
  /** When set, this row represents a fan-out batch (currently: tournament confirmations).
   *  Counts are aggregated across batchSize per-player broadcasts. */
  batchId: string | null
  batchSize: number
  /** Subset of `failed` where the carrier reported WhatsApp error 131049
   *  (per-user marketing template rate limit). The resend flow excludes these from the
   *  Failed bucket within a 24h backoff window. */
  rateLimited: number
}

export interface BroadcastRecipientRow {
  id: number
  name: string | null
  phone: string
  email: string | null
  language: Language
  status: MessageDeliveryStatus
  statusMessage: string | null
  twilioSid: string | null
  /** Name of the template variant actually sent to this recipient; null for free-form sends. */
  templateUsed: string | null
  /** Numeric carrier/WhatsApp error code (e.g. "131049" = rate limit). Null for non-failed
   *  rows. Captured into its own field so the resend flow can skip rate-limited recipients. */
  errorCode: string | null
  /** Display label of the event this message was about; null when not tied to an event. */
  eventName: string | null
}

export interface BroadcastDetail {
  id: number
  channel: MessageChannel
  bodyEn: string | null
  bodyEs: string | null
  subjectEn: string | null
  subjectEs: string | null
  targetLabel: string | null
  createdAt: string
  recipients: BroadcastRecipientRow[]
}

export interface ConversationParticipant {
  phone: string
  name?: string | null
}

export interface CreateGroupConversationRequest {
  title: string
  channel: MessageChannel
  participants: ConversationParticipant[]
  target?: BroadcastTarget | null
}

export interface SendGroupConversationRequest {
  body: string
}

export interface GroupConversationParticipantRow {
  id: number
  name: string | null
  phone: string
  twilioParticipantSid: string | null
}

export interface GroupConversationSummary {
  id: number
  title: string
  channel: MessageChannel
  twilioConversationSid: string
  participantCount: number
  createdAt: string
}

export interface GroupConversationDetail {
  id: number
  title: string
  channel: MessageChannel
  twilioConversationSid: string
  createdAt: string
  participants: GroupConversationParticipantRow[]
}

export type TemplateContext = 0 | 1 | 2 | 3 | 4 | 5 | 6 // FreeForm | TournamentConfirmation | EventReminder | EventCancellation | MonthlyFee | EventDetails | InvoiceNotification
export const TemplateContextValue = {
  FreeForm: 0,
  TournamentConfirmation: 1,
  EventReminder: 2,
  EventCancellation: 3,
  MonthlyFee: 4,
  EventDetails: 5,
  InvoiceNotification: 6,
} as const

export interface WhatsAppTemplateVariable {
  id: number
  position: number
  label: string
  example: string | null
  /** When set, the send pipeline pulls this variable's value from the template's context
   *  registry (see TemplateContext). Null = legacy positional behavior. */
  propertyKey: string | null
}

export interface TemplateProperty {
  key: string
  label: string
}

export interface MappedField {
  id: number
  name: string
  /** Stable property key (starts with "custom.") used by mapped template variables. */
  key: string
  /** Composition with {base.key} placeholders, e.g. "{event.venue}, {event.address}". */
  template: string
  createdAt: string
  updatedAt: string
}

export interface SaveMappedFieldRequest {
  name: string
  template: string
}

export interface TemplateContextOption {
  context: TemplateContext
  label: string
}

export interface TemplatePair {
  id: number
  name: string
  contentSid: string
  language: Language
  previewText: string | null
  variables: WhatsAppTemplateVariable[]
}

export interface WhatsAppTemplate {
  id: number
  name: string
  contentSid: string
  language: Language
  description: string | null
  previewText: string | null
  /** Drives the per-variable property mapping UI and the send-time resolver. */
  context: TemplateContext
  createdAt: string
  variables: WhatsAppTemplateVariable[]
  /** Opposite-language counterpart auto-detected by base name (e.g. `practice_or_game` ↔ `practice_or_game_es`). */
  paired: TemplatePair | null
}

export interface SaveTemplateVariable {
  position: number
  label: string
  example?: string | null
  /** Optional mapping to a property from the template's Context registry. */
  propertyKey?: string | null
}

export interface SaveWhatsAppTemplateRequest {
  name: string
  contentSid: string
  language: Language
  description?: string | null
  previewText?: string | null
  context?: TemplateContext
  variables: SaveTemplateVariable[]
}

// --- Email templates ---

export interface EmailTemplateVariable {
  id: number
  position: number
  label: string
  example: string | null
  /** Optional mapping to a property in the template's context (e.g. "event.dateTime",
   *  "player.fullName"). When set, the bulk-send pipeline fills it automatically and the admin
   *  doesn't have to type a value. */
  propertyKey: string | null
}

export interface EmailTemplatePair {
  id: number
  name: string
  language: Language
  subject: string
  body: string
  context: TemplateContext
  variables: EmailTemplateVariable[]
}

export interface EmailTemplate {
  id: number
  name: string
  language: Language
  description: string | null
  subject: string
  body: string
  /** Which send pipeline this template is for. Drives the property registry surfaced in the
   *  per-variable "Map to" dropdown and the send-time auto-fill. */
  context: TemplateContext
  createdAt: string
  updatedAt: string
  variables: EmailTemplateVariable[]
  /** Opposite-language counterpart auto-detected by base name. */
  paired: EmailTemplatePair | null
}

export interface SaveEmailTemplateRequest {
  name: string
  language: Language
  description?: string | null
  subject: string
  body: string
  context?: TemplateContext
  variables: SaveTemplateVariable[]
}

// --- Schedules (GotSport iCal sync) ---

export interface SaveTeamRequest {
  name: string
  /** Either set these directly, or paste scheduleUrl and the backend parses them out. */
  gotSportEventId?: number | null
  gotSportTeamId?: number | null
  scheduleUrl?: string | null
  messageGroupId?: number | null
}

export interface TeamSummary {
  id: number
  name: string
  gotSportEventId: number
  gotSportTeamId: number
  messageGroupId: number | null
  messageGroupName: string | null
  lastSyncedAt: string | null
  lastSyncMessage: string | null
  upcomingGameCount: number
  createdAt: string
}

export type ScheduledEventKind = 0 | 1 | 2 // Game | Practice | Miscellaneous

export interface ScheduledGame {
  id: number
  teamId: number
  teamName: string
  messageGroupId: number | null
  messageGroupName: string | null
  kind: ScheduledEventKind
  startsAt: string
  endsAt: string | null
  summary: string | null
  location: string | null
  description: string | null
  opponentName: string | null
  /** true = we're home, false = away, null = unknown (practice/training). Drives wear text. */
  isHome: boolean | null
  seriesId: string | null
  isCancelled: boolean
  cancelledAt: string | null
  tournamentId: number | null
  tournamentName: string | null
  /** "Be There" arrival time (ISO, UTC) — when players should show up, typically 15–30 min
   *  before startsAt. null when unset. */
  arriveAt: string | null
  /** Explicit uniform override; null = use the home/away → designation mapping. */
  uniformId: number | null
  /** Structured venue/park; null = none (free-text location only). */
  venueId: number | null
  /** Footwear: 0 = Unspecified, 1 = Cleats, 2 = Turf shoes, 3 = Tennis court shoes. */
  shoeType: ShoeType
}

/** 0 = Unspecified, 1 = Cleats, 2 = Turf shoes, 3 = Tennis court shoes. */
export type ShoeType = 0 | 1 | 2 | 3

/** 0 = None, 1 = Home, 2 = Away, 3 = Practice. At most one uniform per non-None value. */
export type UniformDesignation = 0 | 1 | 2 | 3

/** 0 = Unspecified, 1 = Grass, 2 = Turf, 3 = Hard surface. */
export type SurfaceType = 0 | 1 | 2 | 3

export interface Venue {
  id: number
  name: string
  address: string | null
  surface: SurfaceType
  createdAt: string
  updatedAt: string
}

export interface SaveVenueRequest {
  name: string
  address?: string | null
  surface: SurfaceType
}

export interface Uniform {
  id: number
  name: string
  shirtColor: string | null
  shortsColor: string | null
  sockColor: string | null
  designation: UniformDesignation
  createdAt: string
  updatedAt: string
}

export interface SaveUniformRequest {
  name: string
  shirtColor?: string | null
  shortsColor?: string | null
  sockColor?: string | null
  designation: UniformDesignation
}

export type TournamentKind = 0 | 1 // Tournament | League

export interface TournamentSummary {
  id: number
  name: string
  /** Tournament or League. Shares the same data shape; only labeling differs. */
  kind: TournamentKind
  /** Inclusive first day. YYYY-MM-DD; null when admin hasn't filled it in yet. */
  startDate: string | null
  /** Inclusive last day. */
  endDate: string | null
  /** What LVSS pays to enter — admin tracking only. */
  totalCost: number | null
  /** Cost per family — surfaces as template parameter 3 on the confirmation send. */
  costPerPlayer: number | null
  /** Legacy: dedicated team for single-team tournaments. New flow uses the `teams` list. */
  teamId: number | null
  teamName: string | null
  gotSportEventId: number
  gotSportTeamId: number
  lastSyncedAt: string | null
  lastSyncMessage: string | null
  gameCount: number
  upcomingGameCount: number
  /** Legacy: roster size on the single dedicated team. */
  rosterCount: number
  createdAt: string
  /** All teams in this tournament via the TournamentTeams join. Each carries its own
   *  GotSport sync state. Legacy tournaments are auto-backfilled on deploy. */
  teams: TournamentTeam[]
}

export interface TournamentTeam {
  id: number
  tournamentId: number
  teamId: number
  teamName: string
  gotSportEventId: number
  gotSportTeamId: number
  /** TeamSnap event id (path segment in events.teamsnap.com/events/{eventId}/...). 0 = unset. */
  teamSnapEventId: number
  /** TeamSnap division id (the "bracket" the team plays in). 0 = unset. */
  teamSnapDivisionId: number
  /** TeamSnap per-event participant id — the join key for the team in match-participants. 0 = unset. */
  teamSnapParticipantId: number
  lastSyncedAt: string | null
  lastSyncMessage: string | null
  rosterCount: number
  gameCount: number
  createdAt: string
}

export interface AddTournamentTeamRequest {
  /** Pick from existing Teams. Mutually exclusive with newTeamName. */
  existingTeamId?: number | null
  /** Create a new Team inline with this name. */
  newTeamName?: string | null
  gotSportEventId?: number | null
  gotSportTeamId?: number | null
  teamSnapEventId?: number | null
  teamSnapDivisionId?: number | null
  teamSnapParticipantId?: number | null
  scheduleUrl?: string | null
}

export interface UpdateTournamentTeamRequest {
  gotSportEventId?: number | null
  gotSportTeamId?: number | null
  teamSnapEventId?: number | null
  teamSnapDivisionId?: number | null
  teamSnapParticipantId?: number | null
  scheduleUrl?: string | null
}

export interface SaveTournamentRequest {
  name: string
  kind?: TournamentKind
  startDate?: string | null
  endDate?: string | null
  totalCost?: number | null
  costPerPlayer?: number | null
  teamId?: number | null
  gotSportEventId?: number | null
  gotSportTeamId?: number | null
  scheduleUrl?: string | null
}

export interface CreateTournamentTeamRequest {
  name: string
}

export interface TournamentAttendance {
  playerId: number
  firstName: string
  lastName: string
  parentName: string | null
  parentPhone: string | null
  status: AttendanceStatus
  source: AttendanceSource
  updatedAt: string | null
  /** Per-player paid flag for the tournament/league fee. Drives the
   *  `tournamentfee_*`/`leaguefee_*` reminder fan-out — only Paid=false players get the
   *  reminder. Toggled from the per-team attendance table. */
  paid: boolean
}

export interface TournamentAttendanceList {
  tournamentId: number
  confirmed: number
  declined: number
  maybe: number
  pending: number
  paid: number
  unpaid: number
  items: TournamentAttendance[]
}

export interface SendTournamentConfirmationsResult {
  sent: number
  skipped: number
  total: number
  message: string | null
  /** How many otherwise-matched players were excluded from this resend because their last
   *  failure was WhatsApp 131049 within the 24h backoff window. */
  rateLimitedSkipped: number
}

/** Filter buckets for the per-team "Re-send confirmations" flow. At least one must be true. */
export interface ResendTournamentConfirmationsRequest {
  includeFailed: boolean
  includeUndelivered: boolean
  includeNoResponse: boolean
}

/** Pre-send EN/ES preview for tournament confirmations. The sample uses the first rostered
 *  player's name for variable 2; dates and cost are formatted identically to the actual send. */
export interface TournamentSendPreview {
  samplePlayerName: string
  datesValue: string
  costValue: string
  rosterCount: number
  /** Frontend uses `templateId` + `variables` to render editable inputs and call
   *  `/template-preview` with updated values on change for live re-rendering. */
  templateId: number
  variables: TournamentSendPreviewVariable[]
  englishTemplateName: string
  englishRendered: string | null
  spanishTemplateName: string
  spanishRendered: string | null
}

export interface TournamentSendPreviewVariable {
  position: number
  label: string
  propertyKey: string | null
  value: string
}

export interface EventRecipient {
  phone: string
  name: string | null
  language: Language
}

export interface SavePracticeRequest {
  startsAt: string  // ISO 8601 in UTC
  endsAt?: string | null
  location?: string | null
  summary?: string | null
  venueId?: number | null
  shoeType?: ShoeType
}

export interface SaveGameRequest {
  startsAt: string  // ISO 8601 in UTC
  endsAt?: string | null
  arriveAt?: string | null  // "Be There" time, ISO 8601 in UTC
  opponentName?: string | null
  isHome?: boolean | null
  location?: string | null
  summary?: string | null
  tournamentId?: number | null
  uniformId?: number | null  // explicit uniform override; null = use mapping
  venueId?: number | null    // structured venue/park; null = none
  shoeType?: ShoeType        // 0 = Unspecified … 3 = Tennis court shoes
}

export interface SavePracticeSeriesRequest {
  startDate: string  // YYYY-MM-DD
  endDate: string    // YYYY-MM-DD
  startTime: string  // HH:mm
  endTime?: string | null
  /** 0 = Sunday … 6 = Saturday */
  daysOfWeek: number[]
  location?: string | null
  summary?: string | null
  venueId?: number | null
  shoeType?: ShoeType
}

export interface PracticeSeriesCreated {
  seriesId: string
  count: number
  occurrences: ScheduledGame[]
}

export interface TeamDetail {
  id: number
  name: string
  gotSportEventId: number
  gotSportTeamId: number
  messageGroupId: number | null
  messageGroupName: string | null
  lastSyncedAt: string | null
  lastSyncMessage: string | null
  createdAt: string
  upcomingGames: ScheduledGame[]
  coaches: TeamCoach[]
}

export interface ScheduleSyncResult {
  success: boolean
  added: number
  updated: number
  message: string
}

// --- Event attendance (per rostered player confirmation) ---

export type AttendanceStatus = 0 | 1 | 2 | 3 // Pending | Confirmed | Declined | Maybe
export type AttendanceSource = 0 | 1 // ParentReply | Admin

export interface EventAttendance {
  playerId: number
  firstName: string
  lastName: string
  parentName: string | null
  parentPhone: string | null
  status: AttendanceStatus
  source: AttendanceSource
  updatedAt: string | null
}

export interface EventAttendanceList {
  eventId: number
  confirmed: number
  declined: number
  maybe: number
  pending: number
  items: EventAttendance[]
}

export interface EventAttendanceSummary {
  eventId: number
  confirmed: number
  declined: number
  maybe: number
  pending: number
}

// --- Teams (roster builder) ---

export interface RosterTeamSummary {
  id: number
  name: string
  rosterCount: number
  upcomingGameCount: number
  /** true when GotSport IDs are set (schedule sync available). */
  gotSportLinked: boolean
  messageGroupId: number | null
  messageGroupName: string | null
  createdAt: string
}

export interface RosterMember {
  playerId: number
  firstName: string
  lastName: string
  dateOfBirth: string
  ageBracket: string | null
  parentName: string | null
  parentPhone: string | null
  parentEmail: string | null
  addedAt: string
}

export interface RosterTeamDetail {
  id: number
  name: string
  messageGroupId: number | null
  messageGroupName: string | null
  gotSportLinked: boolean
  gotSportEventId: number
  gotSportTeamId: number
  lastSyncedAt: string | null
  lastSyncMessage: string | null
  /** Legacy single-coach fields, kept for backwards-compatibility. New UI uses `coaches`. */
  coachName: string | null
  coachEmail: string | null
  coachPhone: string | null
  createdAt: string
  roster: RosterMember[]
  upcomingGames: ScheduledGame[]
  coaches: TeamCoach[]
}

export interface SaveCoachRequest {
  coachName?: string | null
  coachEmail?: string | null
  coachPhone?: string | null
}

export type TeamCoachRole = 0 | 1 // 0 = HeadCoach, 1 = AssistantCoach

export interface TeamCoach {
  id: number
  teamId: number
  name: string
  email: string | null
  phone: string | null
  language: Language
  hasWhatsApp: boolean
  /** When set, this team-coach row was picked from the Coaches roster — contact details
   *  came from the linked Coach profile and the UI can offer "View profile". Null when the
   *  admin typed the contact in directly. */
  coachId: number | null
  role: TeamCoachRole
  createdAt: string
}

export interface SaveTeamCoachRequest {
  name: string
  email?: string | null
  phone?: string | null
  language: Language
  hasWhatsApp: boolean
  /** When set, the backend pulls name/email/phone/language/HasWhatsApp from the Coach
   *  profile and ignores the values in this payload — the pick wins. */
  coachId?: number | null
  role: TeamCoachRole
}

export interface AvailablePlayer {
  playerId: number
  firstName: string
  lastName: string
  dateOfBirth: string
  ageBracket: string | null
  parentName: string | null
}

export interface CreateRosterTeamRequest {
  name: string
}

export interface RenameTeamRequest {
  name: string
}

export interface AddRosterMembersRequest {
  playerIds: number[]
}

// --- Phrase translation dictionary ---

export interface PhraseTranslation {
  id: number
  english: string
  spanish: string
  createdAt: string
  updatedAt: string
}

export interface SavePhraseTranslationRequest {
  english: string
  spanish: string
}

export interface TranslateRequest {
  text: string
  from: Language
  to: Language
}

export interface TranslateResponse {
  translated: string
  matchedPhrases: string[]
  fullyTranslated: boolean
}

export type TemplatePreviewSource = 0 | 1 | 2 // ApprovedTemplate | TranslatedValues | Unavailable

export interface TemplatePreviewSide {
  language: Language
  templateName: string
  rendered: string | null
  source: TemplatePreviewSource
  values: Record<string, string> | null
}

export interface TemplatePreviewResponse {
  english: TemplatePreviewSide
  spanish: TemplatePreviewSide
}

export interface TemplatePreviewRequest {
  templateId: number
  values: Record<string, string>
  /** Picked event, so the preview resolves event.* and custom mapped fields server-side. */
  scheduledGameId?: number | null
  tournamentId?: number | null
}

export interface EmailTemplatePreviewSide {
  language: Language
  templateName: string
  subject: string | null
  body: string | null
  source: TemplatePreviewSource
  values: Record<string, string> | null
}

export interface EmailTemplatePreviewResponse {
  english: EmailTemplatePreviewSide
  spanish: EmailTemplatePreviewSide
}

export interface EmailTemplatePreviewRequest {
  templateId: number
  values: Record<string, string>
  scheduledGameId?: number | null
  tournamentId?: number | null
  invoiceId?: number | null
}

// --- Messaging settings (auto-reply text + toggle) ---

export interface MessagingSettings {
  autoReplyEnabled: boolean
  autoReplyTextEn: string
  autoReplyTextEs: string
  zellePhone: string | null
  updatedAt: string
}

export interface SaveMessagingSettingsRequest {
  autoReplyEnabled: boolean
  autoReplyTextEn: string
  autoReplyTextEs: string
  zellePhone?: string | null
}

// --- Threaded view (per-phone inbox) ---

export type ThreadDirection = 0 | 1 // 0 = Inbound, 1 = Outbound

export interface ThreadMessage {
  direction: ThreadDirection
  channel: MessageChannel
  body: string
  at: string
  status: MessageDeliveryStatus | null
  statusMessage: string | null
  broadcastId: number | null
}

/** Admin /admin/players row — durable player + parent contact + current team + current-season
 *  registration status + a glance at uniform assignments. */
export interface AdminPlayerSummary {
  id: number
  firstName: string
  lastName: string
  dateOfBirth: string
  ageBracket: string | null
  parentAccountId: number | null
  parentName: string | null
  parentCellPhone: string | null
  parentEmail: string | null
  currentTeamId: number | null
  currentTeamName: string | null
  waiverSigned: boolean
  registeredThisSeason: boolean
  uniformCount: number
  /** Comma-joined jersey numbers of active (non-returned) uniform assignments. */
  activeJerseyNumbers: string
}

/** One Player row inside a duplicate group. rosterCount + registrationCount hint at which row
 *  is the richer keeper before merging. */
export interface PlayerDuplicateMember {
  id: number
  rosterCount: number
  registrationCount: number
}

/** Group of Player rows that look like the same real kid (same parent, name, DOB). Returned by
 *  GET /admin/players/duplicates and consumed by the AdminPlayers Duplicates panel. */
export interface PlayerDuplicateGroup {
  parentAccountId: number
  parentName: string | null
  firstName: string
  lastName: string
  dateOfBirth: string
  players: PlayerDuplicateMember[]
}

export interface PlayerUniformAssignment {
  id: number
  uniformId: number
  uniformName: string
  uniformDesignation: string | null
  jerseyNumber: string
  assignedAt: string
  returnedAt: string | null
  notes: string | null
  createdAt: string
}

export interface CreatePlayerUniformAssignmentRequest {
  uniformId: number
  jerseyNumber: string
  assignedAt: string
  notes?: string | null
}

export interface UpdatePlayerUniformAssignmentRequest {
  jerseyNumber: string
  assignedAt: string
  returnedAt?: string | null
  notes?: string | null
}

export interface AdminUpdatePlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
}

export interface AdminCreatePlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
  parentAccountId?: number | null
  newParentFirstName?: string | null
  newParentLastName?: string | null
  newParentEmail?: string | null
  newParentCellPhone?: string | null
}

export interface SendRegistrationInviteRequest {
  parentAccountId: number
  additionalNote?: string | null
}

export interface SendRegistrationInviteResult {
  success: boolean
  message: string
}

/** Invoice lifecycle state — admin moves through New → Sent → Paid → Closed. Matches the
 *  backend enum (numeric). */
export type InvoiceStatus = 0 | 1 | 2 | 3
export const InvoiceStatusValue = { New: 0, Sent: 1, Paid: 2, Closed: 3 } as const

/** Categorizes the charge. Matches the backend enum. */
export type InvoiceType = 0 | 1
export const InvoiceTypeValue = { OneTime: 0, Subscription: 1 } as const

export interface InvoiceDto {
  id: number
  parentAccountId: number
  parentName: string | null
  parentEmail: string | null
  parentCellPhone: string | null
  description: string
  amount: number
  currency: string
  type: InvoiceType
  status: InvoiceStatus
  issuedAt: string
  dueDate: string | null
  sentAt: string | null
  paidAt: string | null
  paymentMethod: string | null
  paymentReference: string | null
  notes: string | null
  chargeTypeId: number | null
  chargeTypeName: string | null
  playerId: number | null
  playerName: string | null
  createdAt: string
  updatedAt: string
}

/** Recurrence cadence on a ChargeType — matches the backend enum (numeric). */
export type ChargeRecurrence = 0 | 1 | 2 | 3 | 4 | 5
export const ChargeRecurrenceValue = {
  OneTime: 0, Hourly: 1, Daily: 2, Weekly: 3, Monthly: 4, Yearly: 5,
} as const

export interface ChargeTypeDto {
  id: number
  name: string
  description: string | null
  amount: number
  recurrence: ChargeRecurrence
  active: boolean
  createdAt: string
  updatedAt: string
}

export interface SaveChargeTypeRequest {
  name: string
  description?: string | null
  amount: number
  recurrence: ChargeRecurrence
  active: boolean
}

export interface CreateInvoiceRequest {
  /** Optional when playerId is supplied — the backend derives the parent from the player. */
  parentAccountId?: number | null
  description: string
  amount: number
  currency?: string
  type: InvoiceType
  dueDate?: string | null
  notes?: string | null
  chargeTypeId?: number | null
  playerId?: number | null
}

export interface UpdateInvoiceRequest {
  description: string
  amount: number
  currency?: string
  type: InvoiceType
  dueDate?: string | null
  notes?: string | null
  chargeTypeId?: number | null
  playerId?: number | null
}

export interface ChangeInvoiceStatusRequest {
  status: InvoiceStatus
  paymentMethod?: string | null
  paymentReference?: string | null
}

export interface InvoiceSummaryDto {
  totalCount: number
  newCount: number
  sentCount: number
  paidCount: number
  closedCount: number
  outstandingAmount: number
  paidAmount: number
}

export interface ThreadSummary {
  phone: string
  name: string | null
  parentAccountId: number | null
  parentRegistered: boolean
  lastAt: string
  lastBody: string | null
  lastDirection: ThreadDirection
  inboundCount: number
  outboundCount: number
}

/** One registered parent in the Inbox "Message a parent" picker — for starting a thread
 *  with someone who hasn't replied yet (so they don't appear in the threads list). */
export interface InboxParent {
  parentAccountId: number
  name: string
  phone: string
  language: Language
  hasReplied: boolean
}

export interface ThreadDetail {
  phone: string
  name: string | null
  parentAccountId: number | null
  parentRegistered: boolean
  language: Language | null
  messages: ThreadMessage[]
}

export interface SendThreadReplyRequest {
  channel: MessageChannel
  body: string
}

export interface InboundMessage {
  id: number
  channel: MessageChannel
  fromPhone: string
  toPhone: string | null
  body: string | null
  twilioSid: string | null
  receivedAt: string
  /** ID of the most recent broadcast this phone was a recipient of. Null when the inbound is
   *  unprompted (e.g. parent texts us first). */
  broadcastId: number | null
  /** Short preview text of the linked broadcast — body or subject, whichever was set. */
  broadcastSummary: string | null
}

export const MESSAGE_CHANNEL_LABELS: Record<MessageChannel, string> = {
  0: 'SMS',
  1: 'WhatsApp',
  2: 'Email',
}

export const MESSAGE_DELIVERY_LABELS: Record<MessageDeliveryStatus, string> = {
  0: 'Pending',
  1: 'Queued',
  2: 'Sent',
  3: 'Delivered',
  4: 'Failed',
  5: 'Undelivered',
}

// --- Coaches module (admin HR-style profile, separate from per-team TeamCoach) ---

export interface Coach {
  id: number
  firstName: string
  lastName: string
  cellPhone: string | null
  hasWhatsApp: boolean
  email: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  postalCode: string | null
  monthlyPayment: number | null
  notes: string | null
  language: Language
  createdAt: string
  updatedAt: string
  certifications: CoachCertification[]
}

export interface CoachCertification {
  id: number
  coachId: number
  name: string
  issuingBody: string | null
  /** ISO yyyy-MM-dd. */
  issuedOn: string | null
  /** ISO yyyy-MM-dd. */
  expiresOn: string | null
  certificateNumber: string | null
  notes: string | null
  createdAt: string
}

export interface CoachSummary {
  id: number
  firstName: string
  lastName: string
  cellPhone: string | null
  email: string | null
  monthlyPayment: number | null
  certificationCount: number
  updatedAt: string
}

export interface SaveCoachRecordRequest {
  firstName: string
  lastName: string
  cellPhone?: string | null
  hasWhatsApp: boolean
  email?: string | null
  addressLine1?: string | null
  addressLine2?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  monthlyPayment?: number | null
  notes?: string | null
  language: Language
}

export interface SaveCoachCertificationRequest {
  name: string
  issuingBody?: string | null
  issuedOn?: string | null
  expiresOn?: string | null
  certificateNumber?: string | null
  notes?: string | null
}

// --- Mobile app chat groups (native in-app chat; admin-managed here, used by parents on mobile) ---

/** 0 = parent member, 1 = admin/staff member. */
export type ChatMemberRole = 0 | 1

export interface ChatGroupMemberAdmin {
  id: number
  parentAccountId: number | null
  displayName: string
  role: ChatMemberRole
  addedAt: string
}

export interface ChatGroupAdmin {
  id: number
  title: string
  teamId: number | null
  teamName: string | null
  memberCount: number
  messageCount: number
  createdAt: string
  members: ChatGroupMemberAdmin[]
}

export interface SaveChatGroupRequest {
  title: string
  /** When set, the new group is seeded with every parent on that team's roster. */
  seedFromTeamId?: number | null
}

export interface AddChatGroupMemberRequest {
  parentAccountId: number
}


// ---------- Hosted Tournaments (LVSS-hosted events) ----------

export interface InvitedTeam {
  id: number
  name: string
  headCoachName: string | null
  headCoachPhone: string | null
  headCoachEmail: string | null
  ageGroup: string | null
  notes: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveInvitedTeamRequest {
  name: string
  headCoachName?: string | null
  headCoachPhone?: string | null
  headCoachEmail?: string | null
  ageGroup?: string | null
  notes?: string | null
}

export interface HostedTournamentTeam {
  id: number
  lvssTeamId: number | null
  lvssTeamName: string | null
  invitedTeamId: number | null
  invitedTeamName: string | null
  ageGroup: string | null
  headCoachName: string | null
  headCoachPhone: string | null
  headCoachEmail: string | null
  notes: string | null
  tierId: number | null
  tierName: string | null
  bracketId: number | null
  bracketName: string | null
  paid: boolean
  paidAt: string | null
  paymentMethod: string | null
  paymentReference: string | null
  createdAt: string
}

export interface HostedTournamentBracket {
  id: number
  tierId: number
  name: string
  sortOrder: number
  notes: string | null
  createdAt: string
}

export interface HostedTournamentTier {
  id: number
  name: string
  sortOrder: number
  notes: string | null
  crossBracketPlay: boolean
  createdAt: string
  brackets: HostedTournamentBracket[]
}

export interface HostedTournamentField {
  id: number
  venueFieldId: number | null
  name: string
  sortOrder: number
  notes: string | null
  createdAt: string
}

export interface HostedTournamentMatch {
  id: number
  tierId: number | null
  tierName: string | null
  teamAId: number | null
  teamALabel: string | null
  teamBId: number | null
  teamBLabel: string | null
  fieldId: number | null
  fieldName: string | null
  dayId: number | null
  dayDate: string | null
  startTime: string | null
  durationMinutes: number
  notes: string | null
}

export interface SaveHostedTournamentBracketRequest {
  name: string
  sortOrder: number
  notes?: string | null
}

export interface SaveHostedTournamentFieldRequest {
  name: string
  venueFieldId?: number | null
  sortOrder: number
  notes?: string | null
}

export interface AssignTeamBracketRequest {
  bracketId?: number | null
}

export interface UpdateTierFlagsRequest {
  crossBracketPlay: boolean
}

export interface GenerateScheduleRequest {
  replaceExisting?: boolean
}

export interface SendScheduleEmailRequest {
  subject?: string | null
  intro?: string | null
}

export interface SendScheduleEmailResult {
  sent: number
  skipped: number
  message: string | null
}

export interface PublicScheduleDto {
  name: string
  kind: TournamentKind
  startDate: string
  endDate: string | null
  venueName: string | null
  venueAddress: string | null
  location: string | null
  rulesOfPlay: string | null
  days: HostedTournamentDay[]
  fields: HostedTournamentField[]
  matches: HostedTournamentMatch[]
}

export interface HostedTournamentDay {
  id: number
  /** YYYY-MM-DD */
  date: string
  /** HH:mm:ss when set — the browser <input type="time"> uses HH:mm. */
  startTime: string | null
  endTime: string | null
  notes: string | null
  createdAt: string
}

export interface HostedTournament {
  id: number
  name: string
  kind: TournamentKind
  startDate: string
  endDate: string | null
  venueId: number | null
  venueName: string | null
  venueAddress: string | null
  location: string | null
  costPerTeam: number | null
  notes: string | null
  rulesOfPlay: string | null
  scheduleEmailBody: string | null
  publicSlug: string | null
  matchDurationMinutes: number
  halfMinutes: number
  halftimeMinutes: number
  minutesBetweenGames: number
  createdAt: string
  updatedAt: string
  teams: HostedTournamentTeam[]
  tiers: HostedTournamentTier[]
  days: HostedTournamentDay[]
  fields: HostedTournamentField[]
  matches: HostedTournamentMatch[]
}

export interface SaveHostedTournamentTierRequest {
  name: string
  sortOrder: number
  notes?: string | null
}

export interface SaveHostedTournamentDayRequest {
  date: string
  startTime?: string | null
  endTime?: string | null
  notes?: string | null
}

export interface AssignTeamTierRequest {
  tierId?: number | null
}

export interface SetTeamPaidRequest {
  paid: boolean
  paymentMethod?: string | null
  paymentReference?: string | null
}

// Venue fields (playing surfaces under a venue)
export interface VenueField {
  id: number
  venueId: number
  name: string
  notes: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveVenueFieldRequest {
  name: string
  notes?: string | null
}

export interface SaveHostedTournamentRequest {
  name: string
  kind: TournamentKind
  startDate: string
  endDate?: string | null
  venueId?: number | null
  location?: string | null
  costPerTeam?: number | null
  notes?: string | null
  rulesOfPlay?: string | null
  scheduleEmailBody?: string | null
  matchDurationMinutes: number
  halfMinutes: number
  halftimeMinutes: number
  minutesBetweenGames: number
}

export interface SaveHostedTournamentMatchRequest {
  tierId?: number | null
  teamAId?: number | null
  teamBId?: number | null
  fieldId?: number | null
  dayId?: number | null
  /** "HH:mm:ss" (backend TimeOnly). Null unschedules. */
  startTime?: string | null
  durationMinutes?: number | null
  notes?: string | null
}

export interface AddHostedTournamentTeamRequest {
  lvssTeamId?: number | null
  invitedTeamId?: number | null
  notes?: string | null
}
