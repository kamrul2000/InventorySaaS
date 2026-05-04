# InventorySaaS — Project Understanding Guide

> A study guide written for the developer of this project. It explains every term, how each piece is implemented, what it does, and the most likely questions someone will ask you about it — with prepared answers. Read it once end-to-end, then come back to it before any demo or interview.

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

**One sentence**: A web app that helps a business track its products, warehouses, and stock movements, and is built so many different companies can use the same app at the same time without seeing each other's data.

**Real-world analogy**: Think of Shopify. Many merchants use Shopify, but each merchant only sees their own products and orders. They share the same code and the same servers, but the data is isolated. That's exactly what we built.

**Who would use it**:
- A pharmacy chain tracking which medicines are about to expire
- A retail manager who wants to see today's sales vs purchases
- A warehouse worker recording incoming stock
- An owner pulling a "what is my inventory worth?" report

**What's the AI for?**:
- **Scan a product photo** → the AI reads the label, fills in name/brand/barcode/price for you, so you don't have to type. Useful for adding hundreds of products fast.
- **Chat with your inventory** → ask "what's running low?" or "how were last week's sales?" and the AI answers using your real data.

---

## 2. The big picture diagram

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│   Browser       │◄───────►│   API           │◄───────►│   SQL Server    │
│   (Angular)     │  HTTPS  │   (.NET 10)     │  EF Core│                 │
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
- **A frontend** the user sees (Angular)
- **A backend** that does the work and talks to the database (.NET API)
- **A database** that stores everything (SQL Server)
- **An external AI** that the backend calls when needed (Google Gemini)
- **A background worker** that runs scheduled tasks (Hangfire, lives inside the same .NET process)

---

## 3. Architecture: Clean Architecture explained

### What is "Clean Architecture"?

Your backend is split into **four projects**:

```
InventorySaaS.Domain          ← the rules of your business
       ▲
InventorySaaS.Application     ← what the app DOES (commands like "create product")
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
- Add a new way to create a product (e.g., bulk CSV import) → you write a new command in **Application**, the API just exposes it.

### Layer-by-layer

#### `InventorySaaS.Domain` — the heart
- Contains pure C# classes (entities) like `ProductInfo`, `TenantInfo`, `InventoryBalance`
- **Knows nothing** about EF Core, HTTP, or any framework
- Defines what it MEANS to be a Product, not how it's stored

#### `InventorySaaS.Application` — the use cases
- Contains one **service per business module** — `IProductService`, `IInventoryService`, `IAuthService`, `ISalesOrderService`, etc.
- Each service exposes the operations that module supports (`CreateAsync`, `GetByIdAsync`, `ApproveAsync`, etc.)
- Services depend only on `IApplicationDbContext` (interface in this layer) and pure C# — no EF Core, no HTTP, no framework
- This layer says *what* should happen, not *how*

#### `InventorySaaS.Infrastructure` — the plumbing
- EF Core DbContext (talks to SQL Server)
- Hangfire setup (background jobs)
- Email service (SMTP)
- File storage
- AI integrations (Gemini)
- This is where the *how* lives

#### `InventorySaaS.API` — the front door
- ASP.NET Core controllers
- Middleware (auth, tenant resolution, exception handling)
- The bit that actually accepts HTTP requests

### How does a request flow through the layers?

A user clicks "Create Product" in the browser:

1. **Frontend** sends `POST /api/v1/Products` with JSON
2. **API layer** receives it (`ProductsController`)
3. Controller calls `_productService.CreateAsync(request, cancellationToken)`
4. **Application layer**: `ProductService.CreateAsync` runs the use case — looks up the category, generates a unique SKU, builds the entity
5. Service calls `_context.SaveChangesAsync()` on `IApplicationDbContext` — **Infrastructure layer**'s EF Core writes to SQL Server
6. Service returns a `ProductDto`; the controller wraps it in `Ok(...)`
7. If anything goes wrong, the service throws a typed exception (`NotFoundException` / `BadRequestException` / `ConflictException`); the global `ExceptionHandlingMiddleware` turns it into the right HTTP code + JSON envelope

---

## 4. Glossary of every term used in this project

### Architecture & Patterns

#### **SaaS (Software as a Service)**
You don't install the app — you log in to a website. Gmail, Shopify, Slack are all SaaS. Yours too.

#### **Multi-tenant**
One running app serves many customers ("tenants"). Each tenant only sees their own data. The alternative ("single-tenant") would be running a separate copy of the app for each customer — expensive and slow to update.

#### **Tenant isolation**
The mechanism that makes sure tenant A can never see tenant B's data. In your project this is done two ways:
1. Every tenant-scoped row has a `TenantId` column.
2. EF Core "global query filters" automatically add `WHERE TenantId = X` to every database query, where X comes from the user's JWT.

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
Defined in [Domain/Exceptions/DomainException.cs](src/InventorySaaS.Domain/Exceptions/DomainException.cs). Five types:
- `NotFoundException` → 404
- `ConflictException` → 409 (e.g. duplicate code, duplicate email)
- `BadRequestException` → 400 (e.g. "quantity must be > 0")
- `ForbiddenAccessException` → 403
- `DomainException` (base) → 500 if uncaught

Services throw them; the global [`ExceptionHandlingMiddleware`](src/InventorySaaS.API/Middleware/ExceptionHandlingMiddleware.cs) catches them and produces a uniform `ProblemResponse` JSON. This is *the* mechanism that keeps controllers thin.

#### **DTO (Data Transfer Object)**
A simple class that carries data between layers, e.g. between the API and the frontend, or between the service and the controller. They're "anaemic" — no behaviour, just properties. Three flavours per module:
- `XxxDto` — what the API returns
- `CreateXxxRequest` — body for POST endpoints
- `UpdateXxxRequest` — body for PUT endpoints

#### **Entity**
A class that represents a "thing" in your business — Product, Customer, Warehouse. Lives in the Domain layer. Maps to a database table via EF Core.

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
JWTs expire (yours: 60 minutes). Instead of making the user log in every hour, the frontend silently calls `/auth/refresh-token` with a long-lived **refresh token** to get a new JWT. Each refresh **rotates** the token (old one becomes invalid) — this catches stolen tokens.

#### **Claim**
A piece of information stored inside a JWT: `email`, `tenant_id`, `role`. The backend reads these to know who's making the request.

#### **RBAC (Role-Based Access Control)**
Each user has one or more **roles** (SuperAdmin, TenantAdmin, Manager, Staff, Viewer). Each role can do certain things. The simpler model than permission-by-permission.

#### **Authorization Policy**
ASP.NET Core's way of grouping role checks under a name. Yours:
- `SuperAdminOnly`, `TenantAdminOnly`, `ManagerUp`, `StaffUp`, `ViewerUp`

You apply them with `[Authorize(Policy = "StaffUp")]` on a controller action.

#### **Middleware**
Code that runs before/after every HTTP request. Yours, in order:
1. CorrelationIdMiddleware — assigns a unique ID to each request
2. ExceptionHandlingMiddleware — catches errors and returns a clean JSON response
3. JWT authentication
4. TenantResolutionMiddleware — reads tenant ID from the JWT
5. Authorization (policy checks)

### Database

#### **EF Core (Entity Framework Core)**
Microsoft's ORM. You write C# code like `db.Products.Where(p => p.Name == "X")`, and EF Core translates it to SQL. You don't write SQL by hand.

#### **DbContext**
The object that represents your database connection. `ApplicationDbContext` exposes `DbSet<ProductInfo>`, `DbSet<TenantInfo>`, etc.

#### **Migration**
A C# file describing a change to the database schema (adding a column, etc.). When you run `dotnet ef database update`, EF Core applies pending migrations.

#### **Global Query Filter**
A LINQ expression EF Core silently adds to every query against an entity. Example: `modelBuilder.Entity<ProductInfo>().HasQueryFilter(p => p.TenantId == _currentTenantId)`. This is how tenant isolation is enforced — you literally cannot accidentally write code that returns another tenant's data.

#### **Soft delete**
Instead of `DELETE FROM Products WHERE Id = X`, you set `IsActive = false` (or `IsDeleted = true`). The row stays in the database but is hidden. Lets you recover deletes and audit history.

#### **Optimistic concurrency**
Two users edit the same record at the same time. Without protection, the second save silently overwrites the first. Optimistic concurrency uses a `RowVersion` column (auto-incremented on every save) — when the second user tries to save, EF Core sees their RowVersion is stale and throws an exception so you can show a "this record was modified" message.

#### **Seeding**
Creating starter data when the database is first created (or empty). You seed: subscription plans, roles, permissions, a SuperAdmin user, a Demo Company tenant. See `DatabaseSeeder.cs`.

### Background processing

#### **Background job**
Code that runs *outside* the request-response cycle. Three flavours:
- **Fire-and-forget** — "send this email, don't make me wait"
- **Delayed** — "remind this user in 7 days"
- **Recurring (cron)** — "every hour, scan for low stock"

Yours uses **recurring** for two jobs (low-stock check, expiry check). Both run via Hangfire.

#### **Hangfire**
A C# library that handles the queue, the retries, the persistence to SQL Server, and the dashboard for background jobs. Your project uses Hangfire for two recurring jobs and provides a dashboard at `/hangfire` (SuperAdmin only).

#### **Cron expression**
The string that says when a recurring job should run. `Cron.Hourly` (every hour at :00) and `Cron.Daily` (every day at midnight) are examples. Could also be `"*/5 * * * *"` (every 5 minutes).

### API & HTTP

#### **REST API**
A style of designing HTTP APIs where:
- URLs represent resources: `/api/v1/Products/123`
- HTTP verbs do operations: GET (read), POST (create), PUT (update), DELETE (delete)
- Responses are JSON

Yours is REST.

#### **Swagger / OpenAPI**
Auto-generated, interactive API documentation. Visit `/swagger` to see every endpoint, try them out, send test requests. Great for development.

#### **CORS (Cross-Origin Resource Sharing)**
Browser security: a webpage at `localhost:4200` can't call an API at `localhost:5179` unless the API explicitly allows it. Your `Program.cs` configures CORS to allow your Angular dev server.

#### **Rate limiting**
Stopping a single user/IP from making too many requests. Your project uses `AspNetCoreRateLimit` to cap login attempts at 10/minute (prevents brute-force) and general traffic at 60/minute.

#### **Correlation ID**
A unique GUID assigned to each HTTP request. It's logged in every log line for that request and returned in the response header. When something breaks, one ID lets you find every log entry related to that request.

#### **Server-Sent Events (SSE)**
A way for the server to **stream** data to the browser over a single open HTTP connection. The AI chat endpoint uses SSE to stream tokens as the model generates them — you see text appear word-by-word instead of waiting for the whole response.

### Frontend (Angular)

#### **Standalone components**
Modern Angular components that don't need to be declared in a `NgModule`. Each component lists its own imports. Your project uses standalone components everywhere.

#### **Reactive Forms**
A way of building forms in Angular where the form is a TypeScript object (`FormGroup`) instead of HTML-only. Validation, value tracking, dirty/touched state — all easy. Your `product-form.component.ts` uses Reactive Forms.

#### **Pipe**
A small transformer in templates: `{{ price | currency:'BDT':'৳':'1.2-2' }}`. The pipe takes a value, transforms it, returns a new value for display.

#### **Observable**
A stream of values over time, from RxJS. HTTP calls return Observables. You `.subscribe()` to them to get the result.

#### **HTTP Interceptor**
Code that runs before every outgoing HTTP request. Yours:
- `auth.interceptor.ts` — adds `Authorization: Bearer <token>` to every request, handles 401 by silently refreshing the token
- `error.interceptor.ts` — handles errors globally
- `tenant.interceptor.ts` — adds tenant context

#### **Signal**
Angular's new (v16+) reactive primitive. A `signal<T>()` holds a value; reading it in a template tracks it; updating it re-renders the parts that read it. Your `NotificationService` uses signals to manage the toast list.

#### **Router outlet**
The placeholder where Angular renders the current route's component. `<router-outlet></router-outlet>` in `app.html`.

### AI

#### **Gemini**
Google's family of LLMs (Large Language Models). You use:
- `gemini-2.5-flash-lite` — small, fast, free-tier eligible. Used for both chat and product extraction in your project.

#### **System prompt**
Instructions you give the model that aren't visible to the user. Tell the model what it is, how to behave, what data it has access to. Your AI chat builds a system prompt with the tenant's inventory snapshot.

#### **Token**
The unit of text the model processes. Roughly: 1 token ≈ 0.75 of an English word. Both your input and the model's output are billed by token count (on paid tier).

#### **Vision model**
An LLM that accepts images alongside text. The image is sent base64-encoded inside the request. Gemini 2.5 Flash Lite supports vision — that's what powers your "Scan from Photo" feature.

#### **JSON mode**
Telling the model "your response MUST be valid JSON". Gemini accepts `responseMimeType: "application/json"` in the request, which forces JSON output. Your product extraction uses this so you can deserialize directly into a typed DTO.

#### **Streaming (vs single response)**
With streaming, the model sends chunks of the response as it generates them. With non-streaming, you wait for the full response. Your chat uses streaming (Server-Sent Events) so the user sees text appear in real time. Your product extraction uses non-streaming because it needs the full JSON before it can be parsed.

### Security & operations

#### **Hashing (PBKDF2-SHA512)**
You don't store passwords. You store a **hash** of the password. PBKDF2-SHA512 is a slow, salted hash — even if the database leaks, attackers can't reverse the hash to recover passwords.

#### **Salt**
A random string mixed into the password before hashing, so the same password never produces the same hash twice. Stops attackers from using precomputed "rainbow tables".

#### **Health check**
An HTTP endpoint (`/health`) that returns 200 if the app is healthy, 500 if something's broken. Yours checks the database is reachable. Used by load balancers to know if a server should receive traffic.

#### **Structured logging**
Logging key-value pairs instead of plain text. `logger.LogInformation("User {Email} logged in", email)` produces a log entry like `{ "message": "User logged in", "email": "x@y.com" }`. Searchable, filterable. You use Serilog.

#### **Connection string**
The string that tells your app where the database is and how to connect: `Server=localhost;Database=InventorySaaS;...`. Lives in `appsettings.json`.

#### **User secrets**
A way to keep sensitive config (API keys, passwords) out of `appsettings.json` (which is in git). They live in your machine's user profile and are loaded automatically in development. Your Gemini API key is stored this way.

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
  ├─ 8. JWT auth: signature verified, claims (user_id, tenant_id, role) loaded into HttpContext.User
  │
  ├─ 9. TenantResolutionMiddleware reads tenant_id claim, makes it available via ITenantAccessor
  │
  ├─ 10. Authorization policy [Authorize(Policy = "StaffUp")] — pass
  │
  ├─ 11. ProductsController.Create() runs
  │
  ├─ 12. Controller calls _productService.CreateAsync(request, cancellationToken)
  │
  ├─ 13. ProductService.CreateAsync runs:
  │      - reads tenant_id from ICurrentUserService
  │      - looks up category, brand, unit (or creates them)
  │      - generates unique SKU
  │      - creates new ProductInfo entity
  │      - calls _context.SaveChangesAsync()
  │      (any business-rule failure → throws BadRequestException / NotFoundException /
  │       ConflictException; ExceptionHandlingMiddleware turns it into the right status code)
  │
  ├─ 14. SaveChangesAsync override auto-stamps CreatedAt / CreatedBy / TenantId
  │
  ├─ 15. EF Core translates to SQL: INSERT INTO Products (...) VALUES (...)
  │
  ├─ 16. SQL Server executes the insert, returns the new row
  │
  ├─ 17. Service builds a ProductDto and returns it directly (no wrapper)
  │
  ├─ 18. Serilog's built-in request logger emits "Request finished ... 201 ... 35ms"
  │
  ├─ 19. Controller returns CreatedAtAction(...) — HTTP 201 with the new product as JSON
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
  └─ 25. User sees a green toast in the bottom-right
```

That's the full path. ~24 steps for one button click. Most are invisible — the framework handles them. Your code lives in steps 1, 11, 12, 13, 17, 21, 22.

---

## 6. Module walkthroughs

### 6.1 Authentication module

**What it does**: lets users register, log in, log out, refresh their token, reset their password.

**Files**:
- `Application/Services/IAuthService.cs` + `AuthService.cs` — the 6 auth operations
- `Application/Features/Auth/DTOs/AuthDtos.cs` — request and response shapes
- `Infrastructure/Services/Auth/TokenService.cs` — generates and validates JWTs
- `Infrastructure/Services/Auth/PasswordHasherService.cs` — hashes/verifies passwords
- `API/Controllers/AuthController.cs` — HTTP endpoints (thin)

**How login works (step by step)**:
1. User POSTs `{ email, password }` to `/api/v1/auth/login`
2. `AuthController.Login` calls `_authService.LoginAsync(request, ct)`
3. `AuthService.LoginAsync` looks up the user by email (`NormalizedEmail` index)
4. `PasswordHasherService.Verify(input, storedHash)` — slow PBKDF2 hash compare
5. If invalid → throws `UnauthorizedAccessException` → middleware → 401
6. If valid → `TokenService.GenerateTokensAsync(user, roles)` builds a JWT (claims: userId, tenantId, email, role) **and** a refresh-token row in the database
7. `LastLoginAt` is updated; both tokens are returned to the client
8. Frontend stores both; the auth interceptor uses the JWT on every request
9. When the JWT expires (60 min), the frontend silently calls `/auth/refresh-token` to get a new one (refresh-token rotation: old token revoked, new one issued)

**Things you might be asked**:
- *Why JWT and not session cookies?* → Stateless. No DB lookup per request. Easier to scale to multiple servers.
- *What if a JWT is stolen?* → It's valid until expiry (60 min). That's why we keep them short-lived. The refresh token is rotated on every use, so a stolen refresh token reveals itself the moment the legit user uses theirs.
- *How are passwords stored?* → PBKDF2-SHA512 hash with a per-user salt. Even if the DB leaks, raw passwords aren't recoverable.

### 6.2 Product module

**What it does**: lets users create, list, view, edit, delete products. Includes the AI-powered "Scan from Photo".

**Files**:
- `Application/Services/IProductService.cs` + `ProductService.cs` — `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- `Application/Features/Products/DTOs/ProductDtos.cs` — `ProductDto`, `CreateProductRequest`, `UpdateProductRequest`
- `Application/Features/Products/DTOs/ProductExtractionDtos.cs` — vision result shape
- `API/Controllers/ProductsController.cs` — thin controller; the `extract-from-image` action calls `IProductExtractionService` directly (no service intermediation since it doesn't persist)
- `Infrastructure/Services/AI/GeminiProductExtractionService.cs`

**SKU auto-generation**:
1. Take the first 3 letters of the category name, uppercase: "Food & Beverage" → "FOO"
2. Find the highest existing SKU number with that prefix
3. New SKU = `{prefix}-{maxNumber + 1:D5}` → "FOO-00007"

**AI scan flow**:
1. User clicks "Scan from Photo", picks an image
2. Frontend POSTs the image as multipart to `/api/v1/Products/extract-from-image`
3. Controller validates file (JPEG/PNG, ≤ 5 MB)
4. `GeminiProductExtractionService` builds a request to Google Gemini with the image (base64) + a tuned prompt
5. Gemini returns JSON: `{ name, brand, barcode, suggestedCategory, suggestedSellingPrice, ... }`
6. The service parses and returns it as a `ProductExtractionResult` DTO
7. Frontend pre-fills the form with these values
8. User reviews/edits, clicks Save → goes through the normal `POST /api/v1/Products` → `IProductService.CreateAsync` path

The extract endpoint **does not save anything**. It's just a "draft generator". This is deliberate — the user always has the chance to review.

### 6.3 Inventory module

**What it does**: tracks how much of each product is in each warehouse, including by batch.

**Key concepts**:
- **InventoryBalance** — one row per (product, warehouse, batch). Shows quantity on hand.
- **InventoryTransaction** — one row per movement. The full ledger.

**Stock-in flow** (when goods arrive):
1. User submits a "Stock In" form
2. `POST /api/v1/Inventory/stock-in` → `InventoryService.StockInAsync(request, ct)`
3. Service creates a new `InventoryTransaction` row (audit)
4. Service updates or creates the matching `InventoryBalance` row
5. Both saves happen in the same `SaveChangesAsync` call — either both succeed or both roll back (EF Core transaction)

**Why the ledger pattern?**:
You can always reconstruct the current balance by summing the transactions. If something goes wrong with the cached balance, the ledger is the source of truth.

### 6.4 Purchase Orders / Sales Orders

**What they do**: model the lifecycle of buying from suppliers (PO) and selling to customers (SO).

**State machine**:
- PO: Draft → Submitted → Approved → PartiallyReceived → Received
- SO: Draft → Confirmed → PartiallyDelivered → Delivered → (Returned)

Each state transition is a separate service method (`PurchaseOrderService.ApproveAsync`, `PurchaseOrderService.ReceiveAsync`, `SalesOrderService.ConfirmAsync`, `SalesOrderService.DeliverAsync`, `SalesOrderService.ReturnAsync`) — you can't skip steps. The service inspects the current `Status` and throws `BadRequestException` if the transition is illegal (e.g. trying to deliver a Draft order).

### 6.5 Reports & PDF

**What it does**: generates 4 reports as JSON or PDF: stock summary, low stock, expiry, inventory valuation.

**How PDF generation works**:
1. User clicks "Export PDF"
2. Frontend calls `/api/v1/Reports/stock-summary/pdf`
3. Controller asks `PdfReportService` to render the report
4. `PdfReportService` uses **QuestPDF** — a C# library that builds PDFs declaratively (like writing HTML, but for PDF)
5. Result is a byte array, returned as `application/pdf`
6. Browser downloads it

### 6.6 Hangfire background jobs

**What it does**: runs scheduled tasks even when nobody is using the app.

**Two jobs**:
- `check-low-stock` (hourly) — finds products at/below reorder level, creates notifications
- `check-expiry-alerts` (daily) — finds inventory expiring within 30 days, creates notifications

**Why these run as jobs (not on user action)**:
A warehouse manager logs in at 9 AM expecting to see today's alerts. If the alerts only ran when someone visited the dashboard, they wouldn't be there. Jobs run on a schedule independent of users.

**The dashboard at `/hangfire`**:
SuperAdmin only. Shows job history, success/fail counts, lets you trigger a job manually.

### 6.7 AI Chat

**What it does**: a chat panel where the user can ask questions like "what's running low?" and the AI answers using their real inventory data.

**Flow**:
1. User types a message
2. Frontend POSTs to `/api/v1/Chat`
3. `AiChatService` builds a **system prompt** containing a snapshot of the tenant's inventory:
   - Total products, warehouses, suppliers, customers
   - Total inventory value, total sales, total purchases
   - Low stock items
   - Top products
   - Recent transactions, sales orders, purchase orders
4. Service calls Gemini's **streaming** endpoint with this system prompt + user message
5. Gemini streams back chunks of text via Server-Sent Events
6. Service forwards the chunks to the browser as SSE
7. Frontend renders text token-by-token in real time

**Key implementation detail — retry logic**:
Gemini sometimes returns 429 (rate limited) or 503 (overloaded). The service retries up to 3 times with exponential backoff (2s, 4s, 6s) before giving up.

---

## 7. Common interview / demo questions and how to answer them

Below: questions grouped by topic, each with a **30-second answer** (what to say) and a **deeper answer** (if they ask for more).

### Project overview

#### Q: "Walk me through what this project does."
**30s**: It's a multi-tenant SaaS inventory management system. Many companies can use it at the same time, each seeing only their own data. It covers products, warehouses, stock movements, purchase and sales orders, and reports. It also has two AI features: one that extracts product info from a photo, and one that's a chat assistant for asking questions about your inventory.

**Deeper**: I built the backend in .NET 10 using Clean Architecture in four projects (Domain / Application / Infrastructure / API), with thin controllers delegating to one service per business module. The frontend is Angular 19. Data is in SQL Server with EF Core. Background tasks run on Hangfire. The AI is Google's Gemini, called over REST.

#### Q: "Why did you build this?"
**Honest answer**: It started as a learning project to combine modern .NET, Angular, AI, and proper architecture in one place. Inventory was the domain because it has rich relationships (warehouses, batches, expiries) that exercise the patterns properly.

### Architecture

#### Q: "What is Clean Architecture and why did you use it?"
**30s**: It's a way of organising code so business rules don't depend on frameworks. I split the backend into four projects: Domain (entities), Application (commands/queries), Infrastructure (DB, AI, email), API (controllers). The dependencies only point inward. Tomorrow if I want to swap SQL Server for Postgres, I only change Infrastructure — Domain and Application don't notice.

**Deeper**: The benefit shows up when something changes. For example, when I added the AI scan feature, I added one interface in Application (`IProductExtractionService`), one implementation in Infrastructure (`GeminiProductExtractionService`), and one controller action — without touching anything in Domain. Adding bulk-CSV import would be the same pattern.

#### Q: "Why Controller → Service instead of CQRS / MediatR?"
**30s**: I started with CQRS via MediatR — every endpoint had a separate `*Command` + `*Handler` + `*Validator` (3-4 files per endpoint). For an inventory app with ~80 endpoints, that's a lot of file churn for very little extra value: there's no read model that scales separately from the write model, no Event Sourcing, no message bus. So I migrated to one service per business module — `IProductService` with `CreateAsync` / `GetByIdAsync` / etc. Same Clean Architecture rings, same tests, half the files, simpler stack traces, and validation/logging are now framework-native (model-binding + Serilog request logger).

**Deeper**: The migration was incremental — one module at a time, build clean after each. Domain exceptions plus a single `ExceptionHandlingMiddleware` replaced the `Result<T>` wrapper. The controllers became genuinely thin (2 lines per action). The Infrastructure layer didn't change at all. If a module ever needs CQRS again — say, a separate read store backed by a denormalised view — adding it is a localised change to that one service.

### Multi-tenancy

#### Q: "How do you make sure tenants can't see each other's data?"
**30s**: Two layers. First, every tenant-scoped row has a `TenantId` column. Second, EF Core has global query filters that automatically add `WHERE TenantId = X` to every query. The TenantId comes from a claim in the user's JWT. So even if I forget to filter in code, the database query is filtered automatically.

**Deeper**: There's a SuperAdmin role that can bypass the filter for cross-tenant operations. The tenant ID is read from the JWT claim by `TenantResolutionMiddleware` and stored in `ITenantAccessor`, which is what the EF query filter consults.

#### Q: "What's the alternative to row-level isolation?"
- **Database-per-tenant** — each tenant has their own database. More isolation, more cost, harder migrations.
- **Schema-per-tenant** — each tenant has their own schema in one database. Middle ground.
- **Row-level (what you have)** — cheapest, simplest, requires discipline. Global query filters provide that discipline.

### Authentication

#### Q: "Why JWT instead of cookies/sessions?"
**30s**: Stateless. The token contains everything the server needs (user ID, tenant ID, role) signed with a secret. No database lookup per request. Easier to horizontally scale — any server can validate any token.

#### Q: "How do you handle token expiry?"
**30s**: Access tokens expire in 60 minutes. The frontend has an HTTP interceptor that catches 401 responses, calls `/auth/refresh-token` with the long-lived refresh token, gets a new access token, and replays the original request. Refresh tokens are rotated on each use to detect theft.

#### Q: "How are passwords stored?"
**30s**: PBKDF2-SHA512 with a per-user salt. Even if the database leaks, the original passwords can't be recovered. Verifying a password is intentionally slow (~100 ms) so brute-forcing is impractical.

### AI features

#### Q: "How does the AI scan work?"
**30s**: User uploads a product photo. The backend buffers the image, encodes it as base64, sends it to Google's Gemini vision API along with a prompt that instructs the model to return strict JSON with name, brand, barcode, etc. The response is parsed into a typed DTO and returned to the Angular form, which pre-fills the fields. The user reviews and saves through the normal product-create endpoint.

#### Q: "What if Gemini returns garbage?"
**30s**: Three layers of defence. First, the prompt explicitly says "return ONLY valid JSON with this exact shape". Second, the request uses `responseMimeType: application/json` which forces Gemini to validate its own output. Third, my code strips ```` ```json ```` markdown fences if Gemini wraps the JSON anyway, then calls `JsonSerializer.Deserialize<ProductExtractionResult>`. If parsing fails, the controller returns 502 with a clean message — the user just doesn't get an auto-fill.

#### Q: "Why doesn't the scan endpoint save the product?"
**30s**: Deliberate UX choice. The AI is right ~80% of the time, but the user knows their inventory better than any model — they should have a chance to review and correct. So the endpoint returns a draft, and the user submits it through the existing create-product flow. It's a two-step pipeline by design.

#### Q: "How does the chat work?"
**30s**: When the user sends a message, the backend builds a system prompt containing a real-time snapshot of their inventory — totals, low stock items, top products, recent transactions. That prompt plus the user's message goes to Gemini's streaming endpoint via SSE, and tokens are forwarded to the browser as they arrive. The model has retry-on-429 with exponential backoff because Gemini's free tier rate-limits aggressively.

### Background jobs

#### Q: "Why Hangfire instead of just `Task.Run`?"
**30s**: `Task.Run` dies with the process. Hangfire persists jobs to SQL Server, retries on failure, distributes work across multiple servers, and gives me a dashboard to inspect runs. I get retry, persistence, distribution, and observability for free instead of building all four myself.

#### Q: "What if the same job runs on two servers?"
**30s**: Hangfire uses distributed locks in SQL Server. Even if I scale the API to 5 instances, the hourly low-stock job runs exactly once per hour, total — not 5 times.

### Database

#### Q: "Why SQL Server?"
**30s**: It pairs well with .NET (best EF Core support), has solid Hangfire integration, and supports row-versioning for optimistic concurrency. Could swap to PostgreSQL by changing one line in Infrastructure if needed.

#### Q: "How do you handle concurrent edits?"
**30s**: Optimistic concurrency via a `RowVersion` column. EF Core auto-increments it on every update. When two users edit the same record, the second save throws `DbUpdateConcurrencyException` — I catch it and tell the user "this record was modified, refresh and try again".

### Frontend

#### Q: "Why Angular?"
**30s**: Strong tooling for large enterprise apps, built-in dependency injection, reactive forms, and HTTP interceptors. The component model (standalone components, signals) is mature. I considered React, but Angular's structure suits a multi-feature business app better.

#### Q: "How does the frontend stay in sync with API auth?"
**30s**: An HTTP interceptor adds the bearer token to every request. On 401, the interceptor silently calls `/auth/refresh-token`, swaps the token, and retries the original request — the user never sees a re-login screen unless the refresh token itself is invalid.

### Security

#### Q: "What protects against brute-force login?"
**30s**: Rate limiting via `AspNetCoreRateLimit`. Login is capped at 10 attempts per minute per IP. Plus, password verification uses PBKDF2 which is intentionally slow (~100 ms) — that alone makes brute-force impractical.

#### Q: "What about SQL injection?"
**30s**: EF Core parameterises every query. I never concatenate user input into SQL. Even my custom queries use LINQ which compiles to parameterised SQL.

#### Q: "Where do secrets live?"
**30s**: In development, .NET user-secrets — a per-machine encrypted store outside the project tree. In production, environment variables (Azure App Service / Docker / k8s convention `Section__Key`). Never in source control. The Gemini API key, JWT signing secret, and SMTP password all go through this path.

### Deployment

#### Q: "How would you deploy this?"
**30s**: Three options that scale up:
1. **Azure App Service** — easiest. There's a GitHub Action wired up that builds and deploys on push to main.
2. **Docker** — `docker-compose.yml` runs SQL Server, Redis, API, and frontend in containers. Good for self-hosted.
3. **Kubernetes** — for serious scale: API as a Deployment with a HorizontalPodAutoscaler, SQL Server as a StatefulSet (or managed DB), Redis as a StatefulSet, Hangfire dashboard exposed only internally.

---

## 8. Tradeoffs and decisions you should be able to defend

Every architectural choice has a downside. Here are the ones in your project, what you gained, and what you gave up.

### Multi-tenancy via shared DB + global query filters
**Gained**: cheap, simple, easy to onboard new tenants.
**Lost**: data is physically mingled. A bug in the global filter could leak data. (Mitigation: aggressive testing, integration tests covering tenant boundaries.)

### Controller → Service (replaced CQRS-via-MediatR)
**Gained**: half the file count per module, flat stack traces (Controller → Service → DbContext), simpler onboarding for new devs, faster builds, no MediatR / FluentValidation / AutoMapper dependencies.
**Lost**: MediatR's "free" cross-cutting concerns. Validation now lives at model-binding (data annotations) + inline service guards; logging is the framework's request logger plus correlation IDs. Trade was correct here because the project doesn't have separate read/write models, no event sourcing, and no message bus — i.e. CQRS wasn't earning its keep.

### Hangfire in-process
**Gained**: simple deploy. The API and the job worker are the same binary.
**Lost**: jobs and HTTP traffic compete for the same threads. For high job volume you'd run Hangfire on dedicated worker nodes.

### Free-tier Gemini for AI
**Gained**: zero infrastructure, zero cost during dev.
**Lost**: 20 free vision calls/day. Hits production scale only with billing enabled. Different model availability changes over time.

### JWT with 60 min expiry
**Gained**: simple, stateless, no DB lookup per request.
**Lost**: a stolen JWT works for up to 60 minutes. Could shorten to 5–15 min for very security-sensitive deployments, paying for more refresh-token round-trips.

### Soft delete
**Gained**: undelete, audit, no cascading data loss.
**Lost**: every query has to remember to filter `IsActive = true` (or `!IsDeleted`). Global query filters help here too.

### Angular Material + custom CSS (rather than Bootstrap)
**Gained**: cohesive design system, accessibility built-in.
**Lost**: bigger bundle, opinionated styling. The toast component is custom CSS to avoid pulling in Bootstrap just for toasts.

---

## 9. Demo script — 5 minute version

Use this when showing the project. Hit the highlights, don't try to show everything.

### Minute 1 — Set the stage
> "InventorySaaS is a multi-tenant inventory management SaaS, built with .NET 10 and Angular 19. Multi-tenant means many companies can use it simultaneously, each seeing only their own data. I'll show you the demo tenant."

[Log in as `admin@demo-company.com`]

### Minute 2 — The dashboard
> "On the dashboard you can see total sales, total purchase, inventory value, and total orders. Below that, secondary stats and a recent transactions list. All of this is one API call to `/api/v1/Dashboard`."

[Click around the KPI cards]

### Minute 3 — The AI scan (your headline feature)
> "Adding products one by one is slow, so I built an AI feature. Watch."

[Go to Products → Add → Scan from Photo → pick `1_cola_can.png`]

> "The form just filled itself in. Behind the scenes, the image went to my API, which sent it to Google's Gemini vision model with a strict-JSON prompt. The response was parsed into a typed DTO and patched into the Angular form. I fill in cost price and category, click Save, and I have a new product."

[Save the product]

### Minute 4 — The AI chat
> "There's also an inventory copilot."

[Open chat, type "What items are running low?"]

> "The backend builds a system prompt with a snapshot of my real inventory — totals, low stock items, recent transactions — and streams Gemini's response token-by-token via Server-Sent Events. So you see the answer appear in real time."

### Minute 5 — Architecture wrap-up
> "Architecturally it's Clean Architecture in four projects — Domain, Application, Infrastructure, API. Controllers are thin and delegate to service classes; failures throw typed domain exceptions caught by one global middleware. Multi-tenancy uses EF Core global query filters reading the tenant ID from the JWT. Background jobs (low stock checks, expiry alerts) run hourly and daily on Hangfire. JWT auth with refresh-token rotation. Reports export to PDF via QuestPDF. The chat and scan features both call Gemini's REST API directly — no SDK."

> "Everything is dockerised, with a GitHub Action that deploys to Azure App Service on push to main."

Done.

---

## 10. Things you don't know yet, and that's OK

You don't have to know everything. Here are topics it's reasonable to say "I haven't gone deep on that yet" if asked:

- **Database migration management in production** — what to do when a migration takes 10 minutes on a 100M-row table. (Answer: zero-downtime migration techniques like backfilling in batches.)
- **High-availability deployment** — running the API on multiple servers with a load balancer, primary/secondary SQL Server, etc. You've got the foundation but haven't run it at that scale.
- **Performance tuning** — query plans, index design, caching strategies. You have Redis available but haven't profiled real workloads.
- **Localization (i18n)** — the app is English only. Angular has `@angular/localize` for this; the backend would need to externalise strings.
- **Comprehensive testing** — you have unit and integration test projects but they're light on coverage. Saying "the next thing I'd add is more integration tests around tenant isolation" is a great answer.

Confidence comes from honesty. "I haven't built X but I know what I'd do" is a stronger answer than pretending.

---

## Quick-reference cheat sheet

| Term | One-liner |
|---|---|
| SaaS | Software you log into, not install |
| Multi-tenant | One app, many isolated customers |
| Clean Architecture | Business rules don't depend on frameworks |
| Controller → Service pattern | Thin controller delegates to a focused service class |
| Domain exception | Typed `Exception` subclass that the global middleware maps to an HTTP code |
| `IApplicationDbContext` | Application-layer interface that exposes EF DbSets without referencing EF Core |
| DTO | Plain object that carries data between layers |
| Entity | Class representing a thing in your business |
| EF Core | C# ORM that turns LINQ into SQL |
| Migration | C# file describing a DB schema change |
| Global query filter | LINQ added to every query (used for tenant isolation) |
| JWT | Signed token that proves your identity |
| Refresh token | Long-lived token used to get new JWTs |
| Claim | Field inside a JWT (email, role, tenantId) |
| RBAC | Permission model based on roles |
| Authorization policy | Named role check applied to controllers |
| Middleware | Code running before/after every HTTP request |
| Hangfire | Library for background jobs |
| Cron expression | String describing a job schedule |
| Soft delete | Marking deleted instead of removing |
| Optimistic concurrency | Detecting concurrent edits via RowVersion |
| Rate limiting | Capping requests per IP/user |
| Correlation ID | Unique ID tying log entries to a single request |
| SSE | Streaming text from server to browser |
| REST | URL+verb-based API style |
| Swagger | Auto-generated API explorer |
| CORS | Browser rule for cross-origin requests |
| Hash + salt | Irreversible password storage |
| Reactive Forms | Angular forms backed by TypeScript objects |
| Standalone component | Angular component without an NgModule |
| Pipe | Template transformer (`| currency`) |
| Observable | RxJS stream (HTTP responses) |
| Signal | Angular's reactive primitive (toasts use it) |
| HTTP Interceptor | Code running before every Angular HTTP call |
| Gemini | Google's LLM, used for chat + vision |
| System prompt | Hidden instructions to the model |
| Token (LLM sense) | Unit of text the model bills by |
| JSON mode | Forcing the model to return valid JSON |
| User secrets | Encrypted local config for dev |
| QuestPDF | C# library for generating PDFs |
| Serilog | Structured logging library |

---

*Read the file. Re-read sections that feel hazy. Open the actual code while reading — every term in this guide maps to a specific file in the project. The fastest way to internalise it is to open the file and re-trace what the code is doing while the explanation is fresh in your head.*
