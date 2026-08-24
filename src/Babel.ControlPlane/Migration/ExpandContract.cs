using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Migration;

/// <summary>
/// انضباط التوسيع/الانكماش، بشيفرة تطبيق حقيقية بدل وصف.
///
/// <para><b>المشكلة:</b> عند طرح إصدار على أسطول من مئات القواعد، لا تُرقّى
/// كلها في لحظة واحدة. فبعض المستأجرين على الإصدار السابق بينما التطبيق
/// الجديد يعمل — و<b>قد تكون قاعدة العميل خلف جدار ناري ومغلقة وقت النشر</b>
/// (‏<c>02-architecture.md</c> §10.2 بند 1). فالتطبيق <b>ملزَم</b> بتحمّل
/// مستأجر على الإصدار السابق.</para>
///
/// <para><b>الحلّ في ثلاث مراحل، لا مرحلتين:</b>
/// (1) <b>توسيع</b>: يُضاف العمود الجديد ويُملأ رجعياً ويُربط بمُشغّل مزامنة —
/// العمودان صحيحان معاً؛
/// (2) <b>ترقية الشيفرة</b> على كل النُسخ؛
/// (3) <b>انكماش</b>: يُحذف العمود القديم. القفز من (1) إلى (3) هو الخطأ.</para>
///
/// <para>وشيفرة الإصدار الجديد <b>تقرأ إصدار مخطط المستأجر وتتكيّف</b>.
/// «سيكون الأسطول كلّه على نفس الإصدار» ليست خاصية بل أمنية.</para>
/// </summary>
public static class ExpandContract
{
    /// <summary>يقرأ الأعمدة الموجودة فعلاً — لا يُفترض إصدار من رقم مُخزَّن قد يتأخّر.</summary>
    public static async Task<(bool HasLegacy, bool HasNew)> ProbeAsync(NpgsqlConnection c,
        CancellationToken ct = default)
    {
        var cols = await Db.QueryAsync(c, """
            select column_name from information_schema.columns
             where table_schema = 'ledger' and table_name = 'journal_entry'
               and column_name in ('description_ar', 'memo_ar')
             order by column_name asc
            """, r => r.GetString(0), null, null, ct);
        return (cols.Contains("description_ar"), cols.Contains("memo_ar"));
    }

    /// <summary>
    /// شيفرة الإصدار <b>السابق</b>: تعرف <c>description_ar</c> وحده.
    /// القيد يُكتب برأسه وسطرَيه في <b>عبارة واحدة</b> — القيد بلا سطور يرفضه
    /// المُشغّل المؤجَّل عند الإيداع، وهو المطلوب.
    /// </summary>
    public static Task WriteWithOldCodeAsync(NpgsqlConnection c, Guid entryId, string bookCode,
        long entryNo, string periodCode, DateOnly date, string moduleCode, string textAr,
        CancellationToken ct = default) =>
        WriteAsync(c, "description_ar", "old-code", entryId, bookCode, entryNo, periodCode,
            date, moduleCode, textAr, ct);

    private static async Task WriteAsync(NpgsqlConnection c, string memoColumn, string actor,
        Guid entryId, string bookCode, long entryNo, string periodCode, DateOnly date,
        string moduleCode, string textAr, CancellationToken ct)
    {
        await Db.WriteAsync(c, $"""
            with head as (
                insert into ledger.journal_entry
                    (entry_id, book_code, entry_no, period_code, entry_date, module_code,
                     {memoColumn}, description_en, actor, posted_at)
                values (@id, @b, @n, @p, @d, @m, @ar, @en, @actor, @t)
                returning entry_id
            )
            insert into ledger.journal_line (line_id, entry_id, line_no, account_code, debit, credit)
            select gen_random_uuid(), (select entry_id from head), t.ord, t.code, t.debit, t.credit
              from (values ('1100', 100.0000::numeric, 0.0000::numeric, 1),
                           ('4100', 0.0000::numeric, 100.0000::numeric, 2))
                   as t(code, debit, credit, ord)
             order by t.code asc
            """, 2, p =>
            {
                p.Add(Db.P("id", entryId, NpgsqlDbType.Uuid));
                p.AddWithValue("b", bookCode);
                p.AddWithValue("n", entryNo);
                p.AddWithValue("p", periodCode);
                p.Add(Db.P("d", date, NpgsqlDbType.Date));
                p.AddWithValue("m", moduleCode);
                p.AddWithValue("ar", textAr);
                p.AddWithValue("en", actor == "old-code"
                    ? "posted by previous release" : "posted by new release");
                p.AddWithValue("actor", actor);
                p.AddWithValue("t", Canon.Now());
            }, null, ct);
    }

    public static async Task<string?> ReadWithOldCodeAsync(NpgsqlConnection c, Guid entryId,
        CancellationToken ct = default)
    {
        var rows = await Db.QueryAsync(c,
            "select description_ar from ledger.journal_entry where entry_id = @id",
            r => r.IsDBNull(0) ? null : r.GetString(0),
            p => p.Add(Db.P("id", entryId, NpgsqlDbType.Uuid)), null, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>
    /// شيفرة الإصدار <b>الجديد</b>: تفضّل <c>memo_ar</c>، وترتدّ إلى العمود
    /// القديم على مستأجر لم يُرقَّ بعد. الارتداد <b>شرط طرح</b> لا لطف.
    /// </summary>
    public static async Task WriteWithNewCodeAsync(NpgsqlConnection c, Guid entryId, string bookCode,
        long entryNo, string periodCode, DateOnly date, string moduleCode, string textAr,
        CancellationToken ct = default)
    {
        var (_, hasNew) = await ProbeAsync(c, ct);
        await WriteAsync(c, hasNew ? "memo_ar" : "description_ar", "new-code",
            entryId, bookCode, entryNo, periodCode, date, moduleCode, textAr, ct);
    }

    public static async Task<string?> ReadWithNewCodeAsync(NpgsqlConnection c, Guid entryId,
        CancellationToken ct = default)
    {
        var (_, hasNew) = await ProbeAsync(c, ct);
        var column = hasNew ? "memo_ar" : "description_ar";
        var rows = await Db.QueryAsync(c,
            $"select {column} from ledger.journal_entry where entry_id = @id",
            r => r.IsDBNull(0) ? null : r.GetString(0),
            p => p.Add(Db.P("id", entryId, NpgsqlDbType.Uuid)), null, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>يقرأ العمودين معاً — لإثبات أن المُشغّل يُبقيهما متطابقين أثناء التوسيع.</summary>
    public static async Task<(string? Legacy, string? Fresh)> ReadBothAsync(NpgsqlConnection c,
        Guid entryId, CancellationToken ct = default)
    {
        var (hasLegacy, hasNew) = await ProbeAsync(c, ct);
        if (!hasLegacy && !hasNew) return (null, null);
        var cols = (hasLegacy ? "description_ar" : "null") + ", " + (hasNew ? "memo_ar" : "null");
        var rows = await Db.QueryAsync(c,
            $"select {cols} from ledger.journal_entry where entry_id = @id",
            r => (r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1)),
            p => p.Add(Db.P("id", entryId, NpgsqlDbType.Uuid)), null, ct);
        return rows.Count == 0 ? (null, null) : rows[0];
    }
}
