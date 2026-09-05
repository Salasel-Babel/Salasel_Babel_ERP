using Babel.Contracts.Parameters;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// <b>مسوّدةُ إيداعٍ من منشأة.</b> والحقول الأربعة الأخيرة منقولةٌ بأسمائها حرفاً من
/// <c>HrPayrollSettingsRequest</c> — <c>EffectiveFrom</c> و<c>ApprovedBy</c>
/// و<c>ApprovedOn</c> و<c>SourceRef</c> — لأن ذلك النموذج هو ما يُعمَّم هنا، واسمٌ
/// ثانٍ لنفس الحقل يجعل من يقرأ الاثنين يظنّهما شيئين.
/// </summary>
/// <param name="SetCode">رمز المجموعة.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Approval">حالة الاعتماد — اعتمادُ مستأجرٍ أو توقيعُ محاسب.</param>
/// <param name="ApprovedBy">من اعتمد — <b>إنسان، لا نظام</b>.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
/// <param name="SourceRef">مرجع المصدر الذي أُخذت منه القيم.</param>
/// <param name="Values">القيم بمفاتيحها — <b>كلُّ مفاتيح المجموعة، لا بعضُها</b>.</param>
public sealed record ParameterVersionDraft(
    string SetCode,
    DateOnly EffectiveFrom,
    ParameterApproval Approval,
    string ApprovedBy,
    DateOnly ApprovedOn,
    string SourceRef,
    IReadOnlyDictionary<string, decimal> Values);

/// <summary>قيمةٌ في إصدار، بصنفها كما أُودعت.</summary>
/// <param name="Key">المفتاح.</param>
/// <param name="Kind">الصنف.</param>
/// <param name="Value">القيمة.</param>
public sealed record ParameterValueView(string Key, ParameterValueKind Kind, decimal Value);

/// <summary>إصدارٌ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="SetCode">المجموعة.</param>
/// <param name="Scope">المستوى.</param>
/// <param name="EffectiveFrom">السريان.</param>
/// <param name="Approval">حالة الاعتماد.</param>
/// <param name="ApprovedBy">المعتمِد — فارغٌ لافتراض المنصّة وحده.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد، أو غيابه لافتراض المنصّة.</param>
/// <param name="SourceRef">مرجع المصدر.</param>
/// <param name="Values">القيم، مرتَّبةً بمفاتيحها.</param>
public sealed record ParameterVersionView(
    Guid Id,
    string SetCode,
    ParameterScope Scope,
    DateOnly EffectiveFrom,
    ParameterApproval Approval,
    string ApprovedBy,
    DateOnly? ApprovedOn,
    string SourceRef,
    IReadOnlyList<ParameterValueView> Values);

/// <summary>مستندٌ مُرحَّل استعمل إصداراً.</summary>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PostedOn">تاريخ الترحيل.</param>
public sealed record ParameterUsageView(BabelModule Module, string DocumentType, Guid DocumentId, DateOnly PostedOn);

/// <summary>
/// <b>صفٌّ في قائمة مراجعة المحاسب:</b> إصدارٌ لم يُوقَّع بعد، ومعه كلُّ مستندٍ مُرحَّلٍ
/// استعمله. وهي القائمة التي تُعرض على المحاسب القانوني في الخطوة الأخيرة، ولذلك هي
/// <b>باب قراءةٍ منشور</b> لا تقريرٌ تحسبه شاشة.
/// </summary>
/// <param name="Version">الإصدار.</param>
/// <param name="Usages">ما استعمله من مستندات — فارغةٌ إن لم يُستعمل بعد.</param>
public sealed record ParameterReviewView(ParameterVersionView Version, IReadOnlyList<ParameterUsageView> Usages);

/// <summary>صفٌّ خامٌ من الاستعلام الواحد: ترويسةُ إصدارٍ وقيمُه، وسطرُ استعمالٍ أو غيابه.</summary>
/// <param name="Version">الإصدار.</param>
/// <param name="Usage">الاستعمال، أو <c>null</c>.</param>
public sealed record ParameterReviewRow(ParameterVersionView Version, ParameterUsageView? Usage);

/// <summary>
/// مخزن المعامِلات. <c>internal</c> ما ينفّذه — والباب المعلن هو
/// <see cref="ParameterSettingsService"/> و<see cref="ParameterDirectory"/>.
/// </summary>
public interface IParameterStore
{
    /// <summary>
    /// الإصدارُ السارِي لمجموعةٍ في تاريخ: <b>تجاوزُ المستأجر أوّلاً</b>، فإن لم يوجد
    /// فافتراضُ المنصّة، فإن لم يوجد فلا شيء.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="setCode">المجموعة.</param>
    /// <param name="on">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<ParameterVersionView?> FindEffectiveAsync(
        TenantId tenant, string setCode, DateOnly on, CancellationToken cancellationToken = default);

    /// <summary>
    /// يودِع إصداراً على مستوى المستأجر. يعيد <c>false</c> على تكرار
    /// (المستوى · المجموعة · تاريخ السريان) — والذرّية مفتاحٌ في القاعدة لا فحصٌ يسبق كتابة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="version">الإصدار كاملاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<bool> TryDepositAsync(TenantId tenant, ParameterVersionView version, CancellationToken cancellationToken = default);

    /// <summary>كلُّ ما يراه هذا المستأجر: افتراضاتُ المنصّة وتجاوزاتُه هو وحده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<IReadOnlyList<ParameterVersionView>> ListAsync(TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>يسجّل استعمالاً — <b>آمنُ التكرار</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="usage">الواقعة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask RecordUsageAsync(TenantId tenant, ParameterUsage usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// <b>الاستعلام الواحد</b>: كلُّ إصدارٍ غير موقَّع يراه هذا المستأجر، ومعه كلُّ
    /// مستندٍ مُرحَّلٍ له استعمله — وصلٌ خارجي كي يظهر الإصدار الذي لم يُستعمل بعد.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<IReadOnlyList<ParameterReviewRow>> ReviewAsync(TenantId tenant, CancellationToken cancellationToken = default);
}
