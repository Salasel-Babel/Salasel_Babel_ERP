using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لا منشأةَ اختبارٍ يستعملها إثباتان.</b>
/// <para>
/// <b>ولماذا حارسٌ لا اتّفاق:</b> بعضُ المستندات ليست عن سطرٍ بل عن <b>مجموعةٍ يحدّدها
/// استعلام</b> — مسيّر الرواتب يشمل كل علاقة عمل سارية في المنشأة، والإقفال كل حركةٍ في
/// الفترة. فموظفٌ يسجّله إثباتٌ آخر <b>يدخل مسيّر جاره</b> ويغيّر نتيجته. وهذا مُسجَّل
/// فخّاً (‏فخ-132)، والقاعدة مكتوبة نصّاً في تعليق <c>HrTestEnvironment</c>:
/// «ولكل إثباتٍ منشأتُه».
/// </para>
/// <para>
/// <b>وقد خُولفت مرّتين بعد كتابتها — مقيس.</b> الأولى بين ملفَّين (اختبار تفرّد الفترة
/// تقاسم منشأة نهاية الخدمة)، والثانية <b>داخل ملفٍّ واحد</b> (إثباتان في ملفّ الخصوصية).
/// وفي المرّتين رُدَّ المسيّر بـ<c>hr.payroll_settings_missing</c> عن تصنيفٍ ليس تصنيفه،
/// فبدا العطل في الإعدادات وهو في تقاسم المنشأة. <b>والقاعدة في تعليقٍ لا تمنع مخالفتها</b>
/// — وهو نفسه درسُ فخ-93 وفخ-115.
/// </para>
/// <para>
/// <b>والحدّ هو الإثبات لا الملفّ:</b> المخالفة الثانية وقعت بين دالّتين متجاورتين.
/// </para>
/// </summary>
public sealed class NoTestTenantIsSharedByTwoProofs
{
    /// <summary>‏<c>[Fact]</c> أو <c>[Theory]</c> ثم توقيع الدالّة — بداية إثبات.</summary>
    private static readonly Regex ProofStart =
        new(@"^\s*\[(Fact|Theory)\b", RegexOptions.CultureInvariant);

    /// <summary>إشارة إلى منشأة اختبارٍ مُعلَنة في بيئة الاختبار.</summary>
    private static readonly Regex TenantUse =
        new(@"(?<env>[A-Za-z]+TestEnvironment)\.(?<tenant>[A-Za-z]+Tenant)\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// <b>ما يُسمح بتقاسمه اليوم — ولكلٍّ سببٌ مكتوب.</b>
    /// <para>
    /// وهذا الحارس <b>سقّاطة لا مِكنسة</b>: لا يفرض تفكيك ما هو أخضر اليوم، ويمنع
    /// <b>تقاسماً جديداً</b>. فالضرر لا يقع بالتقاسم وحده بل حين يجري في المنشأة
    /// <b>مستندٌ يمسحها كلّها</b> — مسيّر رواتب، أو إقفال فترة، أو مطابقةٌ على كل حركة.
    /// </para>
    /// <para>
    /// <b>وسببٌ فارغ يُفشل</b>، لأن تصريحاً بلا سبب ليس تصريحاً. ومن يُزيل تقاسماً
    /// يحذف سطره، ومن يحتاج تقاسماً جديداً يكتب سببه ويُقرأ في المراجعة.
    /// </para>
    /// </summary>
    private static readonly (string Tenant, string Why)[] DeclaredSharing =
    [
        ("HrTestEnvironment.EmptyRatesTenant",
            "إثباتان متقابلان على **غياب** صفوف النِّسَب: أحدهما يرفض مسيّراً والآخر يقرأ الجدول فارغاً. "
            + "ومنشأةٌ لا يُودَع فيها صفٌّ أبداً لا يُفسدها جارٌ يزرع فيها، لأن ما تُثبته هو الفراغ نفسه."),

        ("InventoryTestEnvironment.NegativeStockTenant",
            "مقيسٌ أنها خضراء اليوم — والسببُ الكافي لم يُتحقَّق منه بعد: لم يُقَس هل يجري فيها مستندٌ "
            + "يمسح المنشأة. تصريحٌ مؤقّت يُغلق بقراءة الإثباتَين، لا حكمٌ بسلامتهما."),

        ("InventoryTestEnvironment.UnitsTenant",
            "خمسة إثباتات على تحويل الوحدات، وكلٌّ منها يقرأ رصيد **صنفه** لا رصيد المنشأة. "
            + "والمقيس أنها خضراء في مسح العزل وحدةً وحدة."),

        ("InventoryTestEnvironment.ValuationTenant",
            "إثباتات التقييم تقرأ تكلفة **صنفها**، ولا مستند فيها يمسح المنشأة. مقيسة خضراء في مسح العزل."),

        ("PurchasingTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل: كل إثبات يرحّل **مستنده** ويقرأ إيصاله بهويته السداسية، "
            + "فلا يلتقط مستند غيره."),

        ("PurchasingTestEnvironment.InjectedTenant",
            "منشأة الانحراف المحقون: الإثباتات تقرأ حساباً بعينه بعد حقنٍ مقصود."),

        ("RealEstateTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل العقارية: كلٌّ يرحّل مستنده ويقرأ إيصاله."),

        ("SalesTestEnvironment.AdvanceEnabledTenant",
            "منشأة الدفعة المقدّمة: إثباتان على عميلٍ يُنشئه كلٌّ منهما لنفسه."),

        ("SalesTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل: كلٌّ يرحّل مستنده ويقرأ إيصاله بهويته."),
    ];

    [Fact]
    public void EveryDeclaredSharingCarriesAWrittenReason()
    {
        List<string> silent =
        [
            .. DeclaredSharing
                .Where(static row => string.IsNullOrWhiteSpace(row.Why))
                .Select(static row => row.Tenant),
        ];

        Assert.True(
            silent.Count == 0,
            "تصريحُ تقاسمٍ بلا سبب ليس تصريحاً — اكتب سببه أو افصل المنشأتين:\n"
            + string.Join('\n', silent));
    }

    [Fact]
    public void NoTenantIsUsedByMoreThanOneProof()
    {
        Dictionary<string, List<string>> byTenant = [];
        int proofs = 0;

        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(RepositoryLayout.Root, "tests"), "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/');

            if (relative.Contains("/bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.EndsWith("TestEnvironment.cs", StringComparison.Ordinal)
                || relative.EndsWith("NoTestTenantIsSharedByTwoProofs.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            string? proof = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (ProofStart.IsMatch(lines[i]))
                {
                    // اسم الإثبات هو أول توقيعٍ بعد الوسم.
                    for (int j = i + 1; j < lines.Length && j < i + 6; j++)
                    {
                        Match signature = Regex.Match(
                            lines[j], @"public\s+(?:async\s+)?[\w<>\[\]?]+\s+(?<name>[^\s(]+)\s*\(");
                        if (signature.Success)
                        {
                            proof = relative + " · " + signature.Groups["name"].Value;
                            proofs++;
                            break;
                        }
                    }

                    continue;
                }

                if (proof is null)
                {
                    continue;
                }

                foreach (Match use in TenantUse.Matches(lines[i]))
                {
                    string key = use.Groups["env"].Value + "." + use.Groups["tenant"].Value;
                    List<string> users = byTenant.TryGetValue(key, out List<string>? seen) ? seen : byTenant[key] = [];

                    if (!users.Contains(proof, StringComparer.Ordinal))
                    {
                        users.Add(proof);
                    }
                }
            }
        }

        Assert.True(
            proofs >= 50,
            $"الإثباتات المفحوصة {proofs} — أقلّ من أن يكون الفحص ذا معنى. حارسٌ لا يفحص شيئاً يمرّ دائماً.");

        HashSet<string> declared = [.. DeclaredSharing.Select(static row => row.Tenant)];

        // وتصريحٌ لتقاسمٍ لم يعد قائماً يُحذف: قائمةٌ تبقى بعد زوال سببها تكفّ عن أن تحرس.
        List<string> stale =
        [
            .. declared
                .Where(tenant => !byTenant.TryGetValue(tenant, out List<string>? users) || users.Count <= 1)
                .OrderBy(static tenant => tenant, StringComparer.Ordinal),
        ];

        Assert.True(
            stale.Count == 0,
            "تصريحُ تقاسمٍ لم يعد له تقاسم — احذف سطره، فقائمةٌ تبقى بعد سببها تكفّ عن أن تحرس:\n"
            + string.Join('\n', stale));

        List<string> shared =
        [
            .. byTenant
                .Where(pair => pair.Value.Count > 1 && !declared.Contains(pair.Key))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{pair.Key} يستعملها {pair.Value.Count}:\n    " + string.Join("\n    ", pair.Value)),
        ];

        Assert.True(
            shared.Count == 0,
            "منشأةُ اختبارٍ يستعملها أكثر من إثبات — ومستندٌ يمسح المنشأة كلّها يلتقط ما زرعه غيره "
            + "(‏فخ-132). ولكل إثباتٍ منشأتُه:\n"
            + string.Join('\n', shared));
    }
}
