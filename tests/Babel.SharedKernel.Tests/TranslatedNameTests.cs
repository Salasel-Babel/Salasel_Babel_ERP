using Xunit;

namespace Babel.SharedKernel.Tests;

/// <summary>
/// الاسم الذي سجلُّه عربي وترجماته صفوف (ADR-0021). ما يرفضه هذا النوع هو ما يجعله مفيداً.
/// </summary>
public sealed class TranslatedNameTests
{
    [Fact]
    public void TwoNamesWithTheSameContentAreEqual()
    {
        // الاسم قيمة لا مرجع: نسختان بمحتوى واحد تتساويان — وإلا صارت كل مقارنة
        // في اختبار أو في ذاكرة مؤقّتة تعتمد على هوية القاموس لا على محتواه.
        TranslatedName first = new("مبيعات", new Dictionary<string, string> { ["en"] = "Sales", ["ur"] = "فروخت" });
        TranslatedName second = new("مبيعات", new Dictionary<string, string> { ["ur"] = "فروخت", ["en"] = "Sales" });

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentTranslationsAreNotEqual()
    {
        TranslatedName first = new("مبيعات", new Dictionary<string, string> { ["en"] = "Sales" });
        TranslatedName second = new("مبيعات", new Dictionary<string, string> { ["en"] = "Revenue" });
        TranslatedName third = new("مبيعات");

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void ArabicIsMandatoryAndNeverBlank()
    {
        // العربي هو السجلّ ومرجع الارتداد: اسمٌ بلا عربي لا يُبنى أصلاً.
        Assert.Throws<ArgumentNullException>(() => new TranslatedName(null!));
        Assert.Throws<ArgumentException>(() => new TranslatedName(string.Empty));
        Assert.Throws<ArgumentException>(() => new TranslatedName("   "));
        Assert.Throws<ArgumentException>(() => new TranslatedName("\t\n"));
    }

    [Fact]
    public void ArabicIsTrimmed()
    {
        Assert.Equal("مبيعات", new TranslatedName("  مبيعات  ").Arabic);
    }

    [Fact]
    public void AnyNumberOfLanguagesIsExpressible()
    {
        // البند 2 من ADR-0021 حرفياً: «قابلية الترجمة إلى أيّ عدد من اللغات».
        // خمس لغات هنا، ولا عمود جديد ولا نوع جديد ولا فرع في الشيفرة.
        TranslatedName name = new(
            "الصندوق",
            new Dictionary<string, string>
            {
                ["en"] = "Cash",
                ["ur"] = "نقدی",
                ["hi"] = "नकद",
                ["am"] = "ጥሬ ገንዘብ",
                ["tl"] = "Salapi",
            });

        Assert.Equal(5, name.TranslationCount);
        Assert.Equal("नकद", name.In("hi"));
        Assert.Equal("ጥሬ ገንዘብ", name.In("am"));
        Assert.Equal("Salapi", name.In("tl"));
    }

    [Fact]
    public void ArabicIsNeverStoredAsATranslationOfItself()
    {
        // السجلّ ليس ترجمةً لنفسه. قبولُه في الخريطة يُنتج مصدرين للحقيقة
        // ويجعل «العربي» قابلاً للاختلاف عن العربي.
        Assert.Throws<ArgumentException>(
            () => new TranslatedName("مبيعات", new Dictionary<string, string> { ["ar"] = "مبيعات أخرى" }));
        Assert.Throws<ArgumentException>(
            () => new TranslatedName("مبيعات", new Dictionary<string, string> { ["AR"] = "مبيعات أخرى" }));
    }

    [Fact]
    public void AbsenceIsAnAbsentEntryNotAnEmptyString()
    {
        // ترجمة فارغة تُنتج عموداً بلا عنوان يمرّ فحصَ «هل توجد ترجمة؟» — وهو
        // بالضبط العطل الذي لا يُبلَّغ عنه (ADR-0021، بند الارتداد).
        Assert.Throws<ArgumentException>(
            () => new TranslatedName("مبيعات", new Dictionary<string, string> { ["en"] = string.Empty }));
        Assert.Throws<ArgumentException>(
            () => new TranslatedName("مبيعات", new Dictionary<string, string> { ["en"] = "   " }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1en")]
    [InlineData("en-")]
    [InlineData("en--GB")]
    [InlineData("en_GB")]
    [InlineData("en GB")]
    [InlineData("عر")]
    public void MalformedLanguageTagsAreRefused(string tag)
    {
        Assert.False(TranslatedName.IsWellFormedLanguageTag(tag));
        Assert.Throws<ArgumentException>(
            () => new TranslatedName("مبيعات", new Dictionary<string, string> { [tag] = "Sales" }));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ur")]
    [InlineData("en-GB")]
    [InlineData("zh-Hant-HK")]
    public void WellFormedLanguageTagsAreAccepted(string tag)
    {
        Assert.True(TranslatedName.IsWellFormedLanguageTag(tag));
        Assert.Equal("Sales", new TranslatedName("مبيعات", new Dictionary<string, string> { [tag] = "Sales" }).In(tag));
    }

    [Fact]
    public void ATagLongerThanTheMaximumIsRefused()
    {
        string tag = new('a', TranslatedName.MaximumLanguageTagLength + 1);

        Assert.False(TranslatedName.IsWellFormedLanguageTag(tag));
        Assert.True(TranslatedName.IsWellFormedLanguageTag(new string('a', TranslatedName.MaximumLanguageTagLength)));
    }

    [Fact]
    public void ResolutionFallsBackToTheRecordNotToBlankAndSaysSo()
    {
        // الارتداد إلى العربية لا إلى الفراغ ولا إلى المفتاح — **ويُعلَن**.
        TranslatedName name = new("مبيعات", new Dictionary<string, string> { ["en"] = "Sales" });

        NameResolution missing = name.Resolve("ur-PK");

        Assert.Equal("مبيعات", missing.Text);
        Assert.Equal("ar", missing.LanguageTag);
        Assert.True(missing.IsFallback);
    }

    [Fact]
    public void RegionalTagFallsBackToItsPrimarySubtagBeforeTheRecord()
    {
        TranslatedName name = new("مبيعات", new Dictionary<string, string> { ["ur"] = "فروخت" });

        NameResolution resolved = name.Resolve("ur-PK");

        Assert.Equal("فروخت", resolved.Text);
        Assert.Equal("ur", resolved.LanguageTag);
        Assert.False(resolved.IsFallback);
    }

    [Fact]
    public void AnExactTagWinsOverItsPrimarySubtag()
    {
        TranslatedName name = new(
            "مبيعات",
            new Dictionary<string, string> { ["en"] = "Sales", ["en-GB"] = "Turnover" });

        Assert.Equal("Turnover", name.In("en-GB"));
        Assert.Equal("Sales", name.In("en-US"));
    }

    [Fact]
    public void AskingForTheRecordLanguageIsNotAFallback()
    {
        TranslatedName name = new("مبيعات", new Dictionary<string, string> { ["en"] = "Sales" });

        foreach (string tag in new[] { "ar", "AR", "ar-SA", "ar-EG" })
        {
            NameResolution resolved = name.Resolve(tag);

            Assert.Equal("مبيعات", resolved.Text);
            Assert.False(resolved.IsFallback);
        }

        Assert.False(name.Resolve(null).IsFallback);
    }

    [Fact]
    public void AddingALanguageIsAnEntryNotASchemaChange()
    {
        // هذا الاختبار هو القرار نفسه مكتوباً بشيفرة: اللغة الخامسة سطرُ بيانات.
        TranslatedName before = new("مركز التكلفة الرئيسي", new Dictionary<string, string> { ["en"] = "Main cost centre" });
        TranslatedName after = before.With("ur", "مرکزی لاگت مرکز");

        Assert.Equal(1, before.TranslationCount);
        Assert.Equal(2, after.TranslationCount);
        Assert.Equal("مرکزی لاگت مرکز", after.In("ur"));
        Assert.Equal("مركز التكلفة الرئيسي", after.Arabic);
    }

    [Fact]
    public void TranslationsAreOrderedByOrdinalTagRegardlessOfInsertionOrder()
    {
        // الترتيب الحتمي شرطُ حتميّةِ كل ما يُشتقّ من الاسم — عرضاً كان أو تسلسلاً.
        TranslatedName name = new(
            "الصندوق",
            new Dictionary<string, string> { ["ur"] = "نقدی", ["am"] = "ጥሬ ገንዘብ", ["en"] = "Cash" });

        Assert.Equal(["am", "en", "ur"], name.Translations.Keys);
    }

    [Fact]
    public void TheMapIsImmutableFromOutside()
    {
        Dictionary<string, string> source = new() { ["en"] = "Sales" };
        TranslatedName name = new("مبيعات", source);

        source["en"] = "Revenue";
        source["ur"] = "فروخت";

        Assert.Equal("Sales", name.In("en"));
        Assert.Equal(1, name.TranslationCount);
    }
}
