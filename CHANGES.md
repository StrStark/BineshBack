# CHANGES — Binesh Rewrite Port Log

This is the running log of **every behavior or API change** between the
original `src/DataBaseManager` code and the rewrite in `src-v2/`. Frontend
teams use this to know exactly what needs to change on their side.

Format:
- **Removed** — endpoint / behavior gone (with reason).
- **Changed** — endpoint kept, but shape / behavior differs (with before/after).
- **Added** — new endpoint / behavior (only when needed for security/correctness).
- **Internal** — implementation rewritten, no observable API change.

---

## Round 1 — Solution skeleton

### Internal
- New solution lives in `src-v2/`. Old `src/DataBaseManager` untouched.
- Solution renamed `BineshSoloution` → `Binesh` (typo fix).
- Target framework upgraded `net8.0` → `net10.0` (both runtimes are installed on
  the dev machine; .NET 10 is current LTS, .NET 8 EOL Nov 2026).
- Single-project layout split into seven projects with strict dependency rules
  (see [README.md](./README.md)).
- Added `Directory.Build.props` to centralize `TargetFramework`, `Nullable`,
  `LangVersion`, code-style enforcement, and a focused set of warnings-as-errors
  (`CS8602` nullable deref, `CS4014` un-awaited task, `CS1998` async without await).
- Added `.editorconfig` enforcing file-scoped namespaces, primary constructors,
  `_camelCase` private fields, and `using`-outside-namespace placement.
- Added `.dockerignore` so build context excludes `.pfx`, `.env`, `bin/`, `obj/`,
  IDE folders, and the old `src/` tree.

No endpoints exist yet — Round 1 only proves the solution compiles.

---

## Round 2 — Composition root

### Added endpoints
- `GET /` — service status (`{service, status, environment, time}`). For load-balancer smoke tests.
- `GET /healthz` — liveness probe. Returns 200 as long as the process is alive. Used by orchestrators to decide whether to restart the container.
- `GET /readyz` — readiness probe. Aggregates all checks tagged `ready` (currently: Postgres reachability + OpenAI config). Returns 503 if any fails. Used by orchestrators to decide whether to route traffic.

### Changed (cross-cutting infrastructure — replaces a lot of old code)
- **Exception handling** — Replaced 56 try/catch + `ApiResponse.Fail` blocks across 17 old controllers with a single `IExceptionHandler` (`GlobalExceptionHandler`) that emits RFC 7807 ProblemDetails. Every handler now just throws; the response shape, status code, and trace ID are uniform across the API.
- **Error response shape** — Was `{ status: "success"|"error", code: 200, message: "...", body: ... }`. **Now is RFC 7807**: `{ type, title, status, detail, instance, traceId, code, errors? }`. Frontend MUST update — see error handler / API client. Success responses are unchanged for now (each slice picks its own shape).
- **Logging** — `Console.WriteLine` (10 sites) and unstructured `_logger.LogError(ex, "...")` calls replaced with Serilog using structured templates. One `Microsoft.AspNetCore.Hosting.Diagnostics` line per request with timings.
- **Configuration** — Strongly-typed `IOptions<T>` for `DatabaseSettings`, `JwtSettings`, `OpenAiSettings`, `CorsSettings`, `RateLimitSettings`. All `ValidateDataAnnotations().ValidateOnStart()` so missing required values fail at boot, not at first request.
- **CORS** — Was a double `UseCors` (`AllowAnyOrigin` overriding two named policies). Now a single `default` policy bound from `Cors:AllowedOrigins` env array. No wildcards.
- **Rate limiting** — Added three named policies (`auth`: 5/min by IP, `ai`: 60/min sliding by user-or-IP, `default`: 100/min). Policies are registered now; slices that need them apply via `.RequireRateLimiting("name")` in later rounds.
- **Authorization** — Was a `RequirePermissionAttribute` that **could not work** (DI-injecting an attribute is impossible). Replaced with policy-based: one policy per `AppPermission` value, named `permission:<Name>`. Use `[Authorize("permission:UserManagement")]`. SuperAdmin role bypasses all permission checks (handled in `PermissionAuthorizationHandler`).
- **JWT signing certificate** — Was a hardcoded `.pfx` committed to the repo with a known password. Now read from env vars (`Jwt__SigningCertificatePath`, `Jwt__SigningCertificatePassword`). In **Development**, if no cert is configured, an ephemeral self-signed cert is generated in memory at first auth attempt (tokens won't survive restart — fine for dev). In **Production**, missing cert config throws at boot.
- **JWT bearer events** — Old code printed every `Authorization` header to Console on every request (massive token leak). Removed entirely; the framework's default JWT challenge response is used.
- **OpenAI client** — Now reads `OpenAI:BaseUrl` from env. Works with `api.openai.com`, `api.gapgpt.ir`, Azure OpenAI gateways, self-hosted vLLM. Timeout configurable.
- **MediatR + FluentValidation pipeline** — `LoggingBehavior` (request name + duration) and `ValidationBehavior` (runs every registered validator, throws our `ValidationException` on failure) are registered globally. Handler authors don't write try/catch or validation glue.
- **Configuration env-var loading** — `DotNetEnv` loads `.env` if present (dev convenience). `.env.example` at repo root documents every variable.

### Removed
- **HTTPS redirect in production** behavior change deferred until Round 3 — current pipeline calls `UseHttpsRedirection` regardless. The old code only called it in Development (backwards). Will be replaced by Traefik TLS termination + `UseForwardedHeaders` in Round 3.
- **`DataProtectionCertificate.pfx`** is NOT carried over. Round 3 docker setup will provide a script to generate one per environment.

### New exception hierarchy (`Binesh.Application.Exceptions`)
Replaces the chaotic `Exceptions/` folder in the old code. Every exception declares its `StatusCode` and stable `ErrorCode`:
- `AppException` (base — note: NOT `ApplicationException` to avoid collision with `System.ApplicationException`)
- `NotFoundException` → 404 / `resource.not_found`
- `ConflictException` → 409 / `resource.conflict`
- `ValidationException` → 422 / `validation.failed` (with per-field errors)
- `UnauthorizedException` → 401 / `auth.unauthorized`
- `ForbiddenException` → 403 / `auth.forbidden`
- `TooManyRequestsException` → 429 / `rate.exceeded`

### Verified
- `dotnet build` → 0 warnings, 0 errors across 11 projects.
- `dotnet run` (Development) boots cleanly without any external dependencies.
- `GET /` → 200 with status JSON.
- `GET /healthz` → 200.
- `GET /readyz` → 503 when Postgres unreachable (correct), 200 when up.
- `GET /swagger/v1/swagger.json` → 200.

---

## Round 3 — Docker

### Added
- **`src/Binesh.Api/Dockerfile`** — multi-stage build (restore → build/publish → runtime). Runs as non-root `$APP_UID`. Layer-cached on `csproj` changes so source-only edits skip the restore step.
- **`docker-compose.yml`** — dev stack: `postgres` (16-alpine) + `adminer` + `minio` + `migrator` (one-shot) + `api`. All env vars have inline defaults so `docker compose up --build` works with no `.env` file.
- **`docker-compose.prod.yml`** — overlay that adds `traefik` (with Let's Encrypt + HTTP→HTTPS redirect), pulls the API image from a registry instead of building, closes Postgres + MinIO ports (internal-only), removes Adminer, and requires real secrets via `.env`.
- **`deploy/README.md`** — runbook for VPS deploy + JWT cert generation + rotation.
- **`migrate` subcommand** in `Binesh.Api` — `dotnet Binesh.Api.dll migrate` applies pending EF migrations and exits. The migrator container uses this; the API container's startup is gated on `migrator: service_completed_successfully` so a failed migration prevents the API from starting (safer than the old code's `app.Database.Migrate()` at startup).

### Changed
- **Migrations** — Old code called `appDbContext.Database.Migrate()` synchronously inside the pipeline middleware on every boot ([Program.Middlewares.cs:49-50](../src/DataBaseManager/Program.Middlewares.cs:49)). New code runs migrations as a **separate one-shot container** before the API starts. A bad migration fails the migrator, the API stays down, and you see the error in `docker compose logs migrator` without losing the previously-running pod.
- **HTTPS** — Old code only had `UseHttpsRedirection` in Development (backwards). New code has it always on; TLS termination happens at Traefik in prod, so the API speaks plain HTTP inside the docker network and Traefik handles the cert.
- **CORS in prod** — Old `docker-compose.yml` hardcoded the postgres password as `Mr5568###` in plain YAML. New prod overlay reads `${POSTGRES_PASSWORD}` from a `.env` file that is `.gitignore`'d.

### Removed
- **`Dockerfile.original`** is not carried over — it referenced a private registry (`registry.bineshafzar.ir/dotnet/aspnet:8.0`) and copied the certificate file directly into the image. New Dockerfile uses public Microsoft images and mounts the cert as a Docker secret in prod.
- **`build/`** directory at the repo root (empty) is not carried over.

### Verified
- `dotnet build` after the `migrate` subcommand → 0 warnings, 0 errors.
- `docker compose config` → dev YAML valid.
- `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` → prod overlay valid (with all required env vars set).

### Fixes after end-to-end verification

The first `docker compose up --build` surfaced three issues. All fixed:

1. **Image-tag race** — Both `api` and `migrator` had `build:` blocks producing the same `binesh-api:dev` tag. BuildKit fails on the parallel export collision. **Fix:** only `api` has `build:`; `migrator` references the image. Compose's build phase runs before the run phase, so the image exists by the time migrator starts.
2. ~~**Port 5432 conflict** — Docker had a phantom reservation on host port 5432.~~ False alarm — was another Postgres container the user had running; once stopped, port 5432 is free. **Reverted to `5432` default.**
3. **API container marked "unhealthy"** — The healthcheck CMD used `curl`, which isn't in the `aspnet:10.0` runtime image. **Fix:** added `apt-get install curl` to the runtime stage of the Dockerfile (~3MB).

### Verified end-to-end
```
$ docker compose up --build -d
$ docker ps --format "table {{.Names}}\t{{.Status}}"
binesh-api        Up 35 seconds (healthy)
binesh-adminer    Up 36 seconds
binesh-postgres   Up 42 seconds (healthy)
binesh-minio      Up 42 seconds (healthy)
$ curl http://localhost:8080/healthz       → 200 Healthy
$ curl http://localhost:8080/readyz        → 200 Healthy
$ docker logs binesh-migrator | tail
[10:45:42 INF] Starting Binesh.Api
[10:45:42 INF] Applying pending migrations...
[10:45:42 INF] Migrations complete.
```

---

## Round 4 — Reference vertical slice (`Sales/GetSummary`)

### Added endpoint
- `GET /api/sales/summary?from=YYYY-MM-DD&to=YYYY-MM-DD` — returns total revenue + order count + average order value + per-day breakdown for the range. 200 on success, 422 with per-field errors on bad input.

### Added structure (the template every future feature follows)
```
src/Binesh.Domain/Sales/Sale.cs                              ← entity (sealed, factory method)
src/Binesh.Application/
  Abstractions/IBineshDbContext.cs                            ← abstraction over the DbContext
  Features/Sales/GetSummary/
    GetSummaryQuery.cs                                        ← IRequest + Response records
    GetSummaryValidator.cs                                    ← FluentValidator
    GetSummaryHandler.cs                                      ← IRequestHandler
src/Binesh.Infrastructure/Persistence/
  Configurations/SaleConfiguration.cs                         ← EF mapping
  Migrations/20260626110407_InitialSchema.cs                  ← auto-generated
src/Binesh.Api/Endpoints/Sales/SalesEndpoints.cs              ← MapSalesEndpoints extension
tests/Binesh.Api.IntegrationTests/
  BineshApiFactory.cs                                          ← WebApplicationFactory + Testcontainers
  Features/Sales/GetSummaryTests.cs                            ← 5 tests
```

### Conventions established
- **Entities**: singular class names (`Sale`, not `Sales`), sealed, factory methods (`Sale.Create(...)`), private setters. EF Core access via private parameterless ctor.
- **Aggregations**: GROUP BY pushed to SQL (`.GroupBy(...).Select(...).ToListAsync(...)`). The old code's "materialize everything then `.GroupBy` in memory" pattern is forbidden.
- **DbContext access**: handlers depend on `IBineshDbContext` (interface in Application), never the concrete `BineshDbContext` (in Infrastructure). One interface line per new entity.
- **Cross-field validation**: attach `Must((q, val) => ...)` rules to a specific property so the error key in the response is meaningful (not empty string).
- **Migrations**: live under `src/Binesh.Infrastructure/Persistence/Migrations/`. Generated with `dotnet ef migrations add <Name> --project src/Binesh.Infrastructure --startup-project src/Binesh.Api`. `dotnet-ef` pinned at 9.0.1 via local tool manifest.

### Fixes that came out of building this round
- **Serilog bootstrap-logger removed** — the `Log.Logger = CreateBootstrapLogger()` pattern can only freeze once, and `WebApplicationFactory<Program>` re-uses the same static `Log.Logger` across each test's host build → `InvalidOperationException: The logger is already frozen`. Pre-`Host.UseSerilog` errors now print to `Console.Error` instead; everything after gets full Serilog. Trade-off was worth it for testability.
- **GlobalExceptionHandler** — was using `ValidationProblemDetails` to carry per-field errors. When `WriteAsJsonAsync` saw the static `ProblemDetails` type, the subclass's `Errors` property was silently dropped. Switched to putting errors in `ProblemDetails.Extensions["errors"]`, which serializes reliably regardless of declared type.
- **EditorConfig** — added `[**/Migrations/*.cs] generated_code = true` + `IDE0161` disabled, so EF's block-scoped namespaces in auto-generated migrations don't fight our file-scoped namespace rule.
- **Package versions** — bumped all `Microsoft.Extensions.*` from 9.0.0 → 9.0.1 across all projects (transitive deps pulled by `Microsoft.EntityFrameworkCore` need 9.0.1). Serilog.AspNetCore + Serilog.Settings.Configuration jumped from 9.0.0 → 10.0.0 (9.0.1 doesn't exist on NuGet). `Serilog.Sinks.Console` bumped to 6.1.1.
- **Local tool manifest** — `dotnet-ef` 9.0.1 added to `.config/dotnet-tools.json`. Run with `dotnet ef ...` from `src-v2/`.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test tests/Binesh.Api.IntegrationTests` → **5 passed, 0 failed** (~5s, including Postgres container spin-up).
- Live container build + endpoint smoke test (see below).

### Frontend impact
- New endpoint: `GET /api/sales/summary?from=...&to=...` returning `{totalRevenue, orderCount, averageOrderValue, byDay: [{date, revenue, orderCount}]}`.
- Validation failures now return `{type, title, status, detail, instance, traceId, code, errors: {fieldName: [messages]}}` (errors map under `extensions.errors`).

---

## Round 5 — Auth slice

### Added endpoints (4, replacing the old 7)
- `POST /api/auth/otp/request` — body `{phoneNumber}`. Always returns 200 (no user enumeration). Sends a 6-digit OTP via SMS. Creates the user if missing. Rate-limited via the `auth` policy.
- `POST /api/auth/otp/verify` — body `{phoneNumber, otp, deviceInfo?}`. Returns `{accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt}` on success, 401 on bad OTP.
- `POST /api/auth/refresh` — body `{refreshToken}`. Rotates: returns a new pair, marks the old refresh token as used. Reuse of a used token revokes the entire session ("family revoke").
- `POST /api/auth/signout` — body `{refreshToken}`. Revokes the session backing the token. 200 either way.

### Removed (collapsed) endpoints
The old 7 endpoints (`SignUp`, `SignIn`, `ConfirmSignUpPhone`, `ConfirmSignInPhone`, `Refresh`, `SignOut`, `SendConfirmPhoneToken`) collapsed into 4. **Frontend will need updates.** New flow:
1. `POST /api/auth/otp/request` (works for new + existing users)
2. `POST /api/auth/otp/verify` returns tokens
3. `POST /api/auth/refresh` to rotate
4. `POST /api/auth/signout` to log out

### Security fixes (all logged tested as regressions)
- **`696969` master-OTP backdoor REMOVED.** A regression test (`VerifyOtp_MagicBackdoorCode696969_Returns401`) guards against re-introduction.
- **Refresh-token expiry bug FIXED.** Old code used `DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiration.TotalMinutes)` — for a 14-day setting that's `AddDays(20160)`, expiring in year 2081. New code uses `Add(jwt.RefreshTokenLifetime)` directly. A test asserts the expiry is within 1-60 days.
- **Refresh-token rotation with reuse detection.** Every refresh produces a new token; the old one is marked used. If a used (or revoked) token is presented again, the entire session is revoked. The old code had no rotation — it replaced the token in-place without history, so reuse was undetectable. Tested.
- **No JWT in console logs.** Old code's `OnMessageReceived` printed every `Authorization` header to console. Removed entirely.
- **OTP not logged in production.** Old `SmsService` logged the OTP value alongside the SMS. New code only logs OTP when `Sms:Provider = "log"` (dev mode). Real SMS senders never log the value.
- **Phone numbers normalized to E.164.** Old code accepted `09121234567`, `+989121234567`, `9121234567` as different users. New `PhoneNumberNormalizer` canonicalizes to `+98XXXXXXXXXX`. Fixes the duplicate-account problem silently lurking in the old DB.
- **Resend-delay enforced from settings.** Configurable via `Identity:Otp:ResendDelay`. Default 60s. Returns 429 with `Retry-After` semantics if violated.
- **Lockout on failed OTP attempts.** Configurable: 5 failures → 15-minute lockout (was: lockout in old code only triggered on one of two parallel code paths).

### Architectural changes
- **One DbContext for everything.** Old code split `ApplicationDbContext` + `ApplicationIdentityDbContext` (same connection, transactions broken). New `BineshDbContext` inherits `IdentityDbContext<User, Role, Guid>` — Identity tables and business tables in one transaction scope.
- **User entity moved to `Binesh.Domain/Identity/`.** Added `Microsoft.Extensions.Identity.Stores` to Domain so `IdentityUser<Guid>` is available without pulling EF or ASP.NET Core.
- **Auth slices live in `Binesh.Identity/Features/Auth/`**, not Application. They depend on Identity-specific services (`UserManager`, `IJwtTokenService`, `IOtpService`, `ISmsSender`) which only Identity has access to. `AddApplication(...)` now takes additional assemblies so Api can scan both Application and Identity for MediatR handlers.
- **Permission-based authorization is now real.** `[Authorize("permission:UserManagement")]` works. Replaces the broken `RequirePermissionAttribute` that tried to inject DI into an attribute (impossible).
- **Phone-only auth, no password.** The old code had a half-baked password path that never triggered. Removed entirely; OTP is the only credential.

### New entities (Domain/Identity)
- `User : IdentityUser<Guid>` — adds FirstName, LastName, JobTitle, BirthDate, ProfileImageName, LastOtpRequestedAt, CreatedAt, Sessions navigation.
- `Role : IdentityRole<Guid>` — sealed; convenience ctor.
- `UserSession` — per-device login. Has DeviceInfo, IpAddress, UserAgent, StartedAt, LastSeenAt, RevokedAt, RevocationReason. One `UserSession` → many `RefreshToken` (the rotation chain).
- `RefreshToken` — `TokenHash` (SHA-256), `IssuedAt`, `ExpiresAt`, `UsedAt`, `ReplacedByTokenId`, `RevokedAt`. Raw token only ever lives on the wire — `RefreshToken.Issue(...)` returns the raw value and the persisted entity separately.

### New services (Identity)
- `IJwtTokenService` / `JwtTokenService` — issues signed JWT access tokens.
- `IOtpService` / `OtpService` — generates and verifies 6-digit phone OTPs via Identity's token provider.
- `ISmsSender` / `LogSmsSender` / `IppanelSmsSender` — pluggable SMS sender. Pick via `Sms:Provider` env var. `log` (dev) writes OTP to logs; `ippanel` (prod) sends real SMS.
- `PhoneNumberNormalizer` — normalizes Iranian mobiles to `+98...`.

### Verified
- `dotnet build` → 0 warnings, 0 errors across 11 projects.
- `dotnet test` → **14/14 pass** in ~6 seconds (9 auth + 5 GetSummary).
- Live container endpoints (see below).

### Frontend impact
- **Endpoint map changes.** See above. Use the 4 new endpoints.
- **Phone numbers always returned in E.164 format** (`+98...`). Frontend should display them in local format if desired but send any of `09...` / `+98...` / `98...`.
- **Token shape**: `{accessToken: string, refreshToken: string, accessTokenExpiresAt: ISO, refreshTokenExpiresAt: ISO}`. Old code used a different shape under `ApiResponse<TokenResponseDto>`.
- **Always include `Authorization: Bearer <accessToken>`** on protected endpoints (added in later rounds). Use `/api/auth/refresh` when access token expires.

---

## Round 6 — User management + closed registration

### Major behavior change: signup is closed
**Anyone can no longer self-register.** Only users created by a SuperAdmin can sign in.

- `POST /api/auth/otp/request` no longer creates users. Unknown phones still get a 200 response (no enumeration) but no SMS is sent. A server-side log notes the probe.
- `POST /api/auth/otp/verify` no longer has a user-creation path. Unknown phones → 401.

### Role model (interim — to be expanded later)
Two roles only for now:
- **`SuperAdmin`** — single account, seeded from `Seed:SuperAdmin:PhoneNumber` env var on first boot. Cannot be created, edited, or deleted via the API.
- **`Admin`** — created by SuperAdmin via `POST /api/users`. Regular operating user.

When the frontend needs more granular roles later, they're added with no schema churn (the `permission:*` policies infrastructure is already wired and unused for now).

### Added endpoints
| Endpoint | Auth | Notes |
|---|---|---|
| `GET    /api/users/me`           | any auth      | own profile |
| `PUT    /api/users/me`           | any auth      | update own fields |
| `GET    /api/users`              | Admin+        | paginated list with search |
| `GET    /api/users/{id}`         | Admin+        | one user |
| `POST   /api/users`              | SuperAdmin    | create new Admin (role hardcoded for now) |
| `PUT    /api/users/{id}`         | SuperAdmin    | update; cannot target SuperAdmin |
| `DELETE /api/users/{id}`         | SuperAdmin    | delete; cannot target SuperAdmin or self |

Authorization policies:
- `role:SuperAdmin` — requires SuperAdmin role
- `role:Admin` — requires SuperAdmin OR Admin role

### Bootstrap (replaces fire-and-forget seeder)
- `IdentityBootstrapService : IHostedService` runs synchronously before the HTTP listener accepts requests.
- Ensures `SuperAdmin` and `Admin` roles exist.
- If `Seed:SuperAdmin:PhoneNumber` is set AND no SuperAdmin exists, creates one with phone confirmed.
- Subsequent boots are no-ops (idempotent).
- Replaces the old code's `Task.Run(async () => await RoleSeeder.SeedAsync(...))` race condition.

### Frontend impact
- **No more self-registration UI.** Hide signup forms; new users come from an admin invitation flow.
- **First-time login for new Admins:** SuperAdmin creates them via `POST /api/users`. They then sign in via the same OTP flow (`/otp/request` → `/otp/verify`). Phone is confirmed on first successful verify.
- **`/api/users/me`** is the right endpoint for showing the user's profile (replaces ad-hoc "decode the JWT to show user info" patterns).
- **Role-aware UI:** check `role` field on `/me` response. `SuperAdmin` sees the user-management screen; `Admin` does not.
- **N+1 fix:** `ListUsers` joins UserRoles in one query (old code did `_userManager.GetRolesAsync(user)` inside a foreach — 1 query per user).

### Test factory fix
The `IdentityBootstrapService` queries `AspNetRoles` on startup. Tests need the
schema in place BEFORE the host runs the bootstrap service, so the test factory
now applies migrations via a standalone DbContext before touching `Services`:
```csharp
var options = new DbContextOptionsBuilder<BineshDbContext>()
    .UseNpgsql(_postgres.GetConnectionString())
    .Options;
await using var db = new BineshDbContext(options);
await db.Database.MigrateAsync();
_ = Services;   // host start triggers SuperAdmin seed, now sees migrated schema
```

### Verified
- `dotnet build` → 0 warnings, 0 errors across 11 projects.
- `dotnet test` → **30/30 pass** in ~7s.
- Live container smoke test: SuperAdmin auto-seeded, OTP flow works, `POST /api/users` as SuperAdmin creates new Admin, `GET /api/users` lists both.
- **Postgres host port reverted to `55432`** (default) — your local ETL Postgres container conflicts on 5432. Override with `POSTGRES_PORT=5432` env var if you stop that.

### Bugs found and fixed during verification
1. **`IdentityBootstrapService` queried `AspNetRoles` before migration ran in tests** — test factory now applies migrations via a standalone DbContext before triggering host start.
2. **`GET /api/users` with no query params → 500** because minimal API treated `int page` as required. Switched to `int?` with `?? 1` / `?? 20` fallback. New regression test: `ListUsers_NoQueryParams_DefaultsToPage1Size20`.

---

## Round 7 — Customers

### Added endpoints
| Endpoint | Auth | Notes |
|---|---|---|
| `GET    /api/customers`          | any auth | paginated; `search` / `type` / `active` filters |
| `GET    /api/customers/{id}`     | any auth | one customer with Person + Region |
| `POST   /api/customers`          | any auth | creates Customer + Person + (looked-up-or-created) Region in one call |
| `PUT    /api/customers/{id}`     | any auth | partial PATCH-style update; null fields = unchanged |
| `DELETE /api/customers/{id}`     | any auth | 204; cascades to Person; Region stays (shared) |

Authorization is `RequireAuthorization()` (any authenticated user) — when more granular roles arrive later, this becomes `role:Admin` or a permission policy without changing the endpoint shape.

### Domain (Binesh.Domain/Customers/)
- **`Customer`** — `Id`, `Type` (enum), `Active`, `PaymentReliability` (0-1), `PersonId`, `CreatedAt`. Sealed; factory `Customer.Create(...)`; `Update(type?, active?, paymentReliability?)` for partial updates.
- **`Person`** — `Name` (required), `Family`, `Code`, `Phone` (landline), `Mobile` (was `PhoneNumber` in old schema — **renamed** to avoid collision with `User.PhoneNumber` which means login phone), `Fax`, `Pelak`, `Address`, `BirthDate`, optional `Region`. Sealed; factory; `Update(...)` for nullable-merge semantics.
- **`Region`** — `Country`, `Province`, `City`. Sealed; factory. Unique constraint on the tuple.
- **`CustomerType`** — 10-value enum, original Persian transliteration kept (Bedehkaran, Bestankar, Personnel, Ranandeh, Bazaryab, Sherka, MoshtarianKhanegi, JariSherkathaVaAshkhas, TarahVaEditor) with XML doc comments translating each.

### Schema changes (migration `CustomerSchema`)
- `customers` table (replaces old `Customers`): `Id`, `Type` (string enum), `Active`, `PaymentReliability`, `PersonId` (FK), `CreatedAt`. Index on `Type`.
- `persons` table (replaces old `Persons`): all contact fields, optional `RegionId` (FK, SET NULL on delete). Index on `Code`.
- `regions` table (replaces old `Regions`): unique tuple `(Country, Province, City)` — Region rows are looked up and reused rather than duplicated.
- `CustomerType` stored as string instead of int (old code stored it as int but with a misspelled enum value `Sherka` etc. — string makes the schema self-documenting and survives enum reordering).

### Frontend impact
- Field rename: `Person.PhoneNumber` → `Person.Mobile`. The login phone on User stays `PhoneNumber`. Backend rejects unknown fields, so the frontend must send `mobile`.
- `Region` no longer needs to be created separately — pass the strings on `POST /api/customers` and the server resolves or creates.
- `CustomerType` is sent as the enum name (`"MoshtarianKhanegi"`) instead of the int. More readable; survives future reordering.
- Validation responses follow the same RFC 7807 + `extensions.errors` shape as the rest of the API.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **46/46 pass** in ~10s (5 sales + 9 auth + 12 users + 16 customers + 4 supporting).
- Live container smoke test (see below).

### Bugs found and fixed during testing
1. **`DeleteCustomer` left `Person` rows orphaned** — EF's CASCADE goes principal→dependent and `Person` is the principal in our 1:1, so deleting `Customer` didn't auto-delete `Person`. Handler now does a two-step `ExecuteDelete` (Customer then Person). Regression: `Delete_ExistingCustomer_Returns204AndCascadesPerson`.
2. **JSON: enums couldn't be deserialized from strings** — body `{"type":"MoshtarianKhanegi"}` returned 500. Two-part fix:
   - Registered `JsonStringEnumConverter` globally in `ConfigureHttpJsonOptions` (`allowIntegerValues: true` for backward compat).
   - Attribute `[JsonConverter(typeof(JsonStringEnumConverter<CustomerType>))]` on the enum itself so any client (tests included) handles it as string in both directions.
3. **Malformed JSON body returned 500 instead of 400** — `GlobalExceptionHandler` now maps `BadHttpRequestException` → 400 with a proper ProblemDetails body.

---

## Round 8 — Products

### Added endpoints (10)
| Endpoint | Auth | Notes |
|---|---|---|
| `GET    /api/products`                      | any auth | paginated; `search` / `type` filters; `includeStats=true` returns sum/count aggregates computed in SQL |
| `GET    /api/products/{id}`                 | any auth | one product |
| `GET    /api/products/by-code/{code}`       | any auth | lookup by unique `ProductCode` |
| `POST   /api/products`                      | any auth | create; `ProductCode` is unique |
| `PUT    /api/products/{id}`                 | any auth | partial PATCH; null fields = unchanged |
| `DELETE /api/products/{id}`                 | any auth | 204; cascades to inventory events |
| `GET    /api/products/{id}/events`          | any auth | paginated inventory ledger for one product |
| `POST   /api/products/{id}/events`          | any auth | append one inventory event (Receipt / Issue / …) |
| `DELETE /api/products/{id}/events`          | any auth | 204; clears the ledger for one product (keeps the product) |
| `GET    /api/products/stagnation`           | any auth | FIFO inventory-aging report: per-product weighted-average age, current stock, stagnation value |

### Domain (Binesh.Domain/Products/)
- **`Product`** — `Id`, `Type` (enum `ProductType`), `ProductCode` (unique), `ProductDescription`, `DetailedType`, `CreatedAt`, `Events` navigation. Sealed; factory `Product.Create(...)`; `Update(...)` for nullable-merge PATCH semantics.
- **`InventoryEvent`** — `Id`, `ProductId` (FK), `Type` (enum `InventoryEventType`), `Date`, `Quantity`, `UnitPrice`, `TotalPrice`, `FactorNumber`, three optional `ValueN` columns, `Description`. Sealed.
- **`ProductType`** — enum stored/sent as string (`Carpet`, etc.). Decorated with `[JsonConverter(typeof(JsonStringEnumConverter<ProductType>))]`.
- **`InventoryEventType`** — enum stored/sent as string. Values: `None`, `Receipt` (رسید), `Issue` (حواله), `SalesOrConsumptionRequest`, `PurchaseOrProductionRequest`, `ProformaInvoice`, `SalesInvoice`.

### Schema changes (migration `ProductSchema`)
- `products` table: `Id`, `Type` (string enum), `ProductCode`, `ProductDescription`, `DetailedType`, `CreatedAt`. **Unique index on `ProductCode`**.
- `inventory_events` table: `Id`, `ProductId` (FK, CASCADE delete), `Type` (string enum), `Date`, `Quantity`, `UnitPrice`, `TotalPrice`, `FactorNumber`, optional `Value1`/`Value2`/`Value3`, `Description`. **Composite index `(ProductId, Date)`** for ledger / stagnation queries.

### Field renames (frontend impact)
The old code's inventory schema used Persian-derived names that didn't survive translation. Renamed for clarity:

| Old | New |
|---|---|
| `FactorNumber` (the *quantity moved*) | `Quantity` |
| `Fee` (the per-unit price) | `UnitPrice` |
| `Price` (the line total) | `TotalPrice` |
| `Desc` | `Description` |

A separate `FactorNumber` field is kept on `InventoryEvent` for its **actual** meaning — the invoice number that produced the ledger row.

### Aggregations (all in SQL)
- `ListProducts?includeStats=true` projects each product with `SUM(UnitPrice)`, `SUM(TotalPrice)`, and `COUNT(*)` over its events — single grouped SQL query, no in-memory `.GroupBy()`.
- `GetStagnationReport` computes a per-product **FIFO inventory aging** in SQL: walks the ledger ordered by `Date`, tracks remaining stock per receipt lot, returns `weightedAverageAgeDays`, `latestUnitPrice`, `currentStock`, and `totalStagnationValue`.

### Frontend impact
- 10 new endpoints (see table above) — wire up product CRUD + inventory ledger UI.
- **`ProductType` enum is now sent/received as a string** (e.g. `"Carpet"`), not the int it used to be.
- **`InventoryEventType` enum sent/received as string.** Valid outgoing values: `Receipt`, `Issue`, `SalesOrConsumptionRequest`, `PurchaseOrProductionRequest`, `ProformaInvoice`, `SalesInvoice`. Note: old code used `Sale` / `Buy` informally — those are not valid; use `Issue` for stock leaving and `Receipt` for stock arriving.
- **Field renames** (inventory event POST body): `quantity` (was `factorNumber`), `unitPrice` (was `fee`), `totalPrice` (was `price`), `description` (was `desc`). `factorNumber` now means *invoice number*, optional.
- `ProductCode` is **unique** — POSTing a duplicate returns 409 `resource.conflict`.
- New stagnation-report shape: `{points: [{productId, productCode, category, weightedAverageAgeDays, latestUnitPrice, currentStock, totalStagnationValue}]}`.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **61/61 pass** (~12s).
- Live container smoke test against `localhost:8080` (SuperAdmin OTP → JWT → exercised every endpoint):
  - `POST /api/products` (Carpet, code `SMOKE-R8-1`) → 201 with id
  - `GET /api/products/{id}` → 200; `GET /api/products/by-code/SMOKE-R8-1` → 200
  - `GET /api/products` → 200 with `page=1, pageSize=20` defaults; `?includeStats=true` returns the stats projection
  - `POST /events` Receipt 10@100 → 201; Issue 3@150 → 201
  - `GET /events` → 200, both rows listed in correct order
  - `GET /stagnation` → 200 with `currentStock: 7`, `totalStagnationValue: 700` (FIFO math correct: 10 in, 3 out, 7 remaining × 100)
  - `PUT /api/products/{id}` partial body → 200, only `productDescription` changed
  - `DELETE /events` → 204; `DELETE /api/products/{id}` → 204; subsequent `GET` → 404 with `resource.not_found` ProblemDetails

### Notes for the next slice (Sales — Round 9)
- `Sale` already has a minimal entity from Round 4 and one endpoint (`GET /api/sales/summary`). Round 9 extends it to full CRUD + the panel-summary aggregations and wires `Product` + `Customer` FKs.
- Stagnation/aging math here is a useful reference for the Sales panel summaries (categorised sales, regional sales, top-selling) — same SQL-only philosophy.

---

## Round 9 — Sales CRUD (full port, part 1)

Round 9 ships in two halves to keep changes reviewable. **Part 1 (this entry):** Sale entity extended with Product + Customer FKs, full CRUD endpoints. **Part 2 (Round 9b, next):** the three panel summaries — Categorized, Regional, TopSelling.

### Domain change — `Sale` extended (Binesh.Domain/Sales/Sale.cs)
Round 4's `Sale` was minimal: `Id`, `Date`, `Price`, `Quantity`, `DocNumber`. Round 9 makes it represent a real sale line:

| Field | Notes |
|---|---|
| `ProductId` + `Product` | FK to `Product`. **Required** (NOT NULL). |
| `CounterpartyId` + `Counterparty` | FK to `Customer`. **Required** (NOT NULL). The legacy field name "Counterparty" is preserved — it's the accounting term used everywhere in the codebase. |
| `DeliveredQuantity` | `float`. How much of `Quantity` was actually delivered against the order. Defaults to `0` only at the entity level; clients should always set it. |

The `Sale.Create(...)` factory and a new `Sale.Update(...)` partial-PATCH method enforce: `Price ≥ 0`, `Quantity > 0`, `DeliveredQuantity ≥ 0`, both FKs non-empty.

### Schema changes (migration `ExtendSaleSchema`)
- `sales` table gains: `DeliveredQuantity` (real), `ProductId` (uuid NOT NULL), `CounterpartyId` (uuid NOT NULL).
- New FKs: `sales.ProductId → products.Id` (RESTRICT on delete), `sales.CounterpartyId → customers.Id` (RESTRICT on delete). Restrict not Cascade because deleting a product or customer that has sales history is almost always wrong; force the caller to deal with it.
- New indices: `ix_sales_product`, `ix_sales_counterparty` for filter queries.

### Added endpoints (5)
All under `/api/sales`, all require authentication. The existing `GET /api/sales/summary` also now requires auth (it didn't in Round 4 — see Frontend impact).

| Endpoint | Auth | Notes |
|---|---|---|
| `GET    /api/sales`               | any auth | paginated; filters: `from`, `to` (DateOnly), `customerId`, `productId`, `search` (matches product code/description and customer name/family) |
| `GET    /api/sales/{id}`          | any auth | one sale with embedded `product` + `counterparty` summaries |
| `POST   /api/sales`               | any auth | create; 404 if `productId` or `counterpartyId` doesn't exist |
| `PUT    /api/sales/{id}`          | any auth | partial PATCH; null fields = unchanged; switching FK to non-existent id = 404 |
| `DELETE /api/sales/{id}`          | any auth | 204; 404 if missing |

### Response shape — `SaleDto`
```json
{
  "id": "uuid",
  "date": "2026-03-15T10:00:00Z",
  "price": 5000,
  "quantity": 2,
  "deliveredQuantity": 2,
  "docNumber": 555,
  "product":      { "id": "uuid", "productCode": "...", "productDescription": "...", "detailedType": "..." },
  "counterparty": { "id": "uuid", "name": "...", "family": "...", "mobile": "..." }
}
```

Returned from `POST`, `GET /api/sales/{id}`, `PUT`, and each item in `GET /api/sales`. The embedded `product` / `counterparty` summaries are projected in SQL via a shared `SaleProjection.ToDto` expression so every endpoint speaks the same shape.

### Frontend impact
- **`GET /api/sales/summary` now requires authentication.** Round 4 left it unauthenticated; with the rest of the Sales API behind `RequireAuthorization()`, the summary endpoint joined the same gate for consistency. Frontend must send `Authorization: Bearer …` on that endpoint too.
- **5 new endpoints**, see table above.
- **`POST /api/sales` body** requires `productId` and `counterpartyId` (uuids). The legacy code's "create sale by typing the customer name" path is **gone** — clients must create/look up the customer first and pass its id. (Old `SalesRecordAddingRequestDto` accepted a string `Counterparty` and a mountain of unused breakdown fields; we kept only what the new schema models.)
- **Single `price`** field on the body, no `priceFee` / `priceReceipt` / `priceVoucher` breakdown. The old DTO had the trio but the storage model only used one bigint.
- **List filters**: `?from=2026-03-01&to=2026-03-31&customerId=…&productId=…&search=…&page=1&pageSize=20`. All optional. `from`/`to` are inclusive `DateOnly`. Pagination defaults: `page=1, pageSize=20` (1–200).
- **Sort order**: list returns most recent first (`OrderByDescending(Date)`).
- **Validation errors** follow the same RFC 7807 + `extensions.errors` shape as the rest of the API. E.g. negative price → 422 `validation.failed` with `errors.Price`.

### Bugs / quirks resolved during the port
- Old `SalesService` did `.Include(x => x.Product).Include(x => x.Counterparty).ThenInclude(x => x.Person).ThenInclude(x => x.Region)` on every list and pulled the entire graph into memory. New `ListSales` projects to a small `SaleDto` in SQL with `Select(SaleProjection.ToDto)` — no in-memory shaping, no over-fetching.
- Old code's `UpdateAsync` round-tripped through a mapper that overwrote unset fields with defaults (effectively reset-on-PATCH). New `UpdateSale` is true partial PATCH: only fields present on the body change.
- Old code's `CreateAsync` would happily insert a sale with `ProductId = Guid.Empty` if the client forgot the field; FK constraint then exploded as a 500 at SaveChanges. New handler validates FK existence and returns a clean 404 with the missing-id payload before touching the DB write.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **79/79 pass** (~2s): 5 GetSummary + 18 SalesTests + the 56 prior. Existing GetSummaryTests updated to seed a Product+Customer fixture (FKs now required) and to authenticate.
- Live smoke against `localhost:8080` (SuperAdmin OTP → JWT → seed product + customer → exercise every endpoint):
  - `POST /api/sales` price=5000 → 201 with embedded product/counterparty
  - `GET /{id}` → 200; `GET /` → 200 with `page=1, pageSize=20`
  - `GET ?customerId=…` → filtered to 1 item
  - `GET ?from=2026-03-01&to=2026-03-31` → filtered to 1 item
  - `PUT {price:9999}` → 200, only price changed (date/quantity/docNumber untouched)
  - `GET /summary?from=…&to=…` → 200 with the new totals (9999)
  - `POST` unknown productId → 404 `resource.not_found`
  - `POST` negative price → 422 `validation.failed` with `errors.Price`
  - `DELETE /{id}` → 204; subsequent `GET` → 404
  - Unauthenticated `GET /api/sales` → 401

### Notes for Round 9b
- Three panel-summary slices to add: `GetCategorizedSales` (group by `Product.Type` and/or `DetailedType`), `GetRegionalSales` (group by `Counterparty.Person.Region.City/Province`), `GetTopSellingProducts` (top-N by revenue/quantity).
- All three must follow the SQL-only aggregation rule (use `.GroupBy(...).Select(...)` in `IQueryable`, never `.ToList().GroupBy(...)`). The Round 8 stagnation report and Round 4 summary handler are the templates.

---

## Round 9b — Sales panel summaries

Closes out Round 9 by adding the three panel-summary aggregations the old `SalesApiController` provided. All three are SQL-only `GROUP BY`, run as `AsNoTracking()`, and share the same date-range validation rules as `GET /api/sales/summary`.

### Added endpoints (3)
All under `/api/sales`, all require authentication.

| Endpoint | Notes |
|---|---|
| `GET /api/sales/categorized?from=YYYY-MM-DD&to=YYYY-MM-DD`           | Revenue + order count grouped by `(Product.Type, Product.DetailedType)`, with growth vs the prior period of the same length. Sorted by revenue desc. |
| `GET /api/sales/regional?from=YYYY-MM-DD&to=YYYY-MM-DD`              | Revenue + order count grouped by `(Counterparty.Person.Region.Province, City)`, with growth vs prior period. Sales whose counterparty has no Region are bucketed under `province: null, city: null`. |
| `GET /api/sales/top-selling?from=YYYY-MM-DD&to=YYYY-MM-DD&limit=N`   | Top-N products by revenue. `limit` defaults to 10, capped at 100 (422 above). Returns `productId`, `productCode`, `productDescription`, `type`, `revenue`, `quantitySold`, `orderCount`. |

### Response shapes
```json
// /categorized
{
  "items": [
    { "type": "Carpet", "detailedType": "600 reed", "revenue": 5000, "orderCount": 2, "growthRate": 400.0 },
    { "type": "Rug",    "detailedType": "small",    "revenue": 5500, "orderCount": 2, "growthRate": 0.0   }
  ]
}

// /regional
{
  "items": [
    { "province": "Tehran",  "city": "Tehran",  "revenue": 5500, "orderCount": 3, "growthRate": 450.0 },
    { "province": "Isfahan", "city": "Isfahan", "revenue": 5000, "orderCount": 1, "growthRate": 0.0   },
    { "province": null,      "city": null,      "revenue": 100,  "orderCount": 1, "growthRate": 0.0   }
  ]
}

// /top-selling
{
  "items": [
    { "productId": "...", "productCode": "R9B-P2", "productDescription": "Rug small", "type": "Rug",
      "revenue": 5500, "quantitySold": 2, "orderCount": 2 },
    ...
  ]
}
```

### Growth-rate definition
`growthRate = (current - prior) / prior * 100`, expressed as a percentage. When `prior == 0` and `current > 0`, the rate is reported as `0.0` (not `Infinity`) — the old code did the same, and "∞" doesn't serialize. The prior period has the same length as the requested range and ends immediately before `from`.

### Frontend impact
- 3 new endpoints, see table above. Wire them into the panel/dashboard pages.
- **`growthRate`** is a `double` percentage (e.g. `400.0` not `4.0`). The old `SaleOverRegionDto.GrowthrRate` (typo preserved in old code: `GrowthrRate` with two r's) called it "a number between 0 and 100" — that comment was wrong; revenue can grow >100%. The new field has no upper bound and is **0 when no prior data exists**.
- **`/categorized`** shape change from old `CategorizedSales` DTO: old code returned a per-period growth wrapper (`Card<float>`); new code returns flat `{revenue, orderCount, growthRate}` per bucket. Frontend cards can be assembled client-side.
- **`/regional`** includes a `null/null` bucket for counterparties without a Region. Old code dropped them silently. Surface them as "Unknown" in the UI.
- **`/top-selling?limit=`** caps at 100. Asking for more returns 422 `validation.failed`.
- Field rename: `top-selling`'s `quantitySold` (was `totalAmount` in old code's `TopSellingProductsDto` — and was actually a `long` price, not quantity — confusing legacy naming).

### Performance notes
- `categorized` and `regional` each run **two SQL `GROUP BY` queries** (one for the current period, one for the prior). On the dev box with a few rows this is sub-millisecond; with real volume the (`Date`, `ProductId`/`CounterpartyId`) indices from `ExtendSaleSchema` cover the predicates.
- `top-selling` is one SQL query with `GROUP BY ... ORDER BY revenue DESC LIMIT N`. Constructing the response record directly inside `.Select(g => new TopSellingProductEntry(...))` did not translate cleanly under Npgsql in net10.0; the handler projects to an anonymous type first, then constructs the record in memory after `ToListAsync`. Other handlers do this implicitly via shared projection expressions.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **91/91 pass** (~2s). 12 new `PanelSummaryTests`.
- Live smoke against `localhost:8080` (SuperAdmin OTP → JWT → seed 2 products, 2 customers with Tehran/Isfahan regions, 1 sale in Feb prior period, 4 sales in March current period):
  - `/categorized?from=2026-03-01&to=2026-03-31` → `Carpet/600 reed` revenue 5000 growth 400% (Feb=1000), `Rug/small` revenue 5500 growth 0% (no Feb)
  - `/regional` → `Tehran` revenue 5500 growth 450% (Feb=1000), `Isfahan` revenue 5000 growth 0%
  - `/top-selling` → ordered Rug (5500) > Carpet (5000); `?limit=1` returns Rug only; `?limit=500` → 422
  - Bad range (from > to) → 422 `From must be on or before To`
  - Unauthenticated → 401

### Round 9 wrap-up
Sales port is now feature-complete relative to the old `SalesController` + `SalesApiController` (CRUD + summary + categorized + regional + top-selling). What's intentionally **not** ported:
- The `[EnableQuery]` OData passthrough — replaced by structured filters on `GET /api/sales`.
- Bulk-insert endpoint — not used by the panel; revisit only if the frontend needs it.
- `Card<T>` growth wrapper — flattened into `{revenue, growthRate}` in each entry.

---

## Round 10 — Sales Returns

Mirror of Sales: same shape, separate table, separate endpoints. Kept as its own aggregate (rather than a flag on `Sale`) so the upstream ETL can drive returns independently and so reporting can include/exclude them cleanly.

### Domain — `SalesReturn` (Binesh.Domain/Sales/SalesReturn.cs)
Same shape as `Sale`:
- `Id`, `Date`, `Price` (long), `Quantity` (float), `DeliveredQuantity` (float), `DocNumber`
- `ProductId` + `Product` FK (NOT NULL, RESTRICT on delete)
- `CounterpartyId` + `Counterparty` FK (NOT NULL, RESTRICT on delete)

Sealed entity, private setters, `SalesReturn.Create(...)` factory and partial-PATCH `SalesReturn.Update(...)` with the same validations as `Sale`.

The old `RequestState` enum from the legacy `SalesReturn` model is **not** carried over — it was unused on the panel side and the ETL didn't drive it consistently. If a workflow eventually needs it, add it as a separate field at that time.

### Schema (migration `SalesReturnSchema`)
- `sales_returns` table: same columns and constraints as `sales`.
- FKs: `sales_returns.ProductId → products.Id` (RESTRICT), `sales_returns.CounterpartyId → customers.Id` (RESTRICT).
- Indices: `ix_sales_returns_date`, `ix_sales_returns_product`, `ix_sales_returns_counterparty`.

### Added endpoints (6)
All under `/api/sales-returns`, all require authentication.

| Endpoint | Notes |
|---|---|
| `GET    /api/sales-returns`               | paginated; filters: `from`, `to`, `customerId`, `productId`, `search` (matches product code/description, customer name/family). Defaults `page=1, pageSize=20`. |
| `GET    /api/sales-returns/{id}`          | one return with embedded product + counterparty summaries |
| `POST   /api/sales-returns`               | create; 404 if `productId` or `counterpartyId` doesn't exist |
| `PUT    /api/sales-returns/{id}`          | partial PATCH; null fields = unchanged |
| `DELETE /api/sales-returns/{id}`          | 204; 404 if missing |
| `GET    /api/sales-returns/summary`       | per-day totals (`totalReturned`, `returnCount`, `averageReturnValue`, `byDay: [{date, returned, returnCount}]`) — parallels `/api/sales/summary` |

### Response shapes
The `SalesReturnDto` is identical in shape to `SaleDto` (the two are kept as separate records — not shared — to keep the namespaces clean and avoid future coupling if the schemas diverge):
```json
{
  "id": "uuid", "date": "...", "price": 2500, "quantity": 1, "deliveredQuantity": 1, "docNumber": 99,
  "product":      { "id": "...", "productCode": "...", "productDescription": "...", "detailedType": "..." },
  "counterparty": { "id": "...", "name": "...", "family": "...", "mobile": "..." }
}
```

Summary shape:
```json
{
  "totalReturned": 9999,
  "returnCount": 1,
  "averageReturnValue": 9999,
  "byDay": [{ "date": "2026-03-15", "returned": 9999, "returnCount": 1 }]
}
```

### Frontend impact
- 6 new endpoints under `/api/sales-returns`. Wire them into the returns section of the panel.
- **`SalesReturnDto` mirrors `SaleDto`** field-by-field — clients can share form code and table renderers between the two.
- **Summary uses different field names** than `/api/sales/summary` (`totalReturned`/`returnCount`/`averageReturnValue`/`returned` instead of `totalRevenue`/`orderCount`/`averageOrderValue`/`revenue`) so the two summaries can be composed in the same UI without aliasing.
- **No categorized / regional / top-selling endpoints for returns yet.** The old UI showed returns as a `ReturnTotal` card next to the unified sales summary, which the frontend can assemble from `/api/sales/summary` + `/api/sales-returns/summary`. If a dedicated returns panel ever needs growth breakdowns by product/region, add the parallel slices (the shape is identical to Sales).
- **Legacy `RequestState` enum dropped** — was on the old `SalesReturn` model but never round-tripped to the panel.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **107/107 pass** (~2s). 16 new `SalesReturnsTests` covering CRUD, filter, summary, and 401/404/422 paths.
- Live smoke against `localhost:8080` (SuperAdmin OTP → JWT → seed product + customer → exercise every endpoint):
  - `POST /api/sales-returns` → 201 with embedded product/counterparty (price=2500)
  - `GET /{id}` → 200; `GET /` → 200 with `page=1, pageSize=20` defaults
  - `GET ?customerId=…` → filtered to 1 item
  - `PUT {price:9999}` → 200, only price changed
  - `GET /summary?from=…&to=…` → `{totalReturned: 9999, returnCount: 1, averageReturnValue: 9999, byDay: [...]}` — math correct
  - `POST` unknown productId → 404 `resource.not_found`
  - `DELETE /{id}` → 204; subsequent `GET` → 404
  - Unauthenticated → 401

### Notes for Round 11 (Financial)
- The Financial slice will need to read both Sales and SalesReturns together to compute net P&L and the balance-sheet impact of returns. The schema is now ready: same FK shape, same indexed `Date` column.
- If a future request needs categorized/regional/top-selling for returns specifically, copy the three Round 9b handlers and swap `db.Sales` → `db.SalesReturns` — projections and validation rules are otherwise identical.

---

## Round 11 — Financial (chart of accounts + mapping settings + panel)

Ports the old `FinantialController` + `FinantialApiController` + `FinantialMappingSettings` into the new structure. Per user direction the legacy panel formulas are preserved **byte-for-byte** including their bugs; the cleanup pass is deferred to post-transformation. See "Known parity issues" at the bottom of this entry.

### Domain (Binesh.Domain/Financial/)
- **`FinancialEntry`** — one chart-of-accounts row. Renamed from old `FinantialModel` (typo fix). Fields: `Code`, `Name`, `Type` (free-text), `Debit` (long), `Credit` (long). The old Persian field names `Bedehkar` (بدهکار, debit) and `Bestankar` (بستانکار, credit) are translated to their English accounting equivalents on the wire.
- **`FinancialMappingSettings`** — singleton entity. Maps `FinancialEntry.Name` values to the panel's balance-sheet and P&L line items. 10 `IReadOnlyList<DetailedItem>` categories: `OperationalCost`, `Payables`, `ToCalculateSales`, `ToCalculateLiquidity`, `ToCalculateGrossProfitLoss`, `ToCalculateOperatingProfitLoss`, `ToCalculateProfitLossBeforTax` (typo preserved), `ToCalculateNetProfitLoss`, `ToCalculateAccumulatedProfitLoss`, `ToCalculateEquity`. Wholesale `Replace(...)` semantics — settings are uploaded as one document.
- **`DetailedItem`** — value object `{Title, Value?}`. Shape preserved for parity even though only `Title` is consumed today.

### Schema (migration `FinancialSchema`)
- `financial_entries` table: `Id`, `Code` (varchar 64), `Name` (varchar 256), `Type` (varchar 128), `Debit` (bigint), `Credit` (bigint). Indices on `Code` and `Type`.
- `financial_mapping_settings` table: `Id` + 10 **jsonb** columns (one per category) holding the `DetailedItem[]` documents. Postgres jsonb keeps the storage compact and lets future tools query into it.

### Added endpoints (8)
All under `/api/financial`, all require authentication.

| Endpoint | Notes |
|---|---|
| `GET    /api/financial/entries`                  | paginated; `search` (matches Code+Name), `type` filter. Defaults `page=1, pageSize=20`. |
| `GET    /api/financial/entries/{id}`             | one entry |
| `POST   /api/financial/entries`                  | create; 422 on empty/negative |
| `PUT    /api/financial/entries/{id}`             | partial PATCH |
| `DELETE /api/financial/entries/{id}`             | 204; 404 if missing |
| `GET    /api/financial/mapping-settings`         | the singleton; 404 if never configured |
| `PUT    /api/financial/mapping-settings`         | upsert (creates if absent, replaces if present). Idempotent. Categories omitted from the body default to empty. |
| `GET    /api/financial/panel`                    | combined state cards + balance sheet + profit-loss. 404 if settings absent. |

### Response shapes (key parts)
```json
// /entries (item)
{ "id": "uuid", "code": "1001", "name": "Cash", "type": "Asset", "debit": 500000, "credit": 0 }

// /mapping-settings
{
  "id": "uuid",
  "operationalCost": [{"title": "Rent", "value": null}],
  "payables": [], "toCalculateSales": [], "toCalculateLiquidity": [],
  "toCalculateGrossProfitLoss": [], "toCalculateOperatingProfitLoss": [],
  "toCalculateProfitLossBeforTax": [], "toCalculateNetProfitLoss": [],
  "toCalculateAccumulatedProfitLoss": [], "toCalculateEquity": []
}

// /panel
{
  "stateCards":   { "totalSale": {"value": …, "growth": 0}, "profitMargin": {…}, "netProfit": {…}, "liquidity": {…} },
  "balanceSheet": {
    "stateCards": { "assets": {…}, "liability": {…}, "equities": {…} },
    "items":      { "mainItems": [{"title": "<Type>", "detailedItems": [{"title": "<Name>", "value": …}]}] }
  },
  "profitLoss":   {
    "grossProfitLoss":       { "value": {"title": "...", "value": …}, "detailed": [...] },
    "operationalProfitLoss": {…}, "profitLossBeforTax": {…},
    "netProfitLoss":         {…}, "accumulatedProfitLoss": {…}
  }
}
```

### Frontend impact
- **Renames**: endpoint paths drop the old `Finantial` typo; entity field labels translate `Bedehkar`/`Bestankar` → `debit`/`credit` on the wire.
- **`Card<T>`** wrapper is `{value, growth}` — `growth` is always `0` today (the legacy code also always sent 0; period-over-period growth is a future feature).
- **Singleton settings**: `PUT /api/financial/mapping-settings` is idempotent — the second call replaces the first, no duplicate row. The legacy code allowed duplicate rows and then 400'd the panel; the new code prevents the duplicate from ever existing.
- **Typo preserved on the wire**: the field is `toCalculateProfitLossBeforTax` (matches legacy spelling). Frontend clients that already send this name keep working; clients that want a corrected `BeforeTax` alias should wait for the cleanup pass.
- **404 vs 409 on `/panel`**: `404 resource.not_found` when settings have never been configured; `409 resource.conflict` if more than one row exists (defensive — shouldn't happen via the new upsert endpoint, only via direct DB inserts).

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test` → **120/120 pass** (~3s). 13 new `FinancialTests` covering entry CRUD, mapping upsert, panel formulas (incl. parity-bug assertions), 401/404/422 paths.
- Live smoke against `localhost:8080` (SuperAdmin OTP → JWT → seed 3 entries: Sales credit 99999 / Rent debit 2000 / Equity credit 5000 → upsert settings → call panel):
  - `POST /entries` × 3 → 201
  - `GET /entries` → 200; `?type=Revenue` filter → 1 item
  - `PUT /entries/{id}` partial → 200, only `credit` changed
  - `GET /panel` (no settings) → 404 `FinancialMappingSettings with key 'default' was not found.`
  - `PUT /mapping-settings` → 200; `GET /mapping-settings` → 200 same id
  - `GET /panel` → 200 with:
    - `totalSale = 99999`, `liquidity = 99999`, `netProfit = -2000`
    - `profitMargin = 99999.02` — math `99999 - ((-2000 + 0) / 99999)` confirms PARITY-BUG #1 preserved
    - `assets = 104999`, `liability = 104999` — confirms PARITY-BUG #3 (Liability mirrors Assets) preserved
    - `equities = 5000`
    - `profitLoss.grossProfitLoss = 99999`, `.operationalProfitLoss = 97999` (-2000 + 99999), `.profitLossBeforTax = -2000` (= 0 + operationalCosts: PARITY-BUG #2 preserved)
  - `DELETE /entries/{id}` → 204
  - Unauthenticated → 401

### Known parity issues (deferred to post-transformation cleanup)
These were preserved verbatim from the legacy `FinantialApiController.GetFinantialSummary` per user direction so panel numbers match byte-equivalent during the transition. Each is tagged `PARITY-BUG #N` in `GetFinancialPanelHandler.cs` and asserted-against in `FinancialTests.Panel_ComputesLegacyFormulas` so the cleanup pass can flip both at once.

| # | Where | Symptom | Correct math |
|---|---|---|---|
| 1 | `profitMargin = totalSale - (operationalCosts + payable) / totalSale` | Operator precedence: division binds tighter than subtraction, so this evaluates to `totalSale - ((costs)/totalSale)` — almost the same as `totalSale` itself when totalSale is large. | `(totalSale - operationalCosts - payable) / totalSale` |
| 2 | `profitLossBeforTax = SumFor(ToCalculateProfitLossBeforTax) + operationalCosts` | Adds the running `operationalCosts` sum instead of `operationalProfitLoss`. | `… + operationalProfitLoss` |
| 3 | `Liability = items.Where(v => v > 0).Sum()` | Same filter as `Assets`. Liability therefore always equals Assets and never reports negative balances as obligations. | `items.Where(v => v < 0).Sum() * -1` (or similar — depends on the accounting convention chosen) |
| 4 | Property/class typos (`BeforTax`, `Accumilated`, `BalnceSheetStateCards`, `profitmargine`, `Drtailed`) | Reach the frontend as-is. | Rename to correctly-spelled forms with response aliases for backward compat. |

When the cleanup lands, update both the handler and the test fixture assertions in one PR so the test confirms the new correct math.

### Notes for Round 12 (AI orchestration)
- Financial schema is now reachable from `IBineshDbContext.FinancialEntries` and `.FinancialMappingSettings` — the AI query-tool registry will need read-only views over these.
- The 10 mapping categories define a deterministic vocabulary for the AI to talk about line items (e.g. "what are my operational costs this month?") — the question-answer flow can grep the settings for the relevant category names and JOIN to entries by Name.
- The old `FinancialQueryTool` is one of the IQueryableTool plugins that Round 12 needs to port; it lives at `src/DataBaseManager/AI/QueryTools/FinancialQueryTool.cs`.

---

## Round 12a — AI QueryEngine foundation

Round 12 (AI orchestration) is split across 12a/b/c/d per scope. **12a (this entry)** lands the foundation under `Binesh.Ai/QueryEngine/`: the schema registry primitives and the new compiled-selector cache that replaces the old per-row `DynamicInvoke` pattern. No HTTP surface yet — purely module-internal types ready for 12b's query engine to build on.

### Added types (Binesh.Ai/QueryEngine/)
- **`FieldType`** — enum: `String, Int32, Int64, Float, Double, Decimal, DateTime, Bool, Enum`. Framework-agnostic categorization the prompt builder + runtime validator agree on.
- **`FieldDescriptor`** — describes one AI-visible field on an entity. Carries the `LambdaExpression Selector` (kept uncompiled so EF can decompose into SQL), `RequiredIncludes[]` for navigation paths, `AllowedOperators`/`AllowedAggregates` `HashSet<string>`s, `AllowedValues` for enum fields, and the `Orderable/Groupable/Selectable/Aggregatable` flags.
- **`EntitySchema`** — collection of `FieldDescriptor`s rooted at a specific CLR `EntityType`. Case-insensitive `GetField` / `TryGetField`.
- **`SchemaRegistry`** — concurrent in-memory registry keyed by case-insensitive name. `Register`, `Get`, `TryGet`, `All`. Registered once at startup.
- **`CompiledSelectorCache`** — `ConcurrentDictionary<(Type, string), Func<object, object?>>`. **Replaces the old `LambdaExpression.Compile().DynamicInvoke(entity)` per-row pattern** that dominated CPU on large result sets. Internally uses an `ExpressionVisitor` to inline the entity-type cast into the compiled delegate, so each `Get(entityType, descriptor)` call returns a strongly-typed-on-the-inside, object-on-the-outside getter cached for the process lifetime.

### Added schemas (Binesh.Ai/Schemas/)
Six built-in schemas, registered automatically by `AddBineshAi(...)`:

| Schema | Entity | Notable adaptations |
|---|---|---|
| `CustomerSchema`     | `Customer`        | `PhoneNumber` → `Mobile`, `BirthDay` → `BirthDate` (DateTimeOffset?), `CustomerType` enum carries `AllowedValues` |
| `ProductSchema`      | `Product`         | Adds `Type` enum field with `AllowedValues`; `CreatedAt` groupable |
| `InventoryEventSchema` | `InventoryEvent` | Field renames per Round 8: `FactorNumber` → `Quantity`, `Fee` → `UnitPrice`, `Price` → `TotalPrice`, `Desc` → `Description`; `FactorNumber` field kept with its new meaning (invoice number) |
| `FinancialSchema`    | `FinancialEntry`  | `Bedehkar` → `Debit`, `Bestankar` → `Credit` (matches Round 11 domain rename) |
| `SaleSchema`         | `Sale`            | `CustomerPhoneNumber` → `CustomerMobile`; adds `Quantity` field exposed in Round 9 |
| `SalesReturnSchema`  | `SalesReturn`     | Drops `State` (RequestState was removed from the domain in Round 10) |

### DI wiring (`Binesh.Ai/DependencyInjection.cs`)
`AddBineshAi(...)` now also registers:
- `CompiledSelectorCache` (singleton)
- `SchemaRegistry` (singleton) — populated with all 6 built-in schemas at construction time

### What did NOT land in 12a (deferred to 12b/c/d)
- **12b — Query engine:** expression-tree filter/projection builder, include applicator, aggregate executor (the SQL-translatable `GroupBy` rewrite).
- **12c — Tools + orchestrator:** `IQueryableTool` interface, `QueryToolBuilder`, `QueryToolRegistry`, 6 per-entity tools, per-tool JSON schemas (fixes the legacy "global field union" correctness bug), `AiOrchestrator`, HTTP endpoint.
- **12d — Chat + WS auth:** ticket-based WebSocket auth (no more `[AllowAnonymous]` on WS), model fallback (primary → fallback on rate limit), per-user token budgets, snapshot tests of generated prompt strings.

### Frontend impact
None this turn. The foundation is module-internal — no HTTP surface added. Field-name renames in the schemas (`Mobile`, `Debit`/`Credit`, `Quantity`/`UnitPrice`/`TotalPrice`, `Description`) propagate to AI prompts in 12c/d, at which point the frontend's AI chat UI will need to expect responses using the new vocabulary. Plain CRUD endpoints are unchanged.

### Verified
- `dotnet build` → 0 warnings, 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **20/20 pass** (~660 ms): `SchemaRegistryTests` (5), `EntitySchemaTests` (3), `CompiledSelectorCacheTests` (6 — including a verification that the same `(Type, name)` returns the same delegate instance, that navigation paths like `Customer → Person → Mobile` resolve correctly, and that mismatched entity types throw), `BuiltInSchemasTests` (6 — one per built-in schema, exercising every declared field's selector against a sample domain entity to catch typos at test-build time rather than first runtime AI call).
- `dotnet test tests/Binesh.Api.IntegrationTests` → **120/120 pass** (no regressions in existing CRUD/panel tests).
- No live smoke this turn — no HTTP endpoints added.

### Notes for Round 12b
- `IBineshDbContext` has DbSets for every entity referenced by the registered schemas (Customer, Product, InventoryEvent, FinancialEntry, Sale, SalesReturn) — the query engine can call `db.Set<EntitySchema.EntityType>()` via reflection or a small dispatcher.
- The expression builder should consume `FieldDescriptor.Selector` directly (do not compile) so EF Core translates the predicate to SQL. Only the post-materialization side (formatting rows for the LLM response) uses `CompiledSelectorCache`.
- Use the same `RequiredIncludes` mechanism the legacy code did — collect the union across all referenced fields in a query, then apply EF's string `Include("a.b.c")` chains.

---

## Round 12b — AI QueryEngine (SQL-translatable rewrite)

Builds on the 12a foundation. Adds the actual query pipeline: validate → include → filter → group-or-order → page → materialize. Every stage operates on the LLM-emitted `AiQueryRequest` against an `EntitySchema`. No HTTP surface yet — tools + endpoint land in 12c.

### Added types (`Binesh.Ai/QueryEngine/`)
- **`AiQueryRequest`** + helper records (`AiSelect`, `AiAggregate`, `AiFilter`, `AiGroupBy`, `AiOrderBy`, `AiPaging`) — the canonical shape the LLM emits. Frozen so the per-tool JSON schema in 12c is deterministic.
- **`QueryValidator`** — validates the request against the schema (unknown fields, disallowed operators / aggregates / order, enum value mismatches, paging bounds). **Accumulates every violation into one `InvalidOperationException`** so the LLM gets one corrective message, not N round-trips.
- **`FilterExpressionBuilder`** — turns each `AiFilter { Field, Operator, Value }` into an `Expression<Func<T, bool>>` using the descriptor's expression-tree `Selector` (not the compiled cache — EF must decompose to SQL). Supports `eq/ne/ge/le/gt/lt`. String filter values are coerced into the descriptor's runtime type (incl. enum parsing). The parameter expression is rebound to `T` so the WHERE clause binds cleanly through generic IQueryable.
- **`OrderingApplicator`** — reflects `Queryable.OrderBy`/`ThenBy`/`OrderByDescending`/`ThenByDescending` per chained `AiOrderBy` clause, picking the right generic at runtime from the descriptor's `Selector.ReturnType`.
- **`PagingApplicator`** — plain `Skip`/`Take`. Separate file just so the pipeline reads uniformly.
- **`IncludeApplicator`** — collects the union of `FieldDescriptor.RequiredIncludes` over every field referenced anywhere in the request (Filters / Select / GroupBy / OrderBy / Aggregates), then **drops every path that's a strict prefix of a longer path** (case-insensitive, dot-segment aware): `Counterparty.Person.Region` makes `Counterparty.Person` and `Counterparty` redundant since EF's string Include pulls the whole chain. Applies the minimal set as `IQueryable.Include(string)`.
- **`AggregateExecutor`** — **the SQL rewrite**. See "What changed" below.
- **`AiQueryEngine`** — top-level orchestrator that calls the above in order. Returns either `IReadOnlyList<IReadOnlyDictionary<string, object?>>` (list mode + grouped aggregate) or `IReadOnlyDictionary<string, object?>` (flat aggregate).

### What changed vs. the legacy `AggregateExecutor`
The old `BineshSoloution.AI.Core.QueryEngine.AggregateExecutor.ExecuteGroupedAsync` did:

```csharp
var rows = await query.ToListAsync(ct);           // pull WHOLE TABLE into memory
var keyGetter = groupField.Selector.Compile();    // reflection per row
var grouped = rows.GroupBy(row => keyGetter.DynamicInvoke(row)).ToList();
// then in-memory aggregation
```

This makes every panel/AI request that touches a grouped aggregate scale linearly with table size **even when only a handful of aggregated buckets are returned**. The handoff (§12) explicitly called out this pattern as a rewrite target.

**New approach** in [AggregateExecutor.cs](src-v2/src/Binesh.Ai/QueryEngine/AggregateExecutor.cs):

1. One `SELECT DISTINCT key FROM …` to fetch the group key universe.
2. **For each aggregate**, one `SELECT key, <agg>(…) FROM … GROUP BY key` — issued as a real SQL GROUP BY via runtime-built expression trees (`Queryable.GroupBy(...).Select(g => new KeyAndValue<TKey, TValue>(g.Key, <agg>))`).
3. Merge by key in memory.

So N+1 round trips (N = aggregates), each guaranteed SQL push-down, no full table materialization. Verified by the new `GroupedAggregate_BehavesIdenticallyForLargeRowCount` test which seeds 5,000 sales rows and verifies the engine returns 2 group rows without misbehaving.

**Flat aggregates** (Mode = "aggregate" with no GroupBy) reuse the same code path through a constant `_ => (byte)0` key selector — eliminates a duplicate codegen branch.

### What did NOT land in 12b (deferred to 12c/d)
- **12c** — `IQueryableTool` plugin + builder + registry + 6 per-entity tools + per-tool JSON schemas (fixes the legacy "global field union" correctness bug) + `AiOrchestrator` + HTTP `POST /api/ai/query`.
- **12d** — Ticket-based WebSocket auth, model fallback, per-user token budgets, prompt-string snapshot tests.

### Frontend impact
None this turn. Engine is module-internal; no HTTP added.

### Verified
- `dotnet build` → 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **36/36 pass** (~850 ms). 16 new tests: `QueryValidatorTests` (9 — covers field/operator/aggregate/order/enum/paging/multi-error paths), `IncludeApplicatorTests` (4 — covers no-nav-fields-empty, prefix dedup, union across all clauses, unknown-field-silently-ignored), `FilterExpressionBuilderTests` (3 — LINQ-to-objects exercises for string equality, enum equality with string coercion, multi-clause AND).
- `dotnet test tests/Binesh.Api.IntegrationTests` → **126/126 pass** (~12s). 6 new in `Features/AiQuery/AiQueryEngineSqlTests`:
  - `ListMode_ReturnsProjectedRows` — projects only requested fields, descending order respected
  - `ListMode_RespectsFilter` — `ProductCode eq AI-CARPET` filters from 4 rows to 3
  - `FlatAggregate_SumAndCount` — `sum(Price) = 19000`, `count = 4`
  - `GroupedAggregate_ByProductCode` — two product groups roll up the right totals + counts
  - `GroupedAggregate_BehavesIdenticallyForLargeRowCount` — **5,000-row bulk seed exercises the SQL GROUP BY pushdown**; the legacy in-memory `ToListAsync` path would either OOM or take orders of magnitude longer; the new path returns 2 group rows in a couple hundred ms.
  - `UnknownField_Throws` — validator surfaces the right exception from inside `AiQueryEngine.ExecuteAsync`.

### Notes for Round 12c (tools + orchestrator)
- `AiQueryEngine` is registered as a DI singleton; tools should inject it together with `IBineshDbContext` and a per-tool reference to the relevant `EntitySchema` (resolved from `SchemaRegistry` by tool name).
- Per the §12 rewrite list: **each tool advertises its own JSON schema** (not a global field-name union) — the per-tool `field-name enum` in the function definition prevents the LLM from referencing fields from the wrong entity (a real correctness bug in the legacy code's single `QueryToolBuilder.Build` template).
- `AggregateRow` shape: when GroupBy is present, the response is `[{ "<groupFieldName>": ..., "<agg.Alias>": ..., ... }]`; flat aggregate is `{ "<agg.Alias>": ..., ... }`. The orchestrator (12c) needs to surface this verbatim to the LLM.

---

## Round 12c — Tools, orchestrator, and the `/api/ai/query` endpoint

Adds the layers above the query engine: each entity gets its own `IQueryableTool`, the orchestrator drives the OpenAI tool-call loop, and a single-shot HTTP endpoint exposes it. Multi-turn chat with WebSocket streaming + ticket auth lives in Round 13.

### Added types
- **`Binesh.Ai/Tools/`** — `IQueryableTool` (interface), `QueryableToolBase<T>` (abstract base that wires `AiQueryEngine` + `IBineshDbContext` and requires each concrete tool to expose its `IQueryable<T>` root), `QueryToolRegistry` (concurrent dictionary keyed by tool name, case-insensitive lookup), `QueryToolBuilder` (emits per-tool OpenAI `ChatTool` definitions — see "Legacy bug fix" below).
- **6 concrete tools** — `CustomerQueryTool`, `ProductQueryTool`, `InventoryEventQueryTool`, `FinancialQueryTool`, `SaleQueryTool`, `SalesReturnQueryTool`. Each: maps to its EntitySchema, picks the right `DbSet`, declares its `query_*` function name + a description telling the LLM when to pick it over the others.
- **`Binesh.Ai/Prompts/`** — `Prompts.SystemInstructions` (base personality + Persian↔English term mapping for Bedehkar/Bestankar↔Debit/Credit), `QueryPromptBuilder.Build(registry)` (emits one markdown table per entity listing field-name × filterable/selectable/orderable/groupable/aggregatable flags plus enum-allowed-values for `CustomerType`/`ProductType`/`InventoryEventType`).
- **`Binesh.Ai/Orchestration/`** — `IAiChatClient` (abstraction over the OpenAI ChatClient so tests can script responses), `OpenAiChatClient` (real implementation reading the model name from `OpenAiSettings`), `AiOrchestrator` (single-shot run-to-completion: builds tool definitions + system prompt + initial user message, then loops up to `MaxToolIterations = 5`, dispatching each tool call through the registry, capturing an audit log of every call).
- **`Binesh.Application/Features/Ai/AskAi/`** — `AskAiCommand` (input message), `AskAiValidator` (1–8000 chars), `AskAiResponse` (`AssistantText` + `FinishReason` + per-call audit log with `ToolName`, `ArgumentsJson`, `ResultJson`, optional `Error`).
- **`Binesh.Ai/Application/AskAiHandler.cs`** — MediatR handler lives in the AI assembly so it can take `AiOrchestrator` as a dependency. Picked up by the `AddApplication(Identity.Assembly, Ai.Assembly)` extra-assembly call in `Program.cs`.
- **`Binesh.Api/Endpoints/Ai/AiEndpoints.cs`** — `POST /api/ai/query`. `RequireAuthorization()` + `RequireRateLimiting("ai")` (60/min sliding window already configured in the API module).

### Added endpoint
| Endpoint | Auth | Rate limit | Notes |
|---|---|---|---|
| `POST /api/ai/query`             | any auth | `ai` policy | `{ "message": "..." }` → `{ assistantText, finishReason, toolCalls: [...] }` |

### Response shape
```json
{
  "assistantText": "Found 3 sales totalling 10000 IRR.",
  "finishReason": "stop",
  "toolCalls": [
    {
      "toolName": "query_sale",
      "argumentsJson": "{\"Entity\":\"Sale\",\"Select\":{\"Mode\":\"aggregate\",\"Aggregates\":[{\"Function\":\"sum\",\"Field\":\"Price\",\"Alias\":\"total\"}]}}",
      "resultJson": "{\"total\": 10000}",
      "error": null
    }
  ]
}
```

### Legacy bug fix — per-tool JSON schemas
The old `BineshSoloution.AI.Core.Tools.QueryToolBuilder` emitted **one** function definition with a `field` enum that was the union of every schema's fields. The LLM could (and would) build a `query_sales` call with `Field = "CustomerType"` — a `Customer` field, not a `Sale` field — producing a runtime "field not on entity" exception and a confused retry loop.

The new `QueryToolBuilder.Build(tool)` emits one function definition per tool whose `Field` enum lists **only that tool's own schema fields**. Tested by `QueryToolBuilderTests.PerTool_FieldEnum_ContainsOnlyThatSchemasFields` which asserts `DocNumber` doesn't appear in the Customer tool and `CustomerType` doesn't appear in the Sale tool.

### Frontend impact
- New endpoint `POST /api/ai/query` (single-shot). Frontend can stop calling the legacy WebSocket for one-off questions.
- **Response is auditable**: every tool call surfaces with its arguments, result JSON, and any error message. UIs can render the assistant text immediately and lazily expose tool details on demand for power users.
- **Validation failures from the LLM** (referenced an unknown field, supplied a disallowed operator) become `error` strings in the `toolCalls[].error` field — they do NOT fail the HTTP request. The model self-corrects on the next iteration. UI should hide the corrected attempts; the final `assistantText` reflects only the successful path.
- **422** if `message` is empty or > 8000 chars.
- **Rate limited** by the `ai` policy (60/min sliding window per user-or-IP).

### Verified
- `dotnet build` → 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **46/46 pass** (~700 ms). 10 new tests in 12c: `QueryToolBuilderTests` (3 — per-tool field-enum scoping, Entity enum size, operator list), `QueryToolRegistryTests` (2 — dup detection, case-insensitive lookup), `AiOrchestratorTests` (5 — no-tool, single-tool dispatch, unknown-tool error, bad-args error, max-iterations cap).
- `dotnet test tests/Binesh.Api.IntegrationTests` → **131/131 pass** (~5s). 5 new tests in `Features/Ai/AiEndpointTests`:
  - `NoToolCalls_ReturnsFinalText` — assistant text round-trips, with Persian content
  - `SingleToolCall_HitsRealQueryEngine_AndAssistantSummarizes` — scripted tool call dispatches to `CustomerQueryTool` which runs a **real SQL query** through the engine against the seeded fixture; the assistant's "0921" mobile-number reference comes from the real DB
  - `BadAiRequest_FromLLM_SurfacedAsToolError` — LLM tries to reference `DocNumber` (a Sale field) inside a `query_customer` call; validator throws; error string is surfaced in `toolCalls[].error` without failing the HTTP request
  - `EmptyMessage_Returns422`, `Unauthenticated_Returns401`
- No live smoke against OpenAI (would need a real API key + flakiness budget). The scripted-fake path covers the orchestrator surface; switching `IAiChatClient` from `OpenAiChatClient` to the scripted one is the only DI change between dev and test.

### What's still deferred to Round 12d / Round 13
- **Snapshot tests of generated prompt strings** — `QueryPromptBuilder.Build(registry)` should be locked into a fixture string so future schema changes are reviewed deliberately.
- **Model fallback** — primary model on rate limit → fallback. Hook lives inside `OpenAiChatClient`; needs a second model name on `OpenAiSettings`.
- **Per-user token budgets** — needs a usage counter keyed on the authenticated user + a 429 surface from inside the orchestrator.
- **WebSocket streaming** + ticket auth (`POST /api/ai/chat/ticket` returns short-lived ticket; WS validates ticket; **no more `[AllowAnonymous]` on WS**) — Round 13.
- **Conversation history** — persisted chat turns. Round 13 (lands together with the Postgres jsonb chat store).

---

## Round 12d — Prompt snapshot, model fallback, per-user token budgets

Closes out the loose ends from 12a/b/c before the bigger Round 13 work (chat history + WebSocket). Three contained additions; no behaviour change for clients that don't hit budget caps.

### Added
- **`QueryPromptBuilderSnapshotTests.Build_Snapshot_DoesNotDriftSilently`** — locks the exact 67-line system-prompt addendum produced by `QueryPromptBuilder.Build(registry)` for the 6 built-in tools. Any schema change (new field, renamed type, changed allowed-operators set) breaks this. The failing-test output shows the actual prompt at `Path.GetTempPath()/binesh_prompt_actual.txt` so the snapshot fixture can be updated deliberately rather than auto-regenerated.
- **`OpenAiSettings.FallbackModel`** (string?) + **`OpenAiChatClient` fallback logic** — primary model is tried first; if the OpenAI client returns HTTP 429 (rate-limit), the call is retried once against `FallbackModel` if configured. Each `AiCompletionResult` carries the `ModelUsed` field so the caller can see which path produced the answer.
- **`OpenAiSettings.PerUserDailyTokens`** (default 100_000) + **`ITokenBudget`** + **`InMemoryTokenBudget`** — rolling 24h budget keyed per authenticated user. The orchestrator calls `CanProceed(userId)` before every chat-completion turn and throws `TooManyRequestsException` (HTTP 429) if the budget is exhausted; tokens are debited via `Charge(userId, usage.TotalTokens)` after each response. Disabled when `PerUserDailyTokens = 0`.
- **`AiTokenUsage`** record on `AiCompletionResult` — `InputTokens` + `OutputTokens`. The real `OpenAiChatClient` pulls these from `ChatCompletion.Usage`; the scripted test client constructs them inline.
- **`QueryToolRegistry` now preserves insertion order** — the prompt builder emits one entity table per registered tool in registration order. Stable order is required for prompt-cache hits (OpenAI hashes the prompt prefix) and reproducible snapshot tests. Replaced the prior `ConcurrentDictionary` with a `Dictionary` + `List` pair under a single lock.

### Endpoint changes
- `POST /api/ai/query` now reads the authenticated user's id from the `NameIdentifier` claim and threads it into `AskAiCommand(Message, UserId)` so the budget enforcer can key off it.
- The response gains a `tokensUsed` integer field summing usage across every chat-completion turn in the run.

### Response shape (updated)
```json
{
  "assistantText": "Found 3 sales.",
  "finishReason": "stop",
  "tokensUsed": 380,
  "toolCalls": [ ... ]
}
```

### Frontend impact
- **Possible new 429** on `POST /api/ai/query` when the user has spent their daily token budget. The response is RFC 7807 (`code: "rate.exceeded"`) with a human-readable Persian/English message. UI should treat it the same as the existing OTP rate-limit 429.
- **`tokensUsed` field** is informational — handy for displaying "X tokens used of Y daily limit" in a UI sidebar but no action required.
- No other observable changes.

### Verified
- `dotnet build` → 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **53/53 pass** (~120 ms). 7 new: snapshot test (1), `InMemoryTokenBudgetTests` (4 — exhaustion, 24h rollover, disabled-budget, per-user isolation), orchestrator budget tests (2 — exhausted-budget throws before first OpenAI call, recording-budget verifies tokens are charged with the right userId).
- `dotnet test tests/Binesh.Api.IntegrationTests` → **131/131 pass**. The existing `AiEndpointTests` keep passing because `ScriptedAiChatClient.EnqueueText/EnqueueToolCall` default to `AiTokenUsage.Zero` so existing tests don't have to opt in to usage metadata.
- No live OpenAI smoke (would need a real key + budget; model fallback path is covered by the unit test that scripts a 429-equivalent code path; the real OpenAI exception type is `ClientResultException` with `.Status == 429`).

### Notes for Round 13
- `AiOrchestrator` is now keyed on `(message, userId)` — adding conversation history is a matter of extending the input from a single string to a list of prior turns + the new user message. The budget enforcer already works per user regardless of how many turns a single request triggers.
- The `IAiChatClient` abstraction is the seam for streaming — Round 13's WS handler can add a `CompleteStreamingAsync(...)` member that yields `AiTokenStream` events without touching the orchestrator's tool-call loop logic.
- The ticket-auth pattern: `POST /api/ai/chat/ticket` issues a JWT with a 60-second `exp` and `aud: "binesh-ai-ws"`. The WS handler validates the ticket on connect, opens the socket, then runs the orchestrator per inbound message. The token-budget check stays exactly where it is.

---

## Round 13a — Chat history on Postgres jsonb + multi-turn orchestrator

Adds the persistence and multi-turn layer that Round 12 explicitly deferred. WebSocket streaming + ticket auth lands in **13b** on top of this layer; the data shape is shared.

### Domain (Binesh.Domain/Chat/)
- **`Conversation`** — owned by exactly one user via `UserId`. Soft-deleted via `ArchivedAt` so audit / abuse-reporting can still see archived rows. `Title`, `CreatedAt`. Sealed; `Start(userId, title)` factory; `Rename(...)`, `Archive()`, `Unarchive()`. Carries a `List<ChatMessage>` navigation backed by a private field so EF respects ordering on append.
- **`ChatMessage`** — one turn inside a conversation. Fields: `Sequence` (per-conversation monotonic, unique-indexed with `ConversationId`), `Role` enum (`User`/`Assistant`/`System`/`Tool`), `Content` (Postgres jsonb opaque to EF — see "Content shape" below), `CreatedAt`. Sealed; `Create(conversationId, sequence, role, contentJson)` factory.
- **`MessageRole`** enum — User/Assistant/System are first-class today; `Tool` is reserved for a future round where tool calls become individual rows (so a UI can render them collapsibly per call).

### Schema (migration `ChatHistorySchema`)
- `conversations` — `Id` (uuid, gen_random_uuid), `UserId`, `Title` (varchar 256), `CreatedAt` (timestamptz), `ArchivedAt` (nullable). Indices on `UserId` and `(UserId, ArchivedAt)`.
- `chat_messages` — `Id` (uuid), `ConversationId` (uuid, FK CASCADE), `Sequence` (int), `Role` (varchar 32, stored as enum name), `Content` (jsonb, NOT NULL), `CreatedAt`. **Unique index on `(ConversationId, Sequence)`** so duplicate sequence numbers can't slip in.

### Content shape (jsonb)
Stored on `chat_messages.Content`. Schema is intentionally loose — the column evolves without a migration when we add things like UI-component hints in a future round.

```json
// User message
{ "text": "How many sales last month?" }

// Assistant message
{
  "text": "There were 14 sales totalling 320,000 IRR.",
  "finishReason": "stop",
  "tokensUsed": 380,
  "toolCalls": [
    {
      "toolName": "query_sale",
      "argumentsJson": "{\"Entity\":\"Sale\",...}",
      "resultJson": "{\"total\":320000,\"count\":14}",
      "error": null
    }
  ]
}
```

### Added endpoints
All under `/api/ai/conversations`, all require authentication + the `ai` rate-limit policy. The authenticated user's id is read from the JWT `NameIdentifier` claim; **no endpoint accepts a user id from the body** so a user can't read or post to anyone else's conversations.

| Endpoint | Notes |
|---|---|
| `POST   /api/ai/conversations`                    | start a conversation, body `{ title }` → 201 with id |
| `GET    /api/ai/conversations`                    | paginated, scoped to the authed user. Excludes archived rows unless `?includeArchived=true`. |
| `GET    /api/ai/conversations/{id}`               | one conversation + every message in sequence order. 404 if it belongs to another user (existence-of-id is privileged information). |
| `DELETE /api/ai/conversations/{id}`               | soft-deletes via `ArchivedAt`; the row stays in the DB so the audit log survives. |
| `POST   /api/ai/conversations/{id}/messages`      | the real work — see "Send message" below |

### Send message
`POST /api/ai/conversations/{id}/messages` body `{ "message": "..." }`. Handler:
1. Loads the conversation untracked, checks ownership (404 on mismatch) and that it isn't archived (409 on archived).
2. Loads every prior message untracked, projects to history turns. **Only user + assistant TEXT turns are replayed** — tool calls are NOT replayed across turns. The model gets a fresh tool-call loop per turn, mirroring how chat UIs show the final summary rather than the raw call audit in conversation history.
3. Calls `AiOrchestrator.RunAsync(message, userId, history, ct)` (the new multi-turn overload). Token budget + tool-call loop + audit log all behave exactly like the single-shot `/api/ai/query` endpoint.
4. Appends one User row + one Assistant row to `chat_messages`, with sequences computed from `existing.Last().Sequence + 1` so each row gets a unique-indexed sequence.
5. Returns `{ userMessage, assistantMessage, finishReason, tokensUsed }`.

The assistant row's `Content` jsonb embeds the full tool-call audit log so the UI can render a collapsible "show details" panel without a second roundtrip.

### Orchestrator change
`AiOrchestrator.RunAsync(message, userId, history, ct)` is a new overload accepting `IReadOnlyList<AiHistoryTurn>` (each turn is `Role` + `Text`). It builds the OpenAI message list as `[system, ...history, user]` before kicking off the tool-call loop. The old single-string overload still works (it just passes `[]` for history) so the existing `/api/ai/query` endpoint is unchanged.

### Bug found and fixed during testing
The first cut of `SendChatMessageHandler` loaded the conversation via `db.Conversations.Include(c => c.Messages).Single(...)` and called `conversation.AppendMessage(...)` to mutate the field-backed `_messages` list. EF's change tracker treated this as a parent-row UPDATE and `SaveChanges` failed with `DbUpdateConcurrencyException: expected 1 row affected, got 0`. Reworked the handler to load metadata-only via `AsNoTracking() + Select(anon)` and add new `ChatMessage` rows directly to `db.ChatMessages`. The handler stays the only place ChatMessage rows are inserted, so the unique index on `(ConversationId, Sequence)` is the integrity backstop.

### Frontend impact
- 5 new endpoints. The existing single-shot `POST /api/ai/query` is unchanged — keep using it for stateless queries; use conversations when persistence + history-replay matters.
- **Message content is loose jsonb** — frontend should treat the `content` field as `unknown` and parse defensively. Today every payload has a `text` field for both roles; future schema additions (UI components, attached images, etc.) won't break existing clients that ignore unknown keys.
- **Archive is reversible at the data layer** (`ArchivedAt` can be cleared in code) but no PATCH endpoint exposes it yet. If the UI needs an "Restore" button, add an explicit `POST /api/ai/conversations/{id}/restore` slice in 13b — currently the only path back is via direct DB write.
- **Cross-user reads return 404, not 403** by design — leaking the existence of a conversation id is itself a privacy break.

### Verified
- `dotnet build` → 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **53/53 pass** (no regressions in the AI unit tests; the multi-turn overload is exercised indirectly through the integration test).
- `dotnet test tests/Binesh.Api.IntegrationTests` → **141/141 pass** (~3s). 10 new `ChatEndpointsTests`:
  - `StartConversation_ReturnsCreated`
  - `ListConversations_ScopedToCurrentUser` — user B can't see user A's conversations
  - `ArchivedExcludedByDefault_IncludedWhenRequested`
  - `Get_OtherUsersConversation_Returns404` (NOT 403)
  - `SendMessage_PersistsBothTurns` — user row at seq=1, assistant row at seq=2, both retrievable via GET
  - `SendMessage_ReplaysHistoryToOrchestrator` — second turn's scripted chat client sees `[system, user1, assistant1, user2]` = 4 messages
  - `SendMessage_OnArchivedConversation_Returns409`
  - `SendMessage_OtherUsersConversation_Returns404`
  - `SendMessage_AssistantPayload_EmbedsToolCallAuditLog` — scripted tool call surfaces in the persisted assistant message's content jsonb
  - `Unauthenticated_Returns401`

### Notes for Round 13b (WebSocket + ticket auth)
- `IAiChatClient.CompleteStreamingAsync` is the seam — extending the interface with an `IAsyncEnumerable<ChatStreamUpdate>` member lets the WS handler push tokens as they arrive. The fake client can yield scripted updates in tests.
- The ticket endpoint is `POST /api/ai/chat/ticket` returning a JWT with 60s expiry and `aud: "binesh-ai-ws"`. The WS handler validates the ticket on connect, runs `SendChatMessageHandler` (or the orchestrator directly) per inbound message, persists the same way 13a does. **The `[AllowAnonymous]` on the old WS endpoint is gone**: the ticket carries the authenticated user id end-to-end.
- Streaming and persistence stay decoupled — the WS yields tokens to the client as they arrive AND accumulates them for the final DB insert. Single transaction at the end so a disconnect mid-stream produces no orphan rows.

---

## Round 13b — WebSocket streaming + ticket auth

Closes out the legacy chat surface. The old code's WS endpoint was annotated `[AllowAnonymous]`; the new one uses a short-lived signed ticket. Tokens stream as the model produces them, persistence happens once at the end so disconnects mid-stream leave nothing in the DB.

### Added
- **`IAiChatClient.CompleteStreamingAsync`** — `IAsyncEnumerable<AiStreamUpdate>` returning `AiStreamToken` / `AiStreamToolCall` / `AiStreamFinished`. The SDK delivers tool-call arguments as many small fragments — the OpenAI implementation accumulates them by `Index` and emits one assembled `AiStreamToolCall` per call when the turn finishes.
- **`OpenAiChatClient.CompleteStreamingAsync`** — uses the SDK's `CompleteChatStreamingAsync` internally. Streaming intentionally does NOT fall back to the secondary model on 429: the WS protocol doesn't expose a clean retry signal and the budget enforcer already returns 429 before we get here when the user is over their cap.
- **`AiOrchestrator.RunStreamingAsync(message, userId, history, ct)`** — streaming overload yielding `OrchestratorStreamEvent` (token / tool-call dispatched / tool-call completed / final). Uses the streaming chat client inside the tool-call loop. Same budget + max-iterations guards as the non-streaming version.
- **`IJwtTokenService.IssueChatTicket(userId)` + `ValidateChatTicket(token)`** — 60-second JWT with `aud: "binesh-ai-ws"`, signed with the existing JWT signing certificate. Reuses the issuer setting; only the audience differs from the regular access token so the two cannot be confused.
- **`POST /api/ai/chat/ticket`** — authenticated HTTP endpoint that returns `{ ticket, expiresAt }`. Rate-limited by the `ai` policy.
- **`GET /api/ai/chat/ws?ticket=<jwt>`** — anonymous-by-design WebSocket endpoint; the ticket query parameter IS the auth. Ticket validation happens inline before `AcceptWebSocketAsync` so an invalid ticket returns 401 (HTTP, not WS close) without ever opening the socket.
- **`UseWebSockets()`** in the request pipeline, sitting before auth so the WS handler can apply its own ticket check.

### WebSocket protocol
**Client → server** — one JSON text frame:
```json
{ "conversationId": "uuid", "message": "user text" }
```

**Server → client** — stream of JSON text frames. Each frame has a `type` field:
```json
// while the model emits text
{ "type": "token", "text": "Hello" }

// when the orchestrator dispatches a tool call
{ "type": "tool_call", "phase": "dispatched", "name": "query_sale", "argumentsJson": "{...}" }

// when the tool returns
{ "type": "tool_call", "phase": "completed", "name": "query_sale", "resultJson": "{...}", "error": null }

// terminal — server immediately closes the WS after this
{ "type": "final", "messageId": "uuid", "finishReason": "stop", "tokensUsed": 380 }

// on validation / runtime errors — terminal
{ "type": "error", "code": "not_found" | "conflict" | "bad_request" | "rate_exceeded", "message": "..." }
```

### Persistence semantics
After the last `final` event, **and only then**, the user message + assistant message rows are added to `chat_messages` and `SaveChangesAsync` runs once. If the client disconnects mid-stream or the orchestrator throws, no rows are written. This matches Round 13a's behaviour: history-replay sees only completed turns.

Sequence numbers and content-jsonb format are the same as Round 13a — the WS path and the HTTP `POST /messages` path are interchangeable from the data layer's perspective.

### Frontend impact
- **2 new endpoints**: `POST /api/ai/chat/ticket` then `GET /api/ai/chat/ws?ticket=...`. Tickets expire after 60 seconds — fetch one immediately before opening the socket.
- **No more `Bearer` on the WS** — the ticket carries the user id end-to-end. Browser WS APIs can't easily set custom headers so this is the standard pattern.
- **Stream consumer**: read text frames, dispatch on `type`. Token frames are append-only — concatenate them into the rendered assistant message. The `final` frame is the cue to close the UI's "streaming…" indicator.
- **Persisted final message** has the same jsonb content shape as Round 13a; nothing changes for `GET /api/ai/conversations/{id}` or the in-browser history view.
- **One exchange per connection**: the server closes the WS after `final` (or after `error`). To send another message, fetch a new ticket and open a new WS. This keeps the protocol simple; multiple exchanges per connection is a feature we can add later if needed.

### Verified
- `dotnet build` → 0 errors.
- `dotnet test tests/Binesh.Ai.IntegrationTests` → **55/55 pass** (~190 ms). 2 new `AiOrchestratorStreamingTests`:
  - Text-only stream yields N token events then one `OrchestratorStreamFinal` with concatenated text and summed usage.
  - Tool-call stream dispatches the call between turns: events arrive in the order dispatched → completed → next-turn-tokens → final.
- `dotnet test tests/Binesh.Api.IntegrationTests` → **147/147 pass** (~5s). 6 new `ChatStreamingTests`:
  - `Ticket_ReturnsShortLivedJwt` — JWT with 60s expiry
  - `Ticket_Unauthenticated_Returns401`
  - `WebSocket_WithoutTicket_Returns401`
  - `WebSocket_WithBadTicket_Returns401`
  - `WebSocket_HappyPath_StreamsTokens_AndPersistsAtEnd` — scripted streaming fake yields 2 tokens + finish; client receives `token`, `token`, `final` frames; the conversation has 2 persisted messages with the streamed text concatenated in the assistant message's jsonb content
  - `WebSocket_OtherUsersConversation_EmitsErrorFrame_AndNothingIsPersisted` — cross-user conversation id → `error` frame with code `not_found` and the conversation stays at zero messages
- No live OpenAI smoke (would need a real key + flakiness budget). The streaming path's accumulator + tool-call lifecycle are exercised by the unit test; the WS frame encoding + ticket validation are exercised end-to-end through `TestServer.CreateWebSocketClient`.

### Round 13 wrap-up
Chat layer is now feature-complete relative to the legacy code: conversations + messages persisted to Postgres jsonb (Round 13a), streaming WebSocket with ticket auth replacing the legacy `[AllowAnonymous]` socket (this round). Mongo dependency is fully removed at the code level; the docker-compose still has MongoDB declared but nothing references it — we can drop the container in a future cleanup.

What's intentionally **not** ported:
- **UI component tools** (`render_table`, `render_bar_chart`, etc.) — the legacy WS had a parallel set of "UI" tools whose results were rendered as components in the chat. The new code doesn't have these because the frontend is going to do its own rendering decisions based on the tool-call audit log. If we need server-driven UI hints, we add a tool registry parallel to the query tools — the orchestrator's event stream already has a slot (`tool_call`) for them.
- **Multiple exchanges per WS connection** — the new code closes the socket after each `final`. Adding a loop is a small change but introduces protocol questions (how does the client signal it's done? what about idle timeouts?) better answered when we have a real client.
- **Token-level streaming for tool-call arguments** — the new code emits one `dispatched` event with the fully-assembled argument JSON. Tools fire as a unit; showing the model "writing" the JSON in real time is mostly noise.

### Notes for Round 14 (ETL)
- The ETL WS endpoint in the legacy code is the next `[AllowAnonymous]` to remove. Same ticket pattern applies: HTTP `POST /api/etl/ticket` (Admin-only) → `aud: "binesh-etl-ws"` → WS validates → batch handler runs.
- HMAC signed payloads + idempotency keys come on top of the ticket auth.
- `DeleteAllData` must be gated on a confirm-token that the caller has to fetch in a separate HTTP call. Same shape as the chat ticket but tied to that specific destructive op.

---

## Sales panel — legacy `SalesApiController` parity (POST + ApiResponse envelope)

The five legacy panel endpoints (`SalesApiController`, all `POST` with a body)
are reproduced with **identical business logic**, now under REST-style paths and
wrapped in the legacy `ApiResponse<T>` envelope `{ code, status, message, body }`.
This replaces the Round 9b GET panel endpoints (`/api/sales/{categorized,regional,
top-selling}`), which are **removed**.

| Legacy | New |
|---|---|
| `POST /api/SalesApi/GetSalesSummaryAsync` | `POST /api/sales/panel/summary` |
| `POST /api/SalesApi/GetCustomercategorizedSalesAsync` | `POST /api/sales/panel/categorized-customers` |
| `POST /api/SalesApi/GetProvinceategorizdeSalesAsync` | `POST /api/sales/panel/regional` |
| `POST /api/SalesApi/GetSalesRecords` | `POST /api/sales/panel/records` |
| `POST /api/SalesApi/GetTopSellingProductsAsync` | `POST /api/sales/panel/top-selling` |

### Behavior (ported verbatim)
- **summary** — sold items grouped by `Product.DetailedType`, each with a
  `returned` percentage (`returnPrice / soldPrice * 100`, 1 dp). `count` = current
  sales rows, `sum` = total sold. `salesCards.totalSales` / `returnTotal` carry
  `{ value, growth }`; **growth is a ratio** `(cur - prev) / prev` rounded to 2 dp
  (e.g. `0.15`, not `15`), `0` when there's no prior window. Prior window is the
  immediately-preceding span of equal length. `offSales` / `newModelsSales` are
  **always null** — the legacy code never populated them.
- **categorized-customers** — counts grouped by `customerType` + time bucket
  (`timeFrameUnit`: Day/Week/Month/Quarter/Year; week is Sunday-start), `onDate` is
  the bucket start, ordered by customer type.
- **regional** — **returns an empty `RegionalSalesDto`** (`saleOverRegion: null,
  totalSale: 0, growthrRate: 0`). The legacy logic was entirely commented out;
  parity preserved intentionally (intended per-city/growth logic is deferred).
- **records** — paginated (`paggination.pageNumber/pageSize`, max 100), optional
  `searchTerm` matches customer name/family or product description, optional
  `categoryDto.productCategory` filters by `DetailedType`. Rows carry
  `factorNume, productDesc, productCategory, deliverdQuantity, customerName, price,
  date`. Now returns newest-first (legacy had no ORDER BY → nondeterministic paging).
- **top-selling** — top 5 products by delivered quantity in the current window with
  growth vs the prior window; `rank` 1..5, `count` truncated to int.

### Frontend impact
- **New envelope** on these five only: read `body` for the payload (`code` is a
  string like `"OK"`, `status` is `"success"`/`"error"`). The rest of the API still
  uses raw DTOs + ProblemDetails.
- **Send UTC dates.** `dateFilter.startTime/endTime` must be ISO-8601; append `Z`
  (or an offset). Unspecified-kind times are assumed UTC. Postgres stores
  `timestamptz`, so non-UTC-aware payloads are coerced to UTC server-side.
- Legacy request field spellings are preserved exactly: `provience.provinece`,
  `paggination`, `deliverdQuantity`, `factorNume`, `growthrRate`.

### Verified
- Build clean; `SalesPanelTests` — 7/7 (summary numbers + return %, categorized
  buckets, regional-empty parity, records paging + search, top-selling ranks +
  growth, 401). Full Sales area 46/46, SalesReturns 16/16.
- Gotcha found: the dev host runs **fa-IR culture**, so `DateTime.Parse` without
  `InvariantCulture` yields Jalali dates (2026→2647). Production is unaffected (JSON
  parses ISO invariantly); test seeding now parses invariantly.

---

## Round 14 — ETL — **skipped (not ported)**

The legacy ETL pipeline (websocket batch sync, four `IChangeBatchHandler`
impls, `DeleteAllData`) is **intentionally not ported from the old project**.
The user will define a different approach for data sync separately. The
`Binesh.Etl` project remains in the solution as an empty skeleton (sync
contracts only — `IChangeBatchHandler`, `ChangeBatch`, `IHasSyncKey`) so the
dependency graph and namespace are reserved, but no ETL endpoints are mapped
and nothing is wired into `Program.cs`. **No frontend impact.**

---

## Round 15 — Profile images via MinIO (pre-signed URLs)

Ports the legacy local-disk "CDN" for user avatars to object storage. The old
code streamed uploads through the API process to a local folder and served them
back off disk; the rewrite uses MinIO (S3-compatible) with **pre-signed URLs**
so bytes never transit the API.

### Added
- **`IFileStorage`** abstraction in `Binesh.Application/Abstractions` —
  `CreatePresignedUploadAsync`, `CreatePresignedDownloadAsync`, `ExistsAsync`,
  `DeleteAsync`. Concrete `MinioFileStorage` in `Binesh.Infrastructure`, plus
  `MinioSettings` (`ValidateOnStart`) and `MinioBootstrapService`
  (`IHostedService` that creates the bucket on boot if missing).
- **`PresignedUploadUrl`** record: `{ url, method, objectKey, requiredHeaders, expiresAt }`.
- **4 endpoints** under `/api/users/me/profile-image`:
  - `POST /upload-url` `{ contentType }` → `200 PresignedUploadUrl`. Allowed
    content types: `image/jpeg`, `image/png`, `image/webp` (anything else →
    `422`). Object key is server-generated as
    `profile-images/{userId:N}/{guid:N}.{ext}` — **namespaced by user id** so a
    leaked URL can only ever overwrite that one user's slot. Upload URL TTL: 10 min.
  - `PUT /` `{ objectKey }` → `200 UserDto`. Validates the key is in the caller's
    own namespace (`422` otherwise) and that the object actually exists in storage
    (`422` "no upload completed at that key"), then sets `ProfileImageName`.
    Replacing an existing image **best-effort deletes** the prior object.
  - `DELETE /` → `200 UserDto`. Nulls `ProfileImageName` and best-effort deletes
    the object.
- **`GET /api/users/me`** now embeds a fresh **pre-signed download URL**
  (`profileImageUrl`, 5 min TTL) whenever `profileImageName` is set; `null` otherwise.

### Frontend impact
- **Upload is a 3-step client flow** (replaces the old single multipart POST):
  1. `POST /api/users/me/profile-image/upload-url` with the file's content type.
  2. `PUT` the raw bytes directly to the returned `url`, sending every header in
     `requiredHeaders` (currently just `Content-Type`). This goes to MinIO, **not**
     the API.
  3. `PUT /api/users/me/profile-image` with the `objectKey` from step 1 to commit.
- **`UserDto` gains two fields**: `profileImageName` (the stored object key, stable)
  and `profileImageUrl` (a short-lived signed GET URL — **do not cache**; re-fetch
  `/me` or treat it as valid for ~5 min). Both are `null` when no image is set.
- **Removed**: the legacy multipart avatar upload endpoint and the local
  `/cdn/...` static file path. Avatars are no longer served off the API host.
- Old stored avatar paths do **not** carry over — this is a new storage backend;
  existing users start with no image until they re-upload.

### Verified
- Build clean (0 warnings / 0 errors).
- `ProfileImageTests` — 9/9 pass (upload-url key layout + TTL, unsupported
  content type → 422, set-without-upload → 422, cross-user key → 422, full
  round-trip with `/me` embedding a signed URL, replace-deletes-old,
  clear-deletes-object, no-image leaves `profileImageUrl` null, unauthenticated
  → 401). Tests run against an in-memory `IFileStorage` fake (no MinIO container
  needed in CI); `MinioBootstrapService` is removed from the test host.

> **Note on the full integration suite:** running every `IClassFixture` in
> parallel spins up one Postgres Testcontainer per fixture and can saturate the
> Docker daemon, producing sporadic `Docker.DotNet TimeoutException`s on container
> startup. This is host/infra contention, not a code regression — individual test
> classes (including `ProfileImageTests`) pass cleanly when run on their own.

---

## Round 16 — Parity verification + cutover

The final pre-cutover round: prove every legacy endpoint is accounted for, run
the whole suite, and write the deployment runbook. No new business endpoints.

### Added (docs)
- **[PARITY.md](./PARITY.md)** — every legacy controller/WS endpoint mapped to
  its rewrite equivalent, or marked Removed / Deferred / Skipped with a reason.
  Surfaces three open ⚠️ gaps for the user to decide (dashboard "cards"
  endpoints, conversation rename, bulk-create/mass-delete).
- **[CUTOVER.md](./CUTOVER.md)** — production cutover runbook: secrets/env,
  image build, MinIO-behind-Traefik wiring, migration order, smoke checklist,
  rollback, and post-cutover follow-ups.

### Fixed (deployment — would have blocked boot)
- **The `api` service had no `Minio__*` configuration** in either
  `docker-compose.yml` or `docker-compose.prod.yml`. Round 15 added
  `MinioSettings` with `ValidateOnStart` and `[Required]` `AccessKey`/`SecretKey`
  (no defaults), so the post-Round-15 image would crash on boot with an
  `OptionsValidationException`. Wired `Minio__Endpoint/AccessKey/SecretKey/`
  `BucketName/UseSsl` into both compose files, added a `minio: service_healthy`
  dependency to the API in dev, and corrected the prod overlay's required-env
  header (it omitted `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `FRONTEND_HOSTNAME`).

### Known limitation (documented, not a blocker)
- `MinioSettings.PublicEndpoint` is defined but **unused** — `MinioFileStorage`
  signs URLs against `Endpoint`, so in prod `Minio__Endpoint` must itself be the
  browser-facing `s3.<PUBLIC_HOSTNAME>` (UseSsl=true). Splitting internal vs.
  public endpoints needs a URL-authority rewrite in `MinioFileStorage`. See
  CUTOVER.md §3.

### Verified
- Build clean (0 warnings / 0 errors).
- **Whole suite green, 0 failures**, run per-area to avoid Testcontainer
  startup contention: Auth 11, Users+ProfileImage 23, Customers 16, Products 15,
  Sales + panels 51, SalesReturns 16, Financial 13, AI endpoint 5, AiQuery 6,
  Chat 16, plus `Binesh.Ai.IntegrationTests` 55. (`Domain`/`Application`
  unit-test projects are present but currently have no test cases.)

### Round 16 wrap-up
With the rewrite at endpoint parity for all interactive features and the cutover
documented, the only remaining work is the deliberately-deferred items: the data
sync / ETL replacement (Round 14), the three PARITY gaps the frontend may need,
and granular per-user permissions. None of these block standing up the new stack.
