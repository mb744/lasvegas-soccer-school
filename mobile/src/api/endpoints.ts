import { api } from './client';
import type {
  AttendanceStatus,
  ChatGroup,
  ChatMessage,
  DevicePlatform,
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

// ---- Push devices ----

export async function registerDevice(expoPushToken: string, platform: DevicePlatform): Promise<void> {
  await api.post('/mobile/devices', { expoPushToken, platform });
}

export async function unregisterDevice(expoPushToken: string): Promise<void> {
  await api.delete('/mobile/devices', { data: { expoPushToken } });
}
