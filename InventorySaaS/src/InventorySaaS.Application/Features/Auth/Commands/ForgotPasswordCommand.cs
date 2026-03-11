using System.Security.Cryptography;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest<Result>;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(IApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Always return success to prevent email enumeration
        if (user is null)
            return Result.Success();

        // Generate reset token
        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(2);

        await _context.SaveChangesAsync(cancellationToken);

        // Send reset email
        var placeholders = new Dictionary<string, string>
        {
            { "FirstName", user.FirstName ?? "User" },
            { "ResetToken", resetToken },
            { "Email", user.Email }
        };

        await _emailService.SendTemplateAsync(
            user.Email,
            "PasswordReset",
            placeholders,
            cancellationToken);

        return Result.Success();
    }
}
