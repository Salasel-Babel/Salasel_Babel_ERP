using System.Globalization;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارسٌ لا يُثبَت أنه ينطق لا يُفرَّق عن حارس معطَّل.</b>
/// <para>
/// خطوة «التحقق من عدم وجود أسرار في التغييرات» في <c>ci.yml</c> هي آخر خطّ دفاع في
/// مستودعٍ <b>سرَّب فعلاً</b> رمز وصول شخصياً من GitHub فوجب إبطاله، وكشف كلمة مرور جذرِ
/// خادم في لقطة شاشة فوجب تدويرها. وقد كانت — إلى ما قبل هذا الملف — تجد <b>صفراً على
/// كل تشغيل</b>، وتُعفي <c>docs/**</c> و<c>spikes/**</c> و<c>**/ci.yml</c> بالجملة. فكان
/// عندها جوابان لا واحد على السؤال «هل من سرّ؟»: «لا سرّ» و«لا فحص» — ولا شيء في المخرَج
/// يميّزهما.
/// </para>
/// <para>
/// <b>فالعلاج شاهدٌ موجب مُودَع:</b> <c>tools/secret-scan/positive-control.txt</c> يحمل
/// أشكال أسرارٍ حقيقية الشكل مصطنعة القيمة، و<c>run.sh</c> يُطابق عليه <b>كل</b> نمط قبل
/// أن يمسح المستودع. وهذا الملفّ يفرض العقد نفسه <b>داخل مجموعة الاختبارات</b>، فلا يُهزَم
/// بتعديل في السير وحده: النمط الذي لا يطابق الشاهد يُفشل البناء هنا أيضاً.
/// </para>
/// <para>
/// (‏<c>docs/evidence/traps.md#fakh-a-guard-that-never-fires-cannot-be-told-from-a-broken-one</c>)
/// </para>
/// </summary>
public sealed class TheSecretGuardCarriesItsOwnPositiveControl
{
    private const string CiWorkflow = ".github/workflows/ci.yml";
    private const string Scanner = "tools/secret-scan/run.sh";
    private const string PatternsFile = "tools/secret-scan/patterns.txt";
    private const string PositiveControl = "tools/secret-scan/positive-control.txt";
    private const string GuardStepName = "التحقق من عدم وجود أسرار في التغييرات";

    /// <summary>وسم الاستثناء على مستوى السطر — مُميِّز لا إعفاء.</summary>
    private const string ExemptionMarker = "NOT-A-SECRET";

    /// <summary>
    /// حدٌّ أدنى لعدد الأنماط. ملفُّ أنماطٍ صار فارغاً يجعل كل تأكيد أدناه يمرّ على
    /// مجموعة فارغة — وهو عين العطل الذي يمنعه هذا الملف.
    /// </summary>
    private const int MinimumPatterns = 8;

    /// <summary>
    /// إعفاءات الأدلّة التي أُزيلت. <b>عودةُ أيٍّ منها تُفشل البناء</b>: هي بالضبط ما جعل
    /// مفتاحاً خاصّاً يُودَع تحت <c>docs/</c> غير مرئي للحارس.
    /// </summary>
    private static readonly string[] ForbiddenDirectoryExemptions =
    [
        "':!docs/**'",
        "':!spikes/**'",
        "':!**/ci.yml'",
        "\":!docs/**\"",
        "\":!spikes/**\"",
        "\":!**/ci.yml\"",
    ];

    /// <summary>خطوة الحارس في السير تُشغّل الماسح فعلاً — لا نصّاً آخر يحمل الاسم نفسه.</summary>
    [Fact]
    public void TheWorkflowStepActuallyRunsTheScanner()
    {
        string workflow = Read(CiWorkflow);

        Assert.True(
            workflow.Contains(GuardStepName, StringComparison.Ordinal),
            $"خطوة «{GuardStepName}» اختفت من {CiWorkflow} — آخر خطّ دفاع ضدّ إيداع سرّ. · "
            + "The secret guard step vanished from the CI workflow.");

        int at = workflow.IndexOf(GuardStepName, StringComparison.Ordinal);
        string tail = workflow[at..];
        int next = tail.IndexOf("\n      - name:", StringComparison.Ordinal);
        string body = next < 0 ? tail : tail[..next];

        Assert.True(
            body.Contains(Scanner, StringComparison.Ordinal),
            $"خطوة «{GuardStepName}» لم تعد تُشغّل {Scanner} — فالاسم باقٍ والفحص ذهب، ومعه "
            + "الشاهد الموجب الذي يُثبت أن الحارس ينطق. · The guard step no longer invokes the "
            + "scanner, so its self-test is gone with it.");
    }

    /// <summary>
    /// <b>الشاهد الموجب.</b> كل نمط مُعلَن يطابق شيئاً في ملف الشاهد. نمطٌ لا يطابقه
    /// نمطٌ لا نعرف أنه يعمل — و«لم يُعثر على شيء» بعده ليست براءة.
    /// </summary>
    [Fact]
    public void EveryDeclaredPatternFiresOnThePositiveControl()
    {
        var patterns = Patterns();
        string fixture = Read(PositiveControl);

        Assert.True(
            patterns.Count >= MinimumPatterns,
            $"‏{PatternsFile} يحمل {patterns.Count.ToString(CultureInfo.InvariantCulture)} نمطاً "
            + $"والحدّ الأدنى {MinimumPatterns.ToString(CultureInfo.InvariantCulture)} — المسح ضامر "
            + "وكل تأكيد بعده يمرّ على مجموعة فارغة. · The pattern file is vacuous.");

        List<string> silent = [];

        foreach ((string name, string pattern) in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            if (!regex.IsMatch(fixture))
            {
                silent.Add(name);
            }
        }

        Assert.True(
            silent.Count == 0,
            "أنماطٌ في " + PatternsFile + " لا تطابق شيئاً في الشاهد الموجب " + PositiveControl
            + "، فلا دليل على أنها تلتقط ما تدّعي التقاطه: " + string.Join("، ", silent)
            + ". أضِف إلى الشاهد سطراً بشكل ما تحرسه، أو أصلح النمط. · Declared patterns match "
            + "nothing in the positive control, so nothing proves they can fire: "
            + string.Join(", ", silent) + ".");
    }

    /// <summary>
    /// الشاهد <b>لا يحمل وسم الاستثناء</b>. لو حمله لخرج من المسح الحقيقي ومن اختباره
    /// الذاتي معاً — فيصير شاهداً لا يشهد، وهي أخطر حالة: خُضرةٌ تبدو مُبرهَنة وليست.
    /// </summary>
    [Fact]
    public void ThePositiveControlDoesNotExemptItself()
    {
        Assert.DoesNotContain(ExemptionMarker, Read(PositiveControl), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>لا إعفاء دليلٍ في الماسح.</b> الاستثناء الوحيد المسموح مسارٌ كامل واحد هو
    /// الشاهد نفسه. إعفاء الدليل يغطّي <b>ما لم يُكتب بعد</b> ولا يظهر في أي فرق.
    /// </summary>
    [Fact]
    public void TheScannerExemptsOneExactPathAndNoDirectoryGlob()
    {
        string scanner = Read(Scanner);

        foreach (string exemption in ForbiddenDirectoryExemptions)
        {
            Assert.False(
                scanner.Contains(exemption, StringComparison.Ordinal),
                $"عاد إعفاء الدليل {exemption} إلى {Scanner}. إعفاءُ دليلٍ كامل بقعةٌ عمياء: "
                + "مفتاحٌ خاصّ يُودَع تحته لا يراه الحارس أصلاً. المُميِّز على مستوى السطر هو "
                + $"البديل («{ExemptionMarker}»). · A directory-wide exemption came back.");
        }

        Assert.True(
            scanner.Contains("\":!$FIXTURE\"", StringComparison.Ordinal),
            $"‏{Scanner} لم يعد يستثني الشاهد بمساره الكامل وحده — فإمّا صار الشاهد نفسه "
            + "مخالفةً دائمة، وإمّا اتّسع الاستثناء. · The single exact-path exemption is gone.");
    }

    /// <summary>
    /// الشاهد وملفّ الأنماط والماسح <b>موجودة</b>، والشاهد يحمل أكثر من كتلة مفتاح خاص
    /// واحدة — حارس لافراغ على الشاهد نفسه.
    /// </summary>
    [Fact]
    public void ThePositiveControlIsNotEmpty()
    {
        string fixture = Read(PositiveControl);

        int privateKeyBlocks = Regex.Count(
            fixture,
            "-----BEGIN ([A-Z0-9 ]+ )?PRIVATE KEY( BLOCK)?-----",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(
            privateKeyBlocks >= 2,
            $"الشاهد الموجب {PositiveControl} يحمل "
            + privateKeyBlocks.ToString(CultureInfo.InvariantCulture)
            + " كتلة مفتاح خاص — شاهدٌ ضامر لا يُثبت شيئاً. · The positive control has withered.");
    }

    /// <summary>
    /// قراءة الأنماط بنفس قاعدة <c>run.sh</c>: أول سطر تعليق في الفقرة اسمٌ، والسطر
    /// غير الفارغ غير التعليقي نمطٌ.
    /// </summary>
    private static List<(string Name, string Pattern)> Patterns()
    {
        List<(string Name, string Pattern)> result = [];
        string name = string.Empty;

        foreach (string raw in Read(PatternsFile).Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (line.Length == 0)
            {
                name = string.Empty;
            }
            else if (line[0] == '#')
            {
                if (name.Length == 0)
                {
                    name = line.TrimStart('#').Trim();
                }
            }
            else
            {
                result.Add((name, line));
                name = string.Empty;
            }
        }

        return result;
    }

    private static string Read(string relative)
    {
        string path = Path.Combine(RepositoryLayout.Root, relative);
        Assert.True(File.Exists(path), $"{relative} غير موجود — وهو جزء من حارس الأسرار.");
        return File.ReadAllText(path);
    }
}
