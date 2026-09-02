using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.Ai.Tests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>قياسٌ لا رأي: أين يلتقي مِقبضٌ موقَّع بحدّ المعرّفات، وأين يلتقي مخطّطٌ منشور به.</b>
/// <para>
/// <b>الواقعة:</b> حدّ المعرّفات معايَرٌ على <b>نثرٍ يكتبه إنسان</b> — «سلسلةٌ من تسع
/// خاناتٍ فأكثر تشبه معرّفاً». وهذا صحيحٌ في جملةٍ عربية، و<b>خاطئٌ في نصٍّ آليّ</b>:
/// سقفُ حقلٍ في مخطّطٍ منشور (<c>2147483647</c> — أي <c>Int32.MaxValue</c>) عشرُ خانات،
/// ومِقبضُ <c>base64url</c> متّصلٌ قد يحمل سلسلةً كهذه بالصدفة.
/// </para>
/// <para>
/// <b>وما يترتّب على القياس — وهو قرارُ هذا الملفّ:</b>
/// </para>
/// <list type="number">
///   <item><b>البادئة المُجمَّدة (الأدوات ونصّ النظام) لا تمرّ بالمِصفاة</b>، بل تُثبَّت
///         ببصمة العقد. وتمريرُها كان سيُسقط التركيب على <c>Int32.MaxValue</c> في مخطّط —
///         وهو رفضٌ كاذب صريح.</item>
///   <item><b>مواضع المقابض مُستثناة من فحص الشكل داخل البوّابة</b>، وبفحصٍ <b>أقوى</b>
///         لا أضعف: علامةُ تعميةٍ مُوثَّقة لهذه الجلسة بعينها.</item>
/// </list>
/// <para>
/// <b>وما كان مفتوحاً أُغلق — وبالبناء لا بالاحتمال.</b> المِقبض يعبر الحدّ مرّةً في
/// نتيجة أداةٍ عند إصداره، وأخرى في نداء الأداة حين يعيده النموذج؛ فمِقبضٌ يحمل سلسلةَ
/// تسع خاناتٍ بالصدفة كان يُرفض عند الحدّ لا عند العلامة، <b>مرّةً في نحو مئتَي ألف</b>
/// — وهو العطل النادر العشوائيّ الذي لا يُعاد إنتاجه فلا يُصلَح. والعلاج نزل حيث يجب:
/// في <b>المُصدِر</b>. يُكتب المِقبض مجموعاتٍ من ثمانية يفصلها <c>~</c>، و<c>~</c> ليست
/// فاصلاً عند أي درجة لمّ — فلا يتجاوز أي مسارٍ رقميّ فيه ثماني خانات، ولا يطابقه شكلٌ
/// من السبعة بحال. والقياس أدناه صار يطلب <b>صفراً</b> لا «ندرة».
/// </para>
/// </summary>
public sealed class TheBoundaryIsCalibratedForProseNotForTokens
{
    private static readonly TenantId Tenant = new(new Guid("b0c00000-0000-4000-8000-000000000001"));
    private static readonly Guid Company = new("b0c00000-0000-4000-8000-0000000000c1");
    private static readonly Guid Session = new("b0c00000-0000-4000-8000-0000000000f1");

    private static readonly Regex LongDigitRun = new(@"(?<![0-9])[0-9]{9,}(?![0-9])", RegexOptions.CultureInvariant);

    /// <summary>
    /// <b>الواقعة الأولى:</b> ‏<c>Int32.MaxValue</c> — وهو سقفُ حقلٍ حقيقيّ في مخطّطين
    /// منشورين — يرفضه الحدّ. فالبادئة المُجمَّدة لا تُمرَّر به.
    /// </summary>
    [Fact]
    public void سقفُ_عددٍ_صحيحٍ_في_مخطّطٍ_منشور_يرفضه_الحدّ()
    {
        Assert.True(Babel.Ai.Boundary.AgentOutboundScrubber.Inspect("2147483647").IsRefused);

        string[] carrying = [.. AgentToolCatalogue.Embedded.Tools
            .Where(static tool => LongDigitRun.IsMatch(tool.InputSchemaJson))
            .Select(static tool => tool.Name)
            .Order(StringComparer.Ordinal)];

        // ‏قياسٌ لا ادّعاء: أداتان بالضبط تحملان سقفاً كهذا اليوم.
        Assert.Equal(["draftClientCertificate", "draftSubcontractorCertificate"], carrying);
    }

    /// <summary>
    /// <b>والواقعة الثانية:</b> عددُ المقابض التي تحمل سلسلةً من تسع خاناتٍ فأكثر —
    /// مقيساً على عشرين ألف سكّة، لا مقدَّراً. <b>وكان يُطلَب «نادراً» فصار يُطلَب
    /// صفراً</b>، لأن شكل المِقبض نفسه يجعل السلسلة غيرَ ممكنة. والعدد يُطبع في رسالة
    /// التوكيد كي يُقرأ حين يتغيّر تعريفُ المِقبض.
    /// </summary>
    [Fact]
    public void لا_مِقبضَ_واحدٌ_يصطدم_بالحدّ_والاستحالةُ_بنيوية()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);

        const int minted = 20_000;
        int colliding = 0;

        for (int index = 0; index < minted; index++)
        {
            string token = handles
                .Issue(LookupHandlePurpose.Entity, Tenant, Company, Session, Guid.NewGuid(), TimeSpan.FromMinutes(10))
                .Value;

            if (LongDigitRun.IsMatch(token))
            {
                colliding++;
            }
        }

        // ‏**والاصطدام صار مستحيلاً لا نادراً**: المجموعات الثمانية تحدّ أطول مسارٍ رقميّ
        // بثماني خانات، وهو دون الشامل (تسع) ودون الهوية (عشر) ودون الضريبي (خمس عشرة).
        // والنادر العشوائيّ أسوأ من المطّرد — لا يُعاد إنتاجه فلا يُصلَح — فأُزيل أصلُه.
        Assert.Equal(0, colliding);
    }

    /// <summary>
    /// جسمُ حركة مخزونٍ <b>على شكله المنشور</b> — وما عداه يسقط عند مطابقة المخطّط
    /// قبل أن يبلغ فكَّ المِقبض، وهو الصواب.
    /// </summary>
    /// <param name="itemId">موضع المِقبض المفحوص.</param>
    /// <param name="number">رقم الحركة — حقلٌ ليس موضع مِقبض.</param>
    /// <param name="sound">مِقبضٌ سليم لموضعَي المخزن والموقع، كي يبقى المقيس واحداً.</param>
    private static string StockMovement(string itemId, string number, string sound) =>
        JsonSerializer.Serialize(new
        {
            cost = "10",
            direction = "IN",
            itemGroup = "GOODS",
            itemId,
            locationId = sound,
            number,
            occurredOn = "2026-03-01",
            quantity = new { magnitude = "1", unit = "EA" },
            warehouseId = sound,
        });

    /// <summary>مِقبضٌ سليم لهذه الجلسة.</summary>
    private static string SoundHandle(SignedLookupHandles handles) => handles
        .Issue(
            LookupHandlePurpose.Entity, Tenant, Company, Session,
            new Guid("a9e70000-0000-4000-8000-0000000000e1"), TimeSpan.FromMinutes(10))
        .Value;

    /// <summary>
    /// <b>وبناءً على القياس: موضع المِقبض مُستثنى من فحص الشكل داخل البوّابة</b> —
    /// وبفحصٍ أقوى لا أضعف. وهذا الإثبات يقيس الاستثناء على قيمةٍ <b>يستحيل</b> أن
    /// تُسَكّ صدفةً: مِقبضٌ يُصنع ثم تُبدَّل خاناتُه.
    /// </summary>
    [Fact]
    public void موضعُ_المِقبض_يُفحص_بالتوقيع_لا_بالشكل()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);

        // قيمةٌ تحمل شكل «سلسلة رقمية طويلة» في موضع مِقبض: تسقط عند **التوقيع**،
        // ‏ورمزُ الرفض يقول ذلك — لا رمز الشكل.
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", "draftStockMovement", StockMovement(
                itemId: "123456789012345678901234567890",
                number: "MV-1",
                sound: SoundHandle(handles))),
            AgentHarness.Caller(Tenant, Company, Session, "draftStockMovement"),
            new AgentTurnState(4),
            AgentToolCatalogue.Embedded,
            handles);

        Assert.True(gated.IsFailure);
        Assert.Equal("ai.lookup.handle_not_signed", Assert.Single(gated.Errors).Code);
    }

    /// <summary>
    /// وشاهدٌ سلبي يمنع توسيع الاستثناء: حقلٌ <b>ليس</b> موضع مِقبضٍ يبقى تحت فحص الشكل.
    /// </summary>
    [Fact]
    public void حقلٌ_ليس_موضع_مِقبضٍ_يبقى_تحت_فحص_الشكل()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);

        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", "draftStockMovement", StockMovement(
                itemId: SoundHandle(handles),
                number: "123456789012345678901234567890",
                sound: SoundHandle(handles))),
            AgentHarness.Caller(Tenant, Company, Session, "draftStockMovement"),
            new AgentTurnState(4),
            AgentToolCatalogue.Embedded,
            handles);

        Assert.True(gated.IsFailure);
        Assert.Contains(gated.Errors, static error => error.Code.StartsWith(
            "ai.agent.identifier_refused.", StringComparison.Ordinal));
    }

    /// <summary>
    /// وبصمةُ الكتالوج تُثبَّت هنا أيضاً: <b>البادئة تُحرَس بالبصمة لا بالمِصفاة</b>،
    /// فلا يُقرأ هذا الاستثناء إذناً عامّاً.
    /// </summary>
    [Fact]
    public void البادئةُ_تُحرَس_بالبصمة_لا_بالمِصفاة()
    {
        string onDisk = Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(RepositoryRoot.At("contracts/openapi/v1.json"))));

        Assert.Equal(onDisk, AgentToolCatalogue.Embedded.ContractSha256);
    }
}
