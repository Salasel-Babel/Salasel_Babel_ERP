namespace Babel.Ai.Suggestions;

/// <summary>
/// المفردات المغلقة التي يُسمح للنموذج أن يُسمّي منها: رموز الأحداث ورموز الأدوار.
/// <para>
/// واجهة لا نوع ملموس، لأن مصدر المفردات قرار تركيب: مصفوفة مضمَّنة اليوم، وقد تصير
/// قراءةً من قاعدة بيانات المستأجر غداً. وما لا يتغيّر هو أنها <b>مغلقة</b>: ما ليس
/// فيها مرفوض.
/// </para>
/// </summary>
public interface IPostingVocabulary
{
    /// <summary>هل رمز الحدث موجود في المصفوفة؟</summary>
    /// <param name="eventCode">الرمز.</param>
    bool KnowsEvent(string eventCode);

    /// <summary>هل رمز الدور موجود في المصفوفة؟</summary>
    /// <param name="roleCode">الرمز.</param>
    bool KnowsRole(string roleCode);

    /// <summary>عدد رموز الأحداث — يُستعمل في حارس «المفردات ليست فارغة».</summary>
    int EventCount { get; }

    /// <summary>عدد رموز الأدوار.</summary>
    int RoleCount { get; }
}
