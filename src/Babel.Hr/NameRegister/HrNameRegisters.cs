using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;

namespace Babel.Hr.NameRegister;

/// <summary>
/// <b>سجلّات الأسماء التي تملكها هذه الوحدة — وصفٌ للجدول كما ينشئه EF فعلاً.</b>
/// <para>
/// والوصف يُعلَن هنا لا في وحدة الذكاء: تلك لا تعرف اسمَ جدولٍ واحد ولا تستطيع
/// (القاعدة 3)، وهذه لا تعرف من ينطق باسم سجلّها. والمنفذ في <c>Babel.Contracts</c>
/// يجعل الاتجاهين مقطوعين.
/// </para>
/// </summary>
public sealed class HrNameRegisters : INameRegisterCatalogue
{
    /// <summary>الجداول الموصوفة بترتيب مفاتيحها.</summary>
    public static IReadOnlyList<NameRegisterTable> Tables { get; } =
    [
        new NameRegisterTable(
            registerKey: "employee",
            schema: "hr",
            table: "employee",
            idColumn: "Id",
            nameColumn: "NameAr",
            tenantColumn: "TenantId",
            activeColumn: "IsActive",
            subtitleColumn: "Code"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> RegisterKeys { get; } =
        [.. Tables.Select(static table => table.RegisterKey).Order(StringComparer.Ordinal)];
}
