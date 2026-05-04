# InventorySaaS

[![Build & Deploy](https://github.com/kamrul2000/InventorySaaS/actions/workflows/main_inventorysaas.yml/badge.svg)](https://github.com/kamrul2000/InventorySaaS/actions/workflows/main_inventorysaas.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Hangfire](https://img.shields.io/badge/Hangfire-1.8-1F4E79)](https://www.hangfire.io/)
[![AI: Gemini](https://img.shields.io/badge/AI-Gemini%202.5-4285F4?logo=google&logoColor=white)](https://ai.google.dev/)
[![License: Proprietary](https://img.shields.io/badge/License-Proprietary-lightgrey)](#license)

A production-grade, multi-tenant SaaS Inventory Management System built with **ASP.NET Core 10** and **Angular 19**. Strict tenant isolation, role-based access, AI-assisted product onboarding, an AI inventory copilot, and the operational features a real warehouse needs: stock movements, purchase and sales orders, batch/expiry tracking, and PDF reporting.

> **Status**: actively developed. Backend and frontend are functional end-to-end with seeded demo data.

---

## Table of Contents

- [Screenshots](#screenshots)
- [Highlights](#highlights)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Multi-Tenancy Model](#multi-tenancy-model)
- [Feature Catalogue](#feature-catalogue)
- [Advanced Capabilities](#advanced-capabilities)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Roles & Permissions](#roles--permissions)
- [Background Jobs](#background-jobs)
- [Security](#security)
- [Testing](#testing)
- [Docker](#docker)
- [Roadmap](#roadmap)
- [License](#license)

---

## Screenshots


### Dashboard — KPIs at a glance

<img width="1360" height="644" alt="image" src="https://github.com/user-attachments/assets/ed41a5e6-2762-4fb5-9d4f-de43bfce97bb" />

<!-- Until the screenshot exists, GitHub will show a broken image. Replace with: ![Dashboard](docs/screenshots/dashboard.png) -->

### AI Product Scan — photo to draft product in one click

<img width="1354" height="614" alt="image" src="https://github.com/user-attachments/assets/1323afee-72f0-468a-aae6-012798fc611a" />

### AI Inventory Copilot — chat grounded in live tenant data

<img width="1356" height="644" alt="image" src="https://github.com/user-attachments/assets/3fe4875e-bcf0-4ccb-9a59-d147aefddb3f" />

### Product Form — manual entry with reorder level and expiry tracking

<img width="1352" height="633" alt="image" src="https://github.com/user-attachments/assets/a0c9b6ea-273c-41dc-96dd-acdbac8d49ff" />

### Inventory — balances per warehouse, location, and batch

<img width="1362" height="633" alt="image" src="https://github.com/user-attachments/assets/dec6d0e1-6729-4c82-9d89-7ab703b36d4d" />

### Purchase Order Lifecycle — draft → approve → receive

<img width="1366" height="623" alt="image" src="https://github.com/user-attachments/assets/a63bb0e8-cb39-4edf-98ba-5cf32fd08dc2" />

### Sales Order Lifecycle — draft → confirm → deliver

<img width="1366" height="639" alt="image" src="https://github.com/user-attachments/assets/1dc2101e-190e-4b40-9031-49fbed57edb8" />

### Reports — JSON and PDF exports for stock, low stock, expiry, valuation

<img width="1360" height="628" alt="image" src="https://github.com/user-attachments/assets/4acd8058-3f24-42fa-819c-6226189f6778" />
<img width="1349" height="568" alt="image" src="https://github.com/user-attachments/assets/3f315480-0c4c-4a5b-8b2c-41067cd23b58" />


### Hangfire Dashboard — recurring low-stock and expiry jobs

<img width="1349" height="548" alt="image" src="https://github.com/user-attachments/assets/8567e851-81d5-4238-a3fb-3b2faace681e" />

### Swagger — full API surface with JWT auth

<img width="1315" height="639" alt="image" src="https://github.com/user-attachments/assets/a658124f-3c89-4497-9476-f29af7258b24" />

---

## Highlights

- **AI Product Scan** — upload a product photo, the API extracts name, brand, unit, barcode, category suggestion, suggested price, and expiry-tracking hint via Gemini Vision. The Angular form pre-fills automatically.
- **AI Inventory Copilot** — a context-aware chat assistant (Gemini, server-sent events) that has live access to the tenant's inventory, low-stock state, recent transactions, sales and purchase activity. Ask "what's running low?" and it answers with real numbers.
- **Strict multi-tenancy** — every tenant-scoped table carries a `TenantId`; EF Core global query filters and an automatic `SaveChangesAsync` interceptor stamp tenant context. SuperAdmin can cross tenants; everyone else is bounded.
- **Clean Architecture + Controller→Service** — Domain / Application / Infrastructure / API split. Thin controllers delegate to focused service classes; failures flow through typed domain exceptions handled by a single global middleware.
- **Operational depth** — multi-warehouse with locations, batch and expiry tracking, stock in/out/transfer/adjustment, purchase requisitions and goods receipts, sales-order lifecycle (draft → confirm → deliver → return), reorder-level alerts.
- **PDF reporting** — stock summary, low stock, expiry, inventory valuation — each rendered server-side via QuestPDF.
- **Proactive alerts** — Hangfire recurring jobs scan for low stock hourly and expiring stock daily, posting to an in-app notification feed.
- **Hardening built in** — JWT with refresh-token rotation, PBKDF2-SHA512 password hashing, IP-based rate limiting, correlation IDs across logs and responses, soft delete, optimistic concurrency, global exception normalisation.

---

## Tech Stack

| Layer           | Technology                                              |
| --------------- | ------------------------------------------------------- |
| Backend         | ASP.NET Core 10 Web API                                 |
| Frontend        | Angular 19 + Angular Material                           |
| Database        | SQL Server (EF Core 10)                                 |
| Cache           | Redis (optional, falls back to in-memory)               |
| Background jobs | Hangfire on SQL Server                                  |
| Auth            | JWT Bearer + refresh-token rotation                     |
| App pattern     | Controller → Service → DbContext (no MediatR ceremony)  |
| Errors          | Typed domain exceptions → global `ExceptionHandlingMiddleware` (uniform JSON envelope) |
| Logging         | Serilog (console + rolling file, structured) with correlation IDs |
| AI              | Google Gemini (`gemini-2.5-flash-lite`) — chat + vision |
| PDF             | QuestPDF                                                |
| Rate limiting   | AspNetCoreRateLimit (per-IP, per-endpoint rules)        |
| API docs        | Swagger / OpenAPI                                       |

---

## Architecture

```
InventorySaaS/
├── src/
│   ├── InventorySaaS.Domain/         entities, enums, base classes, domain interfaces, exceptions
│   ├── InventorySaaS.Application/    services (one per module), DTOs, IApplicationDbContext
│   ├── InventorySaaS.Infrastructure/ EF Core, services (auth, AI, email, storage, PDF, jobs)
│   └── InventorySaaS.API/            controllers, middleware, Program.cs
├── tests/
│   ├── InventorySaaS.UnitTests/
│   └── InventorySaaS.IntegrationTests/
├── inventory-saas-web/               Angular SPA
├── docker-compose.yml
└── Dockerfile.api
```

**Flow per request**:

```
HTTP → CorrelationIdMiddleware → ExceptionHandlingMiddleware → JWT auth
     → TenantResolutionMiddleware → Authorization policy
     → Controller (thin) → Service → IApplicationDbContext
     → EF Core (with global tenant filter + soft-delete filter) → SQL Server
```

Services throw `NotFoundException` / `ConflictException` / `BadRequestException` on failure;
the global exception middleware maps each to a uniform `ProblemResponse` JSON with the right HTTP code.

---

## Multi-Tenancy Model

- **Row-level data isolation**: every tenant-scoped entity carries `TenantId` (FK to `Tenants`).
- **Automatic enforcement**: EF Core global query filters reject any query that would cross tenant boundaries — no developer has to remember to add `Where(x => x.TenantId == ...)`.
- **Tenant resolution priority**: JWT `tenant_id` claim → `X-TenantId` header (dev/admin) → subdomain (production-ready hook).
- **SuperAdmin escape hatch**: a single SuperAdmin role bypasses the global filter for cross-tenant operations.
- **Tenant-scoped reference data**: brands, units of measure, warehouses, categories — all per tenant.

---

## Feature Catalogue

### Identity & Access

- Tenant registration (creates the tenant + admin user atomically)
- Email/password login with JWT issuance
- Refresh-token rotation
- Forgot-password / reset-password flow (email tokens)
- Logout that revokes the refresh token
- User invitation by email
- Five built-in roles, twelve permission modules (see [Roles & Permissions](#roles--permissions))

### Product & Catalogue

- Products with auto-generated SKU (collision-safe via prefix-max algorithm)
- Categories, brands, units of measure
- Product variants and product images
- Barcode field (manual or AI-extracted)
- Track-expiry flag for perishables
- Reorder level for low-stock alerting
- Soft delete

### Warehouse & Inventory

- Multiple warehouses per tenant
- Warehouse locations (aisle / rack / bin)
- Inventory balances per (product, warehouse, batch)
- Batch number and expiry date tracking
- Stock movements: stock in, stock out, transfer between warehouses, adjustment
- Inventory transaction ledger (full audit trail of every movement)

### Procurement

- Purchase requisitions
- Purchase orders with line items
- PO approval workflow
- Goods receipt against PO (partial receives supported)

### Sales

- Sales orders with line items
- Order lifecycle: Draft → Confirmed → Delivered → Returned
- Customer master with type and contact details

### Reporting

Each report has a JSON endpoint and a `*/pdf` companion that returns a styled PDF:

- Stock summary (current stock by product/warehouse with valuation)
- Low stock (items at or below reorder level)
- Expiry (items expiring within N days)
- Inventory valuation (cost vs. selling value, by category)

### Dashboard

A single KPI endpoint that returns: total products / warehouses / suppliers / customers, low-stock count, expiring count, total inventory value, total sales, total purchases, total orders, recent transactions, top products, stock alerts, recent sales orders, low-stock products.

### Notifications

- In-app feed
- Mark single or all as read
- Auto-generated by Hangfire jobs (low stock, expiry)

### Tenants & Subscription

- SuperAdmin can list/manage all tenants
- Tenant Admin can view/update only their tenant
- Subscription plan model with tiered feature limits (Free / Basic / Professional / Enterprise) — wired for billing-provider integration

### Settings, Audit, User Management

- User CRUD within a tenant
- Audit log table for compliance
- Tenant-level settings

---

## Advanced Capabilities

### 1. AI Product Scan (Vision)

`POST /api/v1/Products/extract-from-image`

Upload a JPEG/PNG of a product (label, packaging, shelf shot) up to 5 MB. The service:

1. Buffers and base64-encodes the image
2. Calls the Gemini `generateContent` REST API with `responseMimeType: application/json` and a strongly-typed prompt (kept as a tunable constant in the service file)
3. Parses the response, strips defensive markdown fences if any
4. Returns a typed `ProductExtractionResult` DTO — never the raw model response

The Angular `product-form` component has a "Scan from Photo" button that calls this endpoint, fuzzy-matches the suggested category against the tenant's existing categories, and patches the form. A confirm dialog protects user-edited fields from being overwritten.

**This endpoint never persists**. The user reviews the draft and submits the existing `POST /api/v1/Products` flow. This is a deliberate two-step UX: extract → review → save.

### 2. AI Inventory Copilot (Chat)

`POST /api/v1/Chat` — server-sent events stream

The service builds a per-request inventory context block (overview KPIs, low stock items, top products, recent transactions, recent sales/purchase orders) and sends it as the system prompt to Gemini. The model has retry-with-backoff on HTTP 429 and 503 (3 attempts, 2s/4s/6s).

Examples:

- _"What items are running low?"_
- _"What did we sell last week?"_
- _"Which suppliers have we been using?"_

The frontend renders responses with markdown formatting.

### 3. Service-based Application Layer

Each module exposes a single `IXxxService` (e.g. `IProductService`, `IInventoryService`). Controllers are intentionally thin — they bind input, call the service, and return its DTO. There is no MediatR, no `Result<T>` wrapper, no per-request pipeline. Failures throw typed domain exceptions (`NotFoundException`, `ConflictException`, `BadRequestException`) which the [ExceptionHandlingMiddleware](InventorySaaS/src/InventorySaaS.API/Middleware/ExceptionHandlingMiddleware.cs) translates to 404 / 409 / 400 with a uniform JSON shape.

Cross-cutting concerns are now framework-native: ASP.NET's request logging covers the timing dimension, Serilog correlation IDs tie everything to one request, and validation lives at the model-binding layer (data annotations on Request DTOs) plus inline guards in services. Adding a new endpoint is a single service method + a single controller action.

### 4. Hangfire Recurring Jobs + Dashboard

- `check-low-stock` — hourly cron, scans inventory below reorder level, creates unread `LowStock` notifications per affected product
- `check-expiry-alerts` — daily cron, finds items expiring within 30 days, creates `ExpiryAlert` notifications

The Hangfire dashboard at `/hangfire` is gated by a custom `HangfireAuthorizationFilter` (SuperAdmin in production).

### 5. PDF Reporting via QuestPDF

`GET /api/v1/Reports/{report}/pdf` — server-rendered PDFs with the tenant's data. QuestPDF gives us pixel-precise layout without headless-browser overhead. Reports support the same query filters as their JSON counterparts.

### 6. Refresh-Token Rotation

Login issues a short-lived JWT (60 min) plus a long-lived `RefreshToken` row. Each `/refresh-token` call invalidates the old token and issues a new one — replay attacks reveal themselves immediately.

### 7. Correlation IDs Across the Stack

Every request gets an `X-Correlation-Id` header (echoed in response and structured logs). When something goes wrong, one ID lets you find the request in logs, the user-visible error, and the Hangfire job that may have triggered it.

### 8. Streaming Server-Sent Events

The chat endpoint streams Gemini's SSE output straight to the browser — tokens render as they arrive, not after a full round-trip wait.

### 9. Optimistic Concurrency

Concurrent edits on the same record are caught via EF Core `RowVersion` columns rather than silently overwritten.

### 10. Health Checks

`GET /health` runs an EF Core `DbContextCheck` so probes catch broken database state, not just "is the process alive".

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- Node.js 20+ and npm
- SQL Server (LocalDB, Express, full, or via Docker)

### 1. Clone

```bash
git clone https://github.com/kamrul2000/InventorySaaS.git
cd InventorySaaS
```

### 2. Backend

```bash
cd src/InventorySaaS.API

# Optional: AI features. Without this, chat and product scan are disabled,
# the rest of the app works.
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "<your-google-ai-key>"

dotnet run
```

The API auto-migrates the database and seeds demo data on startup.

- API: `https://localhost:7001`
- Swagger: `https://localhost:7001/swagger`
- Hangfire: `https://localhost:7001/hangfire`
- Health: `https://localhost:7001/health`

### 3. Frontend

```bash
cd inventory-saas-web
npm install
npm start
```

Frontend at `http://localhost:4200`.

### 4. Sign in

Use seeded credentials:

| Role                    | Email                          | Password       |
| ----------------------- | ------------------------------ | -------------- |
| Super Admin             | `superadmin@inventorysaas.com` | `Admin@123456` |
| Tenant Admin (Demo Co.) | `admin@demo-company.com`       | `Demo@123456`  |

---

## Configuration

### `appsettings.json` keys

| Section                                | Purpose                                                  |
| -------------------------------------- | -------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection`  | SQL Server (app data)                                    |
| `ConnectionStrings:HangfireConnection` | SQL Server (Hangfire); falls back to `DefaultConnection` |
| `ConnectionStrings:Redis`              | optional; if empty, in-memory cache is used              |
| `JwtSettings`                          | issuer, audience, secret, expiry minutes                 |
| `AllowedOrigins`                       | CORS origins (production)                                |
| `FileStorage`                          | local file storage base path and URL                     |
| `Smtp`                                 | host, port, username, password, from-address             |
| `Gemini:ApiKey`                        | **leave empty here**; set via user secrets or env var    |
| `IpRateLimiting`                       | per-IP, per-endpoint rules                               |
| `Serilog`                              | log level overrides                                      |

### Secrets — never commit them

For local development:

```bash
cd src/InventorySaaS.API
dotnet user-secrets set "Gemini:ApiKey" "..."
dotnet user-secrets set "JwtSettings:Secret" "..."
dotnet user-secrets set "Smtp:Password" "..."
```

For production (Azure App Service, Docker, k8s):

```
Gemini__ApiKey         = ...
JwtSettings__Secret    = ...
Smtp__Password         = ...
```

(.NET maps `__` to `:` — `Gemini__ApiKey` becomes `Gemini:ApiKey`.)

---

## API Reference

All endpoints are versioned `/api/v1/`. Authentication is `Authorization: Bearer <jwt>` except where noted.

### Auth

| Verb | Path                    | Notes                      |
| ---- | ----------------------- | -------------------------- |
| POST | `/auth/register`        | tenant + admin user        |
| POST | `/auth/login`           | issues JWT + refresh token |
| POST | `/auth/refresh-token`   | rotates refresh token      |
| POST | `/auth/forgot-password` | sends reset email          |
| POST | `/auth/reset-password`  | applies reset token        |
| POST | `/auth/logout`          | revokes refresh token      |

### Products

| Verb   | Path                           | Auth Policy | Notes                                      |
| ------ | ------------------------------ | ----------- | ------------------------------------------ |
| GET    | `/Products`                    | ViewerUp    | paginated, filter by category/brand, sort  |
| GET    | `/Products/{id}`               | ViewerUp    |                                            |
| POST   | `/Products`                    | StaffUp     | auto-generates SKU                         |
| PUT    | `/Products/{id}`               | StaffUp     |                                            |
| DELETE | `/Products/{id}`               | ManagerUp   | soft delete                                |
| POST   | `/Products/extract-from-image` | StaffUp     | **AI vision** — multipart, JPEG/PNG ≤ 5 MB |

### Categories / Suppliers / Customers / Warehouses

Standard CRUD: `GET` (list/by-id), `POST`, `PUT`, plus `POST /Warehouses/{id}/locations` for warehouse locations.

### Inventory

| Verb | Path                      | Notes                           |
| ---- | ------------------------- | ------------------------------- |
| GET  | `/Inventory/balances`     | per (product, warehouse, batch) |
| GET  | `/Inventory/transactions` | full ledger                     |
| POST | `/Inventory/stock-in`     |                                 |
| POST | `/Inventory/stock-out`    |                                 |
| POST | `/Inventory/transfer`     | between warehouses              |
| POST | `/Inventory/adjustment`   | manual correction               |

### Purchase Orders

| Verb | Path                           |
| ---- | ------------------------------ |
| GET  | `/PurchaseOrders`              |
| GET  | `/PurchaseOrders/{id}`         |
| POST | `/PurchaseOrders`              |
| POST | `/PurchaseOrders/{id}/approve` |
| POST | `/PurchaseOrders/{id}/receive` |

### Sales Orders

| Verb | Path                        |
| ---- | --------------------------- |
| GET  | `/SalesOrders`              |
| GET  | `/SalesOrders/{id}`         |
| POST | `/SalesOrders`              |
| POST | `/SalesOrders/{id}/confirm` |
| POST | `/SalesOrders/{id}/deliver` |

### Reports

| Verb | Path                               | Notes            |
| ---- | ---------------------------------- | ---------------- |
| GET  | `/Reports/stock-summary`           | JSON             |
| GET  | `/Reports/stock-summary/pdf`       | **PDF download** |
| GET  | `/Reports/low-stock`               | JSON             |
| GET  | `/Reports/low-stock/pdf`           | PDF              |
| GET  | `/Reports/expiry`                  | JSON             |
| GET  | `/Reports/expiry/pdf`              | PDF              |
| GET  | `/Reports/inventory-valuation`     | JSON             |
| GET  | `/Reports/inventory-valuation/pdf` | PDF              |

### Dashboard / Notifications / Users / Tenants

- `GET /Dashboard` — KPI bundle
- `GET /Notifications`, `PUT /Notifications/{id}/read`, `PUT /Notifications/read-all`
- `GET/POST/PUT /Users`, `POST /Users/invite`
- `GET /Tenants` (SuperAdminOnly), `GET /Tenants/current`, `PUT /Tenants/current`

### Chat (AI)

- `POST /Chat` — server-sent events stream

---

## Roles & Permissions

| Role            | Scope       | Effective Access                                |
| --------------- | ----------- | ----------------------------------------------- |
| **SuperAdmin**  | system-wide | every tenant, every feature, Hangfire dashboard |
| **TenantAdmin** | own tenant  | full access within tenant                       |
| **Manager**     | own tenant  | everything except user management               |
| **Staff**       | own tenant  | products, inventory, orders (operational)       |
| **Viewer**      | own tenant  | read-only                                       |

Authorization is policy-based. The five policies are:

```csharp
SuperAdminOnly  // SuperAdmin
TenantAdminOnly // TenantAdmin, SuperAdmin
ManagerUp       // Manager, TenantAdmin, SuperAdmin
StaffUp         // Staff, Manager, TenantAdmin, SuperAdmin
ViewerUp        // Viewer, Staff, Manager, TenantAdmin, SuperAdmin
```

Twelve permission modules are seeded (Products, Categories, Warehouses, Inventory, Suppliers, Customers, PurchaseOrders, SalesOrders, Reports, Users, Settings, AuditLogs), each with multiple actions — wired into the database for fine-grained permission checks.

---

## Background Jobs

| Job                   | Schedule | What it does                                                                            |
| --------------------- | -------- | --------------------------------------------------------------------------------------- |
| `check-low-stock`     | hourly   | finds inventory ≤ reorder level, creates `LowStock` notifications per (product, tenant) |
| `check-expiry-alerts` | daily    | finds items expiring within 30 days, creates `ExpiryAlert` notifications                |

Visit `/hangfire` (SuperAdmin) to inspect runs and trigger jobs manually.

---

## Security

- **JWT** with issuer, audience, lifetime, signing-key validation; zero clock skew
- **Refresh tokens** stored as DB rows, rotated on every refresh
- **Password hashing**: PBKDF2-SHA512 (`PasswordHasherService`)
- **Rate limiting**: per-IP, per-endpoint via `AspNetCoreRateLimit` — login is throttled to 10/min
- **Tenant isolation**: enforced at the EF Core query level via global filters
- **CORS**: open for `localhost` in dev, locked to `AllowedOrigins` in production
- **Soft delete**: data is never physically removed
- **Optimistic concurrency**: `RowVersion` on key entities
- **Global exception handling**: every error normalised to a JSON envelope with correlation ID and _no stack traces in production_
- **Auth interceptor on the SPA**: handles 401 by transparently calling `/refresh-token` and replaying the original request

---

## Testing

```bash
# Unit tests
dotnet test tests/InventorySaaS.UnitTests

# Integration tests
dotnet test tests/InventorySaaS.IntegrationTests
```

The `Program` class is exposed as `partial public class Program` so `WebApplicationFactory<Program>` can spin up the API in-process for integration tests.

---

## Docker

```bash
docker compose up -d
```

This brings up:

- SQL Server 2022 (with healthcheck)
- Redis 7
- API container (waits for SQL Server health)
- Frontend container

The Hangfire database is created on the same SQL Server instance under a separate database name.

---


## License

Proprietary — All rights reserved.
