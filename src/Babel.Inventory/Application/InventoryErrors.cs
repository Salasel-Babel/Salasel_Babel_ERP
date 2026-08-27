using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Inventory.Application;

/// <summary>
/// أخطاء وحدة المخزون — رمزٌ ثابت ورسالتان، والعربية أوّلاً لأن المحاسب يقرأ بالعربية
/// (‏ADR-0021).
/// <para>
/// وكل رفض هنا يُسمّي <b>ما الذي يجعله يمرّ</b>، لا «مرفوض» وحدها: الرسالة التي لا
/// تقول للمستخدم ما يفعله تُنتج مكالمة دعم لا تصحيحاً.
/// </para>
/// </summary>
internal static class InventoryErrors
{
    /// <summary>لا أساس تكلفة: يُصرَف صنفٌ لم يرد إلى هذا المستودع قط.</summary>
    public static Error NoCostBasis(string itemId, string warehouseId) => new(
        "inventory.no_cost_basis",
        $"لا أساس تكلفة للصنف «{itemId}» في المستودع «{warehouseId}»: لم يرد إليه قط، "
        + "فتكلفة صرفه ستكون رقماً مخترَعاً لا محسوباً. سجّل استلاماً بتكلفته، أو رصيداً "
        + "افتتاحياً للصنف، ثم أعد المحاولة.",
        $"No cost basis for item '{itemId}' in warehouse '{warehouseId}': it has never been "
        + "received there, so any issue cost would be invented rather than computed. Record a "
        + "receipt with its cost, or an opening balance for the item, then retry.");

    /// <summary>الكمية غير موجبة.</summary>
    public static Error QuantityNotPositive(decimal quantity) => new(
        "inventory.quantity_not_positive",
        FormattableString.Invariant($"الكمية {quantity} غير موجبة. اتجاه الحركة عمودٌ مستقلّ، فالكمية موجبة دائماً."),
        FormattableString.Invariant($"Quantity {quantity} is not positive. Direction is a separate field, so quantity is always positive."));

    /// <summary>حركة بهوية سبق تسجيلها بمحتوى مختلف.</summary>
    public static Error IdentityConflict(
        string documentType, string documentId, string eventCode, string recordedItem, string requestedItem) => new(
        "inventory.movement_identity_conflict",
        $"المستند «{documentType}/{documentId}» على الحدث «{eventCode}» سُجّلت له حركة مخزون "
        + $"للصنف «{recordedItem}»، والآن تُطلب حركة للصنف «{requestedItem}» بالهوية نفسها. "
        + "هوية الحركة هي هوية الترحيل، وهي لكل مستند لا لكل سطر — فمستندٌ بصنفين يحتاج "
        + "هوية أدقّ من هذه، ولا يُلتفّ عليها بحركة ثانية تنحرف عن حسابها الضابط بصمت.",
        $"Document '{documentType}/{documentId}' on event '{eventCode}' already has a stock "
        + $"movement for item '{recordedItem}', and a movement for item '{requestedItem}' is now "
        + "requested under the same identity. Movement identity is posting identity, which is per "
        + "document and not per line — a document with two items needs a finer identity, and must "
        + "not be worked around by a second movement that silently diverges from its control account.");

    /// <summary>كمية مختلفة بالهوية نفسها.</summary>
    public static Error QuantityConflict(
        string documentType, string documentId, string eventCode, decimal recorded, decimal requested) => new(
        "inventory.movement_quantity_conflict",
        FormattableString.Invariant(
            $"المستند «{documentType}/{documentId}» على الحدث «{eventCode}» سُجّلت له حركة بكمية {recorded}، والآن تُطلب {requested} بالهوية نفسها. الإعادة بالهوية نفسها لا تفعل شيئاً؛ أمّا كميةٌ مختلفة فتصحيحٌ، والتصحيح يكون بعكسٍ ثم بجيل تالٍ."),
        FormattableString.Invariant(
            $"Document '{documentType}/{documentId}' on event '{eventCode}' already has a movement of {recorded}, and {requested} is now requested under the same identity. A replay under the same identity does nothing; a different quantity is a correction, and a correction is made by a reversal followed by the next generation."));

    /// <summary>مرتجع بلا صرفٍ أصلي.</summary>
    public static Error OriginalIssueNotFound(string documentType, string documentId, string eventCode) => new(
        "inventory.original_issue_not_found",
        $"لا حركة صرف مسجَّلة للمستند «{documentType}/{documentId}» على الحدث «{eventCode}». "
        + "والمرتجع يُقيَّم بتكلفة صرفه الأصلي لا بتكلفة اليوم، فبلا الصرف الأصلي لا يوجد رقم يُقال.",
        $"No issue movement is recorded for document '{documentType}/{documentId}' on event "
        + $"'{eventCode}'. A return is valued at the cost of its original issue, not at today's cost, "
        + "so without that issue there is no number to state.");

    /// <summary>مرتجع يتجاوز صرفه.</summary>
    public static Error ReturnExceedsIssue(decimal issued, decimal alreadyReturned, decimal requested) => new(
        "inventory.return_exceeds_issue",
        FormattableString.Invariant(
            $"المرتجع {requested} يتجاوز ما بقي من الصرف الأصلي: صُرف {issued} ورُدّ منه {alreadyReturned}."),
        FormattableString.Invariant(
            $"Return of {requested} exceeds what remains of the original issue: {issued} issued, {alreadyReturned} already returned."));

    /// <summary>عبارة كتابة أصابت عدداً غير متوقَّع من الصفوف.</summary>
    public static Error UnexpectedRowCount(string statement, int expected, int actual) => new(
        "inventory.unexpected_row_count",
        FormattableString.Invariant(
            $"العبارة «{statement}» أصابت {actual} صفاً والمتوقَّع {expected}. وPostgreSQL يعدّ «أصبتُ صفر صفوف» نجاحاً، فالعدّ يُؤكَّد بعد كل كتابة."),
        FormattableString.Invariant(
            $"Statement '{statement}' affected {actual} rows where {expected} were expected. PostgreSQL treats 'affected zero rows' as success, so the count is confirmed after every write."));

    /// <summary>الفترة لا تُقفل: المخزون فيه ما لم يُحسم بعد.</summary>
    public static Error PeriodNotCloseable(string periodCode, IReadOnlyList<string> reasons) => new(
        "inventory.period_not_closeable",
        $"لا تُقفل الفترة «{periodCode}» على المخزون بحالته هذه:\n" + string.Join('\n', reasons)
        + "\nكلّ بند أعلاه رقمٌ في الميزانية لا يقابله واقع في المستودع. "
        + "يُحسم بمستند — استلام متأخّر، أو تسوية جرد، أو إعدام — لا بإقفالٍ يمرّ فوقه.",
        $"Period '{periodCode}' cannot be closed for inventory in this state:\n" + string.Join('\n', reasons)
        + "\nEach item above is a balance-sheet figure with no matching reality in the warehouse. "
        + "It is settled by a document — a late receipt, a count adjustment, or a write-off — not by a "
        + "close that passes over it.");

    /// <summary>قراءة نقطة الضبط تعذّرت.</summary>
    public static Error ControlPointUnavailable(IReadOnlyList<Error> causes) => new(
        "inventory.control_point_unavailable",
        "تعذّرت قراءة الحساب الضابط للمخزون، فلا مطابقة: " + string.Join(" · ", causes.Select(static e => e.Code)),
        "The inventory control account could not be read, so no reconciliation is possible: "
        + string.Join(" · ", causes.Select(static e => e.Code)));

    /// <summary>تنسيق رقم للعرض داخل رسالة — ثابت الثقافة دائماً (‏فخ-38 · فخ-75).</summary>
    public static string Number(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
