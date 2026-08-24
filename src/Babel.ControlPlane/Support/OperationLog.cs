using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Support;

public enum OperationOutcome { Allowed, Refused, Failed, Recorded }

/// <summary>
/// سِرد العمليات — الجواب المباشر على فخ-08: مخزن الأحداث يسجّل ما <b>نجح</b>
/// فقط، والأثر المطلوب في أي تحقيق هو الأثر الغائب. كل رفض يُرجَع للنداء
/// يكتب سطراً <b>قبل</b> أن يُرجَع.
///
/// <para>القاعدة القابلة للفحص في هذا المشروع الفرعي: لا <c>throw</c> من
/// حارس استحقاق أو فحص أرشفة أو قاطع دارة قبل سطر هنا.</para>
/// </summary>
public sealed class OperationLog(string controlConnectionString)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task WriteAsync(Guid? tenantId, string actor, string operation,
        OperationOutcome outcome, string reasonAr, object? payload = null,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(controlConnectionString, ct);
        await WriteAsync(c, tenantId, actor, operation, outcome, reasonAr, payload, null, ct);
    }

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

    public async Task<int> CountAsync(string operation, OperationOutcome outcome,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(controlConnectionString,
            "select count(*) from control.operation_log where operation = @op and outcome = @o",
            p => { p.AddWithValue("op", operation); p.AddWithValue("o", outcome.ToString()); }, ct));
}
