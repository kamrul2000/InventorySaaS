namespace InventorySaaS.Application.Features.PurchaseOrders.DTOs;

public record PurchaseOrderDto(
    Guid Id,
    string OrderNumber,
    string SupplierName,
    string WarehouseName,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    string Status,
    decimal TotalAmount,
    List<PurchaseOrderItemDto> Items);

public record PurchaseOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    int ReceivedQuantity,
    int ReturnedQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid WarehouseId,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    List<CreatePurchaseOrderItemRequest> Items);

public record CreatePurchaseOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate);

public record ReceiveGoodsRequest(
    Guid PurchaseOrderId,
    List<ReceiveGoodsItemRequest> Items,
    string? Notes);

public record ReceiveGoodsItemRequest(
    Guid ProductId,
    int Quantity,
    int RejectedQuantity,
    Guid? LocationId,
    string? BatchNumber,
    string? LotNumber,
    DateTime? ExpiryDate);

public record ReturnPurchaseOrderRequest(
    Guid PurchaseOrderId,
    List<ReturnPurchaseOrderItemRequest> Items,
    string? Reason);

public record ReturnPurchaseOrderItemRequest(
    Guid ProductId,
    int Quantity,
    string? Reason);
