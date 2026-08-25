using System.Globalization;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Documents;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// <b>مدخلات ثابتة بالكامل</b> — لا <c>Guid.NewGuid</c>، ولا <c>DateTimeOffset.UtcNow</c>،
/// ولا رقم عشوائي واحد. كل قيمة هنا مكتوبة حرفياً، لأن المتجه الذهبي بلا مدخل ثابت
/// ليس متجهاً ذهبياً بل مولّد ضجيج.
/// <para/>
/// <b>ولا مفتاح خاص ولا شهادة هنا ولا في المستودع كله.</b> كل ما يعتمد على مفتاح
/// يُولَّد وقت التشغيل ويموت مع العملية، ولذلك <b>لا يدخل المتجهات الذهبية</b> —
/// وهذا حدّ مقصود للمتجهات، مذكور في اختبارها.
/// </summary>
internal static class ZatcaFixtures
{
    public static readonly TenantId Tenant = new("acme");

    public static readonly IssuingUnitId Unit = new("POS-01");

    /// <summary>لحظة إصدار ثابتة بالثانية. الكسور مقصوصة عمداً: المستند يكتب الثواني وحدها.</summary>
    public static readonly DateTimeOffset IssuedAt = new(2026, 8, 25, 10, 30, 0, TimeSpan.Zero);

    public static readonly Guid StandardUuid = Guid.Parse("0192f3a0-1111-7000-8000-000000000001");

    public static readonly Guid SimplifiedUuid = Guid.Parse("0192f3a0-2222-7000-8000-000000000002");

    public static readonly ComplianceDocumentId StandardId =
        new(Guid.Parse("0192f3a0-1111-7000-8000-00000000000a"));

    public static readonly ComplianceDocumentId SimplifiedId =
        new(Guid.Parse("0192f3a0-2222-7000-8000-00000000000b"));

    public static readonly JournalEntryRef Entry =
        new(Guid.Parse("0192f3a0-3333-7000-8000-00000000000c"));

    public static ZatcaSellerIdentity Seller { get; } = new(
        CommercialRegistrationNumber: "1010101010",
        CountryCode: "SA");

    public static PartyRef SellerParty { get; } = new(
        NameAr: "شركة سلاسل بابل للمقاولات",
        NameEn: "Salasel Babel Contracting Company",
        TaxRegistrationNumber: "300000000000003",
        AddressAr: "طريق الملك فهد",
        AddressEn: "King Fahd Road",
        AddressParts: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StreetName"] = "طريق الملك فهد",
            ["BuildingNumber"] = "1234",
            ["PlotIdentification"] = "5678",
            ["CitySubdivisionName"] = "حي العليا",
            ["CityName"] = "الرياض",
            ["PostalZone"] = "12211",
            ["CountrySubentity"] = "منطقة الرياض",
            ["CountryCode"] = "SA"
        });

    public static PartyRef BuyerParty { get; } = new(
        NameAr: "مؤسسة العميل التجارية",
        NameEn: "Client Trading Establishment",
        TaxRegistrationNumber: "310000000000003",
        AddressAr: "شارع التحلية",
        AddressEn: "Tahlia Street",
        AddressParts: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StreetName"] = "شارع التحلية",
            ["BuildingNumber"] = "4321",
            ["CityName"] = "جدة",
            ["PostalZone"] = "23442",
            ["CountryCode"] = "SA"
        });

    /// <summary>
    /// فاتورة قياسية (‏B2B): للمشتري رقم تسجيل ضريبي، فمسارها <b>المقاصة</b>.
    /// </summary>
    public static ComplianceDocument Standard(long counter = 1) => new(
        StandardId,
        StandardUuid,
        Tenant,
        Unit,
        ComplianceDocumentKind.Invoice,
        ComplianceFlow.Clearance,
        "INV-2026-000001",
        IssuedAt,
        "SAR",
        SellerParty,
        BuyerParty,
        Lines(),
        Totals(),
        Entry);

    /// <summary>
    /// فاتورة مبسّطة (‏B2C): لا مشتري ولا رقم ضريبي له، فمسارها <b>الإبلاغ</b>.
    /// </summary>
    public static ComplianceDocument Simplified() => new(
        SimplifiedId,
        SimplifiedUuid,
        Tenant,
        Unit,
        ComplianceDocumentKind.Invoice,
        ComplianceFlow.Reporting,
        "SIM-2026-000001",
        IssuedAt,
        "SAR",
        SellerParty,
        null,
        Lines(),
        Totals(),
        Entry);

    /// <summary>سطران بنسبتين مختلفتين، كي تُختبر المجموعات الفرعية للضريبة لا سطر واحد.</summary>
    private static IReadOnlyList<DocumentLine> Lines() =>
    [
        new DocumentLine(1, "خدمات استشارية هندسية", "Engineering consultancy",
            Quantity: 2m, UnitPrice: 500.00m, NetAmount: 1000.00m,
            TaxRate: 15m, TaxAmount: 150.00m, GrossAmount: 1150.00m),

        new DocumentLine(2, "توريد مواد معفاة", "Zero-rated supply",
            Quantity: 1m, UnitPrice: 200.00m, NetAmount: 200.00m,
            TaxRate: 0m, TaxAmount: 0.00m, GrossAmount: 200.00m)
    ];

    private static DocumentTotals Totals() => new(1200.00m, 150.00m, 1350.00m);

    /// <summary>خانة سلسلة ثابتة. البصمة السابقة قيمة معلومة، لا ناتج تشغيل.</summary>
    public static ChainSlot Slot(long counter, byte fill = 0xAB) =>
        new(counter, Enumerable.Repeat(fill, 32).ToArray());

    public static string Describe(ChainSlot slot) =>
        string.Create(CultureInfo.InvariantCulture, $"ICV={slot.Counter}");
}
