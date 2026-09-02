using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;

namespace Babel.RealEstate.NameRegister;

/// <summary>
/// <b>سجلّ المستأجر — <u>ومعه شرطُ الدور، وهو ليس زينة</u>.</b>
/// <para>
/// ‏<c>realestate.party</c> يحمل المستأجرين والملّاك والوسطاء في جدولٍ واحد بقيدٍ على
/// <c>"PartyRole"</c> (‏<c>'lessee' · 'owner' · 'broker'</c>). فسجلُّ «المستأجر» بلا هذا
/// الشرط يُطابق <b>مالكاً</b> باسمٍ متشابه ويُصدر له مِقبضاً صحيحاً — <b>وهو الطرف الخطأ
/// بعينه</b>، عائداً من باب النطاق بدل باب قصِّ الاسم. ولذلك أُضيف الشرط إلى الوصف
/// نفسه، لا إلى مِصفاةٍ بعده يسهل نسيانها.
/// </para>
/// <para>
/// وعمودُ الشركة مُسمّى هنا ولا يُفترض: جداول العقارات تحمل المنشأة <b>والشركة</b> معاً،
/// بخلاف جداول المبيعات والمشتريات والموارد البشرية.
/// </para>
/// </summary>
public sealed class RealEstateNameRegisters : INameRegisterCatalogue
{
    /// <summary>دورُ المستأجر كما يقيّده المخطّط.</summary>
    private const string Lessee = "lessee";

    /// <summary>الجداول الموصوفة.</summary>
    public static IReadOnlyList<NameRegisterTable> Tables { get; } =
    [
        new NameRegisterTable(
            registerKey: "lessee",
            schema: "realestate",
            table: "party",
            idColumn: "Id",
            nameColumn: "NameAr",
            tenantColumn: "TenantId",
            companyColumn: "CompanyId",
            subtitleColumn: "Code",
            roleColumn: "PartyRole",
            roleValue: Lessee),
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> RegisterKeys { get; } =
        [.. Tables.Select(static table => table.RegisterKey).Order(StringComparer.Ordinal)];
}
