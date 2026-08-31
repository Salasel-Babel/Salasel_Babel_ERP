namespace Babel.Inventory.Application;

/// <summary>رصيد صنف في مستودع، كما يدخل حسابَ التكلفة وكما يخرج منه.</summary>
/// <param name="Quantity">الكمية — قد تكون سالبة.</param>
/// <param name="Value">القيمة بمقياس 4.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك بمقياس 6.</param>
/// <param name="HasCostBasis">هل وردت هذه التركيبة مرّةً بتكلفة؟</param>
/// <param name="BaseUnit">
/// وحدة الأساس التي يُمسَك بها هذا الرصيد، أو فراغٌ إن لم يُنشأ الصفّ بعد.
/// <b>وفراغٌ هنا يعني «لم تُثبَّت وحدة بعد» لا «لا وحدة»</b>: أول حركة تُثبّتها.
/// </param>
internal readonly record struct StockPosition(
    decimal Quantity, decimal Value, decimal UnitCost, bool HasCostBasis, string BaseUnit);

/// <summary>أثر حركة على الرصيد.</summary>
/// <param name="Value">قيمة الحركة — موجبة دائماً.</param>
/// <param name="UnitCost">تكلفة الوحدة المستعملة في هذه الحركة.</param>
/// <param name="After">الرصيد بعد الحركة.</param>
/// <param name="DrewOnNegativeStock">هل صارت الكمية سالبة بهذه الحركة أو كانت كذلك؟</param>
internal readonly record struct StockEffect(
    decimal Value, decimal UnitCost, StockPosition After, bool DrewOnNegativeStock);

/// <summary>
/// حساب المتوسط المرجّح المتحرّك — <b>دالّة صافية، بلا قاعدة بيانات وبلا ساعة</b>.
/// <para>
/// طريقة التقييم المعتمدة في هذا المنتج، وحجّتها في
/// <c>docs/decisions/ADR-0039-moving-weighted-average-inventory-valuation.md</c>.
/// وموضعها هنا — منفصلةً عن الاستمرارية — كي تُختبر حالاتُ الحدّ بلا قاعدة بيانات:
/// الصرف الذي يُفرغ الرصيد بالضبط، والصرف على رصيد سالب، والوارد الذي يهبط على سالب.
/// </para>
/// </summary>
internal static class WeightedAverageCost
{
    /// <summary>رمز الطريقة كما يُكتب على كل حركة. لا يُشتقّ ولا يُترجَم — هو معرّف.</summary>
    public const string MethodCode = "moving_weighted_average";

    /// <summary>مقياس القيمة — مقياس المال في كل النطاق.</summary>
    public const int ValueScale = 4;

    /// <summary>
    /// مقياس تكلفة الوحدة — <b>ستّ خانات لا أربع</b>.
    /// <para>
    /// صنفٌ يُشترى بألف حبّة بمئة ريال تكلفة وحدته <c>0.100000</c>؛ وبمقياس أربعة
    /// تصير <c>0.1000</c> والفرق لا يظهر. لكن مقياساً أضيق يُنتج انحرافاً يتراكم على
    /// كل صرف، ورصيد قيمةٍ لا يساوي مجموع حركاته — أي دفتراً مساعداً ينحرف عن حسابه
    /// الضابط بسبب تقريب، وهو ما تعدّه هذه الوثيقة عيباً من الدرجة الأولى.
    /// </para>
    /// </summary>
    public const int UnitCostScale = 6;

    /// <summary>الرصيد الابتدائي: لا كمية، ولا قيمة، و<b>لا أساس تكلفة</b>.</summary>
    public static StockPosition Empty => new(0m, 0m, 0m, false, string.Empty);

    /// <summary>
    /// وارد بتكلفته الفعلية. المتوسط يُعاد حسابه <b>فقط</b> حين تصير الكمية موجبة:
    /// متوسطٌ مقسوم على كمية صفرية أو سالبة عددٌ بلا معنى محاسبي، وكتابته تُخفي
    /// الشذوذ بدل أن تُظهره.
    /// </summary>
    /// <param name="position">الرصيد قبل الحركة.</param>
    /// <param name="quantity">الكمية الواردة — موجبة.</param>
    /// <param name="cost">تكلفة الكمية الواردة كلّها.</param>
    public static StockEffect Receive(StockPosition position, decimal quantity, decimal cost)
    {
        decimal value = Round(cost, ValueScale);
        decimal quantityAfter = position.Quantity + quantity;
        decimal valueAfter = Round(position.Value + value, ValueScale);

        decimal unitCost = quantityAfter > 0m
            ? Round(valueAfter / quantityAfter, UnitCostScale)
            : position.UnitCost;

        StockPosition after = new(quantityAfter, valueAfter, unitCost, true, position.BaseUnit);
        return new StockEffect(value, quantity == 0m ? unitCost : Round(value / quantity, UnitCostScale), after, quantityAfter < 0m);
    }

    /// <summary>
    /// صادر بالمتوسط المتحرّك لحظة التسجيل.
    /// <para>
    /// <b>وحين يُفرغ الصرف الرصيد بالضبط تُنزَّل القيمة كلّها</b> لا حاصلُ الضرب:
    /// وإلا بقي فُتاتُ التقريب قيمةً على كميةٍ صفرية — أي مخزوناً بلا وحدات، وهو
    /// الشكل الذي يجعل رصيد المخزون ينتفخ بالهللات لسنوات.
    /// </para>
    /// <para>
    /// <b>ولا يُعاد حساب المتوسط عند الصرف</b>: الصرف لا يحمل معلومة تكلفة جديدة.
    /// </para>
    /// </summary>
    /// <param name="position">الرصيد قبل الحركة.</param>
    /// <param name="quantity">الكمية المنصرفة — موجبة.</param>
    public static StockEffect Issue(StockPosition position, decimal quantity)
    {
        decimal quantityAfter = position.Quantity - quantity;

        decimal value = position.Quantity > 0m && quantityAfter == 0m
            ? position.Value
            : Round(position.UnitCost * quantity, ValueScale);

        decimal valueAfter = Round(position.Value - value, ValueScale);

        StockPosition after = new(quantityAfter, valueAfter, position.UnitCost, position.HasCostBasis, position.BaseUnit);
        return new StockEffect(value, position.UnitCost, after, quantityAfter < 0m);
    }

    /// <summary>
    /// <b>إلغاء حركة مُسجَّلة بقيمتها هي</b> — لا بمتوسط اليوم.
    /// <para>
    /// وهذا هو الفرق بين «العكس» و«الصرف»: الصرف واقعةٌ جديدة تُقيَّم بما هو معروف
    /// لحظتها، والعكس إبطالُ واقعةٍ قُيِّمت من قبل. فلو أُعيد حساب قيمة العكس بمتوسط
    /// اليوم لبقي في الرصيد فارقٌ يساوي حركة المتوسط بين اللحظتين — رقمٌ لا يقابله
    /// شيء في المستودع، وهو بعينه الشكل الذي يمنع الإقفال في §3.3 من ADR-0039.
    /// </para>
    /// <para>
    /// <b>ولا يُمسّ <c>HasCostBasis</c> بالإنقاص:</b> «ورد هذا الصنف مرّةً بتكلفة»
    /// واقعةٌ تاريخية لا تُمحى بإلغاء الحركة التي أنشأتها — وإنزالُها كان سيجعل صرفاً
    /// لاحقاً يُرفض بـ<c>no_cost_basis</c> على صنفٍ له تاريخ.
    /// </para>
    /// </summary>
    /// <param name="position">الرصيد قبل الإلغاء.</param>
    /// <param name="quantity">كمية الحركة المُلغاة — موجبة.</param>
    /// <param name="value">قيمة الحركة المُلغاة كما سُجّلت.</param>
    /// <param name="inbound">هل حركة الإلغاء واردة؟ (إلغاء صادرٍ وارد، وإلغاء واردٍ صادر.)</param>
    public static StockEffect Annul(StockPosition position, decimal quantity, decimal value, bool inbound)
    {
        decimal quantityAfter = inbound ? position.Quantity + quantity : position.Quantity - quantity;
        decimal valueAfter = Round(inbound ? position.Value + value : position.Value - value, ValueScale);

        decimal unitCost = quantityAfter > 0m
            ? Round(valueAfter / quantityAfter, UnitCostScale)
            : position.UnitCost;

        StockPosition after = new(quantityAfter, valueAfter, unitCost, position.HasCostBasis, position.BaseUnit);

        return new StockEffect(
            value,
            quantity == 0m ? unitCost : Round(value / quantity, UnitCostScale),
            after,
            quantityAfter < 0m);
    }

    /// <summary>
    /// تقريب بمقياس معلن وبقاعدة معلنة.
    /// <para>
    /// <b>والقاعدة مكتوبة صراحةً لأنها ليست القاعدة نفسها على الطرفين:</b>
    /// <c>decimal.Round</c> في .NET تقرّب إلى الزوجي، و<c>round(numeric)</c> في
    /// PostgreSQL تقرّب بعيداً عن الصفر. وكلّ تقريب في هذا المنتج يقع <b>هنا</b> وفي
    /// ‏.NET وحدها؛ ولا عبارة SQL واحدة تقرّب. فمن ينقل هذا الحساب إلى القاعدة يوماً
    /// يغيّر الأرقام وهو لا يقصد — وهذا التعليق هو ما يجعله يراه قبل أن يفعل.
    /// </para>
    /// </summary>
    private static decimal Round(decimal value, int scale) => decimal.Round(value, scale, MidpointRounding.ToEven);
}
