using System.Reflection;
using Babel.Ai.Agent;
using Babel.Ai.Boundary;
using Babel.Ai.Tests.Support;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>البوّابة بنيةٌ لا اصطلاح — ومن نسيها لا يجد ما يمرّره.</b>
/// <para>
/// «نادِ البوّابة قبل التنفيذ» جملةٌ تُنسى في سطرٍ واحد، ونسيانُها لا يُنتج خطأ ترجمة.
/// وهذا الملفّ يقيس أن النسيان <b>غير ممكن</b> على ثلاث حلقاتٍ متسلسلة:
/// </para>
/// <list type="number">
///   <item><see cref="AgentDispatch"/> منشئه داخلي، وله موضع إنشاءٍ <b>واحد</b> هو
///         <c>AgentToolGate</c> — يقرؤه هذا الحارس من المصدر.</item>
///   <item><see cref="IAgentDraftSubmitter"/> لا يقبل نوعاً آخر.</item>
///   <item><see cref="AgentModelRequest"/> منشئه داخلي، ومعامله الأول
///         <see cref="AgentOutboundEnvelope"/> — والظرف لا يُنشَأ إلا خلف المِصفاة
///         (يفرضه حارسٌ قائم). <b>فالخاصّية تنتقل بالتركيب</b>: لا مِصفاة ⇒ لا ظرف ⇒
///         لا طلب ⇒ لا نموذج.</item>
/// </list>
/// <para>
/// وهو الشكل الذي كتبه هذا المستودع ثلاث مرّات قبل اليوم: <c>AccountCode</c> في
/// القاعدة 2، و<c>VoiceDispatch</c> خلف <c>VoiceConfirmationGate</c>، و<c>AgentOutboundEnvelope</c>
/// خلف الحدّ.
/// </para>
/// </summary>
public sealed class TheGateIsTheOnlyDoorToDispatch
{
    private const string GateFile = "src/Babel.Ai/Agent/AgentToolGate.cs";
    private const string TranscriptFile = "src/Babel.Ai/Agent/AgentTranscript.cs";

    private static IEnumerable<string> AgentSources() =>
        Directory.EnumerateFiles(RepositoryRoot.At("src/Babel.Ai"), "*.cs", SearchOption.AllDirectories);

    /// <summary>لا منشئ عامّاً لأيّ نوعٍ على الطريق.</summary>
    [Fact]
    public void NoTypeOnThePathToExecutionHasAPublicConstructor()
    {
        Type[] sealedTypes = [typeof(AgentDispatch), typeof(AgentModelRequest), typeof(AgentWireBlock), typeof(AgentTool)];

        foreach (Type type in sealedTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.NotEmpty(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        }
    }

    /// <summary>
    /// موضع إنشاء الأمر <b>واحد</b> في <c>Babel.Ai</c> كلّها، وهو البوّابة. ولو صار
    /// اثنين لصار «الحارس» رايةً تُقرأ لا باباً يُمرّ منه.
    /// </summary>
    [Fact]
    public void TheDispatchHasExactlyOneConstructionSiteAndItIsTheGate()
    {
        Assert.Equal(
            [GateFile, GateFile, GateFile],
            Sites("new AgentDispatch("));
    }

    /// <summary>وموضع إنشاء الطلب واحد، وهو خاتم النسخة — والظرف شرطُه.</summary>
    [Fact]
    public void TheModelRequestHasExactlyOneConstructionSiteAndItSealsFirst()
    {
        Assert.Equal([TranscriptFile], Sites("new AgentModelRequest("));

        // ‏والمنشئ المنسوخ الذي يولّده السجلّ (‏record) يُستبعَد: معامله الوحيد هو النوع نفسه.
        ConstructorInfo constructor = Assert.Single(
            typeof(AgentModelRequest).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance),
            candidate => candidate.GetParameters().Length > 1);

        Assert.Equal(typeof(AgentOutboundEnvelope), constructor.GetParameters()[0].ParameterType);
    }

    /// <summary>المنفّذ لا يقبل جسماً ولا اسم عملية — يقبل الأمر وحده.</summary>
    [Fact]
    public void TheDraftSubmitterAcceptsNothingButAnAuthorisedDispatch()
    {
        MethodInfo submit = Assert.Single(typeof(IAgentDraftSubmitter).GetMethods());
        ParameterInfo[] parameters = submit.GetParameters();

        Assert.Equal(typeof(AgentDispatch), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal(2, parameters.Length);
    }

    /// <summary>والباب إلى المزوّد لا يقبل نصّاً ولا رسائل — يقبل الطلب وحده.</summary>
    [Fact]
    public void TheModelGatewayAcceptsNothingButASealedRequest()
    {
        MethodInfo stream = Assert.Single(typeof(IAgentModelGateway).GetMethods());
        ParameterInfo[] parameters = stream.GetParameters();

        Assert.Equal(typeof(AgentModelRequest), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal(2, parameters.Length);
    }

    /// <summary>
    /// <b>حزمةُ المزوّد يعرفها ملفٌّ واحد.</b> ولو تسرّبت إلى الحلقة أو البوّابة لصار كل
    /// اختبارٍ محتاجاً إليها — ومجموعةُ اختباراتٍ تنفق على كل تشغيل تُطفأ خلال شهر.
    /// </summary>
    [Fact]
    public void OnlyOneFileInTheModuleKnowsTheProviderPackage()
    {
        List<string> knowing = [];

        foreach (string path in AgentSources())
        {
            string text = File.ReadAllText(path);
            if (text.Contains("using Anthropic;", StringComparison.Ordinal)
                || text.Contains("using Anthropic.Models", StringComparison.Ordinal))
            {
                knowing.Add(Relative(path));
            }
        }

        Assert.Equal(["src/Babel.Ai/Agent/Anthropic/AnthropicAgentGateway.cs"], knowing.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>ولا مفتاح في أي ملفّ.</b> المفتاح يُقرأ من البيئة باسمٍ، والاسم وحده يُكتب —
    /// نفس قاعدة <c>GitHubModelsOptions.TokenVariable</c>، ولنفس السبب: سرٌّ في إعدادٍ
    /// يظهر في سجل وفي أثر وفي رسالة استثناء وفي لقطة ذاكرة.
    /// </summary>
    [Fact]
    public void TheKeyItselfAppearsInNoSourceFileOnlyItsVariableName()
    {
        foreach (string path in AgentSources())
        {
            string text = File.ReadAllText(path);

            Assert.DoesNotContain("sk-ant-", text, StringComparison.OrdinalIgnoreCase);

            // ‏قراءةُ المتغيّر بالاسم مباح؛ وكتابةُ المفتاح في حقلٍ ليست كذلك.
            Assert.DoesNotContain("ApiKey = \"", text, StringComparison.Ordinal);
        }

        // ‏وحقلُ الإعدادات يحمل **الاسم**: قيمتُه الافتراضية اسم متغيّرٍ لا سرّ.
        Assert.Equal("ANTHROPIC_API_KEY", new AgentOptions().ApiKeyVariable);
        Assert.Equal(AgentOptions.DefaultApiKeyVariable, new AgentOptions().ApiKeyVariable);
    }

    private static List<string> Sites(string construct)
    {
        List<string> sites = [];

        foreach (string path in AgentSources())
        {
            string text = File.ReadAllText(path);
            int index = text.IndexOf(construct, StringComparison.Ordinal);

            while (index >= 0)
            {
                sites.Add(Relative(path));
                index = text.IndexOf(construct, index + 1, StringComparison.Ordinal);
            }
        }

        return [.. sites.Order(StringComparer.Ordinal)];
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot.Path, path).Replace('\\', '/');
}
