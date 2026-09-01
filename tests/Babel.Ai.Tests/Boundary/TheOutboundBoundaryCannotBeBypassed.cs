using System.Reflection;
using Babel.Ai.Boundary;
using Babel.Ai.Tests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>الحدّ بنيةٌ لا اصطلاح.</b>
/// <para>
/// «نادِ المِصفاة قبل الإرسال» جملةٌ تُنسى في سطرٍ واحد، ونسيانُها لا يُنتج خطأ ترجمة
/// ولا اختباراً أحمر — يُنتج تسريباً صامتاً. وهذا الملفّ يقيس أن النسيان <b>غير ممكن</b>:
/// </para>
/// <list type="number">
///   <item><see cref="AgentOutboundEnvelope"/> منشئه <b>داخلي</b>، فلا يُنشَأ من خارج التجميعة.</item>
///   <item>وله داخل التجميعة موضع إنشاء <b>واحد</b> — يقرؤه هذا الحارس من المصدر نفسه.</item>
///   <item>و<see cref="IAgentModelTransport{TReply}"/> لا يقبل نوعاً آخر: من لم يختم لا يجد ما يمرّره.</item>
///   <item>ولا حالة ثالثة في الحكم، فلا نوع يُعبَّر به عن «مُنقَّح».</item>
/// </list>
/// <para>
/// وهو الشكل نفسه الذي كتبه هذا المستودع مرّتين: <c>AccountCode</c> في القاعدة 2،
/// و<c>VoiceDispatch</c> خلف <c>VoiceConfirmationGate</c> («فمن نسي أن يسأل البوابة لا
/// يجد ما يمرّره»).
/// </para>
/// </summary>
public sealed class TheOutboundBoundaryCannotBeBypassed
{
    private const string BoundaryFile = "src/Babel.Ai/Boundary/AgentOutboundBoundary.cs";

    private static IEnumerable<string> AiSources() =>
        Directory.EnumerateFiles(RepositoryRoot.At("src/Babel.Ai"), "*.cs", SearchOption.AllDirectories);

    /// <summary>الظرف والجزء والحكم: لا منشئ عامّاً لواحد منها.</summary>
    [Fact]
    public void NoSealedTypeOnThePathToTheModelHasAPublicConstructor()
    {
        Type[] sealedTypes = [typeof(AgentOutboundEnvelope), typeof(AgentOutboundPart), typeof(AgentScrubVerdict)];

        foreach (Type type in sealedTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.NotEmpty(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        }
    }

    /// <summary>
    /// موضع إنشاء الظرف <b>واحد</b> في <c>Babel.Ai</c> كلّها، وهو الحدّ. ولو صار اثنين
    /// لصار «الحارس» رايةً تُقرأ لا باباً يُمرّ منه — وهذا الفحص يُحمِّر البناء على ذلك.
    /// </summary>
    [Fact]
    public void TheEnvelopeHasExactlyOneConstructionSiteAndItIsTheBoundary()
    {
        List<string> sites = [];

        foreach (string path in AiSources())
        {
            string relative = Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/');
            string text = File.ReadAllText(path);

            foreach (string construct in new[] { "new AgentOutboundEnvelope(", "new AgentOutboundPart(" })
            {
                int index = text.IndexOf(construct, StringComparison.Ordinal);
                while (index >= 0)
                {
                    sites.Add(relative + " ⇐ " + construct);
                    index = text.IndexOf(construct, index + 1, StringComparison.Ordinal);
                }
            }
        }

        Assert.Equal(
            [BoundaryFile + " ⇐ new AgentOutboundEnvelope(", BoundaryFile + " ⇐ new AgentOutboundPart("],
            sites.Order(StringComparer.Ordinal));
    }

    /// <summary>الناقل لا يقبل نصّاً ولا قائمة نصوص — يقبل الظرف وحده.</summary>
    [Fact]
    public void TheTransportPortAcceptsNothingButASealedEnvelope()
    {
        MethodInfo send = Assert.Single(typeof(IAgentModelTransport<>).GetMethods());
        ParameterInfo[] parameters = send.GetParameters();

        Assert.Equal(typeof(AgentOutboundEnvelope), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal(2, parameters.Length);
    }

    /// <summary>
    /// لا حالة ثالثة. ولا خاصّية نصّية على الحكم يُسلَّم منها «نصٌّ مُنقَّح» — فالتنقيح
    /// ليس مرفوضاً بقاعدةٍ مكتوبة فحسب، بل <b>لا نوع يحمله</b>.
    /// </summary>
    [Fact]
    public void ThereIsNoThirdOutcomeAndNoTypeThatCouldCarryRedactedText()
    {
        Assert.Equal([AgentScrubOutcome.Clean, AgentScrubOutcome.Refused], Enum.GetValues<AgentScrubOutcome>());

        Assert.DoesNotContain(
            typeof(AgentScrubVerdict).GetProperties(),
            property => property.PropertyType == typeof(string));

        // ‏«لا نوع يحمله» تُقاس على الأنواع لا على النصّ: التوثيق يسمّي المرفوض باسمه
        // اللاتيني عمداً كي يجده من يبحث عنه، والحارس يقرأ الأعضاء لا التعليقات.
        foreach (Type type in typeof(AgentOutboundBoundary).Assembly.GetTypes()
                     .Where(static candidate => candidate.Namespace == "Babel.Ai.Boundary"))
        {
            Assert.DoesNotContain("Redact", type.Name, StringComparison.OrdinalIgnoreCase);

            foreach (MemberInfo member in type.GetMembers())
            {
                Assert.DoesNotContain("Redact", member.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// <b>ثلاثة طرق، ورقمان، وستّ محاولات — ولا واحدة تُنتج ظرفاً.</b> دورُ المستخدم
    /// ونتيجةُ الأداة ورسالةُ النظام: ثلاثتها تُفحص، لا الأولى وحدها. وأخطرها الثانية:
    /// نتيجةُ أداةٍ تُبنى من بيانات محلّية، وهي الطريق الذي يعبر منه سجلٌّ كامل لو نُسي.
    /// </summary>
    [Theory]
    [InlineData(AgentOutboundPartKind.UserTurn)]
    [InlineData(AgentOutboundPartKind.ToolResult)]
    [InlineData(AgentOutboundPartKind.SystemMessage)]
    [InlineData(AgentOutboundPartKind.ReadbackEcho)]
    [InlineData(AgentOutboundPartKind.AssistantTurn)]
    public void AnIdentifierOnAnyRouteRefusesTheWholeCall(AgentOutboundPartKind route)
    {
        foreach ((string value, string code) in new[]
        {
            (BoundaryFixtures.Iban, "ai.agent.identifier_refused.iban"),
            (BoundaryFixtures.IbanGrouped, "ai.agent.identifier_refused.iban"),
            (BoundaryFixtures.NationalId, "ai.agent.identifier_refused.national_id"),
        })
        {
            Result<AgentOutboundEnvelope> sealing =
                AgentOutboundBoundary.Seal(route, "المورد الأول " + value + " والثاني");

            Assert.True(sealing.IsFailure, route.ToString() + " مرّ بـ" + value);
            Assert.Contains(sealing.Errors, error => error.Code == code);
        }
    }

    /// <summary>
    /// <b>الرفض يُبطل النداء كلّه لا الجزء المخالف.</b> ظرفٌ يحمل جزأين سليمين وثالثاً
    /// مخالفاً لا يُرسَل بجزأيه: النموذج لا يعلم أن ثالثاً نُزع، فيملأ الفراغ بثقة —
    /// وهي بعينها الحجّة المكتوبة في <c>VoiceRefusals.SlotMissing</c>.
    /// </summary>
    [Fact]
    public void OneRefusedPartRefusesTheWholeEnvelopeNotJustThatPart()
    {
        Result<AgentOutboundEnvelope> sealing = AgentOutboundBoundary.Seal(
        [
            new AgentOutboundDraft(AgentOutboundPartKind.SystemMessage, "المنشأة المفتوحة: شركة سلاسل بابل"),
            new AgentOutboundDraft(AgentOutboundPartKind.UserTurn, "سجّل فاتورة للمورد " + BoundaryFixtures.Vat),
            new AgentOutboundDraft(AgentOutboundPartKind.ToolResult, "{\"outcome\":\"needs_question\"}"),
        ]);

        Assert.True(sealing.IsFailure);
        Assert.Equal("ai.agent.identifier_refused.vat", Assert.Single(sealing.Errors).Code);
    }

    /// <summary>سببٌ واحد لكل شكل مهما تكرّر في أجزاء الظرف — لا سبعة عن شيء واحد.</summary>
    [Fact]
    public void RepeatingTheSameShapeAcrossPartsStillReadsAsOneSentence()
    {
        Result<AgentOutboundEnvelope> sealing = AgentOutboundBoundary.Seal(
        [
            new AgentOutboundDraft(AgentOutboundPartKind.UserTurn, BoundaryFixtures.NationalId),
            new AgentOutboundDraft(AgentOutboundPartKind.ToolResult, BoundaryFixtures.ResidencyId),
            new AgentOutboundDraft(AgentOutboundPartKind.ReadbackEcho, BoundaryFixtures.NationalId),
        ]);

        Assert.True(sealing.IsFailure);
        Assert.Equal("ai.agent.identifier_refused.national_id", Assert.Single(sealing.Errors).Code);
    }

    /// <summary>ظرفٌ فارغ لا يُختَم: نداءٌ بلا محتوى يستهلك دوراً ويعود بجوابٍ عن لا شيء.</summary>
    [Fact]
    public void AnEmptyEnvelopeIsRefusedRatherThanSent()
    {
        Result<AgentOutboundEnvelope> sealing = AgentOutboundBoundary.Seal([]);

        Assert.True(sealing.IsFailure);
        Assert.Equal("ai.agent.outbound_empty", Assert.Single(sealing.Errors).Code);
    }

    /// <summary>
    /// شاهدٌ سلبي: نداءٌ سليم <b>يُختَم فعلاً</b>. بلا هذا السطر يكون «لا شيء يعبر»
    /// ادّعاءً يمكن الوفاء به بمنع كل شيء.
    /// </summary>
    [Fact]
    public void ACleanCallIsActuallySealedAndKeepsItsWordsUnchanged()
    {
        AgentOutboundDraft[] drafts =
        [
            new(AgentOutboundPartKind.SystemMessage, "المنشأة المفتوحة: شركة سلاسل بابل"),
            new(AgentOutboundPartKind.UserTurn, "سجّل فاتورة مبيعات لشركة المسار الامثل بمبلغ 1500 ريال"),
            new(AgentOutboundPartKind.ToolResult, "{\"outcome\":\"needs_question\",\"handle\":null,\"questionId\":\"…\"}"),
        ];

        Result<AgentOutboundEnvelope> sealing = AgentOutboundBoundary.Seal(drafts);

        Assert.True(sealing.IsSuccess, string.Join(" · ", sealing.Errors.Select(static e => e.Code)));
        Assert.Equal(
            drafts.Select(static draft => draft.Text),
            sealing.Value.Parts.Select(static part => part.Text));
        Assert.Equal(
            drafts.Select(static draft => draft.Kind),
            sealing.Value.Parts.Select(static part => part.Kind));
    }
}
