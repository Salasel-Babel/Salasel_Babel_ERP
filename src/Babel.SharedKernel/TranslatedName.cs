using System.Collections.Immutable;

namespace Babel.SharedKernel;

/// <summary>
/// نتيجة حلّ اسم إلى لغة عرض: النصّ، والوسم الذي أعطاه فعلاً، وهل كان ارتداداً.
/// <para>
/// <b>ولماذا نوعٌ بدل نصّ:</b> الارتداد الصامت خطر بذاته (ADR-0021) — عنوانٌ عربي فوق
/// عمود أرقام في واجهة أردية يجعل القارئ <b>يفترض</b> العنوان الذي يتوقّعه فلا يُبلَّغ
/// عن نقص الترجمة أبداً. فمن أراد النصّ وحده ناداه، ومن أراد أن يُعلن الارتداد وجد ما
/// يُعلنه في القيمة نفسها لا في تخمينٍ يُعاد بناؤه عند كل شاشة.
/// </para>
/// </summary>
/// <param name="Text">النصّ المعروض.</param>
/// <param name="LanguageTag">وسم اللغة الذي جاء منه النصّ فعلاً — <c>ar</c> عند الارتداد.</param>
/// <param name="IsFallback">هل ارتدّ إلى السجلّ العربي لغياب ترجمة مطابقة؟</param>
public readonly record struct NameResolution(string Text, string LanguageTag, bool IsFallback);

/// <summary>
/// <b>اسمٌ سجلُّه عربي وترجماته صفوف.</b> النوع الذي يجعل «إضافة الأردية» إدخالَ مدخل
/// لا هجرةَ مخطّط (ADR-0021 بند 2).
/// <para>
/// <b>الفرق عن <see cref="LocalizedName"/>، وهو فرق في الطبيعة لا في الشكل:</b>
/// <see cref="LocalizedName"/> يحمل <b>واقعتين مسجَّلتين</b> يغطّيهما التوقيع — بيان القيد
/// العربي والإنجليزي يدخلان البايتات المُجزَّأة حقلَين مستقلَّين (<c>memo_ar</c> و
/// <c>memo</c> في المخطّط القانوني v2). وهذا النوع يحمل <b>سجلّاً واحداً وعروضه</b>:
/// العربي هو السجلّ، وما سواه ترجمة <b>لا تدخل بصمةً ولا دفتراً ولا شكلاً قانونياً</b>
/// (بند 3). فلا يحلّ أحدهما محلّ الآخر، ولا يُستعمل هذا النوع في أي حقل يعبر إلى
/// <c>Babel.Canonicalization</c>.
/// </para>
/// <para>
/// <b>والعربي إلزامي غير فارغ</b> لأنه مرجع الارتداد: بلا سجلٍّ مضمون يصير غيابُ الترجمة
/// عموداً بلا عنوان، وهو العطل الذي لا يُبلَّغ عنه.
/// </para>
/// </summary>
public sealed record TranslatedName
{
    /// <summary>أقصى طول لوسم اللغة — يسع <c>zh-Hant-HK</c> وما هو أطول منه.</summary>
    public const int MaximumLanguageTagLength = 35;

    /// <summary>وسم لغة السجلّ. ليس ترجمةً ولا يُخزَّن في الخريطة أبداً.</summary>
    public const string RecordLanguageTag = "ar";

    private readonly ImmutableSortedDictionary<string, string> _translations;

    /// <summary>ينشئ اسماً سجلُّه عربي.</summary>
    /// <param name="arabic">الاسم العربي — السجلّ. إلزامي وغير فارغ.</param>
    /// <param name="translations">الترجمات بوسم BCP-47، أو <c>null</c>.</param>
    /// <exception cref="ArgumentException">اسمٌ عربي فارغ، أو وسمٌ مُشوَّه، أو ترجمة فارغة.</exception>
    public TranslatedName(string arabic, IReadOnlyDictionary<string, string>? translations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arabic);

        Arabic = arabic.Trim();

        ImmutableSortedDictionary<string, string>.Builder builder =
            ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        if (translations is not null)
        {
            foreach (KeyValuePair<string, string> pair in translations)
            {
                if (!IsWellFormedLanguageTag(pair.Key))
                {
                    throw new ArgumentException(
                        $"وسم لغة مُشوَّه «{pair.Key}». / Malformed language tag '{pair.Key}'.",
                        nameof(translations));
                }

                if (string.Equals(pair.Key, RecordLanguageTag, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "العربية سجلٌّ لا ترجمة، فلا تُدخَل في خريطة الترجمات. / "
                        + "Arabic is the record, not a translation; it never enters the translation map.",
                        nameof(translations));
                }

                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        $"ترجمة فارغة للوسم «{pair.Key}» — والغياب يُعبَّر عنه بغياب المدخل لا بنصٍّ فارغ. / "
                        + $"Empty translation for '{pair.Key}'; absence is an absent entry, not an empty string.",
                        nameof(translations));
                }

                builder[pair.Key] = pair.Value.Trim();
            }
        }

        _translations = builder.ToImmutable();
    }

    /// <summary>الاسم العربي — <b>السجلّ</b>. غير فارغ دائماً بحكم بناء النوع.</summary>
    public string Arabic { get; }

    /// <summary>الترجمات بوسم BCP-47، مرتَّبة ترتيباً حرفياً ثابتاً لا يعتمد على ثقافة.</summary>
    public ImmutableSortedDictionary<string, string> Translations => _translations;

    /// <summary>عدد الترجمات. صفر مشروع تماماً: السجلّ وحده يكفي.</summary>
    public int TranslationCount => _translations.Count;

    /// <summary>
    /// يحلّ الاسم إلى لغة عرض: مطابقة تامّة، ثم الوسم الأوّلي (‏<c>ur-PK</c> ⇒ <c>ur</c>)،
    /// ثم <b>ارتداداً إلى العربية — لا إلى الفراغ ولا إلى المفتاح</b> (ADR-0021).
    /// </summary>
    /// <param name="languageTag">وسم لغة العرض المطلوب، أو <c>null</c> لطلب السجلّ نفسه.</param>
    public NameResolution Resolve(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)
            || string.Equals(languageTag, RecordLanguageTag, StringComparison.OrdinalIgnoreCase)
            || languageTag.StartsWith(RecordLanguageTag + "-", StringComparison.OrdinalIgnoreCase))
        {
            return new NameResolution(Arabic, RecordLanguageTag, IsFallback: false);
        }

        if (_translations.TryGetValue(languageTag, out string? exact))
        {
            return new NameResolution(exact, languageTag, IsFallback: false);
        }

        int separator = languageTag.IndexOf('-', StringComparison.Ordinal);

        if (separator > 0)
        {
            string primary = languageTag[..separator];

            if (_translations.TryGetValue(primary, out string? broader))
            {
                return new NameResolution(broader, primary, IsFallback: false);
            }
        }

        return new NameResolution(Arabic, RecordLanguageTag, IsFallback: true);
    }

    /// <summary>النصّ وحده بلغة العرض. مختصرٌ لـ<see cref="Resolve"/> لمن لا يُعلن الارتداد.</summary>
    /// <param name="languageTag">وسم لغة العرض.</param>
    public string In(string? languageTag) => Resolve(languageTag).Text;

    /// <summary>يُرجع اسماً جديداً بترجمة مضافة أو مستبدلة. النوع باقٍ لا يُعدَّل.</summary>
    /// <param name="languageTag">وسم اللغة.</param>
    /// <param name="name">النصّ المترجَم.</param>
    public TranslatedName With(string languageTag, string name)
    {
        Dictionary<string, string> next = new(_translations, StringComparer.Ordinal)
        {
            [languageTag ?? string.Empty] = name,
        };

        return new TranslatedName(Arabic, next);
    }

    /// <summary>
    /// هل النصّ وسمُ لغة سليم الشكل؟ محارف لاتينية وأرقام وشرطات، يبدأ بحرف ولا ينتهي
    /// بشرطة. والقيد لاتيني لأن الوسم <b>معرّف</b> يعبر مسار HTTP ومفاتيح قاعدة البيانات
    /// (‏BCP-47)، لا لأن اللاتينية أفضل.
    /// </summary>
    /// <param name="tag">الوسم المرشَّح.</param>
    public static bool IsWellFormedLanguageTag(string? tag)
        => tag is { Length: > 0 and <= MaximumLanguageTagLength }
            && char.IsAsciiLetter(tag[0])
            && tag[^1] != '-'
            && !tag.Contains("--", StringComparison.Ordinal)
            && tag.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>
    /// المساواة بالمحتوى لا بهوية القاموس.
    /// <para>
    /// <b>ولماذا مكتوبةٌ بيد:</b> المساواة المُولَّدة للسجلّ تقارن الحقول بـ
    /// <c>EqualityComparer&lt;T&gt;.Default</c>، و<see cref="ImmutableSortedDictionary{TKey,TValue}"/>
    /// لا يُنفّذ مساواة بالقيمة — فنسختان بمحتوى واحد كانتا <b>تختلفان</b>. وذلك عطل
    /// صامت: يمرّ في كل مسار لا يقارن، وينفجر في أول ذاكرة مؤقّتة مفتاحها اسم.
    /// </para>
    /// </summary>
    /// <param name="other">الاسم الآخر.</param>
    public bool Equals(TranslatedName? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || !string.Equals(Arabic, other.Arabic, StringComparison.Ordinal))
        {
            return false;
        }

        if (_translations.Count != other._translations.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> pair in _translations)
        {
            if (!other._translations.TryGetValue(pair.Key, out string? value)
                || !string.Equals(pair.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Arabic, StringComparer.Ordinal);

        // الخريطة مرتَّبة ترتيباً حرفياً ثابتاً، فالطيّ عليها حتميّ بلا فرز إضافي.
        foreach (KeyValuePair<string, string> pair in _translations)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => Arabic;
}
