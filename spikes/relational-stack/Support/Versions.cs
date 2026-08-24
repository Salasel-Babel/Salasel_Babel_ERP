using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace BabelRelationalSpike.Support;

/// <summary>
/// Prints the versions of everything actually loaded at runtime, together with
/// the TargetFramework attribute baked into each assembly - which is the real
/// proof that the package ships native net10.0 assemblies rather than being
/// consumed through a netstandard2.0 / net8.0 compatibility fallback.
/// </summary>
public static class Versions
{
    private static readonly (string Package, Type Probe)[] Probes =
    [
        ("WolverineFx",                           typeof(Wolverine.IMessageBus)),
        ("WolverineFx.Postgresql",                typeof(Wolverine.Postgresql.PostgresqlConfigurationExtensions)),
        ("WolverineFx.EntityFrameworkCore",       typeof(Wolverine.EntityFrameworkCore.IDbContextOutbox)),
        ("WolverineFx.RDBMS",                     typeof(Wolverine.RDBMS.DatabaseSettings)),
        ("Microsoft.EntityFrameworkCore",         typeof(Microsoft.EntityFrameworkCore.DbContext)),
        ("Microsoft.EntityFrameworkCore.Relational", typeof(Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions)),
        ("Npgsql.EntityFrameworkCore.PostgreSQL", typeof(NpgsqlDbContextOptionsBuilderExtensions)),
        ("Npgsql",                                typeof(Npgsql.NpgsqlConnection)),
        ("Weasel.Postgresql",                     typeof(Weasel.Postgresql.PostgresqlMigrator)),
        ("JasperFx",                              typeof(JasperFx.AutoCreate)),
    ];

    public static string Render()
    {
        var rows = new List<string[]>();
        foreach (var (pkg, probe) in Probes)
        {
            var asm = probe.Assembly;
            var name = asm.GetName();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            if (info.Contains('+')) info = info[..info.IndexOf('+')];
            var tfm = asm.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "(none)";
            rows.Add([pkg, info.Length > 0 ? info : name.Version!.ToString(), name.Name!, tfm,
                      tfm.Contains("v10.0") ? "yes" : "NO"]);
        }

        var headers = new[] { "PACKAGE", "VERSION", "ASSEMBLY", "TargetFrameworkAttribute", "net10.0?" };
        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=========================================================================");
        sb.AppendLine("  RESOLVED PACKAGE VERSIONS  -  إصدارات الحزم الفعلية");
        sb.AppendLine("=========================================================================");
        sb.AppendLine("  " + string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))));
        sb.AppendLine("  " + string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var r in rows)
            sb.AppendLine("  " + string.Join("  ", r.Select((v, i) => v.PadRight(widths[i]))));
        sb.AppendLine("=========================================================================");
        sb.AppendLine("  Marten assemblies loaded: " +
            (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("Marten") == true)
                ? "YES" : "NONE"));
        sb.AppendLine("=========================================================================");
        return sb.ToString();
    }
}
