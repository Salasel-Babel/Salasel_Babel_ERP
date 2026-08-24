using Npgsql;

namespace BabelPosOffline.Support;

public static class Sql
{
    public static async Task<NpgsqlConnection> OpenAsync(string cs)
    {
        var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        return c;
    }

    public static async Task ExecAsync(string cs, string sql)
    {
        await using var c = await OpenAsync(cs);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task ExecAsync(NpgsqlConnection c, string sql, NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T?> ScalarAsync<T>(string cs, string sql)
    {
        await using var c = await OpenAsync(cs);
        return await ScalarAsync<T>(c, sql);
    }

    public static async Task<T?> ScalarAsync<T>(NpgsqlConnection c, string sql, NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? default : (T)Convert.ChangeType(v, typeof(T))!;
    }

    public static async Task<PostgresException?> ExpectFailureAsync(string cs, string sql)
    {
        try { await ExecAsync(cs, sql); return null; }
        catch (PostgresException ex) { return ex; }
    }

    public static string Describe(PostgresException ex) =>
        $"SQLSTATE {ex.SqlState} ({ex.SqlState switch
        {
            "42501" => "insufficient_privilege",
            "23514" => "check_violation",
            "23505" => "unique_violation",
            "23P01" => "exclusion_violation",
            _ => ex.SqlState
        }}): {ex.MessageText}";

    public static async Task<string> TableAsync(string cs, string sql)
    {
        await using var c = await OpenAsync(cs);
        await using var cmd = new NpgsqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        var cols = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToArray();
        var rows = new List<string[]>();
        while (await r.ReadAsync())
            rows.Add(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "" : Fmt(r.GetValue(i))).ToArray());
        var w = cols.Select((c2, i) => Math.Max(c2.Length, rows.Count == 0 ? 0 : rows.Max(x => x[i].Length))).ToArray();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join("  ", cols.Select((c2, i) => c2.PadRight(w[i]))));
        sb.AppendLine(string.Join("  ", w.Select(x => new string('-', x))));
        foreach (var row in rows) sb.AppendLine(string.Join("  ", row.Select((v, i) => v.PadRight(w[i]))));
        return sb.ToString().TrimEnd();
    }

    private static string Fmt(object v) => v switch
    {
        byte[] b => Convert.ToHexString(b).ToLowerInvariant(),
        DateTime d => d.ToString("O"),
        decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };
}
