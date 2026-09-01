using System.Globalization;
using Babel.Ai.Lookup;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>الطيّ يجعل هذه الأزواج اسماً واحداً — وزوجٌ حقيقيّ لكل قاعدة.</b>
/// <para>
/// كل صفٍّ أدناه اختلافٌ يقع فعلاً في إدخال عربي: همزة تُكتب ولا تُكتب، وتاءٌ مربوطة
/// تُكتب هاءً، وألفٌ مقصورة تُكتب ياءً، وتطويلٌ يُمدّ، وتشكيلٌ يُلصق من مستند، ورقمٌ
/// عربيّ-هنديّ يُكتب لاتينياً — <b>ورقمان في نظامين هنا يُطبَّعان لا يُرفضان</b>.
/// </para>
/// <para>
/// <b>وهذا يخالف <c>ArabicNumerals</c> عن قصد، والفرق فرقُ ثمنٍ لا ذوق.</b> هناك يُرفض
/// <c>١٢3</c> لأن المبلغ يُرحَّل ورقمٌ خاطئ يدخل الدفتر. وهنا المفتاح <b>يوسّع مجموعة
/// المرشّحين لا غير</b>، وما ينتج عن التوسيع سؤالٌ للمستخدم لا قيدٌ في دفتر. فرفضُ
/// «مستودع ٣» لأن المستخدم كتب «مستودع 3» هو رفضُ سؤالٍ صحيح.
/// </para>
/// </summary>
public sealed class TheFoldMakesTheseVariantsOneName
{
    /// <summary>قاعدةٌ، ووجهاها، والاسم الشائع للاختلاف.</summary>
    public static TheoryData<string, string, string> Pairs()
    {
        TheoryData<string, string, string> data = [];
        data.Add("همزة القطع فوق الألف", "أحمد", "احمد");
        data.Add("همزة الوصل تحت الألف", "إبراهيم", "ابراهيم");
        data.Add("ألف المدّة", "آدم", "ادم");
        data.Add("ألف الوصل U+0671", "ٱحمد", "احمد");
        data.Add("التطويل U+0640", "محمــــد", "محمد");
        data.Add("الشدّة", "محمّد", "محمد");
        data.Add("الحركات كاملةً", "مُحَمَّدٌ", "محمد");
        data.Add("التاء المربوطة والهاء", "فاطمة", "فاطمه");
        data.Add("الألف المقصورة والياء", "يحيى", "يحيي");
        data.Add("الهمزة على الواو", "مؤسسة", "موسسه");
        data.Add("الهمزة على الياء", "رئيس", "رييس");
        data.Add("الأرقام العربية-الهندية", "مستودع ٣", "مستودع 3");
        data.Add("الأرقام الشرقية الموسّعة", "مستودع ۳", "مستودع 3");
        data.Add("حالة الأحرف اللاتينية", "Al-Masar LLC", "al-masar llc");
        data.Add("المسافة غير الفاصلة U+00A0", "شركة\u00A0المسار", "شركة المسار");
        data.Add("علامة الاتجاه U+200F", "شركة\u200Fالمسار", "شركةالمسار");
        data.Add("فراغٌ مكرّر وأطراف", "  شركة   المسار  ", "شركة المسار");
        data.Add("اسم شركةٍ حقيقيّ", "شركة المسار الامثل", "شركة المسار الأمثل");
        data.Add("اسمٌ مركّب حقيقيّ", "محمد القحطاني", "محمّد القحطاني");
        return data;
    }

    /// <summary>أزواجٌ يجب أن تبقى مفروقة — الطيّ يوحّد الرسم لا الأسماء.</summary>
    public static TheoryData<string, string> Separated()
    {
        TheoryData<string, string> data = [];
        data.Add("محمد علي القحطاني", "محمد القحطاني");
        data.Add("القحطاني", "القحطان");
        data.Add("محمد القحطاني", "محمد الغامدي");
        data.Add("الرياض", "رياض");
        data.Add("شركة المسار", "شركة المسارات");
        return data;
    }

    /// <summary>كل زوجٍ يطوى إلى المفتاح نفسه حرفاً بحرف.</summary>
    /// <param name="rule">القاعدة المختبَرة.</param>
    /// <param name="left">الوجه الأول.</param>
    /// <param name="right">الوجه الثاني.</param>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryPairFoldsToTheSameKey(string rule, string left, string right)
    {
        Assert.Equal(ArabicNameFold.Fold(right), ArabicNameFold.Fold(left));
        Assert.False(
            string.Equals(left, right, StringComparison.Ordinal),
            "الزوج «" + rule + "» متطابق قبل الطيّ، فلا يُثبت شيئاً");
    }

    /// <summary>
    /// <b>والمفتاح الضيّق يلتقط ما يفلت من الطيّ الكامل.</b> «عبدالله» و«عبد الله» يبقيان
    /// عند 0.545 مقيسة بعد الطيّ — دون أي عتبةٍ معقولة — ويتساويان هنا.
    /// </summary>
    [Fact]
    public void TheTightKeyCatchesTheSplitCompoundName()
    {
        Assert.NotEqual(ArabicNameFold.Fold("عبد الله"), ArabicNameFold.Fold("عبدالله"));
        Assert.Equal(ArabicNameFold.FoldTight("عبد الله"), ArabicNameFold.FoldTight("عبدالله"));
    }

    /// <summary>ما يجب أن يبقى مفروقاً يبقى مفروقاً على المفتاحين معاً.</summary>
    /// <param name="left">الاسم الأول.</param>
    /// <param name="right">الاسم الثاني.</param>
    [Theory]
    [MemberData(nameof(Separated))]
    public void NamesThatMustStayApartStayApart(string left, string right)
    {
        Assert.NotEqual(ArabicNameFold.Fold(right), ArabicNameFold.Fold(left));
        Assert.NotEqual(ArabicNameFold.FoldTight(right), ArabicNameFold.FoldTight(left));
    }

    /// <summary>
    /// <b>حارس لافراغ:</b> نصٌّ يطوى إلى فراغ يجب أن يُعرف فراغاً، لا أن يمرّ ويطابق السجلّ كلّه.
    /// وطيٌّ يتوقّف عن العمل يجعل كل ما فوقه يمرّ بلا معنى — فهذا يُثبت أنه ينطق.
    /// </summary>
    [Fact]
    public void TheFoldIsNotVacuous()
    {
        Assert.Equal(string.Empty, ArabicNameFold.Fold("   \u200Fـــًٌٍ  "));
        Assert.Equal("محمد", ArabicNameFold.Fold("مُحَمَّدٌ"));
        Assert.Equal(
            "احمد",
            ArabicNameFold.Fold("أحمد").ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>بادئةٌ صارمة تُعرَف بادئةً — قياسُ قاعدة السبر، لا تطبيقها.</summary>
    [Fact]
    public void AStrictPrefixIsRecognisedAcrossOrthographicVariants()
    {
        Assert.True(ArabicNameFold.OneFoldsToAStrictPrefixOfTheOther("محمد", "محمّد عل"));
        Assert.False(ArabicNameFold.OneFoldsToAStrictPrefixOfTheOther("محمد", "محمد"));
        Assert.False(ArabicNameFold.OneFoldsToAStrictPrefixOfTheOther("محمد", "احمد"));
    }
}
