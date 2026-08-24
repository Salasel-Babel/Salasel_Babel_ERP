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
    /// <param name="book">الدفتر.</param>
    /// <param name="fiscalYear">السنة المالية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<LedgerChainReport>> VerifyChainAsync(
        TenantId tenant,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, UserId.SystemActor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.VerifyChain", cancellationToken)
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
    /// <param name="book">الدفتر.</param>
    /// <param name="periodCode">رمز الفترة، أو <c>null</c> لكل الفترات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<TrialBalanceRow>>> TrialBalanceFromLinesAsync(
        TenantId tenant,
        string book,
        string? periodCode,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, UserId.SystemActor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.TrialBalance", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<TrialBalanceRow>>.Failure(gate.Errors);
        }

        List<TrialBalanceRow> rows = [];

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select l.account_code, a.name_ar, a.name_en,
                   sum(l.debit_company) as debit, sum(l.credit_company) as credit
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
              join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
             where l.company_id = $1 and e.book_id = $2 and ($3::text is null or e.period_code = $3)
             group by l.account_code, a.name_ar, a.name_en
             order by l.account_code
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(book);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)periodCode ?? DBNull.Value });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new TrialBalanceRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetDecimal(3), reader.GetDecimal(4)));
        }

        return Result<IReadOnlyList<TrialBalanceRow>>.Success(rows);
    }

    private async Task<List<ChainRecord>> ReadChainAsync(
        Guid companyId,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<(int LineNo, string Account, decimal Debit, decimal Credit, string? CostCenter, string Description)>> lines = new();

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand command = new(
            """
            select l.entry_id, l.line_no, l.account_code, l.debit, l.credit, l.cost_center_id, l.description
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
                if (!lines.TryGetValue(entryId, out var list))
                {
                    list = [];
                    lines[entryId] = list;
                }

                list.Add((reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6)));
            }
        }

        List<ChainRecord> records = [];

        await using NpgsqlCommand chain = new(
            """
            select c.chain_seq, c.canon_version, c.prev_hash, c.entry_hash,
                   e.entry_id, e.entry_no, e.entry_date, e.posted_at, e.status, e.actor,
                   e.memo, e.memo_ar, e.source_doc_type, e.source_doc_id, e.idempotency_key, e.currency
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
            CanonicalDocumentBuilder builder = JournalEntrySchema.V1.NewDocument();
            builder.Set("tenant_id", CanonicalValue.Text(companyId.ToString("D", CultureInfo.InvariantCulture)));
            builder.Set("book_id", CanonicalValue.Text(book));
            builder.Set("fiscal_year", CanonicalValue.Integer(fiscalYear));
            builder.Set("entry_id", CanonicalValue.Uuid(entryId));
            builder.Set("entry_no", CanonicalValue.Integer(chainReader.GetInt64(5)));
            builder.Set("entry_date", CanonicalValue.Date(chainReader.GetFieldValue<DateOnly>(6)));
            builder.Set("posted_at", CanonicalValue.Instant(chainReader.GetFieldValue<DateTime>(7)));
            builder.Set("status", CanonicalValue.Token(chainReader.GetString(8)));
            builder.Set("actor", CanonicalValue.Text(chainReader.GetString(9)));
            builder.Set("memo", CanonicalValue.Text(chainReader.GetString(10)));
            builder.Set("memo_ar", CanonicalValue.Text(chainReader.GetString(11)));
            builder.Set("source_ref", CanonicalValue.Text(chainReader.GetString(12) + "/" + chainReader.GetString(13)));
            builder.Set("idempotency_key", CanonicalValue.Text(chainReader.GetString(14)));
            builder.Set("currency", CanonicalValue.Token(chainReader.GetString(15)));

            builder.SetGroup("lines", lines[entryId].Select(static line => new Action<CanonicalItemBuilder>(item =>
            {
                item.Set("line_no", CanonicalValue.Integer(line.LineNo));
                item.Set("account_code", CanonicalValue.Text(line.Account));
                item.Set("debit", CanonicalValue.Amount(line.Debit));
                item.Set("credit", CanonicalValue.Amount(line.Credit));
                item.Set("cost_center", CanonicalValue.TextOrNull(line.CostCenter));
                item.Set("description", CanonicalValue.Text(line.Description));
            })));

            records.Add(new ChainRecord
            {
                Sequence = chainReader.GetInt64(0),
                CanonVersion = chainReader.GetString(1),
                Document = builder.Build(),
                StoredPreviousHash = chainReader.GetFieldValue<byte[]>(2),
                StoredHash = chainReader.GetFieldValue<byte[]>(3),
            });
        }

        return records;
    }
}
