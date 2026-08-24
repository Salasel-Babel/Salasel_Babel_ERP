using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// طلب عكس قيد مُرحَّل.
/// <para>
/// <b>العكس لا الحذف ولا التعديل</b> (‏ADR-0002): القيد الأصلي حقيقة نهائية لا تُمسّ،
/// والتصحيح قيد جديد مرتبط به. ولذلك لا يحمل هذا الطلب أي حقل «تعديل».
/// </para>
/// <para>
/// وبعد العكس يجوز إعادة الترحيل الصحيح: يزيد <see cref="PostingRequest.Generation"/>
/// جيلاً واحداً، فيختلف مفتاح الحصانة ويصير المسار
/// «ترحيل ← عكس ← تصحيح ← إعادة ترحيل» ممكناً بلا التفاف على الإحكام.
/// </para>
/// </summary>
public sealed record ReversalRequest
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>معرّف القيد المراد عكسه. لا يُمسّ ذلك القيد إطلاقاً.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>سبب العكس ثنائي اللغة. إلزامي: عكسٌ بلا سبب لا يُقرأ في تدقيق.</summary>
    public required LocalizedName Reason { get; init; }

    /// <summary>الفاعل.</summary>
    public required UserId Actor { get; init; }

    /// <summary>تاريخ قيد العكس. الافتراضي تاريخ القيد الأصلي إن تُرك فارغاً.</summary>
    public DateOnly? ReversalDate { get; init; }

    /// <summary>إذن استثنائي إن وقع تاريخ العكس في فترة مقفلة.</summary>
    public ClosedPeriodAuthorisation? ClosedPeriodAuthorisation { get; init; }
}
