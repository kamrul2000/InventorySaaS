using System.Globalization;
using System.Text.Json;

namespace InventorySaaS.Application.Common.Barcodes;

/// <summary>
/// Turns a raw scanned string into a <see cref="BarcodePayload"/>.
///
/// Three shapes are recognised, in order of specificity:
/// <list type="number">
///   <item>A JSON object, which is what this system's own QR labels carry.</item>
///   <item>A GS1 Application Identifier string — a pragmatic subset: 01 GTIN, 10 batch,
///         17 expiry, 21 serial, 30 quantity. Full GS1-128 is deliberately out of scope.</item>
///   <item>Anything else, treated as a bare code.</item>
/// </list>
/// Parsing never throws: an unrecognisable payload degrades to a plain code, and the caller
/// reports "not recognised" from the lookup that follows rather than from a parse failure.
/// </summary>
public static class BarcodePayloadParser
{
    /// <summary>FNC1 / group separator, which terminates a variable-length GS1 element.</summary>
    private const char GroupSeparator = '';

    /// <summary>Fixed-length GS1 elements, so the parser knows where each one ends.</summary>
    private static readonly Dictionary<string, int> FixedLengthAis = new()
    {
        ["01"] = 14, // GTIN
        ["17"] = 6,  // Expiry, YYMMDD
        ["11"] = 6,  // Production date, YYMMDD
        ["15"] = 6,  // Best-before, YYMMDD
    };

    private static readonly HashSet<string> VariableLengthAis = ["10", "21", "30", "240", "241"];

    public static BarcodePayload Parse(string? rawValue)
    {
        var raw = rawValue?.Trim() ?? string.Empty;
        if (raw.Length == 0) return new BarcodePayload(string.Empty);

        return TryParseJson(raw, out var json) ? json
            : TryParseGs1(raw, out var gs1) ? gs1
            : new BarcodePayload(raw);
    }

    private static bool TryParseJson(string raw, out BarcodePayload payload)
    {
        payload = null!;
        if (raw.Length < 2 || raw[0] != '{' || raw[^1] != '}') return false;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;

            var code = ReadString(document.RootElement, "p", "code", "product", "sku", "barcode");
            if (string.IsNullOrWhiteSpace(code)) return false;

            payload = new BarcodePayload(
                code.Trim(),
                ReadString(document.RootElement, "b", "batch", "batchNumber", "lot")?.Trim(),
                ReadString(document.RootElement, "s", "serial", "serialNumber")?.Trim(),
                ReadDate(document.RootElement, "e", "expiry", "expiryDate"),
                ReadInt(document.RootElement, "q", "qty", "quantity"),
                BarcodePayloadFormat.Json);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseGs1(string raw, out BarcodePayload payload)
    {
        payload = null!;

        var elements = raw.Contains('(')
            ? ReadParenthesisedAis(raw)
            : ReadConcatenatedAis(raw);

        // A single element is far more likely to be an ordinary barcode that happens to start
        // with "01" than a one-element GS1 string, so require real structure before claiming GS1.
        if (elements is null || elements.Count == 0) return false;
        if (elements.Count == 1 && !raw.Contains('(') && !raw.Contains(GroupSeparator)) return false;

        var code = elements.GetValueOrDefault("01");
        if (string.IsNullOrWhiteSpace(code)) return false;

        payload = new BarcodePayload(
            code,
            elements.GetValueOrDefault("10"),
            elements.GetValueOrDefault("21"),
            ParseGs1Date(elements.GetValueOrDefault("17")),
            int.TryParse(elements.GetValueOrDefault("30"), NumberStyles.None, CultureInfo.InvariantCulture, out var qty) ? qty : null,
            BarcodePayloadFormat.Gs1);

        return true;
    }

    /// <summary>Reads the human-readable form: <c>(01)08801234567890(10)B2401</c>.</summary>
    private static Dictionary<string, string>? ReadParenthesisedAis(string raw)
    {
        var elements = new Dictionary<string, string>();
        var index = 0;

        while (index < raw.Length)
        {
            if (raw[index] != '(') return elements.Count > 0 ? elements : null;

            var close = raw.IndexOf(')', index + 1);
            if (close < 0) return null;

            var ai = raw[(index + 1)..close];
            if (ai.Length is < 2 or > 4 || !ai.All(char.IsAsciiDigit)) return null;

            var valueStart = close + 1;
            var next = raw.IndexOf('(', valueStart);
            var valueEnd = next < 0 ? raw.Length : next;

            elements[ai] = raw[valueStart..valueEnd].Trim(GroupSeparator);
            index = valueEnd;
        }

        return elements;
    }

    /// <summary>
    /// Reads the wire form, where elements run together and only variable-length ones are
    /// terminated by a group separator: <c>0108801234567890 10B2401 ␝ 17271231</c>.
    /// </summary>
    private static Dictionary<string, string>? ReadConcatenatedAis(string raw)
    {
        var elements = new Dictionary<string, string>();
        var index = 0;

        while (index < raw.Length)
        {
            if (raw[index] == GroupSeparator) { index++; continue; }

            if (index + 2 > raw.Length) return null;
            var ai = raw.Substring(index, 2);
            if (!ai.All(char.IsAsciiDigit)) return null;
            index += 2;

            if (FixedLengthAis.TryGetValue(ai, out var length))
            {
                if (index + length > raw.Length) return null;
                elements[ai] = raw.Substring(index, length);
                index += length;
            }
            else if (VariableLengthAis.Contains(ai))
            {
                var separator = raw.IndexOf(GroupSeparator, index);
                var end = separator < 0 ? raw.Length : separator;
                elements[ai] = raw[index..end];
                index = end;
            }
            else
            {
                // An AI we don't model: we can't know where it ends, so stop guessing.
                return elements.Count > 0 ? elements : null;
            }
        }

        return elements;
    }

    /// <summary>
    /// GS1 dates are YYMMDD. Per the spec, YY 00–49 is 2000–2049 and 50–99 is 1950–1999, and a
    /// day of 00 means "last day of that month".
    /// </summary>
    private static DateTime? ParseGs1Date(string? value)
    {
        if (value is not { Length: 6 } || !value.All(char.IsAsciiDigit)) return null;

        var year = int.Parse(value[..2], CultureInfo.InvariantCulture);
        var month = int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(4, 2), CultureInfo.InvariantCulture);

        if (month is < 1 or > 12) return null;

        var fullYear = year <= 49 ? 2000 + year : 1900 + year;
        if (day == 0) day = DateTime.DaysInMonth(fullYear, month);
        if (day > DateTime.DaysInMonth(fullYear, month)) return null;

        return new DateTime(fullYear, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        return null;
    }

    private static DateTime? ReadDate(JsonElement root, params string[] names)
    {
        var text = ReadString(root, names);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static int? ReadInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        }
        return null;
    }
}
