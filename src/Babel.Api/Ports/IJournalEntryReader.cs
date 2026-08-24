using Babel.Api.Wire;
using Babel.SharedKernel;

namespace Babel.Api.Ports;

/// <summary>
/// منفذ قراءة قيد واحد بسطوره.
/// <para>
/// <b>ولماذا هو منفذ بلا تنفيذ في هذا التسليم:</b> السطح العام لـ<c>Babel.Ledger</c> اليوم
/// يكشف <c>IPostingService</c> (كتابة) و<c>LedgerAuditService</c> (ميزان مراجعة + تحقّق من
/// السلسلة) ولا يكشف قراءة قيد مفرد. وأنواع استمرارية الدفتر <c>internal</c> عمداً
/// (القاعدة 1، الطبقة الثانية)، فلا يستطيع الجذر التركيبي أن يقرأ الجداول بنفسه —
/// <b>ولا يجوز أن يستطيع</b>: أول استعلام SQL يُكتب في <c>Babel.Api</c> هو أول سطر منطق
/// أعمال فيه، وهو ما تمنعه القاعدة 13.
/// </para>
/// <para>
/// فالمنفذ يُعلَن هنا ليكون <b>العقد منشوراً وثابتاً</b> لفريق الواجهة من اليوم، ويُسجَّل
/// التنفيذ طلباً على مالك الدفتر: إضافة <c>ReadEntryAsync</c> إلى <c>LedgerAuditService</c>
/// بالسمة <c>[RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]</c>. وإلى أن
/// يهبط ذلك، تُرجع نقطة النهاية <c>501</c> برمز ثابت — لا صفراً ولا قائمة فارغة تُقرأ
/// «لا سطور».
/// </para>
/// </summary>
internal interface IJournalEntryReader
{
    /// <summary>يقرأ قيداً بسطوره داخل نطاق شركة واحدة.</summary>
    /// <param name="tenant">المستأجر — نطاق العزل.</param>
    /// <param name="entryId">معرّف القيد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<JournalEntryDto>> ReadAsync(TenantId tenant, Guid entryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// التنفيذ القائم: يرفض بصوت عالٍ ورمز ثابت.
/// <para>
/// الصمت هنا كان سيكون أسوأ خيار ممكن: نقطة نهاية تُرجع <c>404</c> على قيد موجود تعني
/// أن الواجهة تعرض «لا قيد» عن قيد مُرحَّل — وهو رقم خاطئ صامت بعينه.
/// </para>
/// </summary>
internal sealed class UnavailableJournalEntryReader : IJournalEntryReader
{
    /// <summary>الرمز الثابت الذي تخرج به هذه الحالة.</summary>
    public const string Code = "ledger.read.entry_surface_unavailable";

    /// <inheritdoc />
    public ValueTask<Result<JournalEntryDto>> ReadAsync(
        TenantId tenant,
        Guid entryId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result<JournalEntryDto>.Failure(new Error(
            Code,
            "قراءة قيد مفرد غير متاحة بعد: دفتر الأستاذ لا يكشف سطح قراءة لقيد واحد، والجذر "
            + "التركيبي لا يقرأ جداوله مباشرة — عمداً. العقد منشور وثابت، والتنفيذ ينتظر إضافة "
            + "ReadEntryAsync إلى LedgerAuditService.",
            "Reading a single entry is not available yet: the ledger exposes no read surface for one entry, and "
            + "the composition root does not query its tables directly — by design. The contract is published and "
            + "stable; the implementation awaits ReadEntryAsync on LedgerAuditService.")));
}
