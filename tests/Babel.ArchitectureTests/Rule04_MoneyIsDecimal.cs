using System.Reflection;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 4 — لا <c>float</c> ولا <c>double</c> في أي نوع يمسّ اسمه أو فضاء أسمائه
/// المال أو المبلغ أو الرصيد أو السعر أو النسبة أو الضريبة أو الإجمالي.</b>
/// <para>
/// CONTRIBUTING §3 بند 2: <c>decimal</c> في الكود و<c>NUMERIC(19,4)</c> في قاعدة البيانات.
/// </para>
/// <para>
/// لماذا اختبار يُفشل البناء وليس مراجعة: خطأ الفاصلة العائمة <b>لا يظهر في الاختبارات</b>.
/// <c>0.1 + 0.2</c> يعطي <c>0.30000000000000004</c>، فتمرّ فاتورة واحدة، وتمرّ مئة،
/// ثم لا يطابق ميزان المراجعة بهللة واحدة بعد ستة أشهر — ولا يدلّ شيء على الموضع.
/// </para>
/// <para>
/// المطابقة بالكلمة لا بالنص الخام: <c>ExchangeRate</c> يُلتقط، و<c>Corporate</c> لا يُلتقط.
/// </para>
/// </summary>
public sealed class Rule04_MoneyIsDecimal
{
    private static readonly IReadOnlySet<string> MoneyWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "money", "amount", "amounts", "balance", "balances", "price", "prices",
        "rate", "rates", "tax", "taxes", "total", "totals", "currency", "cost", "costs",
    };

    private static readonly HashSet<Type> BinaryFloatingPoint = [typeof(float), typeof(double), typeof(Half)];

    [Fact]
    public void NoBinaryFloatingPointAnywhereNearMoney()
    {
        List<string> violations = [];
        int inspected = 0;
        int moneyTouching = 0;

        foreach (Type type in BabelAssemblies.AllTypes().Where(static t => !TypeShapes.IsCompilerGenerated(t)))
        {
            bool typeTouchesMoney =
                Identifiers.ContainsWord(type.Name, MoneyWords)
                || Identifiers.ContainsWord(type.Namespace ?? string.Empty, MoneyWords);

            if (typeTouchesMoney)
            {
                moneyTouching++;
            }

            foreach (MemberInfo member in TypeShapes.DeclaredMembers(type))
            {
                bool memberTouchesMoney = typeTouchesMoney || Identifiers.ContainsWord(member.Name, MoneyWords);

                foreach ((string description, Type valueType) in TypeShapes.ValueTypesOf(member))
                {
                    inspected++;

                    bool relevant = memberTouchesMoney || Identifiers.ContainsWord(description, MoneyWords);
                    if (!relevant)
                    {
                        continue;
                    }

                    foreach (Type candidate in TypeShapes.Unwrap(valueType))
                    {
                        if (BinaryFloatingPoint.Contains(candidate))
                        {
                            violations.Add($"{type.FullName}.{description} : {candidate.Name}");
                        }
                    }
                }
            }
        }

        Assert.True(inspected > 200, $"عدد الأعضاء المفحوصة {inspected} أقل من أن يثبت شيئاً.");
        Assert.True(moneyTouching > 0, "لم يُعثر على أي نوع يمسّ المال — القاعدة تمرّ فراغاً.");
        Assert.True(
            violations.Count == 0,
            "فاصلة عائمة ثنائية في موضع مالي — المال decimal دائماً:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NoBinaryFloatingPointAnywhereInSharedKernelOrContracts()
    {
        // النواة المشتركة والعقود لا مبرّر فيهما لفاصلة عائمة إطلاقاً، مالية أو غيرها.
        List<string> violations = [];

        foreach (Assembly assembly in new[] { BabelAssemblies.Named(ModuleMap.SharedKernel), BabelAssemblies.Named(ModuleMap.Contracts) })
        {
            foreach (Type type in BabelAssemblies.TypesOf(assembly).Where(static t => !TypeShapes.IsCompilerGenerated(t)))
            {
                foreach (MemberInfo member in TypeShapes.DeclaredMembers(type))
                {
                    foreach ((string description, Type valueType) in TypeShapes.ValueTypesOf(member))
                    {
                        violations.AddRange(TypeShapes.Unwrap(valueType)
                            .Where(BinaryFloatingPoint.Contains)
                            .Select(candidate => $"{type.FullName}.{description} : {candidate.Name}"));
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, "فاصلة عائمة في SharedKernel أو Contracts:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void MoneyItselfIsBackedByDecimal()
    {
        Assert.Equal(typeof(decimal), typeof(SharedKernel.Money).GetProperty(nameof(SharedKernel.Money.Amount))!.PropertyType);
    }

    [Fact]
    public void TheWordMatcherIsPreciseNotSubstringBased()
    {
        Assert.True(Identifiers.ContainsWord("ExchangeRate", MoneyWords));
        Assert.True(Identifiers.ContainsWord("TotalVatAmount", MoneyWords));
        Assert.True(Identifiers.ContainsWord("unit_price", MoneyWords));
        Assert.False(Identifiers.ContainsWord("Corporate", MoneyWords));
        Assert.False(Identifiers.ContainsWord("Ratelimiter", MoneyWords));
    }
}
