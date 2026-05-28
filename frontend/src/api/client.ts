import axios from 'axios'
import type {
  AddMessageGroupMemberRequest,
  BroadcastDetail,
  BroadcastSummary,
  CreateBroadcastRequest,
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
  RegistrationSummary,
  PhraseTranslation,
  EventRecipient,
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
  TemplatePreviewRequest,
  TemplatePreviewResponse,
  TranslateRequest,
  TranslateResponse,
  UserSummary,
  WhatsAppTemplate,
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
  async listBroadcasts() {
    const r = await api.get<BroadcastSummary[]>('/messaging/broadcasts')
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
  async listThreads() {
    const r = await api.get<ThreadSummary[]>('/messaging/threads')
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
  async getTeam(id: number) {
    const r = await api.get<TeamDetail>(`/schedule/teams/${id}`)
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
