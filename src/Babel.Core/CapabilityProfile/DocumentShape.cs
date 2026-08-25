using System.Collections.Immutable;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// <b>شكل المستند مُشتقّاً، لا مؤلَّفاً.</b>
/// <para>
/// الشاشة دالّةٌ في (العقد المنشور × ملفّ القدرات). ولا يوجد في هذا النوع تخطيط، ولا
/// ترتيب بصري، ولا شرط، ولا تعبير — فتلك أبواب «المنصّة داخل المنصّة» التي رُفضت. هنا
/// حقول ومَن رخّصها، لا أكثر.
/// </para>
/// </summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="NameAr">
/// اسم النوع بالعربية — إلزامي وهو الارتداد المضمون. والعربية هنا ليست «اللغة الأولى» بل
/// شكل السجلّ: النظام السعودي يوجب مسك الدفاتر بالعربية (ADR-0021).
/// </param>
/// <param name="NameKey">
/// مفتاح الترجمة. تعدّد اللغات يعني الترجمة إلى <b>أيّ عدد</b> من اللغات، لا ثنائية
/// عربي/إنجليزي — فلا عمود لغة ثانية هنا.
/// </param>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="AvailableCapabilities">كل قدرات هذا النوع في الكتالوج.</param>
/// <param name="EnabledCapabilities">المُشغَّل منها لهذا المستأجر.</param>
/// <param name="Fields">الحقول القائمة على المستند بهذا الملفّ — الأساسية وحقول القدرات المُشغَّلة.</param>
/// <param name="Defaults">القيم الافتراضية، ومفاتيحها حقول من <paramref name="Fields"/> حصراً.</param>
public sealed record DocumentShape(
    DocumentTypeCode DocumentType,
    string NameAr,
    string NameKey,
    BabelModule Module,
    ImmutableArray<CapabilityCode> AvailableCapabilities,
    ImmutableArray<CapabilityCode> EnabledCapabilities,
    ImmutableArray<string> Fields,
    ImmutableSortedDictionary<string, string> Defaults);
