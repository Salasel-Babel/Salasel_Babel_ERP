using Babel.SharedKernel;

namespace Babel.Contracts.Voice;

/// <summary>
/// ما تُسهم به وحدةٌ من خطط منطوقة. <b>نفس انقلاب الاعتماد الذي في
/// <see cref="IVoiceIntentCatalogue"/> وللسبب نفسه</b> (القاعدة 3): الوحدة تُعلن،
/// ووحدةُ الذكاء تجمع، ولا تعرف إحداهما الأخرى في أي اتجاه.
/// </summary>
public interface IVoicePlanCatalogue
{
    /// <summary>الوحدة المالكة.</summary>
    BabelModule Module { get; }

    /// <summary>خططها. <b>مغلقة</b>: ما ليس فيها لا يُنطَق ولا يُخمَّن.</summary>
    IReadOnlyList<VoicePlan> Plans { get; }
}
