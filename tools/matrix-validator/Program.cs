using System.Text;
using System.Text.Json;
using SalaselBabel.MatrixValidator.Model;
using SalaselBabel.MatrixValidator.Rules;

namespace SalaselBabel.MatrixValidator;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var dataRoot = ArgValue(args, "--data") ?? FindDataRoot();
        var json = args.Contains("--json");
        var listRules = args.Contains("--rules");

        if (listRules)
        {
            foreach (var r in Validator.Rules)
                Console.WriteLine($"{r.Id}  {r.TitleEn}\n      {r.TitleAr}");
            return 0;
        }

        if (dataRoot is null || !Directory.Exists(dataRoot))
        {
            Console.Error.WriteLine("could not locate the data/ directory; pass --data <path>");
            return 2;
        }

        var findings = Validate(dataRoot, out var ds);
        var errors = findings.Count(f => f.Severity == Severity.Error);
        var warnings = findings.Count - errors;

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                data_root = Path.GetFullPath(dataRoot),
                accounts = ds.Accounts.Count,
                postable_accounts = ds.Accounts.Count(a => a.IsPostable),
                roles = ds.Roles.Count,
                role_mappings = ds.RoleMap.Count,
                events = ds.Events.Count,
                posting_events = ds.Events.Count(e => e.PostsEntry),
                guard_rules = ds.GuardRules.Count,
                errors,
                warnings,
                findings = findings.Select(f => new
                {
                    rule = f.RuleId, severity = f.Severity.ToString().ToLowerInvariant(),
                    where = f.Where, message_ar = f.MessageAr, message_en = f.MessageEn
                })
            }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            return errors == 0 ? 0 : 1;
        }

        Console.WriteLine($"data root         : {Path.GetFullPath(dataRoot)}");
        Console.WriteLine($"accounts          : {ds.Accounts.Count} ({ds.Accounts.Count(a => a.IsPostable)} postable / {ds.Accounts.Count(a => !a.IsPostable)} rollup)");
        Console.WriteLine($"dimensions        : {ds.Dimensions.Count}");
        Console.WriteLine($"subledger types   : {ds.SubledgerTypes.Count}");
        Console.WriteLine($"account roles     : {ds.Roles.Count}");
        Console.WriteLine($"role mappings     : {ds.RoleMap.Count}");
        Console.WriteLine($"business events   : {ds.Events.Count} ({ds.Events.Count(e => e.PostsEntry)} posting / {ds.Events.Count(e => !e.PostsEntry)} deliberately post no entry)");
        Console.WriteLine($"posting lines     : {ds.Events.Sum(e => e.Lines.Count)}");
        Console.WriteLine($"scenarios proven  : {ds.Events.Where(e => e.PostsEntry).Sum(e => Math.Max(e.Scenarios.Count, 1))}");
        Console.WriteLine($"guard rules       : {ds.GuardRules.Count}");
        Console.WriteLine();

        foreach (var f in findings) Console.WriteLine(f);

        if (errors == 0)
        {
            Console.WriteLine("OK — 0 errors. كل الفحوص اجتازت.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"FAILED — {errors} error(s), {warnings} warning(s).");
        return 1;
    }

    public static IReadOnlyList<Finding> Validate(string dataRoot, out Dataset ds)
    {
        ds = Dataset.Load(dataRoot);
        return new Validator(ds).Run();
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Walks up from the current directory looking for a data/chart-of-accounts folder.</summary>
    public static string? FindDataRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "chart-of-accounts"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
