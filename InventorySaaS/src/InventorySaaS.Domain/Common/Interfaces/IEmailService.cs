namespace InventorySaaS.Domain.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplateAsync(string to, string templateName, Dictionary<string, string> placeholders, CancellationToken ct = default);
}
