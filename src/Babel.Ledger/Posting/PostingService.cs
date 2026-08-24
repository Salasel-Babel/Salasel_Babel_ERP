using System.Globalization;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Ledger.PostingMatrix;
using Babel.SharedKernel;
using Npgsql;
using NpgsqlTypes;

namespace Babel.Ledger.Posting;

/// <summary>
/// محرك الترحيل — الجهة الوحيدة التي تكتب قيداً في النظام كله.
/// <para>
/// <b>القواعد الأربع المقيسة، مطبَّقة من أول سطر</b> (measurements.md · traps.md):
/// <list type="number">
///   <item><b>صفر ذهاب وإياب داخل قفل متنازَع عليه.</b> الترحيل كله نداء واحد
///         لـ<c>ledger.post_entry</c>. الشكل الطبيعي بلغة ORM يحبس ~4 رحلات داخل
///         معاملة تملك القفل، وسقفه <c>1/(4×RTT)</c> — 8.0 قيد/ث عند RTT = 30مث،
///         والرقم نفسه عند 8 و32 كاتباً. النداء الواحد قيس عند 1,016.8 قيد/ث:
///         <b>فارق 127×</b> (فخ-14).</item>
///   <item><b>تحديث الأرصدة عبارة واحدة بصفوف مرتّبة</b> <c>ORDER BY account_code</c>
///         (فخ-10)، و<c>INSERT ... ON CONFLICT DO UPDATE</c> لا <c>UPDATE</c> مجرّد
///         (فخ-09)، مع تأكيد عدد الصفوف وإجهاض المعاملة عند الاختلاف.</item>
///   <item><b>الإحكام لكل قيد ومستقلّ عن الترتيب</b> — مفتاح فريد، لا حارس تسلسل
///         تصاعدي (فخ-13).</item>
///   <item><b>العدّاد صفّ يُقفل، لا <c>SEQUENCE</c></b> (فخ-12 · ADR-0008).</item>
/// </list>
/// </para>
/// </summary>
public sealed class PostingService : IPostingService, IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly LedgerRuntime _runtime;

    /// <summary>ينشئ محرك الترحيل.</summary>
    public PostingService(IEntitlementEnforcer enforcer, LedgerRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _runtime = runtime;
    }

    private NpgsqlDataSource _dataSource => _runtime.DataSource;

    private LedgerReferenceCache _reference => _runtime.Reference;

    private LedgerOptions _options => _runtime.Options;

    /// <inheritdoc />
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> PostAsync(
        PostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await GateAsync(request.Tenant, request.Source.Module, cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        CompanyReference reference = await _reference
            .GetAsync(request.Tenant.Value, cancellationToken).ConfigureAwait(false);

        // اللحظة تُلتقط **مرّة واحدة** ومقصوصة إلى الميكروثانية قبل التجزئة وقبل
        // التخزين: .NET يحمل 100 نانوثانية و timestamptz يحمل ميكروثانية، والقصّ
        // بعد التجزئة يجعل كل بصمة تفشل عند إعادة التحقق (فخ-16 · SPEC §6.2).
        DateTime postedAt = Instants.CaptureNow();

        Result<PostingPlan> plan = PostingPlanner.Plan(
            request, reference, MatrixCatalog.Default, _options.CompanyCurrency, postedAt);

        if (plan.IsFailure)
        {
            await RecordRefusalAsync(request, plan.Errors, cancellationToken).ConfigureAwait(false);
            return Result<PostingReceipt>.Failure(plan.Errors);
        }

        return await ExecuteAsync(plan.Value, postedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> ReverseAsync(
        ReversalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Tenant, request.Actor, BabelModule.Ledger, EntitlementAccess.Write, "Ledger.Reverse", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        CompanyReference reference = await _reference
            .GetAsync(request.Tenant.Value, cancellationToken).ConfigureAwait(false);

        Result<PostingPlan> plan = await BuildReversalAsync(request, reference, cancellationToken).ConfigureAwait(false);
        if (plan.IsFailure)
        {
            return Result<PostingReceipt>.Failure(plan.Errors);
        }

        return await ExecuteAsync(plan.Value, Instants.CaptureNow(), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result> GateAsync(TenantId tenant, BabelModule module, CancellationToken cancellationToken)
    {
        // بوابتان لا واحدة: استحقاق الدفتر (الكتابة فيه)، واستحقاق الوحدة المصدر.
        // وحدة انقضى اشتراكها تبقى مقروءة بالكامل، ولا تُنشئ قيداً جديداً.
        Result ledgerGate = await _enforcer
            .EnsureAsync(tenant, UserId.SystemActor, BabelModule.Ledger, EntitlementAccess.Write, "Ledger.Post", cancellationToken)
            .ConfigureAwait(false);

        if (ledgerGate.IsFailure)
        {
            return ledgerGate;
        }

        return await _enforcer
            .EnsureAsync(tenant, UserId.SystemActor, module, EntitlementAccess.Write, "Ledger.Post.Source", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// الترحيل: <b>نداء خادم واحد</b>. لا معاملة مفتوحة عبر رحلات العميل، ولا قراءة
    /// بين أخذ القفل والـCOMMIT.
    /// </summary>
    private async ValueTask<Result<PostingReceipt>> ExecuteAsync(
        PostingPlan plan,
        DateTime postedAt,
        CancellationToken cancellationToken)
    {
        CanonicalDocument document = BuildDocument(plan, postedAt);
        CanonicalSplit split = CanonicalSplit.Of(document);
        byte[] genesis = JournalEntrySchema.Genesis(
            plan.CompanyId.ToString("D", CultureInfo.InvariantCulture), plan.BookId, plan.FiscalYear);

        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(string.Empty, connection);
        Bind(command, plan, postedAt, split, genesis);
        command.CommandText = PostEntrySql(command.Parameters.Count);

        try
        {
            await using NpgsqlDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result<PostingReceipt>.Failure(PostingErrors.Invalid(
                    "no_result", "لم تُرجع دالة الترحيل صفاً.", "The posting function returned no row."));
            }

            Guid entryId = reader.GetGuid(0);
            long entryNo = reader.GetInt64(1);
            long chainSeq = reader.GetInt64(2);
            byte[] hash = reader.GetFieldValue<byte[]>(3);
            bool already = reader.GetBoolean(6);

            return Result<PostingReceipt>.Success(new PostingReceipt(
                entryId,
                entryNo,
                Canonicalizer.Hex(hash),
                already,
                chainSeq,
                plan.PeriodCode,
                plan.Generation,
                already ? 0 : plan.Lines.Count));
        }
        catch (PostgresException exception)
        {
            // الرفض من قاعدة البيانات ليس عطلاً بل هو الطبقتان الأولى والثانية
            // وهما تعملان: 42501 صلاحيات · check_violation مشغّل مؤجَّل أو حجب.
            return Result<PostingReceipt>.Failure(
                PostingErrors.Database(exception.SqlState, exception.MessageText));
        }
    }

    /// <summary>
    /// المستند المرجعي <c>babel.journal.entry</c> كما تعرّفه المكتبة المختومة.
    /// <para>
    /// ولاحظ <b>ما ليس فيه</b>: لا <c>*_search</c>، ولا مجاميع، ولا رصيد متحرّك.
    /// مجموعة الاستثناء معلَنة ومُبصَّمة في المخطّط نفسه (SPEC §4)، وأي محاولة
    /// لإدراج اسم منها ترفعها المكتبة بـ<c>CANON-DOC-EXCLUDED-FIELD</c>.
    /// </para>
    /// </summary>
    private static CanonicalDocument BuildDocument(PostingPlan plan, DateTime postedAt)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V1.NewDocument();

        builder.Set("tenant_id", CanonicalValue.Text(plan.CompanyId.ToString("D", CultureInfo.InvariantCulture)));
        builder.Set("book_id", CanonicalValue.Text(plan.BookId));
        builder.Set("fiscal_year", CanonicalValue.Integer(plan.FiscalYear));
        builder.Set("entry_id", CanonicalValue.Uuid(plan.EntryId));

        // قيمة نائبة: رقم القيد يُركَّب داخل الخادم تحت القفل، والقطع في
        // CanonicalSplit يضمن أن موضعه في البايتات هو موضعه هنا بالضبط.
        builder.Set("entry_no", CanonicalValue.Integer(0));

        builder.Set("entry_date", CanonicalValue.Date(plan.EntryDate));
        builder.Set("posted_at", CanonicalValue.Instant(postedAt));
        builder.Set("status", CanonicalValue.Token(plan.Status));
        builder.Set("actor", CanonicalValue.Text(plan.Actor));
        builder.Set("memo", CanonicalValue.Text(plan.Memo));
        builder.Set("memo_ar", CanonicalValue.Text(plan.MemoAr));
        builder.Set("source_ref", CanonicalValue.Text(plan.SourceDocType + "/" + plan.SourceDocId));
        builder.Set("idempotency_key", CanonicalValue.Text(plan.IdempotencyKey));
        builder.Set("currency", CanonicalValue.Token(plan.Currency));

        builder.SetGroup("lines", plan.Lines.Select(static line => new Action<CanonicalItemBuilder>(item =>
        {
            item.Set("line_no", CanonicalValue.Integer(line.LineNo));
            item.Set("account_code", CanonicalValue.Text(line.AccountCode));
            item.Set("debit", CanonicalValue.Amount(line.Debit));
            item.Set("credit", CanonicalValue.Amount(line.Credit));
            item.Set("cost_center", CanonicalValue.TextOrNull(line.CostCenterId));
            item.Set("description", CanonicalValue.Text(line.Description));
        })));

        return builder.Build();
    }

    private static void Bind(
        NpgsqlCommand command,
        PostingPlan plan,
        DateTime postedAt,
        CanonicalSplit split,
        byte[] genesis)
    {
        void Add<T>(T value, NpgsqlDbType type) =>
            command.Parameters.Add(new NpgsqlParameter<T> { TypedValue = value, NpgsqlDbType = type });

        void AddText(string? value) =>
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });

        Add(plan.CompanyId, NpgsqlDbType.Uuid);
        AddText(plan.BookId);
        Add(plan.FiscalYear, NpgsqlDbType.Integer);
        Add(plan.EntryId, NpgsqlDbType.Uuid);
        Add(plan.EntryDate, NpgsqlDbType.Date);
        AddText(plan.PeriodCode);
        Add(postedAt, NpgsqlDbType.TimestampTz);
        AddText(plan.Status);
        AddText(plan.Actor);
        AddText(plan.ActorSearch);
        AddText(plan.Memo);
        AddText(plan.MemoAr);
        AddText(plan.MemoArSearch);
        AddText(plan.SourceModule);
        AddText(plan.SourceDocType);
        AddText(plan.SourceDocId);
        AddText(plan.TriggerCode);
        Add(plan.Generation, NpgsqlDbType.Integer);
        AddText(plan.EventCode);
        AddText(plan.IdempotencyKey);
        AddText(plan.Currency);
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = (object?)plan.ReversesEntryId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid,
        });
        AddText(plan.ReversalReasonAr);
        AddText(plan.ReversalReasonEn);
        AddText(plan.ClosedPeriodPermission);
        AddText(plan.ClosedPeriodAuthoriser);
        AddText(Canonicalizer.CurrentVersion);
        Add(genesis, NpgsqlDbType.Bytea);
        Add(split.Prefix, NpgsqlDbType.Bytea);
        Add(split.Head, NpgsqlDbType.Bytea);
        Add(split.Tail, NpgsqlDbType.Bytea);

        Add(plan.Lines.Select(static _ => Guid.CreateVersion7()).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        Add(plan.Lines.Select(static line => line.LineNo).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Integer);
        Add(plan.Lines.Select(static line => line.AccountCode).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Lines.Select(static line => line.RoleCode).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Lines.Select(static line => line.Qualifier).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Lines.Select(static line => line.Debit).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        Add(plan.Lines.Select(static line => line.Credit).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        Add(plan.Lines.Select(static line => line.DebitCompany).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        Add(plan.Lines.Select(static line => line.CreditCompany).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        Add(plan.Lines.Select(static line => line.FxRate).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        AddTextArray(command, plan.Lines.Select(static line => line.BranchId));
        AddTextArray(command, plan.Lines.Select(static line => line.CostCenterId));
        AddTextArray(command, plan.Lines.Select(static line => line.ProjectId));
        AddTextArray(command, plan.Lines.Select(static line => line.PropertyId));
        AddTextArray(command, plan.Lines.Select(static line => line.UnitId));
        AddTextArray(command, plan.Lines.Select(static line => line.WarehouseId));
        AddTextArray(command, plan.Lines.Select(static line => line.BoqItemId));
        Add(plan.Lines.Select(static line => line.SubledgerKind).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        AddTextArray(command, plan.Lines.Select(static line => line.SubledgerPartyId));
        Add(plan.Lines.Select(static line => line.Description).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Lines.Select(static line => line.DescriptionAr).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Lines.Select(static line => line.DescriptionArSearch).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);

        Add(plan.Balances.Select(static balance => balance.AccountCode).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(plan.Balances.Select(static balance => balance.Debit).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        Add(plan.Balances.Select(static balance => balance.Credit).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
    }

    private static void AddTextArray(NpgsqlCommand command, IEnumerable<string?> values)
        => command.Parameters.Add(new NpgsqlParameter
        {
            Value = values.ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text,
        });

    /// <summary>
    /// نداء الدالة. عدد المعامِلات يُبنى من عدد معامِلات الأمر نفسه، لا يُكتب رقماً
    /// في نصّ: قائمة <c>$1..$n</c> مكتوبة بيد أخطأت مرّة هنا فعلاً — سقط آخر
    /// معامِلين فصار النداء يبحث عن دالة بتوقيع لا وجود له.
    /// </summary>
    private static string PostEntrySql(int parameters)
        => "select * from ledger.post_entry("
           + string.Join(',', Enumerable.Range(1, parameters).Select(
               static index => "$" + index.ToString(CultureInfo.InvariantCulture)))
           + ")";

    /// <summary>
    /// يبني قيد العكس من القيد الأصلي. <b>الأصلي لا يُمسّ إطلاقاً</b> (ADR-0002):
    /// لا <c>UPDATE</c> عليه ولا علم «معكوس» — والصلاحيات لا تسمح بذلك أصلاً.
    /// الرابط في القيد الجديد: <c>reverses_entry_id</c>.
    /// </summary>
    private async ValueTask<Result<PostingPlan>> BuildReversalAsync(
        ReversalRequest request,
        CompanyReference reference,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand header = new(
            """
            select entry_id, book_id, fiscal_year, entry_date, period_code, status, currency,
                   source_module, source_doc_type, source_doc_id, posting_trigger_code,
                   posting_generation, event_code, memo, memo_ar,
                   (select count(*) from ledger.journal_entry r
                     where r.reverses_entry_id = e.entry_id and r.status = 'REVERSAL')
              from ledger.journal_entry e
             where e.company_id = $1 and e.entry_id = $2
            """, connection);
        header.Parameters.AddWithValue(request.Tenant.Value);
        header.Parameters.AddWithValue(request.EntryId);

        string bookId, periodCode, status, currency, sourceModule, docType, docId, triggerCode, eventCode, memo, memoAr;
        int fiscalYear, generation;
        DateOnly entryDate;

        await using (NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result<PostingPlan>.Failure(PostingErrors.EntryNotFound(request.EntryId));
            }

            bookId = reader.GetString(1);
            fiscalYear = reader.GetInt32(2);
            entryDate = reader.GetFieldValue<DateOnly>(3);
            periodCode = reader.GetString(4);
            status = reader.GetString(5);
            currency = reader.GetString(6);
            sourceModule = reader.GetString(7);
            docType = reader.GetString(8);
            docId = reader.GetString(9);
            triggerCode = reader.GetString(10);
            generation = reader.GetInt32(11);
            eventCode = reader.GetString(12);
            memo = reader.GetString(13);
            memoAr = reader.GetString(14);

            if (status == "REVERSAL")
            {
                return Result<PostingPlan>.Failure(PostingErrors.CannotReverseAReversal(request.EntryId));
            }

            if (reader.GetInt64(15) > 0)
            {
                return Result<PostingPlan>.Failure(PostingErrors.AlreadyReversed(request.EntryId));
            }
        }

        List<PlannedLine> lines = [];
        await using (NpgsqlCommand body = new(
            """
            select line_no, account_code, role_code, qualifier, debit, credit,
                   debit_company, credit_company, fx_rate,
                   branch_id, cost_center_id, project_id, property_id, unit_id, warehouse_id, boq_item_id,
                   subledger_kind, subledger_party_id, description, description_ar
              from ledger.journal_line where entry_id = $1 order by line_no
            """, connection))
        {
            body.Parameters.AddWithValue(request.EntryId);
            await using NpgsqlDataReader reader = await body.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // العكس هو قلب الجانبين، ولا شيء غيره: نفس الحساب ونفس المبلغ
                // ونفس الأبعاد. عكسٌ بحساب آخر ليس عكساً بل قيد تصحيح مختلف.
                lines.Add(new PlannedLine
                {
                    LineNo = reader.GetInt32(0),
                    AccountCode = reader.GetString(1),
                    RoleCode = reader.GetString(2),
                    Qualifier = reader.GetString(3),
                    Debit = reader.GetDecimal(5),
                    Credit = reader.GetDecimal(4),
                    DebitCompany = reader.GetDecimal(7),
                    CreditCompany = reader.GetDecimal(6),
                    FxRate = reader.GetDecimal(8),
                    BranchId = reader.IsDBNull(9) ? null : reader.GetString(9),
                    CostCenterId = reader.IsDBNull(10) ? null : reader.GetString(10),
                    ProjectId = reader.IsDBNull(11) ? null : reader.GetString(11),
                    PropertyId = reader.IsDBNull(12) ? null : reader.GetString(12),
                    UnitId = reader.IsDBNull(13) ? null : reader.GetString(13),
                    WarehouseId = reader.IsDBNull(14) ? null : reader.GetString(14),
                    BoqItemId = reader.IsDBNull(15) ? null : reader.GetString(15),
                    SubledgerKind = reader.GetString(16),
                    SubledgerPartyId = reader.IsDBNull(17) ? null : reader.GetString(17),
                    Description = reader.GetString(18),
                    DescriptionAr = reader.GetString(19),
                    DescriptionArSearch = ArabicSearch.Normalize(reader.GetString(19)).Value,
                });
            }
        }

        DateOnly reversalDate = request.ReversalDate ?? entryDate;
        PeriodFacts? period = reference.PeriodOf(reversalDate);
        if (period is null)
        {
            return Result<PostingPlan>.Failure(PostingErrors.NoFiscalPeriod(reversalDate));
        }

        if (period.State == "permanently_closed")
        {
            return Result<PostingPlan>.Failure(PostingErrors.PermanentlyClosedPeriod(period.PeriodCode));
        }

        if (period.State == "closed" && request.ClosedPeriodAuthorisation is null)
        {
            return Result<PostingPlan>.Failure(PostingErrors.ClosedPeriod(period.PeriodCode));
        }

        List<PlannedBalance> balances = [.. lines
            .GroupBy(static line => line.AccountCode, StringComparer.Ordinal)
            .Select(static group => new PlannedBalance(
                group.Key,
                group.Sum(static line => line.DebitCompany),
                group.Sum(static line => line.CreditCompany)))
            .OrderBy(static balance => balance.AccountCode, StringComparer.Ordinal)];

        string actor = request.Actor.ToString();

        return Result<PostingPlan>.Success(new PostingPlan
        {
            EntryId = Guid.CreateVersion7(),
            CompanyId = request.Tenant.Value,
            BookId = bookId,
            FiscalYear = period.FiscalYear,
            EntryDate = reversalDate,
            PeriodCode = period.PeriodCode,
            Status = "REVERSAL",
            Actor = actor,
            ActorSearch = ArabicSearch.Normalize(actor).Value,
            Memo = request.Reason.English,
            MemoAr = request.Reason.Arabic,
            MemoArSearch = ArabicSearch.Normalize(request.Reason.Arabic).Value,
            SourceModule = sourceModule,
            SourceDocType = docType,
            SourceDocId = docId,
            // مفتاح الإحكام يختلف عن الأصل برمز الإطلاق لا بالجيل: العكس حقيقة
            // مستقلة عن «الجيل التالي» الذي قد يأتي بعده تصحيحاً.
            TriggerCode = "REVERSAL:" + triggerCode,
            Generation = generation,
            EventCode = eventCode,
            IdempotencyKey = "reversal:" + request.EntryId.ToString("D", CultureInfo.InvariantCulture),
            Currency = currency,
            ReversesEntryId = request.EntryId,
            ReversalReasonAr = request.Reason.Arabic,
            ReversalReasonEn = request.Reason.English,
            ClosedPeriodPermission = request.ClosedPeriodAuthorisation?.PermissionCode,
            ClosedPeriodAuthoriser = request.ClosedPeriodAuthorisation?.AuthorisedBy.ToString(),
            Lines = [.. lines.Select(static (line, index) => line with { LineNo = index + 1 })],
            Balances = balances,
        });
    }

    /// <summary>
    /// يسجّل الرفض. <b>مخزن الأحداث يسجّل ما نجح فقط بحكم البناء</b> (فخ-08)،
    /// والمرفوض هو ما يُثبت أن الرقابة عملت: ترحيل مرفوض لا أثر له لا يُفرَّق عن
    /// ترحيل لم يُطلَب أصلاً.
    /// </summary>
    private async ValueTask RecordRefusalAsync(
        PostingRequest request,
        IReadOnlyList<Error> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection =
                await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlCommand command = new(
                """
                insert into ledger.process_event
                    (company_id, kind, outcome, actor, event_code, source_doc_type, source_doc_id,
                     reason_code, message_ar, message_en, detail)
                values ($1, 'posting.request', 'refused', $2, $3, $4, $5, $6, $7, $8, $9)
                """, connection);

            Error first = errors[0];
            command.Parameters.AddWithValue(request.Tenant.Value);
            command.Parameters.AddWithValue(request.Actor.ToString());
            command.Parameters.AddWithValue(request.Event.Value ?? string.Empty);
            command.Parameters.AddWithValue(request.Source.DocumentType);
            command.Parameters.AddWithValue(request.Source.DocumentId);
            command.Parameters.AddWithValue(first.Code);
            command.Parameters.AddWithValue(first.MessageAr);
            command.Parameters.AddWithValue(first.MessageEn);
            command.Parameters.AddWithValue(string.Join(" | ", errors.Select(static error => error.Code)));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException)
        {
            // سجلّ الرفض لا يجوز أن يحجب الرفض نفسه عن المستدعي.
        }
    }
}
