# InventorySaaS — Questions & Answers

> The single doc to read before any demo, interview, code review, or stakeholder meeting. Each answer is calibrated to be either a 30-second answer or a deeper follow-up. Pick the level appropriate to your audience.

---

## Table of Contents

- [Part 1 — Product & Business (non-technical)](#part-1--product--business-non-technical)
- [Part 2 — Architecture](#part-2--architecture)
- [Part 3 — Authentication & Authorization](#part-3--authentication--authorization)
- [Part 4 — Multi-Tenancy](#part-4--multi-tenancy)
- [Part 5 — Database & EF Core](#part-5--database--ef-core)
- [Part 6 — Background Jobs (Hangfire)](#part-6--background-jobs-hangfire)
- [Part 7 — AI Features (Gemini)](#part-7--ai-features-gemini)
- [Part 8 — Frontend (Angular)](#part-8--frontend-angular)
- [Part 9 — Debugging, Logging, Observability](#part-9--debugging-logging-observability)
- [Part 10 — Testing](#part-10--testing)
- [Part 11 — Deployment & Operations](#part-11--deployment--operations)
- [Part 12 — Security](#part-12--security)
- [Part 13 — The CQRS → Service migration](#part-13--the-cqrs--service-migration)
- [Part 14 — Things I'd improve next (honest)](#part-14--things-id-improve-next-honest)

---

## Part 1 — Product & Business (non-technical)

### Q: What does this project do, in one sentence?
A web application that lets a business track its products, warehouses, stock movements, suppliers, customers, purchase orders, and sales orders — built so many different companies can use the same app at the same time without seeing each other's data.

### Q: Who is it for?
- A **pharmacy or supermarket chain** tracking which items are about to expire
- A **retail manager** who wants today's sales vs. purchases at a glance
- A **warehouse worker** recording incoming goods and stock transfers
- An **owner** asking "how much is my inventory worth?"
- A **SaaS operator** who wants to onboard many businesses on one platform

### Q: What's the AI for?
Two features:
1. **Scan a product photo** → Gemini Vision reads the label, extracts name / brand / barcode / suggested price / category, and pre-fills the new-product form. Useful when adding hundreds of products quickly.
2. **Inventory copilot chat** → ask "what's running low?" or "what did we sell last week?" and the AI answers using a live snapshot of your real inventory data.

The AI never silently changes data. The product-scan endpoint returns a draft — the user reviews and submits. The chat is read-only.

### Q: What if the AI is wrong or hallucinated?
- **Scan**: Gemini returns ~80% accurate field-level extraction. The form pre-fills are editable. Nothing saves until the user clicks Save and the **normal validation** runs.
- **Chat**: Responses are scoped to a fresh inventory snapshot built per request. The model can't invent inventory — it can only summarise what was sent. Worst case, it phrases something poorly; the user sees the same data on the dashboard anyway.

### Q: What if a tenant's data leaks to another tenant?
That's the failure mode we work hardest to prevent. Three lines of defence:
1. Every tenant-scoped table has a `TenantId` foreign key
2. EF Core global query filters apply `WHERE TenantId = X` automatically
3. The `SaveChangesAsync` override in `ApplicationDbContext` auto-stamps `TenantId` on inserts so a developer can't forget

> ⚠️ **Currently filed for hardening**: the global query filter is presently a no-op (`... || true`) — Phase 9 review item. Until that's fixed, isolation relies on the auto-stamp + the application code. Test integration regularly.

### Q: How much does it cost to run?
- **Compute**: a single small App Service / VM ($10–30/month) handles dozens of tenants
- **Database**: SQL Server Express (free) or Azure SQL Basic ($5–15/month)
- **Hangfire DB**: same SQL Server, separate database — no extra cost
- **Redis**: optional. Without it, in-memory cache works for single-server deploys
- **Gemini AI**: free tier covers ~20 vision calls/day. Paid tier is pennies per request
- **Total realistic dev/test footprint**: <$20/month including AI

### Q: What roles exist and who can do what?
Five roles, hierarchical:
- **SuperAdmin** — system-wide; can see all tenants, access Hangfire dashboard
- **TenantAdmin** — full access within their tenant, including user management
- **Manager** — most operations except user management; can approve POs, confirm SOs, make stock adjustments
- **Staff** — day-to-day: create products / receive goods / deliver orders / move stock
- **Viewer** — read-only

Full permission matrix is in [USER_MANUAL.md](USER_MANUAL.md#what-each-role-can-do).

### Q: What if a user is offline?
The app is online-only. There is no offline cache. A planned future capability (PWA + IndexedDB write-ahead log for stock movements), but not built today.

### Q: What are the subscription plan tiers?
| Plan | Monthly | Max users | Max warehouses | Max products | Advanced reports | API access |
|---|---|---|---|---|---|---|
| Free | ৳0 | 3 | 1 | 100 | No | No |
| Basic | ৳29.99 | 10 | 3 | 1,000 | No | No |
| Professional | ৳79.99 | 50 | 10 | 10,000 | Yes | Yes |
| Enterprise | ৳199.99 | Unlimited | Unlimited | Unlimited | Yes | Yes |

These are seeded into the `SubscriptionPlans` table. The limits are not yet enforced at the API layer — that's a billing-integration item for the roadmap.

### Q: How are dates and currency displayed?
- All timestamps are stored in **UTC** in the database
- The `TenantInfo` entity has `Currency` (default `BDT`) and `Timezone` (default `UTC`); the frontend uses these per-tenant to format
- Prices use `decimal` (currency-safe), never `double` or `float`

---

## Part 2 — Architecture

### Q: What's the overall shape of the backend?
**Clean Architecture in 4 projects**:
```
InventorySaaS.Domain          ← entities, enums, base classes, exceptions (no framework)
       ▲
InventorySaaS.Application     ← services (one per module), DTOs, IApplicationDbContext
       ▲
InventorySaaS.Infrastructure  ← EF Core, JWT, email, Hangfire, AI, file storage, PDF
       ▲
InventorySaaS.API             ← controllers, middleware, Program.cs
```
Outer rings depend on inner rings, never the reverse. The Domain has zero NuGet references — it's pure C#.

### Q: Why Clean Architecture and not just "controllers + services + a flat DbContext"?
Three reasons:
1. **Testability** — Domain logic can be unit-tested without spinning up a database
2. **Replaceable infrastructure** — to switch SQL Server → PostgreSQL, only Infrastructure changes
3. **No accidental coupling** — Application doesn't know that EF Core or HTTP exists, so business code can't accidentally depend on framework details

### Q: Why Controller → Service instead of CQRS / MediatR?
Originally CQRS via MediatR (separate `*Command` + `*Handler` + `*Validator` per endpoint). Migrated to Controller → Service for these reasons:
- **Half the file count** per module (no separate Command + Handler + Validator)
- **Flat stack traces** — `Controller → Service → DbContext`, three frames you actually wrote
- **Lower onboarding cost** — most developers already know "thin controllers + service classes"
- **No CQRS payoff** in this app — there's no separate read model, no event sourcing, no message bus

What's lost: MediatR pipeline behaviors that ran validation/logging automatically. Those are now framework-native (model binding + Serilog request logger).

### Q: Why no Repository pattern?
Services depend on `IApplicationDbContext` — an interface that exposes every `DbSet<>` directly. That's a "lightweight repository" — same testability, full LINQ power, no per-entity boilerplate. Adding `IProductRepository`, `ICategoryRepository`, etc. would just be ceremony.

### Q: Why typed domain exceptions instead of returning `Result<T>`?
The original code wrapped every return in `Result<T>` so callers could check `IsSuccess`. Every controller had to repeat:
```csharp
return result.IsSuccess ? Ok(result.Value) : NotFound();
```
Switching to typed exceptions (`NotFoundException`, `ConflictException`, `BadRequestException`) lets the global `ExceptionHandlingMiddleware` decide the HTTP code once, in one place. Controllers simplified from ~10 lines per action to 2 lines.

### Q: What's the request lifecycle (server side)?
```
HTTP request
 ↓
1. CorrelationIdMiddleware            (assigns X-Correlation-Id, pushes to Serilog scope)
 ↓
2. ExceptionHandlingMiddleware        (try/catch wrapping the entire pipeline)
 ↓
3. UseHttpsRedirection                (no-op in dev with HTTP-only)
 ↓
4. UseCors("AllowFrontend")           (validates Origin)
 ↓
5. UseIpRateLimiting                  (60 rpm general, 10 rpm /auth/login)
 ↓
6. UseAuthentication                  (JWT bearer; fills HttpContext.User)
 ↓
7. TenantResolutionMiddleware         (logs tenant context; lookup is in TenantAccessor)
 ↓
8. UseAuthorization                   (role policies)
 ↓
9. MapControllers                     (route → controller action)
 ↓
10. Controller action                 (binds inputs)
 ↓
11. _xxxService.SomethingAsync(...)   (business logic + EF queries)
 ↓
12. SaveChangesAsync override         (auto-stamps audit fields + TenantId)
 ↓
13. SQL Server
 ↓
14. Returned DTO → Ok(...)
 ↓
15. Bubbles back through all middleware
 ↓
HTTP response (status + JSON + X-Correlation-Id header)
```

### Q: Where does dependency injection happen?
Three places:
1. [`Program.cs`](src/InventorySaaS.API/Program.cs) — framework services (Auth, CORS, Hangfire, Swagger, controllers)
2. [`Application/DependencyInjection.cs`](src/InventorySaaS.Application/DependencyInjection.cs) — `services.AddScoped<IXxxService, XxxService>()` for the 14 business services
3. [`Infrastructure/DependencyInjection.cs`](src/InventorySaaS.Infrastructure/DependencyInjection.cs) — DbContext, Hangfire DB, Redis (or memory cache fallback), all the Infrastructure services (Token, PasswordHasher, AI, Email, PDF, FileStorage)

### Q: Lifetime cheat sheet?
- **Singleton** → one per process (Hangfire server, rate-limit config)
- **Scoped** → one per HTTP request (DbContext, all 14 business services, `ICurrentUserService`, `ITenantAccessor`)
- **Transient** → new instance per injection

---

## Part 3 — Authentication & Authorization

### Q: How does login work?
1. POST `/api/v1/Auth/login` with `{email, password}`
2. `AuthService.LoginAsync` looks up user by `NormalizedEmail`
3. `PasswordHasherService.Verify(input, storedHash)` — slow PBKDF2 compare
4. If valid → `TokenService.GenerateTokensAsync` issues a JWT (60 min) + a refresh-token row
5. Returns `AuthResponse` with both tokens + the user DTO
6. If invalid → throws `UnauthorizedAccessException` → middleware → 401

### Q: What's a JWT?
A signed token containing claims (user_id, email, tenant_id, role). The server doesn't store sessions — it just verifies the signature with a secret key. Stateless, scales horizontally trivially.

A JWT is three base64 parts separated by dots: `header.payload.signature`.

### Q: What's in the JWT for this project?
- `nameid` — user GUID
- `email` — user email
- `tenant_id` — tenant GUID (empty for SuperAdmin)
- `full_name` — display name
- `role` — string (one role per token; for multi-role users, multiple `role` claims)
- standard: `nbf`, `exp`, `iat`, `iss`, `aud`

### Q: What if a JWT is stolen?
The token is valid for **60 minutes**. The refresh-token is rotated on every use, so a stolen refresh-token reveals itself the moment the legit user refreshes (the old token is revoked, the attacker's next request fails). Mitigations: short JWT expiry, refresh-rotation, IP rate-limiting on `/auth` endpoints, HTTPS in production.

### Q: How are passwords stored?
PBKDF2-SHA512 with a per-user random salt. Verifying takes ~100ms by design (slow → brute-force is impractical). Even a full DB leak doesn't reveal raw passwords.

See [`PasswordHasherService.cs`](src/InventorySaaS.Infrastructure/Services/Auth/PasswordHasherService.cs) and the inner `PasswordHasher` static class.

### Q: How does role-based authorization work?
Five named policies in [`Program.cs`](src/InventorySaaS.API/Program.cs):
```csharp
SuperAdminOnly  → [SuperAdmin]
TenantAdminOnly → [TenantAdmin, SuperAdmin]
ManagerUp       → [Manager, TenantAdmin, SuperAdmin]
StaffUp         → [Staff, Manager, TenantAdmin, SuperAdmin]
ViewerUp        → [Viewer, Staff, Manager, TenantAdmin, SuperAdmin]
```
Applied as `[Authorize(Policy = "ManagerUp")]` on controllers or actions.

### Q: Can a user have multiple roles?
Yes. The JWT can carry multiple `role` claims, and `RequireRole` accepts a match against any. The policy passes if **any** of the user's roles is in the required set.

### Q: How does refresh-token rotation work?
1. POST `/api/v1/Auth/refresh-token` with `{refreshToken}`
2. `TokenService.RefreshTokenAsync` looks up the row by token value
3. If active (not revoked, not expired) → revoke it (`RevokedAt = now`), issue a new pair
4. The old token's `ReplacedByToken` field points to the new one (audit chain)
5. Frontend stores the new pair

### Q: What's the difference between 401 and 403?
- **401 Unauthorized** = the JWT bearer middleware rejected the token (missing, expired, bad signature, wrong issuer/audience)
- **403 Forbidden** = JWT validated successfully, but the user's role doesn't match the policy (e.g. Viewer trying `[Authorize(Policy = "StaffUp")]`)

For 401, look at the `JWT Authentication failed:` log line emitted by the `OnAuthenticationFailed` handler in `Program.cs`. For 403, the role just doesn't match — either the user needs more permissions or the policy is wrong.

---

## Part 4 — Multi-Tenancy

### Q: How is tenant isolation enforced?
Two layers:
1. **Schema** — every tenant-scoped entity inherits `TenantEntity`, which adds a `TenantId Guid` foreign key
2. **Behavior** — `ApplicationDbContext.SaveChangesAsync` override auto-stamps `TenantId` from `ITenantAccessor` on every new entity, so a developer can't forget

> ⚠️ **Known gap (Phase 9)**: the EF Core global query filter on `TenantEntity` currently evaluates `... || true` (always true) — it doesn't actually filter reads. Tenant isolation today depends on the auto-stamp on writes + service-layer `WHERE` clauses. Hardening item.

### Q: How does the server know which tenant the user is?
Three lookup paths in [`TenantAccessor.cs`](src/InventorySaaS.Infrastructure/Services/Auth/TenantAccessor.cs):
1. **JWT claim** `tenant_id` (production)
2. **Header** `X-TenantId` (dev/admin convenience)
3. **Subdomain** (planned, not implemented)

The JWT claim is set on login from the user's stored `TenantId`. SuperAdmins have an empty tenant_id and bypass tenant checks where present.

### Q: How does a new tenant register?
POST `/api/v1/Auth/register` with company name + admin user details:
1. `AuthService.RegisterAsync` checks email uniqueness
2. Creates `TenantInfo` row with auto-generated slug (kebab-case + 6-char GUID suffix)
3. Assigns the Free subscription plan
4. Creates the admin user, hashes password, links to TenantAdmin role
5. Issues JWT + refresh token immediately — user is logged in

### Q: What happens if a tenant is deleted?
Currently no DELETE endpoint exists. Deletion would need a cascade strategy decision: hard delete (lose history), soft delete (data hidden but retained), or archive-and-purge (legal compliance). All three are reasonable; pick when there's a real product requirement.

---

## Part 5 — Database & EF Core

### Q: Why SQL Server?
- Tightest EF Core integration
- Solid Hangfire support out of the box
- `RowVersion` for optimistic concurrency is built in
- Handles the workload size of an inventory SaaS easily without sharding

### Q: Could we switch to PostgreSQL?
Yes — change one NuGet (`Npgsql.EntityFrameworkCore.PostgreSQL`), one line in `Infrastructure/DependencyInjection.cs` (`UseNpgsql` instead of `UseSqlServer`), regenerate migrations, swap the Hangfire storage provider. Application and Domain don't change at all.

### Q: What's EF Core?
Microsoft's ORM. You write LINQ in C# (`db.Products.Where(p => p.Name == "X")`) and it generates SQL. No raw SQL strings. Migrations track schema changes in C# files.

### Q: What are migrations?
C# files in [`Infrastructure/Persistence/Migrations/`](src/InventorySaaS.Infrastructure/Persistence/Migrations) describing schema changes. EF Core applies pending migrations automatically on startup via `Database.MigrateAsync()` in the seeder.

To create a new migration:
```bash
dotnet ef migrations add MyMigration --project src/InventorySaaS.Infrastructure --startup-project src/InventorySaaS.API
```

### Q: How is the database seeded?
[`DatabaseSeeder.cs`](src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs) runs on every startup. Each seed step is **idempotent** — checks if data already exists before inserting:
- 4 subscription plans (Free / Basic / Professional / Enterprise)
- 5 roles (SuperAdmin / TenantAdmin / Manager / Staff / Viewer)
- 12 permission modules with actions
- 1 SuperAdmin user
- 1 Demo Company tenant with admin user
- Demo data: 5 products, 2 warehouses, 3 locations, 2 suppliers, 2 customers, randomized inventory balances

### Q: What's a global query filter?
A LINQ predicate EF Core silently adds to every query against an entity. Two are configured in `OnModelCreating`:
1. **Multi-tenant** filter for `TenantEntity` ⚠️ (currently no-op — see Part 4)
2. **Soft-delete** filter for `BaseEntity` — `WHERE NOT IsDeleted` — so deleted rows are hidden from regular queries unless `IgnoreQueryFilters()` is called

### Q: What's soft delete and why use it?
Setting `IsDeleted = true` instead of running `DELETE FROM ...`. The row stays in the database; the global filter hides it from regular reads. Benefits:
- Recover accidental deletes
- Foreign-key integrity preserved
- Audit history intact
- Time-travel queries possible (`IgnoreQueryFilters()` to see deleted)

Cost: every query carries the `WHERE NOT IsDeleted` clause; storage grows over time.

### Q: How are concurrent edits handled?
Optimistic concurrency via `RowVersion byte[]` column on `BaseEntity`. EF Core auto-increments it on every save. If two users edit the same row, the second save throws `DbUpdateConcurrencyException` — catch it and tell the user "this record was modified, refresh and try again."

### Q: How are `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` set?
Auto-stamped by the `SaveChangesAsync` override in `ApplicationDbContext.cs`:
```csharp
case EntityState.Added:
    entry.Entity.CreatedAt = DateTime.UtcNow;
    entry.Entity.CreatedBy = userId;
case EntityState.Modified:
    entry.Entity.UpdatedAt = DateTime.UtcNow;
    entry.Entity.UpdatedBy = userId;
```
Service code never touches these fields directly.

### Q: Why two databases (`InventorySaaS` + `InventorySaaS_Hangfire`)?
Hangfire writes a lot — job queue, completed-job history, server heartbeats. Keeping it in a separate database means:
- The app DB stays clean (no Hangfire tables polluting it)
- You can purge/recreate Hangfire DB without touching app data
- Different backup retention policies if needed

Configured in `appsettings.json` under `ConnectionStrings:HangfireConnection`. Falls back to `DefaultConnection` if not set.

---

## Part 6 — Background Jobs (Hangfire)

### Q: What jobs run on this project?
Two recurring jobs registered in `Program.cs`:
- `check-low-stock` — hourly cron, scans inventory below reorder level, creates `LowStock` notifications
- `check-expiry-alerts` — daily cron, finds items expiring within 30 days, creates `ExpiryAlert` notifications

### Q: Why Hangfire instead of `Task.Run` or a hosted service?
- **Persistence** — jobs survive process restart (stored in SQL)
- **Distributed locks** — even with 5 API instances, the hourly job runs once/hour, total
- **Retries** — failed jobs auto-retry with exponential backoff
- **Dashboard** — `/hangfire` shows runs, success/fail counts, lets you trigger manually
- **All four** for free, instead of building each from scratch

### Q: Where's the dashboard and who can access it?
http://localhost:5179/hangfire (or your prod URL). Gated by [`HangfireAuthorizationFilter`](src/InventorySaaS.API/Middleware/HangfireAuthorizationFilter.cs). In dev it allows anyone; in production, restrict to SuperAdmin.

### Q: How do I add a new recurring job?
1. Create a service in `Infrastructure/Services/BackgroundJobs/MyNewJob.cs` with an `async Task` method
2. Register it in `Infrastructure/DependencyInjection.cs`: `services.AddScoped<MyNewJob>();`
3. In `Program.cs`, add: `RecurringJob.AddOrUpdate<MyNewJob>("my-job", j => j.RunAsync(), Cron.Daily);`
4. Restart — the job appears in the Hangfire dashboard and runs on schedule

### Q: How do I trigger a job for testing?
Open `/hangfire` → Recurring Jobs → click the job → "Trigger now". Or set a breakpoint inside the job's method, then trigger.

### Q: What if a job throws an exception?
Hangfire automatically retries (default: 10 retries with exponential backoff: 1m → 2m → 4m → ... → 17h). After all retries fail, the job moves to the "Failed" state and stays there until manually requeued.

---

## Part 7 — AI Features (Gemini)

### Q: What model does this project use?
`gemini-2.5-flash-lite` — Google's small, fast, free-tier eligible model. Supports vision and JSON mode. Used for both the product scan and the chat.

### Q: How does the product scan work?
1. User picks a JPEG/PNG (≤ 5 MB)
2. Frontend POSTs as `multipart/form-data` to `/api/v1/Products/extract-from-image`
3. Controller validates size and MIME type
4. [`GeminiProductExtractionService`](src/InventorySaaS.Infrastructure/Services/AI/GeminiProductExtractionService.cs) base64-encodes the image, sends to Gemini's `generateContent` REST endpoint with `responseMimeType: "application/json"` and a strict prompt
5. Strips defensive markdown fences if Gemini wraps the JSON anyway
6. Deserializes into `ProductExtractionResult` DTO
7. Returns to the frontend; the form pre-fills

**Critical**: this endpoint **does not save anything**. The user always reviews and submits through the normal `POST /api/v1/Products`.

### Q: What if Gemini returns garbage JSON?
Three layers of defence:
1. Prompt explicitly says "return ONLY valid JSON with this exact shape"
2. `responseMimeType: "application/json"` forces Gemini to validate its own output
3. If parsing still fails, the controller catches `InvalidOperationException` and returns 502 with a clean message

The user just doesn't get an auto-fill — they type the product manually.

### Q: How does the AI chat work?
1. User types a message
2. Frontend POSTs to `/api/v1/Chat`
3. [`AiChatService`](src/InventorySaaS.Infrastructure/Services/AI/AiChatService.cs) builds a per-request system prompt containing live tenant data: KPIs, low stock, top products, recent transactions, recent orders
4. Calls Gemini's streaming endpoint
5. Forwards SSE chunks straight to the browser
6. Frontend renders text token-by-token as it arrives

Retry logic: 3 attempts with exponential backoff (2s/4s/6s) on HTTP 429 (rate limit) or 503 (overloaded).

### Q: Why streaming for chat but not for product scan?
- Chat: text response, often long. Streaming reduces perceived latency dramatically (you see text appearing).
- Product scan: needs the full JSON before it can be parsed. Streaming would just complicate parsing.

### Q: What if the Gemini API key isn't configured?
Both AI features simply don't work. The rest of the app is fully functional. No crashes — the missing key is checked at request time.

### Q: Where does the API key live?
- **Dev**: `dotnet user-secrets set "Gemini:ApiKey" "..."`
- **Prod**: environment variable `Gemini__ApiKey` (the `__` maps to `:` in .NET config)
- **Never** in `appsettings.json` (which is in source control)

### Q: How much does Gemini cost?
- Free tier: ~20 vision calls/day, generous chat quota
- Paid: pennies per 1K tokens, generally negligible for an inventory SaaS

---

## Part 8 — Frontend (Angular)

### Q: Why Angular?
Strong tooling for large enterprise apps, built-in DI, reactive forms, HTTP interceptors, mature standalone component model + signals. Suits structured business apps better than React for this use case.

### Q: What's a standalone component?
Modern Angular components that don't need to be declared in an `NgModule`. Each component lists its own imports. The whole project uses standalone components.

### Q: How does the frontend talk to the backend?
- One [`environment.ts`](inventory-saas-web/src/environments/environment.ts) sets `apiUrl: 'http://localhost:5179'`
- 17 services in [`core/services/`](inventory-saas-web/src/app/core/services/) — one per backend controller
- Each service uses `HttpClient` + observables
- Three HTTP interceptors run on every request: auth, error, tenant

### Q: What does each interceptor do?
- [`auth.interceptor.ts`](inventory-saas-web/src/app/core/interceptors/auth.interceptor.ts) — adds `Authorization: Bearer <jwt>`; on 401, silently calls `/refresh-token` and retries the original request
- [`error.interceptor.ts`](inventory-saas-web/src/app/core/interceptors/error.interceptor.ts) — global error handling, surfaces toasts
- [`tenant.interceptor.ts`](inventory-saas-web/src/app/core/interceptors/tenant.interceptor.ts) — adds `X-TenantId` for dev/admin scenarios

### Q: How do guards work?
[`auth.guard.ts`](inventory-saas-web/src/app/core/guards/auth.guard.ts) blocks unauthenticated users from `MainLayoutComponent` (everything inside the shell). [`role.guard.ts`](inventory-saas-web/src/app/core/guards/role.guard.ts) blocks users without the required role from `users` and `settings` routes.

### Q: How does the silent token refresh work?
1. Request fails with 401
2. `auth.interceptor` catches it
3. Calls `/auth/refresh-token` with the stored refresh token
4. Stores the new access + refresh tokens
5. Retries the original request with the new token
6. User never sees a re-login screen unless the refresh token itself is invalid (then they're sent to `/auth/login`)

### Q: What are signals used for?
The toast notification list. `NotificationService` exposes a `signal<Toast[]>([])` — components reading it auto-update when toasts are added/removed. Lighter than RxJS for simple reactive state.

### Q: What's the routing structure?
[`app.routes.ts`](inventory-saas-web/src/app/app.routes.ts):
- 4 public routes: `/auth/login`, `/auth/register`, `/auth/forgot-password`, `/auth/reset-password`
- Everything else nested under `MainLayoutComponent` (shell with header + sidebar)
- Wildcard route `**` redirects to `/dashboard`

### Q: Why Angular Material plus custom CSS?
Angular Material gives a cohesive design system with built-in accessibility. Custom CSS handles the toast container (cheaper than pulling in a separate library). Sidebar, header, forms, tables — all Material.

---

## Part 9 — Debugging, Logging, Observability

### Q: Where are logs stored?
- **Console** — visible while running `dotnet run`
- **File** — `src/InventorySaaS.API/logs/log-YYYYMMDD.txt`, daily rolling
- Both via Serilog with structured (key-value) entries

### Q: What's a correlation ID?
A unique GUID assigned to every HTTP request by [`CorrelationIdMiddleware`](src/InventorySaaS.API/Middleware/CorrelationIdMiddleware.cs). It's:
- Pushed into Serilog's log scope (every log line for that request includes it)
- Returned in the `X-Correlation-Id` response header
- Included in the `ProblemResponse` body when an exception occurs

To trace a single request across all middleware and the database:
```powershell
Select-String -Path "src/InventorySaaS.API/logs/log-*.txt" -Pattern "<correlationId>"
```

### Q: How do I debug the backend?
Set breakpoints in 5 strategic places (covered in Phase 4 of the learning plan):
1. Controller action entry
2. Service method entry
3. Right before the EF query
4. Right before `SaveChangesAsync`
5. `ExceptionHandlingMiddleware.HandleExceptionAsync` (catch-all)

Then F5 in Visual Studio, hit the endpoint via Swagger, walk through.

### Q: How do I see the SQL EF Core generates?
The Output window in Visual Studio shows it on every query when running in Debug:
```
[INF] Executed DbCommand (4ms) [Parameters=[@__id_0='...'], CommandType='Text', CommandTimeout='30']
SELECT TOP(1) [u].[Id], [u].[Email], ...
```
You can copy-paste straight into SSMS to test interactively.

### Q: How do I debug a Hangfire job?
1. Open `/hangfire` → Recurring Jobs → click the job → "Trigger now"
2. Set a breakpoint inside the job method
3. The job runs on a Hangfire worker thread — breakpoints work the same as anywhere

### Q: How do I trace the EF `ChangeTracker` mid-request?
While paused at a breakpoint, type into the **Immediate window**:
```csharp
_context.ChangeTracker.Entries().Select(e => $"{e.Entity.GetType().Name} ({e.State})").ToList()
```
Shows every entity tracked + its state (Added, Modified, Unchanged). Crucial for finding "I saved the wrong thing" bugs.

---

## Part 10 — Testing

### Q: What tests exist today?
- **`tests/InventorySaaS.UnitTests/`** — entity tests + `PasswordHasher` test
- **`tests/InventorySaaS.IntegrationTests/`** — domain entity + `WebApplicationFactory` setup (the API is exposed as `partial public class Program` for in-process testing)

Coverage is light. The most valuable additions would be integration tests around tenant isolation and multi-step state-machine flows (PO Approve → Receive, SO Confirm → Deliver).

### Q: How do I run tests?
```bash
dotnet test tests/InventorySaaS.UnitTests
dotnet test tests/InventorySaaS.IntegrationTests
dotnet test                              # all
```

### Q: How do I write an integration test that hits a real DB?
The integration test project uses `Microsoft.EntityFrameworkCore.InMemory` for fast in-process tests, or `WebApplicationFactory<Program>` + a test SQL Server instance for full-stack. Pattern:
```csharp
public class ProductsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ProductsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    [Fact]
    public async Task GetProducts_RequiresAuth() {
        var response = await _client.GetAsync("/api/v1/Products");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

### Q: How do I write a unit test for a service?
Mock `IApplicationDbContext` and `ICurrentUserService`. Pattern:
```csharp
var contextMock = new Mock<IApplicationDbContext>();
contextMock.Setup(c => c.Categories).Returns(MockDbSet(new[] { ... }));
var service = new CategoryService(contextMock.Object, currentUserMock.Object);
var result = await service.GetByIdAsync(id, CancellationToken.None);
result.Name.Should().Be("Expected");
```
Mocking `DbSet` is the awkward part — use a helper or use the in-memory provider for nicer ergonomics.

---

## Part 11 — Deployment & Operations

### Q: How do I deploy this to production?
Three options:
1. **Azure App Service** — easiest. There's a GitHub Action wired up for push-to-main deploys
2. **Docker** — `docker-compose.yml` runs SQL Server + Redis + API + frontend in containers
3. **Kubernetes** — for serious scale: API as a Deployment with HPA, SQL Server as StatefulSet (or managed), Hangfire dashboard exposed only internally

### Q: What environment variables do I need to set in production?
```
ConnectionStrings__DefaultConnection  = <prod sql connection>
ConnectionStrings__HangfireConnection = <prod sql connection or sub-DB>
ConnectionStrings__Redis              = <redis connection>     (optional)
JwtSettings__Secret                   = <strong random 64+ char string>
Gemini__ApiKey                        = <google ai api key>    (optional)
Smtp__Host / Smtp__Username / Smtp__Password / Smtp__From    (for password reset emails)
AllowedOrigins__0                     = https://your-frontend.example.com
```

### Q: How do I scale horizontally?
1. **API**: stateless — run N replicas behind a load balancer. JWT means any replica can validate any request
2. **Hangfire**: built-in distributed locks ensure jobs run once across N replicas
3. **Database**: scale SQL Server vertically first; add read replicas for queries; consider sharding by tenant only at very large scale
4. **Cache**: switch from in-memory to Redis (set `ConnectionStrings:Redis`) so cache is shared across replicas

### Q: What's the dev workflow?
```
git pull
dotnet restore && dotnet build
dotnet run --project src/InventorySaaS.API     (terminal 1)
cd inventory-saas-web && npm install && npm start   (terminal 2)
# Open http://localhost:4200
# Login: admin@demo-company.com / Demo@123456
# Backend tests: dotnet test
```

### Q: How are migrations applied in production?
On startup, `DatabaseSeeder.SeedAsync()` calls `_db.Database.MigrateAsync()` which applies any pending migrations idempotently. Risk: a migration that takes 10 minutes blocks startup. For large prod datasets, consider running migrations as a separate deploy step before swapping the API version.

---

## Part 12 — Security

### Q: What are the security controls in place?
- **JWT auth** with issuer/audience/lifetime/signing-key validation
- **Refresh-token rotation** to detect theft
- **PBKDF2-SHA512 password hashing** with per-user salt
- **IP-based rate limiting** (60 rpm general, 10 rpm on `/auth/login`)
- **CORS** restricted to allowed origins in production
- **HTTPS redirection** in production
- **Tenant isolation** at the schema level (TenantId FKs) + auto-stamp on save
- **Soft delete** so accidental deletes don't lose data
- **Optimistic concurrency** to detect concurrent edits
- **Global exception handling** — no stack traces leak to clients in production
- **Anti-enumeration** — login error messages don't reveal whether email exists; forgot-password always returns 200
- **Correlation IDs** to trace requests across logs

### Q: What about SQL injection?
EF Core parameterises every LINQ query. There's no raw SQL string concatenation anywhere. Even custom queries use LINQ, which compiles to parameterised SQL.

### Q: What about XSS?
Angular escapes interpolated text by default (`{{ value }}`). The risk surface is anywhere using `[innerHTML]` — currently used only in the chat panel for AI markdown rendering, which goes through a sanitiser. Other text outputs are escaped.

### Q: What about CSRF?
JWT in the `Authorization` header (not in a cookie) means CSRF is largely a non-issue — a third-party site can't read your token. If you ever switch to cookie-based auth, add `[AutoValidateAntiforgeryToken]`.

### Q: What about secret rotation?
- **JWT signing key**: rotating invalidates all live tokens. Rotate during low-traffic window. Consider supporting multiple valid keys (key rollover) in `TokenValidationParameters` for zero-downtime rotation.
- **Database password**: rotate via your secrets manager; restart the app to pick up the new connection string.
- **Gemini API key**: rotate via Google AI Studio; update `Gemini__ApiKey` env var; restart.

### Q: What's the security review status?
A security audit is **filed for Phase 9**. Known items:
- JWT secret in `appsettings.json` (move to user-secrets / env var only) ⚠️
- Multi-tenant query filter is currently a no-op ⚠️
- AutoMapper CVE — already resolved (package removed during the migration)

---

## Part 13 — The CQRS → Service migration

### Q: What changed?
The Application layer was migrated from CQRS-via-MediatR to plain Controller → Service. Specifically:
- Deleted ~62 handler files (`*Command.cs`, `*Handler.cs`, `*Query.cs`)
- Created 28 new service files (14 interfaces + 14 implementations) under `Application/Services/`
- Rewrote all 15 controllers to be thin (~2 lines per action)
- Removed MediatR, FluentValidation, AutoMapper NuGet packages
- Removed `Result<T>` wrapper — services return DTOs directly or throw typed exceptions
- Removed `ValidationBehavior` and `LoggingBehavior`
- Updated `ExceptionHandlingMiddleware` (no longer needs to catch `FluentValidation.ValidationException`)

### Q: Why?
- **File count** — every endpoint had 3-4 files (Command + Handler + Validator + DTO). Migrated, every endpoint has 0-1 files (DTO only — service method covers the rest)
- **Onboarding** — Controller → Service is the most familiar pattern in .NET; CQRS+MediatR is an extra concept new devs have to learn
- **Stack traces** — three frames you wrote (Controller → Service → DbContext), not seven
- **Build time** — fewer files, less assembly scanning

### Q: What did we lose?
- MediatR pipeline behaviors (validation + logging on every request, automatically). Replaced by:
  - Validation: model binding (data annotations) + inline service guards
  - Logging: Serilog's built-in request logger + correlation IDs (already in place)
- The conceptual separation between "command" (write) and "query" (read). In a project without separate read/write models, this was theoretical anyway.

### Q: Was it worth it?
Yes for this project. The litmus test for CQRS is "do my reads scale differently from my writes?" — for inventory CRUD with shared models, the answer is no. CQRS earns its keep when you have separate read stores (denormalised views, projections), event sourcing, or a message bus — none of which apply here.

### Q: How was the migration done safely?
Module-by-module:
1. Pilot — converted Categories first (smallest module), tested in Swagger, verified behavior identical
2. Repeated for the other 13 modules (Suppliers, Customers, Tenants, Dashboard, Notifications, Warehouses, Reports, Products, Inventory, Auth, Users, PurchaseOrders, SalesOrders) — one or two per session
3. Built and verified after each module
4. Deleted MediatR plumbing only after the last module was migrated

### Q: Could we go back?
Yes. Service methods are direct ports of handler logic — re-introducing MediatR would just mean wrapping each service method as a Command + Handler. About a day of work. But the team would have to want to.

### Q: What bug fixes were bundled in?
- Several `Update` endpoints used to return 400 on "not found"; now return 404 (correct HTTP semantics)
- Several `Create` endpoints used to return 400 on duplicate code; now return 409 Conflict (correct semantics)

---

## Part 14 — Things I'd improve next (honest)

If I had another sprint:

### High priority
1. **Fix the multi-tenant query filter** — currently `... || true` no-op. Replace with `EF.Property<Guid>(e, "TenantId") == _tenantAccessor.TenantId` and handle the SuperAdmin escape hatch
2. **Move the JWT secret out of appsettings.json** — User Secrets in dev, env var in prod
3. **Increase test coverage on tenant isolation** — integration tests that try to read another tenant's data and assert 404

### Medium priority
4. **Relax `ClockSkew = TimeSpan.Zero`** to 5 minutes in dev (we hit this mid-development)
5. **Add data-annotations to all Request DTOs** — bring back automatic 400-on-validation that we lost with FluentValidation removal
6. **Move SaleOrders `Return` action into the controller** (the service method exists; just isn't routed)
7. **Decimal precision** on currency columns — currently warns about default `decimal(18,2)`. Be explicit with `HasPrecision(18, 4)` for prices

### Lower priority
8. **Subscription plan limit enforcement** — entities exist, limits aren't checked at the API layer yet
9. **Audit log writes** — table exists, nothing populates it
10. **Localization** — English-only today
11. **PWA / offline support for stock movements** — high-value for warehouse workers in spotty connectivity
12. **Hangfire production hardening** — restrict dashboard to SuperAdmin only; rotate Hangfire DB credentials

---

## Quick reference

### File locations
| What | Path |
|---|---|
| Entry point | [`src/InventorySaaS.API/Program.cs`](src/InventorySaaS.API/Program.cs) |
| App config | [`src/InventorySaaS.API/appsettings.json`](src/InventorySaaS.API/appsettings.json) |
| Controllers (15) | [`src/InventorySaaS.API/Controllers/`](src/InventorySaaS.API/Controllers/) |
| Middleware (4) | [`src/InventorySaaS.API/Middleware/`](src/InventorySaaS.API/Middleware/) |
| Services (28 files) | [`src/InventorySaaS.Application/Services/`](src/InventorySaaS.Application/Services/) |
| DTOs | [`src/InventorySaaS.Application/Features/{Module}/DTOs/`](src/InventorySaaS.Application/Features/) |
| EF DbContext | [`src/InventorySaaS.Infrastructure/Persistence/ApplicationDbContext.cs`](src/InventorySaaS.Infrastructure/Persistence/ApplicationDbContext.cs) |
| Seeder | [`src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs`](src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs) |
| Auth services | [`src/InventorySaaS.Infrastructure/Services/Auth/`](src/InventorySaaS.Infrastructure/Services/Auth/) |
| Hangfire jobs | [`src/InventorySaaS.Infrastructure/Services/BackgroundJobs/`](src/InventorySaaS.Infrastructure/Services/BackgroundJobs/) |
| AI integrations | [`src/InventorySaaS.Infrastructure/Services/AI/`](src/InventorySaaS.Infrastructure/Services/AI/) |
| Domain entities | [`src/InventorySaaS.Domain/Entities/`](src/InventorySaaS.Domain/Entities/) |
| Domain exceptions | [`src/InventorySaaS.Domain/Exceptions/DomainException.cs`](src/InventorySaaS.Domain/Exceptions/DomainException.cs) |
| Frontend routes | [`inventory-saas-web/src/app/app.routes.ts`](inventory-saas-web/src/app/app.routes.ts) |
| Frontend services (17) | [`inventory-saas-web/src/app/core/services/`](inventory-saas-web/src/app/core/services/) |
| Interceptors (3) | [`inventory-saas-web/src/app/core/interceptors/`](inventory-saas-web/src/app/core/interceptors/) |

### URLs (dev)
| Resource | URL |
|---|---|
| API | http://localhost:5179 |
| Swagger | http://localhost:5179/swagger |
| Hangfire | http://localhost:5179/hangfire |
| Health | http://localhost:5179/health |
| Frontend | http://localhost:4200 |

### Seeded login
| Account | Email | Password |
|---|---|---|
| SuperAdmin | `superadmin@inventorysaas.com` | `Admin@123456` |
| Demo TenantAdmin | `admin@demo-company.com` | `Demo@123456` |

---

*Last updated: this file reflects the post-migration state of the codebase (Controller → Service pattern, no MediatR / FluentValidation / AutoMapper). For the original CQRS-era documentation, see git history.*
