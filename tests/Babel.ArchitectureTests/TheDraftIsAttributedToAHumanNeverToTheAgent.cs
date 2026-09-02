using System.Globalization;
using System.Reflection;
using Babel.Ai.Agent;
using Babel.ArchitectureTests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>المسوّدة التي يقترحها الوكيل تُنسب إلى إنسان — مفروضاً بالبناء، لا مكتوباً في قرار.</b>
/// <para>
/// و«الوكيل يعمل داخل جلسةٍ لإنسانٍ معروف» جملةٌ صحيحة تسقط بثلاث طرق، وكلٌّ منها
/// سطرٌ واحد يكتبه من لا يعرف هذه الجملة:
/// <list type="number">
///   <item><b>ألّا يعبر الإنسان أصلاً</b>: <c>AgentCaller</c> بلا مستخدم، فيضطرّ من
///         يكتب المنفّذ إلى أن يخترع فاعلاً.</item>
///   <item><b>أن يُنسب إلى فاعل النظام</b>: <c>UserId.SystemActor</c> سطرٌ جاهزٌ
///         يُصرِّف، ويجعل كلّ ما ينشئه الوكيل بلا صاحبٍ في سجلّ التدقيق.</item>
///   <item><b>أن يُبنى اعتمادٌ في سطح الوكيل</b>: <c>new ApiPrincipal(tenant, user, {company})</c>
///         سطرٌ يُصرِّف كذلك، <b>ويُسقط «دورك في هذه المنشأة قراءةٌ فقط»</b> — فيصير
///         مسار الوكيل أوسع من الباب الذي يفتحه المتصفّح للإنسان نفسه. وهو تصعيدُ
///         صلاحية لا تفصيلَ تنفيذ.</item>
/// </list>
/// </para>
/// <para>
/// <b>والقياس على اللغة الوسيطة لا على المصدر</b> في الاثنين الأخيرين: نصٌّ يُقرأ
/// يُتحايَل عليه باسمٍ مستعار (<c>using Actor = Babel.SharedKernel.UserId;</c>) أو
/// بدالّةٍ وسيطة في ملفٍّ آخر، واللغة الوسيطة لا يُتحايَل عليها بشيءٍ من ذلك.
/// </para>
/// </summary>
public sealed class TheDraftIsAttributedToAHumanNeverToTheAgent
{
    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>نوعُ الهوية في السطح — داخليٌّ، فيُقرأ بالانعكاس لا بالإشارة.</summary>
    private static Type ApiPrincipal { get; } =
        BabelAssemblies.Named("Babel.Api").GetType("Babel.Api.Security.ApiPrincipal")
        ?? throw new InvalidOperationException("لم يُوجد Babel.Api.Security.ApiPrincipal.");

    /// <summary>
    /// <b>الإنسان يعبر في المتكلّم نفسه</b> — فلا يحتاج المنفّذ إلى أن يخترع فاعلاً،
    /// ولا إلى أن يسأل عنه مخزناً ثانياً قد يُجيب بغيره.
    /// </summary>
    [Fact]
    public void المتكلّمُ_يحمل_إنسانه()
    {
        PropertyInfo? human = typeof(AgentCaller).GetProperty("User", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(human);
        Assert.Equal(typeof(UserId), human!.PropertyType);

        // ‏**وهو يبلغ المنفّذ**: `AgentDispatch.Caller` هو ما يصل إلى من يُنشئ المسوّدة.
        PropertyInfo? caller = typeof(AgentDispatch).GetProperty("Caller", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(caller);
        Assert.Equal(typeof(AgentCaller), caller!.PropertyType);
    }

    /// <summary>
    /// <b>ولا فاعلَ نظامٍ في مسار الوكيل كلّه.</b> ما ينشئه هذا المسار يحمل اسم إنسان،
    /// وإلّا فليس له صاحبٌ يُسأل عنه.
    /// </summary>
    [Fact]
    public void لا_فاعلَ_نظامٍ_في_تجميعة_الوكيل()
    {
        static bool SystemActor(MethodBase method) =>
            string.Equals(method.Name, "get_SystemActor", StringComparison.Ordinal)
            && method.DeclaringType == typeof(UserId);

        IReadOnlyList<string> inTheAgent = CallScan.CallersIn(BabelAssemblies.Named("Babel.Ai"), SystemActor);

        Assert.True(
            inTheAgent.Count == 0,
            "أنواعٌ في Babel.Ai تنسب عملاً إلى فاعل النظام: " + string.Join(" · ", inTheAgent));

        // ── الشاهد الموجب: الماسح يجد الاستعمال حيث هو مشروع فعلاً ────────────
        // ولولا هذا الشاهد لكان «صفر» يعني «الماسح لا يمسك شيئاً» بالقدر نفسه الذي
        // يعني «لا أحد يستعمله» — وهو الفخّ الذي أوقع هذا المستودع مراراً.
        List<string> elsewhere =
        [
            .. BabelAssemblies.Product
                .Where(static assembly => assembly.GetName().Name != "Babel.Ai")
                .SelectMany(assembly => CallScan.CallersIn(assembly, SystemActor)),
        ];

        Assert.True(
            elsewhere.Count > 0,
            "الماسح لم يجد فاعل النظام في أي تجميعة — فصفرُه في Babel.Ai لا يعني شيئاً.");
    }

    /// <summary>
    /// <b>ولا يُبنى اعتمادٌ في سطح الوكيل.</b> الهوية تُحلّ من اعتماد الإنسان في وسيط
    /// المصادقة، وتُحفظ كما هي، وتُثبَّت كما هي — ولا تُركَّب من معرّفين.
    /// </summary>
    [Fact]
    public void لا_تُبنى_هويةٌ_في_سطح_الوكيل()
    {
        static bool Constructs(MethodBase method) =>
            method is ConstructorInfo && method.DeclaringType == ApiPrincipal;

        IReadOnlyList<string> builders = CallScan.CallersIn(BabelAssemblies.Named("Babel.Api"), Constructs);

        // الشاهد الموجب أولاً: من يبني الهوية فعلاً موجودٌ ومقروء.
        Assert.True(
            builders.Count > 0,
            "الماسح لم يجد من يبني الهوية في Babel.Api — فنفيُه عن سطح الوكيل لا يعني شيئاً.");

        List<string> inTheAgentSurface =
        [
            .. builders.Where(static name =>
                name.StartsWith("Babel.Api.Agent.", StringComparison.Ordinal)
                || string.Equals(name, "Babel.Api.Endpoints.AgentEndpoints", StringComparison.Ordinal)),
        ];

        Assert.True(
            inTheAgentSurface.Count == 0,
            "سطحُ الوكيل يبني هويةً بدل أن يقرأ المحلولة: " + string.Join(" · ", inTheAgentSurface)
            + " (البناة المقروءون: " + Count(builders.Count) + ")");
    }
}
