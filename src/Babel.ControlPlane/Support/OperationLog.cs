using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Support;

/// <summary>نتيجة العملية كما تُكتب في سِرد العمليات.</summary>
public enum OperationOutcome
{
    /// <summary>سُمح بها ونُفِّذت.</summary>
    Allowed,

    /// <summary>رُفضت بقرار حارس — <b>وهذا هو السطر الذي لا يكتبه مخزن الأحداث</b>.</summary>
    Refused,

    /// <summary>أُخفقت لعطل لا لقرار.</summary>
    Failed,

    /// <summary>واقعة مُسجَّلة للأثر لا قرار وصول.</summary>
    Recorded
}

/// <summary>
/// سِرد العمليات — الجواب المباشر على فخ-08: مخزن الأحداث يسجّل ما <b>نجح</b>
/// فقط، والأثر المطلوب في أي تحقيق هو الأثر الغائب. كل رفض يُرجَع للنداء
/// يكتب سطراً <b>قبل</b> أن يُرجَع.
///
/// <para>القاعدة القابلة للفحص في هذا المشروع الفرعي: لا <c>throw</c> من
/// حارس استحقاق أو فحص أرشفة أو قاطع دارة قبل سطر هنا.</para>
/// </summary>
/// <param name="controlConnectionString">سلسلة الاتصال بقاعدة التحكّم.</param>
public sealed class OperationLog(string controlConnectionString)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>يكتب سطر سِرد باتصال خاص به.</summary>
    /// <param name="tenantId">المستأجر المعني، أو <c>null</c> لعملية على مستوى المنصّة.</param>
    /// <param name="actor">من نفّذ العملية.</param>
    /// <param name="operation">اسم العملية.</param>
    /// <param name="outcome">نتيجتها.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="payload">حمولة بنيوية تُحفَظ كـ<c>jsonb</c>.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    public async Task WriteAsync(Guid? tenantId, string actor, string operation,
        OperationOutcome outcome, string reasonAr, object? payload = null,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(controlConnectionString, ct);
        await WriteAsync(c, tenantId, actor, operation, outcome, reasonAr, payload, null, ct);
    }

    /// <summary>
    /// يكتب سطر سِرد على اتصال قائم، فيدخل معاملة النداء. يُستعمل حين يجب أن
    /// يُودَع سطر الرفض مع أثره أو لا يُودَع أيّهما.
    /// </summary>
    /// <param name="c">اتصال مفتوح بقاعدة التحكّم.</param>
    /// <param name="tenantId">المستأجر المعني، أو <c>null</c>.</param>
    /// <param name="actor">من نفّذ العملية.</param>
    /// <param name="operation">اسم العملية.</param>
    /// <param name="outcome">نتيجتها.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="payload">حمولة بنيوية.</param>
    /// <param name="tx">معاملة النداء إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    public static async Task WriteAsync(NpgsqlConnection c, Guid? tenantId, string actor,
        string operation, OperationOutcome outcome, string reasonAr, object? payload = null,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await Db.WriteAsync(c, """
            insert into control.operation_log
                (occurred_at, tenant_id, actor, operation, outcome, reason_ar, payload)
            values (@t, @tenant, @actor, @op, @outcome, @reason, @payload)
            """, 1, p =>
            {
                p.AddWithValue("t", Canon.Now());
                p.Add(Db.P("tenant", tenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("actor", actor);
                p.AddWithValue("op", operation);
                p.AddWithValue("outcome", outcome.ToString());
                p.AddWithValue("reason", reasonAr);
                p.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(payload ?? new { }, Json)
                });
            }, tx, ct);
    }

    /// <summary>يعدّ أسطر السِرد لعملية بنتيجة بعينها — به تُثبَت أن الرفض كُتب فعلاً.</summary>
    /// <param name="operation">اسم العملية.</param>
    /// <param name="outcome">النتيجة المطلوب عدّها.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>عدد الأسطر.</returns>
    public async Task<int> CountAsync(string operation, OperationOutcome outcome,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(controlConnectionString,
            "select count(*) from control.operation_log where operation = @op and outcome = @o",
            p => { p.AddWithValue("op", operation); p.AddWithValue("o", outcome.ToString()); }, ct));
}
