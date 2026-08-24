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
    private static readonly Lazy<IReadOnlyList<string>> AllProjectsValue = new(LoadAllProjectFiles);

    /// <summary>مجلدات المنتج التي تُقرأ مشاريعها. ‏<c>spikes/</c> ليست منها عمداً.</summary>
    private static readonly string[] ProductFolders = ["src", "tests", "tools", "demo"];

    /// <summary>جذر المستودع.</summary>
    public static string Root => RootPath.Value;

    /// <summary>
    /// كل مشاريع المنتج المقروءة: <c>src/</c> و<c>tests/</c> و<c>tools/</c> و<c>demo/</c> — وهي
    /// نفس المجلدات التي يمسحها <see cref="CultureScan"/> مضافاً إليها <c>tests/</c>.
    /// مجلد <c>spikes/</c> خارج النطاق عمداً — تجارب لا منتج، كما في القاعدة 8.
    /// </summary>
    public static IReadOnlyList<ProjectFile> Projects => ProjectsValue.Value;

    /// <summary>مشاريع <c>src/</c> فقط.</summary>
    public static IEnumerable<ProjectFile> SourceProjects => Projects.Where(static p => p.IsSource);

    /// <summary>
    /// <b>كل</b> ملف مشروع في المستودع كائناً ما كان مجلده — بلا قائمة مجلدات مسبقة.
    /// <para>
    /// هذا هو الفرق الذي سمح بالتعفّن: قائمة المجلدات الثابتة كانت <c>{src, tests}</c>، فمشروع
    /// تحت <c>tools/</c> أو <c>demo/</c> لم يكن «موجوداً على القرص» بنظر القاعدة 9 أصلاً، وكانت
    /// تمرّ عليه صامتة وهي تُسمّي نفسها «كل مشروع على القرص في ملف الحل».
    /// الاكتشاف هنا بالبحث لا بالقائمة: مجلد جديد يدخل النطاق من تلقاء نفسه.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> AllProjectFilesOnDisk => AllProjectsValue.Value;

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

        foreach (string folder in ProductFolders)
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

    private static List<string> LoadAllProjectFiles()
    {
        List<string> paths = [.. Directory
            .EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Select(static path => System.IO.Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(static relative => !relative.Contains("/bin/", StringComparison.Ordinal)
                                   && !relative.Contains("/obj/", StringComparison.Ordinal)
                                   && !relative.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        if (paths.Count == 0)
        {
            throw new InvalidOperationException("لم يُعثر على أي ملف مشروع على القرص. / No project files were found on disk.");
        }

        return paths;
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
