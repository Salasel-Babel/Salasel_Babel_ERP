using System.Text;

namespace Babel.Canonicalization.Tests;

/// <summary>
/// سياسة النص: <b>الحدّ يُحوِّل، والمُجزِّئ يتحقّق</b>.
/// وهذه هي الخاصية التي تجعل «البصمة تربط البايتات المخزَّنة» مبرهنة لا موعودة.
/// </summary>
public sealed class TextPolicyTests
{
    private const string Rlm = "\u200F";
    private const string Lrm = "\u200E";
    private const string Rlo = "\u202E";
    private const string Alm = "\u061C";
    private const string Zwsp = "\u200B";
    private const string Zwnj = "\u200C";
    private const string Bom = "\uFEFF";
    private const string SoftHyphen = "\u00AD";
    private const string Nbsp = "\u00A0";
    private const string NarrowNbsp = "\u202F";
    private const string Tatweel = "ـ";
    private const string LamAlef = "ﻻ";

    // ═════════ السياسة: رفض عند التجزئة، إزالة عند الحدّ ═════════

    [Theory]
    [InlineData(Rlm)]
    [InlineData(Lrm)]
    [InlineData(Rlo)]
    [InlineData(Alm)]
    [InlineData(Zwsp)]
    [InlineData(Zwnj)]
    [InlineData(Bom)]
    [InlineData(SoftHyphen)]
    [InlineData("\u2066")]
    [InlineData("\u2069")]
    [InlineData("\u202A")]
    [InlineData("\u202C")]
    public void EveryInvisibleFormatControlIsRejectedByTheHasher(string control)
    {
        var ex = Assert.Throws<CanonicalizationException>(
            () => CanonicalValue.Text("فرع" + control + "الرياض"));
        Assert.Equal(CanonErrors.TextFormatControl, ex.Code);
        Assert.Equal(3, ex.Index);
    }

    [Theory]
    [InlineData(Rlm)]
    [InlineData(Rlo)]
    [InlineData(Bom)]
    [InlineData(Zwsp)]
    public void CleanForInputRemovesThemAtTheBoundary(string control)
    {
        var dirty = "فرع" + control + "الرياض";
        var cleaned = TextRules.CleanForInput(dirty);
        Assert.Equal("فرع" + "الرياض", cleaned);
        TextRules.RequireCanonical(cleaned);
    }

    [Theory]
    [InlineData(Nbsp)]
    [InlineData(NarrowNbsp)]
    [InlineData("\u2007")]
    public void NonAsciiSpacesAreRejectedAndFoldedAtTheBoundary(string space)
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a" + space + "b"));
        Assert.Equal(CanonErrors.TextNonAsciiSpace, ex.Code);
        Assert.Equal("a b", TextRules.CleanForInput("a" + space + "b"));
    }

    /// <summary>
    /// لماذا الرفض لا الإزالة: لو أزال المُجزِّئ المحارف غير المرئية، لأمكن تعديل
    /// النص المخزَّن بإدراجها أو حذفها دون أن تتغيّر البصمة — أي أن البصمة تتوقّف
    /// عن ربط ما هو مخزَّن.
    /// </summary>
    [Fact]
    public void StrippingAtHashTimeWouldMakeStoredTextEditableWithoutDetection()
    {
        var stored = "فرع الرياض";
        var edited = "فرع" + Rlm + " الرياض";

        // سياسة «الإزالة عند التجزئة» الافتراضية لكانت تعطي البصمة نفسها:
        Assert.Equal(stored, TextRules.CleanForInput(edited));

        // سياستنا: القيمة المعدَّلة لا تصل إلى دالة التجزئة أصلاً.
        Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text(edited));

        // والقيمة المخزَّنة تُجزَّأ كما هي، حرفاً بحرف.
        Assert.Equal(stored, ((CanonicalValue)CanonicalValue.Text(stored)).Payload);
    }

    // ═════════ NFC ═════════

    [Fact]
    public void TheHasherValidatesNfcAndNeverNormalises()
    {
        const string decomposed = "أرباح";
        const string composed = "أرباح";

        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text(decomposed));
        Assert.Equal(CanonErrors.TextNotNfc, ex.Code);

        Assert.Equal(composed, TextRules.CleanForInput(decomposed));
        Assert.Equal(composed, ((CanonicalValue)CanonicalValue.Text(composed)).Payload);
    }

    [Fact]
    public void CleanForInputIsIdempotent()
    {
        string[] samples =
        [
            "فرع" + Rlm + " الرياض" + Bom,
            "أرباح",
            LamAlef + " يوجد",
            "a\r\nb\rc",
            "١٠٠ ريال",
            "a" + Nbsp + "b"
        ];

        foreach (var s in samples)
        {
            var once = TextRules.CleanForInput(s);
            Assert.Equal(once, TextRules.CleanForInput(once));
            TextRules.RequireCanonical(once);
        }
    }

    // ═════════ الأرقام ═════════

    [Theory]
    [InlineData("١٠٠", "100")]   // ١٠٠ عربية-هندية
    [InlineData("۱۲۳", "123")]   // ۱۲۳ شرقية
    public void NonAsciiDigitsAreRejectedAndFoldedAtTheBoundary(string digits, string expected)
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("قيد " + digits));
        Assert.Equal(CanonErrors.TextNonAsciiDigit, ex.Code);
        Assert.Equal("قيد " + expected, TextRules.CleanForInput("قيد " + digits));
    }

    // ═════════ أشكال العرض ═════════

    [Fact]
    public void ArabicPresentationFormsAreRejectedBecauseNfcDoesNotFixThem()
    {
        Assert.Equal(LamAlef, LamAlef.Normalize(NormalizationForm.FormC));  // NFC لا يفكّها
        Assert.Equal("لا", LamAlef.Normalize(NormalizationForm.FormKC));

        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text(LamAlef + " يوجد"));
        Assert.Equal(CanonErrors.TextPresentationForm, ex.Code);

        Assert.Equal("لا يوجد", TextRules.CleanForInput(LamAlef + " يوجد"));
    }

    [Fact]
    public void CleanForInputDoesNotApplyFullNfkcToTheWholeString()
    {
        // NFKC الكامل يحوّل ﷼ (U+FDFC) إلى «ريال» ويكسر الرموز والأرقام العلوية.
        // تنظيفنا يطبّق NFKC على أشكال العرض العربية وحدها.
        Assert.Equal("50%", TextRules.CleanForInput("50%"));
        Assert.Equal("m2", TextRules.CleanForInput("m2"));
        Assert.Equal("²", "²".Normalize(NormalizationForm.FormC));
        Assert.Equal("2", "²".Normalize(NormalizationForm.FormKC));
        Assert.Equal("²", TextRules.CleanForInput("²"));
    }

    // ═════════ التطويل: مسموح في القيمة الموقَّعة ═════════

    [Fact]
    public void TatweelIsALegitimateSignedCharacterAndOnlySearchFoldsIt()
    {
        var withTatweel = "مكـــتب";
        TextRules.RequireCanonical(withTatweel);          // لا يرمي

        Assert.NotEqual(
            ((CanonicalValue)CanonicalValue.Text(withTatweel)).Payload,
            ((CanonicalValue)CanonicalValue.Text("مكتب")).Payload);

        Assert.Equal(
            ArabicSearch.Normalize(withTatweel).Value,
            ArabicSearch.Normalize("مكتب").Value);

        Assert.Equal(Tatweel, "ـ");
    }

    // ═════════ محارف التحكّم ونهايات الأسطر ═════════

    [Fact]
    public void LineFeedIsTheOnlyPermittedControlCharacter()
    {
        TextRules.RequireCanonical("سطر\nسطر");

        Assert.Equal(CanonErrors.TextCarriageReturn,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a\rb")).Code);
        Assert.Equal(CanonErrors.TextControlChar,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a\tb")).Code);
        Assert.Equal(CanonErrors.TextNul,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a" + "\u0000" + "b")).Code);
        Assert.Equal(CanonErrors.TextControlChar,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a\u0085b")).Code);
    }

    [Fact]
    public void CleanForInputNormalisesEveryLineEndingToLf()
    {
        Assert.Equal("a\nb\nc\nd", TextRules.CleanForInput("a\r\nb\rc\nd"));
        Assert.Equal("a\nb", TextRules.CleanForInput("a\u2028b"));   // U+2028 LINE SEPARATOR
    }

    [Fact]
    public void LoneSurrogatesAreRejectedWithAClearCodeNotAnObscureArgumentException()
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("قيد\uD800"));
        Assert.Equal(CanonErrors.TextLoneSurrogate, ex.Code);
    }

    [Fact]
    public void ValidAstralPlaneTextIsAccepted()
    {
        const string emoji = "💰";     // 💰 U+1F4B0
        TextRules.RequireCanonical("رصيد " + emoji);
    }

    [Fact]
    public void NoncharactersAndPrivateUseAreRejected()
    {
        Assert.Equal(CanonErrors.TextNoncharacter,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a\uFFFEb")).Code);
        Assert.Equal(CanonErrors.TextPrivateUse,
            Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("a\uE000b")).Code);
    }

    // ═════════ تقرير التنظيف ═════════

    [Fact]
    public void VerboseCleaningReportsWhatItChangedForTheAuditTrail()
    {
        var (cleaned, changes) = TextRules.CleanForInputVerbose(
            "فرع" + Rlm + " الرياض" + Nbsp + "١٠٠");
        Assert.Equal("فرع الرياض 100", cleaned);
        Assert.NotEmpty(changes);
        Assert.Contains(changes, c => c.Contains("U+200F", StringComparison.Ordinal));
        Assert.Contains(changes, c => c.Contains("U+00A0", StringComparison.Ordinal));
    }

    [Fact]
    public void InspectReturnsTheFirstProblemWithoutThrowing()
    {
        Assert.Null(TextRules.Inspect("فرع الرياض"));
        var problem = TextRules.Inspect("فرع" + Rlm + " الرياض", "memo_ar");
        Assert.NotNull(problem);
        Assert.Equal(CanonErrors.TextFormatControl, problem!.Code);
        Assert.Equal("memo_ar", problem.Field);
    }
}
