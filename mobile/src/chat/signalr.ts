import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { CHAT_HUB_URL } from '../config';
import { getAccessToken } from '../api/client';
import type { ChatMessage } from '../api/types';

/**
 * One shared SignalR connection to the chat hub for the app's lifetime. The access token is read
 * lazily per (re)connect via accessTokenFactory, so a token rotated by the REST refresh flow is
 * picked up automatically. Components subscribe to incoming messages with `onMessage`.
 */
let connection: HubConnection | null = null;
const listeners = new Set<(msg: ChatMessage) => void>();

function build(): HubConnection {
  const conn = new HubConnectionBuilder()
    .withUrl(CHAT_HUB_URL, {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  conn.on('ReceiveMessage', (msg: ChatMessage) => {
    listeners.forEach((cb) => cb(msg));
  });
  return conn;
}

export async function startChat(): Promise<void> {
  if (!connection) connection = build();
  if (connection.state === HubConnectionState.Disconnected) {
    try {
      await connection.start();
    } catch {
      // Reconnect policy + next app foreground will retry; REST history still works meanwhile.
    }
  }
}

export async function stopChat(): Promise<void> {
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await connection.stop();
  }
  connection = null;
  listeners.clear();
}

export function onMessage(cb: (msg: ChatMessage) => void): () => void {
  listeners.add(cb);
  return () => listeners.delete(cb);
}

/** Send through the hub; falls back to throwing so callers can retry over REST. */
export async function sendViaHub(groupId: number, body: string): Promise<void> {
  if (!connection || connection.state !== HubConnectionState.Connected) {
    throw new Error('not-connected');
  }
  await connection.invoke('SendMessage', groupId, body);
}
