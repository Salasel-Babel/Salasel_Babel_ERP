using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Entitlement;

/// <summary>تعريف خطة اشتراك على المحورين. كل مبلغ <c>decimal</c> ⇄ <c>numeric(19,4)</c>.</summary>
/// <param name="Code">رمز الخطة.</param>
/// <param name="NameAr">اسم الخطة بالعربية — إلزامي.</param>
/// <param name="NameEn">اسم الخطة بالإنجليزية — إلزامي.</param>
/// <param name="MonthlyPrice">السعر الشهري للحزمة — محور الوحدة. <b>قيمة بنيوية للاختبار لا قائمة أسعار.</b></param>
/// <param name="PerUserPrice">سعر المستخدم الواحد بعد المُضمَّن — محور المستخدم.</param>
/// <param name="IncludedUsers">عدد المستخدمين المُضمَّنين في السعر الشهري.</param>
/// <param name="Modules">الوحدات التي تمنحها الخطة — تُقاس على رسم الاعتماديات قبل التطبيق.</param>
/// <param name="Published">هل نُشرت الخطّة بسعرها؟ غيرُ المنشورة لا تُباع (ADR-0092).</param>
public sealed record PlanDefinition(
    string Code, string NameAr, string NameEn,
    decimal MonthlyPrice, decimal PerUserPrice, int IncludedUsers,
    IReadOnlyList<string> Modules,
    bool Published = false);

/// <summary>
/// <b>القيم البنيوية للخطط — للاختبار والإثبات، لا كتالوجاً يُباع منه (ADR-0092).</b>
/// <para>
/// مصدرُ الكتالوج هو <c>control.plan</c> عبر <see cref="PlanDirectory"/>: يُنشئه مشغّلُ المنصّة
/// ويُسعّره وينشره من سطح الخطط، وكلُّ تغييرٍ يُلحَق بسجلّ <c>control.plan_change</c>. وما هنا
/// أربعُ خططٍ بأرقامٍ بنيوية تُبذَر في قواعد الاختبار وحدها — <b>ولا تكتب فوق صفٍّ قائم</b>:
/// سعرٌ غيّره مشغّلٌ لا يعود إلى رقم الاختبار مع أوّل بذرة.
/// </para>
/// <para>كل المبالغ <c>decimal</c> ⇄ <c>numeric(19,4)</c>. لا عائم.</para>
/// </summary>
public static class PlanCatalog
{
    /// <summary>الخططُ البنيوية الأربع — قيمُ اختبارٍ لا قائمةَ أسعار.</summary>
    public static readonly IReadOnlyList<PlanDefinition> Structural =
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

    /// <summary>
    /// يبذر الخططَ البنيوية في قاعدة التحكّم <b>حيث لا صفَّ لها</b> — مُحكَم وقابل لإعادة
    /// التشغيل، ولا يكتب فوق خطّةٍ قائمة: القاعدةُ هي المصدر (ADR-0092).
    /// </summary>
    /// <param name="c">اتصال مفتوح بقاعدة التحكّم.</param>
    /// <param name="publish">
    /// هل تُبذَر منشورةً؟ <c>true</c> في بيئات الاختبار التي تشترك عليها فوراً؛ والافتراض
    /// <c>false</c> لأن النشرَ بسعره فعلُ مشغّلٍ بسندٍ لا فعلُ بذرة.
    /// </param>
    /// <param name="ct">رمز الإلغاء.</param>
    public static async Task SeedAsync(NpgsqlConnection c, bool publish = false, CancellationToken ct = default)
    {
        var plans = Structural.OrderBy(p => p.Code, StringComparer.Ordinal).ToList();
        var values = string.Join(", ",
            plans.Select((_, i) => $"(@c{i}, @ar{i}, @en{i}, @m{i}, @u{i}, @n{i}, 'SAR', @pub, @pat, @pby)"));

        await Db.WriteIdempotentManyAsync(c, $"""
            insert into control.plan
                (plan_code, name_ar, name_en, monthly_price, per_user_price, included_users, currency,
                 published, published_at, published_by)
            values {values}
            on conflict (plan_code) do nothing
            """, plans.Count, p =>
            {
                p.AddWithValue("pub", publish);
                p.Add(Db.P("pat", publish ? Canon.Now() : DBNull.Value, NpgsqlDbType.TimestampTz));
                p.AddWithValue("pby", publish ? "seed:structural" : string.Empty);
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
    /// <summary>يُنشئ اشتراكاً فعّالاً لمستأجر على خطة.</summary>
    /// <param name="c">اتصال مفتوح بقاعدة التحكّم.</param>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="planCode">رمز الخطة.</param>
    /// <param name="startedOn">تاريخ بدء الاشتراك.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>معرّف الاشتراك.</returns>
    public static async Task<Guid> SubscribeAsync(NpgsqlConnection c, Guid tenantId, string planCode,
        DateOnly startedOn, CancellationToken ct = default)
    {
        // ‏لا اشتراكَ على خطّةٍ لم تُنشر: الخطّةُ غيرُ المنشورة لا سعرَ لها يُحتجّ به.
        var plan = await PlanDirectory.RequireAsync(c, planCode, ct);
        if (!plan.Published)
            throw new PlanNotPublishedException(planCode);

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
