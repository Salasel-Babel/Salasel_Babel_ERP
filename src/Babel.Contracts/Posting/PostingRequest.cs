using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// عقد الترحيل: الطريق الوحيد الذي تصل به أي وحدة إلى دفتر الأستاذ.
/// <para>
/// «لا وحدة تكتب في جداول Ledger مباشرة — الكتابة عبر محرك الترحيل فقط»
/// (CONTRIBUTING §3 بند 1 · وثيقة المعمارية §13). القاعدة مفروضة بثلاث طبقات:
/// (1) لا مرجع مشروع من أي وحدة أفقية إلى Babel.Ledger،
/// (2) أنواع استمرارية الدفتر <c>internal</c>،
/// (3) صلاحيات PostgreSQL: <c>INSERT</c> و<c>SELECT</c> فقط للدور التطبيقي (وثيقة المعمارية §3.2).
/// </para>
/// </summary>
public sealed record PostingRequest
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>
    /// مفتاح الحصانة ضد التكرار. القاعدة المعمارية 4: مستقل عن الترتيب،
    /// لأن مزامنة نقاط البيع دون اتصال تُسلّم خارج الترتيب بطبيعتها.
    /// </summary>
    public required IdempotencyKey IdempotencyKey { get; init; }

    /// <summary>المستند المصدر.</summary>
    public required SourceDocument Source { get; init; }

    /// <summary>الحدث الذي أطلق الترحيل.</summary>
    public required PostingTrigger Trigger { get; init; }

    /// <summary>تاريخ المستند الميلادي. الفترة المالية تُشتق منه داخل الدفتر.</summary>
    public required DateOnly DocumentDate { get; init; }

    /// <summary>بيان القيد ثنائي اللغة.</summary>
    public required LocalizedName Narration { get; init; }

    /// <summary>سطور الطلب بأدوارها ومبالغها.</summary>
    public required IReadOnlyList<PostingLine> Lines { get; init; }
}
