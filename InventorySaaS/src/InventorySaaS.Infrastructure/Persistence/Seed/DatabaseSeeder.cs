using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Tenant;
using InventorySaaS.Infrastructure.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Infrastructure.Persistence.Seed;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ApplicationDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await _db.Database.MigrateAsync();

        await SeedSubscriptionPlansAsync();
        await SeedRolesAsync();
        await SeedPermissionsAsync();
        await SeedSuperAdminAsync();
        await SeedDemoTenantAsync();

        _logger.LogInformation("Database seeding completed.");
    }

    private async Task SeedSubscriptionPlansAsync()
    {
        if (await _db.SubscriptionPlans.AnyAsync()) return;

        var plans = new List<SubscriptionPlan>
        {
            new()
            {
                Name = "Free",
                PlanType = SubscriptionPlanType.Free,
                Description = "Basic inventory management for small businesses",
                MonthlyPrice = 0,
                AnnualPrice = 0,
                MaxUsers = 3,
                MaxWarehouses = 1,
                MaxProducts = 100,
                HasAdvancedReporting = false,
                HasApiAccess = false
            },
            new()
            {
                Name = "Basic",
                PlanType = SubscriptionPlanType.Basic,
                Description = "Essential features for growing businesses",
                MonthlyPrice = 29.99m,
                AnnualPrice = 299.99m,
                MaxUsers = 10,
                MaxWarehouses = 3,
                MaxProducts = 1000,
                HasAdvancedReporting = false,
                HasApiAccess = false
            },
            new()
            {
                Name = "Professional",
                PlanType = SubscriptionPlanType.Professional,
                Description = "Advanced features for established businesses",
                MonthlyPrice = 79.99m,
                AnnualPrice = 799.99m,
                MaxUsers = 50,
                MaxWarehouses = 10,
                MaxProducts = 10000,
                HasAdvancedReporting = true,
                HasApiAccess = true
            },
            new()
            {
                Name = "Enterprise",
                PlanType = SubscriptionPlanType.Enterprise,
                Description = "Unlimited features for large organizations",
                MonthlyPrice = 199.99m,
                AnnualPrice = 1999.99m,
                MaxUsers = int.MaxValue,
                MaxWarehouses = int.MaxValue,
                MaxProducts = int.MaxValue,
                HasAdvancedReporting = true,
                HasApiAccess = true
            }
        };

        _db.SubscriptionPlans.AddRange(plans);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Subscription plans seeded.");
    }

    private async Task SeedRolesAsync()
    {
        if (await _db.Roles.AnyAsync()) return;

        var roles = AppRoles.All.Select(r => new Role
        {
            Name = r,
            NormalizedName = r.ToUpperInvariant(),
            Description = r switch
            {
                AppRoles.SuperAdmin => "System-wide administrator with access to all tenants",
                AppRoles.TenantAdmin => "Tenant administrator with full access within their tenant",
                AppRoles.Manager => "Manager with access to most features within tenant",
                AppRoles.Staff => "Staff member with limited operational access",
                AppRoles.Viewer => "Read-only access to tenant data",
                _ => r
            },
            IsSystemRole = true
        });

        _db.Roles.AddRange(roles);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Roles seeded.");
    }

    private async Task SeedPermissionsAsync()
    {
        if (await _db.Permissions.AnyAsync()) return;

        var modules = new Dictionary<string, string[]>
        {
            ["Products"] = ["View", "Create", "Edit", "Delete"],
            ["Categories"] = ["View", "Create", "Edit", "Delete"],
            ["Warehouses"] = ["View", "Create", "Edit", "Delete"],
            ["Inventory"] = ["View", "StockIn", "StockOut", "Transfer", "Adjust"],
            ["Suppliers"] = ["View", "Create", "Edit", "Delete"],
            ["Customers"] = ["View", "Create", "Edit", "Delete"],
            ["PurchaseOrders"] = ["View", "Create", "Edit", "Approve", "Receive"],
            ["SalesOrders"] = ["View", "Create", "Edit", "Confirm", "Deliver"],
            ["Reports"] = ["View", "Export"],
            ["Users"] = ["View", "Create", "Edit", "Delete", "InviteUser"],
            ["Settings"] = ["View", "Edit"],
            ["AuditLogs"] = ["View"]
        };

        var permissions = new List<Permission>();
        foreach (var (module, actions) in modules)
        {
            foreach (var action in actions)
            {
                permissions.Add(new Permission
                {
                    Name = $"{module}.{action}",
                    Module = module,
                    Description = $"{action} permission for {module}"
                });
            }
        }

        _db.Permissions.AddRange(permissions);
        await _db.SaveChangesAsync();

        // Assign all permissions to TenantAdmin role
        var tenantAdminRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.TenantAdmin.ToUpperInvariant());
        var allPermissions = await _db.Permissions.ToListAsync();
        foreach (var permission in allPermissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = tenantAdminRole.Id,
                PermissionId = permission.Id
            });
        }

        // Assign limited permissions to Manager
        var managerRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.Manager.ToUpperInvariant());
        var managerPermissions = allPermissions.Where(p =>
            p.Module != "Users" || p.Name == "Users.View");
        foreach (var permission in managerPermissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = managerRole.Id,
                PermissionId = permission.Id
            });
        }

        // Assign view-only permissions to Viewer
        var viewerRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.Viewer.ToUpperInvariant());
        var viewPermissions = allPermissions.Where(p => p.Name.EndsWith(".View"));
        foreach (var permission in viewPermissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = viewerRole.Id,
                PermissionId = permission.Id
            });
        }

        // Assign operational permissions to Staff
        var staffRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.Staff.ToUpperInvariant());
        var staffModules = new[] { "Products", "Categories", "Warehouses", "Inventory", "Suppliers", "Customers", "PurchaseOrders", "SalesOrders" };
        var staffPermissions = allPermissions.Where(p => staffModules.Contains(p.Module));
        foreach (var permission in staffPermissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = staffRole.Id,
                PermissionId = permission.Id
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Permissions seeded and assigned to roles.");
    }

    private async Task SeedSuperAdminAsync()
    {
        var superAdminEmail = "superadmin@inventorysaas.com";
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == superAdminEmail.ToUpperInvariant())) return;

        var superAdmin = new ApplicationUser
        {
            Email = superAdminEmail,
            NormalizedEmail = superAdminEmail.ToUpperInvariant(),
            FirstName = "Super",
            LastName = "Admin",
            PasswordHash = PasswordHasher.Hash("Admin@123456"),
            IsActive = true,
            EmailConfirmed = true
        };

        _db.Users.Add(superAdmin);
        await _db.SaveChangesAsync();

        var superAdminRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.SuperAdmin.ToUpperInvariant());
        _db.UserRoles.Add(new UserRole { UserId = superAdmin.Id, RoleId = superAdminRole.Id });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Super Admin seeded: {Email}", superAdminEmail);
    }

    private async Task SeedDemoTenantAsync()
    {
        if (await _db.Tenants.AnyAsync()) return;

        var freePlan = await _db.SubscriptionPlans.FirstAsync(p => p.PlanType == SubscriptionPlanType.Professional);

        var tenant = new TenantInfo
        {
            Name = "Demo Company",
            Slug = "demo-company",
            Subdomain = "demo",
            ContactEmail = "admin@demo-company.com",
            Currency = "USD",
            Timezone = "UTC",
            Status = TenantStatus.Active,
            SubscriptionPlanId = freePlan.Id,
            SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        // Create tenant admin
        var tenantAdmin = new ApplicationUser
        {
            Email = "admin@demo-company.com",
            NormalizedEmail = "admin@demo-company.com".ToUpperInvariant(),
            FirstName = "Demo",
            LastName = "Admin",
            PasswordHash = PasswordHasher.Hash("Demo@123456"),
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenant.Id
        };

        _db.Users.Add(tenantAdmin);
        await _db.SaveChangesAsync();

        var tenantAdminRole = await _db.Roles.FirstAsync(r => r.NormalizedName == AppRoles.TenantAdmin.ToUpperInvariant());
        _db.UserRoles.Add(new UserRole { UserId = tenantAdmin.Id, RoleId = tenantAdminRole.Id });
        await _db.SaveChangesAsync();

        // Seed demo data for tenant
        await SeedDemoProductDataAsync(tenant.Id);

        _logger.LogInformation("Demo tenant seeded with ID: {TenantId}", tenant.Id);
    }

    private async Task SeedDemoProductDataAsync(Guid tenantId)
    {
        // Categories
        var electronics = new Domain.Entities.Product.Category { TenantId = tenantId, Name = "Electronics", Description = "Electronic products and components" };
        var clothing = new Domain.Entities.Product.Category { TenantId = tenantId, Name = "Clothing", Description = "Apparel and accessories" };
        var food = new Domain.Entities.Product.Category { TenantId = tenantId, Name = "Food & Beverage", Description = "Food and drink products" };
        _db.Categories.AddRange(electronics, clothing, food);

        // Brands
        var brand1 = new Domain.Entities.Product.Brand { TenantId = tenantId, Name = "TechBrand" };
        var brand2 = new Domain.Entities.Product.Brand { TenantId = tenantId, Name = "FashionCo" };
        _db.Brands.AddRange(brand1, brand2);

        // Units
        var piece = new Domain.Entities.Product.UnitOfMeasure { TenantId = tenantId, Name = "Piece", Abbreviation = "pcs" };
        var kg = new Domain.Entities.Product.UnitOfMeasure { TenantId = tenantId, Name = "Kilogram", Abbreviation = "kg" };
        var box = new Domain.Entities.Product.UnitOfMeasure { TenantId = tenantId, Name = "Box", Abbreviation = "box" };
        _db.UnitsOfMeasure.AddRange(piece, kg, box);

        await _db.SaveChangesAsync();

        // Products
        var products = new List<Domain.Entities.Product.ProductInfo>
        {
            new() { TenantId = tenantId, Name = "Wireless Mouse", Sku = "ELEC-001", CategoryId = electronics.Id, BrandId = brand1.Id, UnitOfMeasureId = piece.Id, CostPrice = 15.00m, SellingPrice = 29.99m, ReorderLevel = 50, Barcode = "1234567890123" },
            new() { TenantId = tenantId, Name = "USB-C Hub", Sku = "ELEC-002", CategoryId = electronics.Id, BrandId = brand1.Id, UnitOfMeasureId = piece.Id, CostPrice = 25.00m, SellingPrice = 49.99m, ReorderLevel = 30 },
            new() { TenantId = tenantId, Name = "Mechanical Keyboard", Sku = "ELEC-003", CategoryId = electronics.Id, BrandId = brand1.Id, UnitOfMeasureId = piece.Id, CostPrice = 45.00m, SellingPrice = 89.99m, ReorderLevel = 20 },
            new() { TenantId = tenantId, Name = "Cotton T-Shirt", Sku = "CLTH-001", CategoryId = clothing.Id, BrandId = brand2.Id, UnitOfMeasureId = piece.Id, CostPrice = 8.00m, SellingPrice = 24.99m, ReorderLevel = 100, HasVariants = true },
            new() { TenantId = tenantId, Name = "Premium Coffee Beans", Sku = "FOOD-001", CategoryId = food.Id, UnitOfMeasureId = kg.Id, CostPrice = 12.00m, SellingPrice = 22.99m, ReorderLevel = 25, TrackExpiry = true },
        };
        _db.Products.AddRange(products);
        await _db.SaveChangesAsync();

        // Warehouses
        var mainWarehouse = new Domain.Entities.Warehouse.WarehouseInfo
        {
            TenantId = tenantId, Name = "Main Warehouse", Code = "WH-MAIN",
            Address = "123 Industrial Blvd", City = "New York", Country = "US", IsDefault = true
        };
        var secondaryWarehouse = new Domain.Entities.Warehouse.WarehouseInfo
        {
            TenantId = tenantId, Name = "Secondary Warehouse", Code = "WH-SEC",
            Address = "456 Commerce St", City = "Los Angeles", Country = "US"
        };
        _db.Warehouses.AddRange(mainWarehouse, secondaryWarehouse);
        await _db.SaveChangesAsync();

        // Locations
        var locations = new List<Domain.Entities.Warehouse.WarehouseLocation>
        {
            new() { TenantId = tenantId, WarehouseId = mainWarehouse.Id, Name = "A1-R1-B1", Aisle = "A1", Rack = "R1", Bin = "B1" },
            new() { TenantId = tenantId, WarehouseId = mainWarehouse.Id, Name = "A1-R1-B2", Aisle = "A1", Rack = "R1", Bin = "B2" },
            new() { TenantId = tenantId, WarehouseId = mainWarehouse.Id, Name = "A2-R1-B1", Aisle = "A2", Rack = "R1", Bin = "B1" },
        };
        _db.WarehouseLocations.AddRange(locations);
        await _db.SaveChangesAsync();

        // Inventory Balances
        foreach (var product in products)
        {
            _db.InventoryBalances.Add(new Domain.Entities.Inventory.InventoryBalance
            {
                TenantId = tenantId,
                ProductId = product.Id,
                WarehouseId = mainWarehouse.Id,
                QuantityOnHand = Random.Shared.Next(10, 200),
                UnitCost = product.CostPrice,
                ExpiryDate = product.TrackExpiry ? DateTime.UtcNow.AddMonths(Random.Shared.Next(1, 12)) : null,
                BatchNumber = product.TrackExpiry ? $"BATCH-{DateTime.UtcNow:yyyyMM}-001" : null
            });
        }
        await _db.SaveChangesAsync();

        // Suppliers
        var suppliers = new List<Domain.Entities.Supplier.SupplierInfo>
        {
            new() { TenantId = tenantId, Name = "Global Tech Supplies", Code = "SUP-001", Email = "orders@globaltech.com", Phone = "+1-555-0101", City = "Shenzhen", Country = "CN", PaymentTerms = "Net 30" },
            new() { TenantId = tenantId, Name = "Fashion Wholesale Inc", Code = "SUP-002", Email = "sales@fashionwholesale.com", Phone = "+1-555-0102", City = "Mumbai", Country = "IN", PaymentTerms = "Net 45" },
        };
        _db.Suppliers.AddRange(suppliers);

        // Customers
        var customers = new List<Domain.Entities.Customer.CustomerInfo>
        {
            new() { TenantId = tenantId, Name = "Retail Store Alpha", Code = "CUS-001", CustomerType = "Retail", Email = "purchasing@alpha.com", Phone = "+1-555-0201", City = "Chicago", Country = "US" },
            new() { TenantId = tenantId, Name = "Online Shop Beta", Code = "CUS-002", CustomerType = "Wholesale", Email = "orders@betashop.com", Phone = "+1-555-0202", City = "Seattle", Country = "US" },
        };
        _db.Customers.AddRange(customers);

        await _db.SaveChangesAsync();
    }
}
