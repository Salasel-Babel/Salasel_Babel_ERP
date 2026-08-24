using System.Globalization;
using System.Text;

namespace SalaselBabel.MatrixValidator;

/// <summary>
/// Minimal RFC 4180 CSV reader. Handles quoted fields, embedded commas, doubled quotes,
/// and a UTF-8 BOM. No dependencies — the seed data must be loadable anywhere.
/// قارئ CSV بسيط يدعم الحقول المقتبسة وعلامة ترتيب البايت.
/// </summary>
public static class Csv
{
    public static List<Dictionary<string, string>> Read(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var rows = Split(text);
        if (rows.Count == 0) return new List<Dictionary<string, string>>();

        var header = rows[0];
        var result = new List<Dictionary<string, string>>(rows.Count - 1);
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 1 && row[0].Length == 0) continue;   // blank line
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < header.Count; c++)
                d[header[c]] = c < row.Count ? row[c] : string.Empty;
            d["__line__"] = (r + 1).ToString(CultureInfo.InvariantCulture);
            result.Add(d);
        }
        return result;
    }

    private static List<List<string>> Split(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
                continue;
            }

            switch (ch)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                    break;
                default: field.Append(ch); break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
