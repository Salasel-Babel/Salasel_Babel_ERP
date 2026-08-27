using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 17 — خطوةٌ ساقطة لا تُسكِت ما بعدها.</b>
/// <para>
/// <b>شقيقة القاعدتين 15 و16، من الجهة الثالثة.</b> تلك تسأل «هل تبني البوّابة ما تدّعي
/// تغطيته؟»، وهذه تسأل السؤال الذي لا يجيب عنه ملفّ الإعداد: <b>«متى آخر مرّة نُفِّذت
/// هذه الخطوة فعلاً؟»</b> — لأن وظيفةً من خطوات متتالية في GitHub Actions تتوقّف عند أول
/// سقوط، ويُعلَّم ما بعدها <c>skipped</c> لا <c>failed</c>. والتشغيل يصير أحمر بصدق، ولا
/// يقول سطرٌ واحد إنّ تسع خطوات لم تُنفَّذ.
/// </para>
/// <para>
/// <b>وقد وقع، ومقيس:</b> سير «الواجهة» (<c>web.yml</c>) كان وظيفةً واحدة متتالية، وكانت
/// <c>npm run audit:i18n</c> تسقط بـ<b>154 مخالفة</b> كلّها في <c>web/src/demo/</c>،
/// فتُعلَّم الخمس التي بعدها <c>skipped</c> — <b>ومنها مصفوفة العرض كاملة</b> (٤ لغات ×
/// ٢ مظهر × ٣ عروض). و<b>سبع تشغيلات من سبع</b> على <c>develop</c> انتهت حمراء، أي أن
/// المصفوفة — الحارس الوحيد على الاتجاه والتخطيط في اللغات الأربع — <b>لم تُنفَّذ على أي
/// التزام قطّ</b>. والفرق بين <c>failed</c> و<c>skipped</c> لا يظهر في العنوان ولا في
/// الشارة ولا في إشعار البريد.
/// (‏<c>docs/evidence/traps.md#fakh-an-early-gate-step-silences-every-step-after-it</c>)
/// </para>
/// <para>
/// <b>ولماذا اختبارٌ لا مراجعة:</b> لأن العطل <b>غير مرئي بالقراءة العادية</b> — الملفّ
/// يبدو سليماً، وكل خطوة مكتوبة في مكانها، والتسلسل هو <b>الافتراض</b> في GitHub Actions
/// فلا يلفت النظر. ويكفي أن يُحذف <c>if: always()</c> من سطرٍ واحد ليعود الحارس مطفأً بلا
/// أن يحمرّ شيء. وهذا الملفّ يُفشل البناء عند ذلك الحذف.
/// </para>
/// <para>
/// <b>والقائمة أدناه ليست إعفاءً</b> — هي إعلانُ <b>التبعيات الحقيقية</b>: خطوةٌ لا معنى
/// لتشغيلها بلا سابقتها <b>يجب</b> أن تبقى متخطّاة. والفرق أنّ تخطّيها <b>مُعلَن ومُفسَّر</b>
/// هنا، وأنّ خطوة الحصيلة في السير تقوله بالحرف في ملخّص التشغيل.
/// </para>
/// </summary>
public sealed class Rule17_AFailingGateStepDoesNotSilenceTheStepsAfterIt
{
    private const string WebWorkflow = ".github/workflows/web.yml";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    /// <summary>ما يجعل خطوةً «خطوةَ إثبات»: أمرٌ يفحص أو يبني أو يختبر.</summary>
    private static readonly Regex ProofCommand =
        new(@"\bnpm (run|test)\b|\bnpx playwright\b", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// <b>التبعيات الحقيقية في سير الواجهة</b> — الخطوات التي <b>يصحّ</b> أن تُتخطّى حين
    /// تسقط سابقتها، ولكلٍّ سببها. ما ليس هنا يجب أن ينجو من سقوط غيره.
    /// </summary>
    private static readonly (string Job, string Id, string Why)[] RealDependencies =
    [
        ("rendering-matrix", "build",
            "المصفوفة تُقدَّم من بناء الإنتاج: vite preview يقدّم dist/، فبلا بناءٍ لا شيء يُقدَّم"),
        ("rendering-matrix", "matrix",
            "بلا dist/ لا معنى لتشغيل المصفوفة — وخطوة الحصيلة تقول صراحةً إن لم تُشغَّل ولماذا"),
    ];

    /// <summary>
    /// <b>حرّاس ci.yml النصّية</b> — grep وgit لا غير، لا تمسّ البناء ولا تحتاجه. وكانت
    /// تُسكَت كلّها بسقوط بناءٍ قبلها، <b>ومنها فحص الأسرار</b>.
    /// </summary>
    private static readonly string[] TextualGuardsInTheBackendGate =
    [
        "لا معرّف فخّ بلا رقم على فرع رئيس",
        "لا معرّف قرار بلا رقم على فرع رئيس",
        "سقف دَين الاسم الإنجليزي لا يرتفع",
        "التحقق من عدم وجود أسرار في التغييرات",
    ];

    /// <summary>
    /// كل خطوة إثبات في سير الواجهة تنجو من سقوط غيرها — إلا التبعيات المُعلَنة أعلاه.
    /// </summary>
    [Fact]
    public void EveryIndependentProofStepInTheWebGateSurvivesAnEarlierFailure()
    {
        var declared = RealDependencies.Select(static d => d.Job + "/" + d.Id).ToHashSet(StringComparer.Ordinal);

        var silenceable = ProofSteps(WebWorkflow)
            .Where(static s => !s.If.Contains("always()", StringComparison.Ordinal))
            .Select(static s => s.Job + "/" + (s.Id.Length > 0 ? s.Id : s.Name))
            .ToList();

        var undeclared = silenceable.Where(x => !declared.Contains(x)).ToList();

        Assert.True(
            undeclared.Count == 0,
            "خطوات إثبات في " + WebWorkflow + " يستطيع سقوطُ خطوةٍ قبلها أن يُحوّلها إلى `skipped` "
                + "بلا أن تُعلَن تبعيتها: " + string.Join("، ", undeclared)
                + ". أضِف `if: always()` أو اكتبها في RealDependencies بسببها (فخ-80). · "
                + "Proof steps that an earlier failure can silently turn into `skipped`: "
                + string.Join(", ", undeclared)
                + ". Add `if: always()` or declare the real dependency."
        );
    }

    /// <summary>
    /// التبعيات المُعلَنة هي <b>بالضبط</b> ما في الملفّ — لا زيادة تُخفي إسكاتاً جديداً،
    /// ولا نقصان يجعل القائمة كذباً.
    /// </summary>
    [Fact]
    public void TheDeclaredDependenciesAreExactlyThoseInTheWorkflow()
    {
        var silenceable = ProofSteps(WebWorkflow)
            .Where(static s => !s.If.Contains("always()", StringComparison.Ordinal))
            .Select(static s => s.Job + "/" + (s.Id.Length > 0 ? s.Id : s.Name))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (job, id, why) in RealDependencies)
        {
            Assert.True(
                silenceable.Contains(job + "/" + id),
                "التبعية المُعلَنة " + job + "/" + id + " (" + why + ") لم تعد موجودة في " + WebWorkflow
                    + " — القائمة صارت تصف ملفّاً غير هذا، فاحذف البند أو أصلح السير. · "
                    + "Declared dependency no longer present; the list has stopped describing the file."
            );
        }
    }

    /// <summary>
    /// مصفوفة العرض <b>لا تعتمد على وظيفة أخرى</b>. هذا هو بيت العطل: <c>needs</c> واحدة
    /// تُعيد لحارس نصوصٍ ساقط قدرتَه على منعها من التشغيل.
    /// </summary>
    [Fact]
    public void TheRenderingMatrixJobDependsOnNoOtherJob()
    {
        var text = Read(WebWorkflow);
        var jobs = Jobs(text);

        var matrixJob = ProofSteps(WebWorkflow)
            .FirstOrDefault(static s => s.Run.Contains("playwright test", StringComparison.Ordinal));

        Assert.True(
            matrixJob is not null,
            WebWorkflow + " لا يُشغّل `playwright test` إطلاقاً — مصفوفة العرض اختفت من البوّابة. · "
                + WebWorkflow + " no longer runs the rendering matrix at all."
        );

        Assert.True(
            jobs.TryGetValue(matrixJob!.Job, out var body) && !body.Contains("\n    needs:", StringComparison.Ordinal),
            "وظيفة مصفوفة العرض (" + matrixJob!.Job + ") صار لها `needs` — فسقوطُ وظيفةٍ أخرى "
                + "يمنعها من التشغيل، وهو عطل فخ-80 بعينه. · The rendering-matrix job gained a "
                + "`needs`, so another job's failure can stop it running again."
        );
    }

    /// <summary>
    /// كل وظيفة في سير الواجهة تكتب <b>ما نُفِّذ وما لم يُنفَّذ</b> في ملخّص التشغيل. فحتى
    /// التخطّي المشروع لا يبقى صامتاً — وهو نصف الفخّ الذي لا يُصلحه <c>always()</c> وحده.
    /// </summary>
    [Fact]
    public void EveryJobInTheWebGateSaysWhatRanAndWhatDidNot()
    {
        foreach (var (name, body) in Jobs(Read(WebWorkflow)))
        {
            Assert.True(
                body.Contains("GITHUB_STEP_SUMMARY", StringComparison.Ordinal),
                "وظيفة «" + name + "» في " + WebWorkflow + " لا تكتب حصيلةً في GITHUB_STEP_SUMMARY، "
                    + "فخطوةٌ لم تُنفَّذ تبقى `skipped` صامتة لا يقرأها أحد (فخ-80). · Job writes no "
                    + "run summary, so a step that did not run stays silently `skipped`."
            );
        }
    }

    /// <summary>
    /// حرّاس <c>ci.yml</c> النصّية لا يُسكتها سقوطُ بناء. <b>وفحص الأسرار منها.</b>
    /// </summary>
    [Fact]
    public void TheTextualGuardsInTheBackendGateAreNotSilencedByAnEarlierFailure()
    {
        var steps = Steps(Read(CiWorkflow));

        foreach (var guard in TextualGuardsInTheBackendGate)
        {
            var step = steps.FirstOrDefault(s => s.Name.Contains(guard, StringComparison.Ordinal));

            Assert.True(
                step is not null,
                "الحارس النصّي «" + guard + "» اختفى من " + CiWorkflow + ". · Textual guard vanished."
            );

            Assert.True(
                step!.If.Contains("always()", StringComparison.Ordinal),
                "الحارس النصّي «" + guard + "» في " + CiWorkflow + " بلا `always()` — فسقوطُ بناءٍ "
                    + "أو اختبارٍ قبله يُطفئه ويُعلَّم `skipped`. وهو حارسٌ نصّي بحت لا يمسّ البناء "
                    + "(فخ-80). · A purely textual guard without `always()`: an earlier build or test "
                    + "failure switches it off."
            );
        }
    }

    /// <summary>
    /// حارس اللافراغ. محلّلٌ توقّف عن التحليل يُعيد صفراً، وصفرٌ يُرضي كل تأكيد أعلاه —
    /// وهو بالضبط عطل فخ-43. فيُثبَت هنا أن القراءة قرأت.
    /// </summary>
    [Fact]
    public void TheComputationIsNotVacuous()
    {
        var webJobs = Jobs(Read(WebWorkflow));
        var webProof = ProofSteps(WebWorkflow).ToList();
        var ciSteps = Steps(Read(CiWorkflow));

        Assert.True(webJobs.Count >= 2, "لم يُقرأ من " + WebWorkflow + " إلا " + webJobs.Count + " وظيفة. · too few jobs parsed.");
        Assert.True(webProof.Count >= 5, "لم تُقرأ من " + WebWorkflow + " إلا " + webProof.Count + " خطوة إثبات. · too few proof steps parsed.");
        Assert.True(ciSteps.Count >= 20, "لم تُقرأ من " + CiWorkflow + " إلا " + ciSteps.Count + " خطوة. · too few steps parsed.");
        Assert.NotEmpty(RealDependencies);
        Assert.NotEmpty(TextualGuardsInTheBackendGate);

        // والشاهد الإيجابي: لا بدّ أن يوجد فعلاً في الواجهة خطواتٌ تحمل always()،
        // وإلا فالتأكيد الأول يمرّ لأن المحلّل لا يرى `if` أصلاً لا لأن الملفّ سليم.
        Assert.True(
            webProof.Count(static s => s.If.Contains("always()", StringComparison.Ordinal)) >= 5,
            "لم يُقرأ `if: always()` على أي خطوة إثبات في " + WebWorkflow
                + " — المحلّل لا يرى شرط `if`, فالتأكيدات أعلاه تمرّ بلا معنى. · "
                + "The parser sees no `if:` at all, so the assertions above pass vacuously."
        );
    }

    /* ── قراءة السير: نصّاً، على شاكلة القاعدتين 15 و16 ──────────────────────
       ولا مكتبة YAML: الحارس يجب أن يقرأ ما يقرأه الإنسان في المراجعة، وأن يظلّ
       يعمل إن تغيّرت حزمة. والشكل هنا ثابت ومعلوم: وظائف بمسافتين، وخطوات بستّ. */

    private sealed record Step(string Job, string Name, string Id, string If, string Run);

    private static string Read(string relative)
    {
        var path = Path.Combine(RepositoryLayout.Root, relative);
        Assert.True(File.Exists(path), relative + " غير موجود — وهو جزء من البوّابة. · missing.");
        return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>الوظائف: الاسم ونصّها كاملاً.</summary>
    private static Dictionary<string, string> Jobs(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = text.Split('\n');
        var inJobs = false;
        string? current = null;
        var body = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("jobs:", StringComparison.Ordinal)) { inJobs = true; continue; }
            if (!inJobs) continue;

            // اسم وظيفة: مسافتان بالضبط ثم معرّف ثم نقطتان.
            var match = Regex.Match(line, @"^  ([A-Za-z][A-Za-z0-9_-]*):\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));
            if (match.Success)
            {
                if (current is not null) result[current] = string.Join("\n", body);
                current = match.Groups[1].Value;
                body = [];
                continue;
            }

            if (current is not null) body.Add(line);
        }

        if (current is not null) result[current] = string.Join("\n", body);
        return result;
    }

    /// <summary>كل خطوة في الملفّ، منسوبةً إلى وظيفتها.</summary>
    private static List<Step> Steps(string text)
    {
        var steps = new List<Step>();

        foreach (var (job, body) in Jobs(text))
        {
            var lines = body.Split('\n');
            var blocks = new List<List<string>>();
            List<string>? block = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("      - ", StringComparison.Ordinal))
                {
                    if (block is not null) blocks.Add(block);
                    block = [line];
                    continue;
                }

                block?.Add(line);
            }

            if (block is not null) blocks.Add(block);

            foreach (var b in blocks)
            {
                var joined = string.Join("\n", b);
                steps.Add(new Step(
                    job,
                    Field(joined, "name"),
                    Field(joined, "id"),
                    Field(joined, "if"),
                    joined
                ));
            }
        }

        return steps;
    }

    /// <summary>الخطوات التي تفحص أو تبني أو تختبر — لا الجلب ولا التهيئة ولا الرفع.</summary>
    private static IEnumerable<Step> ProofSteps(string relative) =>
        Steps(Read(relative)).Where(static s => ProofCommand.IsMatch(s.Run) && !s.Run.Contains("npm ci", StringComparison.Ordinal));

    /// <summary>قيمة مفتاحٍ في الخطوة — على مستوى الخطوة وحدها (ثماني مسافات، أو بعد «- »).</summary>
    private static string Field(string block, string key)
    {
        var match = Regex.Match(
            block,
            @"^(?:      - |        )" + Regex.Escape(key) + @":[ \t]*(.*)$",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5)
        );
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
