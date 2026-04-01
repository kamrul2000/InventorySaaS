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
