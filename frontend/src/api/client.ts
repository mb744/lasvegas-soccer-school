import axios from 'axios'
import type {
  AddMessageGroupMemberRequest,
  BroadcastDetail,
  BroadcastSummary,
  FailedMessage,
  CreateBroadcastRequest,
  SendPerPlayerRequest,
  SendPerPlayerResult,
  PerPlayerPreviewResult,
  CreateGroupConversationRequest,
  CreateOutreachRequest,
  GroupConversationDetail,
  GroupConversationSummary,
  ListGroupsResponse,
  LoginRequest,
  Me,
  MessageGroupDetail,
  MessageGroupMember,
  MessageGroupSummary,
  MessagingConfig,
  OutreachResponse,
  ParentContact,
  PlayerSummary,
  RegistrationDetail,
  RegistrationPlayerDetail,
  AddRegistrationPlayerRequest,
  UpdateRegistrationPlayerRequest,
  AdminCreateRegistrationRequest,
  SignPlayerWaiverRequest,
  LinkUserToRegistrationRequest,
  RegistrationSummary,
  PhraseTranslation,
  EventRecipient,
  EventAttendanceList,
  EventAttendanceSummary,
  AttendanceStatus,
  TournamentSummary,
  SaveTournamentRequest,
  CreateTournamentTeamRequest,
  AddTournamentTeamRequest,
  UpdateTournamentTeamRequest,
  TournamentAttendanceList,
  SendTournamentConfirmationsResult,
  ResendTournamentConfirmationsRequest,
  TournamentSendPreview,
  InboundMessage,
  PracticeSeriesCreated,
  SaveGameRequest,
  SaveMessageGroupRequest,
  SavePhraseTranslationRequest,
  SavePlayerRequest,
  SavePracticeRequest,
  SavePracticeSeriesRequest,
  SaveTeamRequest,
  SaveWhatsAppTemplateRequest,
  SaveEmailTemplateRequest,
  EmailTemplate,
  MessagingSettings,
  SaveMessagingSettingsRequest,
  ThreadSummary,
  AdminPlayerSummary,
  PlayerUniformAssignment,
  CreatePlayerUniformAssignmentRequest,
  UpdatePlayerUniformAssignmentRequest,
  AdminCreatePlayerRequest,
  AdminUpdatePlayerRequest,
  SendRegistrationInviteRequest,
  SendRegistrationInviteResult,
  InvoiceDto,
  InvoiceStatus,
  InvoiceSummaryDto,
  CreateInvoiceRequest,
  UpdateInvoiceRequest,
  ChangeInvoiceStatusRequest,
  ChargeTypeDto,
  SaveChargeTypeRequest,
  InboxParent,
  ThreadDetail,
  ThreadMessage,
  SendThreadReplyRequest,
  ScheduleSyncResult,
  ScheduledGame,
  SignupRequest,
  SubmitRegistrationRequest,
  UpdateRegistrationRequest,
  AgeClassification,
  SaveAgeClassificationRequest,
  Uniform,
  SaveUniformRequest,
  Venue,
  SaveVenueRequest,
  MonthlyFeePreview,
  SendMonthlyFeeRequest,
  TeamDetail,
  TeamSummary,
  RosterTeamSummary,
  RosterTeamDetail,
  AvailablePlayer,
  CreateRosterTeamRequest,
  RenameTeamRequest,
  AddRosterMembersRequest,
  SaveCoachRequest,
  SaveTeamCoachRequest,
  TemplatePreviewRequest,
  TemplatePreviewResponse,
  TemplateProperty,
  TemplateContext,
  MappedField,
  SaveMappedFieldRequest,
  TemplateContextOption,
  Coach,
  CoachSummary,
  SaveCoachRecordRequest,
  SaveCoachCertificationRequest,
  TranslateRequest,
  TranslateResponse,
  UserSummary,
  WhatsAppTemplate,
  ChatGroupAdmin,
  SaveChatGroupRequest,
  AddChatGroupMemberRequest,
} from './types'

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

export const Api = {
  // --- Auth ---
  async signup(payload: SignupRequest) {
    const r = await api.post<Me>('/auth/signup', payload)
    return r.data
  },
  async login(payload: LoginRequest) {
    const r = await api.post<Me>('/auth/login', payload)
    return r.data
  },
  async logout() {
    await api.post('/auth/logout')
  },
  async me(): Promise<Me | null> {
    try {
      const r = await api.get<Me>('/auth/me')
      return r.data
    } catch (e: any) {
      if (e?.response?.status === 401) return null
      throw e
    }
  },
  externalLoginUrl(provider: 'Google' | 'Facebook', returnUrl = '/register') {
    return `/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`
  },
  async listProviders(): Promise<string[]> {
    try {
      const r = await api.get<string[]>('/auth/providers')
      return r.data
    } catch {
      return []
    }
  },

  // --- Players (parent roster) ---
  async listPlayers() {
    const r = await api.get<PlayerSummary[]>('/players')
    return r.data
  },
  async createPlayer(payload: SavePlayerRequest) {
    const r = await api.post<PlayerSummary>('/players', payload)
    return r.data
  },
  async updatePlayer(id: number, payload: SavePlayerRequest) {
    const r = await api.put<PlayerSummary>(`/players/${id}`, payload)
    return r.data
  },
  async deletePlayer(id: number) {
    await api.delete(`/players/${id}`)
  },

  // --- Additional parent/guardian contacts (parent prefill) ---
  async listParentContacts() {
    const r = await api.get<ParentContact[]>('/parent-contacts')
    return r.data
  },

  // --- Registration ---
  async submitRegistration(payload: SubmitRegistrationRequest) {
    const r = await api.post<RegistrationDetail>('/registrations', payload)
    return r.data
  },
  async myRegistrations() {
    const r = await api.get<RegistrationSummary[]>('/registrations/mine')
    return r.data
  },
  async listRegistrations(season?: string) {
    const r = await api.get<RegistrationSummary[]>('/registrations', {
      params: season ? { season } : undefined,
    })
    return r.data
  },
  async getRegistration(id: number) {
    const r = await api.get<RegistrationDetail>(`/registrations/${id}`)
    return r.data
  },
  async updateRegistration(id: number, payload: UpdateRegistrationRequest) {
    const r = await api.put<RegistrationDetail>(`/registrations/${id}`, payload)
    return r.data
  },
  async updatePlayerTrial(regId: number, rpId: number, freeTrialOver: boolean) {
    const r = await api.patch<RegistrationPlayerDetail>(
      `/registrations/${regId}/players/${rpId}/trial`,
      { freeTrialOver })
    return r.data
  },
  async addRegistrationPlayer(regId: number, payload: AddRegistrationPlayerRequest) {
    const r = await api.post<RegistrationDetail>(`/registrations/${regId}/players`, payload)
    return r.data
  },
  async updateRegistrationPlayer(regId: number, rpId: number, payload: UpdateRegistrationPlayerRequest) {
    const r = await api.put<RegistrationDetail>(`/registrations/${regId}/players/${rpId}`, payload)
    return r.data
  },
  async removeRegistrationPlayer(regId: number, rpId: number) {
    await api.delete(`/registrations/${regId}/players/${rpId}`)
  },
  /** Admin creates an empty registration shell for an existing parent (active season by default).
   *  Parent finishes it (consent + sign waivers) by logging in to /account. */
  async adminCreateRegistration(payload: AdminCreateRegistrationRequest) {
    const r = await api.post<RegistrationDetail>('/registrations/admin', payload)
    return r.data
  },
  /** Parent (or admin) ticks the registration-level waiver consent box. */
  async consentRegistration(id: number) {
    const r = await api.post<RegistrationDetail>(`/registrations/${id}/consent`, {})
    return r.data
  },
  /** Parent adds a player to their own registration (owner-gated equivalent of the admin
   *  AddPlayer endpoint). Enrolled unsigned; sign via signRegistrationPlayer. */
  async addOwnRegistrationPlayer(regId: number, payload: AddRegistrationPlayerRequest) {
    const r = await api.post<RegistrationDetail>(`/registrations/${regId}/players/self`, payload)
    return r.data
  },
  /** Parent (or admin) signs a player's waiver. Flips registration-level consent on too if needed. */
  async signRegistrationPlayer(regId: number, rpId: number, payload: SignPlayerWaiverRequest) {
    const r = await api.post<RegistrationDetail>(`/registrations/${regId}/players/${rpId}/sign`, payload)
    return r.data
  },
  /** Admin links a second login as a collaborator on this registration's family. If that user
   *  has their own ParentAccount, its players/contacts are merged into the primary and the
   *  secondary ParentAccount is deleted. */
  async linkUserToRegistration(regId: number, payload: LinkUserToRegistrationRequest) {
    const r = await api.post<RegistrationDetail>(`/registrations/${regId}/links`, payload)
    return r.data
  },
  /** Admin removes a collaborator. The owner can't be unlinked here. */
  async unlinkUserFromRegistration(regId: number, userId: string) {
    const r = await api.delete<RegistrationDetail>(`/registrations/${regId}/links/${encodeURIComponent(userId)}`)
    return r.data
  },
  /** Toggle family-wide no-communications opt-out from the registration detail panel. */
  async setRegistrationCommunications(regId: number, noCommunications: boolean) {
    const r = await api.put<RegistrationDetail>(`/registrations/${regId}/communications`, { noCommunications })
    return r.data
  },
  async listAgeClassifications() {
    const r = await api.get<AgeClassification[]>('/age-classifications')
    return r.data
  },
  async createAgeClassification(payload: SaveAgeClassificationRequest) {
    const r = await api.post<AgeClassification>('/age-classifications', payload)
    return r.data
  },
  async updateAgeClassification(id: number, payload: SaveAgeClassificationRequest) {
    const r = await api.put<AgeClassification>(`/age-classifications/${id}`, payload)
    return r.data
  },
  async deleteAgeClassification(id: number) {
    await api.delete(`/age-classifications/${id}`)
  },
  async listUniforms() {
    const r = await api.get<Uniform[]>('/uniforms')
    return r.data
  },
  async createUniform(payload: SaveUniformRequest) {
    const r = await api.post<Uniform>('/uniforms', payload)
    return r.data
  },
  async updateUniform(id: number, payload: SaveUniformRequest) {
    const r = await api.put<Uniform>(`/uniforms/${id}`, payload)
    return r.data
  },
  async deleteUniform(id: number) {
    await api.delete(`/uniforms/${id}`)
  },
  async listVenues() {
    const r = await api.get<Venue[]>('/venues')
    return r.data
  },
  async createVenue(payload: SaveVenueRequest) {
    const r = await api.post<Venue>('/venues', payload)
    return r.data
  },
  async updateVenue(id: number, payload: SaveVenueRequest) {
    const r = await api.put<Venue>(`/venues/${id}`, payload)
    return r.data
  },
  async deleteVenue(id: number) {
    await api.delete(`/venues/${id}`)
  },
  async deleteRegistration(id: number) {
    await api.delete(`/registrations/${id}`)
  },

  async viewWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },
  async downloadWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, { responseType: 'blob' })
    triggerDownload(r.data as Blob, `waivers-${id}.pdf`)
  },
  async viewPlayerWaiver(regId: number, rpId: number) {
    const r = await api.get(`/registrations/${regId}/players/${rpId}/waiver.pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },
  async downloadPlayerWaiver(regId: number, rpId: number, filenameStem: string) {
    const r = await api.get(`/registrations/${regId}/players/${rpId}/waiver.pdf`, { responseType: 'blob' })
    triggerDownload(r.data as Blob, `waiver-${filenameStem}.pdf`)
  },

  // --- Outreach (admin) ---
  async createOutreach(payload: CreateOutreachRequest) {
    const r = await api.post<OutreachResponse>('/outreach', payload)
    return r.data
  },
  async listOutreach() {
    const r = await api.get<OutreachResponse[]>('/outreach')
    return r.data
  },
  async resendOutreach(id: number) {
    const r = await api.post<OutreachResponse>(`/outreach/${id}/resend`, {})
    return r.data
  },
  async deleteOutreach(id: number) {
    await api.delete(`/outreach/${id}`)
  },

  // --- Admin: user management ---
  async listUsers() {
    const r = await api.get<UserSummary[]>('/admin/users')
    return r.data
  },
  async banUser(id: string) {
    await api.post(`/admin/users/${id}/ban`, {})
  },
  async unbanUser(id: string) {
    await api.post(`/admin/users/${id}/unban`, {})
  },

  // --- Messaging (admin chat/broadcast) ---
  async messagingConfig() {
    const r = await api.get<MessagingConfig>('/messaging/config')
    return r.data
  },
  async listMessagingGroups() {
    const r = await api.get<ListGroupsResponse>('/messaging/groups')
    return r.data
  },
  async getMessagingGroup(id: number) {
    const r = await api.get<MessageGroupDetail>(`/messaging/groups/${id}`)
    return r.data
  },
  async createMessagingGroup(payload: SaveMessageGroupRequest) {
    const r = await api.post<MessageGroupSummary>('/messaging/groups', payload)
    return r.data
  },
  async updateMessagingGroup(id: number, payload: SaveMessageGroupRequest) {
    const r = await api.put<MessageGroupSummary>(`/messaging/groups/${id}`, payload)
    return r.data
  },
  async deleteMessagingGroup(id: number) {
    await api.delete(`/messaging/groups/${id}`)
  },
  async addMessagingGroupMember(id: number, payload: AddMessageGroupMemberRequest) {
    const r = await api.post<MessageGroupMember>(`/messaging/groups/${id}/members`, payload)
    return r.data
  },
  async removeMessagingGroupMember(id: number, memberId: number) {
    await api.delete(`/messaging/groups/${id}/members/${memberId}`)
  },
  async updateMessagingGroupMemberLanguage(id: number, memberId: number, language: 0 | 1) {
    const r = await api.patch<MessageGroupMember>(`/messaging/groups/${id}/members/${memberId}/language`, { language })
    return r.data
  },
  async importActiveSeasonIntoGroup(id: number) {
    const r = await api.post<MessageGroupDetail>(`/messaging/groups/${id}/import-active-season`, {})
    return r.data
  },
  async createBroadcast(payload: CreateBroadcastRequest) {
    const r = await api.post<BroadcastDetail>('/messaging/broadcasts', payload)
    return r.data
  },
  async sendEventPerPlayer(payload: SendPerPlayerRequest) {
    const r = await api.post<SendPerPlayerResult>('/messaging/broadcasts/send-per-player', payload)
    return r.data
  },
  async previewEventPerPlayer(payload: SendPerPlayerRequest) {
    const r = await api.post<PerPlayerPreviewResult>('/messaging/broadcasts/preview-per-player', payload)
    return r.data
  },
  async listBroadcasts() {
    const r = await api.get<BroadcastSummary[]>('/messaging/broadcasts')
    return r.data
  },
  async listFailedMessages() {
    const r = await api.get<FailedMessage[]>('/messaging/failed-messages')
    return r.data
  },
  async listInboundMessages() {
    const r = await api.get<InboundMessage[]>('/messaging/inbound')
    return r.data
  },
  async getBroadcast(id: number) {
    const r = await api.get<BroadcastDetail>(`/messaging/broadcasts/${id}`)
    return r.data
  },
  /** Expands a fan-out batch row: returns all per-player child broadcasts' recipients
   *  flattened into one list. Used by History for tournament confirmation batches. */
  async getBroadcastBatch(batchId: string) {
    const r = await api.get<BroadcastDetail>(`/messaging/batches/${batchId}`)
    return r.data
  },
  async createConversation(payload: CreateGroupConversationRequest) {
    const r = await api.post<GroupConversationDetail>('/messaging/conversations', payload)
    return r.data
  },
  async listConversations() {
    const r = await api.get<GroupConversationSummary[]>('/messaging/conversations')
    return r.data
  },
  async getConversation(id: number) {
    const r = await api.get<GroupConversationDetail>(`/messaging/conversations/${id}`)
    return r.data
  },
  async sendToConversation(id: number, body: string) {
    await api.post(`/messaging/conversations/${id}/messages`, { body })
  },
  async removeConversationParticipant(id: number, participantId: number) {
    await api.delete(`/messaging/conversations/${id}/participants/${participantId}`)
  },
  async deleteConversation(id: number) {
    await api.delete(`/messaging/conversations/${id}`)
  },
  async listWhatsAppTemplates() {
    const r = await api.get<WhatsAppTemplate[]>('/messaging/whatsapp-templates')
    return r.data
  },
  async createWhatsAppTemplate(payload: SaveWhatsAppTemplateRequest) {
    const r = await api.post<WhatsAppTemplate>('/messaging/whatsapp-templates', payload)
    return r.data
  },
  async updateWhatsAppTemplate(id: number, payload: SaveWhatsAppTemplateRequest) {
    const r = await api.put<WhatsAppTemplate>(`/messaging/whatsapp-templates/${id}`, payload)
    return r.data
  },
  async deleteWhatsAppTemplate(id: number) {
    await api.delete(`/messaging/whatsapp-templates/${id}`)
  },
  async listEmailTemplates() {
    const r = await api.get<EmailTemplate[]>('/messaging/email-templates')
    return r.data
  },
  async createEmailTemplate(payload: SaveEmailTemplateRequest) {
    const r = await api.post<EmailTemplate>('/messaging/email-templates', payload)
    return r.data
  },
  async updateEmailTemplate(id: number, payload: SaveEmailTemplateRequest) {
    const r = await api.put<EmailTemplate>(`/messaging/email-templates/${id}`, payload)
    return r.data
  },
  async deleteEmailTemplate(id: number) {
    await api.delete(`/messaging/email-templates/${id}`)
  },
  async getMessagingSettings() {
    const r = await api.get<MessagingSettings>('/messaging/settings')
    return r.data
  },
  async updateMessagingSettings(payload: SaveMessagingSettingsRequest) {
    const r = await api.put<MessagingSettings>('/messaging/settings', payload)
    return r.data
  },
  // --- Admin Players ---
  async listAdminPlayers(q?: string) {
    const params = q?.trim() ? `?q=${encodeURIComponent(q.trim())}` : ''
    const r = await api.get<AdminPlayerSummary[]>(`/admin/players${params}`)
    return r.data
  },
  async listPlayerUniforms(playerId: number) {
    const r = await api.get<PlayerUniformAssignment[]>(`/admin/players/${playerId}/uniforms`)
    return r.data
  },
  async createPlayerUniform(playerId: number, payload: CreatePlayerUniformAssignmentRequest) {
    const r = await api.post<PlayerUniformAssignment>(`/admin/players/${playerId}/uniforms`, payload)
    return r.data
  },
  async updatePlayerUniform(playerId: number, assignmentId: number, payload: UpdatePlayerUniformAssignmentRequest) {
    const r = await api.put<PlayerUniformAssignment>(`/admin/players/${playerId}/uniforms/${assignmentId}`, payload)
    return r.data
  },
  async deletePlayerUniform(playerId: number, assignmentId: number) {
    await api.delete(`/admin/players/${playerId}/uniforms/${assignmentId}`)
  },
  async createAdminPlayer(payload: AdminCreatePlayerRequest) {
    const r = await api.post<AdminPlayerSummary>('/admin/players', payload)
    return r.data
  },
  async updateAdminPlayer(playerId: number, payload: AdminUpdatePlayerRequest) {
    const r = await api.put<AdminPlayerSummary>(`/admin/players/${playerId}`, payload)
    return r.data
  },
  async sendRegistrationInvite(payload: SendRegistrationInviteRequest) {
    const r = await api.post<SendRegistrationInviteResult>('/admin/players/send-registration-invite', payload)
    return r.data
  },

  // --- Admin Invoices ---
  async listInvoices(filter?: { status?: InvoiceStatus; parentAccountId?: number; q?: string }) {
    const params = new URLSearchParams()
    if (filter?.status !== undefined) params.set('status', String(filter.status))
    if (filter?.parentAccountId !== undefined) params.set('parentAccountId', String(filter.parentAccountId))
    if (filter?.q) params.set('q', filter.q)
    const qs = params.toString()
    const r = await api.get<InvoiceDto[]>(`/admin/invoices${qs ? `?${qs}` : ''}`)
    return r.data
  },
  async getInvoiceSummary() {
    const r = await api.get<InvoiceSummaryDto>('/admin/invoices/summary')
    return r.data
  },
  async createInvoice(payload: CreateInvoiceRequest) {
    const r = await api.post<InvoiceDto>('/admin/invoices', payload)
    return r.data
  },
  async updateInvoice(id: number, payload: UpdateInvoiceRequest) {
    const r = await api.put<InvoiceDto>(`/admin/invoices/${id}`, payload)
    return r.data
  },
  async changeInvoiceStatus(id: number, payload: ChangeInvoiceStatusRequest) {
    const r = await api.post<InvoiceDto>(`/admin/invoices/${id}/status`, payload)
    return r.data
  },
  async deleteInvoice(id: number) {
    await api.delete(`/admin/invoices/${id}`)
  },

  // --- Admin Charge Types (catalog of billable types invoices can be created from) ---
  async listChargeTypes(activeOnly = false) {
    const qs = activeOnly ? '?activeOnly=true' : ''
    const r = await api.get<ChargeTypeDto[]>(`/admin/charge-types${qs}`)
    return r.data
  },
  async createChargeType(payload: SaveChargeTypeRequest) {
    const r = await api.post<ChargeTypeDto>('/admin/charge-types', payload)
    return r.data
  },
  async updateChargeType(id: number, payload: SaveChargeTypeRequest) {
    const r = await api.put<ChargeTypeDto>(`/admin/charge-types/${id}`, payload)
    return r.data
  },
  async deleteChargeType(id: number) {
    await api.delete(`/admin/charge-types/${id}`)
  },

  async listThreads() {
    const r = await api.get<ThreadSummary[]>('/messaging/threads')
    return r.data
  },
  /** Admin-triggered Twilio backfill — pulls messages from the last `days` (1-30) and inserts
   *  any our DB is missing. Hourly background reconciler covers the recent window; this is
   *  for recovering older gaps. Idempotent on TwilioSid. */
  async reconcileTwilioMessages(days: number) {
    const r = await api.post<{ outboundInserted: number; inboundInserted: number; message: string }>(
      `/messaging/twilio/reconcile?days=${encodeURIComponent(String(days))}`, {})
    return r.data
  },
  /** Search registered parents for the Inbox "Message a parent" picker. Returns up to `limit`
   *  matches by name or trailing phone digits. `unrepliedOnly` filters out parents who already
   *  have a thread in the inbox (any inbound on record in the last 6 months). */
  async searchInboxParents(query: string, opts?: { unrepliedOnly?: boolean; limit?: number }) {
    const params = new URLSearchParams()
    if (query) params.set('q', query)
    if (opts?.unrepliedOnly) params.set('unrepliedOnly', 'true')
    if (opts?.limit) params.set('limit', String(opts.limit))
    const r = await api.get<InboxParent[]>(`/messaging/parents-search?${params.toString()}`)
    return r.data
  },
  async getThread(phone: string) {
    const r = await api.get<ThreadDetail>(`/messaging/threads/${encodeURIComponent(phone)}`)
    return r.data
  },
  async sendThreadReply(phone: string, payload: SendThreadReplyRequest) {
    const r = await api.post<ThreadMessage>(`/messaging/threads/${encodeURIComponent(phone)}/reply`, payload)
    return r.data
  },
  async previewMonthlyFee() {
    const r = await api.get<MonthlyFeePreview>('/messaging/monthly-fee/preview')
    return r.data
  },
  async sendMonthlyFee(payload: SendMonthlyFeeRequest) {
    const r = await api.post<BroadcastDetail>('/messaging/monthly-fee/send', payload)
    return r.data
  },

  // --- Schedules (GotSport iCal sync) ---
  async listTeams() {
    const r = await api.get<TeamSummary[]>('/schedule/teams')
    return r.data
  },
  async getTeam(id: number, opts?: { includePast?: boolean }) {
    const r = await api.get<TeamDetail>(`/schedule/teams/${id}`, {
      params: opts?.includePast ? { includePast: true } : undefined,
    })
    return r.data
  },
  async createTeam(payload: SaveTeamRequest) {
    const r = await api.post<TeamSummary>('/schedule/teams', payload)
    return r.data
  },
  async updateTeam(id: number, payload: SaveTeamRequest) {
    const r = await api.put<TeamSummary>(`/schedule/teams/${id}`, payload)
    return r.data
  },
  async deleteTeam(id: number) {
    await api.delete(`/schedule/teams/${id}`)
  },
  async syncTeam(id: number) {
    const r = await api.post<ScheduleSyncResult>(`/schedule/teams/${id}/sync`, {})
    return r.data
  },
  async listUpcomingGames(days = 14) {
    const r = await api.get<ScheduledGame[]>('/schedule/games', { params: { days } })
    return r.data
  },
  async createPractice(teamId: number, payload: SavePracticeRequest) {
    const r = await api.post<ScheduledGame>(`/schedule/teams/${teamId}/practices`, payload)
    return r.data
  },
  async createPracticeSeries(teamId: number, payload: SavePracticeSeriesRequest) {
    const r = await api.post<PracticeSeriesCreated>(`/schedule/teams/${teamId}/practice-series`, payload)
    return r.data
  },
  async updatePractice(id: number, payload: SavePracticeRequest) {
    const r = await api.put<ScheduledGame>(`/schedule/practices/${id}`, payload)
    return r.data
  },
  async deletePractice(id: number) {
    await api.delete(`/schedule/practices/${id}`)
  },
  async cancelPractice(id: number) {
    const r = await api.post<ScheduledGame>(`/schedule/practices/${id}/cancel`, {})
    return r.data
  },
  // Miscellaneous events (banquets, photos, fundraisers) — same shape as practice.
  async createMiscEvent(teamId: number, payload: SavePracticeRequest) {
    const r = await api.post<ScheduledGame>(`/schedule/teams/${teamId}/misc-events`, payload)
    return r.data
  },
  async updateMiscEvent(id: number, payload: SavePracticeRequest) {
    const r = await api.put<ScheduledGame>(`/schedule/misc-events/${id}`, payload)
    return r.data
  },
  async deleteMiscEvent(id: number) {
    await api.delete(`/schedule/misc-events/${id}`)
  },
  async cancelMiscEvent(id: number) {
    const r = await api.post<ScheduledGame>(`/schedule/misc-events/${id}/cancel`, {})
    return r.data
  },
  async createGame(teamId: number, payload: SaveGameRequest) {
    const r = await api.post<ScheduledGame>(`/schedule/teams/${teamId}/games`, payload)
    return r.data
  },
  async updateGame(id: number, payload: SaveGameRequest) {
    const r = await api.put<ScheduledGame>(`/schedule/games/${id}`, payload)
    return r.data
  },
  async deleteGame(id: number) {
    await api.delete(`/schedule/games/${id}`)
  },
  async cancelGame(id: number) {
    const r = await api.post<ScheduledGame>(`/schedule/games/${id}/cancel`, {})
    return r.data
  },
  async listEventRecipients(eventId: number) {
    const r = await api.get<EventRecipient[]>(`/schedule/events/${eventId}/broadcast-recipients`)
    return r.data
  },
  async listTournaments() {
    const r = await api.get<TournamentSummary[]>('/schedule/tournaments')
    return r.data
  },
  async createTournament(payload: SaveTournamentRequest) {
    const r = await api.post<TournamentSummary>('/schedule/tournaments', payload)
    return r.data
  },
  async updateTournament(id: number, payload: SaveTournamentRequest) {
    const r = await api.put<TournamentSummary>(`/schedule/tournaments/${id}`, payload)
    return r.data
  },
  async deleteTournament(id: number) {
    await api.delete(`/schedule/tournaments/${id}`)
  },
  async syncTournament(id: number) {
    const r = await api.post<ScheduleSyncResult>(`/schedule/tournaments/${id}/sync`, {})
    return r.data
  },
  /** Legacy: creates the tournament's single dedicated team. New flow uses
   *  addTournamentTeam against the multi-team join. */
  async createTournamentTeam(tournamentId: number, payload: CreateTournamentTeamRequest) {
    const r = await api.post<TournamentSummary>(`/schedule/tournaments/${tournamentId}/team`, payload)
    return r.data
  },
  /** Multi-team workflow: add a team to a tournament (existing team or new) with optional
   *  GotSport sync IDs for this participation. */
  async addTournamentTeam(tournamentId: number, payload: AddTournamentTeamRequest) {
    const r = await api.post<TournamentSummary>(`/schedule/tournaments/${tournamentId}/teams`, payload)
    return r.data
  },
  /** Update GotSport IDs for one TournamentTeam participation. */
  async updateTournamentTeam(tournamentId: number, ttId: number, payload: UpdateTournamentTeamRequest) {
    const r = await api.put<TournamentSummary>(`/schedule/tournaments/${tournamentId}/teams/${ttId}`, payload)
    return r.data
  },
  /** Detach a team from a tournament. The team itself stays. */
  async removeTournamentTeam(tournamentId: number, ttId: number) {
    const r = await api.delete<TournamentSummary>(`/schedule/tournaments/${tournamentId}/teams/${ttId}`)
    return r.data
  },
  /** Sync games for one TournamentTeam participation via its GotSport IDs. */
  async syncTournamentTeam(tournamentId: number, ttId: number) {
    const r = await api.post<ScheduleSyncResult>(`/schedule/tournaments/${tournamentId}/teams/${ttId}/sync`, {})
    return r.data
  },
  /** Parse a block of text copied from the TeamSnap UI schedule table and upsert
   *  ScheduledGame rows for the games this team plays in. Workaround for TeamSnap's
   *  public API not exposing per-match startDate / startTime / venueId. */
  async importTournamentTeamPastedSchedule(
    tournamentId: number, ttId: number, text: string, teamNameOverride?: string | null,
  ) {
    const r = await api.post<ScheduleSyncResult>(
      `/schedule/tournaments/${tournamentId}/teams/${ttId}/import-pasted-schedule`,
      { text, teamNameOverride: teamNameOverride ?? null })
    return r.data
  },
  /** Per-team attendance scoped to a tournament + team combination. */
  async getTournamentTeamAttendance(tournamentId: number, teamId: number) {
    const r = await api.get<TournamentAttendanceList>(`/schedule/tournaments/${tournamentId}/teams/${teamId}/attendance`)
    return r.data
  },
  /** Per-team confirmation send. Reuses the same WhatsApp tournamentparticipation_* template
   *  but fans out only to this team's rostered players. */
  async sendTournamentTeamConfirmations(
    tournamentId: number, teamId: number, overrides?: Record<number, string> | null,
  ) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/tournaments/${tournamentId}/teams/${teamId}/send-confirmations`,
      { overrides: overrides ?? null })
    return r.data
  },
  /** Per-team re-send scoped to a subset of the roster: failed delivery, never delivered
   *  (player added after the original send, or only legacy broadcasts), and/or no response yet. */
  async resendTournamentTeamConfirmations(
    tournamentId: number, teamId: number, filter: ResendTournamentConfirmationsRequest,
  ) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/tournaments/${tournamentId}/teams/${teamId}/resend-confirmations`, filter)
    return r.data
  },
  /** Pre-send bilingual preview of the tournament confirmation message, using the first
   *  rostered player as the sample for the player-name variable. */
  async getTournamentSendPreview(tournamentId: number, teamId: number) {
    const r = await api.get<TournamentSendPreview>(
      `/messaging/tournaments/${tournamentId}/teams/${teamId}/send-preview`)
    return r.data
  },
  /** Per-player tournament confirmation status; mirrors the event-attendance endpoint. */
  async getTournamentAttendance(tournamentId: number) {
    const r = await api.get<TournamentAttendanceList>(`/schedule/tournaments/${tournamentId}/attendance`)
    return r.data
  },
  async setTournamentAttendance(tournamentId: number, playerId: number, status: AttendanceStatus) {
    const r = await api.put<TournamentAttendanceList>(
      `/schedule/tournaments/${tournamentId}/attendance/${playerId}`, { status })
    return r.data
  },
  /** Toggle the per-player Paid flag for a tournament. Drives the fee-reminder fan-out. */
  async setTournamentPaid(tournamentId: number, playerId: number, paid: boolean) {
    const r = await api.put<TournamentAttendanceList>(
      `/schedule/tournaments/${tournamentId}/attendance/${playerId}/paid`, { paid })
    return r.data
  },
  /** Per-team fee-reminder send. Uses `tournamentfee_*` (Tournament Kind) or `leaguefee_*`
   *  (League Kind) and skips players already marked Paid. Accepts edited variable values
   *  from the preview modal; backend skips overrides on player.* variables so each
   *  recipient still gets their own name. */
  async sendTournamentTeamFeeReminders(
    tournamentId: number, teamId: number, overrides?: Record<number, string> | null,
  ) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/tournaments/${tournamentId}/teams/${teamId}/send-fee-reminders`,
      { overrides: overrides ?? null })
    return r.data
  },
  /** Pre-send bilingual preview of the fee-reminder message, sampled from the first unpaid
   *  rostered player. Parallel to getTournamentSendPreview but uses the fee template. */
  async getTournamentFeeSendPreview(tournamentId: number, teamId: number) {
    const r = await api.get<TournamentSendPreview>(
      `/messaging/tournaments/${tournamentId}/teams/${teamId}/fee-send-preview`)
    return r.data
  },
  /** Fan-out: sends a WhatsApp tournament_* template once per rostered player. Each broadcast
   *  is tagged with the tournament so inbound replies update TournamentAttendance. */
  async sendTournamentConfirmations(tournamentId: number) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/tournaments/${tournamentId}/send-confirmations`, {})
    return r.data
  },
  async getEventAttendance(eventId: number) {
    const r = await api.get<EventAttendanceList>(`/schedule/events/${eventId}/attendance`)
    return r.data
  },
  async getTeamAttendanceSummary(teamId: number) {
    const r = await api.get<EventAttendanceSummary[]>(`/schedule/teams/${teamId}/attendance-summary`)
    return r.data
  },
  /** Re-sends the event's most recent message to every pending family. Fans out per-player on
   *  the backend so each parent gets a message personalized to THEIR kid (player.* / parent.*
   *  variables re-resolve per recipient). `overrides` carries inline edits from the preview
   *  modal — same skip-player.* rule as the tournament send. */
  async resendEventMessage(eventId: number, overrides?: Record<number, string> | null) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/events/${eventId}/resend`,
      { overrides: overrides ?? null })
    return r.data
  },
  async resendEventToPlayer(eventId: number, playerId: number, overrides?: Record<number, string> | null) {
    const r = await api.post<SendTournamentConfirmationsResult>(
      `/messaging/events/${eventId}/resend/${playerId}`,
      { overrides: overrides ?? null })
    return r.data
  },
  /** Pre-send EN/ES preview for the event resend modal. Samples the first pending player so
   *  the rendered body shows the per-player personalized form. */
  /** Bilingual preview for the event resend modal. When `playerId` is omitted, samples the
   *  first pending player (used by the per-event "Re-send to no-shows" button). When set,
   *  samples THAT specific player — used by the per-player Re-send button in the attendance
   *  panel so the admin sees the exact body for the family they're about to nudge. */
  async getEventResendPreview(eventId: number, playerId?: number) {
    const qs = playerId === undefined ? '' : `?playerId=${playerId}`
    const r = await api.get<TournamentSendPreview>(`/messaging/events/${eventId}/resend-preview${qs}`)
    return r.data
  },
  async setEventAttendance(eventId: number, playerId: number, status: AttendanceStatus) {
    const r = await api.put<EventAttendanceList>(`/schedule/events/${eventId}/attendance/${playerId}`, { status })
    return r.data
  },

  // --- Teams (roster builder) ---
  async listRosterTeams() {
    const r = await api.get<RosterTeamSummary[]>('/teams')
    return r.data
  },
  async getRosterTeam(id: number) {
    const r = await api.get<RosterTeamDetail>(`/teams/${id}`)
    return r.data
  },
  async createRosterTeam(payload: CreateRosterTeamRequest) {
    const r = await api.post<RosterTeamSummary>('/teams', payload)
    return r.data
  },
  async renameRosterTeam(id: number, payload: RenameTeamRequest) {
    const r = await api.put<RosterTeamSummary>(`/teams/${id}`, payload)
    return r.data
  },
  async deleteRosterTeam(id: number) {
    await api.delete(`/teams/${id}`)
  },
  async listAvailablePlayers(id: number, season?: string) {
    const r = await api.get<AvailablePlayer[]>(`/teams/${id}/available-players`, {
      params: season ? { season } : undefined,
    })
    return r.data
  },
  async addRosterMembers(id: number, payload: AddRosterMembersRequest) {
    const r = await api.post<RosterTeamDetail>(`/teams/${id}/roster`, payload)
    return r.data
  },
  async removeRosterMember(id: number, playerId: number) {
    await api.delete(`/teams/${id}/roster/${playerId}`)
  },
  async updateTeamCoach(id: number, payload: SaveCoachRequest) {
    const r = await api.put<RosterTeamDetail>(`/teams/${id}/coach`, payload)
    return r.data
  },
  /** Multi-coach management. Each call returns the refreshed RosterTeamDetail (which carries
   *  the full coaches list) so callers can update local state without a separate GET. */
  async addTeamCoachEntry(id: number, payload: SaveTeamCoachRequest) {
    const r = await api.post<RosterTeamDetail>(`/teams/${id}/coaches`, payload)
    return r.data
  },
  async updateTeamCoachEntry(id: number, coachId: number, payload: SaveTeamCoachRequest) {
    const r = await api.put<RosterTeamDetail>(`/teams/${id}/coaches/${coachId}`, payload)
    return r.data
  },
  async removeTeamCoachEntry(id: number, coachId: number) {
    const r = await api.delete<RosterTeamDetail>(`/teams/${id}/coaches/${coachId}`)
    return r.data
  },

  // --- Phrase translation dictionary ---
  async listPhraseTranslations() {
    const r = await api.get<PhraseTranslation[]>('/messaging/translations')
    return r.data
  },
  async createPhraseTranslation(payload: SavePhraseTranslationRequest) {
    const r = await api.post<PhraseTranslation>('/messaging/translations', payload)
    return r.data
  },
  async updatePhraseTranslation(id: number, payload: SavePhraseTranslationRequest) {
    const r = await api.put<PhraseTranslation>(`/messaging/translations/${id}`, payload)
    return r.data
  },
  async deletePhraseTranslation(id: number) {
    await api.delete(`/messaging/translations/${id}`)
  },
  async translate(payload: TranslateRequest) {
    const r = await api.post<TranslateResponse>('/messaging/translate', payload)
    return r.data
  },
  async templatePreview(payload: TemplatePreviewRequest) {
    const r = await api.post<TemplatePreviewResponse>('/messaging/template-preview', payload)
    return r.data
  },
  /** Lists the property keys exposed by a TemplateContext, used by the admin Templates UI
   *  to populate the per-variable "Map to" dropdown. */
  async listTemplateProperties(context: TemplateContext) {
    const r = await api.get<TemplateProperty[]>(`/messaging/template-properties/${context}`)
    return r.data
  },
  async listMappedFields() {
    const r = await api.get<MappedField[]>('/mapped-fields')
    return r.data
  },
  async createMappedField(payload: SaveMappedFieldRequest) {
    const r = await api.post<MappedField>('/mapped-fields', payload)
    return r.data
  },
  async updateMappedField(id: number, payload: SaveMappedFieldRequest) {
    const r = await api.put<MappedField>(`/mapped-fields/${id}`, payload)
    return r.data
  },
  async deleteMappedField(id: number) {
    await api.delete(`/mapped-fields/${id}`)
  },
  /** Hard-coded list of TemplateContext options for the Context dropdown on the Templates tab. */
  async listTemplateContexts() {
    const r = await api.get<TemplateContextOption[]>('/messaging/template-contexts')
    return r.data
  },

  // --- Coaches module (admin HR-style profile, separate from per-team TeamCoach) ---
  async listCoaches() {
    const r = await api.get<CoachSummary[]>('/coaches')
    return r.data
  },
  async getCoach(id: number) {
    const r = await api.get<Coach>(`/coaches/${id}`)
    return r.data
  },
  async createCoach(payload: SaveCoachRecordRequest) {
    const r = await api.post<Coach>('/coaches', payload)
    return r.data
  },
  async updateCoach(id: number, payload: SaveCoachRecordRequest) {
    const r = await api.put<Coach>(`/coaches/${id}`, payload)
    return r.data
  },
  async deleteCoach(id: number) {
    await api.delete(`/coaches/${id}`)
  },
  async addCoachCertification(id: number, payload: SaveCoachCertificationRequest) {
    const r = await api.post<Coach>(`/coaches/${id}/certifications`, payload)
    return r.data
  },
  async updateCoachCertification(id: number, certId: number, payload: SaveCoachCertificationRequest) {
    const r = await api.put<Coach>(`/coaches/${id}/certifications/${certId}`, payload)
    return r.data
  },
  async removeCoachCertification(id: number, certId: number) {
    const r = await api.delete<Coach>(`/coaches/${id}/certifications/${certId}`)
    return r.data
  },

  // --- Mobile app chat groups (native in-app chat the mobile app's parents use) ---
  async listChatGroups() {
    const r = await api.get<ChatGroupAdmin[]>('/admin/chat-groups')
    return r.data
  },
  async createChatGroup(payload: SaveChatGroupRequest) {
    const r = await api.post<ChatGroupAdmin>('/admin/chat-groups', payload)
    return r.data
  },
  async renameChatGroup(id: number, payload: SaveChatGroupRequest) {
    const r = await api.put<ChatGroupAdmin>(`/admin/chat-groups/${id}`, payload)
    return r.data
  },
  async deleteChatGroup(id: number) {
    await api.delete(`/admin/chat-groups/${id}`)
  },
  async addChatGroupMember(id: number, payload: AddChatGroupMemberRequest) {
    const r = await api.post<ChatGroupAdmin>(`/admin/chat-groups/${id}/members`, payload)
    return r.data
  },
  async removeChatGroupMember(id: number, memberId: number) {
    const r = await api.delete<ChatGroupAdmin>(`/admin/chat-groups/${id}/members/${memberId}`)
    return r.data
  },
  async postChatGroupMessage(id: number, body: string) {
    await api.post(`/admin/chat-groups/${id}/messages`, { body })
  },
}

function triggerDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
