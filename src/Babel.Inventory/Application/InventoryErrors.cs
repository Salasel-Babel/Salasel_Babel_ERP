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

    /// <summary>
    /// مرتجع بلا حركةٍ أصلية.
    /// <para>
    /// <b>و«حركة» لا «صرف»:</b> يُرَدّ على الصرف بضاعةٌ تعود من العميل، ويُرَدّ على
    /// الاستلام بضاعةٌ تخرج إلى المورد. ونصٌّ يقول «صرف» في الحالتين كان يُرسل قارئه
    /// يبحث عن مستند لا وجود له في مساره.
    /// </para>
    /// </summary>
    public static Error OriginalIssueNotFound(string documentType, string documentId, string eventCode) => new(
        "inventory.original_issue_not_found",
        $"لا حركة مخزون مسجَّلة للمستند «{documentType}/{documentId}» على الحدث «{eventCode}». "
        + "والمرتجع يُقيَّم بتكلفة حركته الأصلية لا بتكلفة اليوم، فبلا تلك الحركة لا يوجد رقم يُقال.",
        $"No stock movement is recorded for document '{documentType}/{documentId}' on event "
        + $"'{eventCode}'. A return is valued at the cost of its original movement, not at today's cost, "
        + "so without that movement there is no number to state.");

    /// <summary>إلغاء حركة لا وجود لها.</summary>
    public static Error OriginalMovementNotFound(string documentType, string documentId, string eventCode) => new(
        "inventory.original_movement_not_found",
        $"لا حركة مخزون مسجَّلة للمستند «{documentType}/{documentId}» على الحدث «{eventCode}»، "
        + "فلا شيء يُلغى. والإلغاء يُبطل واقعةً مُسجَّلة بقيمتها هي، فبلا تلك الواقعة لا يوجد رقم يُقال.",
        $"No stock movement is recorded for document '{documentType}/{documentId}' on event '{eventCode}', "
        + "so there is nothing to reverse. A reversal annuls a recorded fact at its own value; without that fact "
        + "there is no number to state.");

    /// <summary>إلغاء حركة رُدّ عليها سلفاً — الإلغاء الكامل بعد ردٍّ جزئي يُخرج البضاعة مرّتين.</summary>
    public static Error MovementAlreadyReturned(
        string documentType, string documentId, string eventCode, decimal returned) => new(
        "inventory.movement_already_returned",
        FormattableString.Invariant(
            $"حركة المستند «{documentType}/{documentId}» على الحدث «{eventCode}» رُدّ عليها {returned} سلفاً، فلا تُلغى بكاملها: الإلغاء يُعيد الكمية كلّها فتُعاد البضاعة مرّتين. عالِج المرتجع القائم أولاً، أو أصدر إشعاراً دائناً بالباقي بدل العكس."),
        FormattableString.Invariant(
            $"The movement of document '{documentType}/{documentId}' on event '{eventCode}' already has {returned} returned against it, so it cannot be reversed in full: a reversal restores the whole quantity and the goods would return twice. Settle the existing return first, or issue a credit note for the remainder instead of reversing."));

    /// <summary>مرتجع يتجاوز صرفه.</summary>
    public static Error ReturnExceedsIssue(decimal issued, decimal alreadyReturned, decimal requested) => new(
        "inventory.return_exceeds_issue",
        FormattableString.Invariant(
            $"المرتجع {requested} يتجاوز ما بقي من الصرف الأصلي: صُرف {issued} ورُدّ منه {alreadyReturned}."),
        FormattableString.Invariant(
            $"Return of {requested} exceeds what remains of the original issue: {issued} issued, {alreadyReturned} already returned."));

    /// <summary>كمّية بلا وحدة — وهي ليست كمّية.</summary>
    public static Error UnitMissing() => new(
        "inventory.unit_missing",
        "الكمّية بلا وحدة قياس. و«عشرة» ليست معلومة: عشر حبّات أم عشر كراتين؟ "
        + "أرسل رمز الوحدة مع المقدار — وحدة أساس الصنف أو وحدةً لها معامل تحويل إليها.",
        "The quantity carries no unit of measure. 'Ten' is not information: ten pieces or ten cartons? "
        + "Send the unit code with the magnitude — the item's base unit, or a unit that has a conversion factor to it.");

    /// <summary>وحدة لا معامل لها إلى وحدة أساس الرصيد.</summary>
    public static Error UnitNotConvertible(string itemId, string unit, string baseUnit) => new(
        "inventory.unit_not_convertible",
        $"لا معامل تحويل من الوحدة «{unit}» إلى وحدة أساس الصنف «{itemId}» وهي «{baseUnit}». "
        + "وخلطُ وحدتين بلا معامل تقديرٌ لا حساب، ولذلك يُرفض ولا يُقرَّب. "
        + "سجّل معامل التحويل على الصنف، أو أرسل الكمّية بوحدة الأساس.",
        $"There is no conversion factor from unit '{unit}' to the base unit '{baseUnit}' of item '{itemId}'. "
        + "Mixing two units without a factor is an estimate, not a computation, so it is refused rather than rounded. "
        + "Register the conversion factor on the item, or send the quantity in the base unit.");

    /// <summary>تحويل لا يقع بلا باقٍ.</summary>
    public static Error ConversionNotExact(decimal magnitude, string ratio) => new(
        "inventory.unit_conversion_not_exact",
        FormattableString.Invariant(
            $"تحويل المقدار {magnitude} بالمعامل {ratio} لا يقع بلا باقٍ، فالناتج كسرٌ يُقرَّب. والتقريب في كمّية تُضرب في تكلفة الوحدة يدخل المال ويتراكم على كل حركة. أرسل مقداراً يقبل القسمة على مقام المعامل، أو أرسله بوحدة الأساس."),
        FormattableString.Invariant(
            $"Converting magnitude {magnitude} by factor {ratio} does not divide exactly, so the result is a rounded fraction. Rounding a quantity that is multiplied by a unit cost reaches the money and accumulates on every movement. Send a magnitude divisible by the factor's denominator, or send it in the base unit."));

    /// <summary>معامل تحويل غير موجب.</summary>
    public static Error UnitRatioNotPositive(string ratio) => new(
        "inventory.unit_ratio_not_positive",
        $"معامل التحويل «{ratio}» غير موجب. والمقام الصفري ليس معاملاً، والبسط غير الموجب يقلب اتجاه الكمّية بصمت.",
        $"The conversion factor '{ratio}' is not positive. A zero denominator is not a factor, and a non-positive numerator silently reverses the direction of the quantity.");

    /// <summary>حركة تُسلَّم بوحدة تخالف أساس الصنف المُسجَّل.</summary>
    public static Error BaseUnitMismatch(string itemId, string balanceBase, string itemBase) => new(
        "inventory.base_unit_mismatch",
        $"رصيد الصنف «{itemId}» مُمسَك بوحدة الأساس «{balanceBase}»، وكتالوج الأصناف يقول إن أساسه «{itemBase}». "
        + "ورصيدٌ يتغيّر أساسه بعد أن كُتبت عليه حركات لا يُجمَع: مجموعُ حركاته جمعُ أعدادٍ بمقاييس مختلفة. "
        + "التصحيح بإفراغ الرصيد بمستند ثم إعادة إدخاله بالأساس الجديد، لا بتبديل العمود.",
        $"The balance of item '{itemId}' is held in base unit '{balanceBase}', while the item catalogue states its base is "
        + $"'{itemBase}'. A balance whose base changes after movements have been written against it cannot be summed: the sum "
        + "of its movements would add numbers on different scales. Correct it by emptying the balance with a document and "
        + "re-entering it on the new base, not by swapping the column.");

    /// <summary>صنف غير موجود في الكتالوج.</summary>
    public static Error ItemNotFound(string itemId) => new(
        "inventory.item_not_found",
        $"لا صنف بالرمز «{itemId}» في كتالوج هذه المنشأة.",
        $"No item with code '{itemId}' exists in this company's catalogue.");

    /// <summary>رمز صنف مكرّر.</summary>
    public static Error DuplicateItemCode(string code) => new(
        "inventory.duplicate_item_code",
        $"رمز الصنف «{code}» مستعمَل في هذه المنشأة. والرمز هوية تحملها الحركات والقيود، فلا يتكرّر.",
        $"Item code '{code}' is already used in this company. The code is an identity carried by movements and entries, so it is never duplicated.");

    /// <summary>رقم مستند مكرّر.</summary>
    public static Error DuplicateDocumentNumber(string number) => new(
        "inventory.duplicate_document_number",
        $"رقم المستند «{number}» مستعمَل في هذه المنشأة.",
        $"Document number '{number}' is already used in this company.");

    /// <summary>مستند غير موجود.</summary>
    public static Error DocumentNotFound(string documentType, Guid documentId) => new(
        "inventory.document_not_found",
        FormattableString.Invariant($"لا مستند «{documentType}» بالمعرّف {documentId} في هذه المنشأة."),
        FormattableString.Invariant($"No '{documentType}' document with id {documentId} exists in this company."));

    /// <summary>المستند ليس في الحالة المطلوبة.</summary>
    public static Error NotInState(string number, string actual, string expected) => new(
        "inventory.wrong_state",
        $"المستند «{number}» حالته «{actual}» والمطلوب «{expected}».",
        $"Document '{number}' is in state '{actual}' where '{expected}' is required.");

    /// <summary>وارد بلا تكلفة.</summary>
    public static Error ReceiptCostNotPositive(decimal cost) => new(
        "inventory.receipt_cost_not_positive",
        FormattableString.Invariant(
            $"تكلفة الوارد {cost} غير موجبة. والوارد بلا تكلفة يُنشئ أساس تكلفة قيمته صفر، فيصير كل صرفٍ بعده بصفر — رقمٌ صحيحٌ حسابياً وخاطئٌ محاسبياً. أدخل تكلفة الكمية الواردة كلّها."),
        FormattableString.Invariant(
            $"The receipt cost {cost} is not positive. A receipt without cost creates a zero cost basis, so every later issue is valued at zero — arithmetically consistent and accounting-wise wrong. Enter the cost of the whole received quantity."));

    /// <summary>ترحيل رُفض من الدفتر.</summary>
    public static Error PostingRefused(IReadOnlyList<Error> causes) => new(
        "inventory.posting_refused",
        "رفض الدفتر ترحيل مستند المخزون: " + string.Join(" · ", causes.Select(static e => e.MessageAr)),
        "The ledger refused to post the inventory document: " + string.Join(" · ", causes.Select(static e => e.MessageEn)));

    /// <summary>طلب ترحيل بلا رمز حدث.</summary>
    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "inventory.missing_event_code",
        FormattableString.Invariant($"مستند «{documentType}» بالمعرّف {documentId} بلا رمز حدث — وحدثان بلا رمز هويةٌ واحدة."),
        FormattableString.Invariant($"Document '{documentType}' with id {documentId} carries no event code; two events without a code share one identity."));

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
