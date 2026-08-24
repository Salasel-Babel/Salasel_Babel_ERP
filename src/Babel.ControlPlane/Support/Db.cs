using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Support;

/// <summary>
/// خطأ يُرفع حين تُصيب عبارةُ كتابةٍ عدداً من الصفوف <b>غير</b> المتوقَّع.
/// هذا هو الفرق بين فخ-09 وعدمه: PostgreSQL يعتبر «أصبتُ صفر صفوف» نجاحاً،
/// ونحن لا نعتبره كذلك في أي مسار كتابة.
/// </summary>
public sealed class UnexpectedRowCountException(string sql, int expected, int actual)
    : Exception($"عدد الصفوف المتأثرة {actual} والمتوقَّع {expected} — العبارة: {Trim(sql)}")
{
    /// <summary>عدد الصفوف المتوقَّع.</summary>
    public int Expected { get; } = expected;

    /// <summary>عدد الصفوف الذي أصابته العبارة فعلاً.</summary>
    public int Actual { get; } = actual;

    private static string Trim(string s) =>
        s.Length <= 240 ? s.Replace("\n", " ") : s[..240].Replace("\n", " ") + " …";
}

/// <summary>
/// طبقة ADO رقيقة. غرضها الوحيد أن يكون <b>تأكيد عدد الصفوف</b> هو المسار
/// الافتراضي لا الاستثناء (القاعدة 2 في <c>evidence/README.md</c> §3، فخ-09).
/// </summary>
public static class Db
{
    /// <summary>يفتح اتصالاً.</summary>
    /// <param name="cs">سلسلة الاتصال.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>اتصال مفتوح.</returns>
    public static async Task<NpgsqlConnection> OpenAsync(string cs, CancellationToken ct = default)
    {
        var c = new NpgsqlConnection(cs);
        await c.OpenAsync(ct);
        return c;
    }

    /// <summary>يبني أمراً على اتصال ومعاملة.</summary>
    /// <param name="c">الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <returns>الأمر.</returns>
    public static NpgsqlCommand Cmd(NpgsqlConnection c, string sql, NpgsqlTransaction? tx = null)
    {
        var cmd = new NpgsqlCommand(sql, c, tx);
        return cmd;
    }

    /// <summary>ينفّذ عبارة DDL أو عبارة لا يُهمّ عدد صفوفها (إنشاء مخطط، إلخ).</summary>
    public static async Task ExecAsync(NpgsqlConnection c, string sql,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>ينفّذ عبارة على اتصال جديد.</summary>
    /// <param name="cs">سلسلة الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    public static async Task ExecAsync(string cs, string sql, CancellationToken ct = default)
    {
        await using var c = await OpenAsync(cs, ct);
        await ExecAsync(c, sql, null, ct);
    }

    /// <summary>
    /// ينفّذ عبارة كتابة <b>ويؤكّد</b> عدد صفوفها. أي انحراف يرمي ويُفشِل المعاملة.
    /// كل كتابة في مستوى التحكّم تمرّ من هنا.
    /// </summary>
    public static async Task<int> WriteAsync(NpgsqlConnection c, string sql,
        int expectedRows, Action<NpgsqlParameterCollection>? bind = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        bind?.Invoke(cmd.Parameters);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        if (n != expectedRows) throw new UnexpectedRowCountException(sql, expectedRows, n);
        return n;
    }

    /// <summary>
    /// كتابة يكون فيها «صفر صفوف» نتيجةً <b>مشروعة ومقصودة</b> — أي
    /// <c>ON CONFLICT DO NOTHING</c> المستعمَل كحارس إحكام. تُرجِع العدد الفعلي
    /// حتى يستطيع النداء أن يميّز «أُدرِج» من «مكرَّر» بدل أن يبتلع الفرق.
    /// </summary>
    public static async Task<int> WriteIdempotentAsync(NpgsqlConnection c, string sql,
        Action<NpgsqlParameterCollection>? bind = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        bind?.Invoke(cmd.Parameters);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        if (n is not (0 or 1)) throw new UnexpectedRowCountException(sql, 1, n);
        return n;
    }

    /// <summary>
    /// إدراج متعدد الصفوف بحارس إحكام: العدد المقبول بين صفر و<c>max</c>.
    /// يُرجِع العدد الفعلي حتى يميّز النداء «أُدرِج كلياً» من «مكرَّر جزئياً».
    /// </summary>
    public static async Task<int> WriteIdempotentManyAsync(NpgsqlConnection c, string sql,
        int maxRows, Action<NpgsqlParameterCollection>? bind = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        bind?.Invoke(cmd.Parameters);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        if (n < 0 || n > maxRows) throw new UnexpectedRowCountException(sql, maxRows, n);
        return n;
    }

    /// <summary>يقرأ قيمة مفردة.</summary>
    /// <typeparam name="T">نوع القيمة.</typeparam>
    /// <param name="c">الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="bind">ربط المعاملات.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>القيمة، أو الافتراضي عند <c>NULL</c>.</returns>
    public static async Task<T?> ScalarAsync<T>(NpgsqlConnection c, string sql,
        Action<NpgsqlParameterCollection>? bind = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        bind?.Invoke(cmd.Parameters);
        var v = await cmd.ExecuteScalarAsync(ct);
        if (v is null or DBNull) return default;
        if (v is T t) return t;
        return (T)Convert.ChangeType(v, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>يقرأ قيمة مفردة على اتصال جديد.</summary>
    /// <typeparam name="T">نوع القيمة.</typeparam>
    /// <param name="cs">سلسلة الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="bind">ربط المعاملات.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>القيمة، أو الافتراضي عند <c>NULL</c>.</returns>
    public static async Task<T?> ScalarAsync<T>(string cs, string sql,
        Action<NpgsqlParameterCollection>? bind = null, CancellationToken ct = default)
    {
        await using var c = await OpenAsync(cs, ct);
        return await ScalarAsync<T>(c, sql, bind, null, ct);
    }

    /// <summary>يقرأ صفوفاً ويُسقطها بدالّة.</summary>
    /// <typeparam name="T">نوع الصفّ المُسقَط.</typeparam>
    /// <param name="c">الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="map">دالّة الإسقاط.</param>
    /// <param name="bind">ربط المعاملات.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الصفوف.</returns>
    public static async Task<List<T>> QueryAsync<T>(NpgsqlConnection c, string sql,
        Func<NpgsqlDataReader, T> map, Action<NpgsqlParameterCollection>? bind = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var cmd = Cmd(c, sql, tx);
        bind?.Invoke(cmd.Parameters);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<T>();
        while (await r.ReadAsync(ct)) list.Add(map(r));
        return list;
    }

    /// <summary>يقرأ صفوفاً على اتصال جديد.</summary>
    /// <typeparam name="T">نوع الصفّ المُسقَط.</typeparam>
    /// <param name="cs">سلسلة الاتصال.</param>
    /// <param name="sql">العبارة.</param>
    /// <param name="map">دالّة الإسقاط.</param>
    /// <param name="bind">ربط المعاملات.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الصفوف.</returns>
    public static async Task<List<T>> QueryAsync<T>(string cs, string sql,
        Func<NpgsqlDataReader, T> map, Action<NpgsqlParameterCollection>? bind = null,
        CancellationToken ct = default)
    {
        await using var c = await OpenAsync(cs, ct);
        return await QueryAsync(c, sql, map, bind, null, ct);
    }

    /// <summary>معرّف SQL آمن: أسماء قواعد البيانات والأدوار لا تقبل معاملات مرتبطة.</summary>
    public static string Ident(string raw)
    {
        if (raw.Length == 0 || raw.Length > 63)
            throw new ArgumentException($"معرّف غير مقبول: «{raw}»", nameof(raw));
        foreach (var ch in raw)
            if (!(char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '_'))
                throw new ArgumentException(
                    $"معرّف غير مقبول: «{raw}» — يُسمح بـ[a-z0-9_] فقط", nameof(raw));
        if (char.IsAsciiDigit(raw[0]))
            throw new ArgumentException($"معرّف يبدأ برقم: «{raw}»", nameof(raw));
        return raw;
    }

    /// <summary>معامل مرتبط، مع تحويل <c>null</c> إلى <c>DBNull</c>.</summary>
    /// <param name="name">اسم المعامل.</param>
    /// <param name="value">القيمة.</param>
    /// <param name="type">النوع الصريح عند اللزوم.</param>
    /// <returns>المعامل.</returns>
    public static NpgsqlParameter P(string name, object? value, NpgsqlDbType? type = null)
    {
        var p = new NpgsqlParameter(name, value ?? DBNull.Value);
        if (type is not null) p.NpgsqlDbType = type.Value;
        return p;
    }

    /// <summary>مبلغ مالي: <c>decimal</c> ⇄ <c>numeric(19,4)</c>. لا عائم في أي طبقة.</summary>
    public static NpgsqlParameter Money(string name, decimal value) =>
        new(name, NpgsqlDbType.Numeric) { Value = decimal.Round(value, 4, MidpointRounding.ToEven) };
}
