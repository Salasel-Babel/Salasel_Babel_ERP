using System.Globalization;
using Babel.Hr.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Wire;

/// <summary>
/// النقل بين السلك والسطح المنشور لوحدة الموارد البشرية — <b>نقلٌ لا قرار</b>.
/// <para>
/// لا فحص محاسبي هنا ولا حكم: النسبة الغائبة، والمبلغ السالب، وطريقة التسوية المجهولة،
/// وطرف الخزينة الفارغ — كلّها تُرجع من الوحدة برموزها ورسائلها. وما يقع هنا تحويل
/// شكل: نصٌّ إلى <c>decimal</c> بمقياسه المعلن، ونصٌّ إلى <c>DateOnly</c> ميلادي بثقافة
/// ثابتة، و<c>Guid</c> بصيغة 8-4-4-4-12.
/// </para>
/// <para>
/// <b>وكل مبلغ يعبر هذا الحدّ نصّاً</b>: رمزٌ رقمي في حقل مالي يمرّ عند أغلب العملاء على
/// فاصلة عائمة ثنائية، فيقع فقدان الدقّة <b>قبل أن يصل الطلب</b>. <b>والنِّسَب بمقياس
/// ثمانٍ لا أربع</b>: النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة.
/// </para>
/// </summary>
internal static class PayrollMapping
{
    /// <summary>أقصى طول لمرجع نصّي (مصدر نظامي أو أساس قياس).</summary>
    private const int ReferenceLength = 400;

    /// <summary>أقصى طول لرمز أو مفتاح.</summary>
    private const int CodeLength = 64;

    /// <summary>أقصى عدد سطور في طلب واحد — الحدّ المعلن نفسه في سائر السطح.</summary>
    private const int MaxLines = WireMapping.MaxLines;

    // ── الطلبات ──────────────────────────────────────────────────────────────

    /// <summary>يحوّل طلب تسجيل موظف.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrEmployeeRequest ToEmployeeRequest(HrEmployeeRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrEmployeeRequest(
            Name(dto.NameAr, dto.NameTranslations, "nameTranslations"),
            WireMapping.ReadRequiredText(dto.ClassCode, "classCode", CodeLength),
            WireMapping.ReadText(dto.CostCenterId, "costCenterId", CodeLength),
            WireMapping.ReadDate(dto.HiredOn, "hiredOn"),
            new HrIdentityRequest(
                WireMapping.ReadRequiredText(dto.Identity.NationalId, "identity.nationalId", 32),
                WireMapping.ReadRequiredText(dto.Identity.Iban, "identity.iban", CodeLength),
                WireMapping.ReadDate(dto.Identity.BirthDate, "identity.birthDate")));
    }

    /// <summary>يحوّل طلب إنهاء خدمة.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrTerminationRequest ToTerminationRequest(HrTerminationRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrTerminationRequest(
            WireMapping.ReadDate(dto.EndedOn, "endedOn"),
            WireMapping.ReadRequiredText(dto.ReasonKey, "reasonKey", CodeLength));
    }

    /// <summary>يحوّل طلب تعريف مكوّن أجر.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrPayComponentRequest ToPayComponentRequest(HrPayComponentRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrPayComponentRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            Name(dto.NameAr, dto.NameTranslations, "nameTranslations"),
            WireMapping.ReadRequiredText(dto.Kind, "kind", 16),
            dto.EntersContributoryWage,
            dto.EntersEndOfServiceBase);
    }

    /// <summary>يحوّل طلب إسناد قيمة مكوّن.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrPayElementRequest ToPayElementRequest(HrPayElementRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrPayElementRequest(
            WireMapping.ReadRequiredText(dto.ComponentCode, "componentCode", CodeLength),
            WireMapping.ReadDate(dto.EffectiveFrom, "effectiveFrom"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"));
    }

    /// <summary>يحوّل طلب إيداع نِسَب.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrPayrollSettingsRequest ToPayrollSettingsRequest(HrPayrollSettingsRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrPayrollSettingsRequest(
            WireMapping.ReadRequiredText(dto.ClassCode, "classCode", CodeLength),
            WireMapping.ReadDate(dto.EffectiveFrom, "effectiveFrom"),
            WireNumbers.ParseStrict(dto.EmployerRate.Raw, WireNumbers.RateScale, "employerRate"),
            WireNumbers.ParseStrict(dto.EmployeeRate.Raw, WireNumbers.RateScale, "employeeRate"),
            WireNumbers.ParseStrict(dto.MinimumContributoryWage.Raw, WireNumbers.MoneyScale, "minimumContributoryWage"),
            WireNumbers.ParseStrict(dto.MaximumContributoryWage.Raw, WireNumbers.MoneyScale, "maximumContributoryWage"),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", CodeLength),
            WireMapping.ReadDate(dto.ApprovedOn, "approvedOn"),
            WireMapping.ReadRequiredText(dto.SourceRef, "sourceRef", ReferenceLength));
    }

    /// <summary>يحوّل طلب إنشاء مسيّر.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrPayrollRunRequest ToPayrollRunRequest(HrPayrollRunRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrPayrollRunRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            Period(dto.PeriodCode, "periodCode"),
            WireMapping.ReadDate(dto.PeriodStart, "periodStart"),
            WireMapping.ReadDate(dto.PeriodEnd, "periodEnd"));
    }

    /// <summary>يحوّل طلب إنشاء سند صرف رواتب.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrPayrollPaymentRequest ToPayrollPaymentRequest(HrPayrollPaymentRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrPayrollPaymentRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.RunId, "runId"),
            WireMapping.ReadDate(dto.PaidOn, "paidOn"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength));
    }

    /// <summary>يحوّل طلب إنشاء سند سداد تأمينات.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrSocialInsurancePaymentRequest ToSocialInsuranceRequest(HrSocialInsurancePaymentRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrSocialInsurancePaymentRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            Period(dto.PeriodCode, "periodCode"),
            WireMapping.ReadDate(dto.PaidOn, "paidOn"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength));
    }

    /// <summary>يحوّل طلب قيد جزاء.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrDeductionRequest ToDeductionRequest(HrDeductionRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrDeductionRequest(
            WireMapping.ReadGuid(dto.EmployeeId, "employeeId"),
            Period(dto.PeriodCode, "periodCode"),
            WireMapping.ReadRequiredText(dto.CategoryKey, "categoryKey", CodeLength),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", CodeLength),
            WireMapping.ReadDate(dto.ApprovedOn, "approvedOn"));
    }

    /// <summary>يحوّل طلب إنشاء سلفة.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrAdvanceRequest ToAdvanceRequest(HrAdvanceRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Bound(dto.Instalments.Count, "instalments");

        List<HrInstalmentRequest> instalments = [];

        for (int i = 0; i < dto.Instalments.Count; i++)
        {
            string at = FormattableString.Invariant($"instalments[{i}]");

            instalments.Add(new HrInstalmentRequest(
                Period(dto.Instalments[i].PeriodCode, at + ".periodCode"),
                WireNumbers.ParseStrict(dto.Instalments[i].Amount.Raw, WireNumbers.MoneyScale, at + ".amount")));
        }

        return new HrAdvanceRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.EmployeeId, "employeeId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, "amount"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength),
            instalments);
    }

    /// <summary>يحوّل طلب إنشاء مستند استحقاق مخصص.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrProvisionRequest ToProvisionRequest(HrProvisionRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Bound(dto.Shares.Count, "shares");

        List<HrProvisionShareRequest> shares = [];

        for (int i = 0; i < dto.Shares.Count; i++)
        {
            string at = FormattableString.Invariant($"shares[{i}]");

            shares.Add(new HrProvisionShareRequest(
                WireMapping.ReadGuid(dto.Shares[i].EmploymentId, at + ".employmentId"),
                WireNumbers.ParseStrict(dto.Shares[i].PeriodShare.Raw, WireNumbers.MoneyScale, at + ".periodShare")));
        }

        return new HrProvisionRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            Period(dto.PeriodCode, "periodCode"),
            WireMapping.ReadDate(dto.AccruedOn, "accruedOn"),
            WireMapping.ReadRequiredText(dto.MeasurementRef, "measurementRef", ReferenceLength),
            WireMapping.ReadRequiredText(dto.ApprovedBy, "approvedBy", CodeLength),
            shares);
    }

    /// <summary>يحوّل طلب إنشاء مخالصة.</summary>
    /// <param name="dto">الطلب.</param>
    public static HrSettlementRequest ToSettlementRequest(HrSettlementRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HrSettlementRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.EmploymentId, "employmentId"),
            WireMapping.ReadDate(dto.SettledOn, "settledOn"),
            WireNumbers.ParseStrict(dto.SettlementDue.Raw, WireNumbers.MoneyScale, "settlementDue"),
            WireMapping.ReadRequiredText(dto.MeasurementRef, "measurementRef", ReferenceLength),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", 32),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength));
    }

    // ── الأجوبة ──────────────────────────────────────────────────────────────

    /// <summary>يحوّل موظفاً إلى شكله على السلك.</summary>
    /// <param name="employee">الموظف.</param>
    public static HrEmployeeDto ToDto(HrEmployee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new HrEmployeeDto(
            Id(employee.Id),
            employee.Code,
            employee.Name.Arabic,
            Translations(employee.Name),
            employee.ClassCode,
            employee.CostCenterId,
            Id(employee.EmploymentId),
            Date(employee.StartedOn),
            employee.EndedOn is { } ended ? Date(ended) : null,
            employee.State,
            new HrMaskedIdentityDto(employee.Identity.NationalIdMask, employee.Identity.IbanMask));
    }

    /// <summary>يحوّل مكوّن أجر.</summary>
    /// <param name="component">المكوّن.</param>
    public static HrPayComponentDto ToDto(HrPayComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        return new HrPayComponentDto(
            Id(component.Id),
            component.Code,
            component.Name.Arabic,
            Translations(component.Name),
            component.Kind,
            component.EntersContributoryWage,
            component.EntersEndOfServiceBase);
    }

    /// <summary>يحوّل قيمة مكوّن.</summary>
    /// <param name="element">العنصر.</param>
    public static HrPayElementDto ToDto(HrPayElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return new HrPayElementDto(
            Id(element.Id), element.ComponentCode, Date(element.EffectiveFrom), Money(element.Amount));
    }

    /// <summary>يحوّل إصدار نِسَب.</summary>
    /// <param name="settings">الإصدار.</param>
    public static HrPayrollSettingsDto ToDto(HrPayrollSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new HrPayrollSettingsDto(
            Id(settings.Id),
            settings.ClassCode,
            Date(settings.EffectiveFrom),
            Rate(settings.EmployerRate),
            Rate(settings.EmployeeRate),
            Money(settings.MinimumContributoryWage),
            Money(settings.MaximumContributoryWage),
            settings.ApprovedBy,
            Date(settings.ApprovedOn),
            settings.SourceRef);
    }

    /// <summary>يحوّل مسيّراً.</summary>
    /// <param name="run">المسيّر.</param>
    public static HrPayrollRunDto ToDto(HrPayrollRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new HrPayrollRunDto(
            Id(run.Id),
            run.Number,
            run.PeriodCode,
            Date(run.PeriodStart),
            Date(run.PeriodEnd),
            run.State,
            Amounts(run.Amounts),
            run.PayslipCount);
    }

    /// <summary>يحوّل قسيمة.</summary>
    /// <param name="payslip">القسيمة.</param>
    public static HrPayslipDto ToDto(HrPayslip payslip)
    {
        ArgumentNullException.ThrowIfNull(payslip);

        return new HrPayslipDto(
            Id(payslip.Id),
            Id(payslip.RunId),
            Id(payslip.EmployeeId),
            Id(payslip.EmploymentId),
            payslip.EmployeeCode,
            payslip.CostCenterId,
            Money(payslip.ContributoryWage),
            Amounts(payslip.Amounts),
            [
                .. payslip.Components.Select(static component => new HrPayslipComponentDto(
                    component.LineNo,
                    component.ComponentCode,
                    component.Kind,
                    component.EntersContributoryWage,
                    Money(component.Amount))),
            ],
            payslip.State,
            payslip.EntryId is { } entry ? Id(entry) : null,
            payslip.AlreadyPosted);
    }

    /// <summary>يحوّل سند صرف رواتب.</summary>
    /// <param name="payment">السند.</param>
    public static HrPayrollPaymentDto ToDto(HrPayrollPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new HrPayrollPaymentDto(
            Id(payment.Id),
            payment.Number,
            Id(payment.RunId),
            Date(payment.PaidOn),
            payment.SettlementMethod,
            payment.TreasuryPartyId,
            Money(payment.NetPayable),
            payment.State,
            [
                .. payment.Lines.Select(static line => new HrPayrollPaymentLineDto(
                    line.LineNo,
                    Id(line.PayslipId),
                    line.EmployeeCode,
                    Money(line.Amount),
                    line.EntryId is { } entry ? Id(entry) : null)),
            ],
            payment.AlreadyPosted);
    }

    /// <summary>يحوّل سند سداد تأمينات.</summary>
    /// <param name="payment">السند.</param>
    public static HrSocialInsurancePaymentDto ToDto(HrSocialInsurancePayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new HrSocialInsurancePaymentDto(
            Id(payment.Id),
            payment.Number,
            payment.PeriodCode,
            Date(payment.PaidOn),
            Money(payment.Amount),
            Money(payment.AccruedForPeriod),
            payment.SettlementMethod,
            payment.TreasuryPartyId,
            payment.State,
            payment.EntryId is { } entry ? Id(entry) : null,
            payment.AlreadyPosted);
    }

    /// <summary>يحوّل جزاءً.</summary>
    /// <param name="deduction">الجزاء.</param>
    public static HrDeductionDto ToDto(HrDeduction deduction)
    {
        ArgumentNullException.ThrowIfNull(deduction);

        return new HrDeductionDto(
            Id(deduction.Id),
            Id(deduction.EmployeeId),
            deduction.EmployeeCode,
            deduction.PeriodCode,
            deduction.CategoryKey,
            Money(deduction.Amount),
            deduction.ApprovedBy,
            Date(deduction.ApprovedOn),
            deduction.ConsumedByPayslipId is { } consumed ? Id(consumed) : null);
    }

    /// <summary>يحوّل سلفة.</summary>
    /// <param name="advance">السلفة.</param>
    public static HrAdvanceDto ToDto(HrAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);

        return new HrAdvanceDto(
            Id(advance.Id),
            advance.Number,
            Id(advance.EmployeeId),
            advance.EmployeeCode,
            Date(advance.IssuedOn),
            Money(advance.Amount),
            advance.SettlementMethod,
            advance.TreasuryPartyId,
            Money(advance.OutstandingAmount),
            advance.State,
            [
                .. advance.Instalments.Select(static line => new HrInstalmentDto(
                    line.LineNo,
                    line.PeriodCode,
                    Money(line.Amount),
                    line.ConsumedByPayslipId is { } consumed ? Id(consumed) : null)),
            ]);
    }

    /// <summary>يحوّل مستند استحقاق مخصص.</summary>
    /// <param name="provision">المستند.</param>
    public static HrProvisionDto ToDto(HrProvision provision)
    {
        ArgumentNullException.ThrowIfNull(provision);

        return new HrProvisionDto(
            Id(provision.Id),
            provision.Number,
            provision.PeriodCode,
            Date(provision.AccruedOn),
            provision.MeasurementRef,
            provision.ApprovedBy,
            Money(provision.PeriodShare),
            provision.State,
            [
                .. provision.Movements.Select(static movement => new HrProvisionMovementDto(
                    Id(movement.Id),
                    Id(movement.EmploymentId),
                    movement.EmployeeCode,
                    Money(movement.PeriodShare),
                    movement.EntryId is { } entry ? Id(entry) : null)),
            ],
            provision.AlreadyPosted);
    }

    /// <summary>يحوّل مخالصة.</summary>
    /// <param name="settlement">المخالصة.</param>
    public static HrSettlementDto ToDto(HrSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        return new HrSettlementDto(
            Id(settlement.Id),
            settlement.Number,
            Id(settlement.EmploymentId),
            settlement.EmployeeCode,
            Date(settlement.SettledOn),
            Money(settlement.SettlementDue),
            Money(settlement.ProvisionBalance),
            Money(settlement.AmountPaid),
            Money(settlement.Shortfall),
            Money(settlement.Excess),
            Money(settlement.ProvisionUtilised),
            settlement.ScenarioCode,
            settlement.MeasurementRef,
            settlement.SettlementMethod,
            settlement.TreasuryPartyId,
            settlement.State,
            settlement.EntryId is { } entry ? Id(entry) : null,
            settlement.AlreadyPosted);
    }

    /// <summary>يحوّل تقرير المطابقة.</summary>
    /// <param name="report">التقرير.</param>
    public static HrReconciliationDto ToDto(HrReconciliation report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new HrReconciliationDto(
            Date(report.AsOf),
            report.MatchedDocuments,
            report.IsReconciled,
            [
                .. report.Divergences.Select(static divergence => new HrReconciliationDivergenceDto(
                    divergence.DocumentType,
                    divergence.DocumentId,
                    divergence.PartyId,
                    Money(divergence.SubledgerEffect),
                    Money(divergence.ControlEffect),
                    Money(divergence.Divergence),
                    divergence.ReasonCode)),
            ]);
    }

    /// <summary>يحوّل قائمة مكوّنات أجر إلى غلافها.</summary>
    /// <param name="components">المكوّنات.</param>
    public static HrPayComponentListDto ToDto(IReadOnlyList<HrPayComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        return new HrPayComponentListDto(components.Count, [.. components.Select(ToDto)]);
    }

    /// <summary>يحوّل قائمة قيم مكوّنات إلى غلافها.</summary>
    /// <param name="elements">العناصر.</param>
    public static HrPayElementListDto ToDto(IReadOnlyList<HrPayElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return new HrPayElementListDto(elements.Count, [.. elements.Select(ToDto)]);
    }

    /// <summary>يحوّل قائمة إصدارات نِسَب إلى غلافها.</summary>
    /// <param name="settings">الإصدارات.</param>
    public static HrPayrollSettingsListDto ToDto(IReadOnlyList<HrPayrollSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HrPayrollSettingsListDto(settings.Count, [.. settings.Select(ToDto)]);
    }

    /// <summary>يحوّل قائمة قسائم إلى غلافها.</summary>
    /// <param name="payslips">القسائم.</param>
    public static HrPayslipListDto ToDto(IReadOnlyList<HrPayslip> payslips)
    {
        ArgumentNullException.ThrowIfNull(payslips);
        return new HrPayslipListDto(payslips.Count, [.. payslips.Select(ToDto)]);
    }

    // ── أدوات ────────────────────────────────────────────────────────────────

    /// <summary>
    /// اسمٌ سجلُّه عربي وترجماته صفوف. <b>ولا يُقبل وسمان متكرّران</b>: «أي القيمتين»
    /// سؤالٌ بلا جواب، والتجاهل الصامت يجعل المُرسِل يظنّ أنه سجّل ما لم يصل.
    /// </summary>
    private static TranslatedName Name(string arabic, IReadOnlyList<NameValueDto>? translations, string field)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        if (translations is not null)
        {
            Bound(translations.Count, field);

            foreach (NameValueDto entry in translations)
            {
                if (!map.TryAdd(entry.Name, entry.Value))
                {
                    throw WireNumbers.Reject(
                        "wire.translation.duplicate_tag",
                        field,
                        "وسم لغة مكرَّر: «" + entry.Name + "». وأيّ القيمتين تُحفظ سؤالٌ بلا جواب.",
                        "Duplicate language tag: '" + entry.Name + "'. Which of the two values is stored has no answer.");
                }
            }
        }

        try
        {
            return new TranslatedName(arabic, map);
        }
        catch (ArgumentException reason)
        {
            throw WireNumbers.Reject("wire.translation.malformed", field, reason.Message, reason.Message);
        }
    }

    /// <summary>ترجمات الاسم مرتَّبةً ترتيباً حرفياً ثابتاً.</summary>
    private static IReadOnlyList<NameValueDto> Translations(TranslatedName name)
        => [.. name.Translations.Select(static entry => new NameValueDto(entry.Key, entry.Value))];

    /// <summary>
    /// رمز فترة <c>yyyy-MM</c> ميلادي. والشكل يُفحص هنا لا في الوحدة: رمزٌ مُشوَّه
    /// يُنتج مسيّراً لفترة لا يعرفها الدفتر، ورفضُه شكليٌّ بـ400 لا محاسبيٌّ بـ422.
    /// </summary>
    private static string Period(string? value, string field)
    {
        string text = WireMapping.ReadRequiredText(value, field, 7);

        foreach (char c in text)
        {
            if (WireNumbers.IsNonLatinDigit(c))
            {
                throw WireNumbers.Reject(
                    "wire.period.non_latin_digits",
                    field,
                    "الأرقام غير اللاتينية مرفوضة في رمز الفترة رفضاً صريحاً.",
                    "Non-Latin digits are explicitly refused in a period code.");
            }
        }

        if (text.Length != 7
            || text[4] != '-'
            || !int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out _)
            || !int.TryParse(text.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int month)
            || month is < 1 or > 12)
        {
            throw WireNumbers.Reject(
                "wire.period.malformed",
                field,
                "رمز الفترة ميلادي بصيغة yyyy-MM حصراً. صيغة أخرى أو تقويم آخر يُقرأ فترة مالية مختلفة.",
                "A period code is Gregorian yyyy-MM only. Any other spelling or calendar reads as a different fiscal period.");
        }

        return text;
    }

    private static void Bound(int count, string field)
    {
        if (count > MaxLines)
        {
            throw WireNumbers.Reject(
                "wire.collection.too_many",
                field,
                FormattableString.Invariant($"عدد العناصر {count} يتجاوز الحدّ المعلن {MaxLines}."),
                FormattableString.Invariant($"The element count {count} exceeds the declared limit of {MaxLines}."));
        }
    }

    private static HrPayrollAmountsDto Amounts(HrPayrollAmounts amounts) => new(
        Money(amounts.GrossEntitlements),
        Money(amounts.EmployerSocialInsurance),
        Money(amounts.EmployeeSocialInsurance),
        Money(amounts.AdvanceInstalment),
        Money(amounts.Deductions),
        Money(amounts.NetPayable));

    private static string Money(decimal value) => WireNumbers.FormatMoney(value);

    /// <summary>النسبة نصّاً بمقياس ثمانٍ — <b>لا بمقياس المال</b>.</summary>
    private static string Rate(decimal value) => value.ToString("0.00000000", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);
}
