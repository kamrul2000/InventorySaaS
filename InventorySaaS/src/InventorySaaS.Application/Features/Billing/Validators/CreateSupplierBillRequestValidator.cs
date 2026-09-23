using FluentValidation;
using InventorySaaS.Application.Features.Billing.DTOs;

namespace InventorySaaS.Application.Features.Billing.Validators;

public class CreateSupplierBillRequestValidator : AbstractValidator<CreateSupplierBillRequest>
{
    public CreateSupplierBillRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).SetValidator(new CreateSupplierBillItemRequestValidator());
    }
}

public class CreateSupplierBillItemRequestValidator : AbstractValidator<CreateSupplierBillItemRequest>
{
    public CreateSupplierBillItemRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("A line description is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");
        RuleFor(x => x.DiscountRate).InclusiveBetween(0, 100).WithMessage("Discount rate must be between 0 and 100.");
    }
}
