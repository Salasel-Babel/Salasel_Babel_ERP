using System.Reflection;
using System.Xml.Linq;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// قراءة ملفات المشاريع نفسها.
/// <para>
/// لماذا لا يكفي فحص IL: مرجع مشروع موجود في <c>csproj</c> ولم يُستعمل بعد <b>لا يظهر في IL</b>.
/// أي أن فحص IL وحده يمرّ على مرجع خاطئ حتى اليوم الذي يكتب فيه أحدهم أول سطر يستعمله —
/// وعندها تكون المراجعة قد مرّت مرات. المرجع نفسه هو ما يُمنع، لا استعماله.
/// </para>
/// </summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<string> RootPath = new(FindRoot);
    private static readonly Lazy<IReadOnlyList<ProjectFile>> ProjectsValue = new(LoadProjects);

    /// <summary>جذر المستودع.</summary>
    public static string Root => RootPath.Value;

    /// <summary>كل مشاريع <c>src/</c> و<c>tests/</c>. مجلد <c>spikes/</c> خارج النطاق عمداً — تجارب لا منتج.</summary>
    public static IReadOnlyList<ProjectFile> Projects => ProjectsValue.Value;

    /// <summary>مشاريع <c>src/</c> فقط.</summary>
    public static IEnumerable<ProjectFile> SourceProjects => Projects.Where(static p => p.IsSource);

    private static string FindRoot()
    {
        string? fromMetadata = typeof(RepositoryLayout).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static a => a.Key == "BabelRepositoryRoot")?.Value;

        if (fromMetadata is not null && Directory.Exists(Path.Combine(fromMetadata, "src")))
        {
            return Path.GetFullPath(fromMetadata);
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("تعذّر تحديد جذر المستودع. / Could not locate the repository root.");
    }

    private static List<ProjectFile> LoadProjects()
    {
        List<ProjectFile> projects = [];

        foreach (string folder in new[] { "src", "tests" })
        {
            string absolute = Path.Combine(Root, folder);
            if (!Directory.Exists(absolute))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(absolute, "*.csproj", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.Ordinal))
            {
                projects.Add(ProjectFile.Read(path, folder == "src"));
            }
        }

        if (projects.Count == 0)
        {
            throw new InvalidOperationException("لم يُعثر على أي ملف مشروع. / No project files were found.");
        }

        return projects;
    }
}

/// <summary>ملف مشروع مقروءاً: اسمه ومراجعه.</summary>
internal sealed class ProjectFile
{
    private ProjectFile(string path, bool isSource, IReadOnlyList<string> projectReferences, IReadOnlyList<string> packageReferences)
    {
        Path = path;
        IsSource = isSource;
        ProjectReferences = projectReferences;
        PackageReferences = packageReferences;
    }

    /// <summary>المسار المطلق.</summary>
    public string Path { get; }

    /// <summary>هل المشروع تحت <c>src/</c>؟</summary>
    public bool IsSource { get; }

    /// <summary>اسم المشروع دون الامتداد.</summary>
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>أسماء المشاريع المُشار إليها.</summary>
    public IReadOnlyList<string> ProjectReferences { get; }

    /// <summary>أسماء الحزم المُشار إليها.</summary>
    public IReadOnlyList<string> PackageReferences { get; }

    /// <summary>مسار نسبي مقروء في رسائل الفشل.</summary>
    public string RelativePath => System.IO.Path.GetRelativePath(RepositoryLayout.Root, Path).Replace('\\', '/');

    public static ProjectFile Read(string path, bool isSource)
    {
        XDocument document = XDocument.Load(path);

        List<string> projectReferences = [.. document
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => System.IO.Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)];

        List<string> packageReferences = [.. document
            .Descendants("PackageReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .Order(StringComparer.Ordinal)];

        return new ProjectFile(path, isSource, projectReferences, packageReferences);
    }
}
