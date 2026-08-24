using System.Xml.Linq;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 8 — لا <c>WolverineFx.RuntimeCompilation</c> في المنتج.</b>
/// <para>
/// الحزمة تجرّ Roslyn إلى عملية الإنتاج (وثيقة المعمارية §2.2 — مقيس من شجرة الحزم).
/// البديل: التوليد الساكن للشيفرة، <c>dotnet run -- codegen write</c> مع
/// <c>TypeLoadMode.Static</c>.
/// </para>
/// <para>
/// القاعدة مكتوبة الآن رغم أن الوسيط لم يُربط بعد، لأن هذا بالضبط وقتها: إضافة الحزمة
/// «مؤقتاً لتشتغل التجربة» هي الطريقة التي تدخل بها إلى الإنتاج.
/// </para>
/// <para>
/// <c>spikes/</c> خارج النطاق عمداً: تجارب لا منتج، وإحداها تستعمل الحزمة فعلاً.
/// </para>
/// </summary>
public sealed class Rule08_NoRuntimeCompilationInProduction
{
    private const string Banned = "WolverineFx.RuntimeCompilation";

    [Fact]
    public void NoProjectReferencesTheRuntimeCompilationPackage()
    {
        List<string> violations = [.. RepositoryLayout.Projects
            .Where(static project => project.PackageReferences.Contains(Banned, StringComparer.OrdinalIgnoreCase))
            .Select(static project => project.RelativePath)];

        Assert.True(violations.Count == 0, $"{Banned} تجرّ Roslyn إلى الإنتاج:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheCentralPackageFileDoesNotPinIt()
    {
        string path = Path.Combine(RepositoryLayout.Root, "Directory.Packages.props");
        Assert.True(File.Exists(path), "Directory.Packages.props غير موجود — إدارة الإصدارات المركزية شرط.");

        // فحص العناصر لا النص: ذكر اسم الحزمة في تعليق يشرح سبب منعها ليس مخالفة — بل هو المطلوب.
        List<string> pinned = [.. XDocument.Load(path)
            .Descendants("PackageVersion")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(static include => include.Equals(Banned, StringComparison.OrdinalIgnoreCase))];

        Assert.True(pinned.Count == 0, $"{Banned} مثبَّتة مركزياً — الخطوة الأولى نحو دخولها الإنتاج.");
    }

    [Fact]
    public void CentralPackageManagementIsActuallyOn()
    {
        // بلا هذا، كل مشروع يثبّت إصدارَه وحده، ويصير «الرقم الملزم» مختلفاً بين مشروعين.
        string props = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "Directory.Build.props"));
        Assert.Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>", props, StringComparison.Ordinal);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", props, StringComparison.Ordinal);

        // ولا إصدار حزمة مكتوب داخل csproj: مصدر الحقيقة واحد.
        List<string> violations = [.. RepositoryLayout.Projects
            .Where(static project => File.ReadAllText(project.Path).Contains("PackageReference", StringComparison.Ordinal)
                && File.ReadAllLines(project.Path).Any(static line =>
                    line.Contains("PackageReference", StringComparison.Ordinal) && line.Contains("Version=", StringComparison.Ordinal)))
            .Select(static project => project.RelativePath)];

        Assert.True(violations.Count == 0, "إصدار حزمة مكتوب داخل csproj بدل Directory.Packages.props:\n" + string.Join('\n', violations));
    }
}
