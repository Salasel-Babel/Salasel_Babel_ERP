using System.Globalization;
using Babel.Canonicalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Accounts;
using Babel.Ledger.PostingMatrix;
using Babel.SharedKernel;

namespace Babel.Ledger.Posting;

/// <summary>
/// خطّ الحلّ: من حدث تجاري إلى قيد جاهز للكتابة.
/// <para>
/// الترتيب مقصود، وكل خطوة ترفض بصوت عالٍ:
/// <list type="number">
///   <item>قالب الحدث من المصفوفة (أو سطور الطلب الصريحة).</item>
///   <item>تقييم الشروط — وشرطٌ لا يُقيَّم <b>يوقف</b> ولا يُعامل معاملة «خطأ».</item>
///   <item>حساب المبالغ من تعابير خطية على مفردات الحدث.</item>
///   <item>حلّ كل دور عبر خريطة هذه الشركة، مع احترام المؤهّل.</item>
///   <item>الأبعاد الإلزامية على الحساب (GR-COA-002) والحساب التفصيلي (GR-COA-001).</item>
///   <item>قواعد الحجب من <c>guard-rules.json</c> — ومنها GR-RE-001.</item>
///   <item><c>AssertBalanced</c> قبل كتابة أي صفّ.</item>
/// </list>
/// </para>
/// </summary>
internal static class PostingPlanner
{
    private const string DefaultQualifier = "*";

    public static Result<PostingPlan> Plan(
        PostingRequest request,
        CompanyReference reference,
        MatrixCatalog matrix,
        string companyCurrency,
        DateTime postedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(matrix);

        if (!request.Tenant.IsAssigned)
        {
            return Result<PostingPlan>.Failure(PostingErrors.MissingTenant);
        }

        if (!request.IdempotencyKey.IsAssigned)
        {
            return Result<PostingPlan>.Failure(PostingErrors.MissingIdempotencyKey);
        }

        // ── رمز الحدث جزء من هوية الترحيل ─────────────────────────────────
        // ‏D-3 · ADR-0016.
        // المستند الواحد يُنتج حدثين عند الإطلاق نفسه في حالات يومية، ورمزٌ
        // فارغ يجعلهما هوية واحدة فيُبتلع الثاني بصمت. والرفض هنا هو الطبقة
        // الثالثة؛ الأولى قيد التحقق ck_journal_entry_event_code في قاعدة
        // البيانات، والثانية حارس داخل ledger.post_entry نفسها.
        if (!request.Event.IsAssigned)
        {
            return Result<PostingPlan>.Failure(PostingErrors.MissingEventCode);
        }

        Dictionary<string, string> facts = new(StringComparer.Ordinal);
        foreach (PostingFact fact in request.Facts)
        {
            facts[fact.Path] = fact.Value;
        }

        Dictionary<string, decimal> amounts = new(StringComparer.Ordinal);
        foreach (PostingAmount amount in request.Amounts)
        {
            amounts[amount.Name] = amount.Value.Amount;
        }

        Dictionary<string, string> dimensions = new(StringComparer.Ordinal);
        foreach (PostingDimension dimension in request.Dimensions)
        {
            dimensions[dimension.Name] = dimension.Value;
        }

        string currency = request.Currency.IsAssigned ? request.Currency.Value : companyCurrency;
        decimal fxRate = request.ExchangeRate <= 0m ? 1m : request.ExchangeRate;

        // ‏Event مضمون الآن غير فارغ بالفحص أعلاه، فالمسار الصريح لا يُختار إلا
        // إذا حمل الطلب رمز حدث **وسطوراً صريحة** معاً: الرمز يعطي الهوية،
        // والسطور تعطي المحتوى. وطلبٌ بسطور بلا رمز حدث مرفوض قبل هذا السطر.
        Result<List<DraftLine>> draft = request.Lines.Count > 0
            ? FromExplicitLines(request, matrix, dimensions)
            : FromEvent(request, matrix, facts, amounts, dimensions);

        if (draft.IsFailure)
        {
            return Result<PostingPlan>.Failure(draft.Errors);
        }

        // ── الفترة المالية من مداها لا من السنة الميلادية ──────────────────
        PeriodFacts? period = reference.PeriodOf(request.DocumentDate);
        if (period is null)
        {
            return Result<PostingPlan>.Failure(PostingErrors.NoFiscalPeriod(request.DocumentDate));
        }

        if (period.State == "permanently_closed")
        {
            return Result<PostingPlan>.Failure(PostingErrors.PermanentlyClosedPeriod(period.PeriodCode));
        }

        if (period.State == "closed" && request.ClosedPeriodAuthorisation is null)
        {
            return Result<PostingPlan>.Failure(PostingErrors.ClosedPeriod(period.PeriodCode));
        }

        List<Error> errors = [];
        List<PlannedLine> lines = [];

        foreach (DraftLine line in draft.Value)
        {
            string? accountCode = reference.ResolveRole(line.RoleCode, line.Qualifier);
            if (accountCode is null)
            {
                errors.Add(PostingErrors.UnresolvedRole(line.RoleCode, line.Qualifier));
                continue;
            }

            // النوع internal الذي يجعل «الوحدة لا تستطيع تسمية حساب» بنيوياً:
            // رمز الحساب لا يُبنى إلا هنا، وبأرقام ASCII فقط — الأرقام العربية-
            // الهندية تبدو صحيحة وتُجزَّأ خطأ.
            AccountCode typed;
            try
            {
                typed = new AccountCode(accountCode);
            }
            catch (ArgumentException exception)
            {
                errors.Add(PostingErrors.Invalid("bad_account_code", exception.Message, exception.Message));
                continue;
            }

            if (!reference.Accounts.TryGetValue(typed.Value, out AccountFacts? account))
            {
                errors.Add(PostingErrors.UnknownAccount(typed.Value));
                continue;
            }

            if (!account.IsActive)
            {
                errors.Add(PostingErrors.InactiveAccount(account.Code));
                continue;
            }

            // GR-COA-001 — الطبقة الثانية. الأولى بيانات المصفوفة، والثالثة قيد
            // تحقق في قاعدة البيانات لا يتجاوزه خطأ برمجي.
            if (!account.IsPostable)
            {
                errors.Add(PostingErrors.RollupAccount(account.Code));
                continue;
            }

            // GR-COA-002 — كل بُعد إلزامي على الحساب موجود على السطر.
            foreach (string required in account.RequiredDimensions)
            {
                if (string.IsNullOrEmpty(Dimension(line, required)))
                {
                    errors.Add(PostingErrors.MissingDimension(account.Code, required));
                }
            }

            if (account.SubledgerType != "none" && string.IsNullOrEmpty(line.SubledgerPartyId))
            {
                errors.Add(PostingErrors.MissingSubledger(account.Code, account.SubledgerType));
            }

            if (account.CurrencyMode == "company_only" && currency != companyCurrency)
            {
                errors.Add(PostingErrors.CurrencyNotAllowed(account.Code, currency));
            }
            else if (account.CurrencyMode == "fixed" && account.CurrencyCode is { } fixedCurrency && currency != fixedCurrency)
            {
                errors.Add(PostingErrors.CurrencyNotAllowed(account.Code, currency));
            }

            // ── قواعد الحجب، بوقائع السطر نفسه ────────────────────────────
            Error? blocked = EvaluateGuards(matrix, reference, line, facts, amounts);
            if (blocked is not null)
            {
                errors.Add(blocked);
                continue;
            }

            decimal debit = line.Side == PostingSide.Debit ? line.Amount : 0m;
            decimal credit = line.Side == PostingSide.Credit ? line.Amount : 0m;

            lines.Add(new PlannedLine
            {
                LineNo = lines.Count + 1,
                RoleCode = line.RoleCode,
                Qualifier = line.Qualifier,
                AccountCode = typed.Value,
                Debit = Amounts.Normalize(debit),
                Credit = Amounts.Normalize(credit),
                DebitCompany = Amounts.Normalize(debit * fxRate),
                CreditCompany = Amounts.Normalize(credit * fxRate),
                FxRate = fxRate,
                BranchId = line.BranchId,
                CostCenterId = line.CostCenterId,
                ProjectId = line.ProjectId,
                PropertyId = line.PropertyId,
                UnitId = line.UnitId,
                WarehouseId = line.WarehouseId,
                BoqItemId = line.BoqItemId,
                SubledgerKind = line.SubledgerKind,
                SubledgerPartyId = line.SubledgerPartyId,
                Description = line.Description,
                DescriptionAr = line.DescriptionAr,
                DescriptionArSearch = ArabicSearch.Normalize(line.DescriptionAr).Value,
            });
        }

        if (errors.Count > 0)
        {
            return Result<PostingPlan>.Failure(errors);
        }

        if (lines.Count < 2)
        {
            return Result<PostingPlan>.Failure(PostingErrors.TooFewLines(lines.Count));
        }

        // ── AssertBalanced — الطبقة الثالثة، قبل كتابة أي صفّ ──────────────
        decimal totalDebit = lines.Sum(static line => line.DebitCompany);
        decimal totalCredit = lines.Sum(static line => line.CreditCompany);
        if (totalDebit != totalCredit)
        {
            return Result<PostingPlan>.Failure(PostingErrors.Unbalanced(totalDebit, totalCredit));
        }

        // ── صفوف الأرصدة: مجمَّعة بالحساب و**مرتّبة برمزه تصاعدياً** ───────
        // الترتيب ليس تجميلاً: نفس العبارة بصفوف غير مرتّبة قيست عند 0.161 مقابل
        // 1,841.3 معاملة/ث مع 22–35 جموداً — انهيار ~11,000× (فخ-10).
        List<PlannedBalance> balances = [.. lines
            .GroupBy(static line => line.AccountCode, StringComparer.Ordinal)
            .Select(static group => new PlannedBalance(
                group.Key,
                group.Sum(static line => line.DebitCompany),
                group.Sum(static line => line.CreditCompany)))
            .OrderBy(static balance => balance.AccountCode, StringComparer.Ordinal)];

        string memoAr = request.Narration.IsAssigned ? request.Narration.Arabic : string.Empty;
        string actor = request.Actor.ToString();

        return Result<PostingPlan>.Success(new PostingPlan
        {
            EntryId = Guid.CreateVersion7(),
            CompanyId = request.Tenant.Value,
            BookId = request.Book,
            FiscalYear = period.FiscalYear,
            EntryDate = request.DocumentDate,
            PeriodCode = period.PeriodCode,
            Status = "POSTED",
            Actor = actor,
            ActorSearch = ArabicSearch.Normalize(actor).Value,
            Memo = request.Narration.IsAssigned ? request.Narration.English : string.Empty,
            MemoAr = memoAr,
            MemoArSearch = ArabicSearch.Normalize(memoAr).Value,
            SourceModule = request.Source.Module.ToString(),
            SourceDocType = request.Source.DocumentType,
            SourceDocId = request.Source.DocumentId,
            TriggerCode = request.Trigger.ToString(),
            Generation = request.Generation,
            EventCode = request.Event.Value,
            IdempotencyKey = request.IdempotencyKey.Value,
            Currency = currency,
            ClosedPeriodPermission = request.ClosedPeriodAuthorisation?.PermissionCode,
            ClosedPeriodAuthoriser = request.ClosedPeriodAuthorisation?.AuthorisedBy.ToString(),
            Lines = lines,
            Balances = balances,
        });
    }

    /// <summary>سطر وسيط: الدور محسوم والمبلغ محسوب، والحساب لم يُحلّ بعد.</summary>
    internal sealed record DraftLine
    {
        public required string RoleCode { get; init; }

        public required string Qualifier { get; init; }

        public required PostingSide Side { get; init; }

        public required decimal Amount { get; init; }

        public string? BranchId { get; init; }

        public string? CostCenterId { get; init; }

        public string? ProjectId { get; init; }

        public string? PropertyId { get; init; }

        public string? UnitId { get; init; }

        public string? WarehouseId { get; init; }

        public string? BoqItemId { get; init; }

        public string SubledgerKind { get; init; } = "none";

        public string? SubledgerPartyId { get; init; }

        public string Description { get; init; } = string.Empty;

        public string DescriptionAr { get; init; } = string.Empty;
    }

    private static string? Dimension(DraftLine line, string name) => name switch
    {
        "branch" => line.BranchId,
        "cost_center" => line.CostCenterId,
        "project" => line.ProjectId,
        "property" => line.PropertyId,
        "unit" => line.UnitId,
        "warehouse" => line.WarehouseId,
        "boq_item" => line.BoqItemId,
        _ => null,
    };

    private static Result<List<DraftLine>> FromEvent(
        PostingRequest request,
        MatrixCatalog matrix,
        Dictionary<string, string> facts,
        Dictionary<string, decimal> amounts,
        Dictionary<string, string> dimensions)
    {
        string code = request.Event.Value;
        MatrixEvent? definition = matrix.Find(code);
        if (definition is null)
        {
            return Result<List<DraftLine>>.Failure(PostingErrors.UnknownEvent(code));
        }

        // ‏posts_entry = false بيان سياسة محاسبية صريح (توقيع عقد، استحقاق في
        // النموذج المُدار). توليد قيد منه هو اختراع حقيقة محاسبية.
        if (!definition.PostsEntry)
        {
            return Result<List<DraftLine>>.Failure(PostingErrors.EventPostsNoEntry(code));
        }

        List<Error> errors = [];
        List<DraftLine> lines = [];

        foreach (MatrixLine line in definition.Lines)
        {
            if (!string.Equals(line.LineKind, "role", StringComparison.Ordinal))
            {
                errors.Add(PostingErrors.UnsupportedLineKind(code, line.LineNo, line.LineKind));
                continue;
            }

            if (line.When is { Length: > 0 } when)
            {
                string expression = definition.Conditions.TryGetValue(when, out MatrixCondition? condition)
                    ? condition.Expression
                    : when;

                ConditionOutcome outcome = ConditionEvaluator.Evaluate(when, expression, facts, amounts);
                if (!outcome.Evaluated)
                {
                    errors.Add(PostingErrors.UndecidableCondition(code, line.LineNo, outcome.Reason!));
                    continue;
                }

                if (!outcome.Value)
                {
                    continue;
                }
            }

            if (!LinearExpression.TryEvaluate(line.Amount, amounts, out decimal value, out string? unknown))
            {
                errors.Add(PostingErrors.UnknownAmount(code, line.LineNo, unknown ?? line.Amount));
                continue;
            }

            // مبلغ صفر بعد تقييم الشروط سطرٌ بلا أثر محاسبي. يُسقط، ويبقى شرط
            // «سطران على الأقل» هو ما يمنع أن يتحوّل الإسقاط إلى قيد أعرج.
            if (Amounts.Normalize(value) == 0m)
            {
                continue;
            }

            if (value < 0m)
            {
                // مبلغ سالب يعني أن الجانب معكوس، لا أن السطر مدين بسالب:
                // القيد ck_journal_line_sign يرفض السالب في قاعدة البيانات.
                value = -value;
            }

            Result<string> qualifier = Qualifier(line, facts);
            if (qualifier.IsFailure)
            {
                errors.AddRange(qualifier.Errors);
                continue;
            }

            string subledgerKind = line.Subledger is { Length: > 0 } and not "resolved" ? line.Subledger : "none";
            facts.TryGetValue("subledger." + subledgerKind, out string? party);

            lines.Add(new DraftLine
            {
                RoleCode = line.Role,
                Qualifier = qualifier.Value,
                Side = string.Equals(line.Side, "debit", StringComparison.Ordinal) ? PostingSide.Debit : PostingSide.Credit,
                Amount = Amounts.Normalize(value),
                BranchId = Value(dimensions, "branch"),
                CostCenterId = Value(dimensions, "cost_center"),
                ProjectId = Value(dimensions, "project"),
                PropertyId = Value(dimensions, "property"),
                UnitId = Value(dimensions, "unit"),
                WarehouseId = Value(dimensions, "warehouse"),
                BoqItemId = Value(dimensions, "boq_item"),
                SubledgerKind = subledgerKind,
                SubledgerPartyId = party,
                Description = definition.NameEn,
                DescriptionAr = definition.NameAr,
            });
        }

        return errors.Count > 0
            ? Result<List<DraftLine>>.Failure(errors)
            : Result<List<DraftLine>>.Success(lines);
    }

    private static Result<string> Qualifier(MatrixLine line, Dictionary<string, string> facts)
    {
        if (line.QualifierSource is not { Length: > 0 } source)
        {
            return Result<string>.Success(DefaultQualifier);
        }

        if (source.StartsWith("constant:", StringComparison.Ordinal))
        {
            return Result<string>.Success(source["constant:".Length..]);
        }

        if (facts.TryGetValue(source, out string? value) && value.Length > 0)
        {
            return Result<string>.Success(value);
        }

        // الوقوع على المؤهّل الافتراضي هنا يختار حساباً آخر بصمت — والرفض أرحم.
        return Result<string>.Failure(PostingErrors.MissingQualifier(line.Role, source));
    }

    /// <summary>
    /// المسار الصريح — الوحدة تُسلّم سطورها بأدوارها (القيد اليدوي وما شابهه).
    /// <para>
    /// <b>والمصفوفة تُستشار هنا أيضاً، وإن لم يُقرأ منها قالب.</b> السطور تعطي القيد
    /// <b>محتواه</b>، ورمز الحدث يعطيه <b>هويّته</b> — وهو عمود في
    /// <c>uq_posting_identity</c>. فالرمز الذي لا تعرفه المصفوفة رمزٌ مُختلَق، وخطأ
    /// مطبعي واحد فيه يجعل الحقيقة المحاسبية الواحدة هويتين فتُرحَّل مرّتين: قيدان
    /// متوازنان، وسلسلة بصمات سليمة، وترقيم بلا فجوات — وانحرافٌ لا يُرى إلا في مطابقة
    /// دفتر مساعد. وهذا هو المقابل المرآتي للرمز الفارغ الذي أُغلق في ADR-0016، وأثره
    /// معاكس تماماً: ذاك يبتلع حقيقة، وهذا يخلق واحدة زائدة.
    /// </para>
    /// <para>
    /// ولا يُقرأ من الحدث هنا شيء غير <b>وجوده</b>: لا سطوره ولا شروطه ولا مبالغه.
    /// وأنواع سطور أحداث الدفتر (<c>manual</c> · <c>import</c> · <c>sweep</c> ·
    /// <c>mirror</c>) لا تُولَّد من الطلب أصلاً، ولذلك يوجد هذا المسار.
    /// </para>
    /// </summary>
    private static Result<List<DraftLine>> FromExplicitLines(
        PostingRequest request,
        MatrixCatalog matrix,
        Dictionary<string, string> dimensions)
    {
        if (request.Lines.Count == 0)
        {
            return Result<List<DraftLine>>.Failure(PostingErrors.NoLines);
        }

        string code = request.Event.Value;
        MatrixEvent? definition = matrix.Find(code);
        if (definition is null)
        {
            return Result<List<DraftLine>>.Failure(PostingErrors.EventCodeNotInMatrix(code));
        }

        // ‏posts_entry = false بيان سياسة محاسبية صريح، وهو صحيح على المسارين: القالب
        // لا يُولّد منه قيداً، والسطور الصريحة لا تلتفّ حوله. والالتفاف هنا أخطر — لأنه
        // يُنتج قيداً كاملاً تحت رمز أُعلن أنه لا يُنتج قيداً.
        if (!definition.PostsEntry)
        {
            return Result<List<DraftLine>>.Failure(PostingErrors.EventPostsNoEntry(code));
        }

        List<DraftLine> lines = [];

        foreach (PostingLine line in request.Lines)
        {
            Dictionary<string, string> merged = new(dimensions, StringComparer.Ordinal);
            foreach (PostingDimension dimension in line.Dimensions)
            {
                merged[dimension.Name] = dimension.Value;
            }

            lines.Add(new DraftLine
            {
                RoleCode = PostingRoleCodes.OfSide(line.Role, line.Side),
                Qualifier = line.Qualifier is { Length: > 0 } ? line.Qualifier : DefaultQualifier,
                Side = line.Side,
                Amount = Amounts.Normalize(line.Amount.Amount),
                BranchId = line.Scope.BranchId ?? Value(merged, "branch"),
                CostCenterId = line.Scope.CostCenterId ?? Value(merged, "cost_center"),
                ProjectId = line.Scope.ProjectId ?? Value(merged, "project"),
                PropertyId = Value(merged, "property"),
                UnitId = Value(merged, "unit"),
                WarehouseId = Value(merged, "warehouse"),
                BoqItemId = Value(merged, "boq_item"),
                SubledgerKind = line.Subledger.Kind == SubledgerKind.None
                    ? "none"
                    : line.Subledger.Kind.ToString().ToLowerInvariant(),
                SubledgerPartyId = line.Subledger.Kind == SubledgerKind.None ? null : line.Subledger.PartyId,
                Description = line.Narration?.English ?? string.Empty,
                DescriptionAr = line.Narration?.Arabic ?? string.Empty,
            });
        }

        return Result<List<DraftLine>>.Success(lines);
    }

    /// <summary>
    /// قواعد الحجب من <c>guard-rules.json</c> — الطبقة الثانية من ثلاث.
    /// <para>
    /// وواقعة نموذج ملكية العقار <b>تُشتقّ من سجل الأبعاد</b> إن لم تُصرّح بها
    /// الوحدة: قاعدة تعتمد كلياً على أن يتذكّر المُستدعي تسليم الواقعة ليست قاعدة.
    /// </para>
    /// </summary>
    private static Error? EvaluateGuards(
        MatrixCatalog matrix,
        CompanyReference reference,
        DraftLine line,
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyDictionary<string, decimal> amounts)
    {
        Dictionary<string, string> view = new(facts, StringComparer.Ordinal);

        if (line.PropertyId is { Length: > 0 } propertyId
            && reference.PropertyOwnership.TryGetValue(propertyId, out string? ownership))
        {
            view["property.ownership_model"] = ownership;
        }

        foreach (GuardRule rule in matrix.GuardRules)
        {
            if (!string.Equals(rule.Severity, "block", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(rule.AppliesTo.Kind, "account_role", StringComparison.Ordinal))
            {
                // ‏account_property مفروضة أعلاه بخصائص الحساب نفسه (GR-COA-001/002).
                continue;
            }

            if (!string.Equals(rule.AppliesTo.Role, line.RoleCode, StringComparison.Ordinal))
            {
                continue;
            }

            ConditionOutcome outcome = ConditionEvaluator.EvaluateExpression(rule.Condition, view, amounts);

            // قاعدة حجب لا يمكن تقييمها لا تُتجاوَز: الحجب موجود لأن الخطأ الذي
            // يمنعه يضخّم إيراداً واحداً وعشرين ضعفاً (07-real-estate.md §1.3).
            if (!outcome.Evaluated)
            {
                return PostingErrors.Guard(
                    rule.RuleId,
                    $"تعذّر تقييم قاعدة الحجب {rule.RuleId} على هذا السطر: {outcome.Reason} "
                    + "والقاعدة التي لا تُقيَّم لا تُتجاوَز.",
                    $"Guard rule {rule.RuleId} could not be evaluated for this line: {outcome.Reason} "
                    + "A rule that cannot be evaluated is not bypassed.");
            }

            if (outcome.Value)
            {
                return PostingErrors.Guard(rule.RuleId, rule.MessageAr, rule.MessageEn);
            }
        }

        return null;
    }

    private static string? Value(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out string? value) && value.Length > 0 ? value : null;

    /// <summary>يُستعمل في الرسائل فقط.</summary>
    internal static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
