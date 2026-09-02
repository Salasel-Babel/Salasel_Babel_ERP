namespace Babel.Ai.Agent;

/// <summary>
/// نداء أداةٍ كما نطق به النموذج — <b>نوعٌ محايد لا نوع مزوّد</b>.
/// <para>
/// والحياد ليس ذوقاً: البوابة والحلقة والاختبارات تُكتب على هذا النوع، فلا تُجَرّ حزمة
/// المزوّد إلى مسار الاختبار، ولا تُعاد كتابة الحارس إن تغيّر المزوّد.
/// </para>
/// </summary>
/// <param name="Id">معرّف النداء كما أصدره النموذج — يُعاد به الجواب.</param>
/// <param name="Name">اسم الأداة.</param>
/// <param name="ArgumentsJson">
/// الوسائط بنصّها كما أنتجها النموذج. <b>ولا تُقرأ مطابقةً نصّية</b>: تُفكّ بـJSON —
/// فالنماذج تُغيّر هروب المحارف بين الإصدارات.
/// </param>
public sealed record AgentToolCall(string Id, string Name, string ArgumentsJson);
