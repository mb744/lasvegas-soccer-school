import Constants from 'expo-constants';

/**
 * Base URL of the existing ASP.NET backend. Resolution order:
 *   1. EXPO_PUBLIC_API_BASE_URL env var (a `.env` file in mobile/, or an EAS build profile) —
 *      use this for local testing, e.g. http://10.0.2.2:5282 on an Android emulator.
 *   2. extra.apiBaseUrl in app.json.
 *   3. Production.
 * The REST API lives under `${API_BASE_URL}/api`, the SignalR chat hub at `${API_BASE_URL}/hubs/chat`.
 */
export const API_BASE_URL: string =
  process.env.EXPO_PUBLIC_API_BASE_URL ??
  (Constants.expoConfig?.extra?.apiBaseUrl as string | undefined) ??
  'https://registration.lasvegassoccerschool.org';

export const API_URL = `${API_BASE_URL}/api`;
export const CHAT_HUB_URL = `${API_BASE_URL}/hubs/chat`;
