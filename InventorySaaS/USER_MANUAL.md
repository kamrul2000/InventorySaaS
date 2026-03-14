# InventorySaaS - Complete User Manual

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Login Credentials](#2-login-credentials)
3. [System Architecture & Roles](#3-system-architecture--roles)
4. [SuperAdmin Guide](#4-superadmin-guide)
5. [Tenant Admin Guide](#5-tenant-admin-guide)
6. [Manager Guide](#6-manager-guide)
7. [Staff Guide](#7-staff-guide)
8. [Viewer Guide](#8-viewer-guide)
9. [Module-by-Module Walkthrough](#9-module-by-module-walkthrough)
10. [API Reference](#10-api-reference)
11. [Configuration](#11-configuration)
12. [Docker Deployment](#12-docker-deployment)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Getting Started

### Prerequisites

| Software | Version | Required |
|---|---|---|
| .NET SDK | 10+ | Yes |
| Node.js | 20+ | Yes |
| SQL Server | 2019+ (or LocalDB) | Yes |
| Angular CLI | Latest (`npm i -g @angular/cli`) | Yes |
| Redis | 7+ | Optional (falls back to in-memory cache) |

### Running the Backend

```bash
cd InventorySaaS
dotnet restore
dotnet run --project src/InventorySaaS.API
```

The API starts at `http://localhost:5179` (or whichever port .NET assigns).
Swagger UI: `http://localhost:5179/swagger`

> On first run, the database is automatically created and seeded with demo data (subscription plans, roles, permissions, a SuperAdmin user, a demo tenant with products, warehouses, inventory, suppliers, and customers).

### Running the Frontend

```bash
cd inventory-saas-web
npm install
ng serve
```

The Angular app starts at `http://localhost:4200` (or next available port).
Open it in your browser and you'll see the login page.

### Running with Docker (Alternative)

```bash
cd InventorySaaS
docker-compose up -d
```

| Service | URL |
|---|---|
| Frontend | http://localhost |
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Hangfire Dashboard | http://localhost:5000/hangfire |

---

## 2. Login Credentials

### Seeded Accounts

| Account | Email | Password | Role | Tenant |
|---|---|---|---|---|
| **System Administrator** | `superadmin@inventorysaas.com` | `Admin@123456` | SuperAdmin | None (system-wide) |
| **Demo Tenant Admin** | `admin@demo-company.com` | `Demo@123456` | TenantAdmin | Demo Company |

### Which Account Should I Use?

- **To manage the entire system** (all tenants, subscription plans): Use **SuperAdmin**
- **To use the inventory system as a regular business**: Use **Demo Tenant Admin**
- **To test the full workflow** (products, orders, inventory): Use **Demo Tenant Admin**

---

## 3. System Architecture & Roles

### Multi-Tenant Model

InventorySaaS is a multi-tenant SaaS application. Each tenant (company) has completely isolated data. A tenant cannot see or modify another tenant's data.

- **SuperAdmin** exists outside of any tenant and can see all tenants
- All other roles belong to a specific tenant

### Role Hierarchy

```
SuperAdmin (System-wide)
  └── TenantAdmin (Full control within a tenant)
        └── Manager (Most operations, except user management)
              └── Staff (Day-to-day operations)
                    └── Viewer (Read-only access)
```

### What Each Role Can Do

| Feature | SuperAdmin | TenantAdmin | Manager | Staff | Viewer |
|---|---|---|---|---|---|
| View Dashboard | Yes | Yes | Yes | Yes | Yes |
| View Products/Categories | Yes | Yes | Yes | Yes | Yes |
| Create/Edit Products | Yes | Yes | Yes | Yes | No |
| Delete Products | Yes | Yes | Yes | No | No |
| View Warehouses | Yes | Yes | Yes | Yes | Yes |
| Create/Edit Warehouses | Yes | Yes | Yes | No | No |
| Stock In / Stock Out | Yes | Yes | Yes | Yes | No |
| Stock Transfer | Yes | Yes | Yes | Yes | No |
| Stock Adjustment | Yes | Yes | Yes | No | No |
| View Suppliers/Customers | Yes | Yes | Yes | Yes | Yes |
| Create/Edit Suppliers/Customers | Yes | Yes | Yes | Yes | No |
| Create Purchase Orders | Yes | Yes | Yes | Yes | No |
| Approve Purchase Orders | Yes | Yes | Yes | No | No |
| Receive Goods (PO) | Yes | Yes | Yes | Yes | No |
| Create Sales Orders | Yes | Yes | Yes | Yes | No |
| Confirm Sales Orders | Yes | Yes | Yes | No | No |
| Deliver Sales Orders | Yes | Yes | Yes | Yes | No |
| View Reports | Yes | Yes | Yes | Yes | Yes |
| View Notifications | Yes | Yes | Yes | Yes | Yes |
| Manage Users | Yes | Yes | No | No | No |
| Manage Settings | Yes | Yes | No | No | No |
| Manage All Tenants | Yes | No | No | No | No |

---

## 4. SuperAdmin Guide

### What is SuperAdmin?

The SuperAdmin is the system-level administrator who manages the entire SaaS platform. This account does NOT belong to any tenant.

### Login

1. Open the application in your browser
2. Enter email: `superadmin@inventorysaas.com`
3. Enter password: `Admin@123456`
4. Click **Login**

### What You Can Do

#### View All Tenants
- Navigate to the **Dashboard** to see system-wide statistics
- The system manages tenants through the API (`GET /api/v1/tenants`)

#### Hangfire Dashboard
- Access background job monitoring at `/hangfire`
- View scheduled jobs: Low Stock Check (hourly) and Expiry Alert (daily)
- Only SuperAdmin can access the Hangfire dashboard

#### Swagger API
- Access the full API documentation at `/swagger`
- Test any API endpoint directly from the Swagger UI
- Use the **Authorize** button to enter your JWT token

### Limitations
- The SuperAdmin has no tenant context, so tenant-specific pages like **Settings** will show "Not Found"
- To test tenant features, log in as a Tenant Admin instead

---

## 5. Tenant Admin Guide

### What is Tenant Admin?

The Tenant Admin is the owner/administrator of a specific company (tenant). They have full control over their company's data and users.

### Login

1. Open the application in your browser
2. Enter email: `admin@demo-company.com`
3. Enter password: `Demo@123456`
4. Click **Login**

### First Steps After Login

1. **Dashboard** loads automatically showing:
   - Total Products, Total Warehouses, Low Stock Items, Inventory Value
   - Recent Transactions table
   - Stock Alerts table

2. **Review Company Settings** (sidebar → Settings):
   - Company Name, Contact Email, Phone
   - Address, City, Country
   - Currency, Timezone
   - Logo URL

3. **Manage Users** (sidebar → User Management):
   - View all users in your company
   - **Add User**: Create a new user with email, name, password, and role
   - **Invite User**: Send an invitation email to a new team member
   - **Edit User**: Change name, phone, active status, and roles

### Creating a New Tenant (Registration)

1. Go to the Login page
2. Click **"Create Account"** (or navigate to `/auth/register`)
3. Fill in:
   - **Company Name**: Your business name
   - **First Name** and **Last Name**: Admin's name
   - **Email**: Admin's email (becomes login)
   - **Phone**: Optional
   - **Password**: Minimum 8 characters
4. Click **Register**
5. You'll be automatically logged in as TenantAdmin of your new company

---

## 6. Manager Guide

### What is a Manager?

Managers can perform most operations except user management. They can approve purchase orders, adjust inventory, and manage warehouses.

### Key Capabilities
- Create, edit, and delete products
- Create and manage warehouses and locations
- Perform stock adjustments (correct inventory counts)
- Approve purchase orders
- Confirm sales orders
- View all reports

### Things You Cannot Do
- Manage users (create, invite, edit users)
- Change company settings

---

## 7. Staff Guide

### What is Staff?

Staff members handle day-to-day operations like creating products, processing orders, and managing inventory movements.

### Key Capabilities
- Create and edit products, suppliers, customers
- Stock In and Stock Out operations
- Transfer inventory between warehouses
- Create purchase orders and receive goods
- Create and deliver sales orders

### Things You Cannot Do
- Delete products
- Create or manage warehouses
- Adjust inventory (manual corrections)
- Approve purchase orders
- Confirm sales orders
- Manage users or settings

---

## 8. Viewer Guide

### What is a Viewer?

Viewers have read-only access. They can see all data but cannot create, edit, or delete anything.

### Key Capabilities
- View dashboard with KPIs
- Browse products, categories, warehouses
- View inventory balances and transactions
- View supplier and customer lists
- View purchase and sales orders
- View all reports
- View and mark notifications as read

### Things You Cannot Do
- Create, edit, or delete any records
- Perform inventory operations
- Manage users or settings

---

## 9. Module-by-Module Walkthrough

### 9.1 Dashboard

**Path:** `/dashboard` (loads after login)

The dashboard provides a quick overview of your business:

**KPI Cards (top row):**
- **Total Products** - Number of active products in your catalog
- **Total Warehouses** - Number of warehouses
- **Low Stock Items** - Products below reorder level (highlighted in orange)
- **Inventory Value** - Total value of all inventory on hand

**Recent Transactions Table:**
| Column | Description |
|---|---|
| Transaction # | Auto-generated transaction number |
| Type | Color-coded badge: StockIn (green), StockOut (red), Transfer (blue), Adjustment (orange) |
| Product | Product name |
| Quantity | Number of units |
| Date | Transaction date |

**Stock Alerts Table:**
Shows products that are below their reorder level, including current stock and reorder level.

---

### 9.2 Products

**Path:** `/products`

#### Viewing Products
- Paginated table with columns: Name, SKU, Category, Brand, Cost Price, Selling Price, Active
- Use the **search bar** to filter by name or SKU
- Click column headers to sort
- Use pagination controls at the bottom

#### Creating a Product
1. Click **"Add Product"** button
2. Fill in the form:
   - **Name** (required): Product name
   - **SKU**: Leave empty for auto-generation (format: `XXXX-001`)
   - **Barcode**: Optional barcode number
   - **Category** (required): Select from dropdown
   - **Brand**: Optional
   - **Unit of Measure** (required): pcs, kg, box, etc.
   - **Cost Price** (required): Your purchase price
   - **Selling Price** (required): Your selling price
   - **Reorder Level**: Minimum stock threshold for alerts
   - **Track Expiry**: Enable for perishable products
   - **Active**: Toggle product visibility
3. Click **Save**

#### Editing a Product
- Click the **edit icon** on any product row
- Modify fields and click **Save**

#### Deleting a Product
- Click the **delete icon** on any product row
- Confirm in the dialog (soft delete - data is preserved)

---

### 9.3 Categories

**Path:** `/categories`

#### Viewing Categories
- Table shows: Name, Description, Product Count, Active status

#### Creating a Category
1. Click **"Add Category"** button
2. A dialog opens with fields:
   - **Name** (required)
   - **Description**
   - **Parent Category**: For sub-categories
3. Click **Save**

#### Editing a Category
- Click the edit icon → dialog opens with existing data

---

### 9.4 Warehouses

**Path:** `/warehouses`

#### Viewing Warehouses
- Table shows: Name, Code, City, Default status, Active status, Location Count

#### Creating a Warehouse
1. Click **"Add Warehouse"**
2. Fill in:
   - **Name** (required): e.g., "Main Warehouse"
   - **Code** (required): e.g., "WH-001"
   - **Address, City, Country**
   - **Contact Person, Phone**
   - **Is Default**: Check if this is the primary warehouse
3. Click **Save**

#### Warehouse Locations
Locations represent physical positions within a warehouse (Aisle → Rack → Bin).

Example: `A1-R1-B1` means Aisle 1, Rack 1, Bin 1.

To add locations, use the API: `POST /api/v1/warehouses/{warehouseId}/locations`

---

### 9.5 Inventory

**Path:** `/inventory`

This is the core module with **two tabs**:

#### Balances Tab
Shows current stock levels:
| Column | Description |
|---|---|
| Product | Product name |
| SKU | Stock Keeping Unit |
| Warehouse | Which warehouse |
| Location | Aisle-Rack-Bin |
| Qty On Hand | Total quantity physically present |
| Qty Reserved | Quantity committed to sales orders |
| Qty Available | On Hand minus Reserved |
| Unit Cost | Cost per unit |

Use the **Warehouse filter** dropdown to view stock for a specific warehouse.

#### Transactions Tab
Shows all inventory movements:
| Column | Description |
|---|---|
| Transaction # | Auto-generated number |
| Type | StockIn, StockOut, Transfer, Adjustment, PurchaseReceive, SalesIssue |
| Product | Product name |
| Warehouse | Warehouse involved |
| Quantity | Units moved |
| Date | When it happened |

#### Stock In (Adding Inventory)
1. Click **"Stock In"** button
2. Fill in the dialog:
   - **Product**: Select from dropdown
   - **Warehouse**: Select destination warehouse
   - **Location**: Optional specific bin location
   - **Quantity**: Number of units to add
   - **Unit Cost**: Cost per unit
   - **Batch Number**: Optional batch tracking
   - **Expiry Date**: For perishable products
   - **Notes**: Optional description
3. Click **Submit**

#### Stock Transfer (Between Warehouses)
1. Click **"Transfer"** button
2. Fill in the dialog:
   - **Product**: Select product
   - **Source Warehouse + Location**: Where stock is coming from
   - **Destination Warehouse + Location**: Where stock is going
   - **Quantity**: Number of units
   - **Notes**: Optional
3. Click **Submit**

#### Stock Out
Use the API: `POST /api/v1/inventory/stock-out`

#### Stock Adjustment (Manager+ only)
Use the API: `POST /api/v1/inventory/adjustment`
This manually sets a new quantity (for corrections after physical counts).

---

### 9.6 Suppliers

**Path:** `/suppliers`

#### Viewing Suppliers
- Table: Name, Code, Contact Person, Email, Phone, City, Active

#### Creating a Supplier
1. Click **"Add Supplier"**
2. Fill in:
   - **Name** (required)
   - **Code** (required): e.g., "SUP-001"
   - **Contact Person, Email, Phone**
   - **Address, City, Country**
   - **Tax ID**
   - **Payment Terms**: e.g., "Net 30"
3. Click **Save**

---

### 9.7 Customers

**Path:** `/customers`

#### Viewing Customers
- Table: Name, Code, Type (Retail/Wholesale), Contact Person, Email, Phone, City, Active

#### Creating a Customer
1. Click **"Add Customer"**
2. Fill in:
   - **Name** (required)
   - **Code** (required): e.g., "CUS-001"
   - **Customer Type**: Retail or Wholesale
   - **Contact Person, Email, Phone**
   - **Address, City, Country**
   - **Tax ID, Payment Terms, Credit Limit**
3. Click **Save**

---

### 9.8 Purchase Orders

**Path:** `/purchase-orders`

Purchase Orders (POs) track what you buy from suppliers.

#### PO Workflow

```
Draft → Submitted → Approved → Received
                              ↘ Partially Received → Received
```

#### Creating a Purchase Order
1. Click **"Create PO"**
2. Fill in:
   - **Supplier** (required): Select from dropdown
   - **Warehouse** (required): Where goods will be received
   - **Expected Delivery Date**: When you expect delivery
3. Add line items:
   - Click **"Add Item"**
   - Select **Product**, enter **Quantity** and **Unit Price**
   - Line Total is calculated automatically
   - Add as many items as needed
4. Review the **Grand Total**
5. Click **Save**

#### Approving a Purchase Order (Manager+ only)
1. Go to `/purchase-orders`
2. Click on the PO to view details
3. Click **"Approve"** button (available when status is Draft or Submitted)

#### Receiving Goods
1. Open an Approved PO
2. Click **"Receive Goods"**
3. Enter received quantities for each item
4. The system automatically:
   - Creates inventory balances (Stock In)
   - Records inventory transactions
   - Updates PO status to "Received" or "Partially Received"

---

### 9.9 Sales Orders

**Path:** `/sales-orders`

Sales Orders (SOs) track what you sell to customers.

#### SO Workflow

```
Draft → Confirmed → Delivered
                  ↘ Partially Delivered → Delivered
```

#### Creating a Sales Order
1. Click **"Create SO"**
2. Fill in:
   - **Customer** (required): Select from dropdown
   - **Warehouse** (required): Where goods ship from
   - **Delivery Date**: Expected delivery
   - **Shipping Address**: Delivery address
3. Add line items:
   - Click **"Add Item"**
   - Select **Product**, enter **Quantity** and **Unit Price**
4. Click **Save**

#### Confirming a Sales Order (Manager+ only)
1. Open the SO detail page
2. Click **"Confirm"** (available when status is Draft)

#### Delivering a Sales Order
1. Open a Confirmed SO
2. Click **"Deliver"**
3. Enter delivered quantities
4. The system automatically:
   - Deducts inventory (Stock Out)
   - Records inventory transactions
   - Updates SO status

---

### 9.10 Reports

**Path:** `/reports`

Four report tabs available:

#### Stock Summary Report
- Shows current stock across all products and warehouses
- Columns: Product, SKU, Category, Warehouse, Quantity, Unit Cost, Total Value
- **Filters:** Warehouse, Category

#### Low Stock Report
- Products below their reorder level
- Columns: Product, SKU, Warehouse, Current Stock (orange), Reorder Level, Deficit (red)
- **Filter:** Warehouse

#### Expiry Report
- Products approaching expiration
- Columns: Product, SKU, Warehouse, Batch, Expiry Date, Quantity, Days Left
- Color coding: Red (≤7 days), Orange (≤30 days)
- **Filter:** Warehouse, Days Ahead (default: 30)

#### Inventory Valuation Report
- Financial summary by category
- Columns: Category, Product Count, Total Cost Value, Total Selling Value
- **Filter:** Warehouse

---

### 9.11 Notifications

**Path:** `/notifications`

- Lists all notifications with title, message, and timestamp
- Unread notifications are highlighted
- **Types:** Low Stock, Expiry Alert, Purchase Order Created, Sales Order Created, Stock Transfer, System Alert, User Invitation
- **Actions:**
  - Click a notification to mark it as read
  - Click **"Mark All as Read"** to clear all

Notifications are generated automatically by background jobs:
- **Low Stock Check**: Runs every hour, creates alerts when stock drops below reorder level
- **Expiry Alert**: Runs daily, creates alerts for products expiring within 30 days

---

### 9.12 User Management (TenantAdmin only)

**Path:** `/users`

#### Viewing Users
- Table: Email, First Name, Last Name, Active, Created Date

#### Creating a User
1. Click **"Add User"**
2. Fill in:
   - **Email** (required)
   - **First Name** (required)
   - **Last Name** (required)
   - **Password** (required, min 8 chars)
   - **Phone**: Optional
   - **Roles**: Select one or more (TenantAdmin, Manager, Staff, Viewer)
3. Click **Save**

The new user can immediately log in with their email and password.

#### Inviting a User
1. Click **"Invite User"**
2. Enter their email, name, and roles
3. An invitation notification is created (email sending requires SMTP configuration)

#### Editing a User
- Click edit icon on a user row
- Change name, phone, active status, or roles

---

### 9.13 Settings (TenantAdmin only)

**Path:** `/settings`

Update your company information:
- **Company Name**
- **Contact Email**
- **Phone**
- **Address, City, Country**
- **Currency** (e.g., USD, EUR, BDT)
- **Timezone** (e.g., UTC, America/New_York)
- **Logo URL**

---

## 10. API Reference

### Base URL
```
http://localhost:5179/api/v1
```

### Authentication
All authenticated endpoints require a Bearer token in the `Authorization` header:
```
Authorization: Bearer <your-jwt-token>
```

### Getting a Token

**POST** `/api/v1/auth/login`
```json
{
  "email": "admin@demo-company.com",
  "password": "Demo@123456"
}
```

Response:
```json
{
  "isSuccess": true,
  "value": {
    "accessToken": "eyJhbG...",
    "refreshToken": "abc123...",
    "expiresAt": "2026-03-14T13:00:00Z",
    "email": "admin@demo-company.com",
    "fullName": "Demo Admin",
    "roles": ["TenantAdmin"],
    "tenantId": "..."
  }
}
```

### Complete Endpoint List

#### Auth (`/auth`)
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/register` | No | Register new tenant + admin |
| POST | `/login` | No | Login, get JWT + refresh token |
| POST | `/refresh-token` | No | Refresh expired JWT |
| POST | `/forgot-password` | No | Request password reset |
| POST | `/reset-password` | No | Reset password with token |
| POST | `/logout` | Yes | Revoke refresh token |

#### Dashboard (`/dashboard`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | Get KPIs, recent transactions, alerts |

#### Products (`/products`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List products (paginated) |
| GET | `/{id}` | Viewer+ | Get product by ID |
| POST | `/` | Staff+ | Create product |
| PUT | `/{id}` | Staff+ | Update product |
| DELETE | `/{id}` | Manager+ | Delete product |

#### Categories (`/categories`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List categories |
| GET | `/{id}` | Viewer+ | Get category by ID |
| POST | `/` | Staff+ | Create category |
| PUT | `/{id}` | Staff+ | Update category |

#### Warehouses (`/warehouses`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List warehouses |
| GET | `/{id}` | Viewer+ | Get warehouse by ID |
| POST | `/` | Manager+ | Create warehouse |
| PUT | `/{id}` | Manager+ | Update warehouse |
| POST | `/{id}/locations` | Manager+ | Add location to warehouse |

#### Inventory (`/inventory`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/balances` | Viewer+ | List inventory balances |
| GET | `/transactions` | Viewer+ | List transactions |
| POST | `/stock-in` | Staff+ | Add stock |
| POST | `/stock-out` | Staff+ | Remove stock |
| POST | `/transfer` | Staff+ | Transfer between warehouses |
| POST | `/adjustment` | Manager+ | Manual stock adjustment |

#### Suppliers (`/suppliers`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List suppliers |
| GET | `/{id}` | Viewer+ | Get supplier by ID |
| POST | `/` | Staff+ | Create supplier |
| PUT | `/{id}` | Staff+ | Update supplier |

#### Customers (`/customers`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List customers |
| GET | `/{id}` | Viewer+ | Get customer by ID |
| POST | `/` | Staff+ | Create customer |
| PUT | `/{id}` | Staff+ | Update customer |

#### Purchase Orders (`/purchaseorders`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List purchase orders |
| GET | `/{id}` | Viewer+ | Get PO by ID |
| POST | `/` | Staff+ | Create PO |
| POST | `/{id}/approve` | Manager+ | Approve PO |
| POST | `/{id}/receive` | Staff+ | Receive goods |

#### Sales Orders (`/salesorders`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Viewer+ | List sales orders |
| GET | `/{id}` | Viewer+ | Get SO by ID |
| POST | `/` | Staff+ | Create SO |
| POST | `/{id}/confirm` | Manager+ | Confirm SO |
| POST | `/{id}/deliver` | Staff+ | Deliver SO |

#### Reports (`/reports`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/stock-summary` | Viewer+ | Stock summary report |
| GET | `/low-stock` | Viewer+ | Low stock report |
| GET | `/expiry` | Viewer+ | Expiry report |
| GET | `/inventory-valuation` | Viewer+ | Valuation report |

#### Notifications (`/notifications`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | Authenticated | List notifications |
| PUT | `/{id}/read` | Authenticated | Mark as read |
| PUT | `/read-all` | Authenticated | Mark all as read |

#### Tenants (`/tenants`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | SuperAdmin | List all tenants |
| GET | `/current` | Authenticated | Get current tenant info |
| PUT | `/current` | TenantAdmin | Update tenant settings |

#### Users (`/users`)
| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/` | TenantAdmin | List users |
| GET | `/{id}` | TenantAdmin | Get user by ID |
| POST | `/` | TenantAdmin | Create user |
| PUT | `/{id}` | TenantAdmin | Update user |
| POST | `/invite` | TenantAdmin | Invite user |

---

## 11. Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=InventorySaaS;...",
    "HangfireConnection": "Server=localhost;Database=InventorySaaS_Hangfire;...",
    "Redis": ""
  },
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!@#$%",
    "Issuer": "InventorySaaS",
    "Audience": "InventorySaaS.Client",
    "ExpiryMinutes": 60
  }
}
```

| Setting | Description | Default |
|---|---|---|
| DefaultConnection | SQL Server connection string | LocalDB |
| Redis | Redis connection (empty = in-memory cache) | Empty |
| JwtSettings.Secret | JWT signing key (min 32 chars) | Demo key |
| JwtSettings.ExpiryMinutes | Token expiration time | 60 minutes |
| AllowedOrigins | CORS allowed origins | localhost:4200 |

### Angular Environment

File: `inventory-saas-web/src/environments/environment.ts`

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5179'  // Change to match your API port
};
```

**Important:** If your backend runs on a different port, update `apiUrl` in `environment.ts` and restart `ng serve`.

### Subscription Plans

| Plan | Monthly | Annual | Max Users | Max Warehouses | Max Products | Advanced Reports | API Access |
|---|---|---|---|---|---|---|---|
| Free | $0 | $0 | 3 | 1 | 100 | No | No |
| Basic | $29.99 | $299.99 | 10 | 3 | 1,000 | No | No |
| Professional | $79.99 | $799.99 | 50 | 10 | 10,000 | Yes | Yes |
| Enterprise | $199.99 | $1,999.99 | Unlimited | Unlimited | Unlimited | Yes | Yes |

---

## 12. Docker Deployment

### Services

| Service | Image | Port | Description |
|---|---|---|---|
| sqlserver | mssql/server:2022 | 1433 | SQL Server database |
| redis | redis:7-alpine | 6379 | Caching layer |
| api | Custom (.NET) | 5000/5001 | Backend API |
| frontend | Custom (Nginx) | 80 | Angular frontend |

### Environment Variables

```bash
# .env file (copy from .env.example)
SA_PASSWORD=YourStrong!Passw0rd
JWT_SECRET=YourProductionSecretKeyThatIsVeryLong!@#$%
```

### Commands

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f api

# Stop all services
docker-compose down

# Rebuild after code changes
docker-compose up -d --build
```

---

## 13. Troubleshooting

### Backend won't start

**Error: "Cannot open database"**
- Ensure SQL Server is running
- Check connection string in `appsettings.json`
- For LocalDB: `Server=(localdb)\mssqllocaldb`
- For SQL Server: `Server=localhost` (Windows Auth) or `Server=localhost;User Id=sa;Password=...`

**Error: "IApplicationDbContext not registered"**
- Ensure `ApplicationDbContext` implements `IApplicationDbContext`
- Check `Infrastructure/DependencyInjection.cs` has the registration

### Frontend won't start

**Error: "NG0908: Angular requires Zone.js"**
- Ensure `zone.js` is imported in `main.ts`: `import 'zone.js';`
- Ensure `provideZoneChangeDetection()` is in `app.config.ts`

**CORS errors in browser console**
- In development, the API allows all localhost origins
- Ensure the backend is running and accessible
- Check that `environment.ts` has the correct API URL

### Login returns 401

- Verify you're using the correct credentials
- Check the API is running and database is seeded
- Try re-running the backend to trigger seeding

### Dashboard returns 500

- Check backend logs for the exact error
- Usually a query issue - ensure database migrations ran
- Check Serilog output in the terminal

### Pages show blank / empty data

- Check browser console (F12) for errors
- Verify the API URL matches in `environment.ts`
- Ensure you're logged in with the right role for the page

### Port conflicts

- Backend: Change in `launchSettings.json` or use `--urls http://localhost:XXXX`
- Frontend: Use `ng serve --port XXXX`
- Update `environment.ts` if the API port changes

---

## Quick Start Checklist

1. [ ] Start SQL Server
2. [ ] Run backend: `dotnet run --project src/InventorySaaS.API`
3. [ ] Run frontend: `cd inventory-saas-web && ng serve`
4. [ ] Open browser to the Angular URL
5. [ ] Login as `admin@demo-company.com` / `Demo@123456`
6. [ ] Explore Dashboard, Products, Inventory, Purchase Orders, Sales Orders
7. [ ] Try creating a product, stocking in, and creating a sales order
