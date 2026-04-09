using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventorySaaS.Application.Features.Chat.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Infrastructure.Services.AI;

public class AiChatService : IAiChatService
{
    private static readonly HttpClient HttpClient = new();

    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AiChatService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string userMessage,
        List<ChatMessageDto> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            yield return "AI chat is not configured. Please set the Gemini API key in appsettings.json.";
            yield break;
        }

        string inventoryContext;
        try
        {
            inventoryContext = await BuildInventoryContextAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build inventory context");
            inventoryContext = "Unable to load inventory data.";
        }

        var systemPrompt = $"""
            You are an AI inventory assistant called "Inventory Copilot" for a SaaS inventory management system.
            You help users understand their inventory data, identify trends, and make informed decisions.

            Here is the current state of the user's inventory:
            {inventoryContext}

            Guidelines:
            - Respond concisely and clearly
            - Use markdown formatting (bold, lists, tables) when helpful
            - Reference specific product names, SKUs, and numbers from the data
            - If you don't have enough data to answer, say so clearly
            - Only answer questions related to inventory, products, orders, suppliers, customers, and warehouse management
            - For unrelated questions, politely redirect the user to inventory topics
            """;

        // Build Gemini contents array from conversation history
        var contents = new List<object>();
        foreach (var msg in conversationHistory.TakeLast(10))
        {
            var geminiRole = msg.Role == "assistant" ? "model" : "user";
            contents.Add(new { role = geminiRole, parts = new[] { new { text = msg.Content } } });
        }
        contents.Add(new { role = "user", parts = new[] { new { text = userMessage } } });

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { maxOutputTokens = 1024 }
        };

        var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:streamGenerateContent?alt=sse&key={apiKey}";
        var json = JsonSerializer.Serialize(requestBody);

        HttpResponseMessage? response = null;
        string? errorMessage = null;
        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, geminiUrl);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    errorMessage = null;
                    break;
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error {StatusCode} (attempt {Attempt}): {Body}", response.StatusCode, attempt + 1, errorBody);

                // Retry on 429 (rate limit) or 503 (overloaded)
                if ((int)response.StatusCode is 429 or 503 && attempt < maxRetries)
                {
                    var delay = (attempt + 1) * 2000; // 2s, 4s, 6s
                    _logger.LogWarning("Rate limited, retrying in {Delay}ms...", delay);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                errorMessage = $"AI service returned an error (HTTP {(int)response.StatusCode}). Please try again later.";
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Gemini API (attempt {Attempt})", attempt + 1);
                if (attempt < maxRetries)
                {
                    await Task.Delay((attempt + 1) * 2000, cancellationToken);
                    continue;
                }
                errorMessage = "Failed to connect to AI service. Please try again.";
            }
        }

        if (errorMessage != null)
        {
            yield return errorMessage;
            yield break;
        }

        // Stream the Gemini SSE response
        var stream = await response!.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];

            JsonElement parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<JsonElement>(data);
            }
            catch
            {
                continue;
            }

            // Gemini SSE format: { candidates: [{ content: { parts: [{ text: "..." }] } }] }
            if (parsed.TryGetProperty("candidates", out var candidates)
                && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var textEl))
                    {
                        var text = textEl.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return text;
                        }
                    }
                }
            }
        }
    }

    private async Task<string> BuildInventoryContextAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();

        var productCount = await _context.Products.CountAsync(ct);
        var warehouseCount = await _context.Warehouses.CountAsync(ct);
        var supplierCount = await _context.Suppliers.CountAsync(ct);
        var customerCount = await _context.Customers.CountAsync(ct);

        sb.AppendLine("## Overview");
        sb.AppendLine($"- Total Products: {productCount}");
        sb.AppendLine($"- Total Warehouses: {warehouseCount}");
        sb.AppendLine($"- Total Suppliers: {supplierCount}");
        sb.AppendLine($"- Total Customers: {customerCount}");

        var balances = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0)
            .Select(ib => new { ib.QuantityOnHand, ib.UnitCost })
            .ToListAsync(ct);
        var totalValue = balances.Sum(b => (decimal)b.QuantityOnHand * b.UnitCost);
        sb.AppendLine($"- Total Inventory Value: ${totalValue:N2}");

        var totalSales = await _context.SalesOrders
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled)
            .SumAsync(so => so.TotalAmount, ct);
        sb.AppendLine($"- Total Sales Revenue: ${totalSales:N2}");

        var totalPurchases = await _context.PurchaseOrders
            .Where(po => po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled)
            .SumAsync(po => po.TotalAmount, ct);
        sb.AppendLine($"- Total Purchase Spend: ${totalPurchases:N2}");

        var lowStock = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0 && ib.QuantityOnHand <= ib.Product.ReorderLevel)
            .OrderBy(ib => ib.QuantityOnHand)
            .Take(10)
            .Select(ib => new { ib.Product.Name, ib.Product.Sku, ib.QuantityOnHand, ib.Product.ReorderLevel, Warehouse = ib.Warehouse.Name })
            .ToListAsync(ct);

        if (lowStock.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Low Stock Items");
            foreach (var item in lowStock)
                sb.AppendLine($"- {item.Name} (SKU: {item.Sku}) — {item.QuantityOnHand} on hand, reorder at {item.ReorderLevel}, in {item.Warehouse}");
        }

        var topProducts = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0)
            .GroupBy(ib => new { ib.Product.Name, ib.Product.Sku, ib.Product.SellingPrice })
            .Select(g => new { g.Key.Name, g.Key.Sku, g.Key.SellingPrice, TotalQty = g.Sum(x => x.QuantityOnHand) })
            .OrderByDescending(x => x.TotalQty)
            .Take(5)
            .ToListAsync(ct);

        if (topProducts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Top Products by Stock");
            foreach (var p in topProducts)
                sb.AppendLine($"- {p.Name} (SKU: {p.Sku}) — {p.TotalQty} units, selling at ${p.SellingPrice:N2}");
        }

        var recentTx = await _context.InventoryTransactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .Select(t => new { t.TransactionNumber, Type = t.TransactionType.ToString(), t.Product.Name, t.Quantity, t.TransactionDate })
            .ToListAsync(ct);

        if (recentTx.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recent Transactions");
            foreach (var tx in recentTx)
                sb.AppendLine($"- {tx.TransactionNumber}: {tx.Type} — {tx.Name} x{tx.Quantity} on {tx.TransactionDate:yyyy-MM-dd}");
        }

        var recentSO = await _context.SalesOrders
            .OrderByDescending(so => so.OrderDate)
            .Take(5)
            .Select(so => new { so.OrderNumber, Customer = so.Customer.Name, Status = so.Status.ToString(), so.TotalAmount, so.OrderDate })
            .ToListAsync(ct);

        if (recentSO.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recent Sales Orders");
            foreach (var so in recentSO)
                sb.AppendLine($"- {so.OrderNumber}: {so.Customer} — ${so.TotalAmount:N2} ({so.Status}) on {so.OrderDate:yyyy-MM-dd}");
        }

        var recentPO = await _context.PurchaseOrders
            .OrderByDescending(po => po.OrderDate)
            .Take(5)
            .Select(po => new { po.OrderNumber, Supplier = po.Supplier.Name, Status = po.Status.ToString(), po.TotalAmount, po.OrderDate })
            .ToListAsync(ct);

        if (recentPO.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recent Purchase Orders");
            foreach (var po in recentPO)
                sb.AppendLine($"- {po.OrderNumber}: {po.Supplier} — ${po.TotalAmount:N2} ({po.Status}) on {po.OrderDate:yyyy-MM-dd}");
        }

        return sb.ToString();
    }
}
