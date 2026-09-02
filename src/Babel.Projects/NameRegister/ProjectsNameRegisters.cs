using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;

namespace Babel.Projects.NameRegister;

/// <summary>
/// <b>سجلّات الأسماء التي تملكها وحدة المشاريع — خمسةٌ، وثلاثةٌ منها لا تُسمّى بعمود «اسم».</b>
/// <para>
/// <b>وما يُطوى ويُطابَق ليس دائماً حقلاً اسمُه <c>NameAr</c>:</b> عقدُ الباطن يُعرف
/// <b>برقمه</b>، وبندُ جدول الكمّيات <b>بوصفه العربي</b>. والوصفُ يقول ذلك صراحةً بدل أن
/// يفترض عموداً باسمٍ واحد في كل جدول — وافتراضٌ كهذا يسقط على ثلاثةٍ من خمسة.
/// </para>
/// </summary>
public sealed class ProjectsNameRegisters : INameRegisterCatalogue
{
    /// <summary>الجداول الموصوفة.</summary>
    public static IReadOnlyList<NameRegisterTable> Tables { get; } =
    [
        new NameRegisterTable(
            registerKey: "project",
            schema: "projects",
            table: "project",
            idColumn: "Id",
            nameColumn: "NameAr",
            tenantColumn: "TenantId",
            activeColumn: "IsActive",
            subtitleColumn: "Code"),

        new NameRegisterTable(
            registerKey: "contract",
            schema: "projects",
            table: "project_contract",
            idColumn: "Id",
            nameColumn: "Number",
            tenantColumn: "TenantId",
            activeColumn: "IsActive"),

        new NameRegisterTable(
            registerKey: "subcontractor",
            schema: "projects",
            table: "subcontractor",
            idColumn: "Id",
            nameColumn: "NameAr",
            tenantColumn: "TenantId",
            activeColumn: "IsActive",
            subtitleColumn: "Code"),

        new NameRegisterTable(
            registerKey: "subcontract",
            schema: "projects",
            table: "subcontract",
            idColumn: "Id",
            nameColumn: "Number",
            tenantColumn: "TenantId",
            activeColumn: "IsActive"),

        new NameRegisterTable(
            registerKey: "boqItem",
            schema: "projects",
            table: "boq_item",
            idColumn: "Id",
            nameColumn: "DescriptionAr",
            tenantColumn: "TenantId",
            subtitleColumn: "Code"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> RegisterKeys { get; } =
        [.. Tables.Select(static table => table.RegisterKey).Order(StringComparer.Ordinal)];
}
