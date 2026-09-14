# LV Soccer School — Mobile (parents)

React Native + Expo app for parents: see scheduled games & practices, confirm attendance per child,
and chat in admin-created groups. Talks to the existing ASP.NET backend at
`registration.lasvegassoccerschool.org` (REST `/api/mobile/*` + SignalR `/hubs/chat`, JWT bearer auth).

## Stack
- **Expo SDK 52** + **Expo Router** (file-based navigation in `app/`)
- **TanStack Query** for server state, **axios** with silent token refresh (`src/api/client.ts`)
- **@microsoft/signalr** for realtime chat (`src/chat/signalr.ts`)
- **expo-secure-store** for tokens, **expo-notifications** for push
- **i18next** EN/ES (`src/i18n/`), device-locale driven

## Project layout
```
app/                     # Expo Router screens
  _layout.tsx            # providers (Query, Auth) + push-tap routing
  index.tsx              # auth gate → tabs or login
  login.tsx
  (tabs)/                # schedule · chat · profile (auth-guarded; opens chat socket)
  chat/[groupId].tsx     # chat thread (live + history + send)
src/
  api/                   # client (interceptors), endpoints, types (mirror backend MobileDtos)
  auth/                  # AuthContext + SecureStore token storage
  chat/                  # SignalR connection
  push/                  # Expo push registration
  i18n/                  # en / es
  config.ts theme.ts format.ts
```

## Run locally
```bash
cd mobile
npm install
# Align native dep versions to the installed Expo SDK (recommended after first install):
npx expo install --fix

# Point at a locally-running backend (default is production). Either edit extra.apiBaseUrl in
# app.json, or export the public env var the config reads:
#   (config.ts reads Constants.expoConfig.extra.apiBaseUrl)
npx expo start
```
Open in **Expo Go** (Android) for quick iteration. iOS push + secure-store behave best in a
**dev client** (`npx expo run:ios` / EAS development build) rather than Expo Go.

> The backend must have `App:Jwt:SigningKey` set (already in `appsettings.Development.json` for dev),
> otherwise `/api/mobile/*` returns 401 — the mobile auth scheme only registers when a key is present.

## Store deployment (EAS)
1. Create an Expo account + `eas init` to get a real `projectId` (replace the placeholder in
   `app.json` → `extra.eas.projectId`).
2. Add app icons/splash under `assets/` and reference them back in `app.json` (`icon`,
   `splash.image`, `android.adaptiveIcon.foregroundImage`). 1024×1024 icon, 1284×2778 splash.
3. Fill the placeholders in `eas.json` → `submit.production` (Apple ID / ASC app id / team id; Play
   service-account JSON).
4. Builds: `eas build -p ios --profile production` / `eas build -p android --profile production`.
5. Submit: `eas submit -p ios --profile production` / `eas submit -p android --profile production`.
6. First release: use **TestFlight** (iOS) and Play **internal testing** before public submission.
   Provide reviewers a demo parent login. Privacy/data-deletion: reuse the web `/privacy` and
   `/data-deletion` pages.

## Notes
- v1 is **email/password only** (no social login) to keep store review simple; Apple requires Apple
  Sign-In only if another social provider is present.
- Types in `src/api/types.ts` mirror `backend/.../Dtos/MobileDtos.cs` — keep them in sync.
