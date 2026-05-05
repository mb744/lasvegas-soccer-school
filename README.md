# Las Vegas Soccer School

Bilingual (English / Español) registration system for Las Vegas Soccer School.

## What it does

1. **Parent** lands on the marketing page (typically from a Facebook ad), clicks **Start your registration**.
2. They sign up — with **Google**, **Facebook**, or email + password.
3. They register player(s) for the active season: parent info, per-player size/grade, and a fresh per-player digital waiver each season.
4. **Admin** can pre-empt by sending a registration link to a specific email or phone (tracked through `Sent → Account created → Registered`), see all registrations, edit, delete, and download signed waiver PDFs.

The same parent account can register the same kid each season; sizes, grade, and waiver are collected fresh each time.

## Stack

| Layer | Tech |
|---|---|
| Frontend | React 19 + Vite + TypeScript + Tailwind v4 + react-router + react-i18next + react-hook-form + zod |
| Backend | ASP.NET Core 10 Web API + EF Core + ASP.NET Identity (cookie auth) + Google/Facebook OAuth + QuestPDF |
| Database | SQL Server LocalDB (dev) / Azure SQL (prod) |
| Notifications | Azure Communication Services (email + SMS) for the admin "send registration link" feature |

## Repo layout

```
LasVegasSoccerSchool/
├── backend/
│   └── SoccerSchool.Api/        # ASP.NET Core Web API
│       ├── Auth/                # Roles constants
│       ├── Controllers/         # Auth, Players, Registrations, Outreach
│       ├── Data/                # EF Core DbContext + migrations
│       ├── Domain/              # ApplicationUser, ParentAccount, Player, Registration,
│       │                        # RegistrationPlayer, Outreach, enums
│       ├── Dtos/                # Request / response DTOs
│       ├── Options/             # Strongly-typed appsettings
│       └── Services/            # Outreach sender (ACS email + SMS), waiver PDF
├── frontend/                    # Vite React app
│   └── src/
│       ├── api/                 # Axios client (cookie auth) + types
│       ├── auth/                # AuthContext + RequireAuth route guard
│       ├── components/          # Layout, LanguageToggle, SignaturePad
│       ├── i18n/                # en.ts, es.ts, init
│       └── pages/               # Landing, Login, Signup, Register, Admin
├── infra/                       # Bicep modules (Container Apps, SQL, Log Analytics, MI)
└── LasVegasSoccerSchool.slnx
```

## Running locally

### Prerequisites
- .NET 10 SDK
- Node 20+
- SQL Server LocalDB (`sqllocaldb info` should show `MSSQLLocalDB`)

### One-time setup
```powershell
# Backend dependencies + database
cd backend\SoccerSchool.Api
dotnet restore
dotnet ef database update
```

### Run (two terminals)
```powershell
# Terminal 1 — backend
cd backend\SoccerSchool.Api
dotnet run --launch-profile http
# API on http://localhost:5282

# Terminal 2 — frontend
cd frontend
npm install
npm run dev
# UI on http://localhost:5173
```

### First admin (local)

To use `/admin` locally, set the bootstrap admin in `appsettings.Development.json` (or via `dotnet user-secrets`):

```jsonc
{
  "App": {
    "Admin": { "Email": "you@example.com", "Password": "Local-pwd-8+chars" }
  }
}
```

On startup the API ensures that user exists and grants the `Admin` role. After the first run you can change the password via any normal flow; the bootstrap section only creates the user — it doesn't reset the password if the user already exists.

### Social login locally (optional)

Email + password works without any extra config. To exercise Google / Facebook locally:

```jsonc
{
  "App": {
    "OAuth": {
      "Google":   { "ClientId": "xxx.apps.googleusercontent.com", "ClientSecret": "GOCSPX-..." },
      "Facebook": { "AppId": "...", "AppSecret": "..." }
    }
  }
}
```

When registering the OAuth apps, add these as authorized redirect URIs:
- Google:   `http://localhost:5173/signin-google`
- Facebook: `http://localhost:5173/signin-facebook`

(Vite proxies `/api/auth/external/*` and `/signin-*` callbacks to the backend on `:5282`.)

## Configuration

`backend/SoccerSchool.Api/appsettings.json`:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=LasVegasSoccerSchool;..."
  },
  "App": {
    "PublicBaseUrl": "http://localhost:5173",   // base for outreach links
    "ActiveSeason":  "2026/27",                 // stamped on every new Registration
    "Cors":  { "AllowedOrigins": [ "http://localhost:5173" ] },
    "Admin": { "Email": "", "Password": "" },   // bootstrap admin (see "First admin")
    "OAuth": {
      "Google":   { "ClientId": "", "ClientSecret": "" },
      "Facebook": { "AppId": "",    "AppSecret": "" }
    }
  },
  "Acs": {
    "ConnectionString": "",
    "EmailFromAddress": "",
    "SmsFromNumber": ""
  }
}
```

If `Acs:*` is empty the admin "send registration link" still records an outreach row but its status will be `Failed` with a message — useful for tracking outreach manually.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | `/api/auth/signup`               | Public |
| POST | `/api/auth/login`                | Public |
| POST | `/api/auth/logout`               | Authenticated |
| GET  | `/api/auth/me`                   | Authenticated |
| GET  | `/api/auth/external/{provider}`  | Public — kicks off Google/Facebook OAuth |
| GET  | `/api/auth/external/callback`    | Public — OAuth callback |
| GET  | `/api/players`                   | Authenticated (parent) |
| POST | `/api/players`                   | Authenticated (parent) |
| PUT  | `/api/players/{id}`              | Authenticated (parent) |
| DELETE | `/api/players/{id}`            | Authenticated (parent) |
| POST | `/api/registrations`             | Authenticated (parent) |
| GET  | `/api/registrations/mine`        | Authenticated (parent) |
| GET  | `/api/registrations/{id}`        | Owner or Admin |
| GET  | `/api/registrations/{id}/waivers.pdf` | Owner or Admin (combined: one waiver per player) |
| GET  | `/api/registrations/{id}/players/{rpId}/waiver.pdf` | Owner or Admin |
| GET  | `/api/registrations`             | Admin |
| DELETE | `/api/registrations/{id}`      | Admin |
| POST | `/api/outreach`                  | Admin |
| GET  | `/api/outreach`                  | Admin |
| POST | `/api/outreach/{id}/resend`      | Admin |
| DELETE | `/api/outreach/{id}`           | Admin |

Auth is cookie-based (Identity application cookie `lvss.auth`). Admin endpoints require the `Admin` role.

## Waiver

One waiver is generated **per player per season**. The form prepopulates Participant Name (player), Parent/Guardian Name, Phone, and Email from the parent's section — all fields are editable. Team Name is optional.

Each waiver is **digitally signed** on a canvas signature pad (mouse / trackpad / finger) and stored as a base64 PNG on the corresponding `RegistrationPlayer` row. The signed PDF embeds that signature image alongside the timestamp.

Both English and Spanish versions of the full template (Assumption of Risk, Waiver of Liability, Medical Authorization, Media Release, Rules Acknowledgment) are baked into:
- Frontend (rendered in the form): [`frontend/src/i18n/en.ts`](frontend/src/i18n/en.ts) and [`es.ts`](frontend/src/i18n/es.ts) under `register.waiver.*`
- Backend (rendered in the PDF): [`backend/SoccerSchool.Api/Services/WaiverPdfGenerator.cs`](backend/SoccerSchool.Api/Services/WaiverPdfGenerator.cs) — `WaiverText.English` / `WaiverText.Spanish` records.

Edit those two locations together if you tweak the wording.

## Other bilingual content

- Email subject/body and SMS body for the outreach link: [`Services/OutreachSender.cs`](backend/SoccerSchool.Api/Services/OutreachSender.cs) — `BuildEmailContent` / `BuildSmsBody`

## Setting up social login (Google + Facebook)

Both providers require you to register an OAuth app in their developer console and paste the credentials into the deployment.

### Google

1. <https://console.cloud.google.com/apis/credentials> → **Create Credentials → OAuth client ID** → Web application.
2. Authorized redirect URIs:
   - `https://<your-app-fqdn>/signin-google`     (production)
   - `http://localhost:5173/signin-google`       (dev)
3. Copy the **Client ID** and **Client secret**.

### Facebook

1. <https://developers.facebook.com/apps> → **Create App** → "Authenticate and request data from users with Facebook Login" → Consumer.
2. Add **Facebook Login** product → Settings → Valid OAuth Redirect URIs:
   - `https://<your-app-fqdn>/signin-facebook`
   - `http://localhost:5173/signin-facebook`
3. App Settings → Basic → copy **App ID** and **App secret**.

The Bicep deploy outputs both redirect URIs (`googleRedirectUri`, `facebookRedirectUri`) — paste them straight into the consoles.

## Deploying to Azure (containerized)

This repo deploys as a **single Docker container** to **Azure Container Apps** (scale-to-zero), with **Azure SQL serverless** for data and a **user-assigned managed identity** for passwordless DB auth. Images are published to **GHCR** via GitHub Actions, and infra is provisioned via **Bicep**.

```
┌──────────────────────┐      ┌─────────────────────┐
│  GitHub Actions      │─────▶│  GHCR (ghcr.io)     │
│  build + push image  │      │  ghcr.io/owner/repo │
└──────────────────────┘      └─────────────────────┘
        │
        │ az deployment group create ./infra/main.bicep
        ▼
┌──────────────────────────────────────────────────┐
│  Resource group: soccer-school-west              │
│                                                  │
│   Container Apps Env  ──▶  Log Analytics         │
│        │                                         │
│        ▼                                         │
│   Container App (lvss-app)  ──▶  Managed         │
│        │                          Identity       │
│        │ Active Directory Default                │
│        ▼                                         │
│   Azure SQL Server  ──▶  Database (serverless)   │
└──────────────────────────────────────────────────┘
```

### One-time setup

```powershell
# 1. Log in to the right Azure tenant
az login --tenant <your-tenant-id>

# 2. Configure OIDC: managed identity, federated credential trusting the GitHub repo,
#    Contributor on the resource group. Idempotent.
pwsh ./scripts/setup-azure-oidc.ps1 `
  -SubscriptionId <sub-id> `
  -TenantId       <tenant-id> `
  -ResourceGroup  soccer-school-west `
  -Location       westus `
  -GithubRepo     <owner>/<repo>

# 3. Set GitHub repo variables + secrets. Optional fields can be omitted; the deploy
#    will still run but will skip any provider whose values aren't set.
pwsh ./scripts/set-github-vars.ps1 `
  -Repo                   <owner>/<repo> `
  -ClientId               <from setup-oidc output> `
  -TenantId               <from setup-oidc output> `
  -SubscriptionId         <from setup-oidc output> `
  -ResourceGroup          soccer-school-west `
  -SqlAdminObjectId       <from setup-oidc output> `
  -SqlAdminLogin          "<your UPN>" `
  -AdminBootstrapEmail    "you@example.com" `
  -AdminBootstrapPassword "Some-strong-temp-pwd-1!" `
  -GoogleClientId         "xxx.apps.googleusercontent.com" `
  -GoogleClientSecret     "GOCSPX-..." `
  -FacebookAppId          "1234567890" `
  -FacebookAppSecret      "abcdef1234"
```

### Deploy

Push to `main`. The `Deploy` workflow:
1. Builds the container image (multi-stage: Vite → .NET publish → wwwroot copy)
2. Pushes to `ghcr.io/<owner>/<repo>:sha-<short>` and `:latest`
3. Flips the GHCR package to **public** so Container Apps can pull anonymously
4. Logs into Azure via OIDC (no client secret stored anywhere)
5. Runs `az deployment group create` with the new image tag + OAuth credentials + admin bootstrap

The deploy job's "Show deployment outputs" step prints the app URL **and the Google / Facebook redirect URIs** to paste into the developer consoles on first deploy.

### One-time post-deploy: grant the managed identity DB access

The Bicep makes **you** the SQL Entra admin (so you can connect via SSMS / Azure Data Studio for break-glass). The Container App's managed identity needs to be added to the database explicitly:

```powershell
pwsh ./scripts/grant-mi-db-access.ps1 `
  -SqlServerFqdn       lvss-sql-<suffix>.database.windows.net `
  -ManagedIdentityName lvss-id-<suffix>
```

After this, EF Core migrations run on the next container start and the API is fully operational.

### Binding a custom domain

The Bicep module accepts a `customDomain` param (wired in via the `CUSTOM_DOMAIN` GitHub repo variable). When set, the app's `PublicBaseUrl`, OAuth `signin-google` / `signin-facebook` redirect URIs, and the outreach links it sends all use the custom hostname instead of the auto-generated `*.azurecontainerapps.io` FQDN.

The hostname binding itself is a one-time, manual step done after DNS records are in place — it's intentionally not in Bicep because the managed certificate provisioning depends on DNS propagation and would make the deploy flaky.

```powershell
# 1. Set the GitHub repo variable so the next deploy bakes the custom host
#    into env vars (PublicBaseUrl + OAuth redirect URIs).
gh variable set CUSTOM_DOMAIN --body "registration.lasvegassoccerschool.org" --repo <owner>/<repo>

# 2. Push to main (or workflow_dispatch). The "Show deployment outputs" step
#    prints the values you need:
#    - Container App default FQDN  (e.g. lvss-app.<env-id>.<region>.azurecontainerapps.io)
#    - Custom Domain Verification ID  (a 64-char string)
```

In **GoDaddy DNS** for `lasvegassoccerschool.org`, add two records:

| Type  | Host | Value |
|---|---|---|
| CNAME | `registration` | the *default* Container App FQDN from the deploy output |
| TXT   | `asuid.registration` | the *Custom Domain Verification ID* from the deploy output |

Wait ~5 minutes for DNS to propagate, then bind:

```powershell
$RG  = 'soccer-school-west'
$APP = 'lvss-app'
$DOMAIN = 'registration.lasvegassoccerschool.org'

az containerapp hostname add  -g $RG -n $APP --hostname $DOMAIN
az containerapp hostname bind -g $RG -n $APP --hostname $DOMAIN --validation-method CNAME
# Cert provisioning takes a few minutes. Check status:
az containerapp hostname list -g $RG -n $APP -o table
```

Once status shows `Succeeded`, hit `https://registration.lasvegassoccerschool.org` — should serve the app over the managed cert.

Finally, update **both OAuth consoles** to add the production redirect URIs (keep the localhost ones for dev):

- Google → Authorized redirect URIs: `https://registration.lasvegassoccerschool.org/signin-google`
- Google → Authorized JavaScript origins: `https://registration.lasvegassoccerschool.org`
- Facebook → Valid OAuth Redirect URIs: `https://registration.lasvegassoccerschool.org/signin-facebook`
- Facebook → App Domains: `lasvegassoccerschool.org`

### Adding ACS (email + SMS outreach) later

When you're ready, add an ACS resource (Bicep module or portal), then set the Container App's env vars:

```powershell
$RG = 'soccer-school-west'
$APP = (az containerapp list -g $RG --query "[0].name" -o tsv)
az containerapp secret set --resource-group $RG --name $APP --secrets `
  acs-conn="endpoint=https://...;accesskey=..."
az containerapp update --resource-group $RG --name $APP `
  --set-env-vars `
  Acs__ConnectionString=secretref:acs-conn `
  Acs__EmailFromAddress=donotreply@yourdomain.com `
  Acs__SmsFromNumber=+17025551212
```

### Smoke test

After deploy, open `https://<container-app-fqdn>/` and either:
- Sign up as a parent and submit a test registration, or
- Log in with the bootstrap admin account and exercise `/admin` (send a test outreach link to your own email).

### Costs at idle (rough)

| Resource | Cost |
|---|---|
| Container Apps Consumption (scale-to-zero) | $0 idle |
| Azure SQL serverless (paid; autopaused) | ~$0–5/mo at low traffic |
| Log Analytics (PerGB2018, low volume) | ~$0–2/mo |
| Managed Identity, Bicep, GHCR (public) | $0 |

> The Azure SQL free serverless offer isn't available in `westus`. To get the $0 SQL tier, redeploy to `westus2` or `westus3` and add `useFreeLimit: true` back to [`infra/modules/sql.bicep`](infra/modules/sql.bicep).

## Roadmap / not yet implemented

- Replace placeholder waiver wording with official text (sample pending).
- Replace placeholder landing copy with brand content.
- Email/SMS delivery webhooks (ACS supports them; the schema already stores `StatusMessage`).
- CSV export of registrations.
- Self-service password reset (Identity supports it; UI not built).
