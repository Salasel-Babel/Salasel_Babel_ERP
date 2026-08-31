using System.Globalization;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>
/// ‼‼‼  تحذير — اقرأه قبل أي سطر تكتبه بجوار هذا الملف  ‼‼‼
///
/// <code>
/// ┌───────────────────────────────────────────────────────────────────────────┐
/// │  ناتج هذا الملف لا يُجزَّأ أبداً.                                          │
/// │  وناتج هذا الملف لا يُكتب أبداً فوق حقل مُوقَّع.                            │
/// │                                                                           │
/// │  THE OUTPUT OF THIS FILE IS NEVER HASHED.                                 │
/// │  THE OUTPUT OF THIS FILE IS NEVER WRITTEN OVER A SIGNED COLUMN.           │
/// └───────────────────────────────────────────────────────────────────────────┘
/// </code>
///
/// <b>لماذا هذا الملف موجود أصلاً، ولماذا هو الخطر الأول في المشروع:</b>
///
/// هذا مشروع بيانات عربية. البحث فيه يحتاج تطبيعاً: «مكتب الرياض» يجب أن يُوجد
/// بكتابة «مكتب الریاض» و«مكـــتب الرياض» و«مكتب الرياض» بألف مختلفة. أي أن تطبيع
/// البحث <b>مطلوب فعلاً</b>، ولذلك سيُكتب حتماً — إن لم يكن اليوم فبعد سنة.
///
/// والخطر ليس أن يُكتب. الخطر أن يُشغَّل على <b>بيانات مخزَّنة موقَّعة</b>:
/// مطوّر يضيف تطبيع بحث، يشغّل <c>UPDATE ... SET name = normalize(name)</c> مرّة
/// واحدة على الجدول القائم، فتُكسر كل سلسلة في النظام دفعة واحدة، بلا رسالة خطأ،
/// وبلا طريقة للرجوع لأن القيم الأصلية ضاعت.
///
/// <b>الحماية البنيوية المطبَّقة هنا:</b>
///   1. الناتج نوعه <see cref="SearchKey"/> وليس <see cref="string"/>، ولا يوجد
///      تحويل ضمني بينهما. لا يمكن تمريره إلى المُوحِّد القياسي بالخطأ.
///   2. <c>CanonicalValue.Text(SearchKey)</c> موجود ومُعلَّم
///      <c>[Obsolete(error: true)]</c>، فيصير <b>خطأ ترجمة</b> برسالة تشرح السبب،
///      لا مجرّد «لا يوجد تحميل زائد مطابق».
///   3. العمودان منفصلان في قاعدة البيانات: <c>name</c> مُوقَّع،
///      <c>name_search</c> مشتقّ ومستثنى من التجزئة (انظر مجموعة الاستثناء).
///
/// <b>The signed value and the search-normalised value are different things.</b>
/// </summary>
public static class ArabicSearch
{
    /// <summary>
    /// مفتاح بحث. <b>مشتقّ، غير مُوقَّع، ولا يُخزَّن فوق قيمة موقَّعة.</b>
    /// نوع مستقلّ عمداً: لا تحويل ضمنياً إلى string حتى لا يتسرّب إلى المُجزِّئ.
    /// </summary>
    public readonly record struct SearchKey
    {
        internal SearchKey(string value) => Value = value;

        /// <summary>
        /// القيمة النصية للمفتاح. استدعاؤها يجب أن يكون قراراً واعياً:
        /// إن كنت تستدعيها لتخزّن الناتج، تأكّد أن العمود الهدف
        /// <b>مشتقّ ومستثنى من التجزئة</b>.
        /// </summary>
        public string Value { get; }

        public override string ToString() => Value;
    }

    private const string Tatweel = "ـ";

    /// <summary>
    /// تطبيع للبحث فقط. يزيل فروقاً <b>ذات معنى في القيمة الموقَّعة</b>، ولذلك
    /// لا يجوز أن يقترب من التجزئة.
    ///
    /// ما يفعله:
    ///   • تنظيف الحدّ نفسه (Cf، مسافات، أرقام، أشكال عرض، NFC).
    ///   • حذف التطويل U+0640.
    ///   • حذف التشكيل والحركات U+064B–U+065F و U+0670 والشدّة والسكون.
    ///   • توحيد أشكال الألف: أ (U+0623) إ (U+0625) آ (U+0622) ٱ (U+0671) -> ا (U+0627).
    ///   • توحيد الهمزات: ؤ (U+0624) -> و، ئ (U+0626) -> ي، ء (U+0621) تُحذف.
    ///   • ة (U+0629) -> ه (U+0647).
    ///   • ى (U+0649) -> ي (U+064A).
    ///   • الحروف الفارسية/الأردية: ک (U+06A9) -> ك، گ ژ پ چ تبقى، ی (U+06CC) -> ي.
    ///   • خفض حالة الأحرف اللاتينية بالثقافة الثابتة.
    ///   • طيّ المسافات المتتالية وقصّ الأطراف.
    /// </summary>
    public static SearchKey Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var cleaned = TextRules.CleanForInput(value);
        var sb = new StringBuilder(cleaned.Length);

        foreach (var rune in cleaned.EnumerateRunes())
        {
            var cp = rune.Value;

            // التطويل
            if (cp == 0x0640) continue;

            // التشكيل والحركات والعلامات المدمجة
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark) continue;
            if (cp == 0x0670) continue; // ألف خنجرية

            switch (cp)
            {
                case 0x0622: // آ
                case 0x0623: // أ
                case 0x0625: // إ
                case 0x0671: // ٱ
                    sb.Append('ا'); continue;
                case 0x0624: sb.Append('و'); continue; // ؤ -> و
                case 0x0626: sb.Append('ي'); continue; // ئ -> ي
                case 0x0621: continue;                       // ء تُحذف
                case 0x0629: sb.Append('ه'); continue; // ة -> ه
                case 0x0649: sb.Append('ي'); continue; // ى -> ي
                case 0x06A9: sb.Append('ك'); continue; // ک -> ك
                case 0x06CC: sb.Append('ي'); continue; // ی -> ي
                case 0x06D2: sb.Append('ي'); continue; // ے -> ي
            }

            if (cp is >= 'A' and <= 'Z') { sb.Append((char)(cp - 'A' + 'a')); continue; }
            if (cp > 0x7F && cat is UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter)
            {
                sb.Append(rune.ToString().ToLowerInvariant());
                continue;
            }

            sb.Append(rune.ToString());
        }

        // طيّ المسافات
        var collapsed = new StringBuilder(sb.Length);
        var lastWasSpace = false;
        foreach (var ch in sb.ToString())
        {
            var isSpace = ch is ' ' or '\n' or '\t';
            if (isSpace)
            {
                if (!lastWasSpace && collapsed.Length > 0) collapsed.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                collapsed.Append(ch);
                lastWasSpace = false;
            }
        }

        return new SearchKey(collapsed.ToString().TrimEnd());
    }
}
