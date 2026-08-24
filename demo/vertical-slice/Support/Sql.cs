using Npgsql;

namespace BabelDemo.Support;

/// <summary>مساعدات ADO صغيرة كي تبقى الشيفرة مقروءة.</summary>
public static class Sql
{
    public static async Task<NpgsqlConnection> OpenAsync(string cs, CancellationToken ct = default)
    {
        var c = new NpgsqlConnection(cs);
        await c.OpenAsync(ct);
        return c;
    }

    public static async Task ExecAsync(string cs, string sql, CancellationToken ct = default)
    {
        await using var c = await OpenAsync(cs, ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task ExecAsync(NpgsqlConnection c, string sql, NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task<T?> ScalarAsync<T>(string cs, string sql, CancellationToken ct = default)
    {
        await using var c = await OpenAsync(cs, ct);
        return await ScalarAsync<T>(c, sql, ct);
    }

    public static async Task<T?> ScalarAsync<T>(NpgsqlConnection c, string sql, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is null or DBNull ? default : (T)Convert.ChangeType(v, typeof(T))!;
    }

    /// <summary>اسم حالة SQL بالعربية والإنجليزية، لعرضه للمستخدم كما هو.</summary>
    public static string StateName(string? sqlState) => sqlState switch
    {
        "42501" => "insufficient_privilege — صلاحية غير كافية",
        "23514" => "check_violation — مخالفة قيد تحقّق",
        "23505" => "unique_violation — مخالفة تفرّد",
        "23503" => "foreign_key_violation — مخالفة مفتاح أجنبي",
        "21000" => "cardinality_violation — مخالفة عدد الصفوف",
        null => "",
        _ => sqlState
    };
}
