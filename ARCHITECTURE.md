# Golf League Manager — Architecture Plan

**Version 1.2 | April 2026**

> **Implementation (this repo):** The running API uses **Azure SQL Database (Serverless)** as described below, with EF Core migrations applied at Function host startup and the Function App's managed identity as the SQL server's Entra admin. HTTP routes, env vars, CI branch, and file layout are summarized in [`docs/CODEBASE_MAP.md`](docs/CODEBASE_MAP.md).

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Golf Domain Model & Scoring Logic](#2-golf-domain-model--scoring-logic)
3. [Backend — Azure Cloud](#3-backend--azure-cloud)
4. [Web Frontend](#4-web-frontend)
5. [Mobile Apps — Android & iOS](#5-mobile-apps--android--ios)
6. [Authentication & Authorization](#6-authentication--authorization)
7. [Database Schema](#7-database-schema)
8. [API Reference](#8-api-reference)
9. [Deployment Architecture](#9-deployment-architecture)
10. [Cost Estimate](#10-cost-estimate)

---

## 1. System Overview

### What It Does

A golf league management platform supporting:
- **Multiple flights** of players grouped by handicap range, each competing independently
- **Net Stableford scoring** with full handicap stroke allocation per hole
- **Admin web interface**: manage players, flights, handicaps, courses, and rounds
- **Public web interface**: live flight leaderboards and full round history
- **Android & iOS native apps**: mobile-optimized leaderboards, round details, and optional score entry

### Architecture at a Glance

```
┌────────────────────────────────────────────────────────────────┐
│                        Clients                                  │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────┐   │
│  │  Web App     │   │  Android App │   │    iOS App       │   │
│  │  React 18    │   │              │   │                  │   │
│  │  TypeScript  │   │  Flutter 3.x (Dart) — shared codebase   │ │
│  │  Azure SWA   │   │              │   │                  │   │
│  └──────┬───────┘   └──────┬───────┘   └────────┬─────────┘   │
└─────────┼──────────────────┼────────────────────┼─────────────┘
          │                  │                     │
          │         HTTPS / REST API (JWT Bearer)  │
          │                  │                     │
┌─────────▼──────────────────▼─────────────────────▼─────────────┐
│                     Azure Cloud                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │   Azure Functions (Consumption) — HTTP-triggered API      │  │
│  │   REST API at /api/v1/ + background jobs (same host)      │  │
│  └────────────────────────┬──────────────────────────────────┘  │
│                  ┌────────┴──────────┐                          │
│  ┌───────────────▼───┐  ┌───────────▼──────────────────────┐   │
│  │ Azure SQL Database │  │ Azure Static Web Apps (/api proxy)│   │
│  │ (Serverless)       │  │ (hosts web frontend + routes API) │   │
│  └────────────────────┘  └───────────────────────────────────┘  │
│  ┌─────────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ Entra External  │  │ Azure Key    │  │ Application      │   │
│  │ ID (Identity)   │  │ Vault        │  │ Insights         │   │
│  └─────────────────┘  └──────────────┘  └──────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Golf Domain Model & Scoring Logic

### 2.1 Stableford Scoring

The league uses **Net Stableford** — points awarded per hole based on the player's net score (gross strokes minus handicap strokes received) versus par. This levels the playing field across all handicap levels.

#### Points Per Hole

| Net Score vs Par | Result | Points |
|---|---|---|
| Net >= Par + 2 | Double bogey or worse | 0 |
| Net = Par + 1 | Bogey | 1 |
| Net = Par | Par | 2 |
| Net = Par - 1 | Birdie | 3 |
| Net = Par - 2 | Eagle | 4 |
| Net = Par - 3 | Albatross | 5 |
| Net <= Par - 4 | Condor+ | 6 |

Zero is the floor — a bad hole cannot push a player below zero.

```csharp
public static int CalculateStablefordPoints(int netStrokes, int holePar)
{
    int diff = netStrokes - holePar;
    return diff switch
    {
        <= -4 => 6,
        -3    => 5,
        -2    => 4,
        -1    => 3,
         0    => 2,
         1    => 1,
        _     => 0
    };
}
```

#### Course Handicap Calculation

```
CourseHandicap = ROUND(HandicapIndex x (SlopeRating / 113))
```

#### Handicap Stroke Allocation Per Hole

Each hole has a **Stroke Index (SI)** from 1 (hardest) to 18 (easiest). A player's strokes are allocated from the lowest SI upward:

```
StrokesOnHole = FLOOR(CourseHandicap / 18)
              + (1 IF hole.StrokeIndex <= (CourseHandicap MOD 18) ELSE 0)
```

Example: CourseHandicap = 14 receives 1 stroke on holes with SI 1-14, 0 strokes on SI 15-18.

#### Maximum Score Per Hole (Net Double Bogey)

```
MaxGrossScore = Par + 2 + HandicapStrokesOnHole
```

If a player reaches this score they pick up. The hole is recorded at the maximum and scores 0 Stableford points. This speeds play and prevents runaway bad holes from distorting handicaps.

#### Net Score Calculation

```
NetStrokes       = GrossStrokes - StrokesReceivedOnHole
StablefordPoints = max(0, Par + 2 - NetStrokes)
```

### 2.2 Season Leaderboard

Standings within a flight are ranked by **total Stableford points** across all verified, counted rounds in the season.

Tie-breaking order:
1. Higher total Stableford points
2. Higher average points per round
3. Better score in the most recent counted round
4. Count-back: back 9 (holes 10-18), then last 6 (13-18), then last 3 (16-18), then hole-by-hole from 18

**Best-N Rounds Mode:** The league can be configured so only the best N rounds count toward the season total. This lets players miss rounds without full penalty.

### 2.3 Handicap Recalculation

After a round is finalized, an Azure Function recalculates handicaps using the World Handicap System (WHS) algorithm:

1. **Score Differential** per round: `(GrossScore - CourseRating) x (113 / SlopeRating)`
2. Take the **best 8 of the last 20** score differentials
3. `NewHandicapIndex = AVG(best 8) x 0.96`
4. Cap: max 54.0, min +10.0

For leagues not using WHS, a simple adjustment is available:
- Points > 36: reduce handicap by `(points - 36) x 0.1`
- Points < 36: increase handicap by 0.1

Every handicap change is recorded in `HandicapHistory` for a full audit trail.

### 2.4 Flights

Flights group players by handicap range and compete **independently**. A player belongs to exactly one flight per season. Flight assignment is set by the admin at season start; re-flighting mid-season creates a new membership record, preserving the old flight's history.

Cross-flight competitions (e.g., "low gross of the day") are tracked as separate award categories outside flight standings.

### 2.5 Edge Cases

| Scenario | Handling |
|---|---|
| Missed round | No scorecard created; round simply not counted toward season total |
| Incomplete scorecard (withdrawal) | Status = `NO_RETURN`; 0 points toward season; holes completed are kept for handicap purposes |
| Disqualification | Status = `DISQUALIFIED`; excluded from all leaderboards; visible in audit history |
| Plus handicap player | Strokes deducted from gross on the hardest holes (SI 1, 2...) |
| Cancelled / rained-out round | Round status = `CANCELLED`; any in-progress scorecards voided; new round record if rescheduled |

---

## 3. Backend — Azure Cloud

### 3.1 Technology Choices

| Concern | Choice | Reason |
|---|---|---|
| Runtime | .NET 8 Isolated Worker | Azure Functions native runtime; same EF Core and strong typing as App Service |
| Hosting | Azure Functions (Consumption plan) | Pay-per-execution; a golf league is bursty — zero cost when idle, ~$0 at this scale |
| Database | Azure SQL Database (Serverless) | Relational domain; auto-pauses when idle; no minimum DTU cost |
| Caching | None (eliminated) | At 20-100 players, SQL Serverless query time is negligible; Redis ($17/mo) is not justified |
| Identity | Entra External ID | Consumer identity, free tier (<50K MAU), OAuth 2.0 + PKCE on all platforms |
| Secrets | Azure Key Vault + Managed Identity | No secrets in code or config files |
| File storage | Azure Blob Storage (Cool LRS) | Player photos; negligible cost at this scale |
| Observability | Application Insights (free tier, 10% sampling) | Stays under 5 GB/month free threshold |
| IaC | Bicep | Azure-native; checked into `/infra` |

**Why Functions instead of App Service:** App Service S1 is a $69/month fixed cost regardless of load. This league has one score-entry event per week and a handful of leaderboard reads per day. Azure Functions Consumption charges only for actual executions — the first 1 million per month are free. Realistic monthly compute cost: **$0–$2**. The only tradeoff is cold starts (~500–800ms on first request after idle), which is acceptable for admin operations and invisible to public readers (the web frontend pre-fetches via Static Web Apps' `/api` proxy on page load, and the mobile app uses its Drift cache while the warm-up completes in the background).

**API and background jobs are the same Functions host.** HTTP-triggered functions serve the REST API; timer-triggered and queue-triggered functions handle handicap recalculation and push dispatch. No separate App Service is needed.

### 3.2 Project Structure

```
/
├── src/
│   ├── GolfLeague.Functions/      # Azure Functions host (HTTP API + background triggers)
│   ├── GolfLeague.Domain/         # Entities, interfaces, scoring logic (pure .NET)
│   ├── GolfLeague.Application/    # Use cases, CQRS commands/queries (MediatR)
│   └── GolfLeague.Infrastructure/ # EF Core, Azure SQL, Blob Storage
├── tests/
│   ├── GolfLeague.UnitTests/
│   └── GolfLeague.IntegrationTests/
├── infra/                         # Bicep IaC
│   ├── main.bicep
│   ├── modules/
│   ├── dev.parameters.json
│   └── prod.parameters.json
└── entra-config/                  # Entra External ID app registration notes
```

### 3.3 Key Backend Patterns

- **CQRS with MediatR**: Commands (write) and Queries (read) are separate handler classes. No Redis dependency — queries hit SQL directly, which is fast enough at this scale.
- **Repository pattern**: Repositories defined in `Domain`, implemented in `Infrastructure` over EF Core.
- **Audit pipeline**: Every admin action writes to `AuditLog` via a MediatR pipeline behavior — automatic, not per-controller.
- **Health check**: A simple HTTP-triggered `/api/health` function for Azure Monitor availability tests.

---

## 4. Web Frontend

### 4.1 Technology Choices

| Concern | Choice | Reason |
|---|---|---|
| Framework | React 18 + TypeScript + Vite | Ecosystem maturity; Vite for fast dev builds |
| Server state | TanStack Query v5 | Caching, background refetch, optimistic updates |
| Client state | Zustand | Lightweight; covers auth session, scorecard drafts, UI state |
| Forms | React Hook Form + Zod | Zod schemas shareable with backend |
| Routing | React Router v6 Data Router | URL query params for filters; shareable links |
| UI components | Shadcn/ui over Radix UI | Correct ARIA/keyboard, fully customizable, Tailwind-integrated |
| Styling | Tailwind CSS | Design tokens exportable for mobile alignment |
| API types | openapi-typescript (generated from backend OpenAPI spec) | Eliminates manual type drift |
| HTTP client | Axios with typed domain modules | Interceptors for token refresh, 401-retry |
| Hosting | Azure Static Web Apps (Free) | Global CDN, SSL, CI/CD, PR preview URLs, `/api` proxy |

### 4.2 Route Structure

```
Public (no auth required):
  /                          -> Home / Season Overview
  /flights                   -> Flight list
  /flights/:flightId         -> Flight leaderboard (season standings)
  /rounds                    -> Round history list
  /rounds/:roundId           -> Round detail (all scorecards, hole-by-hole)
  /players/:playerId         -> Player profile + season stats
  /login                     -> Admin login (redirects to Entra External ID)

Admin (RequireAuth wrapper, admin role required):
  /admin                     -> Admin dashboard
  /admin/players             -> Player list + add/deactivate
  /admin/players/:id         -> Edit player / override handicap
  /admin/flights             -> Flight management + player assignment
  /admin/rounds              -> Round list (create, edit, finalize)
  /admin/rounds/:id/scores   -> Score entry / correction grid
  /admin/courses             -> Course and tee box management
  /admin/settings            -> League configuration
  /admin/audit               -> Audit log
```

### 4.3 Key Pages

**Public — Flight Leaderboard:**
- Sticky flight selector tabs (A/B/C Flight)
- Ranked table: position, player name, handicap, total points, rounds played, last round
- Color badges for top 3 positions (gold/silver/bronze)
- Authenticated player's row pinned to bottom if scrolled out of view

**Public — Round Detail:**
- Course header, date, weather conditions
- Per-player scorecard accordion: hole-by-hole gross, net, Stableford points
- Standard golf color coding (birdie = yellow-green, eagle = gold) always paired with the numeric label

**Admin — Score Entry Grid:**
- Spreadsheet-style grid: rows = players, columns = holes 1-18
- Tab advances by hole (matches paper scorecard flow); numeric keyboard on mobile
- Stableford points computed live in each cell as gross scores are entered
- Bulk submit with validation; per-player correction available post-submit (admin only)

**Admin — Player Management:**
- Add player form: name, email, initial handicap, flight assignment
- Soft-delete (deactivate) rather than hard delete to preserve round history
- Handicap history drawer showing all changes with reason and timestamp

### 4.4 Accessibility & PWA

- WCAG 2.1 AA target throughout; Radix UI handles ARIA automatically
- Color coding is never the sole differentiator — numeric values always accompany color
- Service worker caches the public leaderboard and most recent round so golfers can check scores on the course without connectivity
- Score entry in admin mode auto-saves to `localStorage` as a draft to protect against connectivity loss mid-entry

---

## 5. Mobile Apps — Android & iOS

### 5.1 Technology Choices

| Concern | Choice | Reason |
|---|---|---|
| Framework | Flutter 3.x (Dart) | Single codebase, GPU-rendered (Impeller), Material 3, ~46% cross-platform market share |
| Architecture | Clean Architecture + MVVM | Flutter team's recommended pattern; layers enforce testability |
| State management | Riverpod 2.x + AsyncNotifier | Models loading/data/error naturally; no BLoC boilerplate |
| Navigation | GoRouter 14.x (Shell Routes) | Preserves tab state; deep linking from push notifications |
| HTTP client | Dio 5.x | Interceptors for token refresh, request cancellation, error mapping |
| Local DB | Drift (type-safe SQLite ORM) | Offline score viewing + offline score entry queue |
| Ephemeral cache | Hive | Auth tokens, user preferences |
| Secure storage | flutter_secure_storage | Refresh tokens in OS Keychain / Android Keystore |
| Authentication | flutter_appauth (AppAuth + PKCE) | Entra External ID; no client secret on device |
| Push notifications | Firebase Cloud Messaging | Handles both APNs (iOS) and FCM (Android); free at league scale |
| Code generation | freezed + json_serializable | Immutable models, exhaustive pattern matching, fromJson/toJson |
| Charts | fl_chart | Handicap sparklines, standings bar charts |
| CI/CD | Codemagic + Fastlane | Native Flutter support; handles both stores |

### 5.2 Directory Structure

```
lib/
├── core/
│   ├── network/         # Dio client, auth interceptor, AppError sealed type
│   ├── auth/            # TokenService, AppAuth integration
│   ├── theme/           # Material 3 ThemeData (course-green Color(0xFF1B5E20))
│   └── utils/           # StablefordCalculator, date formatters
├── features/
│   ├── dashboard/
│   │   ├── data/        # DTOs, API data source, Drift queries, repository impl
│   │   ├── domain/      # Models, repository interface, use cases (pure Dart)
│   │   └── presentation/# Screens, AsyncNotifier ViewModels, widgets
│   ├── leaderboard/
│   ├── rounds/
│   ├── player_profile/
│   └── score_entry/
└── main.dart
```

Each feature is self-contained. Cross-feature navigation uses GoRouter named routes — no direct imports between feature presentation layers.

### 5.3 Navigation Structure

```
AppShell (bottom nav: Home · Leaderboard · Rounds · Profile)
├── /home                           -> DashboardScreen
├── /leaderboard                    -> FlightListScreen
│   └── /leaderboard/:flightId      -> FlightLeaderboardScreen
├── /rounds                         -> RoundHistoryScreen
│   └── /rounds/:roundId            -> RoundDetailScreen
│       └── /rounds/:roundId/player/:playerId -> PlayerScorecardScreen
├── /profile                        -> PlayerProfileScreen
└── /score-entry/:roundId           -> ScoreEntryScreen (deep link; scorer/admin only)
```

### 5.4 Key Screens

**Dashboard:** Glanceable top-3 per flight, latest round summary, pull-to-refresh. Skeleton loading states (not spinners) while data loads.

**Flight Leaderboard:** Ranked `SliverList` (infinite scroll), point trend arrows (up/down/flat vs previous round), gold/silver/bronze tint for top 3. The authenticated user's row is pinned to the viewport bottom if off-screen.

**Round Detail:** Two tabs — "Leaderboard" (round-only rankings) and "Scorecard" (hole-by-hole for the selected player). Scorecard table scrolls horizontally; holes 1-9 and 10-18 each have a subtotal column.

**Player Profile:** Handicap trajectory sparkline (green = improving, amber = rising), season stats, career summary, recent rounds list.

**Score Entry (scorer/admin role only):** One-hole-per-page wizard. Large stepper buttons (56x56dp). Stableford points computed live. Auto-advance on save. Offline queue to Drift with background sync on reconnect. Haptic feedback on taps and hole submission.

### 5.5 Offline Strategy

- **Read cache**: All leaderboard, round, and scorecard data stored in Drift after first load. UI renders from Drift immediately on reopen; background request refreshes.
- **Score entry queue**: Hole scores written to Drift with `pendingSync = true`. `SyncService` watches `connectivity_plus` and POSTs pending batches on reconnection with exponential backoff (2s initial, max 5 retries, 60s max delay).
- **Connectivity indicator**: Non-intrusive amber banner in the app shell when offline; auto-dismisses on reconnect.
- **Cache eviction**: Rounds from prior seasons evicted at app startup. Current season data is always retained.

### 5.6 Push Notifications

| Event | Priority | Delivery |
|---|---|---|
| New round scores posted | High | Full notification (alert + badge + sound) |
| Leaderboard position change | Normal | Silent push -> background refresh |
| Round reminder (24h prior) | Normal | Full notification |
| Handicap updated | Low | Badge increment only |

FCM tokens are refreshed via `onTokenRefresh` and immediately PATCH'd to `/api/v1/users/me/push-token`. Push permission on iOS is requested after the user's first successful leaderboard load (not on first launch).

### 5.7 App Store Deployment

| | Android | iOS |
|---|---|---|
| Min SDK | API 26 (Android 8.0) | iOS 16.0 |
| Store format | `.aab` (App Bundle) | `.ipa` |
| Signing | Codemagic secrets vault (keystore) | Fastlane `match` + App Store Connect API key |
| Track strategy | internal -> alpha -> staged (10% / 50% / 100%) | TestFlight -> App Store |
| Bundle / App ID | `com.golfleague.app` | `com.golfleague.app` |

### 5.8 Testing Strategy

```
         E2E (Patrol)                5%   Smoke tests vs. staging API
       Integration tests            10%   Full flows on emulator/simulator
     Widget tests                   25%   Screens with mocked Riverpod providers
   Unit tests                       60%   Domain, scoring logic, repositories
```

Minimum 80% line coverage enforced in CI. `StablefordCalculator` is a priority unit test target covering all scoring edge cases.

---

## 6. Authentication & Authorization

### Identity: Local accounts (ASP.NET Core Identity)

All users — admins, scorers, and players — have local accounts stored in this app's database. Authentication is handled by ASP.NET Core Identity. The API issues its own JWTs (HS256, signed with `JWT_SIGNING_KEY`); no external identity provider is involved at runtime for token validation.

**Primary login methods:**
- Email + password
- Google OAuth 2.0 (PKCE)
- Facebook OAuth 2.0 (PKCE)

**Role model (`AppUser.Role`):** `admin` | `scorer` | `player`. The role lives on the AppUser, which is linked 1:1 (optional) to a `Player` row via `Player.AppUserId`. A `Player` may exist without an `AppUser` (admin-managed roster entry); an `AppUser` may exist without a `Player` (an admin who isn't a league participant).

**Admin MFA:** required. Admins must enroll either a WebAuthn passkey or TOTP authenticator after primary login. Once enrolled, every admin sign-in carries the primary factor + one of:
- A passkey assertion (preferred — strongest UX & security)
- A 6-digit TOTP code

Players and scorers can optionally enroll a passkey for password-less sign-in.

### First admin bootstrap

On startup, if no admin exists and `ADMIN_BOOTSTRAP_EMAIL` is set, the Functions host creates an admin `AppUser` with no password. The admin then uses the password-reset flow on first sign-in to set a password and enroll a passkey or TOTP authenticator.

### Token model

| Token | Lifetime | Storage |
|---|---|---|
| Access (full) | 1 hour | Web: in-memory + localStorage; Mobile: secure storage |
| Access (MFA-challenge) | 5 minutes | Session-scoped only — exchanged for full tokens after MFA |
| Refresh | 14 days | Web: localStorage; Mobile: secure storage. Rotated on every use. |

Refresh tokens are stored as SHA-256 hashes server-side (`RefreshTokens` table) and rotated on every refresh. Token revocation happens automatically on rotation; explicit revocation is available via `/auth/logout`.

### Client auth flows

| Client | Login UI | Token storage |
|---|---|---|
| Web (React) | `/login` with email+password + Google/Facebook buttons + passkey | Access in memory + localStorage; refresh in localStorage |
| Android | Email+password form + Google/Facebook via `flutter_web_auth_2` | `flutter_secure_storage` (Android Keystore) |
| iOS | Email+password form + Google/Facebook via `flutter_web_auth_2` | `flutter_secure_storage` (iOS Keychain) |

A Dio/Axios interceptor silently refreshes the access token on 401 before retrying the original request.

### API authorization levels

```
[Public]         No JWT required
[Authenticated]  Valid JWT (any role)
[Scorer]         role == "scorer" OR "admin"
[Admin]          role == "admin"
```

ASP.NET Core policy-based authorization enforces these at the function level (`AdminOnly`, `ScorerOrAdmin`, `Authenticated`).

### Backend JWT validation (Azure Functions)

The Functions host reads `JWT_SIGNING_KEY` from Key Vault (via app setting reference) and validates self-issued tokens:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "golf-league-api",
            ValidAudience = "golf-league-api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType = "role",
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });
```

### Required configuration (Key Vault secrets)

| Secret | Purpose |
|---|---|
| `JwtSigningKey` | HS256 signing key for access + MFA-challenge tokens. Must be ≥ 32 characters. |
| `AdminBootstrapEmail` | Email of the first admin account; created with no password on startup. |
| `GoogleClientId` / `GoogleClientSecret` | OAuth credentials for Google sign-in. Omit to disable Google login. |
| `FacebookAppId` / `FacebookAppSecret` | OAuth credentials for Facebook sign-in. Omit to disable Facebook login. |

Plus app settings:

| Setting | Purpose |
|---|---|
| `FIDO2_RP_ID` | Registrable domain for WebAuthn (e.g. `app.golfleague.com`). |
| `FIDO2_RP_ORIGINS` | Comma-separated list of allowed origins (e.g. `https://app.golfleague.com`). |
| `WEB_BASE_URL` | Base URL for invite links emailed to players. |

---

## 7. Database Schema

Azure SQL Database with Entity Framework Core code-first migrations. All tables use `INT IDENTITY` primary keys for SQL performance; a `UniqueIdentifier` alternate key is available where external references are needed.

### Tables

**`Seasons`** — One row per league season (year, name, start/end dates, active flag).

**`Flights`** — Flights within a season: name, handicap range, display order.

**`Players`** — Player registry. `B2CObjectId` links to Azure AD B2C. `IsActive` for soft-delete.

**`FlightMemberships`** — Assigns a player to a flight for a season. Unique constraint: one active membership per player per season.

**`Handicaps`** — Append-only handicap history. Each change (manual or calculated) is a new row with `EffectiveDate`, `Source` (`calculated` | `manual` | `initial`), and optional admin notes.

**`Courses`** + **`CourseHoles`** — Course registry. Each hole stores par, stroke index, and yardages. Multiple tee box configurations supported.

**`Rounds`** — A scheduled or completed round. Status lifecycle: `scheduled -> in_progress -> pending_finalization -> finalized`.

**`RoundParticipants`** — One row per player per round. Snapshots handicap and course handicap at time of play (immutable after finalization).

**`HoleScores`** — One row per hole per participant. Stores gross strokes, handicap strokes received, net strokes, Stableford points, and max-score-cap flag.

**`AuditLog`** — Append-only log of every admin action with before/after JSON value snapshots.

### Key Indexes

```sql
CREATE INDEX IX_FlightMemberships_FlightId  ON FlightMemberships(FlightId);
CREATE INDEX IX_RoundParticipants_RoundId   ON RoundParticipants(RoundId);
CREATE INDEX IX_HoleScores_ParticipantId    ON HoleScores(ParticipantId);
CREATE INDEX IX_Handicaps_PlayerId_Date     ON Handicaps(PlayerId, EffectiveDate DESC);
```

### Season Standings Query

```sql
SELECT
    p.PlayerId,
    p.FirstName + ' ' + p.LastName AS PlayerName,
    COUNT(rp.ParticipantId)         AS RoundsPlayed,
    SUM(rp.TotalStablefordPoints)   AS SeasonTotal,
    AVG(rp.TotalStablefordPoints)   AS AvgPerRound,
    h.HandicapIndex                 AS CurrentHandicap
FROM FlightMemberships fm
JOIN Players p           ON p.PlayerId = fm.PlayerId
JOIN RoundParticipants rp ON rp.PlayerId = fm.PlayerId
JOIN Rounds r            ON r.RoundId = rp.RoundId
    AND r.FlightId = fm.FlightId
    AND r.Status = 'finalized'
    AND r.SeasonId = @SeasonId
JOIN Handicaps h         ON h.PlayerId = p.PlayerId
    AND h.EffectiveDate = (
        SELECT MAX(EffectiveDate) FROM Handicaps WHERE PlayerId = p.PlayerId
    )
WHERE fm.FlightId = @FlightId
  AND rp.IsWithdrawn = 0
GROUP BY p.PlayerId, p.FirstName, p.LastName, h.HandicapIndex
ORDER BY SeasonTotal DESC, AvgPerRound DESC;
```

---

## 8. API Reference

**Base URL:** `https://api.yourgolfleague.com/api/v1`

All responses use a consistent envelope:
```json
{
  "data": {},
  "meta": { "page": 1, "pageSize": 20, "totalCount": 87 },
  "errors": []
}
```

Standard HTTP status codes: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 422 Unprocessable Entity, 500 Internal Server Error.

### Players

```
GET    /players                           [Public]   List active players
GET    /players/:id                       [Public]   Player profile + season stats
POST   /players                           [Admin]    Create player
PUT    /players/:id                       [Admin]    Update player info
DELETE /players/:id                       [Admin]    Deactivate player (soft delete)
GET    /players/:id/scores                [Public]   All scores across rounds
GET    /players/:id/handicap-history      [Public]   Handicap change log
PUT    /players/:id/handicap              [Admin]    Manual handicap override
PATCH  /users/me/push-token               [Auth]     Update FCM push token
```

### Flights

```
GET    /seasons/:seasonId/flights         [Public]   List flights for a season
GET    /flights/:id                       [Public]   Flight details + member list
GET    /flights/:id/standings             [Public]   Season standings for flight
POST   /flights                           [Admin]    Create flight
PUT    /flights/:id                       [Admin]    Update flight name/handicap range
DELETE /flights/:id                       [Admin]    Remove empty flight
POST   /flights/:id/players               [Admin]    Assign player to flight
DELETE /flights/:id/players/:playerId     [Admin]    Remove player from flight
```

### Rounds

```
GET    /rounds                            [Public]   List rounds (paginated, filterable)
GET    /rounds/:id                        [Public]   Round details
GET    /rounds/:id/scorecard              [Public]   All hole-by-hole scorecards
POST   /rounds                            [Admin]    Schedule new round
PUT    /rounds/:id                        [Admin]    Update round metadata
DELETE /rounds/:id                        [Admin]    Cancel unfinalized round
POST   /rounds/:id/finalize               [Admin]    Finalize round; trigger handicap recalc
```

### Scores

```
GET    /rounds/:roundId/scores                        [Public]   All scores for a round
GET    /rounds/:roundId/scores/:playerId              [Public]   Player score in round
POST   /rounds/:roundId/scores                        [Scorer]   Bulk submit scores
PUT    /rounds/:roundId/scores/:playerId              [Admin]    Correct submitted score
GET    /rounds/:roundId/scores/:playerId/holes        [Public]   Hole-by-hole breakdown
PUT    /rounds/:roundId/scores/:playerId/holes        [Scorer]   Submit hole-by-hole scores
```

### Courses

```
GET    /courses                           [Public]   List courses
GET    /courses/:id                       [Public]   Course detail + holes
POST   /courses                           [Admin]    Add course
PUT    /courses/:id                       [Admin]    Update course
```

### League / Admin

```
GET    /league/settings                   [Admin]    League configuration
PUT    /league/settings                   [Admin]    Update configuration
GET    /league/seasons                    [Public]   List all seasons
POST   /league/seasons                    [Admin]    Create new season
GET    /league/seasons/:id/standings      [Public]   Cross-flight season overview
GET    /admin/audit-log                   [Admin]    Admin action history
POST   /handicaps/recalculate             [Admin]    Trigger manual recalculation
```

---

## 9. Deployment Architecture

### Environments

Single production environment only. All testing happens locally and in CI before deploying directly to production.

| Environment | Purpose | Resource Group |
|---|---|---|
| Production | Live league | `golf-league-prod` |

### CI/CD (GitHub Actions)

```
feature/* -> PR -> main (runs full test suite on PR)
main      -> deploy to Production (one-admin approval gate)
```

**Pipeline stages:**
1. Build: `dotnet build`, unit tests, TypeScript/lint checks, Flutter tests
2. Integration tests against a local SQLite in-memory database
3. Publish artifact
4. Deploy to production via `func azure functionapp publish`; EF Core migrations run as a pre-deploy step
5. Smoke test against `/api/health`

Local development uses `func start` (Azure Functions Core Tools) with a local `local.settings.json` pointing at a local SQL Server Express or SQLite database. No cloud resources are consumed during development.

### Infrastructure as Code (Bicep)

```
/infra/
├── main.bicep
├── modules/
│   ├── functions.bicep
│   ├── sql.bicep
│   ├── storage.bicep
│   ├── keyvault.bicep
│   └── appinsights.bicep
└── prod.parameters.json
```

### Monitoring

- **Application Insights**: Request/dependency tracing; custom events (`RoundFinalized`, `HandicapRecalculated`, `ScoreSubmitted`)
- **Availability tests**: Ping `/health` every 5 minutes
- **Alerts**: 5xx error rate > 1% over 5 min -> email; CPU > 80% for 10 min -> email + Teams notification; SQL usage > 80% -> email

---

## 10. Cost Estimate

### Production Monthly

| Service | Tier | Est. Monthly Cost | vs. v1.0 |
|---|---|---|---|
| ~~App Service S1~~ | ~~Standard S1~~ | ~~$69~~ | eliminated |
| Azure Functions | Consumption (< 1M executions) | ~$0-$2 | **-$67** |
| Azure SQL Database | General Purpose Serverless, 1 vCore, auto-pause | ~$10-$20 | -$5 (smaller vCore) |
| ~~Azure Cache for Redis~~ | ~~Basic C0~~ | ~~$17~~ | **eliminated** |
| Azure Blob Storage | Cool LRS, < 1 GB | ~$0.01 | unchanged |
| Entra External ID | Free (< 50K MAU) | $0 | unchanged |
| Azure Key Vault | Standard | ~$1 | unchanged |
| Application Insights | Free tier, 10% sampling | $0 | -$3 (sampling) |
| Azure Static Web Apps | Free tier | $0 | unchanged |
| **Total (Production)** | | **~$11-$23/month** | **-$79-$97/month** |

**Estimated total: ~$11-$23/month** (production only).

That is a reduction of approximately **$125-$140/month** (85%+) compared to v1.0 with no change to functionality.

### What Was Cut and Why

| Cut | Saving | Rationale |
|---|---|---|
| App Service S1 → Functions Consumption | ~$67/month | Traffic is bursty (one round/week, light reads). Fixed compute is wasteful. |
| Redis eliminated | ~$17/month | With no persistent server process, in-process caching has no benefit. SQL Serverless is fast enough. |
| Staging environment eliminated | ~$15-20/month | A golf league has one admin and < 100 users. Not justified. |
| Dev environment eliminated | ~$5-10/month | Developers run `func start` locally with SQL Server Express or SQLite. Zero cloud cost. |
| Application Insights sampling 10% | ~$3/month | Stays under the 5 GB/month free threshold. |

### Growth Path

If the league scales to hundreds of players or year-round activity, the upgrade path is straightforward:
- **Functions → App Service B2** (~$37/month): adds persistent connections, eliminates cold starts, adds a deployment slot.
- **SQL Serverless → Standard S0** (~$15/month fixed): removes auto-pause latency on first request of the day.
- **Add Redis** (~$17/month): only if measured SQL read latency becomes a user-visible problem.

These upgrades can be applied independently as actual usage data justifies them.

---

## Appendix: Full Technology Stack

| Layer | Technology |
|---|---|
| Backend API + jobs | .NET 8 Isolated Worker, Azure Functions (Consumption), MediatR, EF Core 8 |
| Database | Azure SQL Database (Serverless, auto-pause) |
| Identity | Entra External ID, MSAL / flutter_appauth, OAuth 2.0 + PKCE |
| Secrets | Azure Key Vault + Managed Identity |
| Web frontend | React 18, TypeScript, Vite, TanStack Query, Zustand, Shadcn/ui, Tailwind CSS |
| Web hosting | Azure Static Web Apps (Free tier) |
| Mobile | Flutter 3.x (Dart), Riverpod 2.x, GoRouter, Drift, Hive, Dio |
| Mobile auth | flutter_appauth (AppAuth SDK + PKCE), Entra External ID |
| Push notifications | Firebase Cloud Messaging + APNs |
| Mobile CI/CD | Codemagic (free tier) + Fastlane |
| Backend CI/CD | GitHub Actions |
| Infrastructure | Azure Bicep (IaC) |
| Observability | Azure Application Insights (free tier, 10% adaptive sampling) |
