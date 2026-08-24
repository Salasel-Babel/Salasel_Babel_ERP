namespace Babel.Compliance.Abstractions;

/// <summary>المستأجر. مفتاح العزل الأول في كل شيء هنا.</summary>
public readonly record struct TenantId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator string(TenantId id) => id.Value;
}

/// <summary>
/// وحدة إصدار مستقلة: جهاز نقطة بيع، أو نقطة إصدار فواتير، أو فرع.
/// <b>هذه هي الوحدة الذرّية في هذا الحدّ كله</b>: لها شهادتها الخاصة، وعدّادها الخاص،
/// وسلسلتها الخاصة. لا شيء في هذا الحدّ نطاقه «المستأجر» — كل شيء نطاقه «وحدة الإصدار».
/// <para/>
/// One independent issuing unit: a POS device, an invoicing point, a branch. It owns its
/// own certificate, its own counter and its own hash chain. Nothing in this boundary is
/// scoped per tenant; everything is scoped per issuing unit.
/// </summary>
public readonly record struct IssuingUnitId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator string(IssuingUnitId id) => id.Value;
}

/// <summary>هوية المستند داخل نظامنا. لا علاقة لها برقم المستند الظاهر للمستخدم.</summary>
public readonly record struct ComplianceDocumentId(Guid Value)
{
    public static ComplianceDocumentId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("D");
}

/// <summary>محاولة إرسال واحدة. تُنشأ وتُحفظ <b>قبل</b> النداء الشبكي، لا بعده.</summary>
public readonly record struct AttemptId(Guid Value)
{
    public static AttemptId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// إشارة إلى القيد المحاسبي المُرحَّل. إشارة فقط: هذا الحدّ <b>لا يكتب</b> في دفتر الأستاذ
/// ولا يقرأ منه رقماً مالياً لغرض الإرسال. الترحيل تمّ قبل أن يعرف هذا الحدّ بوجود المستند.
/// </summary>
public readonly record struct JournalEntryRef(Guid Value)
{
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// مقبض إلى مادة مفتاح مخزّنة في خزينة مفاتيح أو لدى المزوّد.
/// <b>لا يمرّ مفتاح خاص ولا شهادة خاصة عبر هذا الحدّ إطلاقاً — مقابض فقط.</b>
/// هذا هو ما يجعل الشكلين (المزوّد يحوز / نحن نحوز) قادرين على تقاسم العقد نفسه.
/// </summary>
[DualCustodyCost(
    "مقبض بدل مادة المفتاح: تحت «نحن نحوز» كان يمكن تمرير CngKey أو ECDsa مباشرة بأمان نوعي كامل؛ " +
    "وتحت «المزوّد يحوز» لا وجود لمفتاح محلي أصلاً. المقبض هو القاسم المشترك الأدنى بينهما.",
    Kind = CustodyCostKind.ExtraSurface)]
public readonly record struct CredentialRef(string Value)
{
    public override string ToString() => Value;
    public static readonly CredentialRef None = new("");
    public bool IsNone => string.IsNullOrEmpty(Value);
}

/// <summary>مقبض إلى سرّ في خزينة الأسرار. لا يحمل السرّ نفسه أبداً.</summary>
public readonly record struct SecretRef(string Value)
{
    public override string ToString() => Value;
    public static readonly SecretRef None = new("");
}
