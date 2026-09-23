using FluentValidation;
using InventorySaaS.Application.Features.SalesOrders.DTOs;

namespace InventorySaaS.Application.Features.SalesOrders.Validators;

public class CreateSalesOrderRequestValidator : AbstractValidator<CreateSalesOrderRequest>
{
    public CreateSalesOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).SetValidator(new CreateSalesOrderItemRequestValidator());
    }
}

public class CreateSalesOrderItemRequestValidator : AbstractValidator<CreateSalesOrderItemRequest>
{
    public CreateSalesOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");
        RuleFor(x => x.DiscountRate).InclusiveBetween(0, 100).WithMessage("Discount rate must be between 0 and 100.");
    }
}
