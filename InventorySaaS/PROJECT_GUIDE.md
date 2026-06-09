# InventorySaaS — Project Understanding Guide

> A study guide written for the developer of this project. It explains every term, how each piece is implemented, what it does, and the most likely questions someone will ask you about it — with prepared answers. Read it once end-to-end, then come back to it before any demo or interview.
>
> Companion file: [Q_AND_A.md](Q_AND_A.md) for interview/demo Q&A, and [USER_MANUAL.md](USER_MANUAL.md) for end-user workflows and the role permission matrix.

---

## Table of Contents

1. [What this project is, in plain language](#1-what-this-project-is-in-plain-language)
2. [The big picture diagram](#2-the-big-picture-diagram)
3. [Architecture: Clean Architecture explained](#3-architecture-clean-architecture-explained)
4. [Glossary of every term used in this project](#4-glossary-of-every-term-used-in-this-project)
5. [Request lifecycle: what happens when a user clicks a button](#5-request-lifecycle-what-happens-when-a-user-clicks-a-button)
6. [Module walkthroughs](#6-module-walkthroughs)
7. [Common interview / demo questions and how to answer them](#7-common-interview--demo-questions-and-how-to-answer-them)
8. [Tradeoffs and decisions you should be able to defend](#8-tradeoffs-and-decisions-you-should-be-able-to-defend)
9. [Demo script — 5 minute version](#9-demo-script--5-minute-version)
10. [Things you don't know yet, and that's OK](#10-things-you-dont-know-yet-and-thats-ok)

---

## 1. What this project is, in plain language

**One sentence**: A web app that helps a business track its products, warehouses, stock movements, purchase and sales orders, and the money side (customer invoices/payments and supplier bills/payments), built so many different companies can use the same app at the same time without seeing each other's data.

**Real-world analogy**: Think of Shopify. Many merchants use Shopify, but each merchant only sees their own products and orders. They share the same code and the same servers, but the data is isolated. That's exactly what we built.

**Who would use it**:
- A pharmacy chain tracking which medicines are about to expire
- A retail manager who wants to see today's sales vs purchases
- A warehouse worker recording incoming stock or transferring it between locations
- An accountant raising invoices to customers and paying supplier bills
- An owner pulling a "what is my inventory worth?" report

**What's the AI for?**:
- **Scan a product photo** → the AI reads the label, fills in name/brand/barcode/price for you, so you don't have to type. Useful for adding hundreds of products fast.
- **Chat with your inventory** → ask "what's running low?" or "how were last week's sales?" and the AI answers using your real data.

---

## 2. The big picture diagram

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│   Browser       │◄───────►│   API           │◄───────►│   SQL Server    │
│   (Angular 19)  │  HTTPS  │   (.NET 10)     │  EF Core│  (2 databases)  │
└─────────────────┘         └─────────────────┘         └─────────────────┘
                                    │
                                    │  REST calls
                                    ▼
                            ┌─────────────────┐
                            │   Gemini AI     │   (chat + product scan)
                            │   (Google)      │
                            └─────────────────┘
                                    │
                                    │  jobs run on a schedule
                                    ▼
                            ┌─────────────────┐
                            │   Hangfire      │   (low stock checker, expiry checker)
                            │   (in-process)  │
                            └─────────────────┘
```

You have:
- **A frontend** the user sees (Angular 19, standalone components + signals)
- **A backend** that does the work and talks to the database (.NET 10 API)
- **A database** that stores everything (SQL Server — one app DB plus a separate Hangfire DB)
- **An external AI** that the backend calls when needed (Google Gemini, over plain REST — no SDK)
- **A background worker** that runs scheduled tasks (Hangfire, lives inside the same .NET process)

---

## 3. Architecture: Clean Architecture explained

### What is "Clean Architecture"?

Your backend is split into **four projects**:

```
InventorySaaS.Domain          ← the rules of your business
       ▲
InventorySaaS.Application     ← what the app DOES (services like "create product")
       ▲
InventorySaaS.Infrastructure  ← HOW it talks to the outside world (DB, email, AI)
       ▲
InventorySaaS.API             ← entry point (HTTP controllers)
```

**The arrows go upward**: outer layers depend on inner layers, but inner layers know nothing about outer layers.

### Why bother?

Because if tomorrow you want to:
- Switch from SQL Server to PostgreSQL → you only change **Infrastructure**. Domain and Application don't even notice.
- Switch from a REST API to a desktop app → you only change **API**. Application logic is unchanged.
- Add a new way to create a product (e.g., bulk CSV import) → you write a new method in a service in **Application**, the API just exposes it.

### Layer-by-layer

#### `InventorySaaS.Domain` — the heart
- Contains pure C# classes (entities) like `ProductInfo`, `TenantInfo`, `InventoryBalance`, `Invoice`, `SupplierBill` (36 entity files total)
- **Knows nothing** about EF Core, HTTP, or any framework — it has zero NuGet references
- Defines what it MEANS to be a Product, not how it's stored
- Also holds enums (`OrderStatus`, `TransactionType`, `InvoiceStatus`, `BillStatus`, `PaymentMethod`, …), base classes (`BaseEntity`, `TenantEntity`), and the typed domain exceptions

#### `InventorySaaS.Application` — the use cases
- Contains one **service per business module** — **18 services** (`IProductService`, `IInventoryService`, `IAuthService`, `ISalesOrderService`, `IInvoiceService`, `ISupplierBillService`, etc.), each with an interface + an implementation
- Each service exposes the operations that module supports (`CreateAsync`, `GetByIdAsync`, `ApproveAsync`, etc.)
- Services depend only on `IApplicationDbContext` (interface in this layer) and pure C# — no EF Core, no HTTP, no framework
- This layer says *what* should happen, not *how*

#### `InventorySaaS.Infrastructure` — the plumbing
- EF Core `ApplicationDbContext` (talks to SQL Server) + per-entity `IEntityTypeConfiguration` classes
- Hangfire setup (background jobs)
- Email service (SMTP)
- Local file storage
- AI integrations (Gemini chat + Gemini product extraction)
- Auth services: `TokenService` (JWT), `PasswordHasherService`, `TenantAccessor`, `CurrentUserService`
- PDF report rendering (QuestPDF)
- This is where the *how* lives

#### `InventorySaaS.API` — the front door
- ASP.NET Core controllers (**19 of them**)
- Middleware (correlation ID, exception handling, tenant resolution)
- `Program.cs` — wiring, auth, CORS, rate limiting, Swagger, Hangfire dashboard, seeding, recurring jobs
- The bit that actually accepts HTTP requests

### How does a request flow through the layers?

A user clicks "Create Product" in the browser:

1. **Frontend** sends `POST /api/v1/Products` with JSON
2. **API layer** receives it (`ProductsController`)
3. Controller calls `_productService.CreateAsync(request, cancellationToken)`
4. **Application layer**: `ProductService.CreateAsync` runs the use case — looks up the category, generates a unique SKU, builds the entity
5. Service calls `_context.SaveChangesAsync()` on `IApplicationDbContext` — **Infrastructure layer**'s EF Core writes to SQL Server
6. Service returns a `ProductDto`; the controller wraps it in `Ok(...)` / `CreatedAtAction(...)`
7. If anything goes wrong, the service throws a typed exception (`NotFoundException` / `BadRequestException` / `ConflictException`); the global `ExceptionHandlingMiddleware` turns it into the right HTTP code + JSON envelope

---

## 4. Glossary of every term used in this project

### Architecture & Patterns

#### **SaaS (Software as a Service)**
You don't install the app — you log in to a website. Gmail, Shopify, Slack are all SaaS. Yours too.

#### **Multi-tenant**
One running app serves many customers ("tenants"). Each tenant only sees their own data. The alternative ("single-tenant") would be running a separate copy of the app for each customer — expensive and slow to update.

#### **Tenant isolation**
The mechanism that makes sure tenant A can never see tenant B's data. In your project this is done three ways:
1. Every tenant-scoped row inherits `TenantEntity`, which adds a `TenantId` column.
2. EF Core "global query filters" automatically add `WHERE TenantId = X` to every read query, where X comes from the user's JWT.
3. The `SaveChangesAsync` override **auto-stamps** `TenantId` on every newly inserted tenant row, so a developer can't forget.

#### **Clean Architecture**
A way of organising code so the business rules are at the centre and don't depend on frameworks (database, web, etc.). Your four-project split is Clean Architecture.

#### **Controller → Service pattern**
The shape your project uses. Each controller action does **only** these three things:
1. Bind the HTTP input (route + body + query) into a Request DTO
2. Call the matching method on the service (`_productService.CreateAsync(request, ct)`)
3. Wrap the returned DTO in `Ok(...)` (or `CreatedAtAction(...)`)

The service is where the actual work lives — DB lookups, validations, business rules, building the response DTO. On failure the service **throws** a typed exception; it doesn't return a wrapper object.

```csharp
// In ProductsController — the whole action:
[HttpPost]
public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
{
    var result = await _productService.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

This used to be CQRS-with-MediatR (separate `*Command` + `*Handler` files dispatched via `IMediator`). It was migrated to plain services for clarity and lower file count. Validation and logging — previously implemented as MediatR pipeline behaviors — are now handled by ASP.NET's model binding and Serilog's request logger.

#### **Domain exceptions**
Defined in [Domain/Exceptions/DomainException.cs](src/InventorySaaS.Domain/Exceptions/DomainException.cs). The set:
- `NotFoundException` → 404
- `ConflictException` → 409 (e.g. duplicate code, duplicate email, "PO already billed", "SO already invoiced")
- `BadRequestException` → 400 (e.g. "quantity must be > 0", illegal state transition)
- `ForbiddenAccessException` → 403
- `DomainException` (base) → 500 if uncaught

Services throw them; the global [`ExceptionHandlingMiddleware`](src/InventorySaaS.API/Middleware/ExceptionHandlingMiddleware.cs) catches them and produces a uniform `ProblemResponse` JSON. This is *the* mechanism that keeps controllers thin.

#### **DTO (Data Transfer Object)**
A simple class/record that carries data between layers, e.g. between the API and the frontend, or between the service and the controller. They're "anaemic" — no behaviour, just properties. Typically per module:
- `XxxDto` — what the API returns
- `CreateXxxRequest` — body for POST endpoints
- `UpdateXxxRequest` — body for PUT endpoints

#### **Entity**
A class that represents a "thing" in your business — Product, Customer, Warehouse, Invoice. Lives in the Domain layer. Maps to a database table via EF Core. Some entities carry **behaviour methods** (e.g. `Invoice.ApplyPayment(amount)`, `InventoryBalance.ApplyInbound(qty, cost)`) — small bits of domain logic that live on the entity itself.

#### **Repository pattern (and why we don't use it)**
The classic version wraps DbContext behind a per-entity interface (`IProductRepository`). Your project uses a lighter version — services depend on `IApplicationDbContext` (one interface, exposes every `DbSet<>`). Less ceremony, full LINQ power, identical testability via integration tests.

### Authentication & Authorization

#### **JWT (JSON Web Token)**
A signed token that proves who you are. When you log in, the API generates a JWT containing your user ID, email, role, and tenant ID. You send it back with every request as `Authorization: Bearer <token>`. The API verifies the signature and reads your claims from it.

A JWT looks like three base64 chunks separated by dots:
```
eyJhbGc...  .  eyJuYW1l...  .  signaturebytes
header        payload (claims)   signature
```

The backend doesn't need to look anything up to know who you are — it just verifies the signature using a secret key.

#### **Refresh token**
JWTs expire (yours: 60 minutes). Instead of making the user log in every hour, the frontend silently calls `/auth/refresh-token` with a long-lived **refresh token** to get a new JWT. Each refresh **rotates** the token (old one becomes invalid, `ReplacedByToken` points at the new one) — this catches stolen tokens.

#### **Claim**
A piece of information stored inside a JWT: `email`, `tenant_id`, `role`, `full_name`. The backend reads these to know who's making the request.

#### **RBAC (Role-Based Access Control)**
Each user has one or more **roles** (SuperAdmin, TenantAdmin, Manager, Staff, Viewer). Each role can do certain things. The seeder also creates a fine-grained **permission** table (12 modules with actions), though authorization at the API today is enforced by the five role-based policies.

#### **Authorization Policy**
ASP.NET Core's way of grouping role checks under a name. Yours (in `Program.cs`):
- `SuperAdminOnly` → SuperAdmin
- `TenantAdminOnly` → TenantAdmin, SuperAdmin
- `ManagerUp` → Manager, TenantAdmin, SuperAdmin
- `StaffUp` → Staff, Manager, TenantAdmin, SuperAdmin
- `ViewerUp` → Viewer, Staff, Manager, TenantAdmin, SuperAdmin

You apply them with `[Authorize(Policy = "StaffUp")]` on a controller or action. The common pattern: a controller is `[Authorize(Policy = "ViewerUp")]` at the class level (everyone can read), and write actions override with `StaffUp` (create/operate) or `ManagerUp` (approve/cancel/adjust).

#### **Middleware**
Code that runs before/after every HTTP request. Yours, in order (from `Program.cs`):
1. `CorrelationIdMiddleware` — assigns a unique ID to each request
2. `ExceptionHandlingMiddleware` — catches errors and returns a clean JSON response
3. HTTPS redirect, CORS, IP rate limiting
4. JWT authentication
5. `TenantResolutionMiddleware` — runs after auth, logs/establishes tenant context
6. Authorization (policy checks)

### Database

#### **EF Core (Entity Framework Core)**
Microsoft's ORM. You write C# code like `db.Products.Where(p => p.Name == "X")`, and EF Core translates it to SQL. You don't write SQL by hand.

#### **DbContext**
The object that represents your database connection. `ApplicationDbContext` exposes `DbSet<ProductInfo>`, `DbSet<TenantInfo>`, `DbSet<Invoice>`, `DbSet<SupplierBill>`, etc. — and overrides `OnModelCreating` (query filters) and `SaveChangesAsync` (audit + auto-stamping).

#### **Migration**
A C# file describing a change to the database schema (adding a column, etc.). When the app starts, the seeder calls `Database.MigrateAsync()` to apply pending migrations. Current migrations include the initial create plus the recent billing additions (`AddBillingArInvoicesAndPayments`, `AddBillingApSupplierBillsAndPayments`, `AddPurchaseOrderItemReturnedQuantity`).

#### **Global Query Filter**
A LINQ expression EF Core silently adds to every query against an entity. This is how tenant isolation and soft-delete are enforced. The single combined filter per tenant entity is built in `ApplicationDbContext.OnModelCreating`:
```csharp
e => (_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId) && !e.IsDeleted
```

> **Important gotcha (and the bug that was fixed)**: EF Core allows **only one** query filter per entity — a second `HasQueryFilter` call **overwrites** the first, it does not combine them. An earlier version (a) wrote the tenant predicate so it always evaluated to `true` (no tenant scoping at all), and (b) then called `HasQueryFilter` a second time for soft-delete, silently discarding the first. Net effect: **neither** tenant isolation nor soft-delete filtering was active. The current code builds **one** combined filter per tenant entity (shown above) via reflection over all entity types in `OnModelCreating`. A `null` tenant (seeding, registration, a SuperAdmin acting without tenant context) bypasses the tenant clause but still respects soft-delete. Non-tenant `BaseEntity` types get a soft-delete-only filter.

#### **Soft delete**
Instead of `DELETE FROM Products WHERE Id = X`, you set `IsDeleted = true`. The row stays in the database but is hidden by the global filter. Lets you recover deletes and audit history. (Use `IgnoreQueryFilters()` to see deleted rows.)

#### **Optimistic concurrency**
Two users edit the same record at the same time. Without protection, the second save silently overwrites the first. Optimistic concurrency uses a `RowVersion` column on `BaseEntity` (auto-managed by SQL Server) — when the second user tries to save, EF Core sees their `RowVersion` is stale and throws `DbUpdateConcurrencyException` so you can show a "this record was modified" message.

#### **Audit log**
Every change to a `BaseEntity` is recorded. The `SaveChangesAsync` override in `ApplicationDbContext` collects an `AuditLog` row for each Added/Modified/Deleted entity — capturing the action (Create/Update/Delete), the entity type and id, the changed fields (old vs new values as JSON), the acting user, and a timestamp — then writes them in a second save. A soft-delete is recorded as a `Delete` action. **This is live**, not a placeholder.

#### **Seeding**
Creating starter data when the database is empty. Each step in `DatabaseSeeder.cs` is **idempotent** (checks before inserting). It seeds: 4 subscription plans, 5 roles, 12 permission modules, a SuperAdmin user, a "Demo Company" tenant with admin user, and demo data (categories, brands, units, 5 products, 2 warehouses + 3 locations, 2 suppliers, 2 customers, randomized inventory balances).

### Inventory mechanics

#### **InventoryBalance**
One row per (product, warehouse, location, batch/lot). Holds `QuantityOnHand`, `QuantityReserved`, and a moving weighted-average `UnitCost`. Computed `QuantityAvailable = QuantityOnHand − QuantityReserved`. Has an `ApplyInbound(qty, cost)` method that re-computes the weighted-average cost when new stock arrives.

#### **InventoryTransaction**
One row per movement — the full ledger. Records a `TransactionType`, quantity, unit cost, batch/expiry, and a cross-reference back to the source document (PO/SO number). `TransactionType` values: `StockIn, StockOut, Transfer, Adjustment, Return, Damaged, Lost, PurchaseReceive, SalesIssue, PurchaseReturn`.

#### **Moving weighted-average cost**
When stock arrives at a balance that already has units, the new unit cost is blended in proportionally rather than replaced. Keeps inventory valuation realistic across purchases at different prices.

#### **FIFO by expiry**
When goods are reserved (SO Confirm) or shipped (SO Deliver), the system consumes balances ordered by `ExpiryDate` (earliest first) — so perishable stock leaves first. Delivery records cost at **COGS** (actual cost), never the selling price.

### Background processing

#### **Background job**
Code that runs *outside* the request-response cycle. Yours are **recurring** jobs registered in `Program.cs` and run via Hangfire.

#### **Hangfire**
A C# library that handles the queue, retries, persistence to SQL Server, and a dashboard for background jobs. Your project uses Hangfire for two recurring jobs (both methods on `InventoryAlertJob`) and provides a dashboard at `/hangfire`.

#### **Cron expression**
The string that says when a recurring job should run. `Cron.Hourly` (low-stock check) and `Cron.Daily` (expiry check) are used here.

### API & HTTP

#### **REST API**
URLs represent resources (`/api/v1/Products/123`), HTTP verbs do operations (GET/POST/PUT/DELETE), responses are JSON. Yours is REST, versioned under `/api/v1/`.

#### **Swagger / OpenAPI**
Auto-generated, interactive API documentation at `/swagger` (development only). Configured with a Bearer-token security scheme so you can authorize and try endpoints.

#### **CORS (Cross-Origin Resource Sharing)**
Browser security: a page at `localhost:4200` can't call an API at `localhost:5179` unless the API allows it. In development, `Program.cs` allows any `localhost` origin; in production it reads `AllowedOrigins` from config.

#### **Rate limiting**
`AspNetCoreRateLimit` caps requests per IP — general traffic and stricter limits on `/auth` endpoints (configured under `IpRateLimiting` in `appsettings.json`).

#### **Correlation ID**
A unique GUID assigned to each HTTP request by `CorrelationIdMiddleware`. Pushed into Serilog's log scope, returned in the `X-Correlation-Id` header, and included in error responses. One ID ties together every log line for a request.

#### **Server-Sent Events (SSE)**
A way for the server to **stream** data to the browser over a single open HTTP connection. The AI chat endpoint uses SSE to stream tokens as the model generates them — text appears word-by-word.

### Frontend (Angular)

#### **Standalone components**
Modern Angular components that don't need an `NgModule`. Each lists its own imports. The whole project uses standalone components.

#### **Reactive Forms**
Forms backed by a TypeScript `FormGroup` instead of HTML-only. Validation, value tracking, dirty/touched state — all easy. The product, invoice, payment, order forms use Reactive Forms.

#### **Pipe**
A small template transformer: `{{ price | currency:'BDT':'৳':'1.2-2' }}`.

#### **Observable**
An RxJS stream of values over time. HTTP calls return Observables; you `.subscribe()` to get the result.

#### **HTTP Interceptor**
Code that runs before every outgoing HTTP request. Yours (there are **two**):
- `auth.interceptor.ts` — adds `Authorization: Bearer <token>`; on 401 silently refreshes the token and retries the request
- `error.interceptor.ts` — global error handling, surfaces toasts

(There is no separate tenant interceptor — the tenant comes from the JWT claim server-side.)

#### **Signal**
Angular's reactive primitive. A `signal<T>()` holds a value; reading it in a template tracks it; updating it re-renders the parts that read it. `NotificationService` uses signals for the toast list.

#### **Router outlet**
The placeholder where Angular renders the current route's component. `<router-outlet></router-outlet>`.

#### **ngx-charts**
The charting library used on the dashboard — bar charts (top products, stock alerts) and a doughnut chart (financial snapshot).

### AI

#### **Gemini**
Google's family of LLMs. You use `gemini-2.5-flash-lite` — small, fast, free-tier eligible — for both chat and product extraction.

#### **System prompt**
Hidden instructions to the model. Your AI chat builds a system prompt containing a live snapshot of the tenant's inventory.

#### **Vision model**
An LLM that accepts images. The image is sent base64-encoded inside the request. Gemini 2.5 Flash Lite supports vision — that's what powers "Scan from Photo".

#### **JSON mode**
Telling the model its response MUST be valid JSON, via `responseMimeType: "application/json"`. Product extraction uses this so the result deserializes directly into a typed DTO.

#### **Streaming (vs single response)**
Chat uses streaming (SSE) so the user sees text in real time. Product extraction uses non-streaming because it needs the full JSON before parsing.

### Billing

#### **Accounts Receivable (AR)**
Money customers owe you. Modelled by `Invoice` (+ `InvoiceItem`) and `Payment` (+ `PaymentAllocation`). An invoice can be created manually or generated from a sales order; a payment can be split across several invoices.

#### **Accounts Payable (AP)**
Money you owe suppliers. Modelled by `SupplierBill` (+ `SupplierBillItem`) and `SupplierPayment` (+ `SupplierPaymentAllocation`). A bill can be created manually or generated from a purchase order; a supplier payment can be split across several bills.

#### **Payment allocation**
One payment, split across multiple invoices/bills. Each allocation row records how much of the payment applies to which document, and calls that document's `ApplyPayment(amount)` to advance its status (Issued/Open → PartiallyPaid → Paid).

### Security & operations

#### **Hashing (PBKDF2-SHA512)**
You don't store passwords. You store a salted, slow PBKDF2-SHA512 **hash**. Even if the database leaks, attackers can't reverse it.

#### **Salt**
A random string mixed into the password before hashing, so the same password never produces the same hash twice. Stops precomputed "rainbow table" attacks.

#### **Health check**
`/health` returns 200 if the app (and its database) is healthy. Used by load balancers.

#### **Structured logging**
Logging key-value pairs instead of plain text, via Serilog. Writes to console and a daily rolling file under `logs/`.

#### **Connection string**
Tells the app where the database is. Two are used: `DefaultConnection` (app DB) and `HangfireConnection` (Hangfire DB). Live in `appsettings.json` / env vars.

#### **User secrets**
A per-machine encrypted store for sensitive config (Gemini API key, JWT secret) in development, kept out of `appsettings.json`. In production, environment variables (`Section__Key` convention).

---

## 5. Request lifecycle: what happens when a user clicks a button

Let's trace what happens when a user clicks "Save" on the product form.

```
USER CLICKS SAVE
  │
  ├─ 1. Angular form (product-form.component.ts) calls onSubmit()
  │
  ├─ 2. ProductService.create(data) — observable
  │
  ├─ 3. ApiService.post() — wraps HttpClient.post
  │
  ├─ 4. authInterceptor adds "Authorization: Bearer <jwt>"
  │
  ▼ NETWORK
  │
  ├─ 5. Kestrel (the .NET web server) receives the HTTP request
  │
  ├─ 6. CorrelationIdMiddleware assigns ID, adds to logs and response header
  │
  ├─ 7. ExceptionHandlingMiddleware wraps everything in try/catch
  │
  ├─ 8. CORS + IP rate limiting checks
  │
  ├─ 9. JWT auth: signature verified, claims (nameid, tenant_id, role) loaded into HttpContext.User
  │
  ├─ 10. TenantResolutionMiddleware establishes tenant context (ITenantAccessor)
  │
  ├─ 11. Authorization policy [Authorize(Policy = "StaffUp")] — pass
  │
  ├─ 12. ProductsController.Create() runs
  │
  ├─ 13. Controller calls _productService.CreateAsync(request, cancellationToken)
  │
  ├─ 14. ProductService.CreateAsync runs:
  │      - looks up category, brand, unit
  │      - generates unique SKU
  │      - creates new ProductInfo entity
  │      - calls _context.SaveChangesAsync()
  │      (any business-rule failure → throws BadRequestException / NotFoundException /
  │       ConflictException; ExceptionHandlingMiddleware turns it into the right status code)
  │
  ├─ 15. SaveChangesAsync override auto-stamps CreatedAt / CreatedBy / TenantId
  │      and collects AuditLog rows for the change
  │
  ├─ 16. EF Core translates to SQL: INSERT INTO Products (...) VALUES (...)
  │
  ├─ 17. SQL Server executes the insert, returns the new row
  │
  ├─ 18. Service builds a ProductDto and returns it directly (no wrapper)
  │
  ├─ 19. Serilog's request logger emits "Request finished ... 201 ... 35ms"
  │
  ├─ 20. Controller returns CreatedAtAction(...) — HTTP 201 with the new product as JSON
  │
  ▼ NETWORK
  │
  ├─ 21. Browser receives JSON
  │
  ├─ 22. Angular ProductFormComponent: success callback runs
  │
  ├─ 23. NotificationService.success("Product created")
  │
  ├─ 24. ToastContainerComponent re-renders (signal change)
  │
  └─ 25. User sees a green toast
```

That's the full path. Most steps are invisible — the framework handles them. Your code lives in steps 1–4, 12–14, 18, and 21–24.

---

## 6. Module walkthroughs

### 6.1 Authentication module

**What it does**: lets users register a new tenant, log in, log out, refresh their token, reset their password.

**Files**:
- `Application/Services/IAuthService.cs` + `AuthService.cs`
- `Application/Features/Auth/DTOs/AuthDtos.cs`
- `Infrastructure/Services/Auth/TokenService.cs` — generates and validates JWTs + refresh tokens
- `Infrastructure/Services/Auth/PasswordHasherService.cs` (+ `PasswordHasher` static helper)
- `API/Controllers/AuthController.cs`

**How login works**:
1. User POSTs `{ email, password }` to `/api/v1/Auth/login`
2. `AuthService.LoginAsync` looks up the user by `NormalizedEmail`
3. `PasswordHasherService.Verify(input, storedHash)` — slow PBKDF2 compare
4. If invalid → throws `UnauthorizedAccessException` → middleware → 401
5. If valid → `TokenService.GenerateTokensAsync` builds a JWT (claims: nameid, email, tenant_id, full_name, role) **and** a refresh-token row
6. `LastLoginAt` is updated; both tokens + the user DTO are returned
7. When the JWT expires (60 min), the frontend silently calls `/auth/refresh-token` (refresh-token rotation: old token revoked, new one issued)

**How registration works**:
1. POST `/api/v1/Auth/register` with company + admin details
2. Creates a `TenantInfo` (auto slug = kebab-case name + GUID suffix), assigns the Free plan
3. Creates the admin user (TenantAdmin role), hashes the password
4. Issues a JWT + refresh token immediately — user is logged in

### 6.2 Product module

**What it does**: create, list, view, edit, delete products. Includes the AI "Scan from Photo".

**Files**:
- `Application/Services/IProductService.cs` + `ProductService.cs`
- `Application/Features/Products/DTOs/ProductDtos.cs`, `ProductExtractionDtos.cs`
- `API/Controllers/ProductsController.cs` — the `extract-from-image` action calls `IProductExtractionService` directly (no persistence)
- `Infrastructure/Services/AI/GeminiProductExtractionService.cs`

**SKU auto-generation**: take the first 3 letters of the category name uppercased ("Food & Beverage" → "FOO"), find the highest existing number with that prefix, new SKU = `{prefix}-{max+1:D5}` → "FOO-00007".

**AI scan flow**: upload image → `POST /api/v1/Products/extract-from-image` (JPEG/PNG, ≤ 5 MB) → Gemini vision with strict-JSON prompt → parsed `ProductExtractionResult` → frontend pre-fills the form → user reviews → normal `POST /api/v1/Products`. The extract endpoint **never saves** — it's a draft generator.

### 6.3 Inventory module

**What it does**: tracks how much of each product is in each warehouse/location/batch, plus the full movement ledger.

**Key concepts**: `InventoryBalance` (quantity on hand + reserved + weighted-avg cost) and `InventoryTransaction` (the ledger).

**Operations** (`InventoryController` → `InventoryService`):
| Operation | Route | Policy | What it does |
|---|---|---|---|
| Balances | `GET /api/v1/Inventory/balances` | ViewerUp | list balances |
| Transactions | `GET /api/v1/Inventory/transactions` | ViewerUp | list the ledger |
| Stock In | `POST /api/v1/Inventory/stock-in` | StaffUp | `ApplyInbound` (blends cost), writes `StockIn` txn |
| Stock Out | `POST /api/v1/Inventory/stock-out` | StaffUp | decrements on-hand, writes `StockOut` txn |
| Transfer | `POST /api/v1/Inventory/transfer` | StaffUp | moves stock between warehouses/locations, carries cost forward, writes `Transfer` txn |
| Adjustment | `POST /api/v1/Inventory/adjustment` | ManagerUp | reconciles to a new quantity with a reason, writes `Adjustment` txn |

Each operation writes a transaction (audit) and updates the matching balance in the same `SaveChangesAsync` — both succeed or both roll back. **Why the ledger pattern?** You can always reconstruct the balance by summing transactions; the ledger is the source of truth.

### 6.4 Purchase Orders & Sales Orders

**Purchase Orders** (`PurchaseOrderService`): `Create` (Draft, auto number `PO-yyyyMMdd-####`) → `Approve` (ManagerUp) → `Receive` (StaffUp; increments `ReceivedQuantity`, calls `ApplyInbound` at PO unit price, writes `PurchaseReceive` txns, sets `Received`/`PartiallyReceived`) → `Return` (ManagerUp; decrements inventory, tracks `ReturnedQuantity`). `OrderStatus` for PO: Draft, Submitted, Approved, PartiallyReceived, Received, Cancelled, Returned.

**Sales Orders** (`SalesOrderService`): `Create` (Draft, `SO-yyyyMMdd-####`) → `Confirm` (ManagerUp; validates available stock, **reserves** it FIFO by expiry via `QuantityReserved`) → `Deliver` (StaffUp; ships FIFO by expiry, decrements on-hand, releases reservation, writes `SalesIssue` txns at COGS) → `Return` (ManagerUp; re-absorbs stock at cost) / `Cancel` (ManagerUp; releases reservations). `OrderStatus` for SO: Draft, Confirmed, PartiallyDelivered, Delivered, Cancelled, Returned.

Each transition is a separate service method that inspects the current `Status` and throws `BadRequestException` if the transition is illegal — you can't skip steps.

### 6.5 Billing — Accounts Receivable (customer side)

**What it does**: raise invoices to customers and record their payments.

**Entities**: `Invoice` (+ `InvoiceItem`), `Payment` (+ `PaymentAllocation`).

**Invoice flow** (`InvoiceService`, `InvoicesController`):
- `POST /api/v1/Invoices` (StaffUp) — manual invoice, starts **Draft**
- `POST /api/v1/Invoices/from-sales-order` (StaffUp) — `CreateFromSalesOrderAsync` copies SO items, starts **Issued**, refuses to invoice the same SO twice (ConflictException), and backfills the SO with the invoice number
- `POST /api/v1/Invoices/{id}/issue` (StaffUp) — Draft → Issued
- `POST /api/v1/Invoices/{id}/cancel` (ManagerUp) — only if nothing has been paid
- `GET /api/v1/Invoices/outstanding/{customerId}` (ViewerUp) — unpaid/partial invoices, used by the payment UI

Invoice number = `INV-yyyyMMdd-####` (daily counter). Status: Draft → Issued → PartiallyPaid → Paid (also Overdue, Cancelled). Totals: `TotalAmount = SubTotal + Tax − Discount`; `BalanceDue = TotalAmount − AmountPaid`.

**Payment flow** (`PaymentService`, `PaymentsController`):
- `POST /api/v1/Payments` (StaffUp) — record a customer payment and split it across invoices. Validates: amount > 0, allocations sum ≤ amount, no invoice allocated twice, each invoice belongs to the same customer, isn't Draft/Cancelled, and the allocation ≤ that invoice's balance. For each allocation it calls `invoice.ApplyPayment(amount)` (advancing status) and writes a `PaymentAllocation` row. Payment number = `PAY-yyyyMMdd-####`.

### 6.6 Billing — Accounts Payable (supplier side)

**What it does**: record bills from suppliers and the payments you make against them.

**Entities**: `SupplierBill` (+ `SupplierBillItem`), `SupplierPayment` (+ `SupplierPaymentAllocation`).

**Bill flow** (`SupplierBillService`, `SupplierBillsController`):
- `POST /api/v1/SupplierBills` (StaffUp) — manual bill, starts **Draft**
- `POST /api/v1/SupplierBills/from-purchase-order` (StaffUp) — `CreateFromPurchaseOrderAsync` copies PO items, starts **Open**, refuses to bill the same PO twice (ConflictException)
- `POST /api/v1/SupplierBills/{id}/approve` (StaffUp) — Draft → Open
- `POST /api/v1/SupplierBills/{id}/cancel` (ManagerUp) — only if unpaid
- `GET /api/v1/SupplierBills/outstanding/{supplierId}` (ViewerUp) — open/partial bills for the payment UI

Bill number = `BILL-yyyyMMdd-####`. Status: Draft → Open → PartiallyPaid → Paid (also Overdue, Cancelled). Default terms: due in 30 days.

**Supplier payment flow** (`SupplierPaymentService`, `SupplierPaymentsController`):
- `POST /api/v1/SupplierPayments` (StaffUp) — record a payment to a supplier and split it across bills (same validation shape as customer payments). Payment number = `SPAY-yyyyMMdd-####`. Allocations are optional (you can record unallocated cash).

> **Note on where "generate bill / invoice" lives**: the PO and SO services do *not* generate billing documents. Generation is owned by the billing services — `SupplierBillService.CreateFromPurchaseOrderAsync` and `InvoiceService.CreateFromSalesOrderAsync` — exposed via the `/from-purchase-order` and `/from-sales-order` endpoints.

### 6.7 Reports & PDF

**What it does**: generates reports as JSON or PDF (stock summary, low stock, expiry, inventory valuation).

**How PDF works**: `ReportsController` → `IReportService` builds the data, `IPdfReportService` (QuestPDF) renders it declaratively to a byte array, returned as `application/pdf`.

### 6.8 Dashboard

**What it does**: one call (`GET /api/v1/Dashboard`) returns a `DashboardDto` with KPIs and lists; the Angular dashboard renders cards plus three **ngx-charts** visualisations.

- KPI cards: total sales, total purchases, inventory value, total orders
- Secondary stats: total products, warehouses, low-stock count, expiring-soon count
- Charts: **Top Products by Value** (vertical bar), **Financial Snapshot** (doughnut: sales / purchases / inventory value), **Stock Alerts** (horizontal bar: current vs reorder level)
- Lists: recent transactions, top products, low-stock products, recent sales orders

`DashboardService` computes low stock as `0 < QuantityOnHand ≤ ReorderLevel`, expiring as `ExpiryDate ≤ 30 days away`, and excludes Draft/Cancelled orders from money totals.

### 6.9 Hangfire background jobs

**Two recurring jobs** (both methods on `InventoryAlertJob`, registered in `Program.cs`):
- `check-low-stock` (`Cron.Hourly`) — finds products at/below reorder level, creates `LowStock` notifications
- `check-expiry-alerts` (`Cron.Daily`) — finds inventory expiring within 30 days, creates `ExpiryAlert` notifications

**Why jobs (not on user action)**: a manager logging in at 9 AM expects today's alerts to already exist. Jobs run on a schedule independent of users. Hangfire gives persistence, retries, distributed locks (the job runs once across N instances), and the `/hangfire` dashboard.

### 6.10 AI Chat

**Flow**: user types → `POST /api/v1/Chat` → `AiChatService` builds a system prompt with a live inventory snapshot (KPIs, low stock, top products, recent transactions/orders) → calls Gemini's **streaming** endpoint → forwards SSE chunks to the browser → text renders token-by-token. Retry: up to 3 attempts with backoff (2s/4s/6s) on 429/503.

---

## 7. Common interview / demo questions and how to answer them

> A fuller, categorized set lives in [Q_AND_A.md](Q_AND_A.md). The essentials:

#### Q: "Walk me through what this project does."
**30s**: A multi-tenant SaaS inventory management system. Many companies use it at once, each seeing only their own data. It covers products, warehouses, stock movements (with batch/expiry and weighted-average costing), purchase and sales orders, customer invoicing and payments, supplier bills and payments, reports, and a dashboard. Two AI features: extract product info from a photo, and a chat assistant over your live inventory.

**Deeper**: .NET 10 backend in Clean Architecture (Domain / Application / Infrastructure / API), thin controllers delegating to 18 services. Angular 19 frontend. SQL Server with EF Core. Hangfire for background jobs. Google Gemini over REST for AI.

#### Q: "Why Controller → Service instead of CQRS / MediatR?"
**30s**: I started with CQRS via MediatR — every endpoint had a separate Command + Handler + Validator. For a CRUD-heavy app with no separate read model, no event sourcing, and no message bus, that's a lot of files for little value. I migrated to one service per module: same Clean Architecture rings, half the files, flatter stack traces, validation/logging now framework-native.

#### Q: "How do you make sure tenants can't see each other's data?"
**30s**: Three layers. Every tenant-scoped row inherits `TenantEntity` (a `TenantId` column). EF Core global query filters add `WHERE TenantId = X && !IsDeleted` to every read, where X comes from the JWT. And `SaveChangesAsync` auto-stamps `TenantId` on insert so you can't forget. A `null` tenant (seeding/registration/SuperAdmin) bypasses the tenant clause but still respects soft-delete.

#### Q: "How does the money side work — invoices and bills?"
**30s**: Two mirror modules. Accounts receivable: invoices to customers (manual or generated from a sales order) and payments that allocate across invoices. Accounts payable: supplier bills (manual or generated from a purchase order) and supplier payments that allocate across bills. Each document tracks `AmountPaid` / `BalanceDue` and moves through Draft → Issued/Open → PartiallyPaid → Paid as allocations land. Generating a billing doc from an order is idempotent — you can't invoice the same SO or bill the same PO twice.

#### Q: "What happens to stock when you receive a PO or deliver an SO?"
**30s**: Receiving a PO calls `ApplyInbound` on the balance (blending the PO unit price into a moving weighted-average cost) and writes a `PurchaseReceive` transaction. Delivering an SO consumes balances FIFO by expiry, decrements on-hand, releases the reservation made at Confirm, and writes a `SalesIssue` transaction at COGS — actual cost, never the selling price.

#### Q: "How does the AI scan work / what if it returns garbage?"
**30s**: The image goes to Gemini vision with a strict-JSON prompt and `responseMimeType: application/json`. I strip stray markdown fences and deserialize into a typed DTO; if it still fails to parse, the endpoint returns a clean error and the user just types the product. The endpoint never saves — it pre-fills a form the user reviews.

#### Q: "Why Hangfire instead of `Task.Run`?"
**30s**: `Task.Run` dies with the process. Hangfire persists jobs to SQL, retries on failure, uses distributed locks so a job runs once across N servers, and gives me a dashboard — persistence, retry, distribution, observability for free.

#### Q: "How are passwords stored?"
**30s**: PBKDF2-SHA512 with a per-user salt. Verifying is intentionally slow (~100 ms), so brute-force is impractical; a DB leak doesn't reveal raw passwords.

---

## 8. Tradeoffs and decisions you should be able to defend

### Multi-tenancy via shared DB + global query filters
**Gained**: cheap, simple, easy to onboard new tenants. **Lost**: data is physically mingled; a bug in the filter could leak data. Mitigation: the filter is combined (tenant + soft-delete) and applied by reflection to every tenant entity, plus the write-side auto-stamp, plus integration tests on tenant boundaries.

### Controller → Service (replaced CQRS-via-MediatR)
**Gained**: half the files per module, flat stack traces, simpler onboarding, faster builds, no MediatR / FluentValidation / AutoMapper. **Lost**: MediatR's "free" cross-cutting validation/logging — now model-binding + inline guards + Serilog. Correct here because there's no separate read/write model, no event sourcing, no message bus.

### Entity-level behaviour for billing/inventory
**Gained**: invariants (status transitions, weighted-average cost, balance-due) live on the entity (`Invoice.ApplyPayment`, `InventoryBalance.ApplyInbound`), so services stay thin and the rules are reused. **Lost**: a little "anaemic vs rich model" purity debate — fine for this size.

### Hangfire in-process
**Gained**: simple deploy — API and worker are the same binary. **Lost**: jobs and HTTP traffic share threads. For high job volume you'd run Hangfire on dedicated workers.

### Free-tier Gemini for AI
**Gained**: zero infra, zero cost during dev. **Lost**: limited free vision calls/day; model availability shifts over time.

### JWT with 60 min expiry
**Gained**: simple, stateless, no DB lookup per request. **Lost**: a stolen JWT works up to 60 min — mitigated by short expiry + refresh rotation.

### Soft delete + live audit log
**Gained**: undelete, full change history (who changed what, old vs new). **Lost**: every query carries `!IsDeleted`; the audit write is a second `SaveChanges` per request that mutates data (small write amplification).

---

## 9. Demo script — 5 minute version

### Minute 1 — Set the stage
> "InventorySaaS is a multi-tenant inventory + light-ERP SaaS, .NET 10 and Angular 19. Multi-tenant means many companies use it at once, each seeing only their own data. I'll log into the demo tenant."

[Log in as `admin@demo-company.com` / `Demo@123456`]

### Minute 2 — The dashboard
> "Total sales, purchases, inventory value, orders up top — then charts: top products by value, a financial snapshot doughnut, and stock alerts. It's one call to `/api/v1/Dashboard`."

### Minute 3 — The AI scan (headline feature)
> "Adding products one by one is slow, so I built AI scan." [Products → Add → Scan from Photo → pick an image] "The form filled itself in — the image went to Gemini vision with a strict-JSON prompt, parsed into a typed DTO, patched into the form. I review, click Save."

### Minute 4 — Orders → money
> "I'll confirm and deliver a sales order — that reserves then ships stock FIFO by expiry and records COGS. Then 'generate invoice from this order', and record a customer payment that allocates against it. The invoice moves to Paid. The supplier side mirrors this: receive a PO, generate a bill, pay it."

### Minute 5 — Architecture wrap-up
> "Clean Architecture in four projects; thin controllers delegating to services; failures throw typed domain exceptions caught by one middleware. Multi-tenancy via EF Core global query filters reading the tenant from the JWT, plus a live audit log. Hangfire runs hourly/daily stock + expiry checks. JWT with refresh rotation, PBKDF2 passwords. Reports export to PDF via QuestPDF. Chat and scan call Gemini's REST API directly. Dockerised with a GitHub Action deploying to Azure on push to main."

---

## 10. Things you don't know yet, and that's OK

Reasonable to say "I haven't gone deep on that yet":

- **Production migration management** — what to do when a migration takes minutes on a large table (batched backfills, zero-downtime techniques).
- **High-availability deployment** — multiple API nodes, load balancer, primary/secondary SQL.
- **Performance tuning** — query plans, index design, caching. Redis is wired up but real workloads aren't profiled.
- **Subscription-limit enforcement** — plans/limits are seeded but not yet enforced at the API.
- **Localization (i18n)** — English only today.
- **Comprehensive testing** — unit + integration projects exist but coverage is light; the highest-value additions are tenant-isolation tests and the order/billing state-machine flows.

Confidence comes from honesty. "I haven't built X but I know what I'd do" beats pretending.

---

## Quick-reference cheat sheet

| Term | One-liner |
|---|---|
| SaaS | Software you log into, not install |
| Multi-tenant | One app, many isolated customers |
| Clean Architecture | Business rules don't depend on frameworks |
| Controller → Service | Thin controller delegates to a focused service class |
| Domain exception | Typed `Exception` the global middleware maps to an HTTP code |
| `IApplicationDbContext` | Application-layer interface exposing EF DbSets without referencing EF Core |
| DTO | Plain object that carries data between layers |
| Entity | Class representing a thing in your business (some carry behaviour) |
| EF Core | C# ORM that turns LINQ into SQL |
| Migration | C# file describing a DB schema change |
| Global query filter | Combined tenant + soft-delete LINQ added to every read |
| Audit log | Auto-written change history (who/what/old→new) on every save |
| JWT | Signed token that proves your identity |
| Refresh token | Long-lived, rotated token used to get new JWTs |
| RBAC / policy | Role-based access, grouped under named policies |
| InventoryBalance | On-hand + reserved + weighted-average cost per product/warehouse/batch |
| InventoryTransaction | The movement ledger (StockIn, Transfer, SalesIssue, …) |
| Weighted-average cost | New stock blends into the existing unit cost |
| FIFO by expiry | Reservations/shipments consume earliest-expiry stock first |
| AR (invoices/payments) | Money customers owe you, with payment allocation |
| AP (bills/payments) | Money you owe suppliers, with payment allocation |
| Payment allocation | One payment split across several invoices/bills |
| Hangfire | Library for background jobs (hourly/daily checks) |
| SSE | Streaming text from server to browser (AI chat) |
| Gemini | Google's LLM, used for chat + vision scan |
| ngx-charts | Dashboard charting library |
| QuestPDF | C# library for generating PDF reports |
| Serilog | Structured logging library |
| Correlation ID | Unique ID tying log entries to one request |

---

*Read the file. Re-read sections that feel hazy. Open the actual code while reading — every term here maps to a specific file. The fastest way to internalise it is to open the file and re-trace what the code is doing while the explanation is fresh.*
