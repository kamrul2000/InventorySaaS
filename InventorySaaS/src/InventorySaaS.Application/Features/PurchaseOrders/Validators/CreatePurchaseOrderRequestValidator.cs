using FluentValidation;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;

namespace InventorySaaS.Application.Features.PurchaseOrders.Validators;

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).SetValidator(new CreatePurchaseOrderItemRequestValidator());
    }
}

public class CreatePurchaseOrderItemRequestValidator : AbstractValidator<CreatePurchaseOrderItemRequest>
{
    public CreatePurchaseOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");
        RuleFor(x => x.DiscountRate).InclusiveBetween(0, 100).WithMessage("Discount rate must be between 0 and 100.");
    }
}
