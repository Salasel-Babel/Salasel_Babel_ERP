using System.Globalization;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.Audit;

/// <summary>
/// حكم إعادة التحقق من سلسلة نطاق واحد.
/// <para>
/// <b>ولماذا «أول تسلسل منحرف» لا «هل السلسلة سليمة»:</b> المدقّق لا يسأل «هل عُبث؟»
/// بل «أين؟ ومتى؟ وما الذي بعده يجب أن يُراجَع؟». إجابة منطقية واحدة لا تصلح تقريراً.
/// </para>
/// </summary>
/// <param name="Ok">هل النطاق سليم كاملاً؟</param>
/// <param name="Checked">عدد السجلات التي فُحصت، بما فيها السجل المنحرف.</param>
/// <param name="FirstDivergentSequence">أول رقم تسلسل منحرف، أو <c>null</c>.</param>
/// <param name="Verdict">رمز الحكم الثابت.</param>
/// <param name="ReasonAr">شرح عربي صالح للعرض في تقرير تدقيق.</param>
/// <param name="Detail">تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة.</param>
public sealed record LedgerChainReport(
    bool Ok,
    int Checked,
    long? FirstDivergentSequence,
    string Verdict,
    string ReasonAr,
    string? Detail);

/// <summary>صفّ في ميزان المراجعة.</summary>
/// <param name="AccountCode">رمز الحساب.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameEn">الاسم الإنجليزي.</param>
/// <param name="Debit">مجموع المدين بعملة الشركة.</param>
/// <param name="Credit">مجموع الدائن بعملة الشركة.</param>
public sealed record TrialBalanceRow(string AccountCode, string NameAr, string NameEn, decimal Debit, decimal Credit);

/// <summary>
/// ميزان المراجعة كاملاً: صفوفه، ومجموعاه، وحكم توازنه.
/// <para>
/// <b>ولماذا المجموعان هنا لا في طبقة أعلى:</b> جمع عمود مالي حسابٌ على المال. في طبقة
/// HTTP يمنعه البند (أ) من القاعدة 13 — ومنعه صحيح؛ وفي المتصفّح يُنتج الفخّ نفسه الذي
/// بُني له شكل السلك، لأن <c>Number</c> في JavaScript فاصلة عائمة ثنائية. والموضع
/// الصحيح <c>sum()</c> على <c>numeric</c> داخل PostgreSQL: جمعٌ مضبوط بلا فاصلة عائمة
/// في أي خطوة، ومن <b>الاستعلام نفسه</b> الذي أنتج الصفوف — لا من استعلام ثانٍ قد يقرأ
/// لقطة أخرى.
/// </para>
/// <para>
/// <b>ولماذا صفٌّ مجهول الاسم (‏tuple) لا سجلٌّ مسمّى:</b> هذا النوع يعبر إلى الجذر
/// التركيبي، والبند (ب) من القاعدة 13 يحصر ما يجوز أن يسمّيه السطح من كل وحدة في قائمة
/// <c>PublishedModuleSurface</c> داخل
/// <c>tests/Babel.ArchitectureTests/Rule13_NoBusinessLogicInTheApi.cs</c>. وقد <b>قيس</b>
/// ذلك: سجلّ باسم <c>Babel.Ledger.Audit.TrialBalanceReport</c> أسقط
/// <c>TheApiNamesOnlyThePublishedSurfaceOfEachModule</c> برسالتها الصريحة. وإضافة اسم
/// إلى تلك القائمة <b>قرار معماري</b> يملكه صاحب ذلك الملف — لا هذا الفرع — ورسالة
/// القاعدة نفسها تقول ذلك. والصفّ المجهول يحمل المعنى نفسه بأعضاء مسمّاة ولا يُدخل اسماً
/// جديداً إلى سطح أي وحدة؛ ويُستبدل بالسجلّ المسمّى متى قُبل السطر المقترح.
/// </para>
/// <list type="bullet">
///   <item><c>Rows</c> — الصفوف مرتّبة برمز الحساب.</item>
///   <item><c>TotalDebit</c> — مجموع المدين بعملة الشركة، من <c>sum()</c> لا من جمع الصفوف.</item>
///   <item><c>TotalCredit</c> — مجموع الدائن بعملة الشركة.</item>
///   <item><c>Balanced</c> — هل تساوى المجموعان؟ يُحسم هنا كي لا يُقارَن مبلغان في
///         JavaScript. وميزانٌ غير متوازن <b>يُرى</b>: لا يُقرَّب ولا يُخفى.</item>
/// </list>
/// </summary>

/// <summary>
/// قراءات التدقيق على الدفتر: إعادة التحقق من السلسلة، وميزان المراجعة.
/// <para>
/// كلاهما <b>قراءة محضة</b> ويعملان بدور التطبيق نفسه — الذي لا يملك
/// <c>UPDATE</c> ولا <c>DELETE</c>. أي أن أداة التدقيق لا تستطيع أن تُصلح ما
/// تكتشفه، وهذا هو المطلوب بالضبط.
/// </para>
/// </summary>
public sealed class LedgerAuditService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly LedgerRuntime _runtime;

    /// <summary>ينشئ خدمة التدقيق.</summary>
    public LedgerAuditService(IEntitlementEnforcer enforcer, LedgerRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _runtime = runtime;
    }

    /// <summary>
    /// يعيد التحقق من سلسلة نطاق (شركة × دفتر × سنة مالية) كاملاً، من بصمة التكوين
    /// حتى الرأس، ويسمّي <b>أول</b> تسلسل منحرف.
    /// <para>
    /// المستند يُعاد بناؤه من <b>الحقيقة المجالية المخزَّنة</b> — الأعمدة نفسها —
    /// لا من <c>canonical_bytes</c> المخزَّنة: مقارنة البايتات بنفسها لا تُثبت شيئاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل الحقيقي — من الاعتماد، لا فاعل نظام. محور «المستخدم الفاعل» يقرأه.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="fiscalYear">السنة المالية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<LedgerChainReport>> VerifyChainAsync(
        TenantId tenant,
        UserId actor,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.VerifyChain", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LedgerChainReport>.Failure(gate.Errors);
        }

        List<ChainRecord> records = await ReadChainAsync(tenant.Value, book, fiscalYear, cancellationToken).ConfigureAwait(false);

        byte[] genesis = JournalEntrySchema.Genesis(
            tenant.Value.ToString("D", CultureInfo.InvariantCulture), book, fiscalYear);

        ChainVerification verification = ChainVerifier.VerifyChain(records, genesis);

        return Result<LedgerChainReport>.Success(new LedgerChainReport(
            verification.Ok,
            verification.Checked,
            verification.FirstDivergentSequence,
            verification.Verdict,
            verification.ReasonAr,
            verification.Detail));
    }

    /// <summary>
    /// ميزان المراجعة <b>من السطور غير القابلة للتعديل</b> لا من جدول الأرصدة.
    /// <para>
    /// وهذا هو الفحص الذي يعني شيئاً: جدول الأرصدة إسقاط، والسطور هي الحقيقة.
    /// مقارنة الاثنين هي التي تُظهر انحراف الإسقاط — وانحرافه هو «الرقم الخاطئ
    /// الصامت» بعينه (ADR-0004 · فخ-06).
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل الحقيقي — من الاعتماد، لا فاعل نظام. محور «المستخدم الفاعل» يقرأه.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="periodCode">رمز الفترة، أو <c>null</c> لكل الفترات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<(IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced)>> TrialBalanceFromLinesAsync(
        TenantId tenant,
        UserId actor,
        string book,
        string? periodCode,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.TrialBalance", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<(IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced)>.Failure(gate.Errors);
        }

        List<TrialBalanceRow> rows = [];
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // ── المجموعان من الاستعلام نفسه، بـ grouping sets ────────────────────
        // مجموعة تجميع للتفصيل وأخرى فارغة للإجمالي: رحلة واحدة، ولقطة واحدة من
        // البيانات (استعلامان يفصل بينهما ترحيل يُنتجان صفوفاً ومجموعاً لا يتطابقان).
        // و‏coalesce لأن مجموعة التجميع الفارغة تُنتج صفّاً ولو كان الدخل صفراً.
        await using NpgsqlCommand command = new(
            """
            select l.account_code, a.name_ar, a.name_en,
                   coalesce(sum(l.debit_company), 0) as debit,
                   coalesce(sum(l.credit_company), 0) as credit,
                   grouping(l.account_code) as is_total
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
              join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
             where l.company_id = $1 and e.book_id = $2 and ($3::text is null or e.period_code = $3)
             group by grouping sets ((l.account_code, a.name_ar, a.name_en), ())
             order by grouping(l.account_code), l.account_code
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(book);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)periodCode ?? DBNull.Value });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.GetInt32(5) == 1)
            {
                totalDebit = reader.GetDecimal(3);
                totalCredit = reader.GetDecimal(4);
                continue;
            }

            rows.Add(new TrialBalanceRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetDecimal(3), reader.GetDecimal(4)));
        }

        // ‏المساواة تُحسم هنا لا عند العميل: مقارنتها في JavaScript تعيد الفخّ نفسه
        // الذي بُني له شكل السلك — ‏Number فاصلة عائمة ثنائية.
        return Result<(IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced)>.Success((rows, totalDebit, totalCredit, totalDebit == totalCredit));
    }

    /// <summary>سطر قيد مقروءاً من التخزين، بكل عموده — لا بستّة منها.</summary>
    private sealed record StoredLine(
        int LineNo,
        string AccountCode,
        string RoleCode,
        string Qualifier,
        decimal Debit,
        decimal Credit,
        string Currency,
        decimal FxRate,
        decimal DebitCompany,
        decimal CreditCompany,
        string? BranchId,
        string? CostCenterId,
        string? ProjectId,
        string? PropertyId,
        string? UnitId,
        string? WarehouseId,
        string? BoqItemId,
        string SubledgerKind,
        string? SubledgerPartyId,
        string Description,
        string DescriptionAr);

    /// <summary>
    /// يقرأ السلسلة كاملة ويعيد بناء كل مستند <b>بمخطّط إصداره المخزَّن</b>.
    /// <para>
    /// وهذا هو موضع «التوزيع بالإصدار» عملياً: سجل كُتب تحت v1 يُعاد بناؤه بحقول
    /// v1 بالضبط، وسجل v2 بحقول v2. سلسلة واحدة قد تحمل الاثنين — أول ترقية في
    /// دفتر قائم تنتج ذلك حتماً — وإعادة بناء الكل بمخطّط واحد تكسر نصف السلسلة
    /// بلا أن يتغيّر حرف في البيانات.
    /// </para>
    /// </summary>
    private async Task<List<ChainRecord>> ReadChainAsync(
        Guid companyId,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<StoredLine>> lines = new();

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand command = new(
            """
            select l.entry_id, l.line_no, l.account_code, l.role_code, l.qualifier,
                   l.debit, l.credit, l.currency, l.fx_rate, l.debit_company, l.credit_company,
                   l.branch_id, l.cost_center_id, l.project_id, l.property_id, l.unit_id,
                   l.warehouse_id, l.boq_item_id, l.subledger_kind, l.subledger_party_id,
                   l.description, l.description_ar
              from ledger.journal_line l
              join ledger.chain_link c on c.entry_id = l.entry_id
             where c.company_id = $1 and c.book_id = $2 and c.fiscal_year = $3
             order by l.entry_id, l.line_no
            """, connection))
        {
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(book);
            command.Parameters.AddWithValue(fiscalYear);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                Guid entryId = reader.GetGuid(0);
                if (!lines.TryGetValue(entryId, out List<StoredLine>? list))
                {
                    list = [];
                    lines[entryId] = list;
                }

                list.Add(new StoredLine(
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetString(7),
                    reader.GetDecimal(8),
                    reader.GetDecimal(9),
                    reader.GetDecimal(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? null : reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetString(16),
                    reader.IsDBNull(17) ? null : reader.GetString(17),
                    reader.GetString(18),
                    reader.IsDBNull(19) ? null : reader.GetString(19),
                    reader.GetString(20),
                    reader.GetString(21)));
            }
        }

        List<ChainRecord> records = [];

        await using NpgsqlCommand chain = new(
            """
            select c.chain_seq, c.canon_version, c.prev_hash, c.entry_hash,
                   e.entry_id, e.entry_no, e.entry_date, e.posted_at, e.status, e.actor,
                   e.memo, e.memo_ar, e.source_doc_type, e.source_doc_id, e.idempotency_key, e.currency,
                   e.period_code, e.source_module, e.posting_trigger_code, e.posting_generation,
                   e.event_code, e.reverses_entry_id, e.reversal_reason_ar, e.reversal_reason_en,
                   e.closed_period_permission, e.closed_period_authoriser
              from ledger.chain_link c
              join ledger.journal_entry e on e.entry_id = c.entry_id
             where c.company_id = $1 and c.book_id = $2 and c.fiscal_year = $3
             order by c.chain_seq
            """, connection);
        chain.Parameters.AddWithValue(companyId);
        chain.Parameters.AddWithValue(book);
        chain.Parameters.AddWithValue(fiscalYear);

        await using NpgsqlDataReader chainReader = await chain.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await chainReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            Guid entryId = chainReader.GetGuid(4);
            string canonVersion = chainReader.GetString(1);
            List<StoredLine> entryLines = lines.TryGetValue(entryId, out List<StoredLine>? found) ? found : [];

            CanonicalDocument document = canonVersion == CanonicalV2.Version
                ? RebuildV2(companyId, book, fiscalYear, chainReader, entryLines)
                : RebuildV1(companyId, book, fiscalYear, chainReader, entryLines);

            records.Add(new ChainRecord
            {
                Sequence = chainReader.GetInt64(0),
                CanonVersion = canonVersion,
                Document = document,
                StoredPreviousHash = chainReader.GetFieldValue<byte[]>(2),
                StoredHash = chainReader.GetFieldValue<byte[]>(3),
            });
        }

        return records;
    }

    /// <summary>إعادة بناء مستند v2 من الحقيقة المجالية المخزَّنة.</summary>
    private static CanonicalDocument RebuildV2(
        Guid companyId, string book, int fiscalYear, NpgsqlDataReader row, List<StoredLine> entryLines)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V2.NewDocument();
        builder.Set("tenant_id", CanonicalValue.Text(companyId.ToString("D", CultureInfo.InvariantCulture)));
        builder.Set("book_id", CanonicalValue.Text(book));
        builder.Set("fiscal_year", CanonicalValue.Integer(fiscalYear));
        builder.Set("entry_id", CanonicalValue.Uuid(row.GetGuid(4)));
        builder.Set("entry_no", CanonicalValue.Integer(row.GetInt64(5)));
        builder.Set("entry_date", CanonicalValue.Date(row.GetFieldValue<DateOnly>(6)));
        builder.Set("period_code", CanonicalValue.Text(row.GetString(16)));
        builder.Set("posted_at", CanonicalValue.Instant(row.GetFieldValue<DateTime>(7)));
        builder.Set("status", CanonicalValue.Token(row.GetString(8)));
        builder.Set("reverses_entry_id", row.IsDBNull(21)
            ? CanonicalValue.Null()
            : CanonicalValue.Uuid(row.GetGuid(21)));
        builder.Set("reversal_reason_ar", CanonicalValue.TextOrNull(row.IsDBNull(22) ? null : row.GetString(22)));
        builder.Set("reversal_reason_en", CanonicalValue.TextOrNull(row.IsDBNull(23) ? null : row.GetString(23)));
        builder.Set("source_module", CanonicalValue.Text(row.GetString(17)));
        builder.Set("source_doc_type", CanonicalValue.Text(row.GetString(12)));
        builder.Set("source_doc_id", CanonicalValue.Text(row.GetString(13)));
        builder.Set("posting_trigger_code", CanonicalValue.Text(row.GetString(18)));
        builder.Set("posting_generation", CanonicalValue.Integer(row.GetInt32(19)));
        builder.Set("event_code", CanonicalValue.Text(row.GetString(20)));
        builder.Set("idempotency_key", CanonicalValue.Text(row.GetString(14)));
        builder.Set("currency", CanonicalValue.Token(row.GetString(15)));
        builder.Set("actor", CanonicalValue.Text(row.GetString(9)));
        builder.Set("closed_period_permission", CanonicalValue.TextOrNull(row.IsDBNull(24) ? null : row.GetString(24)));
        builder.Set("closed_period_authoriser", CanonicalValue.TextOrNull(row.IsDBNull(25) ? null : row.GetString(25)));
        builder.Set("memo", CanonicalValue.Text(row.GetString(10)));
        builder.Set("memo_ar", CanonicalValue.Text(row.GetString(11)));

        builder.SetGroup("lines", entryLines.Select(static line => new Action<CanonicalItemBuilder>(item =>
        {
            item.Set("line_no", CanonicalValue.Integer(line.LineNo));
            item.Set("account_code", CanonicalValue.Text(line.AccountCode));
            item.Set("role_code", CanonicalValue.Text(line.RoleCode));
            item.Set("qualifier", CanonicalValue.Text(line.Qualifier));
            item.Set("debit", CanonicalValue.Amount(line.Debit));
            item.Set("credit", CanonicalValue.Amount(line.Credit));
            item.Set("currency", CanonicalValue.Token(line.Currency));
            item.Set("fx_rate", CanonicalValue.Rate(line.FxRate));
            item.Set("debit_company", CanonicalValue.Amount(line.DebitCompany));
            item.Set("credit_company", CanonicalValue.Amount(line.CreditCompany));
            item.Set("branch_id", CanonicalValue.TextOrNull(line.BranchId));
            item.Set("cost_center_id", CanonicalValue.TextOrNull(line.CostCenterId));
            item.Set("project_id", CanonicalValue.TextOrNull(line.ProjectId));
            item.Set("property_id", CanonicalValue.TextOrNull(line.PropertyId));
            item.Set("unit_id", CanonicalValue.TextOrNull(line.UnitId));
            item.Set("warehouse_id", CanonicalValue.TextOrNull(line.WarehouseId));
            item.Set("boq_item_id", CanonicalValue.TextOrNull(line.BoqItemId));
            item.Set("tax_code", CanonicalValue.Null());
            item.Set("subledger_kind", CanonicalValue.Text(line.SubledgerKind));
            item.Set("subledger_party_id", CanonicalValue.TextOrNull(line.SubledgerPartyId));
            item.Set("description", CanonicalValue.Text(line.Description));
            item.Set("description_ar", CanonicalValue.Text(line.DescriptionAr));
        })));

        return builder.Build();
    }

    /// <summary>إعادة بناء مستند v1 — <b>مجمَّد</b>، بحقول v1 وحدها.</summary>
    private static CanonicalDocument RebuildV1(
        Guid companyId, string book, int fiscalYear, NpgsqlDataReader row, List<StoredLine> entryLines)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V1.NewDocument();
        builder.Set("tenant_id", CanonicalValue.Text(companyId.ToString("D", CultureInfo.InvariantCulture)));
        builder.Set("book_id", CanonicalValue.Text(book));
        builder.Set("fiscal_year", CanonicalValue.Integer(fiscalYear));
        builder.Set("entry_id", CanonicalValue.Uuid(row.GetGuid(4)));
        builder.Set("entry_no", CanonicalValue.Integer(row.GetInt64(5)));
        builder.Set("entry_date", CanonicalValue.Date(row.GetFieldValue<DateOnly>(6)));
        builder.Set("posted_at", CanonicalValue.Instant(row.GetFieldValue<DateTime>(7)));
        builder.Set("status", CanonicalValue.Token(row.GetString(8)));
        builder.Set("actor", CanonicalValue.Text(row.GetString(9)));
        builder.Set("memo", CanonicalValue.Text(row.GetString(10)));
        builder.Set("memo_ar", CanonicalValue.Text(row.GetString(11)));
        builder.Set("source_ref", CanonicalValue.Text(row.GetString(12) + "/" + row.GetString(13)));
        builder.Set("idempotency_key", CanonicalValue.Text(row.GetString(14)));
        builder.Set("currency", CanonicalValue.Token(row.GetString(15)));

        builder.SetGroup("lines", entryLines.Select(static line => new Action<CanonicalItemBuilder>(item =>
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
}
