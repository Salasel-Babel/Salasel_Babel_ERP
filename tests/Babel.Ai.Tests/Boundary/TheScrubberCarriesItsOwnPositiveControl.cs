using Babel.Ai.Boundary;
using Babel.Ai.Voice;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>حارسٌ لا يُثبَت أنه ينطق لا يُفرَّق عن حارسٍ معطَّل.</b>
/// <para>
/// نتيجة المِصفاة المعتادة «لا مخالفة». وهي بعينها نتيجةُ مِصفاةٍ توقّفت عن المطابقة —
/// نمطٌ كُسر في تحرير، أو شكلٌ حُذف من القائمة، أو طيٌّ صار لا يفعل شيئاً تحت
/// <c>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT</c>. فيبقى الفرع أخضر إلى الأبد وهو مفتوح.
/// وهذا الملفّ هو الشاهد الموجب المُودَع، على مثال
/// <c>TheSecretGuardCarriesItsOwnPositiveControl</c> و<c>tools/secret-scan/positive-control.txt</c>.
/// </para>
/// <para>
/// <b>وفيه ما هو أقوى من «كلٌّ ينطق»: كلٌّ <u>لازم</u>.</b> لكل شكلٍ نصٌّ <b>هو وحده</b>
/// من يمسكه — فحذفُه من القائمة لا يُنقص جملةً من رسالة، بل يفتح باباً. وهذه هي طفرةُ
/// الحذف مكتوبةً اختباراً دائماً بدل أن تُجرَّب مرّةً بيدٍ ثم تُنسى.
/// </para>
/// </summary>
public sealed class TheScrubberCarriesItsOwnPositiveControl
{
    /// <summary>
    /// لكل شكلٍ نصٌّ يُرفض به <b>وحده</b>. الشكل المُسمّى فيه هو الفرق بين رفضٍ وتسريب.
    /// </summary>
    private static readonly (string Key, string Text)[] Controls =
    [
        // هوية مقطوعة بمسافة: الشامل لا يراها (لا سلسلة متّصلة)، والسجلّ يستثني بادئة 1/2.
        ("national_id", "الموظف " + BoundaryFixtures.NationalId[..4] + " " + BoundaryFixtures.NationalId[4..]),
        // آيبان مجموعاً: أطول سلسلة متّصلة فيه أربع خانات.
        ("iban", "الحساب " + BoundaryFixtures.IbanGrouped),
        ("vat", "الرقم الضريبي 3001 2345 6789 003"),
        ("cr_or_national_id", "السجل التجاري 4030 123456"),
        ("phone", "الجوال 0512 345678"),
        // سلسلة طويلة لا يطابقها شكلٌ مُسمّى: الشبكة الأخيرة وحدها.
        ("digit_run", "المرجع " + BoundaryFixtures.DigitRun),
        // قناع: لا خانات فيه تسعاً، ولا بادئة، ولا طول.
        ("masked_value", "الموظف أحمد الغامدي " + VoiceDisclosure.Mask(BoundaryFixtures.NationalId)),
    ];

    /// <summary>الشواهد كما تقرؤها <c>xunit</c>.</summary>
    public static TheoryData<string, string> SoleCatchFixtures()
    {
        TheoryData<string, string> data = [];

        foreach ((string key, string text) in Controls)
        {
            data.Add(key, text);
        }

        return data;
    }

    /// <summary>
    /// الشاهد الموجب: واحدٌ من كل شكلٍ من الستّة المُسمّاة ⇐ رفضٌ بستّة أخطاء بالضبط.
    /// لا خمسة (شكلٌ سقط)، ولا سبعة (الشامل يُكرِّر ما طالب به غيره).
    /// </summary>
    [Fact]
    public void TheFixtureCarryingOneOfEachShapeIsRefusedWithExactlySixErrors()
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(BoundaryFixtures.OneOfEachShape);

        Assert.Equal(AgentScrubOutcome.Refused, verdict.Outcome);
        Assert.Equal(
            ["national_id", "iban", "vat", "cr_or_national_id", "phone", "digit_run"],
            verdict.Errors.Select(static error => error.Code[AgentBoundaryErrors.CodePrefix.Length..]));
    }

    /// <summary>
    /// <b>طفرة الحذف، مكتوبةً اختباراً.</b> لكل شكلٍ نصٌّ يُرفض <b>به وحده</b>: لو حُذف
    /// الشكل من <see cref="AgentIdentifierShapes.All"/> لعبر ذلك النصّ إلى النموذج.
    /// فالقائمة كلّها حاملةٌ، ولا فيها بندٌ زائد يُطمأنّ إلى وجود غيره.
    /// </summary>
    [Theory]
    [MemberData(nameof(SoleCatchFixtures))]
    public void EveryShapeIsTheSoleGuardOnSomeText(string shapeKey, string text)
    {
        AgentIdentifierShape shape = AgentIdentifierShapes.ByKey(shapeKey);
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(text);

        Assert.Equal(AgentScrubOutcome.Refused, verdict.Outcome);
        Assert.Equal(
            shape.Code,
            Assert.Single(verdict.Errors).Code);
    }

    /// <summary>
    /// القائمة مغلقة ومُعدَّدة بأسمائها. شكلٌ يُضاف بلا شاهدٍ موجب يُفشل هذا الاختبار
    /// قبل أن يُصدَّق صمتُه.
    /// </summary>
    [Fact]
    public void TheShapeListIsClosedAndEveryShapeCarriesAControl()
    {
        Assert.Equal(
            ["national_id", "iban", "vat", "cr_or_national_id", "phone", "digit_run", "masked_value"],
            AgentOutboundScrubber.Shapes.Select(static shape => shape.Key));

        Assert.Equal(
            AgentOutboundScrubber.Shapes.Select(static shape => shape.Key).Order(StringComparer.Ordinal),
            Controls.Select(static control => control.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// رمز كل شكل تحت صدرٍ واحد، وكل جملة عربية غير فارغة وتسمّي الشكل لا «خطأ».
    /// </summary>
    [Fact]
    public void EveryRefusalNamesItsShapeInArabic()
    {
        foreach (AgentIdentifierShape shape in AgentOutboundScrubber.Shapes)
        {
            Assert.StartsWith(AgentBoundaryErrors.CodePrefix, shape.Code, StringComparison.Ordinal);
            Assert.EndsWith(shape.Key, shape.Code, StringComparison.Ordinal);
            Assert.True(
                shape.Refusal.MessageAr.Length >= 20,
                shape.Key + ": جملة الرفض أقصر من أن تقول شيئاً");
            Assert.DoesNotContain("خطأ", shape.Refusal.MessageAr, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>الطيّ يعمل فعلاً — والصياغة الأولى كانت تدّعي حراسةَ ما لا تمسّه.</b>
    /// <para>
    /// كان نصّها يقول إنها تحرس صيرورة <c>Normalize</c> عمليةَ لا شيء تحت التدويل الثابت.
    /// <b>ولا شيء في كشف المعرّفات يعتمد على <c>Normalize</c>:</b> ردُّ الأرقام حسابٌ يدويّ
    /// على فئة يونيكود، ولا نمطَ يحتاج تركيباً قانونياً. فكان الاختبار يبقى أخضر خلال
    /// الانحدار الذي يسمّيه بعينه — وهو أخطر أشكال الحارس: اسمٌ يَعِد بحراسةٍ لا تقع.
    /// </para>
    /// <para>
    /// فصار يقيس ما يقع فعلاً: أنّ <b>ردّ الأرقام</b> يعمل على الأنظمة التي يسمّيها
    /// <c>ArabicNumerals</c> وعلى ما هو أوسع منها، وأنّ الطيّ ليس عمليةَ لا شيء —
    /// والمسحُ الكامل على كل نظامٍ عشريّ في يونيكود في
    /// <c>TheFoldCarriesAPositiveControlPerClass</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDigitFoldIsMeasuredNotAssumed()
    {
        Assert.False(
            AgentOutboundScrubber.Inspect("١٠٩٢٨٣٧٤٦٥").IsClean,
            "الأرقام العربية-الهندية لم تُطوَ — وطيٌّ لا يفعل شيئاً يُنتج «لا مخالفة» على كل نصّ");

        Assert.False(AgentOutboundScrubber.Inspect("۱۰۹۲۸۳۷۴۶۵").IsClean);
        Assert.False(AgentOutboundScrubber.Inspect("१०९२८३७४६५").IsClean);

        // وأوسع من الأربعة: عريضةٌ وبنغالية — وهما خارج ما يعرفه ArabicNumerals.
        Assert.False(AgentOutboundScrubber.Inspect("１０９２８３７４６５").IsClean);
        Assert.False(AgentOutboundScrubber.Inspect("১০৯২৮৩৭৪৬৫").IsClean);

        // ‏**والشاهد على أن الطيّ نفسه ليس لا-عملية**: نصٌّ لا رقم فيه يبقى كما هو،
        // ونصٌّ فيه غير مرئيّ يفقده — فلو صار الطيّ لا-عملية لتساوى الاثنان.
        Assert.NotEqual("1092\u00AD837465", AgentBoundaryText.Fold("1092\u00AD837465"));
    }

    /// <summary>
    /// عدد الأشكال مُعلَن: الستّة التي يُعدّدها جدول التصميم، والسابع — القناع — الذي
    /// تذكره حاشيته. عددٌ ينقص بلا أن يلاحظه أحد هو الحارس المعطَّل نفسه.
    /// </summary>
    [Fact]
    public void TheDeclaredShapeCountIsSixNamedPlusTheMask()
    {
        Assert.Equal(6 + 1, AgentOutboundScrubber.Shapes.Count);
        Assert.Equal("masked_value", AgentOutboundScrubber.Shapes[^1].Key);
    }
}
