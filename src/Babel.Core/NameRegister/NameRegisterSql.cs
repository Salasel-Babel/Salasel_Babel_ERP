using System.Globalization;
using Babel.Contracts.Lookup;
using Npgsql;

namespace Babel.Core.NameRegister;

/// <summary>
/// <b>نصّ الاستعلام وربطُه — بناءٌ واحد يقرؤه المحوّلان، لا نسختان.</b>
/// <para>
/// انقسم المحوّل إلى اثنين (سبرٌ وجَرد) كي لا يحمل كائنٌ واحد الوجهين معاً، <b>ولا
/// يجوز أن يُدفَع ثمنُ الانقسام نسختين من شرط النطاق</b>: نسختان تنحرف إحداهما،
/// والانحراف هنا يعني سجلّاً يُطابَق بلا منشأة. فالشرط هنا مرّةً واحدة، والملفّان
/// يقرآنه.
/// </para>
/// </summary>
internal static class NameRegisterSql
{
    /// <summary>
    /// الشرط المشترك: النطاق، ثم السريان، ثم المطابقة على المفتاحين.
    /// <para>
    /// <b>ومطابقة التشابه تُكتب بالمعامل <c>%</c> قبل الدالّة — وذلك هو الفرق بين
    /// فهرسٍ يعمل وفهرسٍ يُكتب فيه ولا يُقرأ منه.</b> كان الشرط
    /// ‏<c>similarity(search_key, …) &gt;= @threshold</c> وحده: نداءُ دالّة لا معاملٌ
    /// يفهمه <c>gin_trgm_ops</c>، فكان فهرس GIN المبنيّ في الهجرة <b>لا يُستعمل في
    /// أي خطّة</b> — والمسح كامل، والهجرة تحرس غيابَ فهرسٍ عن مسحٍ كامل واقعٍ أصلاً،
    /// و<c>set local pg_trgm.similarity_threshold</c> شيفرةٌ ميّتة لأن <c>%</c> لم
    /// يكن يُستعمل. والمعامل والدالّة معاً لا أحدهما: العتبة تُضبط للمعامل، والشرط
    /// الصريح يبقى مرساةَ الصحّة لو لم يسرِ الضبط.
    /// </para>
    /// </summary>
    /// <param name="table">وصف الجدول.</param>
    public static string Where(NameRegisterTable table)
    {
        string scope = string.Join(
            " and ",
            table.ScopeColumns.Select(static (column, index) =>
                NameRegisterTable.Quote(column)
                + " = @scope"
                + index.ToString(CultureInfo.InvariantCulture)));

        string active = table.ActiveColumn is null
            ? string.Empty
            : " and " + NameRegisterTable.Quote(table.ActiveColumn);

        // ‏**الدور شرطٌ في النطاق لا مِصفاةٌ بعده**: جدولٌ يتقاسمه المستأجر والمالك
        // يُطابِق الطرفَ الخطأ لو سقط هذا السطر، ويُصدر له مِقبضاً صحيحاً.
        string role = table.RoleColumn is null
            ? string.Empty
            : " and " + NameRegisterTable.Quote(table.RoleColumn) + " = @role";

        return " where " + scope + active + role
            + " and ((search_key % babel.fold_arabic(@text)"
            + " and similarity(search_key, babel.fold_arabic(@text)) >= @threshold)"
            + " or search_key_tight = babel.fold_arabic_tight(@text))";
    }

    /// <summary>
    /// استعلام السبر. <b>‏<c>limit 2</c> هو الحارس</b>، و<c>order by</c> على المعرّف
    /// ليكون الجواب حتمياً عند الصفّ الواحد — ولا ترتيب بالدرجة، فلا «أفضل تطابق» هنا.
    /// </summary>
    /// <param name="table">وصف الجدول.</param>
    public static string Probe(NameRegisterTable table)
        => "select " + NameRegisterTable.Quote(table.IdColumn)
        + " from " + table.QualifiedName
        + Where(table)
        + " order by " + NameRegisterTable.Quote(table.IdColumn)
        + " limit 2";

    /// <summary>استعلام الورقة — <b>يُعيد أسماءً، ولا يُستدعى في بناء رسالةٍ لنموذج</b>.</summary>
    /// <param name="table">وصف الجدول.</param>
    public static string Sheet(NameRegisterTable table)
        => "select " + NameRegisterTable.Quote(table.IdColumn)
        + ", " + NameRegisterTable.Quote(table.NameColumn)
        + ", " + (table.SubtitleColumn is null ? "null::text" : NameRegisterTable.Quote(table.SubtitleColumn))
        + " from " + table.QualifiedName
        + Where(table)
        + " order by " + NameRegisterTable.Quote(table.NameColumn)
        + ", " + NameRegisterTable.Quote(table.IdColumn)
        + " limit @cap";

    /// <summary>
    /// يضبط عتبة المعامل <c>%</c> داخل المعاملة بـ<c>SET LOCAL</c>، فتعود عند الإيداع
    /// ولا تلوّث اتصالاً مُعاداً من المجمّع.
    /// </summary>
    /// <param name="connection">الاتصال.</param>
    /// <param name="transaction">المعاملة.</param>
    /// <param name="threshold">العتبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task SetThresholdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        decimal threshold,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand limit = new(
            "set local pg_trgm.similarity_threshold = "
            + threshold.ToString("0.####", CultureInfo.InvariantCulture),
            connection,
            transaction);

        await limit.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يربط الوسائط. <b>والترتيب مشتقٌّ لا مُمرَّر</b>: <see cref="NameRegisterTable"/>
    /// يبني <c>ScopeColumns</c> من عمودَيه المُسمّيَين، فلا يوجد ترتيبٌ يُخطئ أحدٌ في
    /// كتابته ولا يُربط هنا عمودٌ بموضعه ويُقارَن بقيمةٍ من غير جنسه صامتاً.
    /// </summary>
    /// <param name="command">الأمر.</param>
    /// <param name="table">وصف الجدول.</param>
    /// <param name="threshold">العتبة.</param>
    /// <param name="request">السؤال.</param>
    public static void Bind(
        NpgsqlCommand command,
        NameRegisterTable table,
        decimal threshold,
        NameCandidateRequest request)
    {
        command.Parameters.AddWithValue("text", request.Text);
        command.Parameters.AddWithValue("threshold", (float)threshold);
        command.Parameters.AddWithValue("scope0", request.Tenant.Value);

        if (table.CompanyColumn is not null)
        {
            command.Parameters.AddWithValue("scope1", request.CompanyId);
        }

        if (table.RoleValue is not null)
        {
            command.Parameters.AddWithValue("role", table.RoleValue);
        }
    }
}
