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
  waiverConsent: boolean
  players: RegistrationPlayerInput[]
}

export interface RegistrationSummary {
  id: number
  season: string
  parentFirstName: string
  parentLastName: string
  email: string
  cellPhone: string
  language: Language
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
  waiverConsent: boolean
  waiverSignedAt: string | null
  createdAt: string
  players: RegistrationPlayerDetail[]
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

export type MessageChannel = 0 | 1 // 0 = SMS, 1 = WhatsApp
export type MessageDeliveryStatus = 0 | 1 | 2 | 3 | 4 | 5
// Pending Queued Sent Delivered Failed Undelivered

export interface MessagingConfig {
  sms: boolean
  whatsApp: boolean
  conversations: boolean
}

export interface MessageGroupMember {
  id: number
  name: string | null
  phone: string
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
  /** Language to use for recipients without one attached (ad-hoc/individual/dynamic). Default English. */
  defaultLanguage?: Language
  /** Use an approved WhatsApp Content template instead of free-form body. WhatsApp channel only. */
  whatsAppTemplateId?: number | null
  /** Values for the template's positional variables, keyed by position as string. */
  templateVariables?: Record<string, string> | null
  target: BroadcastTarget
}

export interface BroadcastSummary {
  id: number
  channel: MessageChannel
  bodyEn: string | null
  bodyEs: string | null
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
}

export interface SavePracticeRequest {
  startsAt: string  // ISO 8601 in UTC
  endsAt?: string | null
  location?: string | null
  summary?: string | null
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

export const MESSAGE_CHANNEL_LABELS: Record<MessageChannel, string> = {
  0: 'SMS',
  1: 'WhatsApp',
}

export const MESSAGE_DELIVERY_LABELS: Record<MessageDeliveryStatus, string> = {
  0: 'Pending',
  1: 'Queued',
  2: 'Sent',
  3: 'Delivered',
  4: 'Failed',
  5: 'Undelivered',
}
