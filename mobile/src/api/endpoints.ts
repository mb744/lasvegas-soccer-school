import { api } from './client';
import type {
  AdminAnnouncement,
  AdminChatGroup,
  AdminEvent,
  AdminPlayerOption,
  AdminTeamDetail,
  AdminUniform,
  AdminUserRow,
  AdminVenue,
  Announcement,
  AttendanceStatus,
  BlockedUser,
  ChatGroup,
  ChatMessage,
  DevicePlatform,
  InvoiceDetail,
  InvoiceSummary,
  Me,
  Player,
  SaveAnnouncementRequest,
  SaveChatGroupRequest,
  SaveGameRequest,
  SavePracticeRequest,
  SaveTeamCoachRequest,
  SaveTeamRequest,
  ScheduleEvent,
  StaffEventAttendance,
  TeamOption,
  TokenResponse,
  UserCoachTeam,
} from './types';
import type { TrainingLogin } from './types';

// ---- Auth ----

export async function login(email: string, password: string): Promise<TokenResponse> {
  const { data } = await api.post<TokenResponse>('/mobile/auth/login', { email, password });
  return data;
}

export async function fetchMe(): Promise<Me> {
  const { data } = await api.get<Me>('/mobile/auth/me');
  return data;
}

export async function logout(refreshToken: string): Promise<void> {
  await api.post('/mobile/auth/logout', { refreshToken });
}

export async function deleteAccount(): Promise<void> {
  await api.delete('/mobile/auth/me');
}

// ---- Players ----

export async function fetchPlayers(): Promise<Player[]> {
  const { data } = await api.get<Player[]>('/mobile/players');
  return data;
}

// ---- Daily Training login (the kid's username/password for the Daily Training app) ----

export async function fetchTrainingLogin(playerId: number): Promise<TrainingLogin> {
  const { data } = await api.get<TrainingLogin>(`/players/${playerId}/training-login`);
  return data;
}

/** Creates the login or updates it. Omit `password` to keep the current one. */
export async function saveTrainingLogin(
  playerId: number,
  payload: { username: string; password?: string },
): Promise<TrainingLogin> {
  const { data } = await api.put<TrainingLogin>(`/players/${playerId}/training-login`, payload);
  return data;
}

export async function deleteTrainingLogin(playerId: number): Promise<void> {
  await api.delete(`/players/${playerId}/training-login`);
}

// ---- Schedule + attendance ----

export async function fetchSchedule(): Promise<ScheduleEvent[]> {
  const { data } = await api.get<ScheduleEvent[]>('/mobile/schedule');
  return data;
}

export async function setAttendance(
  eventId: number,
  playerId: number,
  status: AttendanceStatus,
): Promise<void> {
  await api.put(`/mobile/events/${eventId}/attendance`, { playerId, status });
}

/**
 * Team-wide attendance counts on one event. Backend returns 403 for callers who are neither
 * admin nor the coach of the event's team; callers should gate the fetch on Me.isAdmin ||
 * Me.coachTeamIds.includes(event.teamId).
 */
export async function fetchStaffEventAttendance(eventId: number): Promise<StaffEventAttendance> {
  const { data } = await api.get<StaffEventAttendance>(`/mobile/staff-events/${eventId}/attendance`);
  return data;
}

// ---- Chat ----

export async function fetchChatGroups(): Promise<ChatGroup[]> {
  const { data } = await api.get<ChatGroup[]>('/mobile/chat/groups');
  return data;
}

export async function fetchChatMessages(groupId: number, before?: number): Promise<ChatMessage[]> {
  const { data } = await api.get<ChatMessage[]>(`/mobile/chat/groups/${groupId}/messages`, {
    params: before ? { before } : undefined,
  });
  return data;
}

export async function sendChatMessage(groupId: number, body: string): Promise<ChatMessage> {
  const { data } = await api.post<ChatMessage>(`/mobile/chat/groups/${groupId}/messages`, { body });
  return data;
}

export async function markChatRead(groupId: number, messageId: number): Promise<void> {
  await api.post(`/mobile/chat/groups/${groupId}/read`, null, { params: { messageId } });
}

export async function reportChatMessage(messageId: number, reason?: string): Promise<void> {
  await api.post(`/mobile/chat/messages/${messageId}/report`, { reason: reason ?? null });
}

export async function fetchChatBlocks(): Promise<BlockedUser[]> {
  const { data } = await api.get<BlockedUser[]>('/mobile/chat/blocks');
  return data;
}

export async function blockChatUser(targetUserId: string): Promise<void> {
  await api.post(`/mobile/chat/blocks/${encodeURIComponent(targetUserId)}`);
}

export async function unblockChatUser(targetUserId: string): Promise<void> {
  await api.delete(`/mobile/chat/blocks/${encodeURIComponent(targetUserId)}`);
}

// ---- Push devices ----

export async function registerDevice(expoPushToken: string, platform: DevicePlatform): Promise<void> {
  await api.post('/mobile/devices', { expoPushToken, platform });
}

export async function unregisterDevice(expoPushToken: string): Promise<void> {
  await api.delete('/mobile/devices', { data: { expoPushToken } });
}

// ---- Invoices ----

export async function fetchInvoices(): Promise<InvoiceSummary[]> {
  const { data } = await api.get<InvoiceSummary[]>('/mobile/invoices');
  return data;
}

export async function fetchInvoice(id: number): Promise<InvoiceDetail> {
  const { data } = await api.get<InvoiceDetail>(`/mobile/invoices/${id}`);
  return data;
}

// ---- Announcements ----

export async function fetchAnnouncements(): Promise<Announcement[]> {
  const { data } = await api.get<Announcement[]>('/mobile/announcements');
  return data;
}

// ---- Admin: announcements CRUD (mobile admin composer) ----

export async function fetchAdminAnnouncements(): Promise<AdminAnnouncement[]> {
  const { data } = await api.get<AdminAnnouncement[]>('/announcements');
  return data;
}

export async function createAnnouncement(req: SaveAnnouncementRequest): Promise<AdminAnnouncement> {
  const { data } = await api.post<AdminAnnouncement>('/announcements', req);
  return data;
}

export async function updateAnnouncement(id: number, req: SaveAnnouncementRequest): Promise<AdminAnnouncement> {
  const { data } = await api.put<AdminAnnouncement>(`/announcements/${id}`, req);
  return data;
}

export async function deleteAnnouncement(id: number): Promise<void> {
  await api.delete(`/announcements/${id}`);
}

// ---- Admin: reference lists ----

export async function fetchAdminTeams(): Promise<TeamOption[]> {
  const { data } = await api.get<TeamOption[]>('/mobile/admin/teams');
  return data;
}

export async function fetchAdminTeamDetail(id: number): Promise<AdminTeamDetail> {
  const { data } = await api.get<AdminTeamDetail>(`/mobile/admin/teams/${id}`);
  return data;
}

export async function fetchAdminEvents(teamId?: number): Promise<AdminEvent[]> {
  const params = teamId ? `?teamId=${teamId}` : '';
  const { data } = await api.get<AdminEvent[]>(`/mobile/admin/events${params}`);
  return data;
}

export async function cancelAdminEvent(id: number): Promise<AdminEvent> {
  const { data } = await api.post<AdminEvent>(`/mobile/admin/events/${id}/cancel`);
  return data;
}

export async function uncancelAdminEvent(id: number): Promise<AdminEvent> {
  const { data } = await api.post<AdminEvent>(`/mobile/admin/events/${id}/uncancel`);
  return data;
}

// ---- Admin: chat groups (uses same endpoints as the web admin) ----

export async function fetchAdminChatGroups(): Promise<AdminChatGroup[]> {
  const { data } = await api.get<AdminChatGroup[]>('/admin/chat-groups');
  return data;
}

export async function postAdminChatMessage(groupId: number, body: string): Promise<void> {
  await api.post(`/admin/chat-groups/${groupId}/messages`, { body });
}

export async function createChatGroup(req: SaveChatGroupRequest): Promise<AdminChatGroup> {
  const { data } = await api.post<AdminChatGroup>('/admin/chat-groups', req);
  return data;
}

export async function updateChatGroup(id: number, req: SaveChatGroupRequest): Promise<AdminChatGroup> {
  const { data } = await api.put<AdminChatGroup>(`/admin/chat-groups/${id}`, req);
  return data;
}

export async function deleteChatGroup(id: number): Promise<void> {
  await api.delete(`/admin/chat-groups/${id}`);
}

export async function addChatGroupMember(groupId: number, parentAccountId: number): Promise<AdminChatGroup> {
  const { data } = await api.post<AdminChatGroup>(`/admin/chat-groups/${groupId}/members`, { parentAccountId });
  return data;
}

export async function removeChatGroupMember(groupId: number, memberId: number): Promise<AdminChatGroup> {
  const { data } = await api.delete<AdminChatGroup>(`/admin/chat-groups/${groupId}/members/${memberId}`);
  return data;
}

// ---- Admin: chat parent picker ----

export interface AdminChatParentSearch {
  parentAccountId: number;
  name: string;
  email: string | null;
  phone: string | null;
}

export async function searchChatParents(q: string, limit = 20): Promise<AdminChatParentSearch[]> {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  params.set('limit', String(limit));
  const { data } = await api.get<AdminChatParentSearch[]>(`/admin/chat-groups/search-parents?${params.toString()}`);
  return data;
}

// ---- Admin: teams CRUD (reuses the web endpoints — dual auth) ----

// Team create lives under /api/schedule/teams (historical — the sync surface owns team CRUD).
export async function createTeam(req: SaveTeamRequest): Promise<{ id: number; name: string }> {
  const { data } = await api.post<{ id: number; name: string }>('/schedule/teams', req);
  return data;
}

export async function updateTeam(id: number, req: SaveTeamRequest): Promise<{ id: number; name: string }> {
  const { data } = await api.put<{ id: number; name: string }>(`/teams/${id}`, req);
  return data;
}

export async function deleteTeam(id: number): Promise<void> {
  await api.delete(`/teams/${id}`);
}

export async function addTeamPlayer(teamId: number, playerId: number): Promise<void> {
  await api.post(`/teams/${teamId}/roster`, { playerId });
}

export async function removeTeamPlayer(teamId: number, playerId: number): Promise<void> {
  await api.delete(`/teams/${teamId}/roster/${playerId}`);
}

export async function addTeamCoach(teamId: number, req: SaveTeamCoachRequest): Promise<void> {
  await api.post(`/teams/${teamId}/coaches`, req);
}

export async function updateTeamCoach(teamId: number, coachRowId: number, req: SaveTeamCoachRequest): Promise<void> {
  await api.put(`/teams/${teamId}/coaches/${coachRowId}`, req);
}

export async function removeTeamCoach(teamId: number, coachRowId: number): Promise<void> {
  await api.delete(`/teams/${teamId}/coaches/${coachRowId}`);
}

export async function fetchAdminPlayers(q?: string): Promise<AdminPlayerOption[]> {
  const params = q ? `?q=${encodeURIComponent(q)}` : '';
  const { data } = await api.get<AdminPlayerOption[]>(`/mobile/admin/players${params}`);
  return data;
}

export async function fetchAdminUniforms(): Promise<AdminUniform[]> {
  const { data } = await api.get<AdminUniform[]>('/mobile/admin/uniforms');
  return data;
}

export async function fetchAdminVenues(): Promise<AdminVenue[]> {
  const { data } = await api.get<AdminVenue[]>('/mobile/admin/venues');
  return data;
}

// ---- Admin: events CRUD (reuses the web endpoints — dual auth on ScheduleController) ----

export async function createPractice(teamId: number, req: SavePracticeRequest): Promise<void> {
  await api.post(`/schedule/teams/${teamId}/practices`, req);
}

export async function updatePractice(id: number, req: SavePracticeRequest): Promise<void> {
  await api.put(`/schedule/practices/${id}`, req);
}

export async function deletePractice(id: number): Promise<void> {
  await api.delete(`/schedule/practices/${id}`);
}

export async function createGame(teamId: number, req: SaveGameRequest): Promise<void> {
  await api.post(`/schedule/teams/${teamId}/games`, req);
}

export async function updateGame(id: number, req: SaveGameRequest): Promise<void> {
  await api.put(`/schedule/games/${id}`, req);
}

export async function deleteGame(id: number): Promise<void> {
  await api.delete(`/schedule/games/${id}`);
}

export async function createMiscEvent(teamId: number, req: SavePracticeRequest): Promise<void> {
  await api.post(`/schedule/teams/${teamId}/misc-events`, req);
}

export async function updateMiscEvent(id: number, req: SavePracticeRequest): Promise<void> {
  await api.put(`/schedule/misc-events/${id}`, req);
}

export async function deleteMiscEvent(id: number): Promise<void> {
  await api.delete(`/schedule/misc-events/${id}`);
}

// ---- Admin: user management ----

export async function fetchAdminUsers(): Promise<AdminUserRow[]> {
  const { data } = await api.get<AdminUserRow[]>('/admin/users');
  return data;
}

export async function updateAdminUserProfile(
  id: string,
  payload: { firstName: string; lastName: string },
): Promise<void> {
  await api.put(`/admin/users/${encodeURIComponent(id)}/profile`, payload);
}

export async function setAdminUserRole(id: string, isAdmin: boolean): Promise<void> {
  await api.put(`/admin/users/${encodeURIComponent(id)}/role`, { isAdmin });
}

/** Teams this user currently coaches — one entry per TeamCoach card whose Email matches them. */
export async function fetchUserCoachTeams(id: string): Promise<UserCoachTeam[]> {
  const { data } = await api.get<UserCoachTeam[]>(`/admin/users/${encodeURIComponent(id)}/coach-teams`);
  return data;
}

/** Full-state replacement of the user's coach-team set. Backend adds cards for new teams and
 *  deletes cards for teams the caller removed, all keyed on the user's email. */
export async function setUserCoachTeams(id: string, teamIds: number[]): Promise<void> {
  await api.put(`/admin/users/${encodeURIComponent(id)}/coach-teams`, { teamIds });
}
