using System.Globalization;

namespace SalaselBabel.MatrixValidator.Tests;

/// <summary>Locates the repository's real data/ directory from the test binary's location.</summary>
public static class SeedData
{
    public static string Root { get; } = Locate();

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "chart-of-accounts"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("the repository data/ directory could not be located from " + AppContext.BaseDirectory);
    }

    /// <summary>Copies the real seed into a scratch directory so a test can corrupt one file.</summary>
    public static string CopyToTemp()
    {
        var target = Path.Combine(Path.GetTempPath(), "coa-fixture-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        CopyDirectory(Root, target);
        return target;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from))
            CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
