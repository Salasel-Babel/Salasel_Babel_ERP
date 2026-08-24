namespace Babel.Ledger.Posting;

/// <summary>سطر قيد بعد أن حُلّ دوره إلى حساب وحُسب مبلغه — قبل أي كتابة.</summary>
internal sealed record PlannedLine
{
    public required int LineNo { get; init; }

    public required string RoleCode { get; init; }

    public required string Qualifier { get; init; }

    /// <summary>رمز الحساب الذي حلّه المحرك من الدور — لا الذي سمّته وحدة (القاعدة 2).</summary>
    public required string AccountCode { get; init; }

    /// <summary>بعملة الحركة.</summary>
    public required decimal Debit { get; init; }

    /// <summary>بعملة الحركة.</summary>
    public required decimal Credit { get; init; }

    /// <summary>بعملة الشركة — وهو ما يفحصه المشغّل المؤجَّل عند COMMIT.</summary>
    public required decimal DebitCompany { get; init; }

    /// <summary>بعملة الشركة — وهو ما يفحصه المشغّل المؤجَّل عند COMMIT.</summary>
    public required decimal CreditCompany { get; init; }

    public required decimal FxRate { get; init; }

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

    /// <summary>عمود البحث المشتقّ. <b>لا يدخل البايتات المُجزَّأة أبداً</b> (فخ-26).</summary>
    public string DescriptionArSearch { get; init; } = string.Empty;
}

/// <summary>صفّ رصيد مجمَّع لحساب واحد داخل قيد واحد.</summary>
internal sealed record PlannedBalance(string AccountCode, decimal Debit, decimal Credit);

/// <summary>
/// القيد كاملاً قبل أن يُكتب: كل حساب محلول، وكل مبلغ محسوب، والتوازن مؤكَّد.
/// <para>
/// وجود هذه المرحلة منفصلة هو ما يجعل <c>AssertBalanced</c> ممكناً <b>قبل كتابة أي
/// صفّ</b> — الطبقة الثالثة من الثلاث، وأقربها إلى الكود وأسرعها في إعطاء رسالة
/// مفهومة. والطبقتان الأبعد (الصلاحيات والمشغّل المؤجَّل) تبقيان صحيحتين حتى لو
/// أخطأت هذه.
/// </para>
/// </summary>
internal sealed record PostingPlan
{
    public required Guid EntryId { get; init; }

    public required Guid CompanyId { get; init; }

    public required string BookId { get; init; }

    public required int FiscalYear { get; init; }

    public required DateOnly EntryDate { get; init; }

    public required string PeriodCode { get; init; }

    public required string Status { get; init; }

    public required string Actor { get; init; }

    public required string ActorSearch { get; init; }

    public required string Memo { get; init; }

    public required string MemoAr { get; init; }

    public required string MemoArSearch { get; init; }

    public required string SourceModule { get; init; }

    public required string SourceDocType { get; init; }

    public required string SourceDocId { get; init; }

    public required string TriggerCode { get; init; }

    public required int Generation { get; init; }

    public required string EventCode { get; init; }

    public required string IdempotencyKey { get; init; }

    public required string Currency { get; init; }

    public Guid? ReversesEntryId { get; init; }

    public string? ReversalReasonAr { get; init; }

    public string? ReversalReasonEn { get; init; }

    public string? ClosedPeriodPermission { get; init; }

    public string? ClosedPeriodAuthoriser { get; init; }

    public required IReadOnlyList<PlannedLine> Lines { get; init; }

    /// <summary>صفوف الأرصدة <b>مرتّبة برمز الحساب تصاعدياً</b> — شرطُ عدم الجمود (فخ-10).</summary>
    public required IReadOnlyList<PlannedBalance> Balances { get; init; }
}
