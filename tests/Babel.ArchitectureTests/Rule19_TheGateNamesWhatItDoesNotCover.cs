using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 19 — البوّابة المحلية تُسمّي ما لا تُغطّيه.</b>
/// <para>
/// <b>ثالثةُ القاعدتين 15 و16، ونفس العطل صعوداً درجة.</b> تلك تضمن أن البوّابة
/// <b>تبني</b> ما تدّعي تغطيته، وأن المسابر تُبنى وإن كانت خارج ملف الحلّ. وهذه تضمن
/// أن ما <b>لا</b> تشغّله البوّابة يبقى <b>مكتوباً باسمه</b>، لا متروكاً ليفترض القارئ
/// أنه مُغطّى.
/// </para>
/// <para>
/// <b>وقد وقع، والثمن مقيس:</b> الإيداع <c>2a34cc9</c> أعاد توليد <c>contracts/openapi/v1.json</c>
/// وتحقّق من حرّاس .NET فوجدها خضراء — ولم يُعِد توليد العميل في <c>web/src/api/generated/</c>.
/// فنزل إلى <c>develop</c> عميلٌ يخالف عقده. ولم يمنعه شيء لأن <c>tools/gate/run.sh</c>
/// لم يكن يشغّل من <c>web/</c> <b>شيئاً على الإطلاق</b>: لا <c>gen:check</c> ولا
/// <c>audit:i18n</c> ولا بناءً ولا اختباراً. والبوّابة التي تُسمّى «البوّابة» ثم تُغطّي
/// نصف المستودع تُعلن خُضرةً أوسع مما قاست.
/// (‏<c>docs/evidence/traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only</c>)
/// </para>
/// <para>
/// <b>والقائمة أدناه ليست إعفاءً، بل إعلان.</b> هي نفس عقد
/// <c>Rule15.BuiltOnlyByTheExplicitBuild</c>: كل خطوة في <c>web.yml</c> إمّا تشغّلها
/// البوّابة محلياً، وإمّا تُكتب هنا <b>ومعها سبب</b>. فمن يقرأ خُضرة البوّابة يعرف
/// بالضبط ما لم تقسه، ومن يضيف خطوةً إلى سير التكامل المستمر يُجبَر على أن يقرّر
/// أيّ الحالتين هي — بدل أن تسقط في الصمت.
/// </para>
/// </summary>
public sealed partial class Rule19_TheGateNamesWhatItDoesNotCover
{
    private const string GateScript = "tools/gate/run.sh";
    private const string WebWorkflow = ".github/workflows/web.yml";

    /// <summary>
    /// خطوات <c>web.yml</c> التي <b>لا</b> تشغّلها البوّابة افتراضياً، وسببُ كلٍّ منها.
    /// <b>ما دام أمرٌ هنا فالبوّابة لا تقيسه</b>، ومن يحذف السبب يحذف التصريح معه.
    /// </summary>
    private static readonly (string Command, string Why)[] NotRunByTheDefaultGate =
    [
        ("npm ci",
            "تثبيت الاعتماديات — دقيقتان تُدفعان في كل بوّابة لكل وكيل؛ خلف --with-frontend"),
        ("npm run build",
            "يحتاج node_modules — خلف --with-frontend"),
        ("npm run lint",
            "يحتاج node_modules — خلف --with-frontend"),
        ("npm test",
            "يحتاج node_modules — خلف --with-frontend"),
        ("npx playwright install --with-deps chromium",
            "تنزيل متصفّح — لا يُدفع محلياً، ولا يُقاس إلا في التكامل المستمر"),
        ("npx playwright test",
            "مصفوفة العرض تحتاج متصفّحاً و`dist/` — التكامل المستمر وحده"),
    ];

    /// <summary>
    /// الفحصان اللذان <b>تشغّلهما</b> البوّابة افتراضياً — مقيسٌ أنهما لا يستوردان إلا
    /// وحدات Node المدمجة، فيعملان بلا <c>node_modules</c> وثمنهما ثوانٍ.
    /// </summary>
    private static readonly string[] RunByTheDefaultGate =
    [
        "node scripts/generate-client.mjs --check",
        "node scripts/audit.mjs",
    ];

    /// <summary>
    /// القدرات التي تملكها البوّابة و<b>لا تُشغّلها افتراضياً</b>، ولكلٍّ رايتها وسببها.
    /// <b>ليست إعفاءً بل إعلاناً</b>: من يقرأ خُضرة البوّابة يعرف بالضبط ما لم تقسه.
    /// </summary>
    private static readonly (string Flag, string Adds, string WhyNotDefault)[] OptionalCapabilities =
    [
        ("--with-frontend",
            "فحوص الواجهة التي تحتاج npm ci: البناء وقواعد الحدّ واختبارات الوحدة",
            "‏npm ci يكلّف دقيقتين في كل بوّابة ولكل وكيل على أربع أنوية — والازدحام نفسه يُفسد القياسات"),
        ("--with-demo",
            "بناء الشركة التجريبية **من الصفر** وتشغيلها بعد إسقاط قواعد العرض الخمس",
            "خطوةٌ **مُدمِّرة**: تُسقط قواعد بيانات، فلا تصلح افتراضاً على آلة يتقاسمها وكلاء يشغّلون مسوحاً؛ وثمنها دقائق"),
    ];

    /// <summary>
    /// كل راية <c>--with-*</c> في نصّ البوّابة <b>مُعلَنة أعلاه ومعها سببها</b>، وكل
    /// راية مُعلَنة <b>منفَّذة فعلاً</b> في النصّ. والاتجاهان كلاهما يُفشل البناء:
    /// رايةٌ جديدة بلا إعلان تُخفي نقص تغطية، وإعلانٌ بلا راية يَعِد بما لا وجود له.
    /// </summary>
    [Fact]
    public void EveryOptionalGateCapabilityIsDeclaredWithItsReasonAndActuallyImplemented()
    {
        string gate = Read(GateScript);
        string[] flagsInScript = [.. GateFlags(gate)];

        Assert.True(
            flagsInScript.Length >= 2,
            $"استُخرجت {flagsInScript.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} راية فقط من "
            + $"{GateScript} — المُحلِّل ضامر والقاعدة تمرّ فراغاً.");

        List<string> problems = [];

        foreach (string flag in flagsInScript)
        {
            if (!OptionalCapabilities.Any(c => string.Equals(c.Flag, flag, StringComparison.Ordinal)))
            {
                problems.Add(
                    $"الراية «{flag}» في {GateScript} وليست في OptionalCapabilities.\n"
                    + "  → أعلنها **ومعها سببها**: قدرةٌ لا تعمل افتراضياً وغير مُعلَنة تُخفي نقص تغطية.");
            }
        }

        foreach ((string flag, string adds, string why) in OptionalCapabilities)
        {
            if (!flagsInScript.Contains(flag, StringComparer.Ordinal))
            {
                problems.Add($"«{flag}» مُعلَنة ولا وجود لها في {GateScript} — إعلانٌ يَعِد بما لا يُنفَّذ.");
            }

            if (string.IsNullOrWhiteSpace(why) || string.IsNullOrWhiteSpace(adds))
            {
                problems.Add($"«{flag}» مُعلَنة بلا سبب أو بلا وصف — تصريحٌ ناقص ليس تصريحاً.");
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>رايات <c>--with-*</c> كما يقرؤها مُحلِّل الوسائط في النصّ.</summary>
    private static IEnumerable<string> GateFlags(string gate)
    {
        foreach (Match match in GateFlagPattern().Matches(gate))
        {
            yield return match.Groups["flag"].Value;
        }
    }

    [Fact]
    public void TheDefaultGateRunsTheTwoDependencyFreeFrontendChecks()
    {
        string gate = Read(GateScript);

        foreach (string command in RunByTheDefaultGate)
        {
            Assert.True(
                gate.Contains(command, StringComparison.Ordinal),
                $"{GateScript} لا يشغّل «{command}» — وهو فحصٌ بلا اعتماديات، فثمنه ثوانٍ. "
                + "بوّابةٌ لا تشغّله تُعلن خُضرةً عن نصف المستودع "
                + "(traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only).");
        }
    }

    /// <summary>
    /// كل أمرٍ في <c>web.yml</c> إمّا تشغّله البوّابة، وإمّا هو **مُصرَّح به** أعلاه.
    /// وأمرٌ جديد لا هذا ولا ذاك يُفشل البناء — وهو المقصود: القرار يُتَّخذ، لا يُنسى.
    /// </summary>
    [Fact]
    public void EveryFrontendWorkflowCommandIsEitherRunLocallyOrDeclaredUnrun()
    {
        string gate = Read(GateScript);
        string[] workflowCommands = [.. WorkflowCommands()];

        Assert.True(
            workflowCommands.Length >= 5,
            $"استُخرج {workflowCommands.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} أمراً فقط من "
            + $"{WebWorkflow} — المُحلِّل ضامر والقاعدة تمرّ فراغاً.");

        List<string> undeclared = [];

        foreach (string command in workflowCommands)
        {
            if (NotRunByTheDefaultGate.Any(entry => string.Equals(entry.Command, command, StringComparison.Ordinal)))
            {
                continue;
            }

            // البوّابة تشغّل `npm run gen:check` بصيغته المباشرة `node scripts/…`.
            if (gate.Contains(command, StringComparison.Ordinal) || RunsTheSameCheck(gate, command))
            {
                continue;
            }

            undeclared.Add(command);
        }

        Assert.True(
            undeclared.Count == 0,
            "أوامرٌ في web.yml لا تشغّلها البوّابة ولا هي مُصرَّح بها في NotRunByTheDefaultGate:\n"
            + string.Join('\n', undeclared.Select(c => $"  {c}\n    → إمّا أن تُشغَّل في {GateScript}، وإمّا أن تُكتب في القائمة **ومعها سبب**.")));

        foreach ((string command, string why) in NotRunByTheDefaultGate)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(why),
                $"التصريح عن «{command}» بلا سبب — تصريحٌ بلا سبب ليس تصريحاً.");

            Assert.True(
                workflowCommands.Contains(command, StringComparer.Ordinal),
                $"«{command}» مُصرَّح بأن البوّابة لا تشغّله، وهو لم يعد في {WebWorkflow} — احذفه من القائمة.");
        }
    }

    /// <summary>‏`npm run gen:check` و`node scripts/generate-client.mjs --check` فحصٌ واحد.</summary>
    private static bool RunsTheSameCheck(string gate, string command) => command switch
    {
        "npm run gen:check" => gate.Contains("scripts/generate-client.mjs --check", StringComparison.Ordinal),
        "npm run audit:i18n" => gate.Contains("scripts/audit.mjs", StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>أوامر `run:` ذات السطر الواحد في سير عمل الواجهة.</summary>
    private static IEnumerable<string> WorkflowCommands()
    {
        foreach (string raw in Read(WebWorkflow).Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("run:", StringComparison.Ordinal))
            {
                continue;
            }

            string command = line["run:".Length..].Trim();

            // ‏`run: |` يفتح كتلة متعدّدة الأسطر — تلك تقارير حصيلة لا فحوص.
            if (command.Length == 0 || command == "|")
            {
                continue;
            }

            yield return command;
        }
    }

    [GeneratedRegex(@"^\s*(?<flag>--with-[a-z-]+)\)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GateFlagPattern();

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepositoryLayout.Root, relative));
}
