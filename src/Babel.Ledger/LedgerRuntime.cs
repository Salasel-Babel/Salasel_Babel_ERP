using Babel.Ledger.PostingMatrix;
using Npgsql;

namespace Babel.Ledger;

/// <summary>
/// موارد الدفتر المشتركة: مصدر اتصال <b>دور التطبيق</b> ولقطة البيانات المرجعية.
/// <para>
/// النوع عام لأن الحاوية تحقنه في مُنشئ عام، وأعضاؤه <c>internal</c> لأن ما بداخله
/// — الحسابات وخريطة الأدوار — لا يعبر حدّ الدفتر (القاعدة 2). أي أن الجذر
/// التركيبي يستطيع أن <b>يمرّره</b> ولا يستطيع أن <b>يقرأ منه</b>.
/// </para>
/// </summary>
public sealed class LedgerRuntime : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly LedgerReferenceCache _reference;

    /// <summary>ينشئ موارد الدفتر من إعداداته.</summary>
    public LedgerRuntime(LedgerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _dataSource = new NpgsqlDataSourceBuilder(options.AppConnectionString).Build();
        _reference = new LedgerReferenceCache(_dataSource);
    }

    internal LedgerOptions Options { get; }

    internal NpgsqlDataSource DataSource => _dataSource;

    internal LedgerReferenceCache Reference => _reference;

    /// <summary>يُسقط لقطة شركة بعد تغيير يملكه المالك (بذر حسابات، إقفال فترة).</summary>
    public void InvalidateReference(Guid companyId) => _reference.Invalidate(companyId);

    /// <inheritdoc />
    public void Dispose()
    {
        _reference.Dispose();
        _dataSource.Dispose();
    }
}
