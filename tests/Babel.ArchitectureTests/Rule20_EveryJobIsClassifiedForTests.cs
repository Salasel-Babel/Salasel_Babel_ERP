using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 20 — كل وظيفة مصنَّفة، وكل سطح اختبارٍ مُدَّعى، ولا شيء يمرّ بلا تصنيف.</b>
/// <para>
/// <b>لماذا وُجدت:</b> الحارس السابق على «صفر اختبارات» كان <b>قائمةَ خيارات</b> يرفضها
/// مُشغّل الاختبارات (‏<c>--nologo</c> وأخواتها). وهُزم مرّتين في يوم واحد: بنقل
/// <c>--nologo</c> إلى <b>سطر استمرار</b> داخل <c>run: |</c> — فلم يعد السطر يحوي
/// <c>dotnet test</c> فلم يره الحارس أصلاً — وبـ<c>--diag</c>، الذي يُنتج فشلاً مطابقاً
/// بايتاً ببايت و<b>ليس في القائمة</b>. وقائمةُ الخيارات مفتوحة: كل خيارٍ جديد، وكل
/// مطبعة، وكل ترقية حزمةِ تطوير، تضيف بنداً لم يخطر لأحد.
/// </para>
/// <para>
/// <b>والمقيس الذي بدأ كل هذا:</b> السطر <c>data-validation.yml:38</c> على <c>develop</c>
/// كان <c>dotnet test … --nologo</c>، و<c>global.json</c> يُدخل المستودع كلَّه في
/// <c>Microsoft.Testing.Platform</c> الذي يرفض خيارات حقبة VSTest. فتلك الخطوة كانت
/// تُشغّل <b>صفر اختبارات</b> كل يوم: <c>Zero tests ran · Exit code: 5 · total: 0</c>.
/// </para>
/// <para>
/// <b>فما الذي يُفحَص هنا بدل ذلك:</b> لا خيارٌ ولا نصُّ أمر. بل ثلاث مجموعات <b>مغلقة
/// مصدرها القرص</b>، وكلُّ عضوٍ فيها يجب أن يكون مُصنَّفاً — وما لا تصنيف له <b>يسقط</b>:
/// <list type="number">
///   <item><b>وظائف السير:</b> كل وظيفة في كل ملفّ تحت <c>.github/workflows/</c> مصنَّفة
///         مرّةً واحدة، إمّا <c>tallied</c> وإمّا <c>untallied</c>. وظيفةٌ جديدة أو
///         معادةُ التسمية أو منسوخة — كلّها حمراء حتى تُصنَّف.</item>
///   <item><b>مشاريع الاختبار:</b> كل مشروع اختبارٍ على القرص يدّعيه سطحٌ واحد بالضبط.
///         و«مشروع اختبار» ليس قائمةً هنا: هو <b>شرط البناء نفسه</b> المقروء من
///         <c>Directory.Build.props</c>، ويسقط هذا الملفّ إن تغيّر الشرط تحته.</item>
///   <item><b>إعدادات مُشغّلات الواجهة:</b> كل <c>web/*.config.*</c> إمّا سطحُ اختبارٍ
///         مُعلَن وإمّا مُصرَّحٌ بأنه لا يُشغّل اختباراً — بسببه.</item>
/// </list>
/// </para>
/// <para>
/// <b>وهذا الملفّ لا يقرأ خياراً واحداً</b>، فلا يستطيع خيارٌ أن يهزمه. أمّا إثباتُ أن
/// الاختبارات <b>نُفِّذت فعلاً</b> فليس هنا أصلاً: هو في <c>tools/test-tally/run.sh</c>،
/// الذي يقرأ <b>ما أنتجه التشغيل</b> — تقريرٌ موجود، وعددُ ما نُفِّذ عند الأرضية أو
/// فوقها، وصفرُ إخفاق — ولا يبالي بأيّ خيارٍ سبّب النقص.
/// (‏<c>docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy</c>)
/// </para>
/// </summary>
public sealed class Rule20_EveryJobIsClassifiedForTests
{
    private const string ManifestPath = "tests/test-surfaces.json";
    private const string WorkflowFolder = ".github/workflows";
    private const string TallyScript = "tools/test-tally/run.sh";

    /// <summary>
    /// <b>شرطُ «مشروع اختبار» كما يراه البناء نفسه</b>، حرفيّاً من
    /// <c>Directory.Build.props</c>. لا يُقرأ هذا الثابت للزينة: يُبحَث عنه في الملفّ،
    /// وإن اختفى سقط الاختبار — لأن اكتشافَ مشاريع الاختبار أدناه مبنيٌّ عليه، وتغييرُه
    /// تحته يجعل الاكتشاف يقرأ مجموعةً غير التي يبنيها MSBuild، بصمت.
    /// </summary>
    private const string TestProjectCondition = "$(MSBuildProjectName.EndsWith('Tests'))";

    /// <summary>التصنيفات — مجموعة مغلقة نملكها. تصنيفٌ خارجها يسقط.</summary>
    private static readonly string[] Classifications = ["tallied", "untallied"];

    /// <summary>المُشغّلات — مجموعة مغلقة نملكها، ويعرفها ملفّ الحصيلة نفسه.</summary>
    private static readonly string[] Runners = ["dotnet", "vitest", "playwright"];

    private static readonly Lazy<Manifest> Loaded = new(Manifest.Load);

    // ── ١ · الوظائف ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القلب.</b> كل وظيفة على القرص مصنَّفة، ولا تصنيف بلا وظيفة. المجموعة مغلقة
    /// من الجهتين، فلا تُهزَم بشيءٍ «لم يخطر لأحد»: ما لم يخطر لأحد <b>غير مصنَّف</b>،
    /// وغيرُ المصنَّف أحمر.
    /// </summary>
    [Fact]
    public void EveryJobInEveryWorkflowIsClassifiedExactlyOnce()
    {
        var onDisk = JobsOnDisk();
        var manifest = Loaded.Value;

        var declared = manifest.Jobs
            .GroupBy(static job => job.Workflow + ":" + job.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        var unclassified = onDisk.Where(job => !declared.ContainsKey(job)).ToList();
        Assert.True(
            unclassified.Count == 0,
            "وظائف في السير لا تصنيف لها في " + ManifestPath + ": " + string.Join("، ", unclassified)
                + ". صنّفها `tallied` (وتُضيف خطوة الحصيلة) أو `untallied` بسببها — وما لا تصنيف "
                + "له يسقط عمداً، لأن هذا هو الفرق بين قائمةٍ تُهزَم بما لم يخطر لأحد ومجموعةٍ "
                + "مغلقة لا يدخلها شيء بصمت. · Jobs with no classification: an unclassified job is "
                + "red by construction, which is exactly what a blocklist of options could never be."
        );

        var stale = declared.Keys.Where(job => !onDisk.Contains(job)).ToList();
        Assert.True(
            stale.Count == 0,
            "تصنيفات في " + ManifestPath + " لوظائف لا وجود لها: " + string.Join("، ", stale)
                + " — السجلّ صار يصف سيراً غير هذا. · Classifications for jobs that no longer exist."
        );

        var twice = declared.Where(static entry => entry.Value > 1).Select(static entry => entry.Key).ToList();
        Assert.True(
            twice.Count == 0,
            "وظائف مصنَّفة أكثر من مرّة: " + string.Join("، ", twice)
                + " — تصنيفان لوظيفة واحدة يعني أن أحدهما لا يُقرأ. · Doubly classified jobs."
        );
    }

    /// <summary>التصنيف من المجموعة المغلقة، وكلُّ <c>untallied</c> يحمل سببه مكتوباً.</summary>
    [Fact]
    public void EveryClassificationIsFromTheClosedSetAndCarriesItsReason()
    {
        foreach (var job in Loaded.Value.Jobs)
        {
            Assert.True(
                Classifications.Contains(job.Classification, StringComparer.Ordinal),
                "الوظيفة " + job.Workflow + ":" + job.Name + " مصنَّفة «" + job.Classification
                    + "» وهو ليس من {" + string.Join("، ", Classifications) + "}. · Classification "
                    + "outside the closed set: the set is owned here, so a new value is a deliberate change."
            );

            Assert.True(
                job.Why.Length >= 20,
                "الوظيفة " + job.Workflow + ":" + job.Name + " بلا سببٍ مكتوب (`why`). و`untallied` "
                    + "بلا سبب هي بالضبط الباب الذي يدخل منه «هذه الوظيفة لا تُشغّل اختبارات» وهي "
                    + "تُشغّلها. · No written reason: an unexplained classification is the escape hatch."
            );
        }
    }

    /// <summary>
    /// كل وظيفة <c>tallied</c> تحمل ختم البدء وخطوة الحصيلة، والحصيلة تسمّي <b>وظيفتها
    /// هي</b> — فلا تُحصي أسطح غيرها وتبدو خضراء.
    /// </summary>
    [Fact]
    public void EveryTalliedJobBeginsTheReportDirectoryAndTalliesItsOwnSurfaces()
    {
        foreach (var job in Loaded.Value.Jobs.Where(static j => j.Classification == "tallied"))
        {
            var body = JobBody(job.Workflow, job.Name);
            var selector = "--job " + Path.GetFileName(job.Workflow) + ":" + job.Name;

            // ‏**والبحث في أوامرَ مقروءة لا في نصّ الوظيفة.** كان `body.Contains(…)` —
            // فسطرُ تعليقٍ يحمل نصّ الأمر كان يُرضيه **بعد حذف الخطوة نفسها**.
            Assert.True(
                StepsOf(job.Workflow, job.Name).Any(s => s.Commands.Any(c => c.Contains(TallyScript + " --begin", StringComparison.Ordinal))),
                "الوظيفة " + job.Workflow + ":" + job.Name + " مصنَّفة `tallied` ولا تختم مجلّد "
                    + "التقارير قبل الاختبارات (`" + TallyScript + " --begin`) — فتقريرٌ من تشغيلٍ "
                    + "سابق قد يُرضي الحصيلة بلا أن يُنفَّذ شيء. · A tallied job that does not stamp "
                    + "the report directory first."
            );

            var step = TallyStepOf(job.Workflow, job.Name, selector);

            Assert.True(
                string.Equals(step.If, "always()", StringComparison.Ordinal),
                "شرطُ خطوة الحصيلة في " + job.Workflow + ":" + job.Name + " هو «" + (step.If ?? "لا شرط")
                    + "» والمنتظَر `always()` **وحدها**. والمطابقة تامّةٌ لا احتواء: "
                    + "`always() && github.event_name == 'schedule'` يحوي `always()` ويُسكِت الخطوة "
                    + "في كل دفعةٍ عادية (فخ-80). · The tally step's condition must be exactly always()."
            );

            Assert.False(
                step.ContinueOnError,
                "خطوة الحصيلة في " + job.Workflow + ":" + job.Name + " تحمل `continue-on-error` — "
                    + "أي أن سقوطها لا يُحمِّر شيئاً، وهي الخطوة الوحيدة التي تعرف ما نُفِّذ. · The tally "
                    + "step is allowed to fail without failing the job."
            );
        }
    }

    /// <summary>
    /// <b>والحصيلة لا تُخصى بالصَّدَفة.</b> الحارس السابق كان يسأل «هل فيها
    /// <c>continue-on-error</c>؟» ولا يسأل <b>ما الأمر الذي تصل حالتُه إلى الوظيفة</b>.
    /// فكان <c>run: tools/test-tally/run.sh --job ci.yml:build-and-enforce || true</c>
    /// يُبقي 205/205 والقاعدة 20 خضراوين والحصيلةُ عاجزةً عن إسقاط شيء أبداً — و
    /// <c>|| true</c> مستعملةٌ في هذا المستودع نفسه (<c>data-validation.yml</c>) فلا
    /// تلفت نظر مراجع.
    /// <para>
    /// <b>والقاعدة ليست تعداد ما يُخصي</b> (<c>|| true</c> · <c>&amp;</c> · <c>; true</c> ·
    /// <c>|| echo skip</c> · <c>set +e</c> · أنبوب): تلك قائمةٌ مفتوحة يهزمها أوّل شكلٍ
    /// لم يخطر. بل خاصّيةٌ واحدة: <b>الاستدعاء هو آخر أمرٍ في <c>run</c>، وحدَه، بلا عامل
    /// صَدَفةٍ حوله</b> — فحالةُ خروج الخطوة هي حالتُه بالبناء. و<c>set +e</c> تسقط من
    /// الحساب لأنها لا تُغيّر حالةَ الأمر الأخير.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTallyStepCannotBeNeuteredByTheShell()
    {
        foreach (var job in Loaded.Value.Jobs.Where(static j => j.Classification == "tallied"))
        {
            var selector = "--job " + Path.GetFileName(job.Workflow) + ":" + job.Name;
            var invocation = TallyScript + " " + selector;
            var step = TallyStepOf(job.Workflow, job.Name, selector);

            Assert.True(
                step.Shell is null || step.Shell.StartsWith("bash", StringComparison.Ordinal),
                "خطوة الحصيلة في " + job.Workflow + ":" + job.Name + " تُبدّل صَدَفتها إلى «"
                    + step.Shell + "» — وصَدَفةٌ أخرى قد تُغيّر معنى حالة الخروج. · unexpected shell."
            );

            Assert.True(
                StatusReachesTheJob(step, invocation),
                "حالةُ خروج الحصيلة في " + job.Workflow + ":" + job.Name + " **لا تصل الوظيفة**. "
                    + "‏`run` فيها " + step.Commands.Count + " أمراً — "
                    + string.Join(" ⏎ ", step.Commands.Select(static c => "«" + c + "»"))
                    + " — والمنتظَر أمرٌ واحد هو «" + invocation + "» وحدَه، بلا سطرٍ قبله "
                    + "وبلا `||` ولا `;` ولا `&` ولا أنبوب. وأيُّ سطرٍ سابق يستطيع أن يُلغي "
                    + "حالةَ الخروج (شرَك) أو أن يُبدّل ما يُستدعى (كتابةٌ فوق السكربت، دالّةٌ، "
                    + "`PATH`) — ولا تُعدّ الطرق فتُعدّ الأسطر. "
                    + "· The tally step must be one command and nothing else."
            );

            var body = JobBody(job.Workflow, job.Name);
            Assert.DoesNotContain("continue-on-error: true", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>شاهدٌ موجب لكل شكلٍ يُخصي — وعددُها يساوي عدد ما يزعم الحكم أنه يمنعه.</b>
    /// الحكمُ أعلاه خاصّيةٌ لا قائمة، لكنه يبقى دعوى حتى يُقاس على الأشكال التي هزمت
    /// سابقَه. وكلُّ شكلٍ هنا مأخوذٌ حرفيّاً من قياس المُحقِّق.
    /// </summary>
    [Fact]
    public void TheStepReaderIsProvenOnTheShapesThatDefeatedItsPredecessor()
    {
        const string Invocation = TallyScript + " --job ci.yml:build-and-enforce";

        static ShellStep Step(string ifExpr, string run) =>
            ParseStep("      - name: الحصيلة\n        if: " + ifExpr + "\n        run: " + run);

        // ① الشكل السليم يمرّ — وإلّا كان الحكم يمنع كل شيء ولا يُثبت شيئاً.
        var good = Step("always()", Invocation);
        Assert.Equal("always()", good.If);
        Assert.True(StatusReachesTheJob(good, Invocation));

        // ② وكلُّ شكلٍ يُخصي يسقط.
        string[] neutered =
        [
            Invocation + " || true",
            Invocation + " &",
            Invocation + " ; true",
            Invocation + " || echo skip",
            Invocation + " | cat",
            Invocation + " && true",
            "true || " + Invocation,
        ];

        foreach (var run in neutered)
        {
            Assert.False(
                StatusReachesTheJob(Step("always()", run), Invocation),
                "الشكل «" + run + "» يُخصي الحصيلة والحكم لم يمسكه. · a neutering shape passed."
            );
        }

        // ③ و`set +e` مع أمرٍ بعده: الأخيرُ ليس الاستدعاء، فتسقط.
        var loosened = ParseStep(
            "      - name: الحصيلة\n        if: always()\n        run: |\n"
            + "          set +e\n          " + Invocation + "\n          echo done"
        );
        Assert.False(StatusReachesTheJob(loosened, Invocation));

        // ④ **والاستدعاء آخرَ أمرٍ في كتلةٍ حرفية لا يكفي** — وهذا الشاهد كان مقلوباً،
        //    يؤكّد الثغرةَ بدل أن يمسكها. والشكلان أدناه مأخوذان حرفيّاً من قياس
        //    المُحقِّق على `ci.yml:345-346`، وكلاهما أعطى القاعدة 20 خضراءَ 15/15:
        //      · شرَكٌ يُلغي حالة الخروج بعد وقوعها — والصَّدَفة `bash -e`؛
        //      · كتابةٌ فوق السكربت المُستدعى — فالحالةُ تصل الوظيفة وهي حالةُ نصٍّ آخر.
        //    ومعهما ثالثٌ ورابع من الجنس نفسه (دالّةُ صَدَفةٍ باسم السكربت، و`PATH`)
        //    ليُقرأ أن هذه **ليست قائمةَ أشكال**: الحكم لا يقرؤها، إنّما يرفض كلَّ كتلةٍ
        //    فيها أكثر من أمرٍ واحد.
        string[] precededByOneLine =
        [
            "trap 'exit 0' EXIT",
            "echo \"exit 0\" > tools/test-tally/run.sh",
            "run() { return 0; }",
            "export PATH=/tmp/shim:$PATH",
            "echo before",
        ];

        foreach (var prelude in precededByOneLine)
        {
            var literal = ParseStep(
                "      - name: الحصيلة\n        if: always()\n        run: |\n"
                + "          " + prelude + "\n          " + Invocation
            );

            Assert.Equal(2, literal.Commands.Count);
            Assert.False(
                StatusReachesTheJob(literal, Invocation),
                "سطرٌ سابق «" + prelude + "» في كتلة الحصيلة نفسها والحكم لم يمسكه — "
                    + "وحالةُ الخروج التي تصل الوظيفة لم تعد حالةَ الحصيلة. · a preceding line passed."
            );
        }

        // ⑤ و`if` تُطابَق تامّةً: ما يحوي `always()` وليس إيّاها يسقط.
        Assert.NotEqual("always()", Step("always() && github.event_name == 'schedule'", Invocation).If);

        // ⑥ والتعليق ليس أمراً: خطوةٌ حُذف أمرُها وبقي وصفُه لا تُقرأ حصيلة.
        var commented = ParseStep(
            "      - name: الحصيلة\n        if: always()\n        run: |\n"
            + "          # " + Invocation + "\n          echo nothing"
        );
        Assert.DoesNotContain(commented.Commands, c => c.Contains(TallyScript, StringComparison.Ordinal));

        // ⑦ و`continue-on-error` تُقرأ مفتاحاً لا نصّاً.
        Assert.True(ParseStep("      - name: x\n        continue-on-error: true\n        run: echo").ContinueOnError);
        Assert.False(ParseStep("      - name: x\n        continue-on-error: false\n        run: echo").ContinueOnError);
    }

    /// <summary>
    /// <b>والعكس — وهو الذي أغلق الثغرة التي وجدتُها في نفسي.</b> كان يُفحَص أن كل وظيفة
    /// <c>tallied</c> تحمل خطوة الحصيلة، ولم يكن يُفحَص العكس. فكان يكفي أن يُغيَّر تصنيف
    /// وظيفةٍ إلى <c>untallied</c> ليصمت الحارس عنها — وقد جرّبتُه فمرّ أخضر. فيُفحَص
    /// هنا الاتجاهان: وظيفةٌ فيها خطوة حصيلةٍ <b>يجب</b> أن تكون <c>tallied</c>، وأن
    /// تسمّي نفسها في وسيط <c>--job</c>.
    /// </summary>
    [Fact]
    public void EveryJobThatCarriesATallyStepIsClassifiedTallied()
    {
        foreach (var job in JobsOnDisk())
        {
            var (workflow, name) = Split(job);
            var body = JobBody(workflow, name);
            if (!body.Contains(TallyScript + " --job", StringComparison.Ordinal)) continue;

            var entry = Loaded.Value.Jobs.FirstOrDefault(j => j.Workflow == workflow && j.Name == name);

            Assert.True(
                entry is not null && entry.Classification == "tallied",
                "الوظيفة " + job + " تحمل خطوة حصيلة وهي مصنَّفة «"
                    + (entry?.Classification ?? "بلا تصنيف") + "» — والحصيلة نفسها ترفض أن تُحصي "
                    + "لوظيفةٍ غير `tallied`، فالسير يسقط عند التشغيل والسجلّ يقول غير ذلك. · A job "
                    + "carrying a tally step must be classified tallied; otherwise the manifest and "
                    + "the workflow disagree and the run fails while the guard stays green."
            );

            var selector = "--job " + Path.GetFileName(workflow) + ":" + name;
            Assert.True(
                body.Contains(TallyScript + " " + selector, StringComparison.Ordinal),
                "الوظيفة " + job + " تُحصي باسم وظيفةٍ غير اسمها — المنتظَر `" + selector
                    + "`. فتُحصي أسطح غيرها وتبدو خضراء. · The job tallies under another job's name."
            );
        }
    }

    /// <summary>
    /// <b>وظيفةٌ تُشغّل سطحاً تُحصيه.</b> لكل سطحٍ في السجلّ علاماتُ استدعاءٍ يملكها
    /// السجلّ (‏<c>invokedBy</c>): مسارُ مشروعه، و<c>--solution Babel.slnx</c> الذي
    /// يُشغّل كلَّ أسطح .NET، واسمُ مُشغّل الواجهة. فإن ظهرت علامةٌ في جسد وظيفة، وجب
    /// أن تكون تلك الوظيفة <c>tallied</c> وأن تدّعي ذلك السطح.
    /// <para>
    /// <b>وهذه طبقةٌ ثانية لا الأولى.</b> الأولى بنيويّة ولا حارس لها:
    /// <c>--minimum-expected-tests</c> على كل نداء اختبار تُسقط التشغيل بالرمز 9 مهما
    /// كان الحارس. وهذه تمنع أن يُنزَع التصنيف بهدوء عن وظيفةٍ ما زالت تُشغّل اختبارات.
    /// وحدُّها مُعلَن: لا تلتقط استدعاءً مبنيّاً من متغيّر أو مخبوءاً في مُغلِّف —
    /// وهو ما تقوله الطبقة الأولى والحصيلة معاً.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryJobThatInvokesASurfaceTalliesIt()
    {
        foreach (var job in JobsOnDisk())
        {
            var (workflow, name) = Split(job);
            var body = JobBody(workflow, name);
            var entry = Loaded.Value.Jobs.FirstOrDefault(j => j.Workflow == workflow && j.Name == name);
            if (entry is null) continue; // يلتقطه اختبار التصنيف أعلاه.

            foreach (var surface in Loaded.Value.Surfaces)
            {
                var marker = surface.InvokedBy.FirstOrDefault(m => body.Contains(m, StringComparison.Ordinal));
                if (marker is null) continue;

                Assert.True(
                    entry.Classification == "tallied",
                    "الوظيفة " + job + " تستدعي السطح «" + surface.Id + "» (بالعلامة «" + marker
                        + "») وهي مصنَّفة «" + entry.Classification + "» — أي تُشغّل اختبارات بلا "
                        + "حصيلةٍ تقول إنها نُفِّذت. · A job that invokes a surface but is not tallied."
                );

                Assert.Contains(surface.Id, entry.Surfaces);
            }
        }
    }

    // ── ٢ · الأسطح ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>كل مشروع اختبارٍ على القرص يدّعيه سطحٌ واحد بالضبط.</b> وهذا ما يمنع الكذب في
    /// السجلّ: لا يكفي أن يُصنَّف أحدٌ وظيفتَه <c>untallied</c> ليُفلت — لأن التغطية
    /// تُشتقّ من <b>القرص</b> لا من السجلّ، فالمشروع يبقى مطلوباً وسطحُه يبقى مطلوباً
    /// أن تُحصيه وظيفةٌ ما.
    /// </summary>
    [Fact]
    public void EveryTestProjectOnDiskIsClaimedByExactlyOneSurface()
    {
        var onDisk = TestProjectsOnDisk();
        var claimed = Loaded.Value.Surfaces
            .Where(static s => s.Runner == "dotnet")
            .GroupBy(static s => s.Project, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

        var unclaimed = onDisk.Where(project => !claimed.ContainsKey(project)).ToList();
        Assert.True(
            unclaimed.Count == 0,
            "مشاريع اختبارٍ على القرص لا يدّعيها سطحٌ في " + ManifestPath + ": "
                + string.Join("، ", unclaimed) + ". مشروعٌ لا سطحَ له لا أرضيةَ له، ولا شيء يقول "
                + "إن اختباراته نُفِّذت. · Test projects on disk that no surface claims: no floor, "
                + "and nothing says their tests ran."
        );

        var ghosts = claimed.Keys.Where(project => !onDisk.Contains(project)).ToList();
        Assert.True(
            ghosts.Count == 0,
            "أسطحٌ تدّعي مشاريع لا وجود لها: " + string.Join("، ", ghosts) + ". · Surfaces claiming "
                + "projects that are not on disk."
        );

        var twice = claimed.Where(static e => e.Value > 1).Select(static e => e.Key).ToList();
        Assert.True(twice.Count == 0, "مشروعٌ يدّعيه سطحان: " + string.Join("، ", twice) + ".");
    }

    /// <summary>
    /// <b>الشرط الذي يُشتقّ منه «مشروع اختبار» ما زال هو شرط البناء.</b> لو غُيّر في
    /// <c>Directory.Build.props</c> لصار الاختبار أعلاه يمسح مجموعةً غير التي يبنيها
    /// MSBuild — ويمرّ أخضر وهو يقرأ الملفّ الخطأ. فيسقط هنا بدل ذلك.
    /// </summary>
    [Fact]
    public void TheTestProjectPredicateIsStillTheOneTheBuildUses()
    {
        var props = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "Directory.Build.props"));

        Assert.True(
            props.Contains(TestProjectCondition, StringComparison.Ordinal),
            "شرط «مشروع اختبار» في Directory.Build.props لم يعد `" + TestProjectCondition + "` — "
                + "واكتشافُ مشاريع الاختبار هنا مبنيٌّ عليه حرفياً. عدّل الثابت في هذا الملفّ مع "
                + "الشرط، وإلا صار الحارس يمسح مجموعةً غير التي يبنيها MSBuild بصمت. · The build's "
                + "own test-project predicate changed; this guard's discovery is derived from it."
        );
    }

    /// <summary>كل سطحٍ يُحصيه على الأقلّ وظيفةٌ واحدة. سطحٌ لا يُحصيه أحد لا يُنفَّذ ولا يُلاحَظ.</summary>
    [Fact]
    public void EverySurfaceIsTalliedBySomeJob()
    {
        var tallied = Loaded.Value.Jobs
            .Where(static job => job.Classification == "tallied")
            .SelectMany(static job => job.Surfaces)
            .ToHashSet(StringComparer.Ordinal);

        var orphans = Loaded.Value.Surfaces
            .Select(static surface => surface.Id)
            .Where(id => !tallied.Contains(id))
            .ToList();

        Assert.True(
            orphans.Count == 0,
            "أسطحٌ لا تُحصيها أي وظيفة: " + string.Join("، ", orphans) + " — مُعلَنةٌ ولا يقول شيءٌ "
                + "إنها نُفِّذت. · Declared surfaces that no job tallies."
        );

        var phantom = tallied.Where(id => Loaded.Value.Surfaces.All(s => s.Id != id)).ToList();
        Assert.True(phantom.Count == 0, "وظيفة تدّعي سطحاً لا وجود له: " + string.Join("، ", phantom));
    }

    /// <summary>شكلُ السطح: معرّفٌ فريد، ومُشغّلٌ معروف، وأرضيةٌ موجبة، ومسارٌ موجود.</summary>
    [Fact]
    public void EverySurfaceIsWellFormedAndItsFloorIsPositive()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var surface in Loaded.Value.Surfaces)
        {
            Assert.True(seen.Add(surface.Id), "معرّف سطحٍ مكرّر: " + surface.Id + ". · Duplicate surface id.");

            Assert.True(
                Runners.Contains(surface.Runner, StringComparer.Ordinal),
                "السطح " + surface.Id + " يُعلن مُشغّلاً خارج المجموعة المغلقة {"
                    + string.Join("، ", Runners) + "}: «" + surface.Runner + "». · Runner outside the "
                    + "closed set; tools/test-tally cannot read its report and refuses to pass it."
            );

            Assert.True(
                surface.MinimumExecuted >= 1,
                "السطح " + surface.Id + " أرضيتُه " + surface.MinimumExecuted + " — وأرضيةُ صفرٍ "
                    + "تُرضيها كل مجموعةٍ فارغة، وهي العطل نفسه. · A floor of zero is satisfied by "
                    + "an empty run: that is the very defect."
            );

            var path = surface.Runner == "dotnet" ? surface.Project : surface.Config;
            Assert.True(
                path.Length > 0 && File.Exists(Path.Combine(RepositoryLayout.Root, path)),
                "السطح " + surface.Id + " يشير إلى مسارٍ لا وجود له: «" + path + "». · Surface points "
                    + "at a path that is not on disk."
            );
        }
    }

    /// <summary>
    /// <b>كل إعداد مُشغّلٍ في الواجهة مُصنَّف.</b> المجموعة مغلقة ومصدرها القرص
    /// (‏<c>web/*.config.*</c>): مُشغّلُ اختباراتٍ ثالث يدخل الواجهة يأتي معه ملفُّ إعداد،
    /// فيقع خارج التصنيف ويسقط — بدل أن يعمل بلا حصيلةٍ ولا أرضية.
    /// </summary>
    [Fact]
    public void EveryFrontEndConfigIsEitherATestSurfaceOrDeclaredNotToBeOne()
    {
        var configs = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "web"), "*.config.*", SearchOption.TopDirectoryOnly)
            .Select(path => "web/" + Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(configs.Count >= 3, "لم يُقرأ من web/ إلا " + configs.Count + " إعداداً — المسح لا يمسح. · too few configs read.");

        var asSurface = Loaded.Value.Surfaces
            .Where(static surface => surface.Runner != "dotnet")
            .Select(static surface => surface.Config)
            .ToHashSet(StringComparer.Ordinal);

        var asNonRunner = Loaded.Value.NonRunnerConfigs.ToHashSet(StringComparer.Ordinal);

        var unclassified = configs.Where(config => !asSurface.Contains(config) && !asNonRunner.Contains(config)).ToList();
        Assert.True(
            unclassified.Count == 0,
            "إعدادات في web/ لا تصنيف لها في " + ManifestPath + ": " + string.Join("، ", unclassified)
                + ". إمّا سطحُ اختبارٍ بأرضية، وإمّا مُصرَّحٌ في `webConfigsThatRunNoTests` بسببه. · "
                + "Front-end configs that are neither a declared test surface nor declared not to be one."
        );

        var both = asSurface.Intersect(asNonRunner, StringComparer.Ordinal).ToList();
        Assert.True(both.Count == 0, "إعدادٌ مُصنَّف مرّتين ومتناقضاً: " + string.Join("، ", both));

        var ghosts = asNonRunner.Where(config => !configs.Contains(config)).ToList();
        Assert.True(ghosts.Count == 0, "تصريحٌ لإعدادٍ لا وجود له: " + string.Join("، ", ghosts));
    }

    /// <summary>
    /// <b>الثقب الذي لا يُغلقه هذا الحارس — مُعلَناً ومحدوداً.</b>
    /// <para>
    /// <b>مقيس على هذا الفرع:</b> إن غُيّر تصنيفُ وظيفةٍ إلى <c>untallied</c>، <b>و</b>نُزعت
    /// منها خطوةُ الحصيلة والختم، <b>و</b>نُزعت رايات <c>--minimum-expected-tests</c> و
    /// <c>--report-xunit-trx</c>، <b>و</b>بُني مسارُ المشروع من متغيّر صَدَفة فلم يعد يطابق
    /// <c>invokedBy</c> نصّاً — فإن القاعدة 20 تبقى <b>خضراء</b> (‏12 من 12). جُرّب فمرّ.
    /// وهذا حدٌّ بنيويّ لا سهو: التحقّق من المُخرَج يُثبت أنّ ما أُعلن قد جرى، ولا يستطيع
    /// أن يُثبت أنّ ما لم يُعلن لم يجرِ — لأن التشغيل الذي لا يُبلّغ لا يترك مُخرَجاً يُسأل.
    /// </para>
    /// <para>
    /// <b>وهذا الاختبار يحدّ الضرر بدل أن يدّعي إغلاقه.</b> الثقب يبقى ثقباً في
    /// <b>مراقبة نداءٍ مكرّر</b>، لا في <b>تغطية سطح</b> — بشرطٍ واحد يُنفَّذ هنا: أن تكون
    /// هناك وظيفةٌ <c>tallied</c> واحدة تُشغّل <b>الحلّ كلّه</b> وتدّعي <b>كلّ</b> أسطح
    /// .NET. فما دام ذلك قائماً، لا يُخفي أيُّ تنزيلِ تصنيفٍ اختباراً عن الإحصاء: يُخفي
    /// نسخةً ثانيةً من تشغيلٍ محسوبٍ أصلاً. وإن سقط هذا الشرط، صار الثقب ثقباً في التغطية
    /// نفسها — فيسقط هذا الاختبار عندها، لا بعدها.
    /// (‏docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy)
    /// </para>
    /// </summary>
    [Fact]
    public void TheHoleThisGuardDoesNotCloseIsBoundedByOneSolutionWideTalliedJob()
    {
        const string SolutionWide = "--solution Babel.slnx";

        var dotnetSurfaces = Loaded.Value.Surfaces
            .Where(static surface => surface.Runner == "dotnet")
            .Select(static surface => surface.Id)
            .ToHashSet(StringComparer.Ordinal);

        var covering = Loaded.Value.Jobs
            .Where(static job => job.Classification == "tallied")
            .Where(job => JobBody(job.Workflow, job.Name).Contains(SolutionWide, StringComparison.Ordinal))
            .Where(job => dotnetSurfaces.IsSubsetOf(job.Surfaces.ToHashSet(StringComparer.Ordinal)))
            .ToList();

        Assert.True(
            covering.Count >= 1,
            "لا وظيفةَ `tallied` واحدة تُشغّل `" + SolutionWide + "` وتدّعي كلَّ أسطح .NET الـ"
                + dotnetSurfaces.Count + ". وهذه هي الدعامة التي تجعل ثقبَ «تنزيل التصنيف» ثقباً "
                + "في مراقبة نداءٍ مكرّر لا في التغطية: بدونها يستطيع تنزيلُ تصنيفٍ أن يُخفي "
                + "اختبارات لا نسخةً ثانية منها. · Without one solution-wide tallied job claiming "
                + "every .NET surface, declassifying a job hides tests rather than a duplicate run."
        );
    }

    // ── ٣ · الشاهد الإيجابي ───────────────────────────────────────────────────

    /// <summary>
    /// <b>الشاهد الإيجابي</b>، على شاكلة <c>TheSecretGuardCarriesItsOwnPositiveControl</c>:
    /// محلّلٌ توقّف عن التحليل يُعيد مجموعاتٍ فارغة، والفراغُ يُرضي <b>كل</b> تأكيدٍ أعلاه
    /// (فخ-43). فيُثبَت هنا أن القراءة قرأت، وأن السجلّ ليس فارغاً، وأن سكربت الحصيلة
    /// موجودٌ وقابلٌ للتنفيذ — فحارسٌ يستدعي ملفّاً غير موجود يُشبه الحارسَ العامل تماماً.
    /// </summary>
    [Fact]
    public void TheComputationIsNotVacuous()
    {
        var manifest = Loaded.Value;

        Assert.True(manifest.Surfaces.Count >= 20, "لم يُقرأ من السجلّ إلا " + manifest.Surfaces.Count + " سطحاً. · too few surfaces parsed.");
        Assert.True(manifest.Jobs.Count >= 8, "لم يُقرأ من السجلّ إلا " + manifest.Jobs.Count + " وظيفة. · too few jobs parsed.");
        Assert.True(JobsOnDisk().Count >= 8, "لم تُقرأ وظائف السير من القرص. · the workflow parser read nothing.");
        Assert.True(TestProjectsOnDisk().Count >= 20, "لم تُكتشف مشاريع الاختبار على القرص. · test-project discovery read nothing.");

        Assert.Contains(manifest.Jobs, static job => job.Classification == "tallied");
        Assert.Contains(manifest.Jobs, static job => job.Classification == "untallied");
        Assert.Contains(manifest.Surfaces, static surface => surface.Runner == "vitest");

        var markerless = manifest.Surfaces.Where(static s => s.InvokedBy.Count == 0).Select(static s => s.Id).ToList();
        Assert.True(
            markerless.Count == 0,
            "أسطحٌ بلا `invokedBy`: " + string.Join("، ", markerless) + " — فاختبار «وظيفةٌ تُشغّل "
                + "سطحاً تُحصيه» يمرّ عليها بلا أن يفحص شيئاً. · Surfaces with no invocation marker "
                + "make that assertion vacuous for them."
        );
        Assert.Contains(manifest.Surfaces, static surface => surface.Runner == "playwright");

        Assert.True(
            File.Exists(Path.Combine(RepositoryLayout.Root, TallyScript)),
            TallyScript + " غير موجود — وكل تأكيدات الحصيلة أعلاه تُشير إلى ملفٍّ لا وجود له، "
                + "وهو حارسٌ يبدو عاملاً وهو غائب. · The tally script itself is missing."
        );

        Assert.True(
            File.Exists(Path.Combine(RepositoryLayout.Root, "tools/test-tally/tally.mjs")),
            "tools/test-tally/tally.mjs غير موجود — والسكربت غلافٌ عليه. · The tally implementation is missing."
        );

        // والشاهد الأخير: مجموع الأرضيات المُعلَنة لأسطح .NET يجب أن يبقى مساوياً لما
        // تُعلنه البوّابة، فلا تنحدر الأرضيات واحدةً واحدةً بلا أن يلاحظ أحد المجموع.
        var total = manifest.Surfaces.Where(static s => s.Runner == "dotnet").Sum(static s => s.MinimumExecuted);
        Assert.True(
            total >= 1631,
            "مجموع أرضيات أسطح .NET صار " + total + " بعد أن كان 1631 — انحدارٌ في الأرضيات نفسها. "
                + "إن حُذفت اختبارات عمداً فاخفض الرقم هنا صراحةً. · The floors themselves regressed."
        );
    }

    /// <summary>
    /// <b>الأرضية مكتوبة في ثلاثة مواضع، فلتتّفق الثلاثة.</b> السجلّ يحملها لتقرأها
    /// الحصيلة، و<c>ci.yml</c> و<c>tools/gate/run.sh</c> يحملانها في
    /// <c>--minimum-expected-tests</c> لتُنفَّذ بنيويّاً بلا حارسٍ إطلاقاً. ورقمٌ يُرفَع
    /// في السجلّ ويُنسى في السير يجعل الطبقتين تحرسان عتبتين مختلفتين، وتُقرأ خُضرةُ
    /// إحداهما ضماناً للأخرى.
    /// <para>
    /// وهذا الفحص لا يُصنّف خياراً ولا يمنعه — يقارن <b>رقمين</b>، فليس فيه قائمةٌ
    /// تُهزَم ببندٍ لم يخطر لكاتبها.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCommittedFloorIsTheSameNumberInTheManifestTheWorkflowAndTheGate()
    {
        var dotnet = Loaded.Value.Surfaces.Where(static s => s.Runner == "dotnet").ToList();
        var solutionFloor = dotnet.Sum(static s => s.MinimumExecuted);
        var architectureFloor = dotnet.Single(static s => s.Id == "dotnet-architecture").MinimumExecuted;

        foreach (var file in new[] { ".github/workflows/ci.yml", "tools/gate/run.sh" })
        {
            var text = File.ReadAllText(Path.Combine(RepositoryLayout.Root, file));

            foreach (var (floor, what) in new[]
                     {
                         (solutionFloor, "مجموع أسطح .NET (‏--solution Babel.slnx)"),
                         (architectureFloor, "اختبارات المعمارية وحدها"),
                     })
            {
                Assert.True(
                    text.Contains("--minimum-expected-tests " + floor.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                    file + " لا يحمل `--minimum-expected-tests " + floor + "` — وهي أرضية «" + what
                        + "» كما يُعلنها " + ManifestPath + ". فالطبقة البنيوية والحصيلة تحرسان "
                        + "عتبتين مختلفتين، وتُقرأ خُضرةُ إحداهما ضماناً للأخرى. · The structural "
                        + "floor and the tally floor have drifted apart."
                );
            }
        }
    }

    // ── قراءة السير والسجلّ ───────────────────────────────────────────────────
    // نصّاً لا بمكتبة YAML، على شاكلة القواعد 15 و16 و17: الحارس يقرأ ما يقرأه
    // الإنسان في المراجعة، ويبقى يعمل إن تغيّرت حزمة.

    private sealed record SurfaceEntry(string Id, string Runner, string Project, string Config, string Report, IReadOnlyList<string> InvokedBy, int MinimumExecuted);

    private sealed record JobEntry(string Workflow, string Name, string Classification, string Why, IReadOnlyList<string> Surfaces);

    private sealed record Manifest(
        IReadOnlyList<SurfaceEntry> Surfaces,
        IReadOnlyList<JobEntry> Jobs,
        IReadOnlyList<string> NonRunnerConfigs)
    {
        public static Manifest Load()
        {
            var path = Path.Combine(RepositoryLayout.Root, ManifestPath);
            Assert.True(File.Exists(path), ManifestPath + " غير موجود — وهو مرجع التصنيف كلّه. · missing manifest.");

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            var surfaces = root.GetProperty("surfaces").EnumerateArray().Select(static element => new SurfaceEntry(
                Text(element, "id"),
                Text(element, "runner"),
                Text(element, "project"),
                Text(element, "config"),
                Text(element, "report"),
                element.TryGetProperty("invokedBy", out var markers)
                    ? markers.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToList()
                    : [],
                element.TryGetProperty("minimumExecuted", out var floor) ? floor.GetInt32() : 0)).ToList();

            var jobs = root.GetProperty("jobs").EnumerateArray().Select(static element => new JobEntry(
                Text(element, "workflow"),
                Text(element, "job"),
                Text(element, "classification"),
                Text(element, "why"),
                element.TryGetProperty("surfaces", out var list)
                    ? list.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToList()
                    : [])).ToList();

            var configs = root.TryGetProperty("webConfigsThatRunNoTests", out var declared)
                ? declared.EnumerateArray().Select(static element => Text(element, "path")).ToList()
                : [];

            return new Manifest(surfaces, jobs, configs);
        }

        private static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>كل وظيفة في كل ملفّ سير على القرص — اكتشافاً لا قائمةً.</summary>
    private static List<string> JobsOnDisk()
    {
        var folder = Path.Combine(RepositoryLayout.Root, WorkflowFolder);
        Assert.True(Directory.Exists(folder), WorkflowFolder + " غير موجود. · missing workflow folder.");

        var jobs = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.yml").Concat(Directory.EnumerateFiles(folder, "*.yaml")).Order(StringComparer.Ordinal))
        {
            var relative = WorkflowFolder + "/" + Path.GetFileName(file);
            foreach (var name in JobNames(File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal)))
            {
                jobs.Add(relative + ":" + name);
            }
        }

        return jobs;
    }

    private static IEnumerable<string> JobNames(string text)
    {
        var inJobs = false;

        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("jobs:", StringComparison.Ordinal)) { inJobs = true; continue; }
            if (!inJobs) continue;

            // انتهت خريطة الوظائف عند أول مفتاحٍ في العمود صفر.
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith('#')) break;

            var match = Regex.Match(line, @"^  ([A-Za-z][A-Za-z0-9_-]*):\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));
            if (match.Success) yield return match.Groups[1].Value;
        }
    }

    private static (string Workflow, string Name) Split(string qualified)
    {
        var cut = qualified.LastIndexOf(':');
        return (qualified[..cut], qualified[(cut + 1)..]);
    }

    private static string JobBody(string workflow, string job)
    {
        var path = Path.Combine(RepositoryLayout.Root, workflow);
        Assert.True(File.Exists(path), workflow + " غير موجود. · missing workflow.");

        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n');
        var body = new List<string>();
        var inside = false;
        var inJobs = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("jobs:", StringComparison.Ordinal)) { inJobs = true; continue; }
            if (!inJobs) continue;

            var match = Regex.Match(line, @"^  ([A-Za-z][A-Za-z0-9_-]*):\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));
            if (match.Success)
            {
                if (inside) break;
                inside = match.Groups[1].Value == job;
                continue;
            }

            if (inside) body.Add(line);
        }

        Assert.True(body.Count > 0, "لم تُقرأ وظيفة " + job + " من " + workflow + ". · job body not parsed.");
        return string.Join("\n", body);
    }

    // ── ٦ · الحصيلة لا تُخصى بالصَّدَفة ────────────────────────────────────────

    /// <summary>
    /// <b>خطوةٌ مقروءةٌ بنيةً لا نصّاً.</b> ما يهمّ في خطوة الحصيلة أربعة أشياء لا خامس
    /// لها: شرطُها، وصَدَفتُها، وهل يُسمح لها بالسقوط، و<b>ما الأمر الذي يُنهي تنفيذها</b>
    /// — لأن حالةَ الخروج التي تصل الوظيفة هي حالةُ آخر أمرٍ في <c>run</c>، لا حالةُ
    /// الأمر الذي كتبه المؤلّف في ذهنه.
    /// </summary>
    private sealed record ShellStep(
        string Text,
        string? If,
        string? Shell,
        bool ContinueOnError,
        IReadOnlyList<string> Commands)
    {
        /// <summary>آخر أمرٍ فعليّ — وهو وحده من تصل حالتُه إلى الوظيفة.</summary>
        public string LastCommand => Commands.Count == 0 ? string.Empty : Commands[^1];
    }

    /// <summary>
    /// يقرأ خطوةً واحدة من نصّها: المفاتيح عند العمق 8، و<c>run</c> قد تكون قيمةً في
    /// السطر نفسه أو كتلةً <c>|</c> أو مطويّة <c>&gt;-</c>. والتعليقات تُحذف قبل الحكم،
    /// لأن سطرَ تعليقٍ يحمل نصّ الأمر كان يُرضي بحثاً نصّياً بعد <b>حذف الخطوة نفسها</b>.
    /// </summary>
    private static ShellStep ParseStep(string stepText)
    {
        var raw = stepText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (raw.Count > 0)
        {
            // «      - name: x» ⇒ «        name: x»: أوّلُ مفتاحٍ يعيش على شرطة القائمة.
            var head = raw[0];
            var dash = head.IndexOf("- ", StringComparison.Ordinal);
            if (dash >= 0) raw[0] = new string(' ', dash + 2) + head[(dash + 2)..];
        }

        string? ifExpr = null;
        string? shell = null;
        var continueOnError = false;
        var commands = new List<string>();

        for (var i = 0; i < raw.Count; i++)
        {
            var line = raw[i];
            if (line.Trim().Length == 0) continue;

            var indent = line.Length - line.TrimStart().Length;
            if (indent != 8) continue;

            var key = Regex.Match(line.Trim(), @"^([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$", RegexOptions.None, TimeSpan.FromSeconds(5));
            if (!key.Success) continue;

            var name = key.Groups[1].Value;
            var inline = key.Groups[2].Value.Trim();

            switch (name)
            {
                case "if":
                    ifExpr = inline;
                    break;
                case "shell":
                    shell = inline;
                    break;
                case "continue-on-error":
                    continueOnError = !string.Equals(inline, "false", StringComparison.OrdinalIgnoreCase);
                    break;
                case "run":
                    commands.AddRange(ReadRun(raw, i, inline));
                    break;
                default:
                    break;
            }
        }

        return new ShellStep(stepText, ifExpr, shell, continueOnError, commands);
    }

    /// <summary>
    /// يقرأ قيمة <c>run</c>: قيمةٌ في السطر نفسه، أو كتلةٌ حرفية <c>|</c> (كل سطرٍ أمر)،
    /// أو مطويّة <c>&gt;</c> (‏الأسطر تُوصَل أمراً واحداً). ويُخرج <b>الأوامر</b> بلا
    /// تعليقات وبلا أسطر فارغة، وبعد وصل أسطر الاستمرار <c>\</c>.
    /// </summary>
    private static List<string> ReadRun(List<string> raw, int at, string inline)
    {
        var folded = inline.StartsWith('>');
        var literal = inline.StartsWith('|');
        var body = new List<string>();

        if (!folded && !literal && inline.Length > 0) body.Add(inline);

        for (var i = at + 1; i < raw.Count; i++)
        {
            var line = raw[i];
            if (line.Trim().Length == 0) { body.Add(string.Empty); continue; }
            var indent = line.Length - line.TrimStart().Length;
            if (indent <= 8) break;
            body.Add(line.Trim());
        }

        // التعليقات تُحذف: سطرُ تعليقٍ يحمل نصّ الأمر ليس أمراً.
        var cleaned = body.Where(static l => !l.StartsWith('#')).ToList();

        // وصلُ أسطر الاستمرار: «foo \» ثم «--bar» أمرٌ واحد.
        var joined = new List<string>();
        var buffer = string.Empty;
        foreach (var line in cleaned)
        {
            var piece = line;
            var continues = piece.EndsWith('\\');
            if (continues) piece = piece[..^1].TrimEnd();
            buffer = buffer.Length == 0 ? piece : buffer + " " + piece;
            if (continues) continue;
            joined.Add(buffer);
            buffer = string.Empty;
        }

        if (buffer.Length > 0) joined.Add(buffer);

        // المطويّة والقيمة السطرية أمرٌ واحد مهما تعدّدت أسطرها.
        if (folded || (!literal && inline.Length > 0))
        {
            var one = string.Join(" ", joined.Where(static l => l.Length > 0));
            return one.Length == 0 ? [] : [one];
        }

        return [.. joined.Where(static l => l.Length > 0)];
    }

    /// <summary>
    /// <b>هل تصل حالةُ خروج هذا الاستدعاء إلى الوظيفة؟</b>
    /// <para>
    /// الشرط: <c>run</c> فيها <b>أمرٌ واحد لا غير</b>، وهو الاستدعاء، <b>ولا عاملَ صَدَفةٍ
    /// حوله</b>. فما بعد الأمر الأخير هو ما تقرأه الوظيفة: ‏<c>|| true</c> تجعل الأخيرَ
    /// <c>true</c>، و<c>; true</c> كذلك، و<c>|| echo skip</c> كذلك، و<c>&amp;</c> تُلقي
    /// الأمر في الخلفية فتُصفّر حالته، و<c>|</c> تُسلّم الحالة لآخر حلقةٍ في الأنبوب.
    /// </para>
    /// <para>
    /// <b>والعطل الذي أغلقه شرطُ «أمرٌ واحد»، مقيساً:</b> كان الحكم يقرأ <b>آخر أمرٍ</b>
    /// وحده، فيمرّ كلُّ ما يسبقه في الكتلة نفسها. وذلك ليس فراغاً نظرياً — سطرٌ واحد
    /// قبل الاستدعاء يكفي، بطريقتين مختلفتين جذرياً:
    /// <list type="number">
    ///   <item><b>يُلغي حالة الخروج بعد وقوعها:</b> ‏<c>trap 'exit 0' EXIT</c> ثم
    ///         الاستدعاء. الصَّدَفة الافتراضية على GitHub هي <c>bash -e</c>، فتسقط عند
    ///         الاستدعاء الفاشل ثم <b>يُشغَّل الشرَك فيخرج بصفر</b>. مقيس: القاعدة 20
    ///         <c>total 15 · failed 0</c> وحصيلةٌ ساقطة.</item>
    ///   <item><b>يُتلف الشيءَ المُستدعى قبل استدعائه:</b> ‏<c>echo "exit 0" &gt;
    ///         tools/test-tally/run.sh</c> ثم الاستدعاء. الاستدعاء نفسه سليمُ الشكل تماماً
    ///         وحالتُه تصل الوظيفة فعلاً — لكنها حالةُ نصٍّ آخر. مقيس: <c>15/15</c> خضراء.</item>
    /// </list>
    /// <b>ولا تُعدّ هاتان في قائمةٍ</b>: بينهما وحدهما شرَكٌ ودالّةٌ وملفّ، وما بعدهما
    /// <c>PATH</c> ودالّةُ صَدَفةٍ تحمل اسم السكربت ومتغيّرُ بيئةٍ يقرؤه. القائمة مفتوحة
    /// أبداً؛ والخاصّية المغلقة واحدة: <b>الخطوة لا تفعل شيئاً سوى الاستدعاء</b>. فما لا
    /// يوجد لا يُخصي، و<c>set +e</c> تسقط من الحساب لأنها لا يجوز أن توجد أصلاً.
    /// </para>
    /// <para>
    /// <b>وثمنُه مقيس ومدفوع:</b> خطواتُ الحصيلة الثلاث في المستودع — <c>ci.yml</c>
    /// و<c>data-validation.yml</c> و<c>web.yml</c> — كلُّها <c>run:</c> بسطرٍ واحد، فلا
    /// تدفع هذه القاعدة شيئاً اليوم. ومن احتاج غداً تهيئةً قبل الحصيلة يكتبها
    /// <b>في خطوةٍ سابقة</b>، وهو ما يجعل حالتَها تصل الوظيفة هي الأخرى.
    /// </para>
    /// </summary>
    private static bool StatusReachesTheJob(ShellStep step, string invocation)
    {
        // ‏**أمرٌ واحد لا غير.** أيُّ أمرٍ سابقٍ في الكتلة نفسها يستطيع أن يُلغي حالة
        // الخروج (شرَك) أو أن يُبدّل ما يُستدعى (كتابةٌ فوق السكربت، دالّة، PATH) —
        // ولا يُعرف عددُ الطرق فتُعدّ، فتُمنع الكتلةُ متعدّدةُ الأوامر من أصلها.
        if (step.Commands.Count != 1) return false;

        var last = step.LastCommand.Trim();
        if (!last.Contains(invocation, StringComparison.Ordinal)) return false;

        // لا عامل صَدَفةٍ في الأمر الأخير: لا وصلَ ولا فصلَ ولا أنبوبَ ولا خلفيةَ ولا
        // استبدالَ أمر. والاستدعاء يبدأ السطر، فلا شيء قبله يُلغي تشغيله.
        if (!last.StartsWith(invocation, StringComparison.Ordinal)) return false;

        return last.IndexOfAny(['|', '&', ';', '`', '(', ')', '\n']) < 0
            && !last.Contains("$(", StringComparison.Ordinal);
    }

    /// <summary>كل خطوات وظيفةٍ، مقروءةً بنيةً.</summary>
    private static IReadOnlyList<ShellStep> StepsOf(string workflow, string job)
    {
        var blocks = new List<List<string>>();
        List<string>? block = null;

        foreach (var line in JobBody(workflow, job).Split('\n'))
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
        return [.. blocks.Select(b => ParseStep(string.Join("\n", b)))];
    }

    /// <summary>خطوةُ الحصيلة — تُعرَف بأمرٍ **مقروء** يحمل مُحدِّد وظيفتها، لا بنصٍّ في الملفّ.</summary>
    private static ShellStep TallyStepOf(string workflow, string job, string selector)
    {
        var found = StepsOf(workflow, job)
            .FirstOrDefault(s => s.Commands.Any(c => c.Contains(TallyScript + " " + selector, StringComparison.Ordinal)));

        Assert.True(
            found is not null,
            "لا خطوةَ حصيلةٍ **مُنفَّذة** في " + workflow + ":" + job + " تحمل «" + TallyScript + " "
                + selector + "». وسطرُ تعليقٍ يحمل النصّ ليس خطوة. · no executed tally step."
        );

        return found!;
    }


    /// <summary>
    /// مشاريع الاختبار على القرص — <b>مشتقّةً من شرط البناء نفسه</b>، لا من قائمة.
    /// و<c>spikes/</c> خارج النطاق كما في القاعدتين 8 و9.
    /// </summary>
    private static List<string> TestProjectsOnDisk() =>
        [.. RepositoryLayout.AllProjectFilesOnDisk
            .Where(static path => !path.StartsWith("spikes/", StringComparison.Ordinal))
            .Where(static path => Path.GetFileNameWithoutExtension(path).EndsWith("Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
}
