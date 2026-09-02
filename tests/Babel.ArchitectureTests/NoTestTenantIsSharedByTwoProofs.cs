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

    /// <summary>
    /// إشارة إلى منشأة اختبارٍ مُعلَنة في بيئة الاختبار.
    /// <para>
    /// <b>و<c>[A-Za-z]*</c> بنجمةٍ لا بزائد — والفرق حرفٌ واحد كلّف الحارس نصفَ عمله.</b>
    /// كان النمط <c>[A-Za-z]+Tenant</c>، وهو يشترط حرفاً واحداً على الأقلّ قبل
    /// <c>Tenant</c>. فالعضو المسمّى <c>Tenant</c> وحده — وهو <b>منشأة كل بيئةٍ
    /// الافتراضية، وأوّل ما يكتبه من يضيف إثباتاً</b> — كان لا يُطابَق إطلاقاً، فلا
    /// يُحصى ولا يُقارَن ولا يُشتكى منه.
    /// </para>
    /// <para>
    /// <b>مقيس عند <c>0bdf821</c> بمسحٍ مستقلّ على <c>tests/</c>:</b> خمسُ منشآت
    /// افتراضية يتقاسمها إثباتان فأكثر وكلّها خارج نظر الحارس —
    /// <c>PurchasingTestEnvironment.Tenant</c> بـ28 إثباتاً، و<c>SalesTestEnvironment.Tenant</c>
    /// بـ13، و<c>ProjectsTestEnvironment.Tenant</c> بـ10، و<c>InventoryTestEnvironment.Tenant</c>
    /// بـ6، و<c>HrTestEnvironment.Tenant</c> بـ3. وهذه الأخيرة هي بعينها منشأة
    /// <b>مسيّر الرواتب</b> التي كُتب <c>فخ-132</c> عنها: المسيّر مسحٌ يشمل كل علاقة عمل
    /// سارية، وإثباتٌ فيها يؤكّد <c>PayslipCount == 3</c>. فالحارس كان يحرس كل منشأةٍ
    /// إلّا الواحدة التي وُصف الخطر بها.
    /// </para>
    /// <para>
    /// <b>ولم يكن أحدٌ ليعلم:</b> المسح الكامل غير المُتخطّى عند <c>0bdf821</c> أخضر —
    /// ‏1087 وحدة وحدها · 1331 اختباراً · صفر سقوط. فالتقاسم لا يُسقط شيئاً اليوم،
    /// والحارس وحده هو ما يمنع الغد. وهذا نفسه درسُ فخ-93: <b>الوصف لا يُفشل شيئاً</b> —
    /// وهنا لم يكن العطل في وصفٍ بل في حرفٍ داخل الحارس نفسه.
    /// </para>
    /// </summary>
    private static readonly Regex TenantUse =
        new(@"(?<env>[A-Za-z]+TestEnvironment)\.(?<tenant>[A-Za-z]*Tenant)\b", RegexOptions.CultureInvariant);

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

        ("HrTestEnvironment.Tenant",
            "**تقاسمٌ ظهر بتوسيع النمط، وهو أخطر ما في القائمة — ويُقرأ إنذاراً لا إذناً.** "
            + "ثلاثة إثباتات: اثنان يصوغان سند تأمينات ولا يسجّلان موظفاً، وثالثٌ يسجّل ثلاثة ويؤكّد "
            + "`PayslipCount == 3`. والمسيّر مسحٌ يشمل **كل** علاقة عمل سارية في المنشأة — وهو الخطر "
            + "الذي كُتب فخ-132 عنه حرفاً بحرف. فالمقيس أنها خضراء اليوم عند 0bdf821 وحدةً وحدة، "
            + "**والسبب الكافي غير قائم**: أوّل إثباتٍ يسجّل موظفاً هنا يقلب التأكيد. من يفصلها يحذف هذا السطر."),

        ("InventoryTestEnvironment.NegativeStockTenant",
            "مقيسٌ أنها خضراء اليوم — والسببُ الكافي لم يُتحقَّق منه بعد: لم يُقَس هل يجري فيها مستندٌ "
            + "يمسح المنشأة. تصريحٌ مؤقّت يُغلق بقراءة الإثباتَين، لا حكمٌ بسلامتهما."),

        ("InventoryTestEnvironment.Tenant",
            "**تقاسمٌ ظهر بتوسيع النمط.** ستّة إثباتات على المطابقة والعكس، وكلٌّ منها يُنشئ صنفه ومستنده "
            + "بلاحقةٍ من مولّد السيناريو ويقرأ **فرق مطابقته** لا رصيد المنشأة. مقيسة خضراء وحدةً وحدة "
            + "في المسح الكامل غير المُتخطّى عند 0bdf821 — والسببُ الكافي لم يُتحقَّق منه إثباتاً إثباتاً."),

        ("InventoryTestEnvironment.UnitsTenant",
            "خمسة إثباتات على تحويل الوحدات، وكلٌّ منها يقرأ رصيد **صنفه** لا رصيد المنشأة. "
            + "والمقيس أنها خضراء في مسح العزل وحدةً وحدة."),

        ("InventoryTestEnvironment.ItemLifecycleTenant",
            "ثلاثة إثباتات على دورة حياة الصنف، **ولكلٍّ منها صنفُه برمزٍ من مولّد اللاحقة**، "
            + "والقراءة على ذلك الصنف وحده: تعديلُه، وحالتُه، ورصيدُه في موضعه. ولا مستند فيها يمسح "
            + "المنشأة، ولا إثبات يقرأ عدّاداً على مستواها. والمقيس أنها خضراء في المسح الكامل."),

        ("InventoryTestEnvironment.PlacementTenant",
            "خمسة إثباتات على التسكين، **ولكلٍّ منها هرمُه**: مستودعٌ وموقعان بلاحقةٍ من المولّد، "
            + "وصنفٌ خاصٌّ به. والقراءات على تلك المواضع بأعيانها. **وواحدٌ منها يقرأ عدّاد قيود المنشأة "
            + "كلّها** — `count(*)` على `ledger.journal_entry` — لكنه يقرؤه **قبل النقل وبعده ويقارن "
            + "الفارق**، لا القيمة المطلقة؛ فجارٌ يكتب قيداً بين القراءتين كان سيُفشله، وذلك لا يقع لأن "
            + "الإثباتات الأربعة الأخرى **لا تُرحّل قيداً واحداً**. والمقيس أنها خضراء في المسح الكامل."),

        ("InventoryTestEnvironment.UnitRegisterTenant",
            "ثلاثة إثباتات على سجلّ الوحدات، **ورموز الوحدات فيها كلّها من مولّد اللاحقة** — وهو الشرط "
            + "الكافي هنا بعينه: رمز الوحدة فريدٌ في المنشأة، فإثباتان يسجّلان «KG» حرفياً كان ثانيهما "
            + "يسقط برفض التكرار **بحسب ترتيب التشغيل**. واللاحقة تُزيل ذلك بالبناء. ولا إثبات فيها يقرأ "
            + "قائمة وحدات المنشأة ولا يَعُدّها."),

        ("InventoryTestEnvironment.ValuationTenant",
            "إثباتات التقييم تقرأ تكلفة **صنفها**، ولا مستند فيها يمسح المنشأة. مقيسة خضراء في مسح العزل."),

        ("ProjectsTestEnvironment.Tenant",
            "**تقاسمٌ ظهر بتوسيع النمط.** عشرة إثباتات على المقاولات، ولكلٍّ عقدُه ومستخلَصُه بلاحقةٍ خاصّة، "
            + "والقراءة على مستند الإثبات نفسه. مقيسة خضراء وحدةً وحدة عند 0bdf821 — والسببُ الكافي "
            + "لم يُتحقَّق منه بعد: لم يُقَس هل يجري فيها مستندٌ يمسح المنشأة كلّها."),

        ("PurchasingTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل: كل إثبات يرحّل **مستنده** ويقرأ إيصاله بهويته السداسية، "
            + "فلا يلتقط مستند غيره."),

        ("PurchasingTestEnvironment.InjectedTenant",
            "منشأة الانحراف المحقون: الإثباتات تقرأ حساباً بعينه بعد حقنٍ مقصود."),

        ("PurchasingTestEnvironment.Tenant",
            "**أوسعُ تقاسمٍ في المستودع: ثمانيةٌ وعشرون إثباتاً — وقد كان غير مرئي بالكامل.** "
            + "منشأة المشتريات الافتراضية: التقاط الفواتير والذمم الدائنة والمطابقة الثلاثية، وكلٌّ "
            + "يُنشئ مورده ومستنده بلاحقةٍ خاصّة. مقيسة خضراء وحدةً وحدة عند 0bdf821 — **والعدد نفسه "
            + "هو السبب في أن يُقرأ هذا السطر عبئاً يُفكَّك لا رخصةً تُستعمل**."),

        ("RealEstateTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل العقارية: كلٌّ يرحّل مستنده ويقرأ إيصاله."),

        ("SalesTestEnvironment.AdvanceEnabledTenant",
            "منشأة الدفعة المقدّمة: إثباتان على عميلٍ يُنشئه كلٌّ منهما لنفسه."),

        ("SalesTestEnvironment.GatewayTenant",
            "منشأة بوّابة الترحيل: كلٌّ يرحّل مستنده ويقرأ إيصاله بهويته."),

        ("SalesTestEnvironment.Tenant",
            "**تقاسمٌ ظهر بتوسيع النمط.** ثلاثة عشر إثباتاً على الذمم المدينة، ولكلٍّ عميلُه وفاتورتُه "
            + "بلاحقةٍ من مولّد السيناريو. مقيسة خضراء وحدةً وحدة عند 0bdf821 — والسببُ الكافي "
            + "لم يُتحقَّق منه إثباتاً إثباتاً."),
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
