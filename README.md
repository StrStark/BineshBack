# Binesh — Rewrite

Rewrite of the original `src/DataBaseManager` (`BineshSoloution`) project.
Lives alongside the old code so both can run during the migration; old code stays
authoritative until parity is reached, then we cut over.

## Architecture

Layered solution, vertical slices for features, MediatR pipeline for cross-cutting concerns.

```
src-v2/
├── src/
│   ├── Binesh.Domain          # entities, value objects, domain events. No dependencies.
│   ├── Binesh.Application     # use cases (Features/*), DTOs, validators, MediatR contracts.
│   ├── Binesh.Infrastructure  # EF Core (Postgres), MinIO, SMS, OpenAI client wrapper.
│   ├── Binesh.Identity        # ASP.NET Identity, JWT, refresh tokens, permissions.
│   ├── Binesh.Ai              # IQueryableTool plugin model, schema registry, orchestrator.
│   ├── Binesh.Etl             # change-batch handlers, sync contracts.
│   └── Binesh.Api             # ASP.NET Core host. Program.cs lives here.
└── tests/
    ├── Binesh.Domain.UnitTests
    ├── Binesh.Application.UnitTests
    ├── Binesh.Ai.IntegrationTests
    └── Binesh.Api.IntegrationTests
```

Dependency rule: **arrows point inward toward Domain.** Domain depends on nothing.
Application depends on Domain. Everything else depends on Application.
Only Binesh.Api is allowed to wire concrete implementations.

## Conventions

- **Feature folders** — every use case lives in `Binesh.Application/Features/<Area>/<UseCase>/`
  with its own endpoint, request, handler, validator, and response co-located.
- **One slice = one folder.** Delete a folder, you deleted the feature. No hunting.
- **No god-classes.** Every controller / endpoint injects only what it actually uses.
- **No `Console.WriteLine`.** Serilog is the only logger.
- **No secrets in source.** All credentials come from env vars or User Secrets.
- **No magic strings.** Permissions, role names, claim types are typed enums or constants.

## Status

The rewrite proceeds in **rounds**, one feature area at a time, each ending with
a clean build, passing integration tests, a live smoke test, and a
[CHANGES.md](./CHANGES.md) entry. See that file for the full port log and every
behavior/API change from the original code.

| # | Round | Status |
|---|---|---|
| 1–3 | Solution skeleton · composition root · Docker | ✅ done |
| 4 | Sales `GetSummary` reference slice | ✅ done |
| 5 | Auth (phone OTP, JWT, refresh rotation) | ✅ done |
| 6 | User management + closed registration | ✅ done |
| 7 | Customers | ✅ done |
| 8 | Products + inventory events + stagnation report | ✅ done |
| 9 | Sales (full CRUD) | ✅ done |
| 9b | Sales panel summaries (categorized · regional · top-selling) | ✅ done |
| 10 | Sales Returns | ✅ done |
| 11 | Financial (entries · Shalli mapping settings · panel) | ✅ done |
| 12 | AI orchestration (query engine · tools · OpenAI · budgets · fallback) | ✅ done |
| 13 | Chat on Postgres jsonb + streaming WebSocket (ticket auth) | ✅ done |
| 14 | ETL | ⏭️ **skipped** — not ported from legacy; new approach TBD by user |
| 15 | Profile images via MinIO (pre-signed URLs) | ✅ done |
| 16 | Parity verification + cutover plan | 🟡 in progress |

**Note on `Binesh.Etl`:** the project stays in the solution as a contracts-only
skeleton (`IChangeBatchHandler`, `ChangeBatch`, `IHasSyncKey`). No ETL endpoints
are mapped and nothing is wired into `Program.cs` — the legacy websocket sync is
deliberately not ported (Round 14).
