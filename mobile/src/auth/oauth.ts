import Constants from 'expo-constants';
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { api } from '../api/client';
import type { TokenResponse } from '../api/types';

// Ends any lingering system browser sessions once auth completes (Expo docs' recommended call).
WebBrowser.maybeCompleteAuthSession();

interface OAuthConfig {
  googleIosClientId?: string;
  googleAndroidClientId?: string;
  googleWebClientId?: string;
  facebookAppId?: string;
}

function readConfig(): OAuthConfig {
  const extra = (Constants.expoConfig?.extra ?? {}) as Record<string, unknown>;
  const oauth = (extra.oauth ?? {}) as Record<string, unknown>;
  return {
    googleIosClientId: oauth.googleIosClientId as string | undefined,
    googleAndroidClientId: oauth.googleAndroidClientId as string | undefined,
    googleWebClientId: oauth.googleWebClientId as string | undefined,
    facebookAppId: oauth.facebookAppId as string | undefined,
  };
}

export function googleConfigured(): boolean {
  const c = readConfig();
  return !!(c.googleIosClientId || c.googleAndroidClientId || c.googleWebClientId);
}

export function facebookConfigured(): boolean {
  return !!readConfig().facebookAppId;
}

/**
 * Runs the Google OAuth flow in the system browser, exchanges the returned id_token with the LVSS
 * backend, and resolves with the app's own JWT + refresh token pair. Throws on cancel or error —
 * the login screen catches and shows a message.
 *
 * Google's iOS OAuth 2.0 client only supports the authorization-code + PKCE flow, not the
 * response_type=id_token shortcut the web client accepts. We ask for a code, then exchange it
 * for tokens client-side (iOS clients are public — no client secret required). The resulting
 * id_token is what the backend actually validates.
 *
 * The redirect URI must use the reversed-client-ID scheme Google hands out at
 * credential-creation time (also registered in Info.plist via CFBundleURLTypes in app.json) —
 * derived from the configured client ID here so the two stay in sync.
 */
export async function signInWithGoogle(): Promise<TokenResponse> {
  const cfg = readConfig();
  const clientId = cfg.googleIosClientId || cfg.googleWebClientId || cfg.googleAndroidClientId;
  if (!clientId) throw new Error('google-not-configured');

  const reversed = 'com.googleusercontent.apps.' + clientId.replace(/\.apps\.googleusercontent\.com$/, '');
  const redirectUri = `${reversed}:/oauth2redirect`;

  const discovery = await AuthSession.fetchDiscoveryAsync('https://accounts.google.com');

  const request = new AuthSession.AuthRequest({
    clientId,
    redirectUri,
    responseType: AuthSession.ResponseType.Code,
    scopes: ['openid', 'email', 'profile'],
    // prompt=select_account forces Google's account chooser every time even when the device is
    // already signed in to a Google account — critical for users with multiple accounts.
    extraParams: { prompt: 'select_account' },
    usePKCE: true,
  });

  // preferEphemeralSession: true tells ASWebAuthenticationSession on iOS not to share cookies
  // with Safari. Combined with prompt=select_account, this guarantees the account picker shows
  // every time — otherwise a phone signed in to a single Google account in Safari can bypass
  // the picker even with prompt=select_account, silently defaulting to the wrong Gmail.
  const result = await request.promptAsync(discovery, { preferEphemeralSession: true });
  if (result.type !== 'success') throw new Error(result.type);
  const code = result.params.code;
  if (!code) throw new Error('no-code');
  const codeVerifier = request.codeVerifier;
  if (!codeVerifier) throw new Error('no-code-verifier');

  // Exchange the auth code for id_token + access_token. iOS clients are public — no client
  // secret is sent; PKCE proves this is the same client that started the flow.
  const tokenResult = await AuthSession.exchangeCodeAsync(
    {
      clientId,
      code,
      redirectUri,
      extraParams: { code_verifier: codeVerifier },
    },
    discovery,
  );
  const idToken = (tokenResult as { idToken?: string }).idToken;
  if (!idToken) throw new Error('no-id-token');

  const { data } = await api.post<TokenResponse>('/mobile/auth/google', { token: idToken });
  return data;
}

/**
 * Runs Facebook OAuth in the system browser, forwards the access token to the LVSS backend, and
 * resolves with the app's tokens. Uses the fb<APPID>://authorize redirect scheme registered on
 * the Facebook Developer console when the iOS platform was added.
 */
export async function signInWithFacebook(): Promise<TokenResponse> {
  const cfg = readConfig();
  if (!cfg.facebookAppId) throw new Error('facebook-not-configured');

  const redirectUri = `fb${cfg.facebookAppId}://authorize`;
  const discovery = {
    authorizationEndpoint: 'https://www.facebook.com/v18.0/dialog/oauth',
    tokenEndpoint: 'https://graph.facebook.com/v18.0/oauth/access_token',
  };

  const request = new AuthSession.AuthRequest({
    clientId: cfg.facebookAppId,
    redirectUri,
    responseType: AuthSession.ResponseType.Token,
    scopes: ['email', 'public_profile'],
    usePKCE: false,
  });

  const result = await request.promptAsync(discovery, { preferEphemeralSession: true });
  if (result.type !== 'success') throw new Error(result.type);
  const accessToken = result.params.access_token;
  if (!accessToken) throw new Error('no-access-token');

  const { data } = await api.post<TokenResponse>('/mobile/auth/facebook', { token: accessToken });
  return data;
}
