using System.Buffers;
using System.Reflection;
using Babel.Ai.Boundary;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>تعريفان لشيءٍ واحد ينحرفان — والسؤال ليس «هل» بل «متى».</b>
/// <para>
/// المِصفاة تحمل شكل رقم التسجيل الضريبي ومجموعة المحارف غير المرئية <b>وهما مكتوبان
/// أصلاً</b> في هذا المستودع: <c>SaudiVatNumber</c> في <c>Babel.Purchasing</c>، و
/// <c>ComplianceText</c> في <c>Babel.Compliance</c>. والتكرار هنا <b>مفروضٌ بالمعمارية
/// لا مُختار</b>: القاعدة 3 تمنع <c>Babel.Ai</c> من الإشارة إلى وحدةٍ أفقية أخرى، و
/// <c>SaudiVatNumber</c> فوق ذلك <c>internal</c>.
/// </para>
/// <para>
/// <b>فالثمن يُدفع هنا:</b> مشروع الاختبار — لا مشروع منتج — يشير إلى الوحدة المالكة
/// ويقرأ تعريفها <b>بالانعكاس</b>، ويُطابق حكمَه بحكم المِصفاة على جدولٍ من الحالات.
/// انحرافٌ بين التعريفين يُحمِّر البناء بدل أن يعيش صامتاً حتى يمرّ رقمٌ من أحدهما ويقف
/// عند الآخر. وهي السابقة نفسها في هذا المشروع: الإشارة إلى مزوّد الهيئة موجودة لأن
/// «الفاكّ عكس المُرمِّز القائم بالضبط» لا يُثبَت إلا بالمُرمِّز نفسه.
/// </para>
/// </summary>
public sealed class TheScrubberAgreesWithTheRepositoryValidators
{
    private static readonly Type SaudiVatNumber =
        typeof(Babel.Purchasing.PurchasingModuleInfo).Assembly
            .GetType("Babel.Purchasing.Application.SaudiVatNumber", throwOnError: true)!;

    /// <summary>
    /// جدولٌ يُحكَم عليه مرّتين: بشكل المِصفاة، وبالمُتحقِّق المالك. والاتّفاق مطلوب على
    /// <b>القبول والرفض معاً</b> — لا على القبول وحده، فالقبول وحده يمرّ بحارسٍ لا يرفض شيئاً.
    /// </summary>
    public static TheoryData<string> VatCandidates() =>
    [
        "300123456789003",     // سليم
        "399999999999993",     // سليم عند الحدّ الأعلى
        "300000000000003",     // سليم عند الحدّ الأدنى
        "30012345678900",      // أربع عشرة خانة
        "3001234567890033",    // ستّ عشرة خانة
        "400123456789003",     // لا يبدأ بـ3
        "300123456789004",     // لا ينتهي بـ3
        "30012345678900A",     // ليست كلّها خانات
        "",                    // فارغ — «لم يُسجَّل رقم»
    ];

    /// <summary>شكل الضريبة في المِصفاة يوافق <c>SaudiVatNumber.Validate</c> قبولاً ورفضاً.</summary>
    /// <param name="candidate">المرشَّح.</param>
    [Theory]
    [MemberData(nameof(VatCandidates))]
    public void TheDuplicatedVatShapeAgreesWithTheOwningModulesValidator(string candidate)
    {
        MethodInfo validate = SaudiVatNumber.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)!;
        Result<string> owned = (Result<string>)validate.Invoke(null, [candidate])!;

        bool scrubberSeesAVatNumber = AgentIdentifierShapes.Vat.Matches(candidate);

        Assert.Equal(owned.IsSuccess, scrubberSeesAVatNumber);
    }

    /// <summary>
    /// مجموعة المحارف غير المرئية هي المجموعة نفسها، محرفاً بمحرف — لا «قريبة منها».
    /// </summary>
    [Fact]
    public void TheInvisibleControlSetIsTheOneTheOwningModuleAlreadyEnumerates()
    {
        FieldInfo field = SaudiVatNumber.GetField(
            "InvisibleControls", BindingFlags.NonPublic | BindingFlags.Static)!;
        SearchValues<char> owned = (SearchValues<char>)field.GetValue(null)!;

        foreach (char character in AgentBoundaryText.InvisibleControls)
        {
            Assert.True(
                owned.Contains(character),
                "محرفٌ في مجموعة الحدّ وليس في مجموعة الوحدة المالكة: U+"
                + ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        // والعكس: لا محرف عند المالك خارج مجموعتنا. تُقرأ المجموعة كلّها بالمسح على
        // المدى الذي تسكنه هذه المحارف — فلا يُعاد كتابة الجدول هنا مرّة ثالثة.
        for (int codePoint = 0; codePoint <= 0xFFFF; codePoint++)
        {
            char character = (char)codePoint;
            if (owned.Contains(character))
            {
                Assert.Contains(character, AgentBoundaryText.InvisibleControls);
            }
        }
    }

    /// <summary>
    /// <b>الآيبان تعريفٌ واحد بعد اليوم.</b> كان <c>VoiceDisclosure</c> يحمل
    /// <c>SA[0-9]{22}</c> المتّصل وحده — وهو <b>يفوت</b> الصيغة التي يكتبها الناس فعلاً.
    /// وهذا الاختبار يقيس أن الثغرة أُغلقت وأن الحارسَين يقرآن الشكل نفسه.
    /// </summary>
    [Fact]
    public void TheSpokenGuardAndTheScrubberReadOneIbanShapeNotTwo()
    {
        foreach (string spelling in new[]
        {
            BoundaryFixtures.Iban,
            BoundaryFixtures.IbanGrouped,
            "SA03-8000-0000-6080-1016-7519",
        })
        {
            Assert.True(
                AgentIdentifierShapes.Iban.Matches(spelling),
                "المِصفاة لا ترى «" + spelling + "» آيباناً");

            Assert.True(
                VoiceDisclosure.Guard("الحساب البنكي " + spelling).IsFailure,
                "الحارس المنطوق لا يرى «" + spelling + "» آيباناً — وهي الثغرة التي أُغلقت");
        }
    }

    /// <summary>
    /// القناع في المِصفاة هو قناع الموارد البشرية نفسه. لو تغيّر <c>MaskPrefix</c> ولم
    /// يتغيّر النمط لعبرت كل قيمة مقنَّعة بصمت.
    /// </summary>
    [Fact]
    public void TheMaskShapeIsTheMaskTheHumanResourcesModuleWrites()
    {
        Assert.True(AgentIdentifierShapes.MaskedValue.Matches(VoiceDisclosure.MaskPrefix));
        Assert.True(AgentIdentifierShapes.MaskedValue.Matches(VoiceDisclosure.Mask("1092837465")));
        Assert.False(AgentIdentifierShapes.MaskedValue.Matches("قائمة: • بند أول • بند ثانٍ"));
    }

    /// <summary>
    /// أنظمة الأرقام تُقرأ من <c>ArabicNumerals</c> ولا تُعاد كتابتها: الطيّ يوافقه
    /// على الأنظمة الأربعة كلّها، بما فيها الديفاناغرية التي تصل من لوحة مفاتيح هندية.
    /// </summary>
    [Fact]
    public void TheDigitSystemsAreTheFourTheRepositoryAlreadyNames()
    {
        foreach (char zero in new[] { '0', '٠', '۰', '०' })
        {
            Assert.True(ArabicNumerals.IsDigitInAnySystem(zero));

            char[] leading = [(char)(zero + 1), zero];
            string ten = new string(leading) + "92837465";
            Assert.True(
                AgentIdentifierShapes.NationalId.Matches(ten),
                "نظام أرقام يعرفه ArabicNumerals ولا يطويه الحدّ: U+"
                + ((int)zero).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
