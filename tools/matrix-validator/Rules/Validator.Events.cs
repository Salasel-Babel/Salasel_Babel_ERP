using SalaselBabel.MatrixValidator.Model;

namespace SalaselBabel.MatrixValidator.Rules;

public sealed partial class Validator
{
    private static readonly string[] EventStatuses = ["drafted", "proposed"];
    private static readonly string[] SweepSelectors =
        ["account_class", "foreign_currency_balances", "imported_opening_lines",
         "operator_entered_lines", "source_entry_lines"];

    /// <summary>Marks a line whose subledger is whichever one the resolved account demands.</summary>
    public const string ResolvedSubledger = "resolved";

    private readonly HashSet<string> _seenEventCodes = new(StringComparer.Ordinal);

    private void CheckEvent(PostingEvent ev)
    {
        var w = $"{ev.SourceFile} ({ev.EventCode})";

        if (!_seenEventCodes.Add(ev.EventCode))
            Add("V10", Severity.Error, w, $"رمز الحدث {ev.EventCode} مكرر", $"Duplicate event code {ev.EventCode}");

        RequireBilingual("V06", w, ev.NameAr, ev.NameEn, "الحدث", "the event");
        if (ev.Trigger is null) Add("V06", Severity.Error, w, "الحدث بلا وصف إطلاق", "The event has no trigger description");
        else RequireBilingual("V06", w + " trigger", ev.Trigger.NameAr, ev.Trigger.NameEn, "إطلاق الحدث", "the event trigger");
        if (ev.Reversal is not null)
            RequireBilingual("V06", w + " reversal", ev.Reversal.NameAr, ev.Reversal.NameEn, "طريقة العكس", "the reversal method");
        if (ev.Precondition is not null)
            RequireBilingual("V06", w + " precondition", ev.Precondition.NameAr, ev.Precondition.NameEn, "الشرط المسبق", "the precondition");
        foreach (var c in ev.Caveats)
            RequireBilingual("V06", w + " caveat " + c.Ref, c.TextAr, c.TextEn, "التحفّظ", "the caveat");

        if (!EventStatuses.Contains(ev.Status))
            Add("V16", Severity.Error, w, $"حالة حدث غير مسموحة: {ev.Status}", $"Illegal event status: {ev.Status}");

        foreach (var (name, a) in ev.Amounts)
        {
            RequireBilingual("V06", $"{w} amount {name}", a.NameAr, a.NameEn, "متغير المبلغ", "the amount variable");
            RequireBilingual("V06", $"{w} amount {name} derivation", a.DerivationAr, a.DerivationEn,
                "طريقة اشتقاق المبلغ", "the amount derivation");
        }
        foreach (var (name, c) in ev.Conditions)
        {
            RequireBilingual("V06", $"{w} condition {name}", c.NameAr, c.NameEn, "الشرط", "the condition");
            if (string.IsNullOrWhiteSpace(c.Expression))
                Add("V06", Severity.Error, $"{w} condition {name}", "الشرط بلا تعبير", "The condition has no expression");
        }
        foreach (var s in ev.Scenarios)
            RequireBilingual("V06", $"{w} scenario {s.Code}", s.NameAr, s.NameEn, "السيناريو", "the scenario");

        // An event that deliberately posts nothing is a first-class statement of accounting policy
        // (07-real-estate.md §14.1 and §14.3-b). It must be explicit and it must carry no lines.
        if (!ev.PostsEntry)
        {
            if (ev.Lines.Count > 0)
                Add("V18", Severity.Error, w, "حدث معلَن أنه لا يولّد قيداً ومع ذلك يحمل سطوراً",
                    "An event declared to post no entry nevertheless carries lines");
            return;
        }

        if (ev.Lines.Count == 0)
        {
            Add("V18", Severity.Error, w, "حدث يولّد قيداً بلا سطور", "An entry-posting event with no lines");
            return;
        }

        CheckProseAccountReferences(ev, w);
        CheckLines(ev, w);
        CheckConditionsAndScenarios(ev, w);
        CheckBalance(ev, w);
    }

    private static readonly System.Text.RegularExpressions.Regex AccountCodeInProse =
        new(@"(?<![0-9])[1-5][0-9]{3}(?![0-9])", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// A matrix line never names an account, but a caveat or a note may cite one so the reviewer can
    /// find it. Those citations rot silently when an account is renumbered, and a stale citation in a
    /// caveat is worse than none: it sends a reviewer to an account that no longer exists.
    /// السطر لا يسمّي حساباً أبداً، لكن التحفّظ قد يستشهد برمز — والاستشهاد البائت يرسل المراجع إلى حساب لم يعد موجوداً.
    /// </summary>
    private void CheckProseAccountReferences(PostingEvent ev, string w)
    {
        var prose = new List<(string field, string text)>();
        if (ev.Trigger is not null) { prose.Add(("trigger", ev.Trigger.NameAr)); prose.Add(("trigger", ev.Trigger.NameEn)); }
        if (ev.Precondition is not null) { prose.Add(("precondition", ev.Precondition.NameAr)); prose.Add(("precondition", ev.Precondition.NameEn)); }
        if (ev.Reversal is not null) { prose.Add(("reversal", ev.Reversal.NameAr)); prose.Add(("reversal", ev.Reversal.NameEn)); }
        foreach (var c in ev.Caveats) { prose.Add(("caveat " + c.Ref, c.TextAr)); prose.Add(("caveat " + c.Ref, c.TextEn)); }
        foreach (var l in ev.Lines) { prose.Add(($"line {l.LineNo} note", l.NoteAr)); prose.Add(($"line {l.LineNo} note", l.NoteEn)); }
        foreach (var (name, a) in ev.Amounts)
        {
            prose.Add(($"amount {name}", a.DerivationAr));
            prose.Add(($"amount {name}", a.DerivationEn));
        }

        foreach (var (field, text) in prose)
        {
            if (string.IsNullOrEmpty(text)) continue;
            foreach (System.Text.RegularExpressions.Match m in AccountCodeInProse.Matches(text))
                if (!_ds.AccountsByCode.ContainsKey(m.Value))
                    Add("V26", Severity.Error, $"{w} {field}",
                        $"النص يشير إلى رمز الحساب {m.Value} وهو غير موجود في دليل الحسابات",
                        $"The text cites account code {m.Value}, which is not in the chart of accounts");
        }
    }

    private void CheckLines(PostingEvent ev, string w)
    {
        var declared = new HashSet<string>(ev.Amounts.Keys, StringComparer.Ordinal);
        var identityDefined = new HashSet<string>(ev.Identities.Keys, StringComparer.Ordinal);
        foreach (var s in ev.Scenarios) identityDefined.UnionWith(s.Identities.Keys);
        var usedVars = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in ev.Lines)
        {
            var lw = $"{w} line {line.LineNo}";

            if (!LineKinds.Contains(line.LineKind))
            {
                Add("V16", Severity.Error, lw, $"نوع سطر غير مسموح: {line.LineKind}", $"Illegal line_kind: {line.LineKind}");
                continue;
            }

            var expectedSides = line.LineKind == "mirror" ? new[] { "mirror" } : Sides;
            if (!expectedSides.Contains(line.Side))
                Add("V16", Severity.Error, lw, $"طرف غير مسموح: {line.Side}", $"Illegal side: {line.Side}");

            // ---- amount expression ----
            LinearExpression? expr = null;
            try { expr = ExpressionParser.Parse(line.Amount); }
            catch (ExpressionException ex)
            {
                Add("V12", Severity.Error, lw, $"تعبير مبلغ غير صالح: {ex.Message}", $"Invalid amount expression: {ex.Message}");
            }

            if (expr is not null)
                foreach (var v in expr.Variables)
                {
                    usedVars.Add(v);
                    if (!declared.Contains(v) && !identityDefined.Contains(v))
                        Add("V12", Severity.Error, lw,
                            $"المتغير {v} مستخدم في المبلغ وغير معلن في amounts",
                            $"Variable {v} is used in the amount but not declared in amounts");
                }

            // ---- role lines resolve to a real, postable, correctly-dimensioned account ----
            if (line.LineKind == "role")
            {
                if (string.IsNullOrWhiteSpace(line.Role))
                {
                    Add("V02", Severity.Error, lw, "سطر من نوع role بلا دور", "A role line with no role");
                    continue;
                }
                if (!_ds.RolesByCode.ContainsKey(line.Role))
                {
                    Add("V02", Severity.Error, lw, $"دور غير معروف: {line.Role}", $"Unknown role: {line.Role}");
                    continue;
                }

                var candidates = _ds.Candidates(line.Role, line.QualifierSource);
                if (candidates.Count == 0)
                {
                    Add("V01", Severity.Error, lw, $"الدور {line.Role} لا يُحلّ إلى حساب", $"Role {line.Role} does not resolve to an account");
                    continue;
                }

                foreach (var account in candidates)
                {
                    if (!account.IsPostable)
                        Add("V04", Severity.Error, lw,
                            $"الدور {line.Role} يُحلّ إلى الحساب التجميعي {account.Code} — الترحيل على الحساب التفصيلي فقط",
                            $"Role {line.Role} resolves to rollup account {account.Code} — posting is only on detail accounts");

                    foreach (var d in account.RequiredDimensions)
                        if (!line.Dimensions.Contains(d))
                            Add("V05", Severity.Error, lw,
                                $"الحساب {account.Code} يفرض البُعد «{d}» والسطر لا يعلنه",
                                $"Account {account.Code} requires dimension '{d}' and the line does not declare it");
                }

                // ---- subledger ----
                var subledgers = candidates.Select(a => a.SubledgerType).Where(x => x != "none").Distinct().ToList();
                var declaredSub = string.IsNullOrEmpty(line.Subledger) ? null : line.Subledger;

                if (declaredSub == ResolvedSubledger)
                {
                    if (line.QualifierSource is null || line.QualifierSource.StartsWith("constant:", StringComparison.Ordinal))
                        Add("V13", Severity.Error, lw,
                            "الدفتر المساعد «resolved» مسموح فقط حين يأتي المؤهل من المستند وقت التشغيل",
                            "Subledger 'resolved' is only allowed when the qualifier comes from the document at run time");
                    else if (subledgers.Count == 0)
                        Add("V13", Severity.Error, lw,
                            "السطر يعلن دفتراً مساعداً «resolved» ولا يحتاج أي حساب من حسابات الدور دفتراً مساعداً",
                            "The line declares subledger 'resolved' but no account this role can reach needs one");
                }
                else if (subledgers.Count == 0)
                {
                    if (declaredSub is not null)
                        Add("V13", Severity.Error, lw,
                            $"السطر يعلن دفتراً مساعداً ولا حساب من حسابات الدور {line.Role} له دفتر مساعد",
                            $"The line declares a subledger but no account reachable through role {line.Role} has one");
                }
                else if (subledgers.Count == 1)
                {
                    if (declaredSub != subledgers[0])
                        Add("V13", Severity.Error, lw,
                            $"الحساب المستهدف له دفتر مساعد إلزامي «{subledgers[0]}» والسطر يعلن «{declaredSub ?? "لا شيء"}» — تنكسر المطابقة اليومية",
                            $"The target account has mandatory subledger '{subledgers[0]}' and the line declares '{declaredSub ?? "none"}' — the daily reconciliation breaks");
                }
                else
                {
                    Add("V13", Severity.Error, lw,
                        $"حسابات الدور {line.Role} تحمل دفاتر مساعدة مختلفة ({string.Join(", ", subledgers)}) — يجب أن يعلن السطر «{ResolvedSubledger}»",
                        $"Accounts reachable through role {line.Role} carry different subledgers ({string.Join(", ", subledgers)}) — the line must declare '{ResolvedSubledger}'");
                }

                foreach (var d in line.Dimensions)
                    if (_ds.Dimensions.All(x => x.Code != d))
                        Add("V23", Severity.Error, lw, $"بُعد غير معروف: {d}", $"Unknown dimension: {d}");

                if (!string.IsNullOrEmpty(line.Subledger) && line.Subledger != ResolvedSubledger
                    && _ds.SubledgerTypes.All(x => x.Code != line.Subledger))
                    Add("V23", Severity.Error, lw, $"نوع دفتر مساعد غير معروف: {line.Subledger}", $"Unknown subledger type: {line.Subledger}");
            }
            else
            {
                // sweep / import / manual / mirror
                if (line.Sweep is null)
                {
                    Add("V19", Severity.Error, lw, "سطر تجميعي بلا محدِّد", "A sweep or import line with no selector");
                    continue;
                }
                if (!SweepSelectors.Contains(line.Sweep.Selector))
                    Add("V19", Severity.Error, lw, $"محدِّد غير مسموح: {line.Sweep.Selector}", $"Illegal selector: {line.Sweep.Selector}");
                if (!line.Sweep.PostableOnly)
                    Add("V19", Severity.Error, lw,
                        "المحدِّد لا يقصر الاختيار على الحسابات القابلة للترحيل",
                        "The selector does not restrict itself to postable accounts");
                RequireBilingual("V06", lw + " selector", line.Sweep.NameAr, line.Sweep.NameEn,
                    "محدِّد السطر التجميعي", "the sweep selector");
                if (line.Sweep.Selector == "account_class" && line.Sweep.Classes.Count == 0)
                    Add("V19", Severity.Error, lw, "محدِّد بالتصنيف بلا تصنيفات", "A class selector with no classes");
                foreach (var cls in line.Sweep.Classes)
                    if (!_ds.AccountsByCode.TryGetValue(cls, out var root) || root.Level != 1)
                        Add("V19", Severity.Error, lw, $"تصنيف رئيسي غير معروف: {cls}", $"Unknown top-level class: {cls}");
            }
        }

        foreach (var name in ev.Amounts.Keys)
            if (!usedVars.Contains(name))
                Add("V20", Severity.Error, w,
                    $"متغير المبلغ {name} معلن ولا يستخدمه أي سطر — إما أنه زائد أو أن سطراً ناقص",
                    $"Amount variable {name} is declared and used by no line — either it is redundant or a line is missing");
    }

    private void CheckConditionsAndScenarios(PostingEvent ev, string w)
    {
        var conditions = new HashSet<string>(ev.Conditions.Keys, StringComparer.Ordinal);

        foreach (var line in ev.Lines)
            foreach (var c in line.WhenConditions())
                if (!conditions.Contains(c))
                    Add("V17", Severity.Error, $"{w} line {line.LineNo}",
                        $"السطر مشروط بشرط غير معلن: {c}", $"The line is gated on an undeclared condition: {c}");

        foreach (var s in ev.Scenarios)
        {
            var sw = $"{w} scenario {s.Code}";
            foreach (var c in s.TrueConditions)
                if (!conditions.Contains(c))
                    Add("V17", Severity.Error, sw, $"سيناريو يشير إلى شرط غير معلن: {c}", $"The scenario references an undeclared condition: {c}");
            foreach (var z in s.ZeroAmounts)
                if (!ev.Amounts.ContainsKey(z))
                    Add("V21", Severity.Error, sw, $"تصفير متغير غير معلن: {z}", $"Zeroing an undeclared amount: {z}");
            foreach (var id in s.Identities.Keys)
                if (s.ZeroAmounts.Contains(id))
                    Add("V22", Severity.Error, sw,
                        $"المتغير {id} مصفَّر ومعرَّف بهوية في السيناريو نفسه — تعريفان متناقضان",
                        $"Variable {id} is both zeroed and identity-defined in the same scenario — two contradictory definitions");
        }

        // Any event with conditional lines must enumerate its scenarios: an unenumerated
        // condition is a claim nobody has checked.
        var hasConditionalLines = ev.Lines.Any(l => l.WhenConditions().Count > 0);
        if (hasConditionalLines && ev.Scenarios.Count == 0)
            Add("V07", Severity.Error, w,
                "الحدث فيه سطور مشروطة ولا يعلن سيناريوهات — لا يمكن إثبات توازنه في كل الحالات",
                "The event has conditional lines but declares no scenarios — its balance cannot be proven in every case");

        if (conditions.Count > 0 && ev.Scenarios.Count == 0)
            Add("V07", Severity.Error, w, "الحدث يعلن شروطاً ولا يعلن سيناريوهات", "The event declares conditions but no scenarios");

        var scenarios = ev.Scenarios.Count > 0
            ? ev.Scenarios
            : new List<Scenario> { new() { Code = "__default__", NameAr = "افتراضي", NameEn = "default" } };

        // V07 — every condition must be true in at least one scenario and false in at least one,
        // and every conditional line must be active in at least one scenario. A rule that can never
        // fire is dead data that a reviewer will nevertheless believe.
        foreach (var c in conditions)
        {
            if (scenarios.All(s => !s.TrueConditions.Contains(c)))
                Add("V07", Severity.Error, w,
                    $"الشرط {c} لا يتحقق في أي سيناريو معلن — القاعدة المعتمدة عليه لا يمكن أن تعمل أبداً",
                    $"Condition {c} is true in no declared scenario — any rule depending on it can never fire");
            if (scenarios.All(s => s.TrueConditions.Contains(c)))
                Add("V07", Severity.Error, w,
                    $"الشرط {c} متحقق في كل سيناريو معلن — فهو ليس شرطاً بل ثابت",
                    $"Condition {c} is true in every declared scenario — it is not a condition, it is a constant");
        }

        foreach (var line in ev.Lines)
        {
            var when = line.WhenConditions();
            if (when.Count == 0) continue;
            if (!scenarios.Any(s => when.All(c => s.TrueConditions.Contains(c))))
                Add("V07", Severity.Error, $"{w} line {line.LineNo}",
                    "السطر المشروط لا يعمل في أي سيناريو معلن", "The conditional line is active in no declared scenario");
        }
    }

    private void CheckBalance(PostingEvent ev, string w)
    {
        var scenarios = ev.Scenarios.Count > 0
            ? ev.Scenarios
            : new List<Scenario> { new() { Code = "__default__", NameAr = "افتراضي", NameEn = "default" } };

        foreach (var s in scenarios)
        {
            var sw = $"{w} scenario {s.Code}";

            var active = ev.Lines
                .Where(l => l.WhenConditions().All(c => s.TrueConditions.Contains(c)))
                .ToList();

            // A single mirror line reproduces a balanced source entry with every side flipped:
            // balance is inherited, not asserted.
            if (active.Count == 1 && active[0].LineKind == "mirror") continue;

            if (active.Count < 2)
            {
                Add("V18", Severity.Error, sw,
                    $"السيناريو ينتج {active.Count} سطراً فعّالاً — القيد يحتاج سطرين على الأقل",
                    $"The scenario yields {active.Count} active line(s) — an entry needs at least two");
                continue;
            }

            var identities = new Dictionary<string, string>(ev.Identities, StringComparer.Ordinal);
            foreach (var (k, v) in s.Identities) identities[k] = v;

            LinearExpression total;
            try
            {
                total = LinearExpression.Zero();
                foreach (var line in active)
                {
                    var e = Reduce(ExpressionParser.Parse(line.Amount), identities, s.ZeroAmounts, sw);
                    total = line.Side == "debit" ? total.Add(e) : total.Subtract(e);
                }
            }
            catch (ExpressionException ex)
            {
                Add("V03", Severity.Error, sw, $"تعذّر إثبات التوازن: {ex.Message}", $"Balance could not be proven: {ex.Message}");
                continue;
            }

            if (!total.IsZero)
                Add("V03", Severity.Error, sw,
                    $"مجموع المدين ناقص مجموع الدائن لا يؤول إلى الصفر — الفرق: {total}",
                    $"Sum of debits minus sum of credits does not reduce to zero — residual: {total}");
        }
    }

    /// <summary>
    /// Zeroes the scenario's zeroed amounts, then substitutes identities to a fixed point.
    /// Order matters: an amount the scenario declares to be zero is zero everywhere, including
    /// inside an identity that mentions it.
    /// </summary>
    private static LinearExpression Reduce(
        LinearExpression e,
        IReadOnlyDictionary<string, string> identities,
        IReadOnlyCollection<string> zeroed,
        string where)
    {
        foreach (var z in zeroed) e = e.Substitute(z, LinearExpression.Zero());

        for (var pass = 0; pass < 32; pass++)
        {
            var target = e.Variables.FirstOrDefault(v => identities.ContainsKey(v));
            if (target is null) return e;

            var replacement = ExpressionParser.Parse(identities[target]);
            foreach (var z in zeroed) replacement = replacement.Substitute(z, LinearExpression.Zero());
            if (replacement.Variables.Contains(target))
                throw new ExpressionException($"identity for '{target}' is self-referential at {where}");

            e = e.Substitute(target, replacement);
        }

        throw new ExpressionException($"identity substitution did not converge at {where} — a cycle is likely");
    }

    // -----------------------------------------------------------------------

    private void Add(string ruleId, Severity sev, string where, string ar, string en) =>
        _findings.Add(new Finding(ruleId, sev, where, ar, en));

    private void RequireBilingual(string ruleId, string where, string ar, string en, string whatAr, string whatEn)
    {
        if (string.IsNullOrWhiteSpace(ar))
            Add(ruleId, Severity.Error, where, $"{whatAr} بلا name_ar", $"{whatEn} is missing name_ar / the Arabic text");
        if (string.IsNullOrWhiteSpace(en))
            Add(ruleId, Severity.Error, where, $"{whatAr} بلا name_en", $"{whatEn} is missing name_en / the English text");
    }
}
