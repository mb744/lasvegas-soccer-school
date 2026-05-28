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
  target: BroadcastTarget
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

export interface WhatsAppTemplateVariable {
  id: number
  position: number
  label: string
  example: string | null
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
  createdAt: string
  variables: WhatsAppTemplateVariable[]
  /** Opposite-language counterpart auto-detected by base name (e.g. `practice_or_game` ↔ `practice_or_game_es`). */
  paired: TemplatePair | null
}

export interface SaveTemplateVariable {
  position: number
  label: string
  example?: string | null
}

export interface SaveWhatsAppTemplateRequest {
  name: string
  contentSid: string
  language: Language
  description?: string | null
  previewText?: string | null
  variables: SaveTemplateVariable[]
}

// --- Email templates ---

export interface EmailTemplateVariable {
  id: number
  position: number
  label: string
  example: string | null
}

export interface EmailTemplatePair {
  id: number
  name: string
  language: Language
  subject: string
  body: string
  variables: EmailTemplateVariable[]
}

export interface EmailTemplate {
  id: number
  name: string
  language: Language
  description: string | null
  subject: string
  body: string
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

export type ScheduledEventKind = 0 | 1 // Game | Practice

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
}

export interface SaveGameRequest {
  startsAt: string  // ISO 8601 in UTC
  endsAt?: string | null
  opponentName?: string | null
  isHome?: boolean | null
  location?: string | null
  summary?: string | null
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
}

export interface ScheduleSyncResult {
  success: boolean
  added: number
  updated: number
  message: string
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
  coachName: string | null
  coachEmail: string | null
  coachPhone: string | null
  createdAt: string
  roster: RosterMember[]
  upcomingGames: ScheduledGame[]
}

export interface SaveCoachRequest {
  coachName?: string | null
  coachEmail?: string | null
  coachPhone?: string | null
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
