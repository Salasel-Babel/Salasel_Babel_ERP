using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>الشاهد الموجب — تصادم تركيب مُتعمَّد يعيش في تجميعة الاختبار.</b>
/// <para>
/// دالّتا امتداد بالاسم نفسه، في فضاء الاسم نفسه، في نوعين مختلفين، وكلتاهما تُستدعى
/// بلا وسيط. هذا هو بالضبط شكل العطل الذي وقع في <c>Babel.Compliance</c>: كان
/// <c>services.AddBabelCompliance()</c> في الجذر التركيبي يذهب إلى الدالة التي لا
/// تسجّل إلا خدمة التطبيق، فلا يُركَّب مسار الالتزام كلّه — والبناء أخضر، والمراجعة
/// لا ترى شيئاً، لأن النداء صحيح تماماً ويُترجم بلا تحذير.
/// </para>
/// <para>
/// ووجوده هنا هو ما يمنع الفراغ: لو ضاق الماسح — بحث عن نوع بدل فضاء اسم، أو نسي
/// المعاملات الاختيارية — لمرّ «لا تصادم» وهو لا يفحص شيئاً.
/// </para>
/// </summary>
internal static class CompositionCollisionControlOne
{
    /// <summary>حِمل بلا وسائط.</summary>
    public static IServiceCollection AddBabelCollisionControl(this IServiceCollection services) => services;
}

/// <summary>الطرف الثاني من الشاهد: الاسم نفسه، فضاء الاسم نفسه، ووسائطه كلها اختيارية.</summary>
internal static class CompositionCollisionControlTwo
{
    /// <summary>حِمل كل وسائطه اختيارية — فيُستدعى هو أيضاً بلا وسيط.</summary>
    public static IServiceCollection AddBabelCollisionControl(
        this IServiceCollection services, string? label = null, int order = 0)
    {
        _ = label;
        _ = order;
        return services;
    }
}

/// <summary>
/// <b>لوحدة واحدة نقطة تركيب واحدة — ولنقطة الدخول العامة عملٌ خلفها.</b>
/// <para>
/// حارسان يمنعان شكلين من «الكود الذي يبدو صحيحاً وليس كذلك»، وقعا كلاهما فعلاً في
/// وحدة الالتزام على <c>origin/develop</c> عند <c>325bddb</c>:
/// </para>
/// <list type="number">
///   <item>
///     <b>تركيبان باسم واحد.</b> دالّتا امتداد على <c>IServiceCollection</c>، بالاسم
///     نفسه وفي فضاء الاسم نفسه، كلتاهما تُستدعى بلا وسيط. قواعد اختيار الحِمل الزائد
///     في C# تحسم بينهما بصمت لصالح الأقلّ معاملات، فيُركَّب نصف الوحدة ويبدو أنها
///     رُكِّبت كاملة (‏<c>docs/evidence/traps.md#fakh-two-registrations-one-name</c>).
///   </item>
///   <item>
///     <b>نقطة دخول عامة ترفض دائماً.</b> خدمة تطبيق تحمل سمة استحقاق وتوقيعاً كامل
///     الشكل، وجسمها يعيد رمز خطأ ينتهي بـ<c>.not_implemented</c> — بينما التنفيذ
///     الحقيقي يعيش في مجلد آخر ولا يستدعيه شيء. القارئ يستنتج أن الميزة غير مبنيّة
///     وهي مبنيّة ومُختبَرة
///     (‏<c>docs/evidence/traps.md#fakh-authoritative-entry-point-that-leads-nowhere</c>).
///   </item>
/// </list>
/// </summary>
public sealed class TheModuleCompositionSeamIsUnambiguous
{
    // ── الحارس الأول: تركيبان باسم واحد ─────────────────────────────────────

    private const string ServiceCollection = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    /// <summary>
    /// دوال الامتداد على <c>IServiceCollection</c> التي تُستدعى بلا وسيط، مجمَّعة
    /// بـ(فضاء الاسم، الاسم). أي مجموعة فيها أكثر من واحدة هي تصادم صامت.
    /// </summary>
    private static IReadOnlyList<string> Collisions(IEnumerable<Type> types, out int inspected)
    {
        List<(string Key, string Where)> callable = [];

        foreach (Type type in types.Where(static t => t is { IsAbstract: true, IsSealed: true }))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                          | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length == 0
                    || parameters[0].ParameterType.FullName != ServiceCollection
                    || method.GetCustomAttribute<ExtensionAttribute>() is null)
                {
                    continue;
                }

                // يُستدعى بلا وسيط زائد: كل ما بعد المُستقبِل اختياري.
                if (!parameters.Skip(1).All(static p => p.IsOptional))
                {
                    continue;
                }

                callable.Add(($"{type.Namespace}.{method.Name}", $"{type.FullName}.{method.Name}"));
            }
        }

        inspected = callable.Count;

        return [.. callable
            .GroupBy(static c => c.Key, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => $"{g.Key} ← {string.Join(" · ", g.Select(static c => c.Where).Order(StringComparer.Ordinal))}")
            .Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void NoTwoRegistrationEntryPointsShareANameInTheSameNamespace()
    {
        IReadOnlyList<string> violations = Collisions(BabelAssemblies.AllTypes(), out int inspected);

        Assert.True(inspected > 0, "لم تُعثر أي دالة تركيب — الحارس يمرّ فراغاً.");
        Assert.True(
            violations.Count == 0,
            "دالّتا تركيب بالاسم نفسه في فضاء الاسم نفسه، وكلتاهما تُستدعى بلا وسيط: "
            + "اختيار الحِمل الزائد يحسم بينهما بصمت، فيُركَّب نصف الوحدة ويبدو أنها كاملة.\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void TheCollisionGuardBitesOnItsOwnControl()
    {
        // الماسح نفسه، مُوجَّهاً إلى تجميعة الاختبار حيث يعيش التصادم المتعمَّد.
        IReadOnlyList<string> violations =
            Collisions(typeof(TheModuleCompositionSeamIsUnambiguous).Assembly.GetTypes(), out int inspected);

        Assert.True(inspected >= 2, $"الماسح لم يرَ إلا {inspected} دالة تركيب في تجميعة الاختبار.");
        Assert.Contains(violations, v => v.Contains("AddBabelCollisionControl", StringComparison.Ordinal));
    }

    // ── الحارس الثاني: نقطة دخول عامة ترفض دائماً ───────────────────────────

    /// <summary>
    /// رمز خطأ ينتهي بـ<c>.not_implemented</c> في مصدر منتج. المطابقة على <b>رمز</b>
    /// الخطأ لا على أي ذكر للكلمة: التعليق الذي يشرح لماذا لا يوجد رمز كهذا يجب أن
    /// يبقى مسموحاً، وإلا صار الحارس يمنع توثيق نفسه.
    /// </summary>
    private static readonly Regex NotImplementedErrorCode = new(
        "\"[A-Za-z0-9_.]+\\.not_implemented\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    private static IReadOnlyList<string> DeadEntryPoints(IEnumerable<(string Path, string Text)> files) =>
        [.. files
            .Where(static f => f.Path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(static f => NotImplementedErrorCode.IsMatch(f.Text))
            .Select(static f => f.Path)
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void NoProductionEntryPointDeclaresAPermanentNotImplementedRefusal()
    {
        string[] tracked = TrackedSourceFiles();
        (string, string)[] corpus =
            [.. tracked
                .Where(static p => p.EndsWith(".cs", StringComparison.Ordinal))
                .Select(p => (p, File.ReadAllText(Path.Combine(RepositoryLayout.Root, p)))) ];

        Assert.True(corpus.Length > 100, $"عدد ملفات المصدر المفحوصة {corpus.Length} أقل من أن يثبت شيئاً.");

        IReadOnlyList<string> violations = DeadEntryPoints(corpus);

        Assert.True(
            violations.Count == 0,
            "نقطة دخول عامة ترفض دائماً برمز ينتهي بـ.not_implemented. النوع العام الذي يبدو "
            + "أنه الطريق إلى الميزة ولا يؤدّي إليها أسوأ من غيابه: القارئ يستنتج أن الميزة "
            + "غير مبنيّة، وقد تكون مبنيّة ومُختبَرة في مجلد آخر.\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void TheDeadEntryPointGuardBitesOnItsOwnControl()
    {
        // شاهد نصّي: لو كفّ الماسح عن المطابقة لمرّ الفحص أعلاه وهو لا يفحص شيئاً.
        (string, string)[] control =
        [
            ("src/Control/Dead.cs", "new Error(\"module.not_implemented\", \"لم يُنفَّذ\", \"not implemented\");"),
            ("src/Control/Alive.cs", "// لا يوجد هنا رمز ينتهي بهذه اللاحقة إطلاقاً."),
            ("src/Control/NotCode.txt", "\"module.not_implemented\""),
        ];

        Assert.Equal(["src/Control/Dead.cs"], DeadEntryPoints(control));
    }

    /// <summary>
    /// ما يتعقّبه git تحت <c>src/</c> — لا ما يقع على القرص. مخرجات بناء أو ملف لم
    /// يُضَف بعد لا يجوز أن يغيّرا حكم الحارس.
    /// </summary>
    private static string[] TrackedSourceFiles()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("src");

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "‏git ls-files أخفق، فلا سبيل إلى معرفة محتوى المستودع — والحارس يرمي ولا "
                + "يخمّن على ما يقع على القرص. / git ls-files failed: " + error);
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
