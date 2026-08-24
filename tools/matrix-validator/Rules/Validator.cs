using SalaselBabel.MatrixValidator.Model;

namespace SalaselBabel.MatrixValidator.Rules;

/// <summary>
/// The whole point of this tool: the posting matrix and the chart of accounts are DATA, and data
/// that nothing checks rots. Every rule below corresponds to a way the seed can be wrong in a manner
/// that would only surface as a wrong number in a customer's financial statements.
/// كل قاعدة أدناه تقابل طريقة يمكن أن تفسد بها البيانات ولا تظهر إلا كرقم خاطئ في قوائم عميل.
/// </summary>
public sealed partial class Validator
{
    public static readonly IReadOnlyList<RuleDescription> Rules = new[]
    {
        new RuleDescription("V01", "دور حساب بلا تعيين", "An account role with no mapping"),
        new RuleDescription("V02", "سطر مصفوفة يشير إلى دور غير معروف", "A matrix line referencing an unknown role"),
        new RuleDescription("V03", "حدث لا يمكن أن تتوازن سطوره بالبناء", "An event whose lines cannot balance by construction"),
        new RuleDescription("V04", "سطر ترحيل يستهدف حساباً تجميعياً", "A posting line targeting a rollup account"),
        new RuleDescription("V05", "بُعد إلزامي ناقص على سطر", "A missing mandatory dimension"),
        new RuleDescription("V06", "اسم عربي أو إنجليزي ناقص", "A missing name_ar or name_en anywhere"),
        new RuleDescription("V07", "قاعدة شرطية لا يمكن أن تتحقق أبداً", "A conditional rule that can never fire"),
        new RuleDescription("V08", "تعيين دور إلى حساب غير موجود", "A role mapped to an account that does not exist"),
        new RuleDescription("V09", "شجرة حسابات مكسورة: أب مفقود أو دورة أو مستوى غير متسق", "A broken account tree: missing parent, cycle, or inconsistent level"),
        new RuleDescription("V10", "رمز حساب أو دور أو حدث مكرر", "A duplicate account, role, or event code"),
        new RuleDescription("V11", "دور معيَّن إلى حساب من تصنيف مخالف لتوقّعه", "A role mapped to an account of the wrong type"),
        new RuleDescription("V12", "تعبير مبلغ يستخدم متغيراً غير معلن", "An amount expression using an undeclared variable"),
        new RuleDescription("V13", "دفتر مساعد إلزامي غير معلن على السطر", "A mandatory subledger not declared on the line"),
        new RuleDescription("V14", "قاعدة حجب تشير إلى دور غير معروف", "A guard rule referencing an unknown role"),
        new RuleDescription("V15", "حساب معيَّن بدور وغير محمي من الحذف", "A role-mapped account not protected from deletion"),
        new RuleDescription("V16", "قيمة غير مسموحة في حقل محصور", "A value outside the allowed vocabulary of a field"),
        new RuleDescription("V17", "سيناريو أو سطر يشير إلى شرط غير معلن", "A scenario or line referencing an undeclared condition"),
        new RuleDescription("V18", "سيناريو بأقل من سطرين فعّالين", "A scenario with fewer than two active lines"),
        new RuleDescription("V19", "سطر تجميعي بلا محدِّد سليم", "A sweep or import line without a sound selector"),
        new RuleDescription("V20", "متغير مبلغ معلن ولا يستخدمه أي سطر", "A declared amount variable no line uses"),
        new RuleDescription("V21", "متغير مصفَّر وغير معلن في سيناريو", "A zeroed amount that was never declared"),
        new RuleDescription("V22", "متغير مصفَّر ومعرَّف بهوية في السيناريو نفسه", "An amount both zeroed and identity-defined in the same scenario"),
        new RuleDescription("V23", "بُعد أو دفتر مساعد غير معروف", "An unknown dimension or subledger type"),
        new RuleDescription("V24", "حساب مقابل بطبيعة غير معكوسة", "A contra account whose natural side is not inverted"),
        new RuleDescription("V25", "تحذير: دور لا يستخدمه أي سطر مصفوفة", "Warning: a role no matrix line uses"),
        new RuleDescription("V26", "نص يشير إلى رمز حساب غير موجود", "Prose referencing an account code that does not exist"),
    };

    private static readonly string[] AccountTypes = ["asset", "liability", "equity", "revenue", "expense"];
    private static readonly string[] Sides = ["debit", "credit"];
    private static readonly string[] Statuses = ["drafted", "proposed", "renamed"];
    private static readonly string[] CurrencyModes = ["any", "company_only", "fixed"];
    private static readonly string[] LineKinds = ["role", "sweep", "import", "manual", "mirror"];
    private static readonly string[] StatementSections =
        ["", "current_asset", "non_current_asset", "current_liability", "non_current_liability",
         "equity", "revenue", "cost_of_revenue", "operating_expense", "other_income", "other_expense"];

    private readonly Dataset _ds;
    private readonly List<Finding> _findings = new();

    public Validator(Dataset ds) => _ds = ds;

    public IReadOnlyList<Finding> Run()
    {
        foreach (var e in _ds.LoadErrors)
            Add("V16", Severity.Error, e, "تعذّر تحليل ملف بيانات", "A data file could not be parsed");

        CheckChartOfAccounts();
        CheckRoles();
        CheckGuardRules();
        foreach (var ev in _ds.Events) CheckEvent(ev);
        CheckRoleUsage();

        return _findings;
    }

    // -----------------------------------------------------------------------

    private void CheckChartOfAccounts()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in _ds.Accounts)
        {
            var w = $"accounts.csv:{a.SourceLine} ({a.Code})";

            if (!seen.Add(a.Code))
                Add("V10", Severity.Error, w, $"رمز الحساب {a.Code} مكرر", $"Duplicate account code {a.Code}");

            RequireBilingual("V06", w, a.NameAr, a.NameEn, "الحساب", "the account");

            if (!AccountTypes.Contains(a.AccountType))
                Add("V16", Severity.Error, w, $"تصنيف حساب غير مسموح: {a.AccountType}", $"Illegal account_type: {a.AccountType}");
            if (!Sides.Contains(a.NaturalSide))
                Add("V16", Severity.Error, w, $"طرف طبيعي غير مسموح: {a.NaturalSide}", $"Illegal natural_side: {a.NaturalSide}");
            if (!Statuses.Contains(a.Status))
                Add("V16", Severity.Error, w, $"حالة غير مسموحة: {a.Status}", $"Illegal status: {a.Status}");
            if (!CurrencyModes.Contains(a.CurrencyMode))
                Add("V16", Severity.Error, w, $"سلوك عملة غير مسموح: {a.CurrencyMode}", $"Illegal currency_mode: {a.CurrencyMode}");
            if (a.CurrencyMode == "fixed" && string.IsNullOrWhiteSpace(a.CurrencyCode))
                Add("V16", Severity.Error, w, "حساب بعملة محددة بلا رمز عملة", "An account fixed to a currency with no currency_code");
            if (!StatementSections.Contains(a.StatementSection))
                Add("V16", Severity.Error, w, $"قسم قائمة مالية غير مسموح: {a.StatementSection}", $"Illegal statement_section: {a.StatementSection}");

            if (a.IsPostable && string.IsNullOrWhiteSpace(a.StatementSection))
                Add("V16", Severity.Error, w, "حساب قابل للترحيل بلا قسم في القوائم المالية", "A postable account with no statement_section");
            if (!a.IsPostable && !string.IsNullOrWhiteSpace(a.StatementSection))
                Add("V16", Severity.Error, w, "حساب تجميعي يحمل قسماً في القوائم المالية", "A rollup account carrying a statement_section");

            // Numbering scheme: the tree must be derivable from the code itself.
            var expectedLevel = a.Code.Length == 4 ? 4 : a.Code.Length;
            if (a.Level != expectedLevel)
                Add("V09", Severity.Error, w, $"المستوى {a.Level} لا يطابق طول الرمز", $"level {a.Level} does not match the code length");
            if (a.Level == 4 != a.IsPostable)
                Add("V09", Severity.Error, w, "الترحيل مسموح على المستوى الرابع فقط", "Only level 4 accounts may be postable");

            if (a.Level == 1)
            {
                if (!string.IsNullOrEmpty(a.ParentCode))
                    Add("V09", Severity.Error, w, "تصنيف رئيسي له أب", "A top-level class must have no parent");
            }
            else
            {
                var expectedParent = a.Code[..(a.Level == 4 ? 3 : a.Level - 1)];
                if (a.ParentCode != expectedParent)
                    Add("V09", Severity.Error, w, $"الأب المعلن {a.ParentCode} لا يطابق المشتق من الرمز {expectedParent}",
                        $"declared parent {a.ParentCode} does not match {expectedParent} derived from the code");
                if (!_ds.AccountsByCode.ContainsKey(a.ParentCode))
                    Add("V09", Severity.Error, w, $"الحساب الأب {a.ParentCode} غير موجود", $"Parent account {a.ParentCode} does not exist");
            }

            if (a.Level > 1 && _ds.AccountsByCode.TryGetValue(a.ParentCode, out var parent) && parent.AccountType != a.AccountType)
                Add("V09", Severity.Error, w, "تصنيف الحساب يخالف تصنيف أبيه", "The account type differs from its parent's");

            var expectedNatural = a.AccountType is "asset" or "expense" ? "debit" : "credit";
            if (a.IsContra && a.NaturalSide == expectedNatural)
                Add("V24", Severity.Error, w, "حساب مقابل لكن طبيعته ليست معاكسة لتصنيفه", "Marked contra but its natural side is not inverted relative to its type");
            if (!a.IsContra && a.NaturalSide != expectedNatural)
                Add("V24", Severity.Error, w, "طبيعة الحساب معاكسة لتصنيفه دون وسم أنه حساب مقابل",
                    "The natural side is inverted relative to the account type without being marked as contra");

            foreach (var d in a.RequiredDimensions)
                if (_ds.Dimensions.All(x => x.Code != d))
                    Add("V23", Severity.Error, w, $"بُعد غير معروف: {d}", $"Unknown dimension: {d}");

            if (!string.IsNullOrEmpty(a.SubledgerType) && _ds.SubledgerTypes.All(x => x.Code != a.SubledgerType))
                Add("V23", Severity.Error, w, $"نوع دفتر مساعد غير معروف: {a.SubledgerType}", $"Unknown subledger type: {a.SubledgerType}");

            if (!a.IsPostable && a.RequiredDimensions.Count > 0)
                Add("V16", Severity.Error, w, "حساب تجميعي يفرض أبعاداً", "A rollup account demanding dimensions");
        }

        foreach (var d in _ds.Dimensions)
            RequireBilingual("V06", $"dimensions.csv:{d.SourceLine} ({d.Code})", d.NameAr, d.NameEn, "البُعد", "the dimension");
        foreach (var s in _ds.SubledgerTypes)
            RequireBilingual("V06", $"subledger-types.csv:{s.SourceLine} ({s.Code})", s.NameAr, s.NameEn, "الدفتر المساعد", "the subledger type");
    }

    // -----------------------------------------------------------------------

    private void CheckRoles()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in _ds.Roles)
        {
            var w = $"account-roles.csv:{r.SourceLine} ({r.Code})";
            if (!seen.Add(r.Code))
                Add("V10", Severity.Error, w, $"رمز الدور {r.Code} مكرر", $"Duplicate role code {r.Code}");
            RequireBilingual("V06", w, r.NameAr, r.NameEn, "الدور", "the role");

            var hasDefault = _ds.RoleMap.Any(m =>
                m.TenantId == Dataset.DefaultTenant && m.RoleCode == r.Code && m.Qualifier == "*");
            if (!hasDefault)
                Add("V01", Severity.Error, w,
                    $"الدور {r.Code} بلا تعيين افتراضي (المؤهل *) في خريطة الأدوار — لن يستطيع محرك الترحيل حلّه",
                    $"Role {r.Code} has no default (qualifier *) mapping — the posting engine could not resolve it");
        }

        foreach (var m in _ds.RoleMap)
        {
            var w = $"role-map.default.csv:{m.SourceLine} ({m.RoleCode}/{m.Qualifier})";
            if (!_ds.RolesByCode.TryGetValue(m.RoleCode, out var role))
            {
                Add("V02", Severity.Error, w, $"تعيين لدور غير معروف: {m.RoleCode}", $"Mapping for unknown role: {m.RoleCode}");
                continue;
            }
            if (!_ds.AccountsByCode.TryGetValue(m.AccountCode, out var acct))
            {
                Add("V08", Severity.Error, w, $"الحساب {m.AccountCode} غير موجود في دليل الحسابات", $"Account {m.AccountCode} is not in the chart of accounts");
                continue;
            }
            if (!acct.IsPostable)
                Add("V04", Severity.Error, w, $"الدور معيَّن إلى حساب تجميعي {acct.Code}", $"The role is mapped to rollup account {acct.Code}");
            if (!string.IsNullOrEmpty(role.ExpectedAccountType) && role.ExpectedAccountType != acct.AccountType)
                Add("V11", Severity.Error, w,
                    $"الدور يتوقع تصنيف {role.ExpectedAccountType} والحساب {acct.Code} تصنيفه {acct.AccountType}",
                    $"The role expects type {role.ExpectedAccountType} but account {acct.Code} is {acct.AccountType}");
            if (!string.IsNullOrEmpty(role.ExpectedSide) && role.ExpectedSide != acct.NaturalSide)
                Add("V11", Severity.Error, w,
                    $"الدور يتوقع طرفاً طبيعياً {role.ExpectedSide} وطبيعة الحساب {acct.Code} هي {acct.NaturalSide}",
                    $"The role expects natural side {role.ExpectedSide} but account {acct.Code} is {acct.NaturalSide}");
            if (!acct.IsProtected)
                Add("V15", Severity.Error, w,
                    $"الحساب {acct.Code} معيَّن بدور ومع ذلك غير محمي من الحذف",
                    $"Account {acct.Code} carries a role yet is not protected from deletion");
        }
    }

    /// <summary>
    /// A role nothing uses is not an error — a tenant may point a manual voucher at it — but it is
    /// worth seeing, because the usual cause is an event somebody forgot to write.
    /// دور لا يستخدمه أحد ليس خطأً، لكن سببه المعتاد حدثٌ نسي أحدهم كتابته.
    /// </summary>
    private void CheckRoleUsage()
    {
        var used = _ds.Events
            .SelectMany(e => e.Lines)
            .Select(l => l.Role)
            .Where(r => !string.IsNullOrEmpty(r))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var r in _ds.Roles.Where(r => !used.Contains(r.Code)))
            Add("V25", Severity.Warning, $"account-roles.csv:{r.SourceLine} ({r.Code})",
                $"الدور {r.Code} لا يستخدمه أي سطر مصفوفة — تحقق أن حدثاً لم يُنسَ",
                $"Role {r.Code} is used by no matrix line — check that an event was not forgotten");
    }

    private void CheckGuardRules()
    {
        foreach (var g in _ds.GuardRules)
        {
            var w = $"guard-rules.json ({g.RuleId})";
            RequireBilingual("V06", w, g.NameAr, g.NameEn, "قاعدة الحجب", "the guard rule");
            RequireBilingual("V06", w + " message", g.MessageAr, g.MessageEn, "رسالة قاعدة الحجب", "the guard rule message");
            if (g.AppliesTo?.Kind == "account_role" && g.AppliesTo.Role is { } role && !_ds.RolesByCode.ContainsKey(role))
                Add("V14", Severity.Error, w, $"قاعدة حجب تشير إلى دور غير معروف: {role}", $"Guard rule references unknown role: {role}");
        }
    }
}
