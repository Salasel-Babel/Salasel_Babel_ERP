using System.Reflection;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 6 — لا شيء يتجاوز الاستحقاق.</b>
/// <para>
/// كل نقطة دخول عامة في خدمة تطبيق تحمل <see cref="RequiresEntitlementAttribute"/>.
/// الاختبار يعدّ نقاط الدخول ويُفشل البناء على أي واحدة بلا سمة.
/// </para>
/// <para>
/// لماذا عند حدّ الخدمة لا عند الواجهة: إخفاء عنصر من القائمة لا يمنع نداء HTTP.
/// وحدة انقضى اشتراكها تبقى مقروءة بالكامل — وهذا هو المطلوب — لكن مسار الكتابة
/// يجب أن يُغلق في مكان واحد يستحيل نسيانه، لا في كل شاشة.
/// </para>
/// <para>
/// ولماذا الآن لا لاحقاً: <c>ReadOnly</c> يمسّ كل مسار كتابة وكل تقرير في كل وحدة؛
/// إضافته بعد أول عميل يدفع تعني إعادة فتح كل ملف (وثيقة المعمارية §17 م-7).
/// </para>
/// <para>
/// <b>والأرضية جزءٌ من هذه القاعدة لا قاعدةٌ ثانية.</b> «لا شيء يتجاوز الاستحقاق»
/// كانت تعني «لا كتابة بلا فحص»؛ وهي تعني الآن أيضاً <b>«لا قراءة تُقطَع بلا حقّ»</b>.
/// فحصان يحرسان ذلك: أن كل وحدة تكتب في الدفتر تُعلن أرضيتها <c>ReadOnly</c>
/// (فلا سجلّ محاسبي يُنزَع)، وأن <b>جدول القرار موضعٌ واحد</b> — إذ إن الطريقة
/// الوحيدة الباقية لتجاوز الأرضية هي أن يكتب مؤلّف وحدةٍ نسخةً ثانية من الجدول في
/// خدمته، فيُسقط <c>ReadOnly</c> من القراءة وهو يظنّ أنه يُنفِذ الاستحقاق.
/// (‏<c>docs/evidence/traps.md#fakh-mandatory-module-cannot-be-read-only</c>)
/// </para>
/// </summary>
public sealed class Rule06_NothingBypassesEntitlement
{
    private static IEnumerable<Type> ApplicationServices() =>
        BabelAssemblies.AllTypes()
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => typeof(IApplicationService).IsAssignableFrom(type))
            .Where(TypeShapes.IsVisibleOutsideAssembly);

    private static IEnumerable<MethodInfo> EntryPoints(Type service) =>
        service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Where(static method => method.DeclaringType != typeof(object));

    [Fact]
    public void EveryPublicEntryPointDeclaresItsEntitlementRequirement()
    {
        List<string> violations = [];
        int entryPoints = 0;

        foreach (Type service in ApplicationServices())
        {
            bool typeAttributed = service.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is not null;

            foreach (MethodInfo method in EntryPoints(service))
            {
                entryPoints++;

                if (!typeAttributed && method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is null)
                {
                    violations.Add($"{service.FullName}.{method.Name}");
                }
            }
        }

        Assert.True(entryPoints > 0, "لم تُعثر أي نقطة دخول — القاعدة تمرّ فراغاً.");
        Assert.True(
            violations.Count == 0,
            "نقاط دخول عامة بلا [RequiresEntitlement] — أي بلا إنفاذ استحقاق:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void EveryEntryPointDeclaresTheModuleItActuallyLivesIn()
    {
        // سمة تعلن وحدة غير وحدتها تفتح ثغرة أدهى من غيابها: تبدو مؤمَّنة وليست كذلك.
        List<string> violations = [];

        foreach (Type service in ApplicationServices())
        {
            string assemblyName = service.Assembly.GetName().Name!;

            foreach (MethodInfo method in EntryPoints(service))
            {
                RequiresEntitlementAttribute? attribute =
                    method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true)
                    ?? service.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true);

                if (attribute is null)
                {
                    continue;
                }

                string declared = ModuleMap.ProjectOf(attribute.Module);
                if (declared != assemblyName)
                {
                    violations.Add($"{service.FullName}.{method.Name} يعلن {attribute.Module} وهو في {assemblyName}");
                }
            }
        }

        Assert.True(violations.Count == 0, "سمات استحقاق تعلن وحدة غير وحدتها:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheEnforcementSeamExistsAndIsUsedByEveryModuleThatHasEntryPoints()
    {
        // السمة إعلان؛ ما يجعلها فعّالة هو استدعاء المنفِّذ. القاعدة تتحقق من وجود
        // اعتماد على IEntitlementEnforcer في كل تجميعة فيها خدمة تطبيق.
        List<string> violations = [];

        foreach (IGrouping<Assembly, Type> group in ApplicationServices().GroupBy(static service => service.Assembly))
        {
            bool takesEnforcer = group.Any(static service => service
                .GetConstructors()
                .SelectMany(static constructor => constructor.GetParameters())
                .Any(static parameter => parameter.ParameterType == typeof(IEntitlementEnforcer)));

            if (!takesEnforcer)
            {
                violations.Add(group.Key.GetName().Name!);
            }
        }

        Assert.True(
            violations.Count == 0,
            "تجميعات فيها خدمات تطبيق لا تحقن IEntitlementEnforcer إطلاقاً:\n" + string.Join('\n', violations));
    }

    /// <summary>
    /// <b>كل وحدة يبلغ عملُها الدفترَ تحمل أرضية «للقراءة فقط».</b>
    /// <para>
    /// المعيار <b>مشتقّ من رسم الاعتماديات لا مكتوب مرّتين</b>: وحدةٌ اعتمادياتها
    /// المتعدّية تبلغ <see cref="BabelModule.Ledger"/> — أو هي الدفتر أو النواة
    /// التي يقوم عليها — هي وحدةٌ يصير عملُها <b>قيداً في دفتر</b>. والقيد يبقى
    /// للعميل بعد انقطاع اشتراكه: يُقرأ، ويُصدَّر، ويُقدَّم به إقرار. فوحدتها
    /// <b>تُخفَّض ولا تُنزَع</b>.
    /// </para>
    /// <para>
    /// والاستثناء الوحيد مشتقّ بالمعيار نفسه لا ممنوح بالاسم:
    /// <see cref="BabelModule.Ai"/> لا يعتمد على الدفتر — التقاطه <b>مسوّدات ما
    /// قبل القيد</b> (‏ADR-0024) لا وقائع محاسبية — فيُنزَع فعلاً. وهو نظير
    /// <c>REP</c> في كتالوج مستوى التحكّم، وللسبب نفسه.
    /// </para>
    /// <para>
    /// ولذلك يفشل البناء على وحدةٍ جديدة تبلغ الدفتر ونُسيت أرضيتها: القائمتان
    /// (‏رسم الاعتماديات، وجرد الأرضيات) تُقرآن من موضعين مستقلّين، فاختلافهما
    /// خطأ لا رأي.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryModuleWhoseWorkReachesTheLedgerDeclaresAReadOnlyFloor()
    {
        static bool ReachesTheLedger(BabelModule module) =>
            module is BabelModule.Ledger or BabelModule.Core
            || ModuleDependencyGraph.TransitiveRequirementsOf(module).Contains(BabelModule.Ledger);

        string[] withoutFloor = [.. ModuleDependencyGraph.All
            .Where(ReachesTheLedger)
            .Where(static module => ModuleDependencyGraph.FloorOf(module) != EntitlementState.ReadOnly)
            .Select(static module => module.ToString())
            .Order(StringComparer.Ordinal)];

        Assert.True(
            withoutFloor.Length == 0,
            "وحدات يبلغ عملُها الدفتر ولا تُعلن أرضية «للقراءة فقط» — أي يمكن نزع "
            + "سجلاتها عن صاحبها:\n" + string.Join('\n', withoutFloor));

        // والعكس: وحدةٌ لا تبلغ الدفتر لا تُمنَح أرضية لم تستحقّها، وإلا صار الجرد
        // «كل شيء» فبطل معناه.
        string[] floorWithoutLedger = [.. ModuleDependencyGraph.All
            .Where(static module => !ReachesTheLedger(module))
            .Where(static module => ModuleDependencyGraph.FloorOf(module) == EntitlementState.ReadOnly)
            .Select(static module => module.ToString())
            .Order(StringComparer.Ordinal)];

        Assert.True(
            floorWithoutLedger.Length == 0,
            "وحدات لا تبلغ الدفتر ومع ذلك تُعلن أرضية «للقراءة فقط»:\n"
            + string.Join('\n', floorWithoutLedger));

        // غير خاوٍ من الطرفين: لو صار كل شيء على جانب واحد لمرّ الفحص بلا معنى.
        Assert.Contains(ModuleDependencyGraph.All, ReachesTheLedger);
        Assert.Contains(ModuleDependencyGraph.All, static m => !ReachesTheLedger(m));
    }

    /// <summary>
    /// <b>وكل وحدة لها مسار كتابة وتبلغ الدفتر مشمولةٌ فعلاً.</b> الفحص السابق
    /// يقيس الرسم؛ وهذا يربطه بسطح الإنفاذ الحقيقي — نقاط الدخول المُعلِنة
    /// <see cref="EntitlementAccess.Write"/> — فلا يمرّ الرسم صحيحاً وسطحُ
    /// الخدمات على شيء آخر.
    /// </summary>
    [Fact]
    public void EveryWritingModuleOnTheLedgerIsCoveredByTheFloor()
    {
        BabelModule[] modulesThatWrite = [.. ApplicationServices()
            .SelectMany(EntryPoints)
            .Select(static method => method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true)
                ?? method.DeclaringType!.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true))
            .Where(static attribute => attribute is { Access: EntitlementAccess.Write })
            .Select(static attribute => attribute!.Module)
            .Distinct()
            .Order()];

        Assert.NotEmpty(modulesThatWrite);

        string[] uncovered = [.. modulesThatWrite
            .Where(static module => module is not BabelModule.Ai)
            .Where(static module => ModuleDependencyGraph.FloorOf(module) != EntitlementState.ReadOnly)
            .Select(static module => module.ToString())
            .Order(StringComparer.Ordinal)];

        Assert.True(
            uncovered.Length == 0,
            "وحدات لها نقطة دخول كتابة ولا أرضية لها:\n" + string.Join('\n', uncovered));
    }

    /// <summary>
    /// <b>جدول القرار موضعٌ واحد — وهذا هو ما يمنع التجاوز فعلاً.</b>
    /// <para>
    /// السمة تُعلن، والمنفِّذ يُنفِذ؛ والثغرة الباقية أن يكتب مؤلّف وحدةٍ
    /// <c>if (state == EntitlementState.Entitled)</c> في خدمته. يبدو صحيحاً، ويكون
    /// قد <b>أسقط <c>ReadOnly</c> من القراءة صامتاً</b> — أي قطع عن عميلٍ انقطع
    /// سداده سجلَّه المحاسبي وهو يظنّ أنه يُنفِذ الاستحقاق.
    /// </para>
    /// <para>
    /// فالفحص يمسح شيفرة الإنتاج كلّها — <b>بلا تعليقات</b>، لأن التعليقات تشرح
    /// الشكل الممنوع عمداً وقاعدةٌ تخلط الاثنين تُجبر المهندس على حذف الشرح
    /// (نفس علّة القاعدة 12) — ويرفض أي ذكر لقيمة من <c>EntitlementState</c> خارج
    /// حدّ الاستحقاق نفسه.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingOutsideTheEntitlementSeamBranchesOnAnEntitlementState()
    {
        string sourceRoot = Path.Combine(RepositoryLayout.Root, "src");

        List<string> violations = [];
        int scanned = 0;

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/');

            if (relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            // حدّ الاستحقاق نفسه: هو الموضع الذي **يجب** أن يقرّر فيه.
            if (relative.Contains("/Entitlement/", StringComparison.Ordinal))
            {
                continue;
            }

            scanned++;
            string code = StripComments(File.ReadAllText(file));

            if (code.Contains("EntitlementState.", StringComparison.Ordinal))
            {
                violations.Add(relative);
            }
        }

        Assert.True(scanned > 100, $"المسح ضامر: {scanned} ملفاً فقط.");
        Assert.True(
            violations.Count == 0,
            "شيفرة إنتاج خارج حدّ الاستحقاق تفرّع على EntitlementState — أي نسخة ثانية "
            + "من جدول القرار، وهي الطريقة الوحيدة الباقية لإسقاط ReadOnly من القراءة "
            + "صامتاً:\n" + string.Join('\n', violations.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// <b>ما هو قائم اليوم، مقيساً قبل أي تغيير.</b>
    /// <para>
    /// المسح أعلاه يستثني <c>/Entitlement/</c> — وهو الصواب: هناك <b>يجب</b> أن يُقرَّر.
    /// لكنّه لذلك لا يرى شيئاً ممّا يقع <b>داخل</b> الحدّ، والنسخة الثانية من جدول القرار
    /// تقع هناك بالضبط. وهذا الفحص يُثبّت الحالة القائمة قبل توحيدها كي يُقرأ الفرق
    /// في السلوك لا في الدعوى: <c>Babel.Core</c> يكتب القاعدة <b>مرّتين</b> —
    /// <c>EntitlementEnforcer.Allows</c> و<c>EntitlementSet.Allows</c>، ولكلٍّ نسخته
    /// من «‏<c>ReadOnly</c> تعني القراءة وحدها» — و<c>Babel.ControlPlane</c> يكتبها مرّة.
    /// </para>
    /// </summary>
    [Fact]
    public void جدول_القرار_مكتوبٌ_مرّتين_في_النواة_اليوم()
    {
        IReadOnlyList<EntitlementDecisionScan.EntitlementSeam> seams = EntitlementDecisionScan.Seams;

        Assert.Equal(
            ["Babel.ControlPlane", "Babel.Core"],
            seams.Select(static seam => seam.Project).Order(StringComparer.Ordinal));

        // غير خاوٍ: ماسحٌ لا يقرأ عضواً واحداً يمرّ أخضر إلى الأبد (فخ-68).
        foreach (EntitlementDecisionScan.EntitlementSeam seam in seams)
        {
            Assert.Empty(seam.BlockScopedNamespaceFiles);
            Assert.True(seam.Files.Count >= 5, $"{seam.Project}: {seam.Files.Count} ملفاً فقط في الحدّ.");
            Assert.True(seam.Members.Count >= 20, $"{seam.Project}: {seam.Members.Count} عضواً فقط — الماسح ضامر.");
        }

        Assert.Equal(
            ["src/Babel.Core/Entitlement/EntitlementEnforcer.cs::Allows", "src/Babel.Core/Entitlement/EntitlementSet.cs::Allows"],
            seams.Single(static s => s.Project == "Babel.Core").Decisions.Select(static m => m.Display).Order(StringComparer.Ordinal));

        Assert.Equal(
            ["src/Babel.ControlPlane/Entitlement/EntitlementModel.cs::Allows"],
            seams.Single(static s => s.Project == "Babel.ControlPlane").Decisions.Select(static m => m.Display).Order(StringComparer.Ordinal));
    }

    /// <summary>الشيفرة بلا تعليقات — القاعدة تفحص ما يُنفَّذ لا ما يُشرح (نفس القاعدة 12).</summary>
    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"//.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return text;
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        string[] modulesWithServices = [.. ApplicationServices()
            .Select(static service => service.Assembly.GetName().Name!)
            .Distinct()
            .Order(StringComparer.Ordinal)];

        // القائمة **جرد صريح لا حدّ أعلى**: وحدة جديدة تحمل خدمة تطبيق تُضاف هنا بقرار
        // واعٍ — وهذا هو ما يمنع ظهور خدمة تطبيق سابعة دون أن يراها أحد. (نفس شكل
        // القائمة في القاعدة 5 وللسبب نفسه.)
        Assert.Equal(
            ["Babel.Ai", "Babel.Compliance", ModuleMap.Core, "Babel.Inventory", ModuleMap.Ledger, "Babel.Purchasing", "Babel.Sales"],
            modulesWithServices);
    }
}
