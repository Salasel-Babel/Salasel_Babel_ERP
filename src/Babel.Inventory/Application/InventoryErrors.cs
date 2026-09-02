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

    /// <summary>
    /// تحويلٌ يتجاوز مدى العدد العشري — <b>يُمسَك ويُسمّى ولا يُترك يرمي</b>.
    /// <para>
    /// المقدار يعبر السلك بعشرين خانة صحيحة والبسط يبلغ ملياراً، وحاصلُهما يتجاوز مدى
    /// <c>decimal</c>. واستثناءٌ غير مُمسَك يخرج خطأَ خادم 500 — أي «عطلٌ عندنا» — وهو
    /// في الحقيقة مُدخَل مرفوض له علاجٌ يُقال.
    /// </para>
    /// </summary>
    public static Error ConversionOverflows(decimal magnitude, string ratio) => new(
        "inventory.unit_conversion_overflow",
        FormattableString.Invariant(
            $"تحويل المقدار {magnitude} بالمعامل {ratio} يتجاوز مدى العدد العشري المخزَّن. والرقم الذي لا يُمثَّل لا يُقرَّب ولا يُقتطع: أرسل مقداراً أصغر، أو أرسله بوحدةٍ أكبر."),
        FormattableString.Invariant(
            $"Converting magnitude {magnitude} by factor {ratio} exceeds the range of the stored decimal. A number that cannot be represented is neither rounded nor truncated: send a smaller magnitude, or send it in a larger unit."));

    /// <summary>
    /// تحويل بين صنفَي كمّية مختلفين — <b>وهذا ليس تحويلاً بل كثافة</b>.
    /// </summary>
    public static Error UnitClassMismatch(
        string fromUnit, string fromClass, string toUnit, string toClass) => new(
        "inventory.unit_class_mismatch",
        $"لا تحويل بين «{fromUnit}» وهي {ClassAr(fromClass)} و«{toUnit}» وهي {ClassAr(toClass)}. "
        + "والمعامل بين وحدتين من صنفٍ واحد واقعةٌ فيزيائية — الكيلوغرام ألف غرام دائماً — أمّا بين "
        + "صنفين مختلفين فهو **كثافةُ مادّة** لا معامل: «كم كيلوغراماً في اللتر؟» جوابه يختلف بين "
        + "الماء والزيت والرصاص، ويختلف للمادّة الواحدة بالحرارة. فلا يُسجَّل معاملٌ يبدو ثابتاً وهو "
        + "خاصّيةُ مادّة، ولا يُقرَّب.",
        $"There is no conversion between '{fromUnit}' ({ClassEn(fromClass)}) and '{toUnit}' ({ClassEn(toClass)}). "
        + "A factor between two units of one class is a physical fact — a kilogram is a thousand grams, always — "
        + "whereas between two classes it is a **material density**, not a factor: 'how many kilograms in a litre?' "
        + "is answered differently for water, oil and lead, and differently for one material at different "
        + "temperatures. A factor that looks constant but is a property of a substance is not recorded, and not rounded.");

    /// <summary>وحدة قياس غير مسجَّلة في سجلّ المنشأة.</summary>
    public static Error UnitNotRegistered(string unitCode) => new(
        "inventory.unit_not_registered",
        $"لا وحدة قياس بالرمز «{unitCode}» في سجلّ هذه المنشأة. "
        + "وسجلّ الوحدات يصف ولا يُبطل — فالحركة برمزٍ غير مسجَّل تمرّ — لكن **معامل التحويل لا "
        + "يُسجَّل بين رمزين لا يُعرَف صنف كمّيتهما**: التحويل بلا صنفٍ معلوم تقديرٌ لا حساب.",
        $"No unit of measure with code '{unitCode}' exists in this company's register. "
        + "The unit register describes rather than invalidates — a movement under an unregistered code still works — "
        + "but **a conversion factor is not recorded between two codes whose quantity class is unknown**: converting "
        + "without a known class is an estimate, not a computation.");

    /// <summary>وحدة قياس مُعطَّلة.</summary>
    public static Error UnitInactive(string unitCode) => new(
        "inventory.unit_inactive",
        $"وحدة القياس «{unitCode}» مُعطَّلة، فلا يُسجَّل عليها معامل جديد.",
        $"The unit of measure '{unitCode}' is deactivated, so no new factor is recorded on it.");

    /// <summary>رمز وحدة قياس مكرّر.</summary>
    public static Error DuplicateUnitCode(string code) => new(
        "inventory.duplicate_unit_code",
        $"رمز وحدة القياس «{code}» مستعمَل في هذه المنشأة. والرمز هوية تحملها كل حركة، فلا يتكرّر.",
        $"Unit of measure code '{code}' is already used in this company. The code is an identity carried by every movement, so it is never duplicated.");

    /// <summary>صنف كمّية خارج الأصناف المعلَنة.</summary>
    public static Error UnknownQuantityClass(string quantityClass, IReadOnlyList<string> known) => new(
        "inventory.unknown_quantity_class",
        $"صنف الكمّية «{quantityClass}» ليس من الأصناف المعلَنة: {string.Join(" · ", known)}. "
        + "والقائمة مغلقة عمداً: صنفان مكتوبان بحرفين مختلفين يبدوان مختلفين وهما واحد، فيُرفض تحويلٌ مشروع أو يُقبل تحويلٌ مستحيل.",
        $"Quantity class '{quantityClass}' is not one of the declared classes: {string.Join(" · ", known)}. "
        + "The list is closed on purpose: two classes spelled differently look different while being the same, so a legitimate conversion gets refused or an impossible one accepted.");

    /// <summary>معامل تحويل مكرّر بين الوحدتين نفسيهما.</summary>
    public static Error DuplicateUnitConversion(string fromUnit, string toUnit) => new(
        "inventory.duplicate_unit_conversion",
        $"يوجد معامل تحويل من «{fromUnit}» إلى «{toUnit}» في هذه المنشأة. "
        + "ومعاملان لزوجٍ واحد تعريفان متناقضان لواقعةٍ فيزيائية واحدة، ولا يُقرَّر أيّهما يُقرأ.",
        $"A conversion factor from '{fromUnit}' to '{toUnit}' already exists in this company. "
        + "Two factors for one pair are two contradictory definitions of one physical fact, and nothing decides which is read.");

    /// <summary>لا معامل مسجَّل بين الوحدتين.</summary>
    public static Error NoConversionBetween(string fromUnit, string toUnit) => new(
        "inventory.no_conversion_between_units",
        $"لا معامل تحويل مسجَّل من «{fromUnit}» إلى «{toUnit}» في هذه المنشأة. "
        + "ولا يُشتقّ معاملٌ بسلسلةٍ من معاملين: السلسلة تُنتج تحويلاً لم يقرّه أحد، وكسرُها الوسيط "
        + "يُقرَّب قبل أن يُضرب في الثاني. سجّل المعامل صراحةً.",
        $"No conversion factor from '{fromUnit}' to '{toUnit}' is registered in this company. "
        + "A factor is not derived by chaining two others: a chain produces a conversion nobody approved, and its "
        + "intermediate fraction gets rounded before it is multiplied by the second. Register the factor explicitly.");

    /// <summary>صنف كمّية بالعربية للرسائل.</summary>
    private static string ClassAr(string quantityClass) => quantityClass switch
    {
        "COUNT" => "عدد",
        "WEIGHT" => "وزن",
        "VOLUME" => "حجم",
        "LENGTH" => "طول",
        _ => "مساحة",
    };

    /// <summary>
    /// صنف كمّية بالإنجليزية — <b>نصّ تشخيصي يصحب رمزاً ثابتاً</b> لا نصّ عرض
    /// (‏ADR-0021 §6.2): قارئه مطوّرٌ يُصلح، والرمز المعروض هو <c>Class</c> نفسه.
    /// </summary>
    private static string ClassEn(string quantityClass) => quantityClass switch
    {
        "COUNT" => "count",
        "WEIGHT" => "weight",
        "VOLUME" => "volume",
        "LENGTH" => "length",
        _ => "area",
    };

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

    /// <summary>
    /// تغيير وحدة أساس صنفٍ كُتبت عليه حركات — <b>يُرفض</b>.
    /// <para>
    /// ورصيدٌ يتغيّر أساسه بعد أن كُتبت عليه حركات لا يُجمَع: مجموع حركاته جمعُ أعدادٍ
    /// بمقاييس مختلفة. والتصحيح بإفراغ الرصيد بمستند ثم إعادة إدخاله بالأساس الجديد.
    /// </para>
    /// </summary>
    public static Error BaseUnitLockedByHistory(string itemId, string baseUnit, long movements) => new(
        "inventory.base_unit_locked_by_history",
        FormattableString.Invariant(
            $"لا تتغيّر وحدة أساس الصنف «{itemId}» وهي «{baseUnit}»: كُتبت عليه {movements} حركة. ومجموعُ حركاته بعد التغيير جمعُ أعدادٍ بمقاييس مختلفة — رقمٌ صحيحٌ حسابياً وبلا معنىً. أفرِغ رصيده بمستند ثم أعِد إدخاله بالأساس الجديد، أو سجّل صنفاً جديداً."),
        FormattableString.Invariant(
            $"The base unit of item '{itemId}', which is '{baseUnit}', cannot be changed: {movements} movements have been written against it. After the change the sum of its movements would add numbers on different scales — arithmetically valid and meaningless. Empty its balance with a document and re-enter it on the new base, or register a new item."));

    /// <summary>
    /// وارد على صنفٍ مُعطَّل — <b>يُرفض، والصادر لا يُرفض</b>.
    /// <para>
    /// وذلك هو معنى إيقاف الصنف حرفياً: توقّف عن شرائه، وبِع ما بقي منه.
    /// </para>
    /// </summary>
    public static Error ItemInactive(string itemId) => new(
        "inventory.item_inactive",
        $"الصنف «{itemId}» مُعطَّل، فلا يُستلَم منه جديد. والصادر منه يبقى مسموحاً حتى ينفد رصيده — "
        + "وذلك معنى إيقافه: توقّف عن شرائه، وبِع ما بقي. أعِد تفعيله إن كان الإيقاف خطأً.",
        $"Item '{itemId}' is deactivated, so no new stock is received for it. Issuing it remains allowed until its "
        + "balance runs out — that is what deactivating an item means: stop buying it and sell the rest. Reactivate it "
        + "if the deactivation was a mistake.");

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

    // ── التسكين ──────────────────────────────────────────────────────────────

    /// <summary>موضع تسكين غير موجود في السجلّ.</summary>
    public static Error PlaceNotFound(string level, string code) => new(
        "inventory.storage_place_not_found",
        $"لا {LevelAr(level)} بالرمز «{code}» في سجلّ تسكين هذه المنشأة.",
        $"No {LevelEn(level)} with code '{code}' exists in this company's placement register.");

    /// <summary>موضع تسكين غير موجود، مطلوبٌ بمعرّفه.</summary>
    public static Error PlaceIdNotFound(string level, Guid id) => new(
        "inventory.storage_place_not_found",
        FormattableString.Invariant($"لا {LevelAr(level)} بالمعرّف {id} في سجلّ تسكين هذه المنشأة."),
        FormattableString.Invariant($"No {LevelEn(level)} with id {id} exists in this company's placement register."));

    /// <summary>رمز موضع مكرّر داخل مستواه.</summary>
    public static Error DuplicatePlaceCode(string level, string code) => new(
        "inventory.duplicate_storage_place_code",
        $"رمز {LevelAr(level)} «{code}» مستعمَل في هذه المنشأة. والرمز هوية تحملها حركات المخزون، فلا يتكرّر داخل مستواه.",
        $"The {LevelEn(level)} code '{code}' is already used in this company. The code is an identity carried by stock movements, so it is never duplicated within its level.");

    /// <summary>
    /// أبٌ مُعطَّل: موضعٌ يُسجَّل تحت موضعٍ لا يعمل.
    /// <para>
    /// والرفض هنا لا التعطيل المتسلسل: تعطيلُ الأب لا يجوز أصلاً وفيه رصيد، فإن عُطّل
    /// فقد خلا — وإضافةُ ابنٍ جديد تحت خالٍ مُعطَّل إحياءٌ من الباب الخلفي.
    /// </para>
    /// </summary>
    public static Error ParentPlaceInactive(string level, string code) => new(
        "inventory.storage_place_parent_inactive",
        $"{LevelArDefinite(level)} «{code}» مُعطَّل، فلا يُسجَّل تحته موضعٌ جديد. أعِد تفعيله أو اختر أباً عاملاً.",
        $"The {LevelEn(level)} '{code}' is deactivated, so no new place is registered under it. Reactivate it or choose an active parent.");

    /// <summary>
    /// تعطيل موضعٍ فيه رصيد — <b>يُمنع</b>.
    /// <para>
    /// <b>ولماذا يُمنع هنا ويُسمح على الصنف:</b> الموضع المُعطَّل لا يُنقَل منه ولا
    /// يُصرف، فالبضاعة تبقى فيه بقيمتها في الحساب الضابط <b>بلا بابٍ تخرج منه</b> —
    /// رقمٌ في الميزانية لا يقابله واقعٌ يُبلغ. والصنف المُعطَّل بخلافه يُصرف حتى ينفد.
    /// </para>
    /// </summary>
    public static Error PlaceStillHoldsStock(
        string level, string code, string itemId, decimal quantity, string unit) => new(
        "inventory.storage_place_still_holds_stock",
        FormattableString.Invariant(
            $"لا يُعطَّل {LevelAr(level)} «{code}» وفيه رصيد: الصنف «{itemId}» فيه {quantity} {unit}. والموضع المُعطَّل لا يُنقَل منه ولا يُصرف، فتبقى البضاعة بقيمتها في الحساب الضابط بلا بابٍ تخرج منه. انقل ما فيه إلى موضع آخر — أو أخرِجه بمستند — ثم عطّله."),
        FormattableString.Invariant(
            $"The {LevelEn(level)} '{code}' cannot be deactivated while it holds stock: item '{itemId}' has {quantity} {unit} in it. A deactivated place can neither be transferred from nor issued from, so the goods stay there at their value in the control account with no door out. Transfer its contents elsewhere — or issue them out on a document — then deactivate it."));

    /// <summary>تعطيل موضعٍ تحته مواضع عاملة.</summary>
    public static Error PlaceStillHasActiveChildren(string level, string code, int children) => new(
        "inventory.storage_place_has_active_children",
        FormattableString.Invariant(
            $"لا يُعطَّل {LevelAr(level)} «{code}» وتحته {children} موضعاً عاملاً. والتعطيل المتسلسل يُخفي ما عُطّل تبعاً عمّن عطّله، فلا يُعرف بعدها ما يُعاد تفعيله عند التراجع. عطّل ما تحته أولاً."),
        FormattableString.Invariant(
            $"The {LevelEn(level)} '{code}' cannot be deactivated while {children} active places sit under it. A cascading deactivation hides what was deactivated by consequence from whoever deactivated it, so nobody knows what to reactivate on a rollback. Deactivate its children first."));

    /// <summary>موضع مُعطَّل يُستعمل في حركة.</summary>
    public static Error PlaceInactive(string level, string code) => new(
        "inventory.storage_place_inactive",
        $"{LevelArDefinite(level)} «{code}» مُعطَّل، فلا تُسكَّن فيه بضاعة ولا تُنقَل إليه.",
        $"The {LevelEn(level)} '{code}' is deactivated, so no goods are placed in it and nothing is transferred into it.");

    /// <summary>
    /// موضعٌ لا يقع تحت الأب المذكور — <b>والمسار إفادةٌ تُصدَّق لا زينة</b>.
    /// <para>
    /// موضعان بالرمز نفسه تحت أبوين شيئان مختلفان. وقبولُ «‏A1» بلا سؤالٍ عن أبيه كان
    /// يُخرج الرفّ الصحيح من المبنى الخطأ، ويجعل العنوان يحمل معنىً لا يصدق.
    /// </para>
    /// </summary>
    public static Error PlaceNotUnderParent(string code, string actualParent, string expectedParent) => new(
        "inventory.storage_place_not_under_parent",
        $"الموضع «{code}» يقع تحت «{actualParent}» لا تحت «{expectedParent}». "
        + "والموضع يُقرأ بأبيه لا بمفرده: موضعان بالرمز نفسه تحت أبوين شيئان مختلفان.",
        $"Place '{code}' sits under '{actualParent}', not under '{expectedParent}'. "
        + "A place is read together with its parent: two places with the same code under two parents are two different things.");

    /// <summary>الاسم فارغ.</summary>
    public static Error NameMissing() => new(
        "inventory.name_missing",
        "الاسم فارغ. والاسم العربي هو السجلّ لا ترجمةٌ ثانية (‏ADR-0021)، فلا يُترك فارغاً.",
        "The name is empty. The Arabic name is the record, not a second translation (ADR-0021), so it is never left empty.");

    /// <summary>رقم نقل مكرّر.</summary>
    public static Error DuplicateTransferNumber(string number) => new(
        "inventory.duplicate_transfer_number",
        $"رقم مستند النقل «{number}» مستعمَل في هذه المنشأة.",
        $"Transfer document number '{number}' is already used in this company.");

    /// <summary>نقل إلى الموضع نفسه.</summary>
    public static Error TransferToSamePlace(string warehouseId, string locationId) => new(
        "inventory.transfer_to_same_place",
        $"المصدر والوجهة موضعٌ واحد: «{warehouseId}/{locationId}». والنقل إلى الموضع نفسه ليس نقلاً — "
        + "حركتان تُلغيان بعضهما وتُحدّثان صفّ رصيدٍ واحد مرّتين في معاملة واحدة.",
        $"Source and destination are one place: '{warehouseId}/{locationId}'. A transfer to the same place is not a transfer — "
        + "two movements that cancel each other and update one balance row twice inside a single transaction.");

    /// <summary>نقل بكمّية تتجاوز رصيد المصدر.</summary>
    public static Error TransferExceedsBalance(
        string itemId, string warehouseId, string locationId, decimal available, decimal requested, string unit) => new(
        "inventory.transfer_exceeds_balance",
        FormattableString.Invariant(
            $"النقل {requested} {unit} يتجاوز رصيد الصنف «{itemId}» في «{warehouseId}/{locationId}» وهو {available} {unit}. والصرف على رصيد سالب واقعةٌ تُوسَم وتُقبل لأن البيع قبل إدخال الاستلام يقع؛ أمّا **النقل** فيُحرّك بضاعةً بين رفّين فعلياً، ولا يُنقَل ما ليس موجوداً. سجّل الاستلام الناقص أولاً."),
        FormattableString.Invariant(
            $"Transferring {requested} {unit} exceeds the balance of item '{itemId}' at '{warehouseId}/{locationId}', which is {available} {unit}. Issuing against a negative balance is flagged and accepted because selling before the receipt is entered does happen; a **transfer**, though, physically moves goods between two shelves, and what does not exist is not moved. Record the missing receipt first."));

    /// <summary>اسم المستوى بالعربية للرسائل.</summary>
    private static string LevelAr(string level) => level switch
    {
        "WAREHOUSE" => "مستودع",
        "LOCATION" => "موقع",
        _ => "رفّ",
    };

    /// <summary>اسم المستوى بالعربية معرّفاً.</summary>
    private static string LevelArDefinite(string level) => level switch
    {
        "WAREHOUSE" => "المستودع",
        "LOCATION" => "الموقع",
        _ => "الرفّ",
    };

    /// <summary>
    /// اسم المستوى بالإنجليزية — <b>نصّ تشخيصي يصحب رمزاً ثابتاً</b>، لا نصّ عرض
    /// (‏ADR-0021 §6.2): قارئه مطوّرٌ يُصلح، والرمز <c>Code</c> هو ما تفرزه الواجهة.
    /// </summary>
    private static string LevelEn(string level) => level switch
    {
        "WAREHOUSE" => "warehouse",
        "LOCATION" => "location",
        _ => "bin",
    };

    /// <summary>تنسيق رقم للعرض داخل رسالة — ثابت الثقافة دائماً (‏فخ-38 · فخ-75).</summary>
    public static string Number(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
