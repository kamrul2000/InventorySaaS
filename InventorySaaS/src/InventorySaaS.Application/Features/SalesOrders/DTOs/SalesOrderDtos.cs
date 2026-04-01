namespace InventorySaaS.Application.Features.SalesOrders.DTOs;

public record SalesOrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string WarehouseName,
    DateTime OrderDate,
    DateTime? DeliveryDate,
    string Status,
    decimal TotalAmount,
    List<SalesOrderItemDto> Items);

public record SalesOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    int DeliveredQuantity,
    int ReturnedQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public record CreateSalesOrderRequest(
    Guid CustomerId,
    Guid WarehouseId,
    DateTime? DeliveryDate,
    string? ShippingAddress,
    string? Notes,
    List<CreateSalesOrderItemRequest> Items);

public record CreateSalesOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate);

public record DeliverSalesOrderRequest(
    Guid SalesOrderId,
    List<DeliverSalesOrderItemRequest> Items,
    string? Notes);

public record DeliverSalesOrderItemRequest(
    Guid ProductId,
    int Quantity);

public record ReturnSalesOrderRequest(
    Guid SalesOrderId,
    List<ReturnSalesOrderItemRequest> Items,
    string? Reason);

public record ReturnSalesOrderItemRequest(
    Guid ProductId,
    int Quantity,
    string? Reason);
