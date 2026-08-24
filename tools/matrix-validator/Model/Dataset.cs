using System.Globalization;
using System.Text.Json;

namespace SalaselBabel.MatrixValidator.Model;

/// <summary>
/// Everything the validator needs, loaded from the seed files on disk.
/// كل ما تحتاجه أداة التحقق، محمّلاً من ملفات البيانات التأسيسية.
/// </summary>
public sealed class Dataset
{
    public const string DefaultTenant = "__default__";

    public List<Account> Accounts { get; init; } = new();
    public List<Dimension> Dimensions { get; init; } = new();
    public List<SubledgerType> SubledgerTypes { get; init; } = new();
    public List<AccountRole> Roles { get; init; } = new();
    public List<RoleMapping> RoleMap { get; init; } = new();
    public List<PostingEvent> Events { get; init; } = new();
    public List<GuardRule> GuardRules { get; init; } = new();
    public List<string> LoadErrors { get; init; } = new();

    public Dictionary<string, Account> AccountsByCode { get; private set; } = new();
    public Dictionary<string, AccountRole> RolesByCode { get; private set; } = new();

    public void Index()
    {
        AccountsByCode = new Dictionary<string, Account>(StringComparer.Ordinal);
        foreach (var a in Accounts) AccountsByCode.TryAdd(a.Code, a);
        RolesByCode = new Dictionary<string, AccountRole>(StringComparer.Ordinal);
        foreach (var r in Roles) RolesByCode.TryAdd(r.Code, r);
    }

    /// <summary>Resolves a role (with an optional qualifier) exactly the way the engine must.</summary>
    public Account? Resolve(string role, string qualifier = "*", string tenant = DefaultTenant)
    {
        var exact = RoleMap.FirstOrDefault(m =>
            m.TenantId == tenant && m.RoleCode == role && m.Qualifier == qualifier);
        if (exact is not null && AccountsByCode.TryGetValue(exact.AccountCode, out var a1)) return a1;

        var fallback = RoleMap.FirstOrDefault(m =>
            m.TenantId == tenant && m.RoleCode == role && m.Qualifier == "*");
        if (fallback is not null && AccountsByCode.TryGetValue(fallback.AccountCode, out var a2)) return a2;

        return null;
    }

    /// <summary>
    /// Every account a role can land on for a given line. A line whose qualifier is a constant
    /// resolves to exactly one account; a line whose qualifier comes from the document at run time
    /// can land on any account mapped to that role, so the checks must hold for all of them.
    /// كل حساب يمكن أن يصل إليه الدور في سطر بعينه — المؤهل الثابت يعطي حساباً واحداً والمؤهل الديناميكي
    /// يعطي كل حسابات الدور، والفحص يجب أن يصحّ عليها جميعاً.
    /// </summary>
    public IReadOnlyList<Account> Candidates(string role, string? qualifierSource, string tenant = DefaultTenant)
    {
        if (qualifierSource is not null && qualifierSource.StartsWith("constant:", StringComparison.Ordinal))
        {
            var q = qualifierSource["constant:".Length..];
            var a = Resolve(role, q, tenant);
            return a is null ? Array.Empty<Account>() : new[] { a };
        }

        if (qualifierSource is null)
        {
            var a = Resolve(role, "*", tenant);
            return a is null ? Array.Empty<Account>() : new[] { a };
        }

        var all = RoleMap
            .Where(m => m.TenantId == tenant && m.RoleCode == role)
            .Select(m => AccountsByCode.TryGetValue(m.AccountCode, out var acct) ? acct : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct()
            .ToList();
        return all;
    }

    public static Dataset Load(string dataRoot)
    {
        var ds = new Dataset();

        var coa = Path.Combine(dataRoot, "chart-of-accounts");
        var mtx = Path.Combine(dataRoot, "posting-matrix");

        foreach (var r in Csv.Read(Path.Combine(coa, "accounts.csv")))
            ds.Accounts.Add(new Account
            {
                Code = r["code"], NameAr = r["name_ar"], NameEn = r["name_en"],
                ParentCode = r["parent_code"],
                Level = int.TryParse(r["level"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv) ? lv : 0,
                AccountType = r["account_type"], NaturalSide = r["natural_side"],
                IsPostable = Bool(r["is_postable"]), IsContra = Bool(r["is_contra"]),
                StatementSection = r["statement_section"], SubledgerType = r["subledger_type"],
                RequiredDimensions = Split(r["required_dimensions"]),
                CurrencyMode = r["currency_mode"], CurrencyCode = r["currency_code"],
                IsProtected = Bool(r["is_protected"]), Status = r["status"],
                SourceRef = r["source_ref"], CaveatAr = r["caveat_ar"], CaveatEn = r["caveat_en"],
                SourceLine = int.Parse(r["__line__"], CultureInfo.InvariantCulture)
            });

        foreach (var r in Csv.Read(Path.Combine(coa, "dimensions.csv")))
            ds.Dimensions.Add(new Dimension
            {
                Code = r["dimension_code"], NameAr = r["name_ar"], NameEn = r["name_en"],
                SourceLine = int.Parse(r["__line__"], CultureInfo.InvariantCulture)
            });

        foreach (var r in Csv.Read(Path.Combine(coa, "subledger-types.csv")))
            ds.SubledgerTypes.Add(new SubledgerType
            {
                Code = r["subledger_code"], NameAr = r["name_ar"], NameEn = r["name_en"],
                SourceLine = int.Parse(r["__line__"], CultureInfo.InvariantCulture)
            });

        foreach (var r in Csv.Read(Path.Combine(mtx, "account-roles.csv")))
            ds.Roles.Add(new AccountRole
            {
                Code = r["role_code"], NameAr = r["name_ar"], NameEn = r["name_en"],
                ExpectedAccountType = r["expected_account_type"], ExpectedSide = r["expected_side"],
                Status = r["status"], SourceLine = int.Parse(r["__line__"], CultureInfo.InvariantCulture)
            });

        foreach (var r in Csv.Read(Path.Combine(mtx, "role-map.default.csv")))
            ds.RoleMap.Add(new RoleMapping
            {
                TenantId = r["tenant_id"], RoleCode = r["role_code"], Qualifier = r["qualifier"],
                AccountCode = r["account_code"], Status = r["status"],
                SourceLine = int.Parse(r["__line__"], CultureInfo.InvariantCulture)
            });

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
        var eventsDir = Path.Combine(mtx, "events");
        foreach (var f in Directory.GetFiles(eventsDir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            try
            {
                var file = JsonSerializer.Deserialize<EventFile>(File.ReadAllText(f), opts)!;
                foreach (var e in file.Events)
                {
                    e.SourceFile = Path.GetFileName(f);
                    ds.Events.Add(e);
                }
            }
            catch (JsonException ex)
            {
                ds.LoadErrors.Add($"{Path.GetFileName(f)}: {ex.Message}");
            }
        }

        var guardPath = Path.Combine(mtx, "guard-rules.json");
        if (File.Exists(guardPath))
        {
            try
            {
                var gf = JsonSerializer.Deserialize<GuardRuleFile>(File.ReadAllText(guardPath), opts)!;
                ds.GuardRules.AddRange(gf.Rules);
            }
            catch (JsonException ex)
            {
                ds.LoadErrors.Add($"guard-rules.json: {ex.Message}");
            }
        }

        ds.Index();
        return ds;
    }

    private static bool Bool(string s) => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Split(string s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<string>()
            : s.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
