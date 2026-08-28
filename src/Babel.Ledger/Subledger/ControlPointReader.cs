using Babel.Contracts.Subledger;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.Subledger;

/// <summary>
/// محوّل نقطة الضبط: يقرأ من الدفتر صافي حركة سطور دفتر مساعد بعينه.
/// <para>
/// <b>ولماذا هنا الآن:</b> كان <see cref="IControlPointReader"/> منفذاً <b>معلَناً في
/// العقود ولا ينفّذه أحد في <c>src/</c></b>. تنفيذُه الوحيد كان في تجهيزات الاختبار
/// وفي أداة العرض، وقد كُتب في التجهيزة نصّاً أنّ «موضعه الطبيعي هو الجذر التركيبي…
/// وُضع هنا لأن الجذر ليس ملك ذلك التسليم — وهو بند في التقرير». وأثرُ غيابه أن
/// <c>ReceivablesService</c> و<c>PayablesService</c> و<c>InventoryValuationService</c>
/// <b>لم تكن قابلة للبناء في الخادم أصلاً</b> — ومعها <c>SalesInvoiceService</c> التي
/// تعتمد على <c>IInventoryValuation</c>. أي أن ثلاث خدمات وأربعة أسطر تسجيل كانت
/// خضراء في كل اختبار، ويسقط أول نداء حقيقي عليها عند حقن الاعتماديات.
/// </para>
/// <para>
/// <b>ولم يُكتشف ذلك قطّ لسببٍ واحد: لا باب HTTP كان يبلغ أياً منها.</b> والحاوية لا
/// تتحقّق من رسم بياني لا يطلبه أحد؛ فمسارٌ لا يُسلَك لا يُظهر تسجيلاً ناقصاً. وهذا
/// هو العطل نفسه الذي وصفته <c>tools/gate/run.sh --with-demo</c> في العرض التجريبي:
/// «الحلّ يُترجَم، والاختبارات خضراء، وأول تشغيل حقيقي يرمي عند حقن الاعتماديات».
/// </para>
/// <para>
/// <b>وموضعه الدفتر لا الجذر التركيبي:</b> الجذر ممنوع من حمل حزمة استمرارية أصلاً
/// (القاعدة 13 تفحص <c>Babel.Api.csproj</c> فترفض <c>Npgsql</c> و
/// <c>EntityFrameworkCore</c>)، وهذا الاستعلام يقرأ جداول الدفتر. فالدفتر يملك
/// الاستعلام، والعقد يملك الشكل، والجذر يصل بينهما بسطر تسجيل واحد لا يرى أيّاً منهما.
/// </para>
/// <para>
/// ولاحظ أنه <b>لا يسمّي حساباً</b>: الاستعلام على <c>subledger_kind</c> لا على رقم
/// حساب، فأي حساب ضابط يُضاف لاحقاً لهذا الدفتر المساعد يدخل المطابقة من تلقاء نفسه
/// (القاعدة 2).
/// </para>
/// <para>
/// <b>واتصال دور التطبيق وحده</b>، من <see cref="LedgerRuntime"/>: القراءة لا تحتاج
/// أكثر منه، واتصال المالك لا يدخل حاوية الاعتماديات إطلاقاً (ADR-0003).
/// </para>
/// </summary>
internal sealed class ControlPointReader : IControlPointReader
{
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>ينشئ المحوّل فوق موارد الدفتر.</summary>
    /// <param name="runtime">موارد الدفتر.</param>
    public ControlPointReader(LedgerRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _dataSource = runtime.DataSource;
    }

    /// <inheritdoc />
    public async ValueTask<Result<ControlPointSnapshot>> ReadAsync(
        TenantId tenant,
        string subledgerKind,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        List<ControlPointMovement> movements = [];
        decimal net = 0m;

        await using NpgsqlConnection connection = await _dataSource
            .OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select e.source_doc_type,
                   e.source_doc_id,
                   coalesce(l.subledger_party_id, ''),
                   sum(l.debit_company - l.credit_company)
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where l.company_id = $1
               and l.subledger_kind = $2
               and e.entry_date <= $3
             group by e.source_doc_type, e.source_doc_id, coalesce(l.subledger_party_id, '')
             order by e.source_doc_type, e.source_doc_id
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(subledgerKind);
        command.Parameters.AddWithValue(asOf);

        await using NpgsqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            decimal value = reader.GetDecimal(3);
            net += value;
            movements.Add(new ControlPointMovement(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), value));
        }

        return Result<ControlPointSnapshot>.Success(new ControlPointSnapshot(net, movements));
    }
}
