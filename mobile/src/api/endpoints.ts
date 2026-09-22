import { api } from './client';
import type {
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
  ScheduleEvent,
  TokenResponse,
} from './types';

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
