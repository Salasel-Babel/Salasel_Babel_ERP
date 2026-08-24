using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Metering;

/// <summary>
/// مُسجّل القياس.
///
/// <para><b>ثلاث ضمانات، وكلها مُثبَتة في الإثباتات:</b></para>
/// <list type="number">
/// <item><b>لا عدّ مزدوج</b>: المفتاح الأساسي <c>(tenant_id, idempotency_key)</c>
/// مع <c>ON CONFLICT DO NOTHING</c>. الإحكام لكل حدث و<b>مستقلّ عن الترتيب</b>
/// — لا حارس يقارن تسلسلاً مُطبَّقاً بـ<c>&gt;</c> أو <c>&lt;</c> (فخ-13).</item>
/// <item><b>لا فقدان عند الانهيار</b>: تعذّر الكتابة يُحوِّل الدفعة إلى مخزن
/// محلّي مُثبَّت على القرص، يُصرَّف لاحقاً بنفس المفاتيح.</item>
/// <item><b>ترتيب أقفال ثابت</b>: صفوف كل دفعة مرتّبة بـ
/// <c>(tenant_id, idempotency_key)</c> قبل الإصدار (فخ-10).</item>
/// </list>
/// </summary>
public sealed class UsageRecorder(ControlPlaneOptions options, UsageSpool spool)
{
    /// <summary>المخزن الاحتياطي المحلّي الذي تُثبَّت فيه الأحداث حين تتعذّر قاعدة التحكّم.</summary>
    public UsageSpool Spool { get; } = spool;

    /// <summary>حجم الدفعة الواحدة. عبارة واحدة لكل دفعة — لا صفّاً صفّاً (فخ-14).</summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>يسجّل حدث قياس واحداً.</summary>
    /// <param name="e">الحدث.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>حصيلة التسجيل.</returns>
    public Task<RecordOutcome> RecordAsync(UsageEvent e, CancellationToken ct = default) =>
        RecordAsync([e], ct);

    /// <summary>
    /// يسجّل دفعة أحداث. <b>لا عدّ مزدوج تحت إعادة المحاولة:</b> مفتاح الإحكام
    /// الذي يورّده المنتِج مع <c>ON CONFLICT DO NOTHING</c> يجعل إعادة الدفعة
    /// نفسها بأي ترتيب تُرجِع مكرَّرات لا إدراجات.
    /// </summary>
    /// <param name="events">الأحداث.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>حصيلة التسجيل: المقبول والمكرَّر والمُثبَّت على القرص.</returns>
    public async Task<RecordOutcome> RecordAsync(IReadOnlyList<UsageEvent> events,
        CancellationToken ct = default)
    {
        if (events.Count == 0) return new RecordOutcome(0, 0, 0);

        var normalised = events.Select(x => x.Normalised())
            .OrderBy(x => x.TenantId).ThenBy(x => x.IdempotencyKey, StringComparer.Ordinal)
            .ToList();

        try
        {
            await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
            var accepted = 0;
            for (var off = 0; off < normalised.Count; off += BatchSize)
                accepted += await InsertBatchAsync(c,
                    normalised.Skip(off).Take(BatchSize).ToList(), ct);
            return new RecordOutcome(accepted, normalised.Count - accepted, 0);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or IOException)
        {
            // قاعدة التحكّم غير متاحة: لا يُسقَط الحدث، بل يُثبَّت على القرص.
            var n = Spool.Append(normalised);
            return new RecordOutcome(0, 0, n);
        }
    }

    private static async Task<int> InsertBatchAsync(NpgsqlConnection c,
        List<UsageEvent> batch, CancellationToken ct)
    {
        var values = string.Join(", ", batch.Select((_, i) =>
            $"(@t{i}, @k{i}, @p{i}, @m{i}, @u{i}, @e{i}, @q{i}, @o{i}, @r{i}, @s{i})"));

        return await Db.WriteIdempotentManyAsync(c, $"""
            insert into control.usage_event
                (tenant_id, idempotency_key, period_code, module_code, user_ref,
                 event_kind, quantity, occurred_at, recorded_at, source)
            values {values}
            on conflict (tenant_id, idempotency_key) do nothing
            """, batch.Count, p =>
            {
                var now = Canon.Now();
                for (var i = 0; i < batch.Count; i++)
                {
                    var e = batch[i];
                    p.Add(Db.P($"t{i}", e.TenantId, NpgsqlDbType.Uuid));
                    p.AddWithValue($"k{i}", e.IdempotencyKey);
                    p.AddWithValue($"p{i}", e.PeriodCode);
                    p.AddWithValue($"m{i}", e.ModuleCode);
                    p.Add(Db.P($"u{i}", e.UserRef, NpgsqlDbType.Text));
                    p.AddWithValue($"e{i}", e.EventKind);
                    p.Add(Db.Money($"q{i}", e.Quantity));      // numeric(19,4) — لا عائم
                    p.AddWithValue($"o{i}", e.OccurredAt);
                    p.AddWithValue($"r{i}", now);
                    p.AddWithValue($"s{i}", e.Source);
                }
            }, null, ct);
    }

    /// <summary>
    /// يُصرِّف المخزن المحلّي إلى قاعدة التحكّم. آمن للتشغيل مراراً، وآمن
    /// للمقاطعة: الملف لا يُحذف إلا بعد نجاح الإدراج كاملاً.
    /// </summary>
    public async Task<RecordOutcome> DrainSpoolAsync(CancellationToken ct = default)
    {
        var pending = Spool.ReadAll();
        if (pending.Count == 0) return new RecordOutcome(0, 0, 0);

        var ordered = pending
            .OrderBy(x => x.TenantId).ThenBy(x => x.IdempotencyKey, StringComparer.Ordinal)
            .ToList();

        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        var accepted = 0;
        for (var off = 0; off < ordered.Count; off += BatchSize)
            accepted += await InsertBatchAsync(c, ordered.Skip(off).Take(BatchSize).ToList(), ct);

        Spool.Clear();
        return new RecordOutcome(accepted, ordered.Count - accepted, 0);
    }

    /// <summary>يلتقط عيّنة تزامن — مادّة تعريف «المستخدم المتزامن» إن اختير.</summary>
    public async Task SampleConcurrencyAsync(Guid tenantId, string periodCode, int activeUsers,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        await Db.WriteAsync(c, """
            insert into control.concurrency_sample (tenant_id, period_code, sampled_at, active_users)
            values (@t, @p, @at, @n)
            on conflict (tenant_id, sampled_at) do update
               set active_users = greatest(control.concurrency_sample.active_users, excluded.active_users)
            """, 1, p =>
            {
                p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("p", periodCode);
                p.AddWithValue("at", Canon.Now());
                p.AddWithValue("n", activeUsers);
            }, null, ct);
    }
}
