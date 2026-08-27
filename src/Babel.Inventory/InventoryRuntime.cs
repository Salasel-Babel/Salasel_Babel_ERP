using Babel.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory;

/// <summary>
/// موارد وحدة المخزون المشتركة داخل نطاق الطلب.
/// <para>
/// النوع عام لأن الحاوية تحقنه، وأعضاؤه <c>internal</c> لأن جداول الوحدة لا تعبر
/// حدّها (القاعدة 5). نفس شكل <c>PurchasingRuntime</c> وللسبب نفسه.
/// </para>
/// </summary>
public sealed class InventoryRuntime : IDisposable
{
    private readonly InventoryDbContext _database;

    /// <summary>ينشئ الموارد من الإعدادات.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    public InventoryRuntime(InventoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _database = Build(options);
    }

    internal InventoryOptions Options { get; }

    internal InventoryDbContext Database => _database;

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    internal static InventoryDbContext Build(InventoryOptions options)
        => new(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(options.ConnectionString).Options);
}

/// <summary>
/// ناشر مخطّط المخزون — خارج حاوية الاعتماديات عمداً: نشر المخطّط عملية مالك.
/// <para>
/// ‏<c>EnsureCreated</c> ينشئ الشكل الحالي كاملاً في قاعدة فارغة <b>ولا يفعل شيئاً في
/// قاعدة قائمة</b>؛ ونصوص الترقية — حين تُوجد — تُشغَّل بعده بالترتيب وكلٌّ منها مكتوب
/// ليُعاد تشغيله بلا أثر. ولا نصّ ترقية اليوم لأن هذا هو الشكل الأول للمخطّط.
/// </para>
/// <para>
/// <b>ولا هجرة تكتب في جدول حركة أو رصيد مضى</b>: ما مضى واقعةٌ سُجّلت، والتصحيح
/// بحركة مضادّة — وهو الشرط نفسه الذي جعل هجرةً «تُصلح» عموداً تُتلف دفتراً سليماً
/// (‏<c>docs/evidence/traps.md#fakh-71</c>).
/// </para>
/// </summary>
public static class InventorySchemaDeployer
{
    /// <summary>ينشئ مخطّط <c>inventory</c> وجداوله إن لم توجد.</summary>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task DeployAsync(InventoryOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using InventoryDbContext database = InventoryRuntime.Build(options);
        await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
