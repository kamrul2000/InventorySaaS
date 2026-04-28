using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Infrastructure.Services.AI;

public class GeminiProductExtractionService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GeminiProductExtractionService> logger) : IProductExtractionService
{
    private const string Model = "gemini-2.5-flash-lite";
    private const string EndpointTemplate =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

    // Tune this string to improve extraction quality — keep the JSON schema in sync with ProductExtractionResult.
    private const string ExtractionPrompt = """
        You are an expert product cataloger for a multi-tenant inventory management system.
        Analyze the attached product image (label, packaging, or shelf photo) and extract
        structured product information.

        Return ONLY a single valid JSON object with this exact shape — no markdown, no code
        fences, no commentary, no leading or trailing text:

        {
          "name": string | null,                  // Product name as printed on the packaging. Strip generic marketing taglines.
          "description": string | null,           // 1-2 sentence inventory-catalog description of what the product is.
          "brandName": string | null,             // Brand or manufacturer if clearly visible.
          "unitName": string | null,              // Unit of measure if visible: e.g. "kg", "g", "litre", "ml", "piece", "pack", "bottle".
          "barcode": string | null,               // Barcode digits if a barcode is clearly readable. Otherwise null.
          "suggestedCategory": string | null,     // General category name, e.g. "Beverages", "Snacks", "Cleaning Supplies", "Electronics", "Personal Care".
          "suggestedSellingPrice": number | null, // Retail price if printed on the package. Plain number, no currency symbol.
          "suggestedCostPrice": number | null,    // Cost / wholesale price if visible. Almost always null.
          "trackExpiry": boolean,                 // true if you can see an expiry / best-before date OR the product type typically has one (food, medicine, cosmetics, dairy). Otherwise false.
          "notes": string | null                  // Optional hint for the user: size info, visible expiry date, "low confidence: image is blurry", etc.
        }

        Rules:
        - Use null for any field you are not confident about. Do not guess.
        - Numeric fields must be plain JSON numbers (not strings) and must omit currency symbols, commas, or units.
        - Do not invent a barcode if you cannot read one.
        - If the image does not show a product, return all nullable string/number fields as null,
          set "trackExpiry": false, and put a short reason in "notes".
        """;

    public async Task<ProductExtractionResult> ExtractFromImageAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured. Set Gemini:ApiKey via user secrets or environment variables.");
        }

        // Buffer to base64 — Gemini's REST API expects inline_data as base64.
        await using var memory = new MemoryStream();
        await imageStream.CopyToAsync(memory, cancellationToken);
        var base64Image = Convert.ToBase64String(memory.ToArray());

        var requestBody = new GeminiRequest(
            Contents:
            [
                new GeminiContent(
                [
                    new GeminiPart { Text = ExtractionPrompt },
                    new GeminiPart { InlineData = new GeminiInlineData(mimeType, base64Image) }
                ])
            ],
            GenerationConfig: new GeminiGenerationConfig(
                ResponseMimeType: "application/json",
                Temperature: 0.2,
                MaxOutputTokens: 1024));

        var url = string.Format(EndpointTemplate, Model, Uri.EscapeDataString(apiKey));

        logger.LogInformation(
            "Calling Gemini extraction endpoint (model={Model}, mimeType={MimeType}, imageBytes={Bytes})",
            Model, mimeType, memory.Length);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Gemini extraction returned {StatusCode}: {Body}",
                    (int)response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"Gemini vision API returned HTTP {(int)response.StatusCode}.");
            }

            var gemini = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Gemini response body was empty.");

            var rawText = gemini.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                throw new InvalidOperationException("Gemini response did not contain any text part.");
            }

            var json = StripJsonFences(rawText);

            var extracted = JsonSerializer.Deserialize<ProductExtractionResult>(json, ExtractionJsonOptions)
                ?? throw new InvalidOperationException("Gemini returned JSON that deserialized to null.");

            return extracted;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Gemini extraction returned unparseable JSON.");
            throw new InvalidOperationException("Vision model returned a response that could not be parsed as JSON.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error calling Gemini extraction endpoint.");
            throw new InvalidOperationException("Failed to reach the vision service. Please try again.", ex);
        }
    }

    /// <summary>
    /// Removes ```json ... ``` fences if the model wrapped the payload despite being told not to.
    /// </summary>
    private static string StripJsonFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith('`')) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;

        var withoutOpener = trimmed[(firstNewline + 1)..];
        var lastFence = withoutOpener.LastIndexOf("```", StringComparison.Ordinal);
        return (lastFence >= 0 ? withoutOpener[..lastFence] : withoutOpener).Trim();
    }

    private static readonly JsonSerializerOptions ExtractionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    // --- Gemini wire types (kept private; never returned to callers) ---

    private sealed record GeminiRequest(
        [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        [JsonPropertyName("inline_data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiInlineData? InlineData { get; init; }
    }

    private sealed record GeminiInlineData(
        [property: JsonPropertyName("mime_type")] string MimeType,
        [property: JsonPropertyName("data")] string Data);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("responseMimeType")] string ResponseMimeType,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiResponseContent? Content);

    private sealed record GeminiResponseContent(
        [property: JsonPropertyName("parts")] List<GeminiResponsePart>? Parts);

    private sealed record GeminiResponsePart(
        [property: JsonPropertyName("text")] string? Text);
}
