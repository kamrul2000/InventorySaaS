using System.Net;
using System.Net.Mail;
using InventorySaaS.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Infrastructure.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = _configuration.GetSection("Smtp");
        var host = smtp["Host"];
        var username = smtp["Username"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
        {
            // No real SMTP provider is configured (the default in dev/test - appsettings.json
            // ships empty Username/Password). Rather than silently discarding the email like
            // before, log its full contents at a visible level so password-reset and invite
            // flows stay testable end-to-end without a real mail server (AUTH-05).
            _logger.LogWarning(
                "SMTP is not configured - email NOT actually sent. To: {To}\nSubject: {Subject}\n{Body}",
                to, subject, htmlBody);
            return;
        }

        try
        {
            var port = int.TryParse(smtp["Port"], out var parsedPort) ? parsedPort : 587;
            var from = smtp["From"] ?? "noreply@inventorysaas.com";

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, smtp["Password"]),
                EnableSsl = true
            };
            using var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };

            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Outbound mail being unavailable shouldn't fail the operation that triggered it
            // (registration, invite, password reset already committed) - log it so it's visible
            // and actionable instead of surfacing as an unrelated 500 to the caller.
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }

    public Task SendTemplateAsync(string to, string templateName, Dictionary<string, string> placeholders, CancellationToken ct = default)
    {
        var subject = templateName switch
        {
            "PasswordReset" => "Reset Your Password - InApp",
            "UserInvitation" => "You've Been Invited to InApp",
            "EmailVerification" => "Verify Your Email - InApp",
            _ => templateName
        };

        var body = templateName switch
        {
            "PasswordReset" => BuildPasswordResetEmail(placeholders),
            "UserInvitation" => BuildUserInvitationEmail(placeholders),
            _ => BuildGenericEmail(templateName, string.Join("<br/>", placeholders.Select(p => $"{p.Key}: {p.Value}")))
        };

        return SendAsync(to, subject, body, ct);
    }

    private string BuildPasswordResetEmail(Dictionary<string, string> placeholders)
    {
        var firstName = placeholders.GetValueOrDefault("FirstName", "User");
        var token = placeholders.GetValueOrDefault("ResetToken", "");
        var email = placeholders.GetValueOrDefault("Email", "");
        var frontendUrl = _configuration["AllowedOrigins:0"] ?? "http://localhost:4200";
        var resetLink = $"{frontendUrl}/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f5f5f5;font-family:Inter,Segoe UI,-apple-system,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5;padding:40px 0;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;box-shadow:0 2px 8px rgba(0,0,0,0.06);overflow:hidden;'>
        <!-- Header -->
        <tr><td style='background:#e8602c;padding:24px 32px;text-align:center;'>
          <span style='font-size:22px;font-weight:700;color:#fff;letter-spacing:-0.5px;'>In<span style='color:#fff;'>App</span></span>
          <br/><span style='font-size:11px;color:rgba(255,255,255,0.8);letter-spacing:0.5px;'>Inventory App</span>
        </td></tr>
        <!-- Body -->
        <tr><td style='padding:32px;'>
          <h1 style='font-size:20px;font-weight:600;color:#1a202c;margin:0 0 12px;'>Reset your password</h1>
          <p style='font-size:14px;color:#718096;line-height:1.6;margin:0 0 24px;'>
            Hi {firstName},<br/><br/>
            We received a request to reset your password. Click the button below to choose a new one. This link will expire in <strong>2 hours</strong>.
          </p>
          <table width='100%' cellpadding='0' cellspacing='0'>
            <tr><td align='center'>
              <a href='{resetLink}' style='display:inline-block;padding:13px 32px;font-size:15px;font-weight:600;color:#fff;background:#e8602c;text-decoration:none;border-radius:8px;'>
                Reset Password
              </a>
            </td></tr>
          </table>
          <p style='font-size:12px;color:#a0aec0;line-height:1.5;margin:24px 0 0;'>
            If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.
          </p>
          <hr style='border:none;border-top:1px solid #e2e8f0;margin:24px 0 16px;'/>
          <p style='font-size:11px;color:#a0aec0;margin:0;'>If the button doesn't work, copy and paste this URL into your browser:<br/>
            <a href='{resetLink}' style='color:#e8602c;word-break:break-all;'>{resetLink}</a>
          </p>
        </td></tr>
        <!-- Footer -->
        <tr><td style='background:#f7fafc;padding:16px 32px;text-align:center;'>
          <p style='font-size:11px;color:#a0aec0;margin:0;'>InApp Inventory Management &copy; {DateTime.UtcNow.Year}</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }

    private string BuildUserInvitationEmail(Dictionary<string, string> placeholders)
    {
        var firstName = placeholders.GetValueOrDefault("FirstName", "there");
        var email = placeholders.GetValueOrDefault("Email", "");
        var tempPassword = placeholders.GetValueOrDefault("TempPassword", "");
        var frontendUrl = _configuration["AllowedOrigins:0"] ?? "http://localhost:4200";
        var loginLink = $"{frontendUrl}/auth/login";

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f5f5f5;font-family:Inter,Segoe UI,-apple-system,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5;padding:40px 0;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;box-shadow:0 2px 8px rgba(0,0,0,0.06);overflow:hidden;'>
        <tr><td style='background:#e8602c;padding:24px 32px;text-align:center;'>
          <span style='font-size:22px;font-weight:700;color:#fff;letter-spacing:-0.5px;'>In<span style='color:#fff;'>App</span></span>
          <br/><span style='font-size:11px;color:rgba(255,255,255,0.8);letter-spacing:0.5px;'>Inventory App</span>
        </td></tr>
        <tr><td style='padding:32px;'>
          <h1 style='font-size:20px;font-weight:600;color:#1a202c;margin:0 0 12px;'>You've been invited</h1>
          <p style='font-size:14px;color:#718096;line-height:1.6;margin:0 0 16px;'>
            Hi {firstName},<br/><br/>
            You've been invited to join your team's InApp Inventory workspace. Sign in with the temporary credentials below, then change your password.
          </p>
          <table width='100%' cellpadding='0' cellspacing='0' style='background:#f7fafc;border-radius:8px;margin:0 0 24px;'>
            <tr><td style='padding:16px 20px;'>
              <p style='font-size:13px;color:#4a5568;margin:0 0 4px;'><strong>Email:</strong> {email}</p>
              <p style='font-size:13px;color:#4a5568;margin:0;'><strong>Temporary password:</strong> {tempPassword}</p>
            </td></tr>
          </table>
          <table width='100%' cellpadding='0' cellspacing='0'>
            <tr><td align='center'>
              <a href='{loginLink}' style='display:inline-block;padding:13px 32px;font-size:15px;font-weight:600;color:#fff;background:#e8602c;text-decoration:none;border-radius:8px;'>
                Sign In
              </a>
            </td></tr>
          </table>
        </td></tr>
        <tr><td style='background:#f7fafc;padding:16px 32px;text-align:center;'>
          <p style='font-size:11px;color:#a0aec0;margin:0;'>InApp Inventory Management &copy; {DateTime.UtcNow.Year}</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }

    private static string BuildGenericEmail(string title, string bodyContent)
    {
        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body style='margin:0;padding:0;background:#f5f5f5;font-family:Inter,Segoe UI,-apple-system,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5;padding:40px 0;'>
    <tr><td align='center'>
      <table width='480' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:12px;padding:32px;'>
        <tr><td>
          <h1 style='font-size:20px;color:#1a202c;margin:0 0 16px;'>{title}</h1>
          <p style='font-size:14px;color:#718096;line-height:1.6;'>{bodyContent}</p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }
}
