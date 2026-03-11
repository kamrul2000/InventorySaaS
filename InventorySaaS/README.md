# InventorySaaS - Multi-Tenant SaaS Inventory Management System

A production-ready, enterprise-grade multi-tenant SaaS Inventory Management System built with ASP.NET Core Web API and Angular.

## Architecture

```
InventorySaaS/
├── src/
│   ├── InventorySaaS.Domain/          # Domain entities, enums, interfaces, exceptions
│   ├── InventorySaaS.Application/     # CQRS handlers, DTOs, validators, mappings
│   ├── InventorySaaS.Infrastructure/  # EF Core, services, persistence, auth
│   └── InventorySaaS.API/            # Controllers, middleware, Program.cs
├── tests/
│   ├── InventorySaaS.UnitTests/
│   └── InventorySaaS.IntegrationTests/
├── inventory-saas-web/                # Angular 19 frontend
├── docker-compose.yml
├── Dockerfile.api
└── README.md
```

### Clean Architecture Layers

- **Domain**: Entities, value objects, enums, domain exceptions, interfaces
- **Application**: MediatR commands/queries, DTOs, FluentValidation, AutoMapper profiles
- **Infrastructure**: EF Core DbContext, entity configurations, JWT auth, file storage, email service, Hangfire jobs
- **API**: Controllers, middleware (exception handling, correlation ID, tenant resolution), Swagger

### Multi-Tenant Architecture

- Row-level data isolation via `TenantId` on every tenant-scoped entity
- EF Core global query filters enforce tenant boundaries automatically
- Tenant resolution: JWT claim (primary) → `X-TenantId` header (dev) → subdomain (production-ready)
- Super Admin can access all tenants; Tenant Admin is scoped to their tenant

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core 10 Web API |
| Frontend | Angular 19 + Angular Material 21 |
| Database | SQL Server |
| ORM | Entity Framework Core 10 |
| Authentication | JWT + Refresh Tokens |
| Authorization | Policy-based + Role-based |
| Caching | Redis (optional, falls back to memory cache) |
| Background Jobs | Hangfire |
| Logging | Serilog |
| Validation | FluentValidation |
| CQRS | MediatR |
| API Docs | Swagger / OpenAPI |
| Charts | ngx-charts |

## Modules

1. **Authentication & Authorization** - JWT login, registration, refresh tokens, password reset, RBAC
2. **Tenant Management** - Multi-tenant isolation, subscription plans, tenant settings
3. **User Management** - User CRUD, role assignment, invite users
4. **Product Management** - SKU generation, categories, brands, units, variants, images, batch/lot/expiry
5. **Warehouse Management** - Multiple warehouses, rack/bin/location support
6. **Inventory Transactions** - Stock in/out/transfer/adjustment, returns, purchase receive, sales issue
7. **Supplier Management** - Supplier CRUD with contacts and performance notes
8. **Customer Management** - Customer CRUD with types and contacts
9. **Purchase Management** - Requisitions, purchase orders, goods receipt, purchase returns
10. **Sales Management** - Sales orders, delivery, returns, invoice-ready structure
11. **Reporting & Dashboard** - Stock summary, low stock, expiry, valuation, KPI dashboard
12. **Notifications** - Low stock alerts, expiry alerts, in-app notifications
13. **Audit & Logging** - Structured logging, audit trail, correlation IDs

## Roles

| Role | Scope | Access |
|------|-------|--------|
| Super Admin | System-wide | All tenants, all features |
| Tenant Admin | Own tenant | Full access within tenant |
| Manager | Own tenant | Most features except user management |
| Staff | Own tenant | Operational features (products, inventory, orders) |
| Viewer | Own tenant | Read-only access |

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- SQL Server (LocalDB, Express, or full)
- Redis (optional)
- Angular CLI (`npm install -g @angular/cli`)

## Getting Started

### 1. Clone and configure

```bash
cd InventorySaaS
cp .env.example .env
# Edit .env with your settings
```

### 2. Backend Setup

```bash
# Restore packages
dotnet restore

# Update connection string in appsettings.Development.json if needed

# Run the API (will auto-migrate and seed data)
cd src/InventorySaaS.API
dotnet run
```

The API will be available at `https://localhost:7001` with Swagger at `https://localhost:7001/swagger`.

### 3. Frontend Setup

```bash
cd inventory-saas-web
npm install
ng serve
```

The frontend will be available at `http://localhost:4200`.

### 4. Docker Setup (Alternative)

```bash
docker-compose up -d
```

- API: http://localhost:5000
- Frontend: http://localhost
- Hangfire Dashboard: http://localhost:5000/hangfire

## Seed Data

The application automatically seeds:

### Users
| Email | Password | Role |
|-------|----------|------|
| superadmin@inventorysaas.com | Admin@123456 | Super Admin |
| admin@demo-company.com | Demo@123456 | Tenant Admin (Demo Company) |

### Demo Tenant: "Demo Company"
- 3 product categories (Electronics, Clothing, Food & Beverage)
- 2 brands, 3 units of measure
- 5 sample products with inventory
- 2 warehouses with locations
- 2 suppliers, 2 customers
- Subscription: Professional plan

## API Endpoints

### Authentication
- `POST /api/v1/auth/register` - Register new tenant
- `POST /api/v1/auth/login` - Login
- `POST /api/v1/auth/refresh-token` - Refresh JWT token
- `POST /api/v1/auth/forgot-password` - Request password reset
- `POST /api/v1/auth/reset-password` - Reset password
- `POST /api/v1/auth/logout` - Logout (revoke refresh token)

### Tenants
- `GET /api/v1/tenants` - List all tenants (Super Admin)
- `GET /api/v1/tenants/current` - Get current tenant
- `PUT /api/v1/tenants/current` - Update current tenant

### Users
- `GET /api/v1/users` - List users
- `GET /api/v1/users/{id}` - Get user
- `POST /api/v1/users` - Create user
- `PUT /api/v1/users/{id}` - Update user
- `POST /api/v1/users/invite` - Invite user

### Products
- `GET /api/v1/products` - List products (paginated, filterable)
- `GET /api/v1/products/{id}` - Get product
- `POST /api/v1/products` - Create product
- `PUT /api/v1/products/{id}` - Update product
- `DELETE /api/v1/products/{id}` - Soft delete product

### Categories, Warehouses, Suppliers, Customers
- Standard CRUD endpoints following the same pattern

### Inventory
- `GET /api/v1/inventory/balances` - Inventory balances
- `GET /api/v1/inventory/transactions` - Transaction history
- `POST /api/v1/inventory/stock-in` - Stock in
- `POST /api/v1/inventory/stock-out` - Stock out
- `POST /api/v1/inventory/transfer` - Stock transfer
- `POST /api/v1/inventory/adjustment` - Stock adjustment

### Purchase Orders
- `GET /api/v1/purchaseorders` - List POs
- `GET /api/v1/purchaseorders/{id}` - Get PO
- `POST /api/v1/purchaseorders` - Create PO
- `POST /api/v1/purchaseorders/{id}/approve` - Approve PO
- `POST /api/v1/purchaseorders/{id}/receive` - Receive goods

### Sales Orders
- `GET /api/v1/salesorders` - List SOs
- `POST /api/v1/salesorders` - Create SO
- `POST /api/v1/salesorders/{id}/confirm` - Confirm SO
- `POST /api/v1/salesorders/{id}/deliver` - Deliver SO

### Dashboard & Reports
- `GET /api/v1/dashboard` - Dashboard KPIs
- `GET /api/v1/reports/stock-summary` - Stock summary
- `GET /api/v1/reports/low-stock` - Low stock report
- `GET /api/v1/reports/expiry` - Expiry report
- `GET /api/v1/reports/inventory-valuation` - Inventory valuation

### Notifications
- `GET /api/v1/notifications` - List notifications
- `PUT /api/v1/notifications/{id}/read` - Mark as read
- `PUT /api/v1/notifications/read-all` - Mark all as read

## Database Schema

Key tables: `Tenants`, `SubscriptionPlans`, `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `RefreshTokens`, `Products`, `Categories`, `Brands`, `UnitsOfMeasure`, `ProductVariants`, `ProductImages`, `Warehouses`, `WarehouseLocations`, `InventoryBalances`, `InventoryTransactions`, `Suppliers`, `Customers`, `PurchaseOrders`, `PurchaseOrderItems`, `GoodsReceipts`, `GoodsReceiptItems`, `PurchaseRequisitions`, `SalesOrders`, `SalesOrderItems`, `Notifications`, `AuditLogs`

## Running Tests

```bash
# Unit tests
dotnet test tests/InventorySaaS.UnitTests

# Integration tests
dotnet test tests/InventorySaaS.IntegrationTests
```

## Background Jobs

Hangfire runs these recurring jobs:
- **Low Stock Check** - Every hour, checks inventory against reorder levels
- **Expiry Alert** - Daily, checks for products expiring within 30 days

Access the Hangfire dashboard at `/hangfire` (Super Admin only in production).

## Security Features

- JWT authentication with refresh token rotation
- Password hashing with PBKDF2-SHA512
- Rate limiting on authentication endpoints
- CORS policy enforcement
- Global exception handling (no sensitive data leaked)
- Correlation ID tracking across requests
- Soft delete (data never permanently removed)
- Optimistic concurrency via RowVersion
- Tenant data isolation enforced at query level

## Extending the System

### Adding a new module
1. Create domain entities in `Domain/Entities/`
2. Add EF configurations in `Infrastructure/Persistence/Configurations/`
3. Add DbSet to `ApplicationDbContext`
4. Create DTOs, Commands, Queries in `Application/Features/`
5. Add controller in `API/Controllers/`
6. Add Angular service and components in `inventory-saas-web/`

### Adding a billing provider
Implement the billing interfaces scaffolded in the subscription plan system. The `SubscriptionPlan` entity and tenant subscription tracking are ready for integration with Stripe, Paddle, or similar.

## License

Proprietary - All rights reserved.
