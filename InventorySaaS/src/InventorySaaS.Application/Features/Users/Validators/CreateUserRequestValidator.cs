using FluentValidation;
using InventorySaaS.Application.Features.Auth.Validators;
using InventorySaaS.Application.Features.Users.DTOs;

namespace InventorySaaS.Application.Features.Users.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).MustBeAStrongPassword();
    }
}
