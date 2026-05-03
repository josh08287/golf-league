# Agent notes — Golf League

This monorepo contains:

- **Backend**: Azure Functions (.NET 9) in `src/GolfLeague.Functions`, with domain and CQRS in `GolfLeague.Domain` / `GolfLeague.Application`, and EF Core + SQLite in `GolfLeague.Infrastructure`.
- **Database**: The authoritative store is a **SQLite file** synchronized with **Azure Blob Storage** (not Azure SQL). See `Program.cs`, `BlobSyncedDbContext`, and `Infrastructure/DependencyInjection.cs`.
- **Web**: React + Vite in `web/`; API base defaults to `/api/v1` via `web/src/lib/api.ts`.
- **Mobile**: Flutter in `mobile/lib/`; API and Entra settings in `mobile/lib/core/config.dart`.

**Planning doc vs code:** `ARCHITECTURE.md` describes product goals and domain rules (Stableford, flights, etc.) but still mentions Azure SQL in places. For **storage, routing, and deployment**, trust **`docs/CODEBASE_MAP.md`** and the always-on rule `.cursor/rules/golf-league-codebase.mdc`.

**CI default branch:** `master` (see `.github/workflows/`).

When you need detail without re-scanning the tree, open **`docs/CODEBASE_MAP.md`**.
