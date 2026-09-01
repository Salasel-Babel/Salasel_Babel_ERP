using Babel.Ai.Boundary;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>المِصفاة الخارجة — والمعرّف المشوَّه معرّفٌ كامل عند من يقرؤه.</b>
/// <para>
/// المعرّف المكتوب كما هو يلتقطه أي نمط، وليس هو ما يُقاس هنا. الذي يُقاس هو المعرّف
/// الذي <b>حاول أن يمرّ</b>: بأرقام عربية-هندية، أو بتطويلٍ بين خاناته، أو بواصلٍ عديم
/// العرض، أو مقطوعاً بمسافة، أو ملتصقاً بكلمةٍ أطول منه.
/// </para>
/// <para>
/// <b>ومعها شاهدٌ سلبي في كل باب:</b> حارسٌ «يرفض دائماً» يمرّ في كل اختبار رفض ويُعطَّل
/// في أوّل يومٍ حقيقي. فما يجب أن يعبر — مبلغٌ بفواصله، وتاريخٌ بشرطاته، ورقم فاتورة —
/// مقيسٌ هنا بقدر ما يجب أن يُرفض.
/// </para>
/// </summary>
public sealed class AgentOutboundScrubberTests
{
    private static IReadOnlyList<string> Codes(string text) =>
        [.. AgentOutboundScrubber.Inspect(text).Errors.Select(static error => error.Code)];

    private static void Refuses(string text, string shapeKey)
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(text);

        Assert.Equal(AgentScrubOutcome.Refused, verdict.Outcome);
        Assert.Contains(AgentIdentifierShapes.ByKey(shapeKey).Code, Codes(text));
    }

    private static void Passes(string text)
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(text);

        Assert.True(
            verdict.IsClean,
            "نصٌّ سليم رُفض — والحارس الذي يرفض السليم يُعطَّل: «" + text + "» ⇐ "
            + string.Join(" · ", verdict.Errors.Select(static error => error.Code)));
    }

    // ── الأشكال الستّة كما يُكتب كلٌّ منها ──────────────────────────────────

    [Theory]
    [InlineData(BoundaryFixtures.NationalId, "national_id")]
    [InlineData(BoundaryFixtures.ResidencyId, "national_id")]
    [InlineData(BoundaryFixtures.Iban, "iban")]
    [InlineData(BoundaryFixtures.IbanGrouped, "iban")]
    [InlineData(BoundaryFixtures.Vat, "vat")]
    [InlineData(BoundaryFixtures.CommercialRegister, "cr_or_national_id")]
    [InlineData(BoundaryFixtures.Phone, "phone")]
    [InlineData(BoundaryFixtures.PhoneInternational, "phone")]
    [InlineData("00966512345678", "phone")]
    [InlineData(BoundaryFixtures.DigitRun, "digit_run")]
    public void كل_شكل_مُسمّى_يُرفض_ويُسمّى_باسمه(string value, string shapeKey) =>
        Refuses("سجّل للمورد " + value + " فاتورة", shapeKey);

    [Fact]
    public void الرفض_يحمل_الجملة_العربية_حرفاً_بحرف_كما_قرّرها_التصميم()
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(BoundaryFixtures.NationalId);

        Error only = Assert.Single(verdict.Errors);
        Assert.Equal("ai.agent.identifier_refused.national_id", only.Code);
        Assert.Equal(
            "لا أُرسل رقم الهوية أو الإقامة إلى النموذج. اكتبه في حقله على الشاشة.",
            only.MessageAr);
    }

    [Fact]
    public void رقم_هوية_واحد_يُنتج_جملةً_واحدة_لا_جملتين()
    {
        // الشامل يطابق العشر خانات نفسها. ولو بُلِّغ عنه لصار للمستخدم سببان لعطلٍ واحد،
        // وهو ما يجعله يقرأ الأوّل ويتجاهل الباقي.
        Assert.Equal(["ai.agent.identifier_refused.national_id"], Codes(BoundaryFixtures.NationalId));
        Assert.Equal(["ai.agent.identifier_refused.iban"], Codes(BoundaryFixtures.Iban));
        Assert.Equal(["ai.agent.identifier_refused.vat"], Codes(BoundaryFixtures.Vat));
        Assert.Equal(["ai.agent.identifier_refused.phone"], Codes(BoundaryFixtures.Phone));
    }

    // ── الحالات العدائية: المعرّف الذي يحاول أن يمرّ ────────────────────────

    [Theory]
    // أرقام عربية-هندية · فارسية موسّعة · ديفاناغرية — والأنظمة الأربعة من ArabicNumerals
    [InlineData("١٠٩٢٨٣٧٤٦٥")]
    [InlineData("۱۰۹۲۸۳۷۴۶۵")]
    [InlineData("१०९२८३७४६५")]
    // خلطُ نظامين في رقمٍ واحد: ArabicNumerals يرفضه، والمِصفاة **تطويه ثم ترفضه**
    [InlineData("١٠٩2837465")]
    // تطويل بين الخانات
    [InlineData("1092\u0640837465")]
    [InlineData("10\u064092\u0640837465")]
    // واصلٌ عديم العرض · فاصلٌ عديم العرض · علامة اتجاه · مسافة عديمة العرض · علامة ترتيب
    [InlineData("1092\u200D837465")]
    [InlineData("1092\u200C837465")]
    [InlineData("1092\u200F837465")]
    [InlineData("1092\u200B837465")]
    [InlineData("1092\uFEFF837465")]
    [InlineData("1092\u2066837465\u2069")]
    // مقطوعٌ بمسافة · بمسافة غير فاصلة · بمسافة ضيّقة · بمسافات كثيرة
    [InlineData("1092 837465")]
    [InlineData("1092\u00A0837465")]
    [InlineData("1092\u202F837465")]
    [InlineData("109 283 7465")]
    // مغروسٌ في كلمة أطول — النظرتان على الخانات وحدها لا على الحروف
    [InlineData("INV1092837465X")]
    [InlineData("مرجع/1092837465/أ")]
    public void رقم_هوية_يحاول_المرور_مشوَّهاً_يُلتقَط(string disguised) =>
        Refuses("الموظف أحمد الغامدي " + disguised + " من فرع الرياض", "national_id");

    [Theory]
    [InlineData(BoundaryFixtures.IbanGrouped)]
    [InlineData("SA03-8000-0000-6080-1016-7519")]
    [InlineData("SA03\u00A08000\u00A00000\u00A06080\u00A01016\u00A07519")]
    [InlineData("sa0380000000608010167519")]
    [InlineData("SA\u0660\u0663\u0668\u0660\u0660\u0660\u0660\u0660\u0660\u0660\u0666\u0660\u0668\u0660\u0661\u0660\u0661\u0666\u0667\u0665\u0661\u0669")]
    [InlineData("SA0380000000\u0640608010167519")]
    public void آيبان_يحاول_المرور_مشوَّهاً_يُلتقَط(string disguised) =>
        Refuses("حوّل إلى " + disguised + " المبلغ", "iban");

    [Fact]
    public void آيبان_ملتصق_بحرفٍ_قبله_يفوته_شكلُه_ويلتقطه_الشامل()
    {
        // النظرة الخلفية (?<![A-Za-z0-9]) تمنع «xSA…» من مطابقة شكل الآيبان — وهذا
        // مقصود: لولاها لالتُقط كل «…SA» في وسط كلمة. والشبكة الأخيرة هي التي تمسكه،
        // وهي بالضبط ما تُشترى به الطبقة الزائدة.
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect("مرجعxSA0380000000608010167519");

        Assert.Equal(AgentScrubOutcome.Refused, verdict.Outcome);
        Assert.Equal(["ai.agent.identifier_refused.digit_run"], Codes("مرجعxSA0380000000608010167519"));
    }

    [Theory]
    [InlineData("3001 2345 6789 003", "vat")]
    [InlineData("3001-2345-6789-003", "vat")]
    [InlineData("\u0663\u0660\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669\u0660\u0660\u0663", "vat")]
    [InlineData("4030 123456", "cr_or_national_id")]
    [InlineData("0512 345678", "phone")]
    [InlineData("050-123-4567", "phone")]
    [InlineData("+966 512345678", "phone")]
    [InlineData("+966 51 234 5678", "phone")]
    public void شكلٌ_مقطوعٌ_عبر_الكلمات_يُلمّ_ثم_يُرفض(string disguised, string shapeKey) =>
        Refuses("المورد " + disguised + " هنا", shapeKey);

    [Fact]
    public void القيمة_المقنَّعة_لا_تعبر_أيضاً_والقناع_هو_قناع_الموارد_البشرية_نفسه()
    {
        // ‏«القناع مع اسمٍ في الجملة نفسها» هو إعادة تعرّف: أربع خانات تكفي حين يكون
        // الاسم مذكوراً. والقناع موضعُه ورقة السؤال التي يرسمها الخادم ولا يراها النموذج.
        string masked = VoiceDisclosure.Mask(BoundaryFixtures.NationalId);

        Assert.Equal(VoiceDisclosure.MaskPrefix + "7465", masked);
        Refuses("الموظف أحمد الغامدي، الهوية " + masked, "masked_value");
    }

    // ── الشواهد السلبية: ما يجب أن يعبر ─────────────────────────────────────

    [Theory]
    // كلامٌ عاديّ باسم شركةٍ لا وجود لها في القاعدة — وهو المثال الذي بُني عليه القرار
    [InlineData("سجّل فاتورة مبيعات لشركة المسار الامثل بمبلغ 1500 ريال")]
    [InlineData("من هو محمد القحطاني بالضبط؟")]
    // مبلغٌ بفواصله وكسره: لمّ النقطة والفاصلة كان سيصنع «هوية» من مبلغ
    [InlineData("المبلغ 12,345,678.90 ريال")]
    [InlineData("الإجمالي 1,234,567.89 والضريبة 185,185.18")]
    // تواريخ بصيغها الثلاث
    [InlineData("بتاريخ 2026-09-01")]
    [InlineData("بتاريخ 01/09/2026")]
    [InlineData("من 2024 إلى 2026")]
    // أرقام مستندات — والشرطة لا تُلمّ للأشكال المجرّدة، ولهذا يعبر هذا السطر
    [InlineData("رقم الفاتورة INV-2026-000412")]
    [InlineData("أمر الشراء PO-2026-0001 والاستلام GR-2026-0007")]
    [InlineData("القيد 2026-000412 في دفتر الأستاذ")]
    // كمّيات وأسعار متجاورة
    [InlineData("الكمية 12 كرتون والسعر 3400 ريال")]
    [InlineData("خصم 15% على 2500")]
    // قيمٌ مقنَّعة؟ لا — نصٌّ بلا أرقام أصلاً
    [InlineData("افتح شاشة الموردين")]
    [InlineData("")]
    public void ما_يجب_أن_يعبر_يعبر(string text) => Passes(text);

    [Fact]
    public void النص_الخارج_هو_الأصل_حرفاً_بحرف_ولا_شيء_يُنقَّح()
    {
        // الطيّ كان للفحص وحده. ما يصل النموذج هو ما كتبه صاحبه: بأسمائه، وبأرقامه
        // العربية-الهندية، وبتشكيله. ولا وجود لحالة «مُنقَّح» أصلاً في نوع الحكم.
        const string spoken = "سجّل فاتورة لشركة المسار الأمثل بمبلغ ١٥٠٠ ريال";

        Result<AgentOutboundEnvelope> envelope =
            AgentOutboundBoundary.Seal(AgentOutboundPartKind.UserTurn, spoken);

        Assert.True(envelope.IsSuccess);
        Assert.Equal(spoken, Assert.Single(envelope.Value.Parts).Text);
        Assert.Equal(2, Enum.GetValues<AgentScrubOutcome>().Length);
    }

    [Fact]
    public void الأخطاء_تُعاد_كلّها_بترتيب_الأشكال_لا_أوّلها()
    {
        AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(BoundaryFixtures.OneOfEachShape);

        Assert.Equal(
            [.. AgentIdentifierShapes.All.Take(6).Select(static shape => shape.Code)],
            verdict.Errors.Select(static error => error.Code));
    }
}
