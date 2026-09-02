using System.Globalization;
using System.Text;
using Babel.Ai.Boundary;
using Babel.Ai.Voice;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>الشاهد الموجب كان لكل شكل، والثغرة كانت في الطيّ — فبقيت المنظومة كلّها خضراء.</b>
/// <para>
/// ‏<c>EveryShapeIsTheSoleGuardOnSomeText</c> طفرةُ <b>حذف شكل</b> وحدها. وكلّ ما هرب
/// فعلاً — شرطةٌ ليّنة بين خانتين، قطعُ سطر، أرقامٌ عريضة — لم يكن شكلاً ناقصاً بل
/// <b>صفَّ طيٍّ ناقصاً</b>، ويُسقِط <b>الأشكال السبعة معاً</b> لأن الطيّ يسبقها كلّها.
/// فمهما اكتمل الشاهد الموجب لكل شكل، بقي أعمى عن هذه الفئة بحكم البناء.
/// </para>
/// <para>
/// <b>فهذا الملفّ شاهدٌ موجب لكل <u>صفّ طيّ</u> لا لكل شكل</b>: كل نظام أرقام عشرية في
/// يونيكود، وكل فئةٍ غير مرئية، وكل صنف فراغ — <b>مسحاً على المستوى الأساسي كلّه لا
/// جدولاً مكتوباً بيد</b>. وجدولٌ مكتوب بيدٍ هو بعينه العطل الذي أنتج الثغرة: مسحٌ يقرأ
/// الفئة من يونيكود لا يمكن أن يكون ناقصاً بالنسيان.
/// </para>
/// </summary>
public sealed class TheFoldCarriesAPositiveControlPerClass
{
    /// <summary>
    /// <b>كل نظام أرقام عشرية في يونيكود</b> — لا الأربعة التي يسمّيها
    /// <c>ArabicNumerals</c>. كانت هويةٌ بأرقام <b>عريضة</b> (‏<c>U+FF10</c>) تعبر
    /// نظيفة، وهي ضغطةُ مفتاحٍ واحدة في أي مُدخِل شرق آسيوي ولا تُفرَّق بالعين.
    /// </summary>
    [Fact]
    public void EveryUnicodeDecimalDigitSystemFoldsAndTheIdentityIsCaught()
    {
        int systems = 0;
        List<string> escaped = [];

        foreach (int zero in DecimalZeroes())
        {
            systems++;

            // «1092837465» مكتوبةً بأرقام هذا النظام.
            StringBuilder identity = new(10);
            foreach (char digit in "1092837465")
            {
                identity.Append(char.ConvertFromUtf32(zero + (digit - '0')));
            }

            if (AgentOutboundScrubber.Inspect("رقم الهوية " + identity).IsClean)
            {
                escaped.Add("U+" + zero.ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        Assert.True(systems >= 60, "مسحُ أنظمة الأرقام لم يجد إلا " + systems + " نظاماً — المسح نفسه معطَّل");
        Assert.Empty(escaped);
    }

    /// <summary>
    /// <b>كل محرفٍ غير مرئي في المستوى الأساسي</b> يُدسّ بين خانتين: الشرطة الليّنة
    /// ‏<c>U+00AD</c> — وهي ما يدسّه لصقٌ عاديّ من PDF أو Word لا مهاجم — وواصل الكلمات
    /// ‏<c>U+2060</c> ومحدِّدات الصور <c>U+FE00+</c> وواصل العناقيد <c>U+034F</c>
    /// ومحارف التحكّم كلّها.
    /// </summary>
    [Fact]
    public void EveryInvisibleCharacterBetweenTwoDigitGroupsIsStillCaught()
    {
        int swept = 0;
        List<string> escaped = [];

        for (int codePoint = 0; codePoint <= 0xFFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            Rune rune = new(codePoint);
            if (!AgentBoundaryText.IsStripped(rune))
            {
                continue;
            }

            swept++;

            if (AgentOutboundScrubber.Inspect("رقم الهوية 1092" + rune + "837465").IsClean)
            {
                escaped.Add("U+" + codePoint.ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        Assert.True(swept >= 900, "مسحُ غير المرئي لم يجد إلا " + swept + " محرفاً — المسح نفسه معطَّل");
        Assert.Empty(escaped);
    }

    /// <summary>
    /// <b>كل فراغ في المستوى الأساسي</b> بين خانتين — والجدولة وقطعُ السطر منها.
    /// وقطعُ السطر هو ما يقع فعلاً في جسم <c>tool_result</c>، وهو الموضع الذي يسمّيه
    /// الحدّ نفسه أخطرَ المواضع.
    /// </summary>
    [Fact]
    public void EveryWhitespaceBetweenTwoDigitGroupsIsStillCaught()
    {
        int swept = 0;
        List<string> escaped = [];

        for (int codePoint = 0; codePoint <= 0xFFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            Rune rune = new(codePoint);
            if (!AgentBoundaryText.IsWhitespaceJoiner(rune))
            {
                continue;
            }

            swept++;

            if (AgentOutboundScrubber.Inspect("رقم الهوية 1092" + rune + "837465").IsClean)
            {
                escaped.Add("U+" + codePoint.ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        Assert.True(swept >= 25, "مسحُ الفراغ لم يجد إلا " + swept + " محرفاً — المسح نفسه معطَّل");
        Assert.Empty(escaped);
    }

    /// <summary>
    /// <b>والبديل المفرد لا يفصل خانتين.</b> تعدادُ النقاط يستبدله بـ<c>U+FFFD</c>، ولو
    /// بقي لصار فاصلاً لا يُلَمّ — وهو المدخل الوحيد الذي لا يبلغه مسحُ المستوى الأساسي
    /// أعلاه، لأنه ليس نقطةَ ترميزٍ صالحة أصلاً.
    /// </summary>
    [Fact]
    public void ALoneSurrogateBetweenTwoDigitGroupsIsStillCaught()
    {
        Assert.True(AgentOutboundScrubber.Inspect("رقم الهوية 1092\uD800837465").IsRefused);
        Assert.True(AgentOutboundScrubber.Inspect("رقم الهوية 1092\uFFFD837465").IsRefused);
    }

    /// <summary>
    /// <b>النقطة والفاصلة والمائلة تُلمّ للأشكال المُرتكِزة</b> — ولا يُخترع بذلك مبلغٌ
    /// ولا تاريخ، لأن الشكل المُرتكِز يحمل ارتكازه معه.
    /// </summary>
    [Theory]
    [InlineData("SA03.8000.0000.6080.1016.7519", "iban")]
    [InlineData("SA03,8000,0000,6080,1016,7519", "iban")]
    [InlineData("SA03/8000/0000/6080/1016/7519", "iban")]
    [InlineData("300.123.456.789.003", "vat")]
    [InlineData("05.12.34.56.78", "phone")]
    [InlineData("05.123.45678", "phone")]
    public void AnAnchoredIdentifierSplitByDotsIsRefusedByItsOwnShape(string text, string shapeKey)
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect("القيمة " + text);

        Assert.True(verdict.IsRefused, text);
        Assert.Contains(
            AgentIdentifierShapes.ByKey(shapeKey).Code,
            verdict.Errors.Select(static error => error.Code));
    }

    /// <summary>
    /// <b>وهذه هي الضريبة التي دفعناها ولم ندفعها:</b> ما دام اللمّ بالنقطة محصوراً في
    /// الأشكال المُرتكِزة، يبقى المبلغُ والتاريخ ورقمُ المستند نصّاً عادياً يعبر.
    /// <b>وهذا الصفّ هو الشاهد السالب المفقود</b> الذي جعل مجموعةَ الاختبارات خضراء وهي
    /// ترفض ثلث كلام المحاسب.
    /// </summary>
    [Theory]
    [InlineData("سجّل فاتورة مبيعات بمبلغ 1,500,000,000 ريال")]
    [InlineData("الرصيد الافتتاحي 1,000,000,000")]
    [InlineData("المبلغ 250,000,000 والضريبة 37,500,000")]
    [InlineData("قيد يومية بمبلغ 123,456,789.00")]
    [InlineData("مبلغ 12,345,678.90 بتاريخ 01/09/2026")]
    [InlineData("رقم الأمر INV-2026-000412")]
    [InlineData("سجّل 100 قطعة من الصنف أ")]
    [InlineData("الفاتورة رقم 4512 بتاريخ 2026-09-01")]
    public void TheAccountingSentenceWrittenWithItsSeparatorsPasses(string sentence)
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(sentence);

        Assert.True(
            verdict.IsClean,
            sentence + " ⇐ " + string.Join(" · ", verdict.Errors.Select(static error => error.Code)));
    }

    /// <summary>
    /// <b>والمقابل مُعلَن لا مُخفى:</b> المبلغ نفسه مكتوباً <b>بلا فواصل</b> يُرفض —
    /// وهو رفضٌ صحيح لأن «عشر خاناتٍ تبدأ بـ1» شكلُ هويةٍ ولا شيء محلّيّ يفرّق. والرسالة
    /// <b>تسمّي المخرج</b>، فتنتهي الدورة بدل أن تُعاد.
    /// </summary>
    [Theory]
    [InlineData("سجّل فاتورة مبيعات بمبلغ 1500000000 ريال")]
    [InlineData("الرصيد الافتتاحي 1000000000")]
    public void TheSameAmountWrittenBareIsRefusedAndTheRefusalNamesTheWayOut(string sentence)
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(sentence);

        Assert.True(verdict.IsRefused);
        Assert.All(
            verdict.Errors,
            static error => Assert.Contains(
                AgentBoundaryErrors.AmountRemedyAr, error.MessageAr, StringComparison.Ordinal));
    }

    /// <summary>
    /// القناعُ يُبنى من ثابت الموارد البشرية، ويحتمل الفراغ بين محارفه — و«قائمةٌ
    /// بنقاطٍ ونصّ» لا تطابق.
    /// </summary>
    [Fact]
    public void TheMaskIsCaughtEvenWhenItsUnitsAreSpacedApart()
    {
        Assert.True(AgentOutboundScrubber.Inspect("الموظف أحمد " + VoiceDisclosure.Mask("1092837465")).IsRefused);
        Assert.True(AgentOutboundScrubber.Inspect("الموظف أحمد • • • •7465").IsRefused);
        Assert.True(AgentOutboundScrubber.Inspect("الموظف أحمد •­•­•­•7465").IsRefused);
        Assert.True(AgentOutboundScrubber.Inspect("قائمة: • بند أول • بند ثانٍ • بند ثالث • بند رابع").IsClean);
    }

    /// <summary>
    /// المجموعة المُعدَّدة التي يتّفق عليها الحدُّ والوحدات المالكة <b>مطويّةٌ فعلاً</b>.
    /// فيبقى اختبار الاتّفاق حمّالاً: لو ضاقت فئةُ الطيّ يوماً دون تلك المجموعة لاحمرّ هذا.
    /// </summary>
    [Fact]
    public void TheEnumeratedAgreementSetIsASubsetOfWhatTheFoldStrips()
    {
        foreach (char character in AgentBoundaryText.InvisibleControls)
        {
            Assert.True(
                AgentBoundaryText.IsStripped(new Rune(character)),
                "محرفٌ في مجموعة الاتّفاق ولا يطويه الطيّ: U+"
                + ((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>نقاط بداية كل نظام أرقام عشرية في يونيكود — بالمسح لا بجدول.</summary>
    private static IEnumerable<int> DecimalZeroes()
    {
        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            Rune rune = new(codePoint);
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber
                && Rune.GetNumericValue(rune) == 0)
            {
                yield return codePoint;
            }
        }
    }
}
