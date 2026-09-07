using System.Globalization;
using System.Text.Json;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Entitlement;

/// <summary>خطّةٌ لم تُنشر بعد — لا تُباع ولا يُفتح عليها اشتراك.</summary>
public sealed class PlanNotPublishedException(string planCode)
    : Exception($"الخطّة «{planCode}» غير منشورة: لا اشتراكَ عليها حتى ينشرها مشغّلُ المنصّة بسعرها من سطح الخطط (ADR-0092).")
{
    /// <summary>رمز الخطّة.</summary>
    public string PlanCode { get; } = planCode;
}

/// <summary>خطّةٌ لا صفَّ لها في <c>control.plan</c>.</summary>
public sealed class PlanNotFoundException(string planCode)
    : Exception($"خطّة غير معروفة: «{planCode}» — لا صفَّ لها في control.plan.")
{
    /// <summary>رمز الخطّة.</summary>
    public string PlanCode { get; } = planCode;
}

/// <summary>
/// <b>كتالوجُ الخطط — من قاعدة مستوى التحكّم لا من الشيفرة (ADR-0092).</b>
/// <para>
/// كان <c>PlanCatalog.All</c> قائمةً في الشيفرة، وكان بذرُها يكتب فوق الأسعار في القاعدة عند
/// كلّ تشغيل؛ فسعرٌ يغيّره مشغّلُ المنصّة كان يعود إلى رقم الاختبار مع أوّل نشرة. والآن
/// الصفُّ هو المصدر: يُنشأ ويُسعَّر ويُنشر من سطح المنصّة، وكلُّ تغييرٍ يُلحَق بسجلّ
/// <c>control.plan_change</c> بفاعله وسنده وسببه وما قبله وما بعده.
/// </para>
/// </summary>
public static class PlanDirectory
{
    private const string Select = """
        select p.plan_code, p.name_ar, p.name_en, p.monthly_price, p.per_user_price, p.included_users, p.published,
               coalesce((select string_agg(m.module_code, ',' order by m.module_code)
                           from control.plan_module m where m.plan_code = p.plan_code), '') as modules
          from control.plan p
        """;

    /// <summary>كلُّ الخطط — المنشورة وغيرها — مرتَّبةً برمزها.</summary>
    public static async Task<IReadOnlyList<PlanDefinition>> AllAsync(NpgsqlConnection c, CancellationToken ct = default) =>
        await Db.QueryAsync(c, Select + " order by p.plan_code", Read, null, null, ct);

    /// <summary>الخططُ المنشورة وحدها — ما يجوز أن يُباع.</summary>
    public static async Task<IReadOnlyList<PlanDefinition>> PublishedAsync(NpgsqlConnection c, CancellationToken ct = default) =>
        await Db.QueryAsync(c, Select + " where p.published order by p.plan_code", Read, null, null, ct);

    /// <summary>خطّةٌ برمزها، أو <c>null</c>.</summary>
    public static async Task<PlanDefinition?> FindAsync(NpgsqlConnection c, string planCode, CancellationToken ct = default)
    {
        var rows = await Db.QueryAsync(c, Select + " where p.plan_code = @p", Read,
            p => p.AddWithValue("p", planCode), null, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>خطّةٌ برمزها، أو رفضٌ باسمها.</summary>
    public static async Task<PlanDefinition> RequireAsync(NpgsqlConnection c, string planCode, CancellationToken ct = default) =>
        await FindAsync(c, planCode, ct) ?? throw new PlanNotFoundException(planCode);

    /// <summary>
    /// يُنشئ خطّةً أو يعدّلها <b>بسندٍ وسبب</b>، ويُلحق التغيير بالسجلّ. وحزمةُ الوحدات
    /// تُستبدل كلّها؛ ووحدةٌ ليست في الكتالوج تُرفض باسمها قبل أن يُكتب شيء.
    /// </summary>
    public static async Task<PlanDefinition> UpsertAsync(
        NpgsqlConnection c, PlanDefinition plan, ChangeAuthority authority, DateTimeOffset now, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(authority);
        authority.Validate();

        if (string.IsNullOrWhiteSpace(plan.Code) || !plan.Code.All(static ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))
            throw new ArgumentException($"رمز الخطّة «{plan.Code}» ليس من الشكل A-Z0-9_ الكبير.", nameof(plan));
        if (plan.MonthlyPrice < 0m || plan.PerUserPrice < 0m || plan.IncludedUsers < 0)
            throw new ArgumentException("أسعارُ الخطّة والمستخدمون المُضمَّنون لا تكون سالبة.", nameof(plan));
        if (plan.Modules.Count == 0)
            throw new ArgumentException("خطّةٌ بلا وحدات لا تُباع.", nameof(plan));
        foreach (var module in plan.Modules)
            ModuleCatalog.Require(module);

        var before = await FindAsync(c, plan.Code, ct);

        await using var tx = await c.BeginTransactionAsync(ct);

        await Db.WriteIdempotentAsync(c, """
            insert into control.plan
                (plan_code, name_ar, name_en, monthly_price, per_user_price, included_users, currency, published, published_at, published_by)
            values (@c, @ar, @en, @m, @u, @n, 'SAR', @pub, @pat, @pby)
            on conflict (plan_code) do update
               set name_ar = excluded.name_ar, name_en = excluded.name_en,
                   monthly_price = excluded.monthly_price, per_user_price = excluded.per_user_price,
                   included_users = excluded.included_users,
                   published = excluded.published,
                   published_at = case when excluded.published and not control.plan.published then excluded.published_at else control.plan.published_at end,
                   published_by = case when excluded.published and not control.plan.published then excluded.published_by else control.plan.published_by end
            """, p =>
            {
                p.AddWithValue("c", plan.Code);
                p.AddWithValue("ar", plan.NameAr);
                p.AddWithValue("en", plan.NameEn);
                p.Add(Db.Money("m", plan.MonthlyPrice));
                p.Add(Db.Money("u", plan.PerUserPrice));
                p.AddWithValue("n", plan.IncludedUsers);
                p.AddWithValue("pub", plan.Published);
                p.Add(Db.P("pat", plan.Published ? Canon.Instant(now) : DBNull.Value, NpgsqlDbType.TimestampTz));
                p.AddWithValue("pby", plan.Published ? authority.Actor : string.Empty);
            }, tx, ct);

        // ‏الحزمةُ تُستبدل كلّها: تُحذف صفوفُ الخطّة — بعددها المقروء قبل المعاملة لا أكثر — ثم تُكتب.
        await Db.WriteIdempotentManyAsync(c, "delete from control.plan_module where plan_code = @c",
            before?.Modules.Count ?? 0, p => p.AddWithValue("c", plan.Code), tx, ct);

        var modules = plan.Modules.Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList();
        var values = string.Join(", ", modules.Select((_, i) => $"(@c, @m{i})"));
        await Db.WriteIdempotentManyAsync(c, $"insert into control.plan_module (plan_code, module_code) values {values}",
            modules.Count, p =>
            {
                p.AddWithValue("c", plan.Code);
                for (var i = 0; i < modules.Count; i++) p.AddWithValue($"m{i}", modules[i]);
            }, tx, ct);

        var after = plan with { Modules = modules };
        await Db.WriteAsync(c, """
            insert into control.plan_change (change_id, plan_code, changed_at, actor, authority, reason_ar, before, after)
            values (@id, @c, @at, @actor, @auth, @reason, @before, @after)
            """, 1, p =>
            {
                p.Add(Db.P("id", Guid.CreateVersion7(), NpgsqlDbType.Uuid));
                p.AddWithValue("c", plan.Code);
                p.Add(Db.P("at", now, NpgsqlDbType.TimestampTz));
                p.AddWithValue("actor", authority.Actor);
                p.AddWithValue("auth", authority.Authority);
                p.AddWithValue("reason", authority.ReasonAr);
                p.Add(Db.P("before", before is null ? DBNull.Value : Json(before), NpgsqlDbType.Jsonb));
                p.Add(Db.P("after", Json(after), NpgsqlDbType.Jsonb));
            }, tx, ct);

        await tx.CommitAsync(ct);
        return await RequireAsync(c, plan.Code, ct);
    }

    /// <summary>سجلُّ تغييرات خطّة، من الأقدم إلى الأحدث.</summary>
    public static async Task<IReadOnlyList<PlanChange>> ChangesAsync(NpgsqlConnection c, string planCode, CancellationToken ct = default) =>
        await Db.QueryAsync(c, """
            select change_id, changed_at, actor, authority, reason_ar
              from control.plan_change
             where plan_code = @c
             order by changed_at, change_id::text
            """,
            r => new PlanChange(r.GetGuid(0), r.GetFieldValue<DateTimeOffset>(1), r.GetString(2), r.GetString(3), r.GetString(4)),
            p => p.AddWithValue("c", planCode), null, ct);

    private static PlanDefinition Read(NpgsqlDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2),
        r.GetDecimal(3), r.GetDecimal(4), r.GetInt32(5),
        r.GetString(7).Length == 0 ? [] : r.GetString(7).Split(','),
        r.GetBoolean(6));

    private static string Json(PlanDefinition plan) => JsonSerializer.Serialize(new
    {
        code = plan.Code,
        nameAr = plan.NameAr,
        nameEn = plan.NameEn,
        monthlyPrice = plan.MonthlyPrice.ToString("F4", CultureInfo.InvariantCulture),
        perUserPrice = plan.PerUserPrice.ToString("F4", CultureInfo.InvariantCulture),
        includedUsers = plan.IncludedUsers,
        modules = plan.Modules.OrderBy(m => m, StringComparer.Ordinal).ToArray(),
        published = plan.Published,
    });
}

/// <summary>سطرٌ من سجلّ تغييرات الخطط.</summary>
public sealed record PlanChange(Guid Id, DateTimeOffset ChangedAt, string Actor, string Authority, string ReasonAr);
