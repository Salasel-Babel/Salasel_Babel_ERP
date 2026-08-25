using System.Collections.Immutable;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// ما يصل من العميل لنوع مستند واحد: مفاتيح مُشغَّلة أو مُطفأة، وقيم افتراضية.
/// <para>
/// <b>مسودّة لا ملفّ:</b> النوع لا يقول شيئاً عن صلاحية ما يحمله، ولا يُخزَّن، ولا يُشتق
/// منه شكل شاشة. الطريق الوحيد إلى ملفّ صالح هو
/// <see cref="ValidatedCapabilityProfile.Create(CapabilityProfileDraft, IPostingEventDirectory)"/>.
/// </para>
/// </summary>
/// <param name="Capabilities">القدرات بأسمائها كما وردت، وقيمتها المنطقية.</param>
/// <param name="Defaults">القيم الافتراضية بأسماء حقولها كما وردت.</param>
public sealed record DocumentProfileDraft(
    IReadOnlyDictionary<string, bool> Capabilities,
    IReadOnlyDictionary<string, string> Defaults)
{
    /// <summary>مسودّة نوع مستند بلا قدرة مُشغَّلة ولا قيمة افتراضية.</summary>
    public static DocumentProfileDraft Bare { get; } = new(
        ImmutableSortedDictionary<string, bool>.Empty,
        ImmutableSortedDictionary<string, string>.Empty);
}

/// <summary>مسودّة ملفّ قدرات مستأجر: نوع مستند لكل مفتاح.</summary>
/// <param name="Documents">أنواع المستندات بأسمائها كما وردت.</param>
public sealed record CapabilityProfileDraft(IReadOnlyDictionary<string, DocumentProfileDraft> Documents);
