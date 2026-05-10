# Codebase map (AI / developer reference)

Dense reference for **this repository’s implemented behavior**. For product intent and golf-domain detail, see `ARCHITECTURE.md` — but **data storage and some deployment details differ** (noted below).

---

## 1. Truth vs `ARCHITECTURE.md`

| Topic | `ARCHITECTURE.md` (plan) | This repo (implemented) |
|--------|---------------------------|-------------------------|
| Database | Azure SQL Database | Azure SQL Database (matches plan) |
| Migrations | EF migrations against SQL | EF Core migrations under `src/GolfLeague.Infrastructure/Migrations/`; `MigrateAsync()` runs at Function host startup |
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
- **Identity**: ASP.NET Core Identity backs the `AppUser` entity (Guid PK). `AppDbContext` extends `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`. Roles: `admin` / `scorer` / `player`, stored on `AppUser.Role` (single role per user).
- **JWT**: self-issued HS256 tokens. `Program.cs` validates with `JWT_SIGNING_KEY`. Issuer/audience = `golf-league-api`. Access tokens: 1h. MFA-challenge tokens: 5min. Refresh tokens: 14d, hashed at rest in `RefreshTokens`, rotated on every refresh.
- **Auth endpoints**: `Functions/AuthFunctions.cs` — `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/current`. Social: `Functions/ExternalAuthFunctions.cs`. MFA: `Functions/MfaFunctions.cs` (TOTP) and `Functions/PasskeyFunctions.cs` (WebAuthn / FIDO2).
- **Auth services**: `Infrastructure/Auth/AuthService.cs`, `JwtTokenService.cs`, `MfaService.cs`, `ExternalAuthService.cs`, `PasskeyService.cs`.
- **Per-request auth**: `Middleware/AuthMiddleware.cs` runs JWT bearer validation so `HttpRequest.HttpContext.User` is set with `role` and `NameIdentifier` claims.
- **Role checks**: `Helpers/HttpRequestExtensions.cs` — `RequireRole`, `GetUserId` (returns `AppUser.Id` as a Guid string), `TryDeserializeAsync`.
- **Admin bootstrap**: in `Program.cs` `EnsureDatabaseInitializedAsync`, if no admin exists and `ADMIN_BOOTSTRAP_EMAIL` is set, an admin AppUser is created with no password (requires password-reset flow on first sign-in).

### Azure SQL

- **`Infrastructure/DependencyInjection.cs`**: registers `AppDbContext` with `UseSqlServer(SQL_CONNECTION_STRING)`; enables retry-on-failure (6 retries, 30s max delay) for transient Azure SQL faults; sets command timeout to 60s for Serverless cold-start; defaults `QueryTrackingBehavior` to `NoTracking`.
- **`Program.cs` `EnsureDatabaseInitializedAsync`**: opens a startup scope and calls `MigrateAsync()` via the configured execution strategy, then `SeedActiveSeasonAsync`.
- **Auth**: production uses `Authentication=Active Directory Default` in the connection string — the Function App's system-assigned managed identity is the SQL server's AAD admin (set in `infra/modules/sql.bicep`), so it has full DDL + DML rights and no SQL password is stored anywhere. Locally the same setting falls through to `az login` / Visual Studio credentials.
- **Environment variables**: `SQL_CONNECTION_STRING` (Azure SQL ADO.NET connection string with `Authentication=Active Directory Default`), `BLOB_STORAGE_ACCOUNT` (player photos, future use).
- **Migrations**: `dotnet ef migrations add <Name> -p src/GolfLeague.Infrastructure -s src/GolfLeague.Functions`. Generated under `src/GolfLeague.Infrastructure/Migrations/`. Applied automatically at Function host startup.

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
- **API**: `src/lib/api.ts` — Axios instance; base URL `import.meta.env.VITE_API_BASE_URL ?? '/api/v1'`; attaches access token from `lib/auth.ts`; 401 → refresh → retry → redirect to `/login`.
- **Auth**: `src/lib/auth.ts` (token storage + auth API calls), `src/hooks/useAuth.ts` (login/logout + bootstrap from stored token), `src/store/authStore.ts` (Zustand user state).
- **Login pages**: `/login`, `/register`, `/auth/callback` (social OAuth return), `/auth/mfa` (admin TOTP step).
- **Types**: `src/types/api.ts` (and hooks under `src/hooks/`, `src/hooks/admin/`).
- **SWA**: `web/public/staticwebapp.config.json` — SPA fallback to `index.html`.

---

## 6. Mobile app (`mobile/`)

- **API base**: `lib/api/api_client.dart` (`apiClientProvider` exposes a Dio configured against `_baseUrl`).
- **Auth**: `lib/auth/auth_service.dart` (email+password + Google/Facebook via `flutter_web_auth_2` + TOTP exchange), `lib/auth/auth_providers.dart` (`authServiceProvider`, `myStatusProvider`).
- **Tokens**: stored via `flutter_secure_storage` (`access_token`, `refresh_token`). Social redirect: `com.golfleague.app://auth`.
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
| Change DB schema | `GolfLeague.Infrastructure/Data/AppDbContext.cs` + entity under `GolfLeague.Domain/Entities/`; then `dotnet ef migrations add <Name>` |
| Apply migrations / seed | `Program.cs` startup (`EnsureDatabaseInitializedAsync`) |
| Web API consumer | `web/src/lib/api.ts`, then hooks/pages |
| Mobile API consumer | `mobile/lib/core/config.dart`, repository impl under `features/*/data/`; admin bulk calls `features/admin/data/admin_league_service.dart` |
| Authorization rules | `Program.cs` policies + `HttpRequestExtensions.RequireRole` in each function |
