using System.Reflection;
using System.Text.Json;
using Babel.Ai.Voice;

namespace Babel.Ai.Agent;

/// <summary>
/// <b>الكتالوج — يُبنى مرّةً عند الإقلاع من العقد المنشور، ولا يتغيّر بين نداءَين ولا بين مستخدمَين.</b>
/// <para>
/// <b>ولماذا لا يُرشَّح بالاستحقاق:</b> الأدوات تُرسَل في <b>الموضع صفر</b> من بادئة الذاكرة،
/// و<c>tools = build(user)</c> هو القاتل الصامت الكلاسيكي — يمنح كل مستخدمٍ فضاء ذاكرةٍ
/// خاصّاً به فلا يُقرأ شيء أبداً. فالكتالوج واحدٌ للجميع بايتاً ببايت، والاستحقاق يُفحص في
/// <see cref="AgentToolGate"/> بعد أن ينطق النموذج وقبل أن يُنفَّذ شيء. وهذا هو الترتيب
/// الصحيح ذاكرةً <b>وأمناً</b> معاً: النموذج لا يُؤتمَن على التصفية أصلاً.
/// </para>
/// <para>
/// <b>وطبقةٌ ثانية عند التركيب:</b> الكتالوج يُرشَّح بـ<see cref="VoiceOperationGuard.Permits"/>،
/// <b>ثم يُرفض التركيب كلّه</b> إن بقي فيه ما ترفضه الخطوة الثالثة من البوابة. فلا يكفي أن
/// تحرس البوابة وقت التشغيل: عمليةٌ ممنوعة داخل كتالوجٍ صالح تعني أن النموذج <b>رآها</b>،
/// وأنّ الحاجز الأخير صار سطراً واحداً في دالّة. وهي سابقة
/// <c>AiModuleRegistration</c> نفسها: «سجلّ النيّات المنطوقة معتلّ فلا يُركَّب».
/// </para>
/// </summary>
public sealed class AgentToolCatalogue
{
    /// <summary>الاسم المنطقي للمورد المضمَّن.</summary>
    public const string ResourceName = "Babel.Ai.Agent.tool-catalogue.json";

    /// <summary>
    /// المقاطع التي لا تُعكَس — <b>مُعادةٌ حرفياً</b> من
    /// <c>NoVoiceIntentReachesAPostingOperation</c>، ومطابقتها مفروضة باختبار معماري.
    /// </summary>
    public static IReadOnlyList<string> IrreversibleSegments { get; } =
    [
        "/posting", "/activation", "/approval", "/termination", "/revocation", "/reversal", "/lapse",
    ];

    /// <summary>
    /// <b>هل يحمل هذا المسار مقطعاً لا يُعكَس؟</b> — <b>بالمقاطع لا بالنهاية</b>.
    /// <para>
    /// كان الفحص <c>EndsWith</c>، فكان مسارٌ مثل <c>…/sales-invoices/posting/confirm</c>
    /// يمرّ من المولّد ومن التركيب ومن البوّابة معاً. والعقد اليوم <b>لا يحمل</b> مقطعاً
    /// غير طرفيّ من هذه الستّة — مقيس — فالتغيير <b>لا يُغيّر كتالوج اليوم</b>؛ لكنه
    /// يُعيد للجملة المكتوبة معناها: «يُقرأ من المسار، فعمليةٌ تُسمّى غداً بأي اسم
    /// ومسارُها يمرّ بـ‎posting تبقى ممنوعة».
    /// </para>
    /// </summary>
    /// <param name="path">المسار المنشور.</param>
    public static string? IrreversibleSegmentIn(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string irreversible in IrreversibleSegments)
            {
                if (string.Equals(segment, irreversible[1..], StringComparison.Ordinal))
                {
                    return irreversible;
                }
            }
        }

        return null;
    }

    private readonly Dictionary<string, AgentTool> _byName;

    private AgentToolCatalogue(
        string contractSha256,
        IReadOnlyList<string> registerKeys,
        IReadOnlyList<AgentTool> tools)
    {
        ContractSha256 = contractSha256;
        RegisterKeys = registerKeys;
        Tools = tools;
        _byName = tools.ToDictionary(static tool => tool.Name, StringComparer.Ordinal);
    }

    /// <summary>الكتالوج المضمَّن — يُقرأ ويُتحقَّق منه مرّةً واحدة لعمر العملية.</summary>
    public static AgentToolCatalogue Embedded { get; } = Load();

    /// <summary>بصمة العقد الذي وُلِّد منه الكتالوج. يقارنها حارسٌ بالعقد على القرص.</summary>
    public string ContractSha256 { get; }

    /// <summary>مفاتيح السجلّات المعروضة على النموذج — مفردةٌ مغلقة لا تُشتقّ من التسجيل.</summary>
    public IReadOnlyList<string> RegisterKeys { get; }

    /// <summary>الأدوات، مرتّبةً ترتيباً <b>ثابتاً</b> (‏<see cref="StringComparer.Ordinal"/>).</summary>
    public IReadOnlyList<AgentTool> Tools { get; }

    /// <summary>
    /// <b>هل يستطيع هذا المتكلّم أن يستهلك مِقبضاً أصلاً؟</b> — أي هل يبلغ استحقاقُه
    /// عمليةً واحدة تعلن حقلاً شكلُه معرّف.
    /// <para>
    /// وهو شرط أدوات البروتوكول: المِقبض سلطةُ <b>تسمية</b> لا سلطةُ فعل، ولا معنى
    /// لسكّه لمن لا يملك ما يسكبه فيه. وسكُّه مع ذلك ليس فعلاً بلا أثر: جوابُه «نعم/لا»
    /// على وجود اسمٍ في سجلّ المنشأة، وهو التسريب الذي بُني هذا المسار كلّه لمنعه.
    /// </para>
    /// <para>
    /// <b>وما يبقى مُعلَناً لا مُغطّى:</b> هذا يمنع <b>من لا يستهلك شيئاً</b> ولا يقصر
    /// كلَّ متكلّمٍ على السجلّات التي تخصّ وحداته. الأدقّ يحتاج خريطة «سجلّ ⇐ عملية»،
    /// والعقد المنشور لا يحملها اليوم: من خمسة وعشرين اسمَ حقلٍ معرّف في الكتالوج
    /// ستّةٌ فقط تقابل سجلّاً. وهو مذكورٌ في «ما ينقض هذا القرار».
    /// </para>
    /// </summary>
    /// <param name="permittedOperationIds">العمليات المسموح بها لهذا المتكلّم.</param>
    public bool EntitlesAnyHandleConsumer(IReadOnlySet<string> permittedOperationIds)
    {
        ArgumentNullException.ThrowIfNull(permittedOperationIds);

        return Tools.Any(tool =>
            tool.OperationId is not null
            && tool.IdFields.Count > 0
            && permittedOperationIds.Contains(tool.OperationId));
    }

    /// <summary>يحلّ اسماً إلى أداة، أو <c>null</c> — والكتالوج مغلق فلا مقاربة بأقرب شبيه.</summary>
    /// <param name="name">الاسم كما ورد من النموذج.</param>
    public AgentTool? Resolve(string? name) =>
        name is not null && _byName.TryGetValue(name, out AgentTool? tool) ? tool : null;

    /// <summary>
    /// يقرأ الكتالوج المضمَّن ويتحقّق منه. <b>ويرمي عند أي اعتلال</b> — والرمي عند
    /// الإقلاع أرخص من كتالوجٍ نصفُه صالح يعمل تسعاً وتسعين مرّة ثم يعرض على النموذج باباً
    /// لا يُعكَس في المرّة المئة.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن كان المورد غائباً أو الكتالوج معتلّاً.</exception>
    public static AgentToolCatalogue Load()
    {
        Assembly assembly = typeof(AgentToolCatalogue).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "كتالوج أدوات الوكيل غير مضمَّن في التجميعة (" + ResourceName + ") — فلا تُركَّب حلقة بلا كتالوج.");

        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        string sha = root.GetProperty("contractSha256").GetString()
            ?? throw new InvalidOperationException("كتالوج الأدوات بلا بصمة عقد.");

        List<string> registerKeys = [.. root.GetProperty("registerKeys")
            .EnumerateArray().Select(static value => value.GetString()!)];

        List<AgentTool> tools = [];
        List<string> refused = [];

        foreach (JsonElement entry in root.GetProperty("tools").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString()!;
            string? operationId = entry.GetProperty("operationId").GetString();
            string? path = entry.GetProperty("path").GetString();
            string? method = entry.GetProperty("method").GetString();
            string description = entry.GetProperty("description").GetString() ?? string.Empty;

            List<string> idFields = [.. entry.GetProperty("idFields")
                .EnumerateArray().Select(static value => value.GetString()!)];

            // ‏**النصّ كما وُلِّد** — GetRawText يُعيد بايتات المصدر لا إعادة تسلسل.
            string schema = entry.GetProperty("inputSchema").GetRawText();

            if (operationId is null)
            {
                if (!AgentProtocolTools.Contains(name))
                {
                    refused.Add(name + " — أداةٌ بلا عملية وليست من أدوات البروتوكول");
                    continue;
                }
            }
            else
            {
                // الطبقة الثانية: الحارس المنطوق نفسه، ثم مقاطع المسار التي لا تُعكَس.
                string? why = VoiceOperationGuard.Refuse(operationId);
                if (why is not null)
                {
                    refused.Add(operationId + " — " + why);
                    continue;
                }

                if (path is null)
                {
                    refused.Add(operationId + " — بلا مسارٍ منشور");
                    continue;
                }

                string? irreversible = IrreversibleSegmentIn(path);

                if (irreversible is not null)
                {
                    refused.Add(operationId + " — مسارُه يمرّ بـ" + irreversible);
                    continue;
                }
            }

            tools.Add(new AgentTool(name, operationId, path, method, description, idFields, schema));
        }

        if (refused.Count > 0)
        {
            throw new InvalidOperationException(
                "كتالوج أدوات الوكيل معتلّ فلا يُركَّب: " + string.Join(" · ", refused));
        }

        if (tools.Count == 0)
        {
            throw new InvalidOperationException("كتالوج أدوات الوكيل فارغ — وحارسٌ على لا شيء يمرّ على كل شيء.");
        }

        foreach (string required in new[] { AgentProtocolTools.LookupEntity, AgentProtocolTools.AskQuestion })
        {
            if (!tools.Exists(tool => string.Equals(tool.Name, required, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("كتالوج أدوات الوكيل بلا أداة «" + required + "».");
            }
        }

        // ترتيبٌ ثابت لا ترتيب الملفّ: البايتات المُرسَلة هي ما يُذاكَر.
        tools.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        return new AgentToolCatalogue(sha, registerKeys, tools);
    }
}
