using System.Globalization;
using Babel.Projects.Surface;

namespace Babel.Api.Wire;

/// <summary>
/// النقل بين شكل السلك وسطح وحدة المقاولات المنشور — <b>نقلٌ لا قرار</b>.
/// <para>
/// لا حساب ولا دور ولا حدث ولا قاعدة توازن هنا: هذا الملفّ يقرأ نصّاً ويكتب نصّاً،
/// ويرفض ما لا يطابق النحو المنشور برسالةٍ تسمّي الحقل. والقرار المحاسبي في الوحدة،
/// والحساب في المصفوفة (القاعدة 13).
/// </para>
/// <para>
/// <b>والمال والكمّية والنسبة نصوصٌ على الطرفين:</b> ‏JSON لا يملك نوعاً عشرياً، وأغلب
/// العملاء يمرّرون الرمز الرقمي على فاصلة عائمة ثنائية — فيقع فقدان الدقّة قبل أن يصل
/// الطلب، ولا يملك الخادم استرداده.
/// </para>
/// </summary>
internal static class ProjectsMapping
{
    private const int CodeLength = 64;
    private const int NameLength = 200;
    private const int DescriptionLength = 400;
    private const int UnitLength = 16;
    private const int IdentifierLength = 64;
    private const int LanguageTagLength = 35;

    // ── الطلبات ──────────────────────────────────────────────────────────────

    /// <summary>يقرأ طلب تسجيل مشروع.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsProjectRequest ToProjectRequest(ProjectRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsProjectRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            WireMapping.ReadRequiredText(dto.NameAr, "nameAr", NameLength),
            Translations(dto.NameTranslations, "nameTranslations"),
            WireMapping.ReadDate(dto.StartedOn, "startedOn"));
    }

    /// <summary>يقرأ طلب إنشاء عقد مقاولة.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsContractRequest ToContractRequest(ProjectContractRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsContractRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.ProjectId, "projectId"),
            WireMapping.ReadRequiredText(dto.CustomerPartyId, "customerPartyId", IdentifierLength),
            WireMapping.ReadDate(dto.SignedOn, "signedOn"),
            WireNumbers.ParseStrict(dto.RetentionRate.Raw, WireNumbers.RateScale, "retentionRate"),
            ReadMonths(dto.GuaranteeMonths, "guaranteeMonths"),
            BoqItems(dto.Items, "items"));
    }

    /// <summary>يقرأ طلب أمر تغييري.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsChangeOrderRequest ToChangeOrderRequest(ChangeOrderRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsChangeOrderRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.ContractId, "contractId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            WireMapping.ReadRequiredText(dto.ReasonAr, "reasonAr", DescriptionLength),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", NameLength),
            BoqItems(dto.AddedItems, "addedItems"));
    }

    /// <summary>يقرأ طلب تسجيل مقاول من الباطن.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsSubcontractorRequest ToSubcontractorRequest(SubcontractorRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsSubcontractorRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            WireMapping.ReadRequiredText(dto.NameAr, "nameAr", NameLength),
            Translations(dto.NameTranslations, "nameTranslations"),
            WireMapping.ReadText(dto.VatNumber ?? string.Empty, "vatNumber", 32));
    }

    /// <summary>يقرأ طلب إنشاء عقد باطن.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsSubcontractRequest ToSubcontractRequest(SubcontractRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        List<ProjectsSubcontractLineRequest> lines = [];
        int index = 0;

        foreach (SubcontractLineRequestDto line in dto.Lines)
        {
            string field = FormattableString.Invariant($"lines[{index}]");
            index++;

            lines.Add(new ProjectsSubcontractLineRequest(
                WireMapping.ReadRequiredText(line.Code, field + ".code", CodeLength),
                WireMapping.ReadRequiredText(line.DescriptionAr, field + ".descriptionAr", DescriptionLength),
                Measure(line.ContractQuantity, field + ".contractQuantity"),
                WireNumbers.ParseStrict(line.UnitRate.Raw, WireNumbers.MoneyScale, field + ".unitRate")));
        }

        return new ProjectsSubcontractRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.ProjectId, "projectId"),
            WireMapping.ReadGuid(dto.SubcontractorId, "subcontractorId"),
            WireMapping.ReadDate(dto.SignedOn, "signedOn"),
            WireNumbers.ParseStrict(dto.RetentionRate.Raw, WireNumbers.RateScale, "retentionRate"),
            ReadMonths(dto.GuaranteeMonths, "guaranteeMonths"),
            lines);
    }

    /// <summary>يقرأ طلب إنشاء مستخلص — عميلٍ كان أو باطن.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsCertificateRequest ToCertificateRequest(CertificateRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.SequenceNo < 1)
        {
            throw WireNumbers.Reject(
                "wire.sequence.out_of_range",
                "sequenceNo",
                "تسلسل المستخلص داخل عقده يبدأ من واحد.",
                "A certificate's sequence within its contract starts at one.");
        }

        List<ProjectsCertificateLineRequest> lines = [];
        int index = 0;

        foreach (CertificateLineRequestDto line in dto.Lines)
        {
            string field = FormattableString.Invariant($"lines[{index}]");
            index++;

            lines.Add(new ProjectsCertificateLineRequest(
                line.ItemId is null ? null : WireMapping.ReadGuid(line.ItemId, field + ".itemId"),
                WireMapping.ReadRequiredText(line.LineKind, field + ".lineKind", 16),
                WireMapping.ReadRequiredText(line.DescriptionAr, field + ".descriptionAr", DescriptionLength),
                Measure(line.CumulativeQuantity, field + ".cumulativeQuantity"),
                WireNumbers.ParseStrict(line.Amount.Raw, WireNumbers.MoneyScale, field + ".amount")));
        }

        return new ProjectsCertificateRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.OwnerId, "ownerId"),
            dto.SequenceNo,
            WireMapping.ReadDate(dto.PeriodFrom, "periodFrom"),
            WireMapping.ReadDate(dto.PeriodTo, "periodTo"),
            lines);
    }

    /// <summary>يقرأ طلب صرف دفعة مقدمة لمقاول.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsAdvanceRequest ToAdvanceRequest(SubcontractorAdvanceRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsAdvanceRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.SubcontractId, "subcontractId"),
            WireMapping.ReadDate(dto.PaidOn, "paidOn"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", IdentifierLength),
            dto.GuaranteeId is null ? null : WireMapping.ReadGuid(dto.GuaranteeId, "guaranteeId"));
    }

    /// <summary>يقرأ طلب إفراج عن محتجز دائن.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsRetentionReleaseRequest ToReleaseRequest(RetentionReleaseRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsRetentionReleaseRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.RetentionMovementId, "retentionMovementId"),
            WireMapping.ReadDate(dto.ReleasedOn, "releasedOn"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", NameLength));
    }

    /// <summary>يقرأ طلب تحصيل محتجز مدين.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsRetentionCollectionRequest ToCollectionRequest(RetentionCollectionRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProjectsRetentionCollectionRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.RetentionMovementId, "retentionMovementId"),
            WireMapping.ReadDate(dto.CollectedOn, "collectedOn"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", IdentifierLength));
    }

    /// <summary>يقرأ طلب تسجيل خطاب ضمان.</summary>
    /// <param name="dto">الجسم.</param>
    public static ProjectsGuaranteeRequest ToGuaranteeRequest(GuaranteeRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.ContractId is null && dto.SubcontractId is null)
        {
            throw WireNumbers.Reject(
                "wire.field.missing_either",
                "contractId",
                "خطاب الضمان يخصّ عقد عميل أو عقد باطن — وواحدٌ منهما إلزامي. وخطابٌ بلا عقدٍ "
                + "يخصّه سجلٌّ لا يُطالَب به أحد.",
                "A guarantee belongs to a client contract or a subcontract — one of them is required. A "
                + "guarantee attached to neither is a record nobody can call upon.");
        }

        return new ProjectsGuaranteeRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadRequiredText(dto.Kind, "kind", 32),
            dto.ContractId is null ? null : WireMapping.ReadGuid(dto.ContractId, "contractId"),
            dto.SubcontractId is null ? null : WireMapping.ReadGuid(dto.SubcontractId, "subcontractId"),
            WireMapping.ReadRequiredText(dto.IssuerNameAr, "issuerNameAr", NameLength),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadDate(dto.EffectiveFrom, "effectiveFrom"),
            WireMapping.ReadDate(dto.ExpiresOn, "expiresOn"),
            WireMapping.ReadRequiredText(dto.AttachmentId, "attachmentId", IdentifierLength));
    }

    // ── الأجوبة ──────────────────────────────────────────────────────────────

    /// <summary>يكتب مشروعاً على السلك.</summary>
    /// <param name="project">المشروع.</param>
    public static ProjectDto ToDto(ProjectsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectDto(
            Id(project.Id),
            project.Code,
            project.NameAr,
            Names(project.NameTranslations),
            Date(project.StartedOn),
            project.IsActive,
            [.. project.Contracts.Select(static c => new ProjectContractSummaryDto(Id(c.Id), c.Number, c.CurrencyCode))]);
    }

    /// <summary>يكتب قائمة المشاريع.</summary>
    /// <param name="projects">المشاريع.</param>
    public static ProjectListDto ToDto(IReadOnlyList<ProjectsProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        return new ProjectListDto(projects.Count, [.. projects.Select(ToDto)]);
    }

    /// <summary>يكتب عقداً على السلك.</summary>
    /// <param name="contract">العقد.</param>
    public static ProjectContractDto ToDto(ProjectsContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return new ProjectContractDto(
            Id(contract.Id),
            contract.Number,
            Id(contract.ProjectId),
            contract.ProjectCode,
            contract.CustomerPartyId,
            contract.CurrencyCode,
            Date(contract.SignedOn),
            Rate(contract.RetentionRate),
            contract.GuaranteeMonths,
            Pending(contract.PendingPolicy));
    }

    /// <summary>يكتب بنود جدول الكميات.</summary>
    /// <param name="items">البنود.</param>
    public static BoqItemListDto ToDto(IReadOnlyList<ProjectsBoqItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new BoqItemListDto(items.Count, [.. items.Select(BoqItem)]);
    }

    /// <summary>يكتب أمراً تغييرياً.</summary>
    /// <param name="order">الأمر.</param>
    public static ChangeOrderDto ToDto(ProjectsChangeOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new ChangeOrderDto(
            Id(order.Id),
            order.Number,
            Id(order.ContractId),
            Date(order.IssuedOn),
            order.ReasonAr,
            order.ApprovedBy,
            [.. order.AddedItems.Select(BoqItem)]);
    }

    /// <summary>يكتب قائمة أوامر تغييرية.</summary>
    /// <param name="orders">الأوامر.</param>
    public static ChangeOrderListDto ToDto(IReadOnlyList<ProjectsChangeOrder> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        return new ChangeOrderListDto(orders.Count, [.. orders.Select(ToDto)]);
    }

    /// <summary>يكتب مقاولاً من الباطن.</summary>
    /// <param name="party">المقاول.</param>
    public static SubcontractorDto ToDto(ProjectsSubcontractor party)
    {
        ArgumentNullException.ThrowIfNull(party);

        return new SubcontractorDto(
            Id(party.Id), party.Code, party.NameAr, Names(party.NameTranslations), party.VatNumber, party.IsActive);
    }

    /// <summary>يكتب عقد باطن.</summary>
    /// <param name="subcontract">العقد.</param>
    public static SubcontractDto ToDto(ProjectsSubcontract subcontract)
    {
        ArgumentNullException.ThrowIfNull(subcontract);

        return new SubcontractDto(
            Id(subcontract.Id),
            subcontract.Number,
            Id(subcontract.ProjectId),
            subcontract.ProjectCode,
            Id(subcontract.SubcontractorId),
            subcontract.CurrencyCode,
            Date(subcontract.SignedOn),
            Rate(subcontract.RetentionRate),
            subcontract.GuaranteeMonths,
            Pending(subcontract.PendingPolicy));
    }

    /// <summary>يكتب بنود عقد الباطن.</summary>
    /// <param name="lines">البنود.</param>
    public static SubcontractLineListDto ToDto(IReadOnlyList<ProjectsSubcontractLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return new SubcontractLineListDto(
            lines.Count,
            [
                .. lines.Select(static line => new SubcontractLineDto(
                    Id(line.Id),
                    line.Code,
                    line.LineNo,
                    line.DescriptionAr,
                    Measure(line.ContractQuantity),
                    Money(line.UnitRate))),
            ]);
    }

    /// <summary>يكتب مستخلصاً على السلك.</summary>
    /// <param name="certificate">المستخلص.</param>
    public static CertificateDto ToDto(ProjectsCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new CertificateDto(
            Id(certificate.Id),
            certificate.Number,
            Id(certificate.OwnerId),
            certificate.SequenceNo,
            Date(certificate.PeriodFrom),
            Date(certificate.PeriodTo),
            certificate.State,
            Rate(certificate.RetentionRate),
            [
                .. certificate.Lines.Select(static line => new CertificateLineDto(
                    Id(line.Id),
                    line.LineNo,
                    line.LineKind,
                    line.ItemId is { } item ? Id(item) : null,
                    line.ItemCode,
                    line.DescriptionAr,
                    Measure(line.CumulativeQuantity),
                    Measure(line.PreviousQuantity),
                    Money(line.Amount))),
            ],
            Pending(certificate.PendingPolicy),
            certificate.EntryId is { } entry ? Id(entry) : null,
            certificate.AlreadyPosted);
    }

    /// <summary>يكتب قائمة مستخلصات.</summary>
    /// <param name="certificates">المستخلصات.</param>
    public static CertificateListDto ToDto(IReadOnlyList<ProjectsCertificate> certificates)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        return new CertificateListDto(certificates.Count, [.. certificates.Select(ToDto)]);
    }

    /// <summary>يكتب مستنداً مالياً على السلك.</summary>
    /// <param name="document">المستند.</param>
    public static ProjectsDocumentDto ToDto(ProjectsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ProjectsDocumentDto(
            Id(document.Id),
            document.Number,
            document.State,
            Money(document.Amount),
            document.EntryId is { } entry ? Id(entry) : null,
            document.AlreadyPosted);
    }

    /// <summary>يكتب خطاب ضمان.</summary>
    /// <param name="guarantee">الضمان.</param>
    public static GuaranteeDto ToDto(ProjectsGuarantee guarantee)
    {
        ArgumentNullException.ThrowIfNull(guarantee);

        return new GuaranteeDto(
            Id(guarantee.Id),
            guarantee.Number,
            guarantee.Kind,
            guarantee.ContractId is { } contract ? Id(contract) : null,
            guarantee.SubcontractId is { } subcontract ? Id(subcontract) : null,
            guarantee.IssuerNameAr,
            Money(guarantee.Amount),
            Date(guarantee.EffectiveFrom),
            Date(guarantee.ExpiresOn),
            guarantee.AttachmentId);
    }

    /// <summary>يكتب سجلّ المحتجزات.</summary>
    /// <param name="register">السجلّ.</param>
    public static RetentionRegisterDto ToDto(ProjectsRetentionRegister register)
    {
        ArgumentNullException.ThrowIfNull(register);

        return new RetentionRegisterDto(
            Date(register.AsOf),
            [
                .. register.Rows.Select(static row => new RetentionRegisterRowDto(
                    Id(row.MovementId),
                    row.Side,
                    row.PartyKind,
                    row.PartyId,
                    row.ProjectCode,
                    row.DocumentType,
                    row.DocumentId,
                    Money(row.Amount),
                    Money(row.Outstanding),
                    Date(row.MovedOn),
                    Date(row.DueOn))),
            ],
            Money(register.ReceivableTotal),
            Money(register.PayableTotal));
    }

    /// <summary>يكتب كشف المقاولين.</summary>
    /// <param name="statement">الكشف.</param>
    public static SubcontractorStatementDto ToDto(ProjectsSubcontractorStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        return new SubcontractorStatementDto(
            Date(statement.AsOf),
            [
                .. statement.Rows.Select(static row => new SubcontractorStatementRowDto(
                    Id(row.SubcontractorId),
                    row.Code,
                    row.NameAr,
                    Names(row.NameTranslations),
                    Money(row.Effect))),
            ],
            Money(statement.SubledgerTotal),
            Money(statement.ControlTotal),
            Money(statement.Divergence),
            statement.IsReconciled);
    }

    /// <summary>يكتب موقف العقد.</summary>
    /// <param name="position">الموقف.</param>
    public static ContractPositionDto ToDto(ProjectsContractPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        return new ContractPositionDto(
            Id(position.ContractId),
            position.ContractNumber,
            position.PostedCertificateCount,
            Money(position.RetentionOutstanding),
            Money(position.AdvanceOutstanding),
            Pending(position.PendingPolicy));
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    private static BoqItemDto BoqItem(ProjectsBoqItem item) => new(
        Id(item.Id),
        item.Code,
        item.LineNo,
        item.DescriptionAr,
        Measure(item.ContractQuantity),
        Money(item.UnitRate),
        item.ChangeOrderId is { } order ? Id(order) : null);

    private static List<ProjectsBoqItemRequest> BoqItems(IReadOnlyList<BoqItemRequestDto> items, string field)
    {
        List<ProjectsBoqItemRequest> read = [];
        int index = 0;

        foreach (BoqItemRequestDto item in items)
        {
            string path = FormattableString.Invariant($"{field}[{index}]");
            index++;

            read.Add(new ProjectsBoqItemRequest(
                WireMapping.ReadRequiredText(item.Code, path + ".code", CodeLength),
                WireMapping.ReadRequiredText(item.DescriptionAr, path + ".descriptionAr", DescriptionLength),
                Measure(item.ContractQuantity, path + ".contractQuantity"),
                WireNumbers.ParseStrict(item.UnitRate.Raw, WireNumbers.MoneyScale, path + ".unitRate")));
        }

        return read;
    }

    private static ProjectsMeasure Measure(MeasureRequestDto? dto, string field)
    {
        if (dto is null)
        {
            throw WireNumbers.Reject(
                "wire.text.missing",
                field,
                "الكمّية بوحدتها مفقودة — ولا كمّية مجرّدة تعبر هذا الحدّ.",
                "The quantity with its unit is missing; no bare quantity crosses this boundary.");
        }

        return new ProjectsMeasure(
            WireNumbers.ParseStrict(dto.Magnitude.Raw, WireNumbers.QuantityScale, field + ".magnitude"),
            WireMapping.ReadRequiredText(dto.Unit, field + ".unit", UnitLength));
    }

    private static List<ProjectsNameValue> Translations(IReadOnlyList<NameValueDto> translations, string field)
    {
        List<ProjectsNameValue> read = [];
        int index = 0;

        foreach (NameValueDto translation in translations)
        {
            string path = FormattableString.Invariant($"{field}[{index}]");
            index++;

            read.Add(new ProjectsNameValue(
                WireMapping.ReadRequiredText(translation.Name, path + ".name", LanguageTagLength),
                WireMapping.ReadRequiredText(translation.Value, path + ".value", NameLength)));
        }

        return read;
    }

    private static int ReadMonths(int value, string field)
    {
        if (value is < 0 or > 600)
        {
            throw WireNumbers.Reject(
                "wire.integer.out_of_range",
                field,
                "فترة الضمان بالأشهر بين صفر وستمئة — والحدّ مكتوب صراحةً لا متروكاً.",
                "The guarantee period in months is between zero and six hundred; the bound is written, not left open.");
        }

        return value;
    }

    private static IReadOnlyList<NameValueDto> Names(IReadOnlyList<ProjectsNameValue> translations)
        => [.. translations.Select(static t => new NameValueDto(t.Name, t.Value))];

    private static IReadOnlyList<ProjectsPendingItemDto> Pending(IReadOnlyList<ProjectsPendingItem> items)
        => [.. items.Select(static item => new ProjectsPendingItemDto(item.Code, item.TitleAr, item.TitleEn, item.SourceRef))];

    private static MeasureDto Measure(ProjectsMeasure measure)
        => new(WireNumbers.FormatQuantity(measure.Magnitude), measure.Unit);

    private static string Money(decimal value) => WireNumbers.FormatMoney(value);

    /// <summary>
    /// النسبة نصّاً بمقياس ثمانٍ — وهو المقياس المنشور للنِّسَب على هذا السطح، لا مقياس المال.
    /// </summary>
    private static string Rate(decimal value) => value.ToString("0.00000000", CultureInfo.InvariantCulture);

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
