using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Entitlement;

public sealed record PlanDefinition(
    string Code, string NameAr, string NameEn,
    decimal MonthlyPrice, decimal PerUserPrice, int IncludedUsers,
    IReadOnlyList<string> Modules);

/// <summary>
/// الخطط. التسعير على <b>المحورين</b>: سعر شهري للحزمة + سعر لكل مستخدم بعد
/// المُضمَّن.
///
/// <para><b>⚠️ الأسعار هنا قيم بنيوية للاختبار، لا قائمة أسعار.</b> السعر
/// الشهري المستهدف وعدد المستأجرين سؤالان مفتوحان على المالك
/// (<c>docs/decisions/README.md</c> — «أسئلة على المالك»)، وكل مبلغ في هذا
/// الملف يُستبدل قبل أول عرض سعر. لا التزام تعاقدي يُبنى على رقم هنا.</para>
///
/// <para>كل المبالغ <c>decimal</c> ⇄ <c>numeric(19,4)</c>. لا عائم.</para>
/// </summary>
public static class PlanCatalog
{
    public static readonly IReadOnlyList<PlanDefinition> All =
    [
        new("ESSENTIAL", "الأساسية", "Essential", 900.0000m, 60.0000m, 3,
            ["CORE", "AR", "AP"]),
        new("GROWTH", "النامية", "Growth", 1800.0000m, 55.0000m, 8,
            ["CORE", "AR", "AP", "INV", "FA", "REP"]),
        new("RETAIL", "التجزئة", "Retail", 2400.0000m, 50.0000m, 12,
            ["CORE", "AR", "AP", "INV", "POS", "REP"]),
        new("FULL", "الشاملة", "Full", 3600.0000m, 45.0000m, 20,
            ["CORE", "AR", "AP", "INV", "POS", "PRJ", "PAY", "FA", "REP"]),
    ];

    public static PlanDefinition Require(string code) =>
        All.FirstOrDefault(p => p.Code == code)
        ?? throw new ArgumentException($"خطة غير معروفة: «{code}»", nameof(code));

    public static async Task SeedAsync(NpgsqlConnection c, CancellationToken ct = default)
    {
        var plans = All.OrderBy(p => p.Code, StringComparer.Ordinal).ToList();
        var values = string.Join(", ",
            plans.Select((_, i) => $"(@c{i}, @ar{i}, @en{i}, @m{i}, @u{i}, @n{i}, 'SAR')"));

        await Db.WriteAsync(c, $"""
            insert into control.plan
                (plan_code, name_ar, name_en, monthly_price, per_user_price, included_users, currency)
            values {values}
            on conflict (plan_code) do update
               set name_ar = excluded.name_ar, name_en = excluded.name_en,
                   monthly_price = excluded.monthly_price,
                   per_user_price = excluded.per_user_price,
                   included_users = excluded.included_users
            """, plans.Count, p =>
            {
                for (var i = 0; i < plans.Count; i++)
                {
                    p.AddWithValue($"c{i}", plans[i].Code);
                    p.AddWithValue($"ar{i}", plans[i].NameAr);
                    p.AddWithValue($"en{i}", plans[i].NameEn);
                    p.Add(Db.Money($"m{i}", plans[i].MonthlyPrice));
                    p.Add(Db.Money($"u{i}", plans[i].PerUserPrice));
                    p.AddWithValue($"n{i}", plans[i].IncludedUsers);
                }
            }, null, ct);

        var links = plans.SelectMany(p => p.Modules.Select(m => (p.Code, Module: m)))
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ThenBy(x => x.Module, StringComparer.Ordinal).ToList();

        var lvalues = string.Join(", ", links.Select((_, i) => $"(@p{i}, @m{i})"));
        await Db.WriteIdempotentManyAsync(c, $"""
            insert into control.plan_module (plan_code, module_code)
            values {lvalues}
            on conflict (plan_code, module_code) do nothing
            """, links.Count, p =>
            {
                for (var i = 0; i < links.Count; i++)
                {
                    p.AddWithValue($"p{i}", links[i].Code);
                    p.AddWithValue($"m{i}", links[i].Module);
                }
            }, null, ct);
    }

    /// <summary>يفتح اشتراكاً فعّالاً للمستأجر. مُحكَم: نفس المستأجر والخطة والتاريخ = صفّ واحد.</summary>
    public static async Task<Guid> SubscribeAsync(NpgsqlConnection c, Guid tenantId, string planCode,
        DateOnly startedOn, CancellationToken ct = default)
    {
        var existing = await Db.QueryAsync(c, """
            select subscription_id from control.subscription
             where tenant_id = @t and plan_code = @p and started_on = @s
            """, r => r.GetGuid(0),
            x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", planCode);
                x.Add(Db.P("s", startedOn, NpgsqlDbType.Date));
            }, null, ct);
        if (existing.Count > 0) return existing[0];

        var id = Guid.CreateVersion7();
        await Db.WriteAsync(c, """
            insert into control.subscription
                (subscription_id, tenant_id, plan_code, started_on, ends_on, state)
            values (@id, @t, @p, @s, null, 'Active')
            """, 1, x =>
            {
                x.Add(Db.P("id", id, NpgsqlDbType.Uuid));
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", planCode);
                x.Add(Db.P("s", startedOn, NpgsqlDbType.Date));
            }, null, ct);
        return id;
    }
}
