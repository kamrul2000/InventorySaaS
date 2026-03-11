using AutoMapper;
using InventorySaaS.Application.Features.Auth.DTOs;
using InventorySaaS.Application.Features.Categories.DTOs;
using InventorySaaS.Application.Features.Customers.DTOs;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Features.Notifications.DTOs;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Features.Suppliers.DTOs;
using InventorySaaS.Application.Features.Tenants.DTOs;
using InventorySaaS.Application.Features.Users.DTOs;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Notification;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Entities.Tenant;
using InventorySaaS.Domain.Entities.Warehouse;

namespace InventorySaaS.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Tenant
        CreateMap<TenantInfo, TenantDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.PlanName, opt => opt.MapFrom(s => s.SubscriptionPlan.Name));

        // Users
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(d => d.Roles, opt => opt.MapFrom(s =>
                s.UserRoles.Select(ur => ur.Role.Name).ToList()));

        // Products
        CreateMap<ProductInfo, ProductDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand != null ? s.Brand.Name : null))
            .ForMember(d => d.UnitName, opt => opt.MapFrom(s => s.UnitOfMeasure.Name));

        // Category
        CreateMap<Category, CategoryDto>()
            .ForMember(d => d.ProductCount, opt => opt.MapFrom(s => s.Products.Count));

        // Brand
        CreateMap<Brand, BrandDto>();

        // UnitOfMeasure
        CreateMap<UnitOfMeasure, UnitOfMeasureDto>();

        // Warehouse
        CreateMap<WarehouseInfo, WarehouseDto>()
            .ForMember(d => d.LocationCount, opt => opt.MapFrom(s => s.Locations.Count));

        CreateMap<WarehouseLocation, WarehouseLocationDto>();

        // Inventory
        CreateMap<InventoryBalance, InventoryBalanceDto>()
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, opt => opt.MapFrom(s => s.Product.Sku))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.LocationName, opt => opt.MapFrom(s => s.Location != null ? s.Location.Name : null))
            .ForMember(d => d.QuantityAvailable, opt => opt.MapFrom(s => s.QuantityAvailable));

        CreateMap<InventoryTransaction, InventoryTransactionDto>()
            .ForMember(d => d.TransactionType, opt => opt.MapFrom(s => s.TransactionType.ToString()))
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, opt => opt.MapFrom(s => s.Product.Sku))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name));

        // Supplier
        CreateMap<SupplierInfo, SupplierDto>();

        // Customer
        CreateMap<CustomerInfo, CustomerDto>();

        // Purchase Order
        CreateMap<PurchaseOrder, PurchaseOrderDto>()
            .ForMember(d => d.SupplierName, opt => opt.MapFrom(s => s.Supplier.Name))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Items));

        CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, opt => opt.MapFrom(s => s.Product.Sku));

        // Sales Order
        CreateMap<SalesOrder, SalesOrderDto>()
            .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.Name))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Items));

        CreateMap<SalesOrderItem, SalesOrderItemDto>()
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, opt => opt.MapFrom(s => s.Product.Sku));

        // Notification
        CreateMap<NotificationInfo, NotificationDto>()
            .ForMember(d => d.Type, opt => opt.MapFrom(s => s.Type.ToString()));
    }
}
