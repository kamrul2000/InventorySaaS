using System.Text;

namespace InventorySaaS.Application.Common.Csv;

/// <summary>
/// Minimal RFC 4180 reader/writer. The import surface is small enough that a parser we control
/// beats a dependency: we need quoted fields, embedded commas and newlines, and nothing else.
/// </summary>
public static class CsvFile
{
    /// <summary>
    /// Splits CSV text into rows of raw fields. Quoted fields may contain commas, CR/LF and
    /// doubled quotes (<c>""</c>). A UTF-8 BOM and a trailing newline are both tolerated.
    /// </summary>
    public static List<List<string>> Parse(string content)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(content)) return rows;

        // Excel writes a BOM; left in place it would become part of the first header name.
        if (content[0] == '﻿') content = content[1..];

        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    fieldWasQuoted = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    fieldWasQuoted = false;
                    break;

                case '\r':
                    // Swallow CR; the following LF (or its absence) ends the row.
                    break;

                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    fieldWasQuoted = false;
                    break;

                default:
                    field.Append(c);
                    break;
            }
        }

        // A file that doesn't end in a newline still has one final field to flush.
        if (field.Length > 0 || fieldWasQuoted || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        // Drop rows that are entirely empty — a blank line carries no record.
        return rows.Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
    }

    /// <summary>Escapes one field: quoted only when it contains a comma, quote, or line break.</summary>
    public static string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');

        return needsQuotes
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    /// <summary>Builds a CSV document with a UTF-8 BOM so Excel opens non-ASCII names correctly.</summary>
    public static byte[] Build(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", headers.Select(EscapeField))).Append("\r\n");

        foreach (var row in rows)
            sb.Append(string.Join(",", row.Select(EscapeField))).Append("\r\n");

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
    }
}
