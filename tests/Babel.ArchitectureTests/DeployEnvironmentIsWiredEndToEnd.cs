using System.Globalization;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>ثلاث قوائم يجب أن تتّفق، ولم يكن شيء يفحص اتّفاقها.</b>
/// <para>
/// حزمة النشر تقرأ إعدادها من ملفّ <c>.env</c> يُكتب على الخادم لحظة النشر. وهذا يعني
/// ثلاث قوائم منفصلة عن الشيء نفسه: <b>ما يطلبه</b> <c>deploy/compose.yml</c>، و<b>ما
/// يكتبه</b> كاتبو <c>.env</c> الثلاثة (سير عمل النشر، وحارس الحزمة، و<c>up.sh</c>)،
/// و<b>ما توثّقه</b> <c>deploy/README.md</c>. وثلاثتها تُحرَّر بأيدٍ مختلفة في أوقات
/// مختلفة، ولا شيء كان يقارنها.
/// </para>
/// <para>
/// <b>والعطل الذي أوجد هذا الحارس وقع فعلاً ومُقيس:</b> أُضيف
/// <c>BABEL_STORAGE_TICKET_KEY</c> إلى <c>compose.yml</c> بصيغة <c>${…:?}</c>، وإلى جدول
/// الأسرار الإلزامية في <c>README.md</c> — <b>ولم يُضَف إلى واحد من الكتّاب الثلاثة</b>.
/// فصار: سرٌّ موثَّق، ومطلوبٌ بالبناء، ولا يبلغ الخادم أبداً. وأثره بالترتيب:
/// </para>
/// <list type="number">
///   <item><description>
///     <c>deploy-check.yml</c> يتوقّف عند <b>أول خطوة</b> — <c>compose config</c> يرفض
///     المتغيّر الناقص — فيصير حارسُ الحزمة كلّه أحمر، <b>ولا يبلغ شيئاً ممّا بعده</b>.
///   </description></item>
///   <item><description>
///     وبما أنه أحمر، لم يعد يمسك ما كان يمسكه: نزلت وحدة الموارد البشرية بمتغيّر
///     <c>BABEL_HR_DB</c> <b>يرفض الخادم الإقلاع بدونه</b> — ولم يكن في <c>compose.yml</c>
///     إطلاقاً. حارسٌ أحمر لا يحرس؛ وحُمرتُه صارت خلفيةً تُقرأ «معروف».
///   </description></item>
///   <item><description>
///     وأوّل نشرة حقيقية كانت ستتوقّف عند <c>compose up</c> برسالة عربية تسمّي المتغيّر —
///     وهو أفضل ما في الأمر، لأن الحالة الأخرى (متغيّرٌ له <c>:-</c> صامت) <b>لا تتوقّف
///     أصلاً</b> بل تنشر شيئاً آخر بهدوء.
///   </description></item>
/// </list>
/// <para>
/// <b>ولماذا هنا لا في التكامل المستمر:</b> <c>deploy-check.yml</c> يحتاج عفريت حاويات
/// وخمس عشرة دقيقة، ولا يعمل في البوّابة المحلية أصلاً. وهذا الحارس نصوصٌ تُقرأ: ثوانٍ،
/// وفي كل تشغيل بوّابة. والفرق بينهما هو الفرق بين «يُكتشف قبل الدفع» و«يُكتشف بعده».
/// </para>
/// </summary>
public sealed class DeployEnvironmentIsWiredEndToEnd
{
    /// <summary>ملفّ الحزمة — مصدر قائمة «ما هو مطلوب».</summary>
    private const string ComposePath = "deploy/compose.yml";

    /// <summary>إعداد الحافة — يقرأ متغيّرات البيئة بصيغته الخاصّة.</summary>
    private const string CaddyPath = "deploy/Caddyfile";

    /// <summary>جدول الأسرار — مصدر قائمة «ما هو موثَّق».</summary>
    private const string ReadmePath = "deploy/README.md";

    private const string DeployWorkflowPath = ".github/workflows/deploy.yml";

    /// <summary>
    /// كتّاب ملفّ <c>.env</c> الذي يقرؤه <c>compose</c>: ثلاثة، وكلّهم يجب أن يكتبوا
    /// كل ما يطلبه الملفّ. و<b>الثلاثة لا واحد</b>: العطل الأصلي أصاب ثلاثتهم معاً،
    /// وحارسٌ يفحص سير عمل النشر وحده كان سيُبقي الحزمة المحلية وحارس التكامل مكسورين.
    /// </summary>
    private static readonly EnvironmentFileWriter[] Writers =
    [
        new("سير عمل النشر", DeployWorkflowPath, "      - name: كتابة ملف البيئة على الخادم", "      - name: "),
        new("حارس حزمة النشر", ".github/workflows/deploy-check.yml", "          umask 077", "          } > deploy/.env"),
        new("‏deploy/up.sh (وضع الحاويات)", "deploy/up.sh", "  cat > \"$compose_env\" <<EOF", "\nEOF"),
    ];

    private static readonly Lazy<ComposeReferences> Compose = new(ReadCompose);
    private static readonly Lazy<DeployStep> Step = new(ReadDeployStep);

    /// <summary>
    /// <b>١ · ما يطلبه <c>compose</c> بـ<c>:?</c> يكتبه كل كاتب.</b>
    /// <para>
    /// هذه هي طبقة «لا إقلاع»: متغيّرٌ مطلوب بهذه الصيغة وغائبٌ عن <c>.env</c> يوقف
    /// <c>compose up</c> برسالته العربية <b>بعد</b> أن تُبنى الصور وتُسحب وتُنقل الملفّات —
    /// أي أن ثمن الاكتشاف دورةُ نشر كاملة إلى خادمٍ حيّ.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryVariableComposeRefusesToStartWithoutIsWrittenByEveryEnvironmentFileWriter()
    {
        List<string> offenders = [];

        foreach (EnvironmentFileWriter writer in Writers)
        {
            HashSet<string> written = writer.Written();

            foreach (string required in Compose.Value.Required.Order(StringComparer.Ordinal))
            {
                if (!written.Contains(required))
                {
                    offenders.Add(
                        "‏" + required + " — يطلبه " + ComposePath + " بـ${…:?} ولا يكتبه «"
                        + writer.Label + "» (" + writer.Path + ")");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} متغيّراً مطلوباً لا يبلغ ملفّ البيئة:\n")
            + string.Join('\n', offenders)
            + "\n\nوأثرُ كلٍّ منها واحد: `compose up` يتوقّف برسالة تسمّي المتغيّر — على الخادم،\n"
            + "بعد أن بُنيت الصور وسُحبت. أضِف السطر إلى الكاتب المذكور بالشكل نفسه الذي\n"
            + "تُكتب به بقيّة السطور، ولا تُعطِه افتراضاً: الافتراض يجعل العطل صامتاً بدل أن يُعلَن.");
    }

    /// <summary>
    /// <b>٢ · وكل سرٍّ أو متغيّرٍ يقرؤه سير عمل النشر من مخزن الأسرار يصل فعلاً إلى <c>.env</c>.</b>
    /// <para>
    /// <b>وهذه هي الطبقة الخطرة، لا سابقتها.</b> متغيّرٌ مطلوب بـ<c>:?</c> وغائبٌ
    /// <b>يوقف</b> النشر؛ أمّا متغيّرٌ له <c>:-</c> صامت — و<c>BABEL_SITE</c> منها،
    /// افتراضُه <c>:80</c> — فيغيب بلا صوت ويُنشَر شيءٌ آخر: مالكٌ ضبط نطاقه في السرّ،
    /// وسطرُ كتابته سقط، فيُعرَض المنتج على HTTP عارٍ أمام صاحب القرار بينما كل شيء أخضر.
    /// فالقاعدة: سرٌّ يُقرأ في <c>env:</c> ولا يُكتب في <c>.env</c> إمّا سطرٌ ناقص وإمّا
    /// سرٌّ ميّت — وكلاهما يُصلَح، ولا يُسكَت عنه.
    /// </para>
    /// </summary>
    [Fact]
    public void EverySecretTheDeployWorkflowReadsIsActuallyWrittenIntoTheEnvironmentFile()
    {
        HashSet<string> written = Writers[0].Written();

        // ‏**والنطاق هو ما تقرؤه الحزمة وحده.** أسرارُ النقل — المضيف والمستخدم والمنفذ
        // والمسار — تُقرأ في الخطوة نفسها ولا مكان لها في `.env`: هي تخصّ الوصول إلى
        // الخادم لا إعداد ما يعمل عليه، **ولا يجوز أن تُكتب فيه**.
        List<string> offenders =
        [
            .. Step.Value.FromSecretStore
                .Where(name => Compose.Value.All.Contains(name))
                .Where(name => !written.Contains(name))
                .Order(StringComparer.Ordinal)
                .Select(static name => "‏" + name + " — تقرؤه الحزمة، ويُقرأ في env: من مخزن الأسرار، ولا يُكتب في .env"),
        ];

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} قيمةً يقرؤها سير عمل النشر ولا يُرسلها:\n")
            + string.Join('\n', offenders)
            + "\n\nوالقيمة التي لا تصل لا تُعلن غيابها إن كان لها ‎:-‎ في compose: تُنشَر النشرة\n"
            + "بالافتراض بهدوء. أضِف سطر echo داخل الكتلة التي تُمرَّر إلى ssh بالمدخل القياسي —\n"
            + "**ولا تضعها في وسيط ssh ولا في echo خارج الكتلة**: قيمةٌ في سطر أمر تظهر في ps\n"
            + "على الخادم وقد تظهر في سجلّ سير العمل.");
    }

    /// <summary>
    /// <b>٣ · ولا سطر ميّت: ما يُكتب في <c>.env</c> يقرؤه أحد.</b>
    /// <para>
    /// سطرٌ يكتب متغيّراً لا يذكره <c>compose.yml</c> ولا <c>Caddyfile</c> يضلّل من يقرأ:
    /// يبدو أن الشيء مضبوط وهو لا يبلغ حاويةً واحدة. وهو الوجه الآخر للعطل نفسه — إعادةُ
    /// تسميةٍ في <c>compose</c> تترك السطر القديم يعمل بلا أثر.
    /// </para>
    /// </summary>
    [Fact]
    public void NoEnvironmentFileLineIsWrittenForAVariableNothingReads()
    {
        List<string> offenders = [];

        foreach (EnvironmentFileWriter writer in Writers)
        {
            foreach (string written in writer.Written().Order(StringComparer.Ordinal))
            {
                if (!Compose.Value.All.Contains(written))
                {
                    offenders.Add("‏" + written + " — يكتبه «" + writer.Label + "» ولا يقرؤه " + ComposePath + " ولا " + CaddyPath);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} سطراً ميّتاً في ملفّ البيئة:\n")
            + string.Join('\n', offenders)
            + "\n\nإمّا أن يُقرأ السطر أو يُحذف. إعدادٌ يبدو مضبوطاً ولا يبلغ شيئاً أسوأ من\n"
            + "إعدادٍ غائب، لأن الغائب يُبحث عنه والميّت يُطمأنّ إليه.");
    }

    /// <summary>
    /// <b>٤ · وكل متغيّر يقرؤه <c>compose</c> موثَّق — ومعه افتراضُه إن كان له افتراض.</b>
    /// <para>
    /// <b>وهذه القاعدة موجّهة إلى <c>:-</c> بالذات.</b> متغيّرٌ بافتراضٍ صامت لا يوقف شيئاً:
    /// النشرة تقوم، والحارس أخضر، وما نُشر ليس ما قُصد. والعلاج الوحيد الذي لا يكسر شيئاً
    /// هو أن يكون <b>معلَناً</b>: مكتوباً في <c>README</c> باسمه وافتراضه، فيقرؤه المالك
    /// ويقرّر. وافتراضٌ غير مكتوب افتراضٌ لا يعرفه أحد إلا من يقرأ YAML.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryVariableComposeReadsIsDocumentedInTheDeployReadme()
    {
        string readme = ReadText(ReadmePath);

        HashSet<string> documented =
        [
            .. Regex.Matches(readme, @"`([A-Z][A-Z0-9_]{2,})`", RegexOptions.None, TimeSpan.FromSeconds(5))
                .Select(static match => match.Groups[1].Value),
        ];

        List<string> offenders =
        [
            .. Compose.Value.All
                .Where(name => !documented.Contains(name))
                .Order(StringComparer.Ordinal)
                .Select(name => "‏" + name + Describe(name)),
        ];

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} متغيّراً تقرؤه الحزمة ولا يذكره {ReadmePath}:\n")
            + string.Join('\n', offenders)
            + "\n\nوالأخطر فيها ما له ‎:-‎: لا يوقف نشرة، بل ينشر غير المقصود بصمت. اكتبه في\n"
            + "جدول deploy/README.md §1 باسمه وافتراضه، كي يكون قراراً مقروءاً لا سلوكاً مخبوءاً.");
    }

    /// <summary>
    /// <b>٥ · والفحص القَبْلي يسمّي كل سرٍّ إلزامي قبل أن يُلمس الخادم.</b>
    /// <para>
    /// «ارفض ولا تخمّن» مطبَّقاً على النشر: سرٌّ ناقص يجب أن يوقف السير <b>باسمه</b> في
    /// الخطوة الأولى، لا أن يظهر بعد دقائق داخل <c>compose up</c> على خادمٍ نصفِ منشور.
    /// والقاعدة تشمل كل ما يطلبه <c>compose</c> بـ<c>:?</c> ويأتي من مخزن الأسرار — لا
    /// ما يشتقّه سير العمل لنفسه كالوسم والسجلّ.
    /// </para>
    /// </summary>
    [Fact]
    public void ThePreFlightCheckNamesEveryMandatorySecretBeforeTheServerIsTouched()
    {
        List<string> offenders =
        [
            .. Compose.Value.Required
                .Where(name => Step.Value.FromSecretStore.Contains(name))
                .Where(name => !Step.Value.PreFlight.Contains(name))
                .Order(StringComparer.Ordinal)
                .Select(static name => "‏" + name + " — إلزامي بـ${…:?} ويأتي سرّاً، وليس في حلقة for name in"),
        ];

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} سرّاً إلزامياً لا يسمّيه الفحص القَبْلي:\n")
            + string.Join('\n', offenders)
            + "\n\nأضِفه إلى حلقة `for name in …` في خطوة «كتابة ملف البيئة على الخادم». الفرق\n"
            + "ليس تجميلياً: بدونها يفشل النشر داخل compose up على خادم حيّ بعد أن بُنيت\n"
            + "الصور ونُقلت الملفّات؛ ومعها يتوقّف قبل أن يُفتح اتصال واحد، برسالة تسمّي الناقص.");
    }

    /// <summary>
    /// <b>شاهدٌ موجب على القارئ نفسه.</b> حارسٌ قوائمُه فارغة يمرّ دائماً ولا يُثبت شيئاً —
    /// وهو نمطٌ وقع في هذا المستودع من قبل. فتُفحص هنا مخرجات القراءة بأسماء بعينها
    /// وأشكالٍ بعينها: صيغة <c>:?</c>، وصيغة <c>:-</c>، وصيغة <c>Caddy</c>، والتعليقات
    /// المستبعَدة.
    /// </summary>
    [Fact]
    public void TheReaderActuallyReadsTheThreeListsAndNotEmptySets()
    {
        Assert.True(Compose.Value.Required.Count >= 5, "المطلوب بـ:? — " + Compose.Value.Required.Count.ToString(CultureInfo.InvariantCulture));
        Assert.True(Compose.Value.All.Count >= 12, "كل ما تقرؤه الحزمة — " + Compose.Value.All.Count.ToString(CultureInfo.InvariantCulture));

        // صيغة `:?` تُقرأ مطلوبةً، وصيغة `:-` لا تُقرأ مطلوبةً.
        Assert.Contains("BABEL_STORAGE_TICKET_KEY", Compose.Value.Required);
        Assert.Contains("POSTGRES_PASSWORD", Compose.Value.Required);
        Assert.DoesNotContain("BABEL_LOG_LEVEL", Compose.Value.Required);
        Assert.Contains("BABEL_LOG_LEVEL", Compose.Value.All);

        // وصيغة Caddy `{$VAR}` تُقرأ، وتعليقاتُه لا تُقرأ: `{$VAR}` في شرحٍ ليس إعداداً.
        Assert.Contains("BABEL_TLS_MODE", Compose.Value.All);
        Assert.DoesNotContain("VAR", Compose.Value.All);

        foreach (EnvironmentFileWriter writer in Writers)
        {
            HashSet<string> written = writer.Written();
            Assert.True(written.Count >= 8, writer.Label + " — عدد السطور المقروءة: " + written.Count.ToString(CultureInfo.InvariantCulture));
            Assert.Contains("POSTGRES_PASSWORD", written);
        }

        Assert.True(Step.Value.FromSecretStore.Count >= 5, "أسرار الخطوة: " + Step.Value.FromSecretStore.Count.ToString(CultureInfo.InvariantCulture));
        Assert.True(Step.Value.PreFlight.Count >= 4, "أسماء الفحص القَبْلي: " + Step.Value.PreFlight.Count.ToString(CultureInfo.InvariantCulture));
        Assert.Contains("BABEL_SITE", Step.Value.PreFlight);
    }

    private static string Describe(string name)
    {
        string? silent = Compose.Value.SilentDefault(name);
        return silent is null
            ? string.Empty
            : silent.Length == 0
                ? " — وله افتراض صامت **فارغ**"
                : " — وله افتراض صامت «" + silent + "»";
    }

    private static string ReadText(string relative)
    {
        string absolute = Path.Combine(RepositoryLayout.Root, relative);

        return File.Exists(absolute)
            ? File.ReadAllText(absolute)
            : throw new InvalidOperationException("ملفّ الحزمة غائب: " + relative + " / missing deployment file: " + relative);
    }

    /// <summary>يحذف الأسطر التعليقية وحدها — لا كل ما بعد <c>#</c>، كي لا تُبتر قيمة فيها المحرف.</summary>
    private static string WithoutCommentLines(string text, char marker)
    {
        IEnumerable<string> kept = text
            .Split('\n')
            .Where(line => line.TrimStart().Length == 0 || line.TrimStart()[0] != marker);

        return string.Join('\n', kept);
    }

    private static ComposeReferences ReadCompose()
    {
        string compose = WithoutCommentLines(ReadText(ComposePath), '#');
        string caddy = WithoutCommentLines(ReadText(CaddyPath), '#');

        HashSet<string> all = [];
        HashSet<string> required = [];
        Dictionary<string, string> defaults = [];

        // ‏`${NAME}` · `${NAME:?رسالة}` · `${NAME:-افتراض}` — والتصنيف **لكل اسم لا لكل ذكر**:
        // ‏`BABEL_IMAGE_TAG` مذكور مرّة بـ`:?` ومرّات عارياً، وهو مطلوب بحكم الذكر الأول.
        foreach (Match match in Regex.Matches(compose, @"\$\{([A-Z][A-Z0-9_]*)(:[?-])?([^}]*)\}", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            string name = match.Groups[1].Value;
            all.Add(name);

            switch (match.Groups[2].Value)
            {
                case ":?":
                    required.Add(name);
                    break;
                case ":-":
                    defaults[name] = match.Groups[3].Value;
                    break;
                default:
                    break;
            }
        }

        // وصيغة Caddy مختلفة: `{$NAME}` و`{$NAME:افتراض}`.
        foreach (Match match in Regex.Matches(caddy, @"\{\$([A-Z][A-Z0-9_]*)(:([^}]*))?\}", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            string name = match.Groups[1].Value;
            all.Add(name);

            if (match.Groups[2].Success)
            {
                defaults.TryAdd(name, match.Groups[3].Value);
            }
        }

        return new ComposeReferences(all, required, defaults);
    }

    private static DeployStep ReadDeployStep()
    {
        string workflow = ReadText(DeployWorkflowPath);
        string region = Region(workflow, "      - name: كتابة ملف البيئة على الخادم", "      - name: ", DeployWorkflowPath);

        HashSet<string> secrets =
        [
            .. Regex.Matches(region, @"^\s+([A-Z][A-Z0-9_]*):\s*\$\{\{\s*(secrets|vars)\.", RegexOptions.Multiline, TimeSpan.FromSeconds(5))
                .Select(static match => match.Groups[1].Value),
        ];

        Match loop = Regex.Match(region, @"for name in ((?:[A-Z0-9_]+|\s|\\)+);", RegexOptions.None, TimeSpan.FromSeconds(5));

        HashSet<string> preFlight = loop.Success
            ? [.. loop.Groups[1].Value.Split([' ', '\n', '\r', '\t', '\\'], StringSplitOptions.RemoveEmptyEntries)]
            : throw new InvalidOperationException(
                "لم تُقرأ حلقة الفحص القَبْلي `for name in …` في " + DeployWorkflowPath
                + " — والحارس الذي لا يجد ما يفحصه يقول ذلك ولا يمرّ.");

        return new DeployStep(secrets, preFlight);
    }

    private static string Region(string text, string start, string end, string path)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);

        if (from < 0)
        {
            throw new InvalidOperationException(
                "لم يُعثر على «" + start + "» في " + path + " — تغيّر شكل الملفّ فبطل القارئ. عدّله، ولا تُسقط الحارس.");
        }

        int to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);

        return to < 0 ? text[from..] : text[from..to];
    }

    /// <summary>ما تقرؤه الحزمة: كل اسم، وما هو إلزامي منه، وما له افتراض صامت.</summary>
    private sealed class ComposeReferences(
        HashSet<string> all,
        HashSet<string> required,
        Dictionary<string, string> defaults)
    {
        public HashSet<string> All => all;

        public HashSet<string> Required => required;

        public string? SilentDefault(string name) => defaults.TryGetValue(name, out string? value) ? value : null;
    }

    /// <summary>خطوة «كتابة ملف البيئة على الخادم» مقروءةً: مصادرُها، وأسماء فحصها القَبْلي.</summary>
    private sealed class DeployStep(HashSet<string> fromSecretStore, HashSet<string> preFlight)
    {
        public HashSet<string> FromSecretStore => fromSecretStore;

        public HashSet<string> PreFlight => preFlight;
    }

    /// <summary>كاتبُ ملفّ <c>.env</c>: اسمه المقروء، وملفّه، وحدود الكتلة التي يكتب فيها.</summary>
    private sealed class EnvironmentFileWriter(string label, string path, string start, string end)
    {
        private readonly Lazy<HashSet<string>> _written = new(() => Read(path, start, end));

        public string Label => label;

        public string Path => path;

        public HashSet<string> Written() => _written.Value;

        private static HashSet<string> Read(string path, string start, string end)
        {
            string region = Region(ReadText(path), start, end, path);

            // شكلان: `echo "NAME=…"` داخل كتلة تُمرَّر بالأنبوب، و`NAME=…` داخل heredoc.
            HashSet<string> names =
            [
                .. Regex.Matches(region, @"^\s*(?:echo "")?([A-Z][A-Z0-9_]*)=", RegexOptions.Multiline, TimeSpan.FromSeconds(5))
                    .Select(static match => match.Groups[1].Value),
            ];

            return names.Count > 0
                ? names
                : throw new InvalidOperationException(
                    "لم يُقرأ سطرُ بيئةٍ واحد من " + path + " — تغيّر شكل الكتلة فبطل القارئ. عدّله، ولا تُسقط الحارس.");
        }
    }
}
