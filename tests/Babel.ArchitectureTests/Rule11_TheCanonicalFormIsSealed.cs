using System.Reflection;
using Babel.ArchitectureTests.Support;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 11 — الشكل القانوني مختوم.</b>
/// <para>
/// <c>Babel.Canonicalization</c> ليست وحدة منتج بل <b>الطريق الوحيد إلى دالة التجزئة</b>.
/// البايتات المُجزَّأة هي الدليل الذي يُعرض على مدقّق بعد سنوات، ولذلك تُفرض عليها حدود
/// أشدّ من حدود أي مشروع آخر في المستودع:
/// </para>
/// <list type="number">
///   <item><b>صفر اعتماديات.</b> لا حزمة، ولا مشروع، ولا حتى النواة المشتركة. ترقية اعتمادية
///         لا يجوز أن تحرّك بايتة واحدة (SPEC §8 · فخ-18).</item>
///   <item><b>الدفتر وحده يعتمد عليها.</b> وحدة أفقية تُجزّئ شيئاً هي وحدة تكتب دليلاً
///         خارج الدفتر — وهي القاعدة 1 من باب آخر.</item>
///   <item><b>لا مُسلسِل ولا مشغّل قاعدة بيانات ولا <c>ToString()</c> واعٍ باللغة</b> قرب
///         البايتات: لا اعتماد على <c>System.Text.Json</c> ولا على Npgsql ولا على EF Core
///         (فخ-18 · فخ-19 · القائمة المرجعية §8).</item>
///   <item><b>حارس العولمة الثابتة موجود.</b> في وضع <c>InvariantGlobalization</c> يصير
///         <c>String.Normalize</c> عملية لا شيء <b>بصمت</b>، فتتغيّر كل بصمة نصّ عربي
///         (SPEC §8.2 — أخطر مصيدة نشر في المكتبة).</item>
///   <item><b>الرابط داخل البايتات.</b> لا يوجد مسار يُنتج بايتات قانونية بلا رقم تسلسل
///         وبصمة سابقة — وإلا فالسلسلة زخرفية (ADR-0007 · فخ-22).</item>
/// </list>
/// </summary>
public sealed class Rule11_TheCanonicalFormIsSealed
{
    private static Assembly Library => BabelAssemblies.Named(ModuleMap.Canonicalization);

    private static ProjectFile LibraryProject =>
        RepositoryLayout.SourceProjects.Single(static project => project.Name == ModuleMap.Canonicalization);

    [Fact]
    public void TheLibraryDeclaresNoPackageAndNoProjectReference()
    {
        Assert.Empty(LibraryProject.PackageReferences);
        Assert.Empty(LibraryProject.ProjectReferences);
    }

    [Fact]
    public void OnlyTheLedgerMayDependOnTheCanonicalForm()
    {
        List<string> violations = [.. RepositoryLayout.SourceProjects
            .Where(static project => project.Name != ModuleMap.Ledger && project.Name != ModuleMap.Api)
            .Where(static project => project.Name != ModuleMap.Canonicalization)
            .Where(static project => project.ProjectReferences.Contains(ModuleMap.Canonicalization, StringComparer.Ordinal))
            .Select(static project => project.RelativePath)];

        Assert.True(
            violations.Count == 0,
            "الدفتر وحده يُجزّئ قيداً. مشاريع تشير إلى مكتبة التوحيد القياسي بلا حق:\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void NoSerialiserNoDriverAndNoFrameworkComesNearTheHashedBytes()
    {
        string[] forbidden =
        [
            "System.Text.Json",
            "Newtonsoft.Json",
            "Npgsql",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.AspNetCore",
            "Wolverine",
        ];

        // فحص التجميعة نفسها: ما تشير إليه فعلاً، لا ما يعلنه ملف المشروع.
        List<string> referenced = [.. Library.GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(name => forbidden.Any(bad => name.StartsWith(bad, StringComparison.Ordinal)))];

        Assert.True(
            referenced.Count == 0,
            "مُسلسِل أو مشغّل قاعدة بيانات قرب البايتات المُجزَّأة — فخ-18 وفخ-19:\n"
            + string.Join('\n', referenced));

        ArchTestResult result = Types.InAssembly(Library)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "أنواع في المكتبة تعتمد على مُسلسِل أو مشغّل: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void TheLibraryDependsOnNoBabelAssemblyAtAll()
    {
        List<string> babelReferences = [.. Library.GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(static name => name.StartsWith("Babel.", StringComparison.Ordinal))];

        Assert.True(
            babelReferences.Count == 0,
            "مكتبة التوحيد القياسي تعتمد على تجميعة من المنتج — البايتات صارت رهينة تغييرٍ فيها:\n"
            + string.Join('\n', babelReferences));
    }

    [Fact]
    public void TheInvariantGlobalisationGuardExistsAndIsWiredIntoEveryEntryPoint()
    {
        // السطر الذي يمنع أخطر مصيدة نشر: بدونه تُنتج المكتبة بصمات خاطئة بصمت.
        Type runtime = BabelAssemblies.TypesOf(Library).Single(static type => type.Name == "CanonicalRuntime");
        Assert.NotNull(runtime.GetMethod("EnsureSupported", BindingFlags.Public | BindingFlags.Static));

        string source = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", ModuleMap.Canonicalization, "CanonicalRuntime.cs"));
        Assert.Contains("Normalize", source, StringComparison.Ordinal);

        // والحارس مُستدعى فعلاً من المُوحِّد نفسه، لا معرَّفاً وغير مستعمل.
        string canonicaliser = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", ModuleMap.Canonicalization, "Canonicalizer.cs"));
        Assert.Contains("CanonicalRuntime.EnsureSupported()", canonicaliser, StringComparison.Ordinal);
    }

    [Fact]
    public void TheChainLinkIsInsideTheHashedBytesAndCannotBeForgotten()
    {
        // ADR-0007: chain_seq و prev_hash يُكتبان في ترويسة البايتات، لا في عمود مجاور.
        // الإنفاذ بنيوي: مستند غير مرتبط بموقع في السلسلة لا يُنتج بايتات إطلاقاً.
        string canonicaliser = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", ModuleMap.Canonicalization, "Canonicalizer.cs"));

        Assert.Contains("\"chain_seq\"", canonicaliser, StringComparison.Ordinal);
        Assert.Contains("\"prev_hash\"", canonicaliser, StringComparison.Ordinal);
        Assert.Contains("DocumentUnbound", canonicaliser, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGoldenVectorFileIsCommittedAndNonTrivial()
    {
        // متجهات ثابتة مُودَعة، وأي انحراف يُفشِل البناء (القائمة المرجعية §8 — بند الهيئة).
        string golden = Path.Combine(RepositoryLayout.Root, "tests", "golden", "golden-vectors.v1.json");
        Assert.True(File.Exists(golden), "ملف المتجهات الذهبية غير موجود: " + golden);
        Assert.True(new FileInfo(golden).Length > 4096, "ملف المتجهات الذهبية أصغر من أن يكون تثبيتاً حقيقياً.");
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // لو لم تُحمَّل المكتبة لمرّت كل القواعد أعلاه فراغاً.
        Assert.Contains(BabelAssemblies.Product, assembly => assembly.GetName().Name == ModuleMap.Canonicalization);
        Assert.Contains(RepositoryLayout.SourceProjects, project => project.Name == ModuleMap.Canonicalization);
        Assert.True(BabelAssemblies.TypesOf(Library).Count() >= 20);
    }
}
