# Las Vegas Soccer School

Bilingual (English / Español) registration system for Las Vegas Soccer School.

## What it does

1. **Admin** opens `/admin`, enters a parent's email or phone + language, clicks **Send invitation**.
2. App generates a unique tokenized link and emails / texts it via **Azure Communication Services**.
3. Parent clicks the link → bilingual landing page → registration form for parent + multiple players + electronic waiver consent.
4. On submit, the app stores the registration and a signed **PDF waiver** is generated on demand from `/admin`.

## Stack

| Layer | Tech |
|---|---|
| Frontend | React 19 + Vite + TypeScript + Tailwind v4 + react-router + react-i18next + react-hook-form + zod |
| Backend | ASP.NET Core 10 Web API + EF Core + QuestPDF |
| Database | SQL Server LocalDB (dev) / Azure SQL (prod) |
| Notifications | Azure Communication Services (email + SMS) |

## Repo layout

```
LasVegasSoccerSchool/
├── backend/
│   └── SoccerSchool.Api/        # ASP.NET Core Web API
│       ├── Auth/                # Admin API key middleware
│       ├── Controllers/         # InvitationsController, RegistrationsController
│       ├── Data/                # EF Core DbContext + migrations
│       ├── Domain/              # Invitation, Registration, Player, enums
│       ├── Dtos/                # Request / response DTOs
│       ├── Options/             # Strongly-typed appsettings
│       └── Services/            # Token gen, ACS sender, waiver PDF
├── frontend/                    # Vite React app
│   └── src/
│       ├── api/                 # Axios client + types
│       ├── components/          # Layout, LanguageToggle
│       ├── i18n/                # en.ts, es.ts, init
│       └── pages/               # LandingPage, RegisterPage, AdminPage
└── LasVegasSoccerSchool.sln
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

Then open <http://localhost:5173/admin> and enter the dev admin key from `appsettings.json`:
- Default: `dev-admin-key-change-me`
- Change for production!

## Configuration

`backend/SoccerSchool.Api/appsettings.json`:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=LasVegasSoccerSchool;..."
  },
  "App": {
    "PublicBaseUrl": "http://localhost:5173",       // base for invite links
    "AdminApiKey": "dev-admin-key-change-me",       // X-Admin-Key header value
    "Cors": { "AllowedOrigins": [ "http://localhost:5173" ] }
  },
  "Acs": {
    "ConnectionString": "",     // Azure Communication Services connection string
    "EmailFromAddress": "",     // verified sender, e.g. donotreply@yourdomain.com
    "SmsFromNumber": ""         // E.164 number provisioned in ACS, e.g. +17025551212
  }
}
```

If `Acs:ConnectionString` is empty, the API still creates the invitation and returns the link — the admin UI shows it for manual copy/paste. The status will be `Failed` with the message `ACS not configured`.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | `/api/invitations` | Admin |
| GET | `/api/invitations` | Admin |
| POST | `/api/invitations/{id}/resend` | Admin |
| GET | `/api/invitations/by-token/{token}` | Public |
| POST | `/api/registrations` | Public (token-gated) |
| GET | `/api/registrations` | Admin |
| GET | `/api/registrations/{id}` | Admin |
| GET | `/api/registrations/{id}/waivers.pdf` | Admin (combined: one waiver per player) |
| GET | `/api/registrations/{id}/players/{playerId}/waiver.pdf` | Admin (single player) |

Admin endpoints require header `X-Admin-Key: <AdminApiKey>`.

## Waiver

One waiver is generated **per player**. The form prepopulates Participant Name (player), Parent/Guardian Name, Phone, and Email from the parent's section — all fields are editable. Team Name is optional.

Each waiver is **digitally signed** on a canvas signature pad (mouse / trackpad / finger) and stored as a base64 PNG in the database. The signed PDF embeds that signature image alongside the timestamp.

Both English and Spanish versions of the full template (Assumption of Risk, Waiver of Liability, Medical Authorization, Media Release, Rules Acknowledgment) are baked into:
- Frontend (rendered in the form): [`frontend/src/i18n/en.ts`](frontend/src/i18n/en.ts) and [`es.ts`](frontend/src/i18n/es.ts) under `register.waiver.*`
- Backend (rendered in the PDF): [`backend/SoccerSchool.Api/Services/WaiverPdfGenerator.cs`](backend/SoccerSchool.Api/Services/WaiverPdfGenerator.cs) — `WaiverText.English` / `WaiverText.Spanish` records.

Edit those two locations together if you tweak the wording.

## Other bilingual content

- Email subject/body and SMS body for the invite link: [`Services/InviteSender.cs`](backend/SoccerSchool.Api/Services/InviteSender.cs) — `BuildEmailContent` / `BuildSmsBody`

## Deploying to Azure (containerized)

This repo deploys as a **single Docker container** to **Azure Container Apps** (scale-to-zero), with **Azure SQL serverless free tier** for data and a **user-assigned managed identity** for passwordless DB auth. Images are published to **GHCR** via GitHub Actions, and infra is provisioned via **Bicep**.

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
│   Azure SQL Server  ──▶  Database (free SL)      │
└──────────────────────────────────────────────────┘
```

### One-time setup

```powershell
# 1. Log in to the right Azure tenant (this is in tenant 8ad10099-..., not your default)
az login --tenant <your-tenant-id>

# 2. Configure OIDC: creates managed identity, federated credential trusting the GitHub repo,
#    grants Contributor on the resource group. Idempotent.
pwsh ./scripts/setup-azure-oidc.ps1 `
  -SubscriptionId <sub-id> `
  -TenantId       <tenant-id> `
  -ResourceGroup  soccer-school-west `
  -Location       westus `
  -GithubRepo     <owner>/<repo>

# Script prints the values you need. Set them as GitHub repo VARIABLES (not secrets — none are sensitive):
pwsh ./scripts/set-github-vars.ps1 `
  -Repo             <owner>/<repo> `
  -ClientId         <from output> `
  -TenantId         <from output> `
  -SubscriptionId   <from output> `
  -ResourceGroup    soccer-school-west `
  -SqlAdminObjectId <from output> `
  -SqlAdminLogin    "<your UPN>"
```

### Deploy

Push to `main`. The `Deploy` workflow:
1. Builds the container image (multi-stage: Vite → .NET publish → wwwroot copy)
2. Pushes to `ghcr.io/<owner>/<repo>:sha-<short>` and `:latest`
3. Flips the GHCR package to **public** so Container Apps can pull anonymously
4. Logs into Azure via OIDC (no client secret stored anywhere)
5. Runs `az deployment group create` with the new image tag

The deploy job's "Show deployment outputs" step prints the app URL.

### One-time post-deploy: grant the managed identity DB access

The Bicep makes **you** the SQL Entra admin (so you can connect via SSMS / Azure Data Studio for break-glass). The Container App's managed identity needs to be added to the database explicitly:

```powershell
pwsh ./scripts/grant-mi-db-access.ps1 `
  -SqlServerFqdn       lvss-sql-<suffix>.database.windows.net `
  -ManagedIdentityName lvss-id-<suffix>
```

Get the `<suffix>` from the deploy job output, or:
```powershell
az resource list -g soccer-school-west --resource-type Microsoft.Sql/servers --query "[0].name" -o tsv
az resource list -g soccer-school-west --resource-type Microsoft.ManagedIdentity/userAssignedIdentities --query "[?starts_with(name, 'lvss-id')].name" -o tsv
```

After this, EF Core migrations run on the next container start and the API is fully operational.

### Adding ACS (email + SMS invites) later

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

```powershell
$base = "https://<container-app-fqdn>"
$key  = (az containerapp secret show -g soccer-school-west -n lvss-app --secret-name admin-api-key --query value -o tsv)
$h = @{ "X-Admin-Key" = $key; "Content-Type" = "application/json" }
Invoke-RestMethod -Uri "$base/api/invitations" -Method Post -Headers $h `
  -Body '{"email":"you@yourdomain.com","language":0}'
```

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
- Strong admin auth (current X-Admin-Key is fine for one trusted admin; switch to Entra ID / OAuth if multiple admins are needed).
- Email/SMS delivery webhooks (ACS supports them; the schema already stores `StatusMessage`).
- CSV export of registrations.
