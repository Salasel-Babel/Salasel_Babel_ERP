using System.Reflection;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// كل تجميعات المنتج، مُحمَّلة من مجلد الإخراج لا مذكورة بالاسم.
/// <para>
/// الاكتشاف بالمجلد لا بقائمة ثابتة عمداً: وحدة جديدة تدخل كل القواعد تلقائياً،
/// ولا تحتاج مطوّراً يتذكّر إضافتها إلى قائمة — والقاعدة التي تعتمد على التذكّر ليست قاعدة.
/// </para>
/// </summary>
internal static class BabelAssemblies
{
    private static readonly Lazy<IReadOnlyList<Assembly>> Loaded = new(Load);

    /// <summary>تجميعات المنتج (src) دون مشاريع الاختبار.</summary>
    public static IReadOnlyList<Assembly> Product => Loaded.Value;

    /// <summary>تجميعة باسمها.</summary>
    public static Assembly Named(string simpleName) =>
        Product.FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"لم تُحمَّل التجميعة '{simpleName}'. / Assembly '{simpleName}' was not loaded.");

    /// <summary>كل الأنواع في تجميعات المنتج، بما فيها الداخلية والمتداخلة.</summary>
    public static IEnumerable<Type> AllTypes() => Product.SelectMany(SafeTypes);

    /// <summary>أنواع تجميعة واحدة.</summary>
    public static IEnumerable<Type> TypesOf(Assembly assembly) => SafeTypes(assembly);

    private static List<Assembly> Load()
    {
        string baseDirectory = AppContext.BaseDirectory;
        List<Assembly> assemblies = [];

        foreach (string path in Directory.EnumerateFiles(baseDirectory, "Babel.*.dll").OrderBy(static p => p, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(".Tests", StringComparison.Ordinal) || name.Equals("Babel.ArchitectureTests", StringComparison.Ordinal))
            {
                continue;
            }

            assemblies.Add(Assembly.LoadFrom(path));
        }

        if (assemblies.Count < 16)
        {
            throw new InvalidOperationException(
                $"عدد التجميعات المحمَّلة {assemblies.Count} أقل من المتوقّع؛ القواعد ستمرّ فراغاً. / "
                + $"Only {assemblies.Count} assemblies loaded; the rules would pass vacuously.");
        }

        return assemblies;
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static t => t is not null)!;
        }
    }
}
