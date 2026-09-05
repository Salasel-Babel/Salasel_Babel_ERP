using System.Globalization;
using Babel.Contracts.Parameters;
using Babel.Core.Parameters;

namespace Babel.Api.Wire;

/// <summary>
/// نقلٌ بين السلك وعقد النواة. <b>ولا قرار واحد هنا</b>: الفهرس المغلق، وحارسُ النسبة،
/// ورفضُ الإيداع الجزئي — كلّها في <c>ParameterSettingsService</c>. وما هنا قراءةُ نصٍّ
/// وتحويلُ تاريخ.
/// </summary>
internal static class ParameterMapping
{
    private const int CodeLength = 64;
    private const int ReferenceLength = 600;
    private const int ApproverLength = 200;

    /// <summary>يحوّل طلب الإيداع.</summary>
    /// <param name="dto">الطلب.</param>
    public static ParameterVersionDraft ToDraft(ParameterVersionRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Dictionary<string, decimal> values = new(StringComparer.Ordinal);

        for (int index = 0; index < dto.Values.Count; index++)
        {
            ParameterValueRequestDto value = dto.Values[index];
            string at = FormattableString.Invariant($"values[{index}]");
            string key = WireMapping.ReadRequiredText(value.Key, at + ".key", CodeLength);

            if (!values.TryAdd(key, WireNumbers.ParseStrict(value.Value.Raw, WireNumbers.RateScale, at + ".value")))
            {
                throw new WireFormatException(
                    "wire.parameter.duplicate_key",
                    "المفتاح «" + key + "» مذكورٌ مرّتين في الإيداع. وقيمتان لمفتاحٍ واحد لا تُرجَّح إحداهما على "
                    + "الأخرى بقاعدة، فيُرفض الطلب بدل أن يُختار أحدهما صامتاً.",
                    "Key '" + key + "' appears twice in the deposit. Two values for one key cannot be ranked by any "
                    + "rule, so the request is refused rather than one being chosen silently.");
            }
        }

        return new ParameterVersionDraft(
            WireMapping.ReadRequiredText(dto.SetCode, "setCode", CodeLength),
            WireMapping.ReadDate(dto.EffectiveFrom, "effectiveFrom"),
            Approval(dto.Approval),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", ApproverLength),
            WireMapping.ReadDate(dto.ApprovedOn, "approvedOn"),
            WireMapping.ReadRequiredText(dto.SourceRef, "sourceRef", ReferenceLength),
            values);
    }

    /// <summary>يحوّل إصداراً إلى السلك.</summary>
    /// <param name="version">الإصدار.</param>
    public static ParameterVersionDto ToDto(ParameterVersionView version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new ParameterVersionDto(
            version.Id.ToString("D", CultureInfo.InvariantCulture),
            version.SetCode,
            ParameterApprovalInfo.TokenOf(version.Scope),
            Date(version.EffectiveFrom),
            ParameterApprovalInfo.TokenOf(version.Approval),
            version.ApprovedBy,

            // ‏**فراغٌ لا تاريخٌ مخترَع**: افتراضُ المنصّة لم يعتمده إنسان، فلا تاريخ اعتماد
            // له. وكتابةُ تاريخٍ هنا — ولو تاريخَ الشحن — ادّعاءُ واقعةٍ لم تقع.
            version.ApprovedOn is { } approvedOn ? Date(approvedOn) : string.Empty,
            version.SourceRef,
            [.. version.Values.Select(static value => new ParameterValueDto(
                value.Key, ParameterApprovalInfo.TokenOf(value.Kind), Number(value.Value)))]);
    }

    /// <summary>يحوّل قائمة الإصدارات.</summary>
    /// <param name="versions">الإصدارات.</param>
    public static ParameterVersionListDto ToDto(IReadOnlyList<ParameterVersionView> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return new ParameterVersionListDto(versions.Count, [.. versions.Select(ToDto)]);
    }

    /// <summary>يحوّل قائمة المراجعة.</summary>
    /// <param name="review">الصفوف.</param>
    public static ParameterReviewListDto ToDto(IReadOnlyList<ParameterReviewView> review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ParameterReviewListDto(
            review.Count,
            [.. review.Select(static entry => new ParameterReviewEntryDto(
                ToDto(entry.Version),
                entry.Usages.Count,
                [.. entry.Usages.Select(static usage => new ParameterUsageDto(
                    usage.Module.ToString(),
                    usage.DocumentType,
                    usage.DocumentId.ToString("D", CultureInfo.InvariantCulture),
                    Date(usage.PostedOn)))]))]);
    }

    private static ParameterApproval Approval(string token) => token switch
    {
        "tenant_approved" => ParameterApproval.TenantApproved,
        "auditor_signed" => ParameterApproval.AuditorSigned,

        // ‏**و«افتراضُ منصّة» ليس خياراً على هذا الباب**: يُشحن مع المنتج ولا يُكتب من
        // مسار طلب. والرفض هنا شكليّ، والرفض المجالي في الخدمة نفسها كذلك.
        _ => throw new WireFormatException(
            "wire.parameter.approval_unknown",
            "حالة اعتماد غير معروفة على هذا الباب: «" + token + "». والمقبول: tenant_approved أو auditor_signed.",
            "Unknown approval state on this door: '" + token + "'. Accepted: tenant_approved or auditor_signed."),
    };

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// القيمة نصّاً <b>بمقياسها كما أُودعت</b> — لا بمقياسٍ مُوحَّد.
    /// <para>
    /// وقصُّها إلى أربع خانات كان سيجعل نسبةً بمقياس ثمانٍ تعود مقصوصةً إلى العميل،
    /// فيقرأ رقماً غير الذي أُودع.
    /// </para>
    /// </summary>
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
