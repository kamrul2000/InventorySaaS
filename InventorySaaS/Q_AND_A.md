# InventorySaaS — Questions & Answers

> The single doc to read before any demo, interview, code review, or stakeholder meeting. Each answer is calibrated to be either a 30-second answer or a deeper follow-up. Pick the level appropriate to your audience. For the conceptual study guide, see [PROJECT_GUIDE.md](PROJECT_GUIDE.md); for end-user workflows and the role permission matrix, see [USER_MANUAL.md](USER_MANUAL.md).

---

## Table of Contents

- [Part 1 — Product & Business (non-technical)](#part-1--product--business-non-technical)
- [Part 2 — Architecture](#part-2--architecture)
- [Part 3 — Authentication & Authorization](#part-3--authentication--authorization)
- [Part 4 — Multi-Tenancy](#part-4--multi-tenancy)
- [Part 5 — Database & EF Core](#part-5--database--ef-core)
- [Part 6 — Inventory, Orders & Costing](#part-6--inventory-orders--costing)
- [Part 7 — Billing (AR & AP)](#part-7--billing-ar--ap)
- [Part 8 — Background Jobs (Hangfire)](#part-8--background-jobs-hangfire)
- [Part 9 — AI Features (Gemini)](#part-9--ai-features-gemini)
- [Part 10 — Frontend (Angular)](#part-10--frontend-angular)
- [Part 11 — Debugging, Logging, Observability](#part-11--debugging-logging-observability)
- [Part 12 — Testing](#part-12--testing)
- [Part 13 — Deployment & Operations](#part-13--deployment--operations)
- [Part 14 — Security](#part-14--security)
- [Part 15 — The CQRS → Service migration](#part-15--the-cqrs--service-migration)
- [Part 16 — Things I'd improve next (honest)](#part-16--things-id-improve-next-honest)

---

## Part 1 — Product & Business (non-technical)

### Q: What does this project do, in one sentence?
A web application that lets a business track its products, warehouses, stock movements, suppliers, customers, purchase and sales orders, **and the money side** — customer invoices/payments and supplier bills/payments — built so many different companies can use the same app at the same time without seeing each other's data.

### Q: Who is it for?
- A **pharmacy or supermarket chain** tracking which items are about to expire
- A **retail manager** who wants today's sales vs. purchases at a glance
- A **warehouse worker** recording incoming goods and stock transfers
- An **accountant** raising customer invoices and paying supplier bills
- An **owner** asking "how much is my inventory worth?"
- A **SaaS operator** who wants to onboard many businesses on one platform

### Q: What's the AI for?
Two features:
1. **Scan a product photo** → Gemini Vision reads the label, extracts name / brand / barcode / suggested price / category, and pre-fills the new-product form.
2. **Inventory copilot chat** → ask "what's running low?" or "what did we sell last week?" and the AI answers using a live snapshot of your real inventory data.

The AI never silently changes data. The product-scan endpoint returns a draft — the user reviews and submits. The chat is read-only.

### Q: What if the AI is wrong or hallucinated?
- **Scan**: ~80% accurate field extraction; the pre-fills are editable and nothing saves until the user clicks Save and normal validation runs.
- **Chat**: responses are scoped to a fresh inventory snapshot per request. The model can only summarise what was sent; the user sees the same data on the dashboard anyway.

### Q: What if a tenant's data leaks to another tenant?
That's the failure mode we work hardest to prevent. Three lines of defence:
1. Every tenant-scoped table inherits `TenantEntity` (a `TenantId` column)
2. EF Core global query filters apply `WHERE TenantId = X && !IsDeleted` automatically on every read
3. The `SaveChangesAsync` override auto-stamps `TenantId` on inserts so a developer can't forget

The combined query filter is real and active (it is **not** a no-op — an earlier `... || true` bug was fixed; see Part 4). Keep integration tests on the tenant boundary as the regression net.

### Q: What roles exist and who can do what?
Five roles, hierarchical:
- **SuperAdmin** — system-wide; sees all tenants, accesses the Hangfire dashboard
- **TenantAdmin** — full access within their tenant, including user management and settings
- **Manager** — most operations; can approve POs, confirm/cancel SOs, make stock adjustments, cancel invoices/bills
- **Staff** — day-to-day: create products, receive goods, deliver orders, move stock, create invoices/bills, record payments
- **Viewer** — read-only

The mapping to policies: `ViewerUp` (read), `StaffUp` (create/operate), `ManagerUp` (approve/cancel/adjust), `TenantAdminOnly` (users/settings), `SuperAdminOnly` (cross-tenant + Hangfire). Full permission matrix in [USER_MANUAL.md](USER_MANUAL.md).

### Q: What are the subscription plan tiers?
| Plan | Monthly / Annual | Max users | Max warehouses | Max products | Advanced reports | API access |
|---|---|---|---|---|---|---|
| Free | 0 / 0 | 3 | 1 | 100 | No | No |
| Basic | 29.99 / 299.99 | 10 | 3 | 1,000 | No | No |
| Professional | 79.99 / 799.99 | 50 | 10 | 10,000 | Yes | Yes |
| Enterprise | 199.99 / 1,999.99 | Unlimited | Unlimited | Unlimited | Yes | Yes |

Seeded into `SubscriptionPlans`. New self-registrations get **Free**; the seeded Demo tenant is on **Professional**. The limits are **not yet enforced** at the API layer — a roadmap item.

### Q: How are dates and currency displayed?
- Timestamps are stored in **UTC**.
- `TenantInfo` has `Currency` (default `BDT`) and `Timezone` (default `UTC`); the frontend formats per-tenant.
- Money uses `decimal` (currency-safe), never `double`/`float`.

---

## Part 2 — Architecture

### Q: What's the overall shape of the backend?
**Clean Architecture in 4 projects**:
```
InventorySaaS.Domain          ← entities (36), enums, base classes, exceptions (no framework)
       ▲
InventorySaaS.Application     ← 18 services (one per module), DTOs, IApplicationDbContext
       ▲
InventorySaaS.Infrastructure  ← EF Core, JWT, email, Hangfire, AI, file storage, PDF
       ▲
InventorySaaS.API             ← 19 controllers, middleware, Program.cs
```
Outer rings depend on inner rings, never the reverse. The Domain has zero NuGet references.

### Q: Why Clean Architecture and not just "controllers + services + a flat DbContext"?
1. **Testability** — Domain logic can be unit-tested without a database
2. **Replaceable infrastructure** — SQL Server → PostgreSQL touches only Infrastructure
3. **No accidental coupling** — Application doesn't know EF Core or HTTP exists

### Q: Why Controller → Service instead of CQRS / MediatR?
Originally CQRS via MediatR (Command + Handler + Validator per endpoint). Migrated because:
- **Half the file count** per module
- **Flat stack traces** — Controller → Service → DbContext
- **Lower onboarding cost** — the most familiar .NET pattern
- **No CQRS payoff** — no separate read model, no event sourcing, no message bus

What's lost: MediatR pipeline behaviors (validation/logging). Now framework-native (model binding + Serilog request logger). Full detail in [Part 15](#part-15--the-cqrs--service-migration).

### Q: Why no Repository pattern?
Services depend on `IApplicationDbContext` — an interface exposing every `DbSet<>`. A "lightweight repository": same testability, full LINQ power, no per-entity boilerplate.

### Q: Why typed domain exceptions instead of returning `Result<T>`?
The global `ExceptionHandlingMiddleware` decides the HTTP code once, in one place. Controllers drop from ~10 lines per action to 2. Services throw `NotFoundException` (404), `ConflictException` (409), `BadRequestException` (400), `ForbiddenAccessException` (403).

### Q: Where does some logic live on the entity rather than the service?
Invariants that must hold no matter who calls them live as entity methods: `Invoice.ApplyPayment(amount)` / `ReversePayment(amount)`, `SupplierBill.ApplyPayment(...)`, `InventoryBalance.ApplyInbound(qty, cost)`. Services orchestrate; these methods enforce the state transition / cost math.

### Q: What's the request lifecycle (server side)?
```
HTTP request
 ↓ 1. CorrelationIdMiddleware       (X-Correlation-Id, Serilog scope)
 ↓ 2. ExceptionHandlingMiddleware   (try/catch over the whole pipeline)
 ↓ 3. UseHttpsRedirection
 ↓ 4. UseCors("AllowFrontend")
 ↓ 5. UseIpRateLimiting
 ↓ 6. UseAuthentication             (JWT bearer → HttpContext.User)
 ↓ 7. TenantResolutionMiddleware    (establishes tenant context)
 ↓ 8. UseAuthorization              (role policies)
 ↓ 9. MapControllers                (route → action; binds inputs)
 ↓ 10. _xxxService.SomethingAsync   (business logic + EF queries)
 ↓ 11. SaveChangesAsync override    (audit + auto-stamp audit fields + TenantId)
 ↓ 12. SQL Server
 ↓ 13. Returned DTO → Ok(...)
 ↓ bubbles back through middleware
HTTP response (status + JSON + X-Correlation-Id header)
```

### Q: Where does dependency injection happen?
1. [`Program.cs`](src/InventorySaaS.API/Program.cs) — framework services (Auth, CORS, rate limiting, Hangfire, Swagger, controllers, health checks)
2. [`Application/DependencyInjection.cs`](src/InventorySaaS.Application/DependencyInjection.cs) — the 18 business services as `Scoped`
3. [`Infrastructure/DependencyInjection.cs`](src/InventorySaaS.Infrastructure/DependencyInjection.cs) — DbContext, Hangfire storage, Redis-or-memory cache, and the Infrastructure services (Token, PasswordHasher, AI chat, AI extraction, Email, PDF, FileStorage, TenantAccessor, CurrentUserService)

### Q: Lifetime cheat sheet?
- **Singleton** → Hangfire server, rate-limit config
- **Scoped** → DbContext, all business services, `ICurrentUserService`, `ITenantAccessor`
- **Transient** → new per injection

---

## Part 3 — Authentication & Authorization

### Q: How does login work?
1. POST `/api/v1/Auth/login` with `{email, password}`
2. `AuthService.LoginAsync` looks up the user by `NormalizedEmail`
3. `PasswordHasherService.Verify(input, storedHash)` — slow PBKDF2 compare
4. If valid → `TokenService.GenerateTokensAsync` issues a JWT (60 min) + a refresh-token row
5. Returns `AuthResponse` with both tokens + the user DTO
6. If invalid → throws `UnauthorizedAccessException` → middleware → 401

### Q: What's in the JWT for this project?
- `nameid` — user GUID
- `email` — user email
- `tenant_id` — tenant GUID (empty for SuperAdmin)
- `full_name` — display name
- `role` — string (one claim per role; multiple for multi-role users)
- standard: `nbf`, `exp`, `iat`, `iss`, `aud`

### Q: What if a JWT is stolen?
Valid for **60 minutes**. The refresh-token is rotated on every use, so a stolen refresh-token reveals itself the moment the legit user refreshes (the old token is revoked; the attacker's next refresh fails). Mitigations: short JWT expiry, refresh rotation, IP rate-limiting on `/auth`, HTTPS in prod.

### Q: How are passwords stored?
PBKDF2-SHA512 with a per-user random salt. Verifying takes ~100 ms by design. A full DB leak doesn't reveal raw passwords. See [`PasswordHasherService.cs`](src/InventorySaaS.Infrastructure/Services/Auth/PasswordHasherService.cs) and the inner `PasswordHasher` helper.

### Q: How does role-based authorization work?
Five named policies in [`Program.cs`](src/InventorySaaS.API/Program.cs):
```
SuperAdminOnly  → SuperAdmin
TenantAdminOnly → TenantAdmin, SuperAdmin
ManagerUp       → Manager, TenantAdmin, SuperAdmin
StaffUp         → Staff, Manager, TenantAdmin, SuperAdmin
ViewerUp        → Viewer, Staff, Manager, TenantAdmin, SuperAdmin
```
Common pattern: the controller class is `[Authorize(Policy = "ViewerUp")]`; write actions override with `StaffUp`, and approve/cancel/adjust actions with `ManagerUp`.

### Q: How does refresh-token rotation work?
1. POST `/api/v1/Auth/refresh-token` with `{refreshToken}`
2. `TokenService.RefreshTokenAsync` looks up the row
3. If active → revoke it (`RevokedAt = now`), issue a new pair, set `ReplacedByToken` (audit chain)
4. Frontend stores the new pair

### Q: What's the difference between 401 and 403?
- **401** = the JWT bearer middleware rejected the token (missing/expired/bad signature/wrong issuer/audience). Look at the `JWT Authentication failed:` log line from the `OnAuthenticationFailed` handler.
- **403** = the JWT validated but the user's role doesn't match the policy (e.g. Viewer hitting a `StaffUp` action).

---

## Part 4 — Multi-Tenancy

### Q: How is tenant isolation enforced?
Three layers:
1. **Schema** — every tenant-scoped entity inherits `TenantEntity` (`TenantId Guid`)
2. **Reads** — a single combined global query filter per tenant entity:
   ```csharp
   e => (_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId) && !e.IsDeleted
   ```
3. **Writes** — `SaveChangesAsync` auto-stamps `TenantId` from `ITenantAccessor` on new tenant entities

### Q: Wasn't the query filter a no-op at one point?
Yes — that bug is **fixed**. EF Core allows only one filter per entity, and an earlier version (a) wrote the tenant predicate so it always evaluated `true`, and (b) called `HasQueryFilter` twice, the second silently discarding the first — so neither tenant nor soft-delete filtering ran. The current `OnModelCreating` reflects over all entity types and applies **one** combined predicate per tenant entity (above), and a soft-delete-only filter for non-tenant `BaseEntity` types. A `null` tenant (seeding/registration/SuperAdmin) bypasses the tenant clause but still respects soft-delete.

### Q: How does the server know which tenant the user is?
`ITenantAccessor` (impl `TenantAccessor.cs`) resolves the tenant primarily from the JWT `tenant_id` claim, set at login from the user's stored `TenantId`. SuperAdmins carry an empty tenant id and bypass tenant scoping where present.

### Q: How does a new tenant register?
POST `/api/v1/Auth/register`:
1. `AuthService.RegisterAsync` checks email uniqueness
2. Creates a `TenantInfo` with an auto-generated slug (kebab-case + GUID suffix)
3. Assigns the **Free** subscription plan
4. Creates the admin user (TenantAdmin), hashes the password
5. Issues JWT + refresh token immediately

### Q: What happens if a tenant is deleted?
No DELETE endpoint exists today. Deletion needs a cascade decision: hard delete (lose history), soft delete (hidden but retained), or archive-and-purge (compliance). Pick when there's a real requirement.

---

## Part 5 — Database & EF Core

### Q: Why SQL Server?
Tightest EF Core integration, solid Hangfire support, built-in `RowVersion` for optimistic concurrency, and it handles an inventory-SaaS workload without sharding.

### Q: Could we switch to PostgreSQL?
Yes — swap one NuGet (`Npgsql.EntityFrameworkCore.PostgreSQL`), one line in `Infrastructure/DependencyInjection.cs` (`UseNpgsql`), regenerate migrations, swap Hangfire's storage provider. Application and Domain don't change.

### Q: What are migrations and how are they applied?
C# files in [`Infrastructure/Persistence/Migrations/`](src/InventorySaaS.Infrastructure/Persistence/Migrations). On startup, `DatabaseSeeder.SeedAsync()` calls `Database.MigrateAsync()` to apply pending migrations idempotently. Current set: `InitialCreate`, `AddPurchaseOrderItemReturnedQuantity`, `AddBillingArInvoicesAndPayments`, `AddBillingApSupplierBillsAndPayments`.

To add one:
```bash
dotnet ef migrations add MyMigration --project src/InventorySaaS.Infrastructure --startup-project src/InventorySaaS.API
```

### Q: How is the database seeded?
[`DatabaseSeeder.cs`](src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs) runs on every startup; each step is idempotent:
- 4 subscription plans, 5 roles, 12 permission modules (with actions)
- 1 SuperAdmin user (`superadmin@inventorysaas.com` / `Admin@123456`)
- 1 "Demo Company" tenant on the Professional plan, admin `admin@demo-company.com` / `Demo@123456`
- Demo data: 3 categories, 2 brands, 3 units, 5 products, 2 warehouses + 3 locations, 2 suppliers, 2 customers, randomized inventory balances (10–200 qty, batch/expiry on trackable items)

### Q: What's a global query filter? (the two kinds here)
1. **Tenant + soft-delete** combined filter on every `TenantEntity` (see Part 4)
2. **Soft-delete only** filter on non-tenant `BaseEntity` types (`WHERE NOT IsDeleted`)

### Q: What's soft delete and why use it?
Setting `IsDeleted = true` instead of `DELETE`. The row stays; the global filter hides it. Benefits: recover accidental deletes, preserve FK integrity, keep audit history, time-travel with `IgnoreQueryFilters()`. Cost: every query carries `WHERE NOT IsDeleted`; storage grows.

### Q: How are concurrent edits handled?
Optimistic concurrency via `RowVersion byte[]` on `BaseEntity`. The second save on a stale row throws `DbUpdateConcurrencyException` — catch it and tell the user "this record was modified, refresh and try again."

### Q: How are CreatedAt / UpdatedAt / CreatedBy / UpdatedBy set?
Auto-stamped in the `SaveChangesAsync` override — service code never touches them. Added → `CreatedAt`/`CreatedBy`; Modified → `UpdatedAt`/`UpdatedBy`.

### Q: Is there an audit log, and is it actually populated?
Yes — **and it's live**. The `SaveChangesAsync` override collects an `AuditLog` row for every Added/Modified/Deleted `BaseEntity`: action (Create/Update/Delete), entity type + id, the changed fields as old/new JSON (Modified) or a scalar snapshot (Add/Delete), the acting user + email, tenant, and timestamp. A soft-delete is recorded as a `Delete`. Audit rows are written in a second `SaveChanges` after the main save. (Earlier docs said "the table exists but nothing populates it" — that is no longer true.)

### Q: Why two databases (`InventorySaaS` + Hangfire DB)?
Hangfire writes a lot (queue, history, heartbeats). Keeping it in a separate DB keeps the app DB clean, lets you purge/recreate Hangfire data independently, and supports different backup policies. Configured via `ConnectionStrings:HangfireConnection` (falls back to `DefaultConnection`).

---

## Part 6 — Inventory, Orders & Costing

### Q: How does inventory tracking work?
Two entities: `InventoryBalance` (on-hand + reserved + moving weighted-average `UnitCost`, keyed by product/warehouse/location/batch) and `InventoryTransaction` (the ledger). Every movement writes a transaction **and** updates a balance in the same `SaveChangesAsync` — both commit or both roll back. You can always reconstruct a balance by summing transactions.

### Q: What inventory operations exist?
`InventoryController` → `InventoryService`:
- `GET /balances`, `GET /transactions` (ViewerUp)
- `POST /stock-in` (StaffUp) — `ApplyInbound` blends cost, writes `StockIn`
- `POST /stock-out` (StaffUp) — decrements, writes `StockOut`
- `POST /transfer` (StaffUp) — moves between warehouses/locations carrying cost forward, writes `Transfer`
- `POST /adjustment` (ManagerUp) — reconciles to a new quantity with a reason, writes `Adjustment`

`TransactionType`: `StockIn, StockOut, Transfer, Adjustment, Return, Damaged, Lost, PurchaseReceive, SalesIssue, PurchaseReturn`.

### Q: How is product cost calculated?
**Moving weighted-average.** When stock arrives (`ApplyInbound`), the incoming unit cost is blended into the existing balance proportionally rather than replacing it. Outbound movements (delivery, stock-out) value stock at this cost — that's COGS, never the selling price.

### Q: What's the purchase-order lifecycle?
`PurchaseOrderService` / `PurchaseOrdersController`: `Create` (Draft, `PO-yyyyMMdd-####`) → `Approve` (ManagerUp) → `Receive` (StaffUp) → `Return` (ManagerUp). `OrderStatus`: Draft, Submitted, Approved, PartiallyReceived, Received, Cancelled, Returned. **Receive** increments `ReceivedQuantity`, calls `ApplyInbound` at the PO unit price, writes `PurchaseReceive` transactions, and sets `Received` or `PartiallyReceived`.

### Q: What's the sales-order lifecycle?
`SalesOrderService` / `SalesOrdersController`: `Create` (Draft, `SO-yyyyMMdd-####`) → `Confirm` (ManagerUp) → `Deliver` (StaffUp) → `Return` / `Cancel` (ManagerUp). `OrderStatus`: Draft, Confirmed, PartiallyDelivered, Delivered, Cancelled, Returned.
- **Confirm** validates available stock and **reserves** it FIFO by expiry (`QuantityReserved`)
- **Deliver** ships FIFO by expiry, decrements on-hand, releases the reservation, writes `SalesIssue` at COGS
- **Return** re-absorbs stock at cost; **Cancel** releases reservations

### Q: Can you skip order states?
No. Each transition is its own service method that inspects the current `Status` and throws `BadRequestException` on an illegal move (e.g. delivering a Draft).

---

## Part 7 — Billing (AR & AP)

### Q: What's the billing module?
Two mirror-image sub-modules:
- **Accounts Receivable (customer side)** — `Invoice` (+ `InvoiceItem`) and `Payment` (+ `PaymentAllocation`)
- **Accounts Payable (supplier side)** — `SupplierBill` (+ `SupplierBillItem`) and `SupplierPayment` (+ `SupplierPaymentAllocation`)

Both follow the same shape: a document with line items, totals (`SubTotal + Tax − Discount = TotalAmount`), an `AmountPaid` and computed `BalanceDue`, a status, and a payment that **allocates** across multiple documents.

### Q: How is an invoice created?
- **Manual**: `POST /api/v1/Invoices` (StaffUp) → starts **Draft**
- **From a sales order**: `POST /api/v1/Invoices/from-sales-order` (StaffUp) → `CreateFromSalesOrderAsync` copies SO items, starts **Issued**, backfills the SO with the invoice number, and refuses to invoice the same SO twice (`ConflictException`)
- `POST /{id}/issue` (StaffUp) Draft → Issued; `POST /{id}/cancel` (ManagerUp) only if nothing paid
- `GET /outstanding/{customerId}` (ViewerUp) feeds the payment UI

Number `INV-yyyyMMdd-####`. Status: Draft → Issued → PartiallyPaid → Paid (+ Overdue, Cancelled).

### Q: How is a supplier bill created?
- **Manual**: `POST /api/v1/SupplierBills` (StaffUp) → starts **Draft**
- **From a purchase order**: `POST /api/v1/SupplierBills/from-purchase-order` (StaffUp) → `CreateFromPurchaseOrderAsync` copies PO items, starts **Open**, refuses to bill the same PO twice
- `POST /{id}/approve` (StaffUp) Draft → Open; `POST /{id}/cancel` (ManagerUp) only if unpaid
- `GET /outstanding/{supplierId}` (ViewerUp) feeds the payment UI

Number `BILL-yyyyMMdd-####`, default due in 30 days. Status: Draft → Open → PartiallyPaid → Paid (+ Overdue, Cancelled).

### Q: Where does "generate bill / generate invoice" actually live?
**In the billing services, not the order services.** `PurchaseOrderService` and `SalesOrderService` have no bill/invoice generation methods. Generation is owned by `SupplierBillService.CreateFromPurchaseOrderAsync` and `InvoiceService.CreateFromSalesOrderAsync`, exposed via `/from-purchase-order` and `/from-sales-order`. This keeps the order modules focused on stock and the billing modules focused on money.

### Q: How does recording a payment allocate across documents?
`POST /api/v1/Payments` (customer) or `/api/v1/SupplierPayments` (supplier), both StaffUp. The service validates: amount > 0, the sum of allocations ≤ the payment amount, no document allocated twice, each document belongs to the same customer/supplier and isn't Draft/Cancelled, and each allocation ≤ that document's `BalanceDue`. Then per allocation it calls `document.ApplyPayment(amount)` — which advances the status (Issued/Open → PartiallyPaid → Paid) — and writes an allocation row. Customer payment number `PAY-yyyyMMdd-####`, supplier `SPAY-yyyyMMdd-####`. Supplier payments may be recorded without allocations (unallocated cash) and allocated later.

### Q: What stops double-paying or over-allocating?
The per-allocation guard `allocation.Amount ≤ document.BalanceDue`, plus the "no document allocated twice per payment" and "sum of allocations ≤ payment amount" checks. Domain methods (`ApplyPayment` / `ReversePayment`) also keep `AmountPaid` and `Status` consistent. (Note: `ReversePayment` exists on the entities but isn't yet exposed via an endpoint — see Part 16.)

### Q: Is Overdue automatic?
No. `Overdue` is a defined status but nothing auto-transitions a document to it today; due dates are stored and could be surfaced by a report or a future scheduled job.

---

## Part 8 — Background Jobs (Hangfire)

### Q: What jobs run on this project?
Two recurring jobs registered in `Program.cs`, both methods on `InventoryAlertJob`:
- `check-low-stock` — `Cron.Hourly`, scans inventory at/below reorder level, creates `LowStock` notifications
- `check-expiry-alerts` — `Cron.Daily`, finds items expiring within 30 days, creates `ExpiryAlert` notifications

### Q: Why Hangfire instead of `Task.Run` or a hosted service?
Persistence (jobs survive restart, stored in SQL), distributed locks (runs once across N instances), automatic retry with backoff, and a dashboard — all for free instead of building each.

### Q: Where's the dashboard and who can access it?
`/hangfire`, gated by [`HangfireAuthorizationFilter`](src/InventorySaaS.API/Middleware/HangfireAuthorizationFilter.cs). In dev it's permissive; in production, restrict to SuperAdmin.

### Q: How do I add a new recurring job?
1. Add a method to a job service in `Infrastructure/Services/BackgroundJobs/`
2. Register the service in `Infrastructure/DependencyInjection.cs`
3. In `Program.cs`: `RecurringJob.AddOrUpdate<MyJob>("id", j => j.RunAsync(), Cron.Daily);`
4. Restart — it appears in the dashboard and runs on schedule

### Q: What if a job throws?
Hangfire auto-retries (default 10 attempts, exponential backoff). After exhausting retries it sits in "Failed" until manually requeued.

---

## Part 9 — AI Features (Gemini)

### Q: What model does this project use?
`gemini-2.5-flash-lite` — Google's small, fast, free-tier-eligible model with vision + JSON mode. Used for both scan and chat. Called over plain REST (no SDK).

### Q: How does the product scan work?
1. User picks a JPEG/PNG (≤ 5 MB)
2. Frontend POSTs `multipart/form-data` to `/api/v1/Products/extract-from-image`
3. Controller validates size + MIME type
4. [`GeminiProductExtractionService`](src/InventorySaaS.Infrastructure/Services/AI/GeminiProductExtractionService.cs) base64-encodes the image, calls `generateContent` with `responseMimeType: "application/json"` and a strict prompt
5. Strips defensive markdown fences if present
6. Deserializes into `ProductExtractionResult`
7. Returns to the frontend; the form pre-fills

**The endpoint does not save.** The user reviews and submits via the normal `POST /api/v1/Products`.

### Q: What if Gemini returns garbage JSON?
Three layers: the prompt demands strict JSON; `responseMimeType: "application/json"` forces the model to validate its output; if parsing still fails, the controller returns a clean error (no auto-fill) and the user types the product manually.

### Q: How does the AI chat work?
`POST /api/v1/Chat` → [`AiChatService`](src/InventorySaaS.Infrastructure/Services/AI/AiChatService.cs) builds a per-request system prompt with live tenant data (KPIs, low stock, top products, recent transactions/orders) → calls Gemini's streaming endpoint → forwards SSE chunks straight to the browser → text renders token-by-token. Retry: 3 attempts, backoff 2s/4s/6s, on 429/503.

### Q: Why streaming for chat but not scan?
Chat is long free text — streaming cuts perceived latency. Scan needs the full JSON before parsing, so streaming would only complicate it.

### Q: What if the Gemini API key isn't configured?
Both AI features simply don't work; the rest of the app is fully functional. The missing key is handled at request time (no crash).

### Q: Where does the API key live?
- **Dev**: `dotnet user-secrets set "Gemini:ApiKey" "..."`
- **Prod**: env var `Gemini__ApiKey`
- **Never** in `appsettings.json`

---

## Part 10 — Frontend (Angular)

### Q: Why Angular?
Strong tooling for large enterprise apps, built-in DI, reactive forms, HTTP interceptors, a mature standalone-component model + signals. Suits a structured, multi-feature business app.

### Q: How is the frontend organised?
- `environment.ts` sets `apiUrl: 'http://localhost:5179'`
- **21 services** in [`core/services/`](inventory-saas-web/src/app/core/services/) — one per backend area plus `api.service`, `notification.service`, `ai-chat.service`, `tenant.service`
- **18 feature folders** in `features/` — auth, dashboard, products, categories, warehouses, inventory (stock-in / stock-transfer), suppliers, customers, purchase-orders, sales-orders, **invoices, payments, supplier-bills, supplier-payments**, reports, notifications, users, settings
- Two HTTP interceptors run on every request

### Q: How many interceptors are there, and what do they do?
**Two** (there is no tenant interceptor — the old docs were wrong):
- [`auth.interceptor.ts`](inventory-saas-web/src/app/core/interceptors/auth.interceptor.ts) — adds `Authorization: Bearer <jwt>`; on 401, silently calls `/refresh-token` and retries the original request
- [`error.interceptor.ts`](inventory-saas-web/src/app/core/interceptors/error.interceptor.ts) — global error handling, surfaces toasts

### Q: How do guards work?
[`auth.guard.ts`](inventory-saas-web/src/app/core/guards/auth.guard.ts) blocks unauthenticated users from everything inside `MainLayoutComponent`. [`role.guard.ts`](inventory-saas-web/src/app/core/guards/role.guard.ts) restricts `users` and `settings` to TenantAdmin/SuperAdmin.

### Q: How does the silent token refresh work?
401 → `auth.interceptor` calls `/auth/refresh-token` with the stored refresh token → stores the new pair → retries the original request. The user only sees a re-login if the refresh token itself is invalid.

### Q: What are signals used for?
The toast list. `NotificationService` exposes a `signal<Toast[]>([])`; components reading it auto-update. Lighter than RxJS for simple reactive state.

### Q: What does the dashboard show?
KPI cards (sales, purchases, inventory value, orders), secondary stats (products, warehouses, low-stock, expiring-soon), three **ngx-charts** visuals (Top Products by Value bar, Financial Snapshot doughnut, Stock Alerts horizontal bar), and lists (recent transactions, top products, low-stock products, recent sales). All from one `GET /api/v1/Dashboard`.

### Q: What's the routing structure?
[`app.routes.ts`](inventory-saas-web/src/app/app.routes.ts): four public `/auth/*` routes; everything else nested under `MainLayoutComponent` behind `authGuard`; `users` and `settings` additionally behind `roleGuard`. Components are eagerly loaded (no lazy loading). `''` and `**` redirect to `/dashboard`.

---

## Part 11 — Debugging, Logging, Observability

### Q: Where are logs stored?
Console (while running `dotnet run`) and a daily rolling file at `src/InventorySaaS.API/logs/log-YYYYMMDD.txt`, both via Serilog with structured entries.

### Q: What's a correlation ID?
A GUID assigned per request by [`CorrelationIdMiddleware`](src/InventorySaaS.API/Middleware/CorrelationIdMiddleware.cs). Pushed into Serilog's scope (every log line for that request includes it), returned as `X-Correlation-Id`, and included in error responses. To trace a request:
```powershell
Select-String -Path "src/InventorySaaS.API/logs/log-*.txt" -Pattern "<correlationId>"
```

### Q: Where do I set breakpoints to walk a request?
1. Controller action entry
2. Service method entry
3. Before the EF query
4. Before `SaveChangesAsync`
5. `ExceptionHandlingMiddleware` (catch-all)

### Q: How do I see the SQL EF Core generates?
The Output window shows `Executed DbCommand (...)` with parameters on every query in Debug. Copy-paste into SSMS to test.

### Q: How do I inspect the ChangeTracker mid-request?
While paused, in the Immediate window:
```csharp
_context.ChangeTracker.Entries().Select(e => $"{e.Entity.GetType().Name} ({e.State})").ToList()
```

### Q: How do I find who changed a record?
Query the `AuditLogs` table — it records action, entity type + id, old/new values, user, and timestamp for every change (see Part 5).

---

## Part 12 — Testing

### Q: What tests exist today?
- `tests/InventorySaaS.UnitTests/` — entity tests + `PasswordHasher`
- `tests/InventorySaaS.IntegrationTests/` — domain entity tests + `WebApplicationFactory` setup (`Program` is `public partial` for in-process testing)

Coverage is light. Highest-value additions: tenant-isolation integration tests, and the multi-step state machines — PO Approve→Receive→Return, SO Confirm→Deliver→Return, and the billing flows (invoice from SO, payment allocation reaching Paid).

### Q: How do I run tests?
```bash
dotnet test tests/InventorySaaS.UnitTests
dotnet test tests/InventorySaaS.IntegrationTests
dotnet test                              # all
```

### Q: How do I unit-test a service?
Mock `IApplicationDbContext` and `ICurrentUserService`, or use the EF Core in-memory provider for nicer `DbSet` ergonomics:
```csharp
var service = new CategoryService(contextMock.Object, currentUserMock.Object);
var result = await service.GetByIdAsync(id, CancellationToken.None);
result.Name.Should().Be("Expected");
```

---

## Part 13 — Deployment & Operations

### Q: How do I deploy this to production?
1. **Azure App Service** — easiest; a GitHub Action deploys on push to main
2. **Docker** — `docker-compose.yml` runs SQL Server + Redis + API + frontend
3. **Kubernetes** — API as a Deployment with HPA, SQL Server as StatefulSet (or managed), Hangfire dashboard internal-only

### Q: What environment variables do I need in production?
```
ConnectionStrings__DefaultConnection  = <prod sql connection>
ConnectionStrings__HangfireConnection = <prod sql connection or sub-DB>
ConnectionStrings__Redis              = <redis connection>     (optional)
JwtSettings__Secret                   = <strong random 64+ char string>
Gemini__ApiKey                        = <google ai api key>    (optional)
Smtp__Host / Smtp__Username / Smtp__Password / Smtp__From      (password-reset emails)
AllowedOrigins__0                     = https://your-frontend.example.com
```

### Q: How do I scale horizontally?
1. **API** — stateless; run N replicas behind a load balancer (JWT means any replica validates any request)
2. **Hangfire** — distributed locks ensure jobs run once across replicas
3. **Database** — scale SQL Server up first; read replicas for queries; shard by tenant only at very large scale
4. **Cache** — set `ConnectionStrings:Redis` so cache is shared across replicas

### Q: What's the dev workflow?
```
git pull
dotnet restore && dotnet build
dotnet run --project src/InventorySaaS.API           (terminal 1)
cd inventory-saas-web && npm install && npm start    (terminal 2)
# http://localhost:4200 — login admin@demo-company.com / Demo@123456
dotnet test
```

### Q: How are migrations applied in production?
On startup, `DatabaseSeeder.SeedAsync()` calls `Database.MigrateAsync()`. Risk: a slow migration blocks startup. For large datasets, run migrations as a separate deploy step before swapping the API version.

---

## Part 14 — Security

### Q: What security controls are in place?
- **JWT auth** with issuer/audience/lifetime/signing-key validation
- **Refresh-token rotation** to detect theft
- **PBKDF2-SHA512** password hashing with per-user salt
- **IP-based rate limiting** (stricter on `/auth`)
- **CORS** restricted to allowed origins in production
- **HTTPS redirection**
- **Tenant isolation** — combined query filter (active) + write-side auto-stamp
- **Soft delete** so accidental deletes don't lose data
- **Optimistic concurrency** for concurrent edits
- **Audit log** — every change recorded with user + old/new values
- **Global exception handling** — no stack traces leak to clients
- **Anti-enumeration** — login doesn't reveal whether the email exists; forgot-password always returns 200
- **Correlation IDs** for traceability

### Q: What about SQL injection?
EF Core parameterises every LINQ query. No raw SQL string concatenation anywhere.

### Q: What about XSS?
Angular escapes `{{ }}` interpolation by default. The main `[innerHTML]` surface is the chat panel rendering AI markdown, which goes through a sanitiser.

### Q: What about CSRF?
The JWT lives in the `Authorization` header, not a cookie, so a third-party site can't ride it. If you ever switch to cookie auth, add `[AutoValidateAntiforgeryToken]`.

### Q: What's the JWT `ClockSkew` setting?
`ClockSkew = TimeSpan.Zero` — tokens expire exactly at `exp` with no grace window. Strict, but it can cause spurious 401s if server clocks drift; consider a small skew (e.g. 1–2 min) if you see edge-of-expiry failures.

### Q: Any outstanding security items?
- Confirm the JWT secret is sourced from user-secrets (dev) / env var (prod), not committed in `appsettings.json`
- Lock the Hangfire dashboard to SuperAdmin in production
- Keep growing tenant-isolation integration tests as the regression net (the filter is correct today; tests keep it that way)

---

## Part 15 — The CQRS → Service migration

### Q: What changed?
The Application layer moved from CQRS-via-MediatR to plain Controller → Service:
- Deleted the `*Command` / `*Handler` / `*Query` files
- Created services under `Application/Services/` (interface + implementation per module)
- Rewrote controllers to be thin (~2 lines per action)
- Removed MediatR, FluentValidation, AutoMapper
- Removed the `Result<T>` wrapper — services return DTOs or throw typed exceptions
- Removed `ValidationBehavior` / `LoggingBehavior`
- Simplified `ExceptionHandlingMiddleware` (no longer catches `FluentValidation.ValidationException`)

### Q: Why?
Fewer files per endpoint, flatter stack traces, lower onboarding cost, faster builds — and no CQRS payoff in an app with shared read/write models, no event sourcing, no message bus.

### Q: What did we lose, and how is it replaced?
MediatR's automatic validation + logging. Replaced by model binding (data annotations) + inline service guards for validation, and Serilog's request logger + correlation IDs for logging.

### Q: Was it worth it?
Yes. The litmus test for CQRS is "do my reads scale differently from my writes?" — for inventory/billing CRUD with shared models, no. CQRS earns its keep with separate read stores, projections, event sourcing, or a message bus — none apply here.

### Q: What bug fixes were bundled in?
- Several `Update` endpoints returned 400 on "not found"; now 404
- Several `Create` endpoints returned 400 on duplicate code; now 409 Conflict
- The multi-tenant query filter no-op was fixed (combined tenant + soft-delete; see Part 4)

---

## Part 16 — Things I'd improve next (honest)

### High priority
1. **Enforce subscription-plan limits** at the API (users/warehouses/products/feature gates) — entities and limits are seeded but unchecked
2. **Expose payment reversal** — `Invoice.ReversePayment` / `SupplierBill.ReversePayment` exist on the entities but no endpoint calls them; needed to undo a misapplied payment
3. **Auto-flag Overdue** — a scheduled job (or report) that marks Issued/Open documents past their due date
4. **Grow tenant-isolation + state-machine tests** — the filter and order/billing flows are the riskiest paths

### Medium priority
5. **Data annotations on all Request DTOs** — restore automatic 400-on-validation lost with FluentValidation
6. **Explicit decimal precision** on money columns (`HasPrecision(18, 4)`) to silence the default `decimal(18,2)` warning and avoid rounding surprises on tax/discount
7. **Relax `ClockSkew`** from zero to a small window to avoid edge-of-expiry 401s
8. **Production Hangfire hardening** — restrict the dashboard to SuperAdmin

### Lower priority
9. **Localization** — English-only today
10. **PWA / offline support for stock movements** — high value for warehouse workers on spotty connectivity
11. **Aging reports** for AR/AP (30/60/90-day buckets) now that the billing data exists

---

## Quick reference

### File locations
| What | Path |
|---|---|
| Entry point | [`src/InventorySaaS.API/Program.cs`](src/InventorySaaS.API/Program.cs) |
| App config | [`src/InventorySaaS.API/appsettings.json`](src/InventorySaaS.API/appsettings.json) |
| Controllers (19) | [`src/InventorySaaS.API/Controllers/`](src/InventorySaaS.API/Controllers/) |
| Middleware (4) | [`src/InventorySaaS.API/Middleware/`](src/InventorySaaS.API/Middleware/) |
| Services (18 modules) | [`src/InventorySaaS.Application/Services/`](src/InventorySaaS.Application/Services/) |
| DTOs | [`src/InventorySaaS.Application/Features/{Module}/DTOs/`](src/InventorySaaS.Application/Features/) |
| EF DbContext | [`src/InventorySaaS.Infrastructure/Persistence/ApplicationDbContext.cs`](src/InventorySaaS.Infrastructure/Persistence/ApplicationDbContext.cs) |
| Seeder | [`src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs`](src/InventorySaaS.Infrastructure/Persistence/Seed/DatabaseSeeder.cs) |
| Auth services | [`src/InventorySaaS.Infrastructure/Services/Auth/`](src/InventorySaaS.Infrastructure/Services/Auth/) |
| Hangfire jobs | [`src/InventorySaaS.Infrastructure/Services/BackgroundJobs/`](src/InventorySaaS.Infrastructure/Services/BackgroundJobs/) |
| AI integrations | [`src/InventorySaaS.Infrastructure/Services/AI/`](src/InventorySaaS.Infrastructure/Services/AI/) |
| Domain entities (36) | [`src/InventorySaaS.Domain/Entities/`](src/InventorySaaS.Domain/Entities/) |
| Domain exceptions | [`src/InventorySaaS.Domain/Exceptions/DomainException.cs`](src/InventorySaaS.Domain/Exceptions/DomainException.cs) |
| Frontend routes | [`inventory-saas-web/src/app/app.routes.ts`](inventory-saas-web/src/app/app.routes.ts) |
| Frontend services (21) | [`inventory-saas-web/src/app/core/services/`](inventory-saas-web/src/app/core/services/) |
| Interceptors (2) | [`inventory-saas-web/src/app/core/interceptors/`](inventory-saas-web/src/app/core/interceptors/) |

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

*Last updated to reflect the current codebase: Controller → Service pattern (no MediatR / FluentValidation / AutoMapper), the combined-and-active multi-tenant query filter, the live audit log, and the AR/AP billing module (invoices, payments, supplier bills, supplier payments). For the original CQRS-era documentation, see git history.*
