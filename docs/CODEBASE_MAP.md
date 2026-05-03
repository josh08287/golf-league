# Codebase map (AI / developer reference)

Dense reference for **this repository’s implemented behavior**. For product intent and golf-domain detail, see `ARCHITECTURE.md` — but **data storage and some deployment details differ** (noted below).

---

## 1. Truth vs `ARCHITECTURE.md`

| Topic | `ARCHITECTURE.md` (plan) | This repo (implemented) |
|--------|---------------------------|-------------------------|
| Database | Azure SQL Database | **SQLite file** persisted in **Azure Blob Storage** |
| Migrations | EF migrations against SQL | Startup runs **CREATE TABLE IF NOT EXISTS** from model script + blob sync; local dev uses `AppDbContextFactory` |
| Default git branch | `main` in narrative | CI uses **`master`** |

---

## 2. Repository layout

```
src/
  GolfLeague.Functions/     HTTP triggers, Program.cs, AuthMiddleware
  GolfLeague.Domain/        Entities, enums, repository interfaces
  GolfLeague.Application/   MediatR commands/queries, DTOs, AuditBehavior
  GolfLeague.Infrastructure/ DependencyInjection, EF AppDbContext, BlobSyncedDbContext, repositories
web/                        Vite React app (src/pages, hooks, lib/api.ts)
mobile/lib/                 Flutter app by feature + core/
infra/                      main.bicep, modules (storage, functions, keyvault, …)
tests/GolfLeague.Functions.Tests/
```

---

## 3. Backend runtime

- **.NET 9** isolated worker Functions (`GolfLeague.Functions.csproj`).
- **MediatR** registers handlers from `GolfLeague.Application`; **`AuditBehavior`** wraps auditable commands.
- **JWT**: `Program.cs` configures `JwtBearer` with `https://login.microsoftonline.com/{ENTRA_TENANT_ID}/v2.0` and audience `ENTRA_CLIENT_ID`.
- **Per-request auth**: `Middleware/AuthMiddleware.cs` runs authentication so `HttpRequest.HttpContext.User` is set.
- **Role checks**: `Helpers/HttpRequestExtensions.cs` — `RequireRole`, `GetUserId`, `TryDeserializeAsync`.

### SQLite + Azure Blob

- **`Infrastructure/DependencyInjection.cs`**: builds `BlobServiceClient` / `BlobContainerClient` with `DefaultAzureCredential`; registers `AppDbContext` pointing SQLite at a temp path.
- **`Data/BlobSyncedDbContext.cs`**: after `SaveChanges`, copies DB to a temp file and uploads to blob (semaphore prevents concurrent uploads).
- **`Program.cs` `EnsureDatabaseInitializedAsync`**: download blob if newer → `EnsureAllTablesExistAsync` → `SeedActiveSeasonAsync` → upload.
- **Environment variables**: `BLOB_STORAGE_ACCOUNT`, `SQLITE_BLOB_CONTAINER`, `SQLITE_BLOB_NAME` (see `local.settings.json` for local names).

### HTTP routing

- **`host.json`**: `"routePrefix": "api"`.
- Function `Route = "v1/players"` → **`/api/v1/players`**.

---

## 4. HTTP API surface (from Function routes)

All prefixed with **`/api`**. Public reads use `AuthorizationLevel.Anonymous`; writes still call `RequireRole` inside the function.

| Method | Route | Notes |
|--------|-------|--------|
| GET | `v1/players`, `v1/players/{id}`, `v1/players/{id}/handicap-history` | Paged list on query params |
| POST | `v1/players` | admin |
| PUT/PATCH | `v1/players/{id}` | admin |
| DELETE | `v1/players/{id}` | duplicate route entries in source — check file for behavior |
| POST | `v1/players/{id}/deactivate` | admin |
| POST/PUT | `v1/players/{id}/handicap` | admin |
| GET | `v1/seasons` | |
| POST | `v1/seasons` | admin |
| POST | `v1/seasons/{id}/activate` | admin |
| DELETE | `v1/seasons/{id}` | admin |
| GET | `v1/flights`, `v1/flights/{id}/standings` | |
| POST | `v1/flights` | admin |
| DELETE | `v1/flights/{id}` | admin |
| GET | `v1/rounds`, `v1/rounds/{id}`, `v1/rounds/{id}/participants`, `v1/rounds/{id}/scorecards`, `v1/rounds/{id}/scorecards/{playerId}`, `v1/rounds/{id}/scores/{playerId}` | |
| POST | `v1/rounds` | admin |
| DELETE | `v1/rounds/{id}` | admin |
| POST | `v1/rounds/{id}/finalize` | admin |
| PUT | `v1/rounds/{id}/scores/{playerId}/holes` | scorer or admin |
| GET | `v1/courses`, `v1/courses/{id}` | |
| POST | `v1/courses` | admin |
| PUT | `v1/courses/{id}/holes` | admin |
| DELETE | `v1/courses/{id}` | admin |
| GET | `v1/admin/audit-log` | admin |
| GET | `health` | **`/api/health`** |

---

## 5. Web app (`web/`)

- **Entry**: `src/main.tsx`, routes in `src/App.tsx`, admin routes from `src/routes/adminRoutes`.
- **API**: `src/lib/api.ts` — Axios instance; base URL `import.meta.env.VITE_API_BASE_URL ?? '/api/v1'`; MSAL silent token on requests; 401 → refresh → retry → login redirect.
- **Auth config**: `src/lib/msalConfig.ts` (env: `VITE_ENTRA_CLIENT_ID`, `VITE_REDIRECT_URI`; API scope embedded — align with Entra app registration).
- **Types**: `src/types/api.ts` (and hooks under `src/hooks/`, `src/hooks/admin/`).
- **SWA**: `web/public/staticwebapp.config.json` — SPA fallback to `index.html`.

---

## 6. Mobile app (`mobile/`)

- **API base**: `lib/core/config.dart` (`apiBaseUrl`, Entra `authority`, `clientId`, `redirectUri`, OIDC `scopes`).
- **HTTP**: `lib/core/network/dio_client.dart` (`tokenServiceProvider`, `dioClientProvider`), `auth_interceptor.dart`; tokens via `lib/core/auth/token_service.dart`.
- **Auth UI refresh**: `lib/core/auth/auth_tick.dart` (`authTickProvider`, `bumpAuthTick`) — watch this after login/logout so admin gate and profile update.
- **Offline**: Drift DB `lib/core/database/app_database.dart`; score sync `features/score_entry/data/sync_service.dart`.
- **Public features**: `features/dashboard`, `leaderboard`, `rounds`, `player_profile`, `score_entry` — each with `data/`, `domain/`, `presentation/` where applicable.
- **League admin (web parity)**: `features/admin/` — `data/admin_league_service.dart` (HTTP), screens under `presentation/`; GoRouter paths `/admin`, `/admin/players`, `/admin/players/:id`, `/admin/flights`, `/admin/rounds`, `/admin/courses`, `/admin/seasons`, `/admin/audit-log`, `/admin/settings`. Profile tab: Sign in, Sign out, admin shortcut when JWT has `admin` role.
- **Riverpod**: Hand-written `Provider` / `FutureProvider` / `NotifierProvider` (no `riverpod_generator` in this repo — avoids SDK `meta` pin conflicts). Feature `providers.dart` files import `dio_client.dart` for shared clients.
- **Stableford**: `lib/core/utils/stableford_calculator.dart` (+ tests in `test/core/utils/`).

---

## 7. Infrastructure & CI

- **Bicep** (`infra/main.bicep`): Application Insights, storage (photos + **database** container), Function App app settings including blob SQLite keys, Key Vault.
- **Deploy API**: `.github/workflows/deploy-api.yml` — `dotnet test`, `dotnet publish` `GolfLeague.Functions`, deploy with `Azure/functions-action` (app name in workflow).
- **Deploy Web**: `.github/workflows/deploy-web.yml` — `npm ci` / `npm run build` in `web/`, deploy with `Azure/static-web-apps-deploy`, `skip_app_build: true`, output `web/dist`.

---

## 8. Quick file index for common tasks

| Task | Start here |
|------|------------|
| Add/change REST endpoint | `GolfLeague.Functions/Functions/*.cs` + Application command/query + optional repository |
| Change DB schema | `GolfLeague.Infrastructure/Data/AppDbContext.cs` + entity under `GolfLeague.Domain/Entities/` |
| Change blob/sync behavior | `BlobSyncedDbContext.cs`, `Program.cs` startup |
| Web API consumer | `web/src/lib/api.ts`, then hooks/pages |
| Mobile API consumer | `mobile/lib/core/config.dart`, repository impl under `features/*/data/`; admin bulk calls `features/admin/data/admin_league_service.dart` |
| Authorization rules | `Program.cs` policies + `HttpRequestExtensions.RequireRole` in each function |
