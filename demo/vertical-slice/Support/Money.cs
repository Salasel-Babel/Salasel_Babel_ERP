using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BabelDemo.Support;

/// <summary>
/// المال decimal في C# وNUMERIC(19,4) في PostgreSQL — ولا يتحوّل إلى float في أي طبقة،
/// بما فيها JSON. لذلك يُسلسل كنص بمقياس ثابت "0.0000"، فلا يمرّ أبداً عبر double
/// في محلّل JavaScript.
///
/// Money is decimal in C# and NUMERIC(19,4) in PostgreSQL, and must not become a
/// float in ANY layer — JSON included. A JSON number would be parsed by the browser
/// as an IEEE-754 double, so money crosses the wire as a fixed-scale STRING.
/// </summary>
internal sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => Money.Parse(reader.GetString()),
            JsonTokenType.Number => reader.GetDecimal(),   // exact decimal parse, never double
            JsonTokenType.Null => 0m,
            _ => throw new JsonException("expected a money string such as \"1000.0000\"")
        };

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(Money.Render(value));
}

internal static class Money
{
    public static string Render(decimal d) => d.ToString("0.0000", CultureInfo.InvariantCulture);

    /// <summary>تحليل نص إلى decimal بلا أي مرور بـ double. يقبل الفواصل الألفية والأرقام العربية-الهندية.</summary>
    public static decimal Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        var t = Normalize(s);
        return decimal.Parse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                             CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? s, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(s)) return true;
        return decimal.TryParse(Normalize(s), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out value);
    }

    private static string Normalize(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        var n = 0;
        foreach (var ch in s)
        {
            if (ch is ',' or '_' or '٬' or '‏' or '‎' or ' ' or ' ') continue;
            if (ch is '٫' or '،') { buf[n++] = '.'; continue; }        // Arabic decimal separator
            if (ch >= '٠' && ch <= '٩') { buf[n++] = (char)('0' + (ch - '٠')); continue; }
            if (ch >= '۰' && ch <= '۹') { buf[n++] = (char)('0' + (ch - '۰')); continue; }
            buf[n++] = ch;
        }
        return new string(buf[..n]).Trim();
    }
}
