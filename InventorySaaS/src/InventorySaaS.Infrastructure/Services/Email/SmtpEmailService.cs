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

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        // In production, integrate with SMTP, SendGrid, AWS SES, etc.
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);

        // Placeholder: actual SMTP implementation
        // var smtpSettings = _configuration.GetSection("Smtp");
        // using var client = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"]!));
        // client.Credentials = new NetworkCredential(smtpSettings["Username"], smtpSettings["Password"]);
        // client.EnableSsl = true;
        // var message = new MailMessage(smtpSettings["From"]!, to, subject, htmlBody) { IsBodyHtml = true };
        // await client.SendMailAsync(message, ct);

        return Task.CompletedTask;
    }

    public Task SendTemplateAsync(string to, string templateName, Dictionary<string, string> placeholders, CancellationToken ct = default)
    {
        var subject = templateName switch
        {
            "PasswordReset" => "Reset Your Password",
            "UserInvitation" => "You've Been Invited",
            "EmailVerification" => "Verify Your Email",
            _ => templateName
        };

        var body = $"<html><body><p>Template: {templateName}</p>";
        foreach (var (key, value) in placeholders)
        {
            body += $"<p>{key}: {value}</p>";
        }
        body += "</body></html>";

        return SendAsync(to, subject, body, ct);
    }
}
