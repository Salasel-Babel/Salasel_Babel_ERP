using System.Globalization;
using Babel.RealEstate.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Wire;

/// <summary>
/// طلب تسجيل عقار على السلك.
/// <para>
/// <b>ولا حقل مستأجر فيه ولا حقل شركة:</b> النطاق من الاعتماد ومن المسار. وأي حقل غير
/// معروف يُرفض الطلب كلّه بسببه.
/// </para>
/// </summary>
internal sealed record PropertyRequestDto
{
    /// <summary>رمز العقار — وهو ما يظهر بُعداً على سطر القيد.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — <b>السجلّ</b> لا ترجمة ثانية.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم بأوسمة BCP-47. ولا حقل إنجليزي ثابت: الإنجليزية واحدة من N.</summary>
    public IReadOnlyList<NameValueDto>? NameTranslations { get; init; }

    /// <summary>نموذج الملكية: <c>own_property</c> أو <c>managed_for_others</c> — بلا افتراضي.</summary>
    public required string OwnershipModel { get; init; }

    /// <summary>المالك في نموذج الإدارة، ومعدومٌ في الملكية الذاتية.</summary>
    public string? OwnerId { get; init; }
}

/// <summary>عقارٌ كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي — السجلّ.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="OwnershipModel">نموذج الملكية المُسجَّل في الدفتر.</param>
/// <param name="OwnerId">المالك إن وُجد.</param>
/// <param name="OwnerShareNumerator">بسط حصّة المالك نصّاً.</param>
/// <param name="OwnerShareDenominator">مقام حصّة المالك نصّاً.</param>
internal sealed record PropertyDto(
    string Id,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string OwnershipModel,
    string? OwnerId,
    string OwnerShareNumerator,
    string OwnerShareDenominator);

/// <summary>طلب تسجيل وحدة داخل عقار.</summary>
internal sealed record UnitRequestDto
{
    /// <summary>رمز الوحدة.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — السجلّ.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم.</summary>
    public IReadOnlyList<NameValueDto>? NameTranslations { get; init; }

    /// <summary>الاستعمال: <c>residential</c> أو <c>commercial</c> — يُدخَل ولا يُشتقّ.</summary>
    public required string Usage { get; init; }

    /// <summary>المعاملة الضريبية: <c>standard</c> أو <c>exempt</c> — تُدخَل ولا تُشتقّ.</summary>
    public required string VatTreatment { get; init; }
}

/// <summary>وحدةٌ كما تخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="PropertyId">العقار المالك.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="Usage">الاستعمال.</param>
/// <param name="VatTreatment">المعاملة الضريبية.</param>
internal sealed record UnitDto(
    string Id,
    string PropertyId,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string Usage,
    string VatTreatment);

/// <summary>طلب تسجيل طرف عقاري — مستأجر أو مالك.</summary>
internal sealed record RealEstatePartyRequestDto
{
    /// <summary>الرمز.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — السجلّ.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم.</summary>
    public IReadOnlyList<NameValueDto>? NameTranslations { get; init; }

    /// <summary>رقم التسجيل الضريبي، ونصٌّ فارغ لمن لا رقم له — والغياب واقعة لا نقص.</summary>
    public required string VatNumber { get; init; }

    /// <summary>الإقامة الضريبية: <c>resident</c> أو <c>non_resident</c> — بلا افتراضي.</summary>
    public required string TaxResidency { get; init; }
}

/// <summary>طرفٌ عقاري كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Role">الدور.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="TaxResidency">الإقامة الضريبية.</param>
internal sealed record RealEstatePartyDto(
    string Id,
    string Role,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string VatNumber,
    string TaxResidency);

/// <summary>قسطٌ مُصرَّح به في طلب العقد.</summary>
internal sealed record InstalmentDto
{
    /// <summary>بداية الفترة المستحقّة بصيغة yyyy-MM-dd.</summary>
    public required string PeriodFrom { get; init; }

    /// <summary>نهايتها.</summary>
    public required string PeriodTo { get; init; }

    /// <summary>تاريخ الاستحقاق.</summary>
    public required string DueOn { get; init; }

    /// <summary>مبلغ القسط — <b>نصّاً</b> لا رمزاً رقمياً.</summary>
    public required WireDecimal Amount { get; init; }
}

/// <summary>طلب <b>تسجيل</b> عقد إيجار مُحرَّر في منصّة إيجار — مسوّدة قيد.</summary>
internal sealed record LeaseRegistrationRequestDto
{
    /// <summary>رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة، ولا يولّده هذا النظام.</summary>
    public required string EjarContractNumber { get; init; }

    /// <summary>الوحدة المؤجَّرة — ومنها يُشتقّ العقار.</summary>
    public required string UnitId { get; init; }

    /// <summary>المستأجر.</summary>
    public required string LesseeId { get; init; }

    /// <summary>بداية المدّة.</summary>
    public required string StartsOn { get; init; }

    /// <summary>نهايتها.</summary>
    public required string EndsOn { get; init; }

    /// <summary>قيمة العقد المتَّفق عليها نصّاً — تُصرَّح مستقلّةً كي يبقى فحص المجموع فحصاً.</summary>
    public required WireDecimal TotalRent { get; init; }

    /// <summary>الأقساط بفتراتها ومبالغها — تصل مصرَّحاً بها ولا تُوزَّع.</summary>
    public required IReadOnlyList<InstalmentDto> Instalments { get; init; }
}

/// <summary>قيدُ تسجيلِ عقدٍ كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EjarContractNumber">رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة.</param>
/// <param name="PropertyId">العقار.</param>
/// <param name="UnitId">الوحدة.</param>
/// <param name="LesseeId">المستأجر.</param>
/// <param name="StartsOn">بداية المدّة.</param>
/// <param name="EndsOn">نهايتها.</param>
/// <param name="TotalRent">قيمة العقد نصّاً.</param>
/// <param name="State">‏<c>DRAFT</c> أو <c>BILLABLE</c> — حالة القيد لا حالة العقد.</param>
internal sealed record LeaseRegistrationDto(
    string Id,
    string EjarContractNumber,
    string PropertyId,
    string UnitId,
    string LesseeId,
    string StartsOn,
    string EndsOn,
    string TotalRent,
    string State);

/// <summary>سطر جدول الدفعات <b>بمعرّفه</b> — وهو مدخل الفوترة.</summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="Seq">تسلسله.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="DueOn">تاريخ الاستحقاق.</param>
/// <param name="Amount">المبلغ نصّاً.</param>
/// <param name="IsInvoiced">هل فُوتر؟</param>
internal sealed record ScheduleLineDto(
    string Id,
    int Seq,
    string PeriodFrom,
    string PeriodTo,
    string DueOn,
    string Amount,
    bool IsInvoiced);

/// <summary>جدول الدفعات كما يخرج على السلك.</summary>
/// <param name="LeaseId">العقد.</param>
/// <param name="Lines">السطور بمعرّفاتها.</param>
internal sealed record ScheduleDto(string LeaseId, IReadOnlyList<ScheduleLineDto> Lines);

/// <summary>طلب إنشاء فاتورة إيجار <b>مسوّدة</b>.</summary>
internal sealed record RentInvoiceRequestDto
{
    /// <summary>رقم الفاتورة.</summary>
    public required string Number { get; init; }

    /// <summary>العقد.</summary>
    public required string LeaseId { get; init; }

    /// <summary>تاريخ الإصدار.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>أقساط جدول الدفعات المفوترة، بمعرّفاتها كما نشرها مورد الجدول.</summary>
    public required IReadOnlyList<string> ScheduleLineIds { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً — <b>نصّاً</b>، وتصل مع الطلب ولا تُكتب في شيفرة.</summary>
    public required WireDecimal TaxRate { get; init; }
}

/// <summary>فاتورة إيجار كما تخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Net">الصافي نصّاً.</param>
/// <param name="Tax">الضريبة نصّاً.</param>
/// <param name="Gross">الإجمالي نصّاً.</param>
/// <param name="EventCode">الحدث الذي اختارته الوحدة من نموذج الملكية المُسجَّل.</param>
/// <param name="VatTreatment">معاملة الوحدة الضريبية وقت الإصدار.</param>
/// <param name="ExemptionReasonCode">رمز سبب الإعفاء، وفراغٌ ما دام غير معروف.</param>
/// <param name="ExemptionReasonPending">علامة ظاهرة على إعفاءٍ بلا رمز سبب.</param>
/// <param name="EntryId">معرّف القيد إن رُحّلت.</param>
/// <param name="AlreadyPosted">هل كانت الهوية مُرحَّلة قبل هذا النداء؟</param>
internal sealed record RentInvoiceDto(
    string Id,
    string Number,
    string State,
    string Net,
    string Tax,
    string Gross,
    string EventCode,
    string VatTreatment,
    string ExemptionReasonCode,
    bool ExemptionReasonPending,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>طلب تسجيل سند قبض من مستأجر.</summary>
internal sealed record TenantReceiptRequestDto
{
    /// <summary>رقم السند.</summary>
    public required string Number { get; init; }

    /// <summary>المستأجر، أو <c>null</c> فالمبلغ ورد بلا مرجع يربطه بأحد.</summary>
    public string? LesseeId { get; init; }

    /// <summary>تاريخ القبض.</summary>
    public required string ReceivedOn { get; init; }

    /// <summary>طريقة التسوية — مؤهِّل الدور الذي تقرؤه المصفوفة.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>الخزينة أو الحساب البنكي في دفتره المساعد.</summary>
    public required string TreasuryPartyId { get; init; }

    /// <summary>المبلغ المقبوض نصّاً.</summary>
    public required WireDecimal Received { get; init; }
}

/// <summary>طلب تخصيص سند ورد بلا مرجع.</summary>
internal sealed record AllocationRequestDto
{
    /// <summary>المستأجر الذي تبيّن أن المبلغ له.</summary>
    public required string LesseeId { get; init; }
}

/// <summary>سند قبض كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Received">المقبوض نصّاً.</param>
/// <param name="EventCode">الحدث المُرحَّل.</param>
/// <param name="EntryId">قيد الترحيل.</param>
/// <param name="IsAllocated">هل خُصِّص؟</param>
/// <param name="AllocationEntryId">قيد التخصيص المستقلّ إن وقع.</param>
/// <param name="AlreadyPosted">هل كانت الهوية مُرحَّلة قبل النداء؟</param>
internal sealed record TenantReceiptDto(
    string Id,
    string Number,
    string State,
    string Received,
    string EventCode,
    string? EntryId,
    bool IsAllocated,
    string? AllocationEntryId,
    bool AlreadyPosted);

/// <summary>شرائح أعمار المتأخرات نصّاً.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع.</param>
internal sealed record ArrearsBandsDto(
    string NotDue,
    string Days1To30,
    string Days31To60,
    string Days61To90,
    string Over90,
    string Total);

/// <summary>متأخرات مستأجر واحد.</summary>
/// <param name="PartyId">المستأجر.</param>
/// <param name="Code">رمزه.</param>
/// <param name="NameAr">اسمه العربي.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="Bands">شرائحه.</param>
internal sealed record ArrearsPartyDto(
    string PartyId,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    ArrearsBandsDto Bands);

/// <summary>تقرير أعمار المتأخرات ومطابقته بنقطة ضبطه.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">المستأجرون.</param>
/// <param name="Totals">المجاميع.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في الدفتر نصّاً.</param>
/// <param name="Divergence">الفارق نصّاً.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟</param>
internal sealed record ArrearsDto(
    string AsOf,
    IReadOnlyList<ArrearsPartyDto> Parties,
    ArrearsBandsDto Totals,
    string ControlTotal,
    string Divergence,
    bool IsReconciled);

/// <summary>
/// نقل موارد العقارات بين السلك والسطح المنشور للوحدة.
/// <para>
/// <b>نقلٌ لا حساب</b> (القاعدة 13 البند أ): لا مجموع ولا فرق ولا تقريب هنا — المجاميع
/// تصل محسوبةً من الوحدة، والمبالغ تُنسَّق بتمثيلها القانوني وحده. وكل تاريخ ورقم يمرّ
/// بماسح <see cref="WireMapping"/> نفسه لا بنسخة ثانية منه (فخ-40).
/// </para>
/// </summary>
internal static class RealEstateMapping
{
    /// <summary>أقصى عدد أقساط في عقد واحد — حدٌّ معلن لا مفاجأة عند أول حمولة كبيرة.</summary>
    public const int MaxInstalments = 600;

    /// <summary>أقصى عدد أقساط تُفوتَر في فاتورة واحدة.</summary>
    public const int MaxInvoicedInstalments = 120;

    private const int CodeLength = 64;
    private const int NameLength = 256;
    private const int ClassificationLength = 32;

    /// <summary>يقرأ طلب تسجيل عقار.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstatePropertyRequest ToPropertyRequest(PropertyRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new RealEstatePropertyRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            Name(dto.NameAr, dto.NameTranslations),
            WireMapping.ReadRequiredText(dto.OwnershipModel, "ownershipModel", ClassificationLength),
            OptionalId(dto.OwnerId, "ownerId"));
    }

    /// <summary>ينقل عقاراً إلى شكله على السلك.</summary>
    /// <param name="property">العقار.</param>
    public static PropertyDto ToDto(RealEstateProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new PropertyDto(
            Id(property.Id),
            property.Code,
            property.Name.Arabic,
            Translations(property.Name),
            property.OwnershipModel,
            property.OwnerId is null ? null : Id(property.OwnerId.Value),

            // ‏**الحصّة كسرٌ لا عدد عشري**، وطرفاه يعبران نصّاً كما يعبر كل عدد يُعرَض.
            WireNumbers.FormatInt64(property.OwnerShareNumerator),
            WireNumbers.FormatInt64(property.OwnerShareDenominator));
    }

    /// <summary>يقرأ طلب تسجيل وحدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstateUnitRequest ToUnitRequest(UnitRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new RealEstateUnitRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            Name(dto.NameAr, dto.NameTranslations),
            WireMapping.ReadRequiredText(dto.Usage, "usage", ClassificationLength),
            WireMapping.ReadRequiredText(dto.VatTreatment, "vatTreatment", ClassificationLength));
    }

    /// <summary>ينقل وحدةً إلى شكلها على السلك.</summary>
    /// <param name="unit">الوحدة.</param>
    public static UnitDto ToDto(RealEstateUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return new UnitDto(
            Id(unit.Id),
            Id(unit.PropertyId),
            unit.Code,
            unit.Name.Arabic,
            Translations(unit.Name),
            unit.Usage,
            unit.VatTreatment);
    }

    /// <summary>يقرأ طلب تسجيل طرف عقاري.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstatePartyRequest ToPartyRequest(RealEstatePartyRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new RealEstatePartyRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            Name(dto.NameAr, dto.NameTranslations),
            WireMapping.ReadText(dto.VatNumber, "vatNumber", CodeLength),
            WireMapping.ReadRequiredText(dto.TaxResidency, "taxResidency", ClassificationLength));
    }

    /// <summary>ينقل طرفاً إلى شكله على السلك.</summary>
    /// <param name="party">الطرف.</param>
    public static RealEstatePartyDto ToDto(RealEstateParty party)
    {
        ArgumentNullException.ThrowIfNull(party);

        return new RealEstatePartyDto(
            Id(party.Id),
            party.Role,
            party.Code,
            party.Name.Arabic,
            Translations(party.Name),
            party.VatNumber,
            party.TaxResidency);
    }

    /// <summary>يقرأ طلب تسجيل عقد إيجار.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstateLeaseRequest ToLeaseRegistrationRequest(LeaseRegistrationRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Instalments.Count is 0 or > MaxInstalments)
        {
            throw WireNumbers.Reject(
                "wire.body.malformed",
                "instalments",
                "عدد الأقساط خارج الحدّ المعلن. وعقدٌ بلا قسط واحد لا جدول دفعات له، "
                + "والنظام لا يولّده من قيمة العقد لأن التوزيع يستلزم سياسة تقريب لم تُحسم.",
                "The instalment count is outside the declared limit. A lease with no instalment has no payment schedule, "
                + "and the system does not generate one from the contract value because the split requires an unsettled "
                + "rounding policy.");
        }

        return new RealEstateLeaseRequest(
            WireMapping.ReadRequiredText(dto.EjarContractNumber, "ejarContractNumber", CodeLength),
            RequiredId(dto.UnitId, "unitId"),
            RequiredId(dto.LesseeId, "lesseeId"),
            WireMapping.ReadDate(dto.StartsOn, "startsOn"),
            WireMapping.ReadDate(dto.EndsOn, "endsOn"),
            WireNumbers.ParseStrict(dto.TotalRent.Raw, WireNumbers.MoneyScale, "totalRent"),
            [
                .. dto.Instalments.Select((instalment, index) => new RealEstateInstalmentRequest(
                    WireMapping.ReadDate(instalment.PeriodFrom, Field("instalments", index, "periodFrom")),
                    WireMapping.ReadDate(instalment.PeriodTo, Field("instalments", index, "periodTo")),
                    WireMapping.ReadDate(instalment.DueOn, Field("instalments", index, "dueOn")),
                    WireNumbers.ParseStrict(
                        instalment.Amount.Raw, WireNumbers.MoneyScale, Field("instalments", index, "amount")))),
            ]);
    }

    /// <summary>ينقل قيد تسجيلٍ إلى شكله على السلك.</summary>
    /// <param name="lease">قيد التسجيل.</param>
    public static LeaseRegistrationDto ToDto(RealEstateLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        return new LeaseRegistrationDto(
            Id(lease.Id),
            lease.EjarContractNumber,
            Id(lease.PropertyId),
            Id(lease.UnitId),
            Id(lease.LesseeId),
            Date(lease.StartsOn),
            Date(lease.EndsOn),
            WireNumbers.FormatMoney(lease.TotalRent),
            lease.State);
    }

    /// <summary>ينقل جدول الدفعات إلى شكله على السلك.</summary>
    /// <param name="leaseId">العقد.</param>
    /// <param name="lines">السطور.</param>
    public static ScheduleDto ToDto(Guid leaseId, IReadOnlyList<RealEstateScheduleLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return new ScheduleDto(
            Id(leaseId),
            [
                .. lines.Select(static line => new ScheduleLineDto(
                    Id(line.Id),
                    line.Seq,
                    Date(line.PeriodFrom),
                    Date(line.PeriodTo),
                    Date(line.DueOn),
                    WireNumbers.FormatMoney(line.Amount),
                    line.IsInvoiced)),
            ]);
    }

    /// <summary>يقرأ طلب فاتورة إيجار.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstateRentInvoiceRequest ToRentInvoiceRequest(RentInvoiceRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.ScheduleLineIds.Count is 0 or > MaxInvoicedInstalments)
        {
            throw WireNumbers.Reject(
                "wire.body.malformed",
                "scheduleLineIds",
                "عدد الأقساط المفوترة خارج الحدّ المعلن، وفاتورةٌ بلا قسط قيدٌ بلا مبلغ.",
                "The invoiced instalment count is outside the declared limit, and an invoice with no instalment is an "
                + "entry with no amount.");
        }

        return new RealEstateRentInvoiceRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            RequiredId(dto.LeaseId, "leaseId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            [.. dto.ScheduleLineIds.Select((value, index) => RequiredId(value, Field("scheduleLineIds", index)))],
            WireNumbers.ParseStrict(dto.TaxRate.Raw, WireNumbers.MoneyScale, "taxRate"));
    }

    /// <summary>ينقل فاتورة إيجار إلى شكلها على السلك.</summary>
    /// <param name="invoice">الفاتورة.</param>
    public static RentInvoiceDto ToDto(RealEstateRentInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return new RentInvoiceDto(
            Id(invoice.Id),
            invoice.Number,
            invoice.State,
            WireNumbers.FormatMoney(invoice.Net),
            WireNumbers.FormatMoney(invoice.Tax),
            WireNumbers.FormatMoney(invoice.Gross),
            invoice.EventCode,
            invoice.VatTreatment,
            invoice.ExemptionReasonCode,
            invoice.ExemptionReasonPending,
            invoice.EntryId is null ? null : Id(invoice.EntryId.Value),
            invoice.AlreadyPosted);
    }

    /// <summary>يقرأ طلب سند قبض.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstateReceiptRequest ToReceiptRequest(TenantReceiptRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new RealEstateReceiptRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            OptionalId(dto.LesseeId, "lesseeId"),
            WireMapping.ReadDate(dto.ReceivedOn, "receivedOn"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", ClassificationLength),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength),
            WireNumbers.ParseStrict(dto.Received.Raw, WireNumbers.MoneyScale, "received"));
    }

    /// <summary>يقرأ طلب تخصيص.</summary>
    /// <param name="dto">الحمولة.</param>
    public static RealEstateAllocationRequest ToAllocationRequest(AllocationRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new RealEstateAllocationRequest(RequiredId(dto.LesseeId, "lesseeId"));
    }

    /// <summary>ينقل سند قبض إلى شكله على السلك.</summary>
    /// <param name="receipt">السند.</param>
    public static TenantReceiptDto ToDto(RealEstateReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new TenantReceiptDto(
            Id(receipt.Id),
            receipt.Number,
            receipt.State,
            WireNumbers.FormatMoney(receipt.Received),
            receipt.EventCode,
            receipt.EntryId is null ? null : Id(receipt.EntryId.Value),
            receipt.IsAllocated,
            receipt.AllocationEntryId is null ? null : Id(receipt.AllocationEntryId.Value),
            receipt.AlreadyPosted);
    }

    /// <summary>ينقل تقرير المتأخرات إلى شكله على السلك.</summary>
    /// <param name="report">التقرير.</param>
    public static ArrearsDto ToDto(RealEstateArrears report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ArrearsDto(
            Date(report.AsOf),
            [
                .. report.Parties.Select(static party => new ArrearsPartyDto(
                    Id(party.PartyId),
                    party.Code,
                    party.Name.Arabic,
                    Translations(party.Name),
                    Bands(party.Bands))),
            ],
            Bands(report.Totals),
            WireNumbers.FormatMoney(report.ControlTotal),
            WireNumbers.FormatMoney(report.Divergence),
            report.IsReconciled);
    }

    private static ArrearsBandsDto Bands(RealEstateArrearsBands bands) => new(
        WireNumbers.FormatMoney(bands.NotDue),
        WireNumbers.FormatMoney(bands.Days1To30),
        WireNumbers.FormatMoney(bands.Days31To60),
        WireNumbers.FormatMoney(bands.Days61To90),
        WireNumbers.FormatMoney(bands.Over90),
        WireNumbers.FormatMoney(bands.Total));

    private static IReadOnlyList<NameValueDto> Translations(TranslatedName name)
        => [.. name.Translations.Select(static entry => new NameValueDto(entry.Key, entry.Value))];

    /// <summary>
    /// يبني الاسم من سجلّه العربي وترجماته صفوفاً. <b>ولا حقل إنجليزي ثابت</b>:
    /// الإنجليزية واحدة من N (ADR-0021).
    /// </summary>
    private static TranslatedName Name(string arabic, IReadOnlyList<NameValueDto>? translations)
    {
        string record = WireMapping.ReadRequiredText(arabic, "nameAr", NameLength);
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        foreach (NameValueDto entry in translations ?? [])
        {
            if (map.ContainsKey(entry.Name))
            {
                throw WireNumbers.Reject(
                    "wire.body.repeated",
                    "nameTranslations",
                    "ترجمة مكرَّرة للوسم «" + entry.Name + "».",
                    "A repeated translation for the tag '" + entry.Name + "'.");
            }

            map[entry.Name] = WireMapping.ReadRequiredText(entry.Value, "nameTranslations", NameLength);
        }

        try
        {
            return new TranslatedName(record, map);
        }
        catch (ArgumentException exception)
        {
            throw WireNumbers.Reject("wire.body.malformed", "nameTranslations", exception.Message, exception.Message);
        }
    }

    private static string Field(string collection, int index)
        => string.Create(CultureInfo.InvariantCulture, $"{collection}[{index}]");

    private static string Field(string collection, int index, string member)
        => string.Create(CultureInfo.InvariantCulture, $"{collection}[{index}].{member}");

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Guid RequiredId(string? value, string field)
    {
        string text = WireMapping.ReadRequiredText(value, field, 36);
        return Guid.TryParseExact(text, "D", out Guid id)
            ? id
            : throw WireNumbers.Reject(
                "wire.identifier.malformed",
                field,
                "المعرّف يجب أن يكون بصيغة 8-4-4-4-12 بأحرف صغيرة.",
                "The identifier must be in 8-4-4-4-12 form in lower case.");
    }

    private static Guid? OptionalId(string? value, string field)
        => value is null ? null : RequiredId(value, field);
}
