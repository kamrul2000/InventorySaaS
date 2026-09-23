using FluentValidation;
using InventorySaaS.Application.Features.Auth.DTOs;

namespace InventorySaaS.Application.Features.Auth.Validators;

// Shared password-strength rule: at least 8 characters, at least one letter and one digit.
// Matches (and enforces server-side) the frontend's Validators.minLength(8) on the register form.
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> MustBeAStrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
}

public class RegisterTenantRequestValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.AdminPassword).MustBeAStrongPassword();
        RuleFor(x => x.AdminFirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdminLastName).NotEmpty().MaximumLength(100);
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).MustBeAStrongPassword();
    }
}
