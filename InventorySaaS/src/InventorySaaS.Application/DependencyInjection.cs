using FluentValidation;
using InventorySaaS.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySaaS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // FluentValidation validators (see Features/**/Validators) - invoked explicitly by the
        // Application services that need them, not via MVC auto-validation.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Services (Controller → Service pattern)
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IUnitOfMeasureService, UnitOfMeasureService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImportService, ProductImportService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IPickingService, PickingService>();
        services.AddScoped<IStockCountService, StockCountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISupplierBillService, SupplierBillService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();

        return services;
    }
}
