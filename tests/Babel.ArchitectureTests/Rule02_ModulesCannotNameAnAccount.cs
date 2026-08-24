using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Posting;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 2 — الوحدة لا تستطيع تسمية حساب.</b>
/// <para>
/// الوحدة تصف حدثاً تجارياً؛ مصفوفة الترحيل تقرّر الحساب (03-accounting-core.md §4).
/// الإنفاذ بنيوي: <c>AccountCode</c> نوع <c>internal</c> داخل الدفتر، و<c>Babel.Contracts</c>
/// لا يكشف شيئاً يشبه رقم حساب. لا يحتاج المطوّر إلى انضباط — الاسم غير مرئي له.
/// </para>
/// <para>
/// لماذا تستحق قاعدة مستقلة: وحدة تسمّي حساباً تعني أن تعديل دليل الحسابات لدى العميل
/// يصبح تعديلاً في كود المبيعات ونشر إصدار، بدل تعديل صف في جدول.
/// </para>
/// </summary>
public sealed class Rule02_ModulesCannotNameAnAccount
{
    private static readonly IReadOnlySet<string> AccountWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "account", "accounts", "gl", "coa" };

    [Fact]
    public void TheAccountCodeTypeExistsAndIsInternalToTheLedger()
    {
        Assembly ledger = BabelAssemblies.Named(ModuleMap.Ledger);

        Type accountCode = Assert.Single(
            BabelAssemblies.TypesOf(ledger),
            static type => type.Name == "AccountCode");

        Assert.False(
            TypeShapes.IsVisibleOutsideAssembly(accountCode),
            "AccountCode مكشوف خارج الدفتر: القاعدة 2 تنهار عند هذه النقطة بالضبط.");
    }

    [Fact]
    public void NoAssemblyOutsideTheLedgerDeclaresAnAccountIdentifierType()
    {
        List<string> violations = [.. BabelAssemblies.Product
            .Where(static assembly => assembly.GetName().Name != ModuleMap.Ledger)
            .SelectMany(BabelAssemblies.TypesOf)
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => Identifiers.ContainsWord(type.Name, AccountWords))
            .Select(static type => type.FullName!)];

        Assert.True(
            violations.Count == 0,
            "أنواع تسمّي حساباً خارج الدفتر:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ThePostingContractExposesNoAccountShapedMember()
    {
        Assembly contracts = BabelAssemblies.Named(ModuleMap.Contracts);
        List<string> violations = [];

        foreach (Type type in BabelAssemblies.TypesOf(contracts).Where(static t => !TypeShapes.IsCompilerGenerated(t)))
        {
            foreach (MemberInfo member in TypeShapes.DeclaredMembers(type))
            {
                if (Identifiers.ContainsWord(member.Name, AccountWords))
                {
                    violations.Add($"{type.FullName}.{member.Name}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "عقد الترحيل يكشف عضواً يسمّي حساباً — لحظة كسر القاعدة 2:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void APostingLineCarriesARoleAndNotAnAccount()
    {
        // حارس مباشر على النوع الذي يُغري بإضافة حقل حساب.
        Assert.Contains(typeof(PostingLine).GetProperties(), property => property.Name == nameof(PostingLine.Role));
        Assert.DoesNotContain(
            typeof(PostingLine).GetProperties(),
            property => Identifiers.ContainsWord(property.Name, AccountWords));
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        Assert.NotEmpty(BabelAssemblies.TypesOf(BabelAssemblies.Named(ModuleMap.Contracts)));
        Assert.True(Identifiers.ContainsWord("AccountCode", AccountWords));
        Assert.True(Identifiers.ContainsWord("ReceivableAccountId", AccountWords));
        Assert.False(Identifiers.ContainsWord("Accountability", AccountWords));
    }
}
