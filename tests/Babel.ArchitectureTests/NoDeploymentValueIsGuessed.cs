using System.Reflection;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارس الارتداد الصامت — قيمةُ نشرٍ غائبة تُرفض ولا تُخمَّن.</b>
/// <para>
/// <b>العطل الذي يجعله مستحيلاً، بنصّ ما كان مكتوباً:</b>
/// <code>
/// Environment.GetEnvironmentVariable("BABEL_SALES_DB")
///   ?? "Host=…;Database=babel_sales;Username=postgres;…"
/// </code>
/// تسعُ نظائرَ لهذا السطر في المستودع، سبعٌ منها بالمستخدم الفائق للعنقود. ونشرةٌ
/// ينقصها المتغيّر <b>لا تتعطّل</b>: تعمل الوحدة بصلاحيةٍ كاملة على قاعدةٍ لم يقصدها
/// أحد، وتُقرأ الخُضرة صحّةً. وهذا أخطر من التعطّل بمرتبة، لأن التعطّل يُرى.
/// </para>
/// <para>
/// <b>وثلاثة فحوص لا واحد</b>، لأن العطل يعود من ثلاثة أبواب: نصُّ اتصالٍ يُكتب من
/// جديد في وحدة، أو صنفُ إعداداتٍ يُضاف بلا رفضٍ يسمّي متغيّره، أو رفضٌ يوجد ولا
/// يناديه الجذر التركيبي فيبقى الخادم يقلع على الفراغ.
/// </para>
/// <para>
/// <b>ولكلٍّ شاهدُه الموجب (ADR-0056):</b> نمطٌ يُثبَت أنه ينطق على عيّنةٍ مُركَّبة،
/// ومسحٌ يُثبَت أنه زار ملفّات، وانعكاسٌ يُثبَت أنه وجد أصنافاً. وحارسٌ يمرّ على
/// مجموعةٍ فارغة لا يُفرَّق عن حارسٍ معطَّل.
/// </para>
/// </summary>
public sealed partial class NoDeploymentValueIsGuessed
{
    /// <summary>
    /// الملفّ الوحيد الذي يجوز أن يُبنى فيه نصّ اتصالٍ محلّي — وفيه وحده اسمُ المِعوَد
    /// واسمُ المستخدم الفائق. وهو مُعلَنٌ هنا بمساره كي يُقرأ الاستثناء لا يُخمَّن.
    /// </summary>
    private const string TheOneDeclaredPlace = "src/Babel.SharedKernel/DeploymentSetting.cs";

    /// <summary>الجذر التركيبي — بوّابةُ الإقلاع فيه.</summary>
    private const string CompositionRoot = "src/Babel.Api/Hosting/BabelApiHost.cs";

    /// <summary>اسم بوّابة الإقلاع.</summary>
    private const string BootGate = "EnsureDeploymentConfigured";

    /// <summary>
    /// المجلّدات الممسوحة: <c>src/</c> كلّها، و<c>demo/company/</c> لأنها
    /// <b>صورةُ الترحيل في النشر</b> (<c>deploy/Dockerfile.migrator</c>) لا أداةَ
    /// مطوّر. و<c>demo/vertical-slice/</c> خارج النطاق: دَينٌ مُعلَن بـADR-0037 لا
    /// يُشحن في صورةٍ واحدة، و<c>spikes/</c> خارجه بالقاعدة 8.
    /// </summary>
    private static readonly string[] ScannedFolders = ["src", Path.Combine("demo", "company")];

    /// <summary>
    /// نصُّ اتصالٍ كامل مكتوبٌ حرفاً: سلسلةٌ فيها <c>Host=</c> و<c>Username=</c> معاً.
    /// <para>
    /// <b>وشرطُ اجتماعهما مقصود:</b> اسمُ مضيفٍ وحده قد يكون إعداداً مشروعاً يُقرأ من
    /// البيئة بافتراضٍ ظاهر، أمّا نصُّ اتصالٍ كامل مكتوبٌ في شيفرة <b>فهو الشكل الذي
    /// يجعل الوحدة تعمل على قاعدةٍ لم يقصدها أحد</b> — وهو ما يُمنع هنا.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"""[^""\n]*Host=[^""\n]*Username=[^""\n]*""", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionLiteral();

    /// <summary>اسمُ المستخدم الفائق داخل نصّ اتصال — الشكل الخطر بعينه.</summary>
    [GeneratedRegex(@"Username=postgres", RegexOptions.CultureInvariant)]
    private static partial Regex SuperuserLiteral();

    // ── ١ · لا نصّ اتصال مكتوبٌ حرفاً خارج الموضع المُعلَن ──────────────────────

    /// <summary>
    /// لا ملفّ في النطاق يكتب نصّ اتصالٍ كاملاً، ولا يذكر المستخدم الفائق داخل نصّ
    /// اتصال — إلا <see cref="TheOneDeclaredPlace"/>.
    /// </summary>
    [Fact]
    public void NoModuleWritesAConnectionStringLiteralOfItsOwn()
    {
        List<string> offenders = [];
        int filesScanned = 0;

        foreach (string path in ScannedSourceFiles())
        {
            filesScanned++;
            string relative = Relative(path);

            if (string.Equals(relative, TheOneDeclaredPlace, StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(path);

            foreach (Match match in ConnectionLiteral().Matches(text))
            {
                offenders.Add(relative + ": نصّ اتصالٍ مكتوبٌ حرفاً — " + Shorten(match.Value));
            }

            foreach (Match match in SuperuserLiteral().Matches(text))
            {
                offenders.Add(relative + ": المستخدم الفائق داخل نصّ اتصال — " + Shorten(match.Value));
            }
        }

        // شاهدٌ موجب على المسح نفسه: مجموعةٌ فارغة تعني نطاقاً انكسر لا مستودعاً نظيفاً.
        Assert.True(
            filesScanned >= 100,
            FormattableString.Invariant($"مُسح {filesScanned} ملفّاً فقط — النطاق انكسر، والخُضرة لا تعني شيئاً."));

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} نصَّ اتصالٍ مكتوبٌ في شيفرة:\n")
            + string.Join('\n', offenders.Order(StringComparer.Ordinal))
            + "\n\nوغيابُ متغيّرٍ عن نشرةٍ لا يُعطّل شيئاً حينئذ: الوحدة تعمل على قاعدةٍ لم\n"
            + "يقصدها أحد، وبصلاحيةٍ لم يقصدها أحد، بلا سطرٍ واحد يقول ذلك. اقرأ القيمة\n"
            + "بـ‏DeploymentSetting.Connection، واجعل الغياب رفضاً يسمّي المتغيّر.");
    }

    /// <summary>
    /// <b>الشاهد الموجب على النمطين:</b> يُثبَت أنهما ينطقان على عيّنةٍ مُركَّبة هنا،
    /// وعلى الموضع المُعلَن الذي يبني النصّ فعلاً — فصمتُهما على بقيّة المستودع نتيجة
    /// لا عطل.
    /// </summary>
    [Fact]
    public void TheConnectionLiteralPatternsActuallyFire()
    {
        // عيّنةٌ مُركَّبة في هذا السطر، وهي بالضبط شكل ما كان مكتوباً في تسع وحدات.
        const string sample = "\"Host=example.invalid;Port=5432;Database=babel_x;Username=postgres\"";

        Assert.Matches(ConnectionLiteral(), sample);
        Assert.Matches(SuperuserLiteral(), sample);

        // وما لا يجوز أن يُلتقَط: مضيفٌ وحده، أو اسمُ دورٍ وحده، ليسا نصَّ اتصال.
        Assert.DoesNotMatch(ConnectionLiteral(), "\"127.0.0.1\"");
        Assert.DoesNotMatch(SuperuserLiteral(), "Env(\"BABEL_CP_ADMIN_USER\", \"postgres\")");

        // والموضع المُعلَن يُطابَق فعلاً — فالاستثناء استثناءٌ عن شيءٍ موجود لا عن فراغ.
        string declared = File.ReadAllText(Path.Combine(RepositoryLayout.Root, TheOneDeclaredPlace));
        Assert.Contains("Host=", declared, StringComparison.Ordinal);
    }

    // ── ٢ · كل صنف إعداداتٍ يُسمّي متغيّره يحمل رفضاً يسمّيه ────────────────────

    /// <summary>
    /// كل صنفٍ في المنتج يُعلن ثابتاً اسمُه ينتهي بـ<c>ConnectionVariable</c> يحمل
    /// دالّةً عامّة <c>Ensure…Configured</c> ترفض الغياب. وثابتٌ بلا رفض هو <b>اسمُ
    /// متغيّرٍ بلا من يشتكي غيابه</b>.
    /// </summary>
    [Fact]
    public void EveryOptionsTypeThatNamesAConnectionVariableAlsoRefusesItsAbsence()
    {
        List<string> offenders = [];
        List<Type> declaring = [];

        foreach (Type type in BabelAssemblies.AllTypes())
        {
            List<string> variables = [.. type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(static f => f.Name)
                .Where(static name => name.EndsWith("ConnectionVariable", StringComparison.Ordinal))];

            if (variables.Count == 0)
            {
                continue;
            }

            declaring.Add(type);

            bool refuses = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(static m => m.Name.StartsWith("Ensure", StringComparison.Ordinal)
                    && m.Name.EndsWith("Configured", StringComparison.Ordinal)
                    && m.GetParameters().Length == 0);

            if (!refuses)
            {
                offenders.Add(type.FullName + ": يُعلن " + string.Join("، ", variables) + " ولا يحمل Ensure…Configured");
            }
        }

        // شاهدٌ موجب: الانعكاس وجد أصنافاً فعلاً.
        Assert.True(
            declaring.Count >= 8,
            FormattableString.Invariant($"وُجد {declaring.Count} صنفَ إعداداتٍ يُسمّي متغيّر اتصاله — الانعكاس أو المسح انكسر."));

        Assert.True(
            offenders.Count == 0,
            "صنفُ إعداداتٍ يُسمّي متغيّره ولا يرفض غيابه:\n" + string.Join('\n', offenders.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// <b>والرفضُ يقع فعلاً ويسمّي متغيّره.</b> فحصُ الشكل وحده لا يكفي: دالّةٌ اسمُها
    /// <c>EnsureConfigured</c> وجسمُها فارغ تُرضي الفحص السابق ولا تمنع شيئاً. فيُنشأ
    /// هنا كلُّ صنفِ إعداداتٍ، ويُفرَّغ اتصالُه، ويُطلَب رفضُه — <b>ويُقرأ نصُّ الرسالة</b>
    /// بحثاً عن اسم المتغيّر الذي على قارئها أن يضبطه.
    /// </summary>
    [Fact]
    public void EveryRefusalActuallyThrowsAndNamesTheVariableTheReaderMustSet()
    {
        List<string> offenders = [];
        int checkedRefusals = 0;

        foreach (Type type in BabelAssemblies.AllTypes())
        {
            foreach (FieldInfo field in type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
                .Where(static f => f.Name.EndsWith("ConnectionVariable", StringComparison.Ordinal)))
            {
                // ‏`AppConnectionVariable` ⇒ `AppConnectionString` + `EnsureAppConfigured`.
                string prefix = field.Name[..^"ConnectionVariable".Length];
                string variable = (string)field.GetRawConstantValue()!;

                PropertyInfo? connection = type.GetProperty(prefix + "ConnectionString");
                MethodInfo? refuse = type.GetMethod("Ensure" + prefix + "Configured", Type.EmptyTypes);

                if (connection is null || refuse is null)
                {
                    offenders.Add(type.FullName + ": لا خاصيّة " + prefix + "ConnectionString أو لا دالّة Ensure" + prefix + "Configured");
                    continue;
                }

                object instance = Activator.CreateInstance(type)!;
                connection.SetValue(instance, string.Empty);

                Exception? thrown = Record.Exception(() => refuse.Invoke(instance, null));
                checkedRefusals++;

                if (thrown?.InnerException is not InvalidOperationException refusal)
                {
                    offenders.Add(type.FullName + "." + refuse.Name + ": اتصالٌ فارغ ولم يُرفض — هذا هو الارتداد الصامت بعينه");
                    continue;
                }

                if (!refusal.Message.Contains(variable, StringComparison.Ordinal))
                {
                    offenders.Add(type.FullName + "." + refuse.Name + ": يرفض ولا يسمّي «" + variable + "» — فمن يقرأ الرسالة لا يعرف ماذا يضبط");
                }
            }
        }

        Assert.True(
            checkedRefusals >= 12,
            FormattableString.Invariant($"فُحص {checkedRefusals} رفضاً فقط — الانعكاس انكسر، والخُضرة لا تعني شيئاً."));

        Assert.True(
            offenders.Count == 0,
            "رفضٌ غائبٌ أو أعمى:\n" + string.Join('\n', offenders.Order(StringComparer.Ordinal)));
    }

    // ── ٣ · وبوّابةُ الإقلاع تنادي رفضَ كلّ واحدٍ منها ──────────────────────────

    /// <summary>
    /// كلُّ صنفِ إعداداتٍ يرفض غياب اتصاله <b>مذكورٌ بالاسم في بوّابة الإقلاع</b>.
    /// <para>
    /// <b>ولماذا فحصٌ ثالث:</b> رفضٌ مكتوبٌ لا يناديه أحد ليس رفضاً. وهو الشكل الذي
    /// وقع فعلاً: الموارد البشرية كانت الوحدة الوحيدة المنادى رفضُها، وثمانٍ غيرها
    /// تحمل ارتداداً — والخادم يقلع سليم الظاهر ويردّ <c>/health</c> بنجاح.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBootGateNamesEveryOptionsTypeThatCanRefuse()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryLayout.Root, CompositionRoot));
        int start = source.IndexOf(BootGate + "(WebApplication app)", StringComparison.Ordinal);

        Assert.True(start >= 0, CompositionRoot + " لا يحمل بوّابة إقلاع باسم " + BootGate + " — الحارس بلا موضوع.");

        string gate = source[start..];
        int end = gate.IndexOf("\n    }", StringComparison.Ordinal);
        Assert.True(end > 0, "تعذّر تحديد نهاية بوّابة الإقلاع — القارئ انكسر.");
        gate = gate[..end];

        // الوحدات التي يركّبها هذا الخادم فعلاً. ومستوى التحكّم ليس منها: سطحه
        // اختياري يُفتح بـ`Babel:Fleet:Enabled` ويعمل بدورٍ ثالث، وإعداده خارج هذا
        // النطاق بقرارٍ مكتوب لا بسهو.
        string[] mustBeNamed =
        [
            "LedgerOptions", "CoreOptions", "SalesOptions", "PurchasingOptions",
            "InventoryOptions", "RealEstateOptions", "ProjectsOptions", "HrOptions", "StorageOptions",
        ];

        List<string> missing = [.. mustBeNamed.Where(name => !gate.Contains(name, StringComparison.Ordinal))];

        Assert.True(
            missing.Count == 0,
            "بوّابة الإقلاع لا تسمّي: " + string.Join("، ", missing)
            + "\nوحدةٌ لا يُفحص اتصالها عند الإقلاع تقلع على الفراغ ثمّ تسقط عند أوّل نداء\n"
            + "برسالةٍ تُقرأ عطلَ شبكةٍ لا إعداداً ناقصاً.");
    }

    // ── ٤ · ولا مدّةَ اعتمادٍ ساكنةٌ في نموذج الوصول ───────────────────────────

    /// <summary>
    /// <c>AccessLimits</c> لا يحمل مدّةً واحدة. المُدَد الثلاث — وأخطرها عمرُ اعتماد
    /// التجديد، وهي <b>المدّة التي يبقى فيها اعتمادٌ مسروق صالحاً</b> — سياسةُ أمنٍ
    /// تُشدَّد لحظةَ حادثة، فلا تسكن في صنفٍ لا يقبل ضبطاً.
    /// </summary>
    [Fact]
    public void TheAccessLifetimesAreNotStaticConstantsAnyMore()
    {
        Type limits = BabelAssemblies.Named("Babel.Core").GetTypes()
            .Single(static t => string.Equals(t.FullName, "Babel.Core.Access.AccessLimits", StringComparison.Ordinal));

        List<string> durations = [.. limits
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static p => p.PropertyType == typeof(TimeSpan))
            .Select(static p => p.Name)];

        Assert.True(
            durations.Count == 0,
            "مُدَدٌ ساكنة عادت إلى AccessLimits: " + string.Join("، ", durations)
            + "\nموضعُها AccessPolicy — تُقرأ من البيئة، ولها سقفٌ يُرفض تجاوزه ولا يُقصّ.");

        // شاهدٌ موجب: الصنف نفسه ما زال يحمل حدوده الأربعة، فالفحص لم يقرأ نوعاً فارغاً.
        Assert.True(
            limits.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length >= 4,
            "‏AccessLimits بلا حدود — القارئ يقرأ نوعاً آخر.");
    }

    private static IEnumerable<string> ScannedSourceFiles()
    {
        foreach (string folder in ScannedFolders)
        {
            string absolute = Path.Combine(RepositoryLayout.Root, folder);

            if (!Directory.Exists(absolute))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(absolute, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Relative(path);

                if (relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return path;
            }
        }
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');

    private static string Shorten(string value) =>
        value.Length <= 90 ? value : value[..90] + "…";
}
