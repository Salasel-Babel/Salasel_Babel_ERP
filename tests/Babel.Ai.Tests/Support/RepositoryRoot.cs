using System.Reflection;

namespace Babel.Ai.Tests.Support;

/// <summary>
/// جذر المستودع. يُحقن في التجميعة وقت البناء، وله ارتداد يصعد حتى يجد
/// <c>Directory.Packages.props</c> — كي لا يعتمد الحارس على مجلد تشغيل بعينه.
/// </summary>
internal static class RepositoryRoot
{
    private static readonly Lazy<string> Value = new(Find);

    /// <summary>المسار المطلق لجذر المستودع.</summary>
    public static string Path => Value.Value;

    /// <summary>مسار مطلق داخل المستودع.</summary>
    /// <param name="relative">المسار النسبي بشرطات مائلة.</param>
    public static string At(string relative) =>
        System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private static string Find()
    {
        string? declared = typeof(RepositoryRoot).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static a => a.Key == "BabelRepositoryRoot")?.Value;

        if (declared is not null && Directory.Exists(System.IO.Path.Combine(declared, "src")))
        {
            return System.IO.Path.GetFullPath(declared);
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("تعذّر تحديد جذر المستودع. / Could not locate the repository root.");
    }
}
