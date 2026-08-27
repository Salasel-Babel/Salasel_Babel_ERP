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
    /// <b>جدول القرار مرّةً واحدة في كل تجميعة — والمسح داخل الحدّ، لا خارجه.</b>
    /// <para>
    /// الفحص السابق يستثني <c>/Entitlement/</c>، وهو الصواب: هناك <b>يجب</b> أن يُقرَّر.
    /// وثمنُ ذلك أنه <b>لا يرى شيئاً ممّا يقع داخل الحدّ</b> — والنسخة الثانية تقع هناك
    /// بالضبط. وقد كانت: <c>Babel.Core</c> كتب القاعدة <b>مرّتين</b>، في
    /// <c>EntitlementEnforcer.Allows</c> وفي <c>EntitlementSet.Allows</c>، ولكلٍّ نسخته
    /// من «‏<c>ReadOnly</c> تعني القراءة وحدها»؛ فتعديل إحداهما وسهو الأخرى يمنح مستأجراً
    /// منقطع الاشتراك <b>كتابةً</b> من الطريق الذي لم يُحدَّث — وهو بعينه العطل الذي وُجد
    /// ADR-0034 لمنعه، مُعاداً مجلداً واحداً إلى الداخل.
    /// (‏<c>docs/evidence/traps.md#fakh-the-decision-table-is-duplicated-inside-its-own-seam</c>)
    /// </para>
    /// <para>
    /// <b>و«يقرّر» معرَّفة بنيوياً لا بالاسم:</b> عضوٌ يُعيد <c>bool</c> مجرّداً ويقرن
    /// <c>EntitlementState.ReadOnly</c> بقيمة من نوع نيّة الوصول. فحارسٌ يبحث عن اسم
    /// <c>Allows</c> يُلتَفّ عليه بإعادة تسمية؛ وهذا يقرأ <b>الشكل</b>.
    /// </para>
    /// </summary>
    [Fact]
    public void جدول_القرار_موضعٌ_واحد_في_كل_تجميعة()
    {
        IReadOnlyList<EntitlementDecisionScan.EntitlementSeam> seams = EntitlementDecisionScan.Seams;

        AssertTheScannerIsNotVacuous(seams);

        List<string> violations = [];

        foreach (EntitlementDecisionScan.EntitlementSeam seam in seams)
        {
            if (seam.Decisions.Count != 1)
            {
                violations.Add(FormattableString.Invariant(
                    $"{seam.Project}: {seam.Decisions.Count} موضع قرار والمطلوب واحد — ")
                    + string.Join(" · ", seam.Decisions.Select(static m => m.Display).Order(StringComparer.Ordinal)));
            }
        }

        Assert.True(
            violations.Count == 0,
            "جدول القرار مكتوب أكثر من مرّة داخل حدّ الاستحقاق. النسخة الثانية هي الطريق "
            + "الذي يُمنح منه مستأجرٌ منقطع الاشتراك كتابةً حين يُحدَّث أحدهما ويُنسى الآخر:\n"
            + string.Join('\n', violations));
    }

    /// <summary>
    /// <b>ولا موضع ثانٍ يقرن حالةً بنيّة وصول</b> — ولو لم يُعِد <c>bool</c>.
    /// <para>
    /// الفحص السابق يقرأ «من يقرّر»؛ وهذا يقرأ «من يعرف الشكل أصلاً». والفرق مهمّ:
    /// نسخةٌ ثانية تُكتب <c>void</c> أو <c>Task</c> أو ترمي استثناءً بدل أن تُعيد
    /// <c>bool</c> تمرّ من الأول وتُمسَك هنا.
    /// </para>
    /// <para>
    /// والجرد أدناه <b>ليس حدّاً أعلى بل قائمة واعية</b>: كل عضو فيه يقرن الحالة بالنيّة
    /// و<b>لا يقرّر</b> — <c>Refusal</c> تُسمّي الرفض بعد وقوعه (‏«انقطع الاشتراك» شيء
    /// و«لم تُشترَ قط» شيء آخر). فمن يضيف عضواً جديداً يقرن الاثنين مضطرٌّ إلى أن يقرّر:
    /// إن كان يقرّر فليُحذف ويُنادَ <c>EntitlementRules.Allows</c>، وإن كان يُسمّي فليُضَف
    /// هنا بحجّته. (نفس شكل الجرد في القاعدة 5 وللسبب نفسه.)
    /// </para>
    /// </summary>
    [Fact]
    public void لا_موضع_ثانٍ_يقرن_حالة_استحقاق_بنيّة_وصول()
    {
        (string Project, string[] Members)[] declared =
        [
            ("Babel.ControlPlane", ["Allows", "Refusal"]),
            (ModuleMap.Core, ["Allows"]),
        ];

        IReadOnlyList<EntitlementDecisionScan.EntitlementSeam> seams = EntitlementDecisionScan.Seams;

        AssertTheScannerIsNotVacuous(seams);

        Assert.Equal(
            declared.Select(static d => d.Project).Order(StringComparer.Ordinal),
            seams.Select(static seam => seam.Project).Order(StringComparer.Ordinal));

        List<string> violations = [];

        foreach ((string project, string[] members) in declared)
        {
            EntitlementDecisionScan.EntitlementSeam seam = seams.Single(s => string.Equals(s.Project, project, StringComparison.Ordinal));
            HashSet<string> declaredMembers = new(members, StringComparer.Ordinal);

            violations.AddRange(seam.Pairings
                .Where(m => !declaredMembers.Contains(m.Name))
                .Select(static m => $"{m.Display} — يقرن حالةً بنيّة وصول وليس في الجرد")
                .Order(StringComparer.Ordinal));

            violations.AddRange(members
                .Where(m => !seam.Pairings.Any(p => string.Equals(p.Name, m, StringComparison.Ordinal)))
                .Select(m => $"{project}::{m} — في الجرد ولم يعد موجوداً: يُحذف من الجرد لا يُترك")
                .Order(StringComparer.Ordinal));
        }

        Assert.True(
            violations.Count == 0,
            "الجرد الصريح لا يطابق ما على القرص. عضوٌ جديد يقرن حالةً بنيّة وصول إمّا **يقرّر** — "
            + "فليُحذف ويُنادِ EntitlementRules.Allows — وإمّا **يُسمّي الرفض** بعد وقوعه، "
            + "فليُضَف إلى الجرد أعلاه بحجّته:\n"
            + string.Join('\n', violations));
    }

    /// <summary>
    /// <b>وجدولا التجميعتين قاعدةٌ واحدة، لا قاعدتان متشابهتان.</b>
    /// <para>
    /// <c>Babel.ControlPlane</c> بلا مرجعية إلى أي مشروع بابل وبلا مرجعية إليه
    /// (‏<see cref="ModuleMap"/>: مجموعة مراجعه المسموحة <b>فارغة</b>)، فلا نوع مشترك
    /// يستطيع أن يحمل الجدول للاثنين، ولا انعكاسٌ من هنا يبلغه. والرابط الوحيد الممكن
    /// بلا كسر ذلك الحدّ هو <b>المصدر على القرص</b>: يُقرأ الجدولان ويُوحَّدان — بلا
    /// فراغات، وباسمَي نوعَي نيّة الوصول وأسماء الوسائط موحَّدة — ثم يُقارَنان.
    /// </para>
    /// <para>
    /// وهذا يُغلق <b>شطر القاعدة</b> من الدَّين المُعلَن في ADR-0034 («النموذجان ما زالا
    /// اثنين»). ويبقى <b>شطر الكتالوج</b> مفتوحاً: وحدةٌ تُضاف إلى أحد جردَي الوحدات ولا
    /// تُضاف إلى الآخر لا يمسكها شيء — وهو دَينٌ آخر، مذكور بحدوده في ADR-جديد.
    /// </para>
    /// </summary>
    [Fact]
    public void جدولا_التجميعتين_قاعدةٌ_واحدة()
    {
        IReadOnlyList<EntitlementDecisionScan.EntitlementSeam> seams = EntitlementDecisionScan.Seams;

        AssertTheScannerIsNotVacuous(seams);

        (string Project, string Normalised)[] tables = [.. seams
            // أوّل قرار لا وحيده: عددُ المواضع شأنُ الفحص الأول، وهذا يقارن القاعدة.
            .Select(static seam => (seam.Project, EntitlementDecisionScan.Normalise(seam.Decisions[0])))
            .OrderBy(static t => t.Project, StringComparer.Ordinal)];

        // غير خاوٍ من طرف الشكل: نصٌّ مُوحَّد فقد ذراع «للقراءة فقط» أو ذكر نيّة الوصول
        // لم يعد جدول قرار، ومقارنة فارغتين تمرّ دائماً.
        foreach ((string project, string normalised) in tables)
        {
            Assert.Contains("EntitlementState.ReadOnly", normalised, StringComparison.Ordinal);
            Assert.Contains("ACCESS.Read", normalised, StringComparison.Ordinal);
            Assert.True(normalised.Length > 80, $"{project}: النصّ المُوحَّد {normalised.Length} محرفاً فقط.");
        }

        Assert.True(
            tables.Select(static t => t.Normalised).Distinct(StringComparer.Ordinal).Count() == 1,
            "جدولا القرار في التجميعتين ليسا القاعدة نفسها — والتجميعتان لا تتراجعان، "
            + "فلا شيء غير هذا الفحص يربطهما:\n"
            + string.Join('\n', tables.Select(static t => $"{t.Project}: {t.Normalised}")));
    }

    /// <summary>
    /// <b>الماسح غير خاوٍ من طرفه هو.</b> حارسٌ مجموعتُه فارغة يمرّ أخضر إلى الأبد،
    /// وقد وقع ذلك في هذا المستودع (‏<c>traps.md#fakh-a-guard-whose-corpus-is-the-disk-not-the-repository</c>).
    /// فيُثبَت هنا أن الحدّين وُجدا، وأن الملفات قُرئت، وأن الأعضاء فُصلت فعلاً، وأن
    /// كل حدٍّ يحمل قراراً واحداً على الأقل — فتوقّفُ المُحلِّل يُقرأ أحمر لا أخضر.
    /// </summary>
    private static void AssertTheScannerIsNotVacuous(IReadOnlyList<EntitlementDecisionScan.EntitlementSeam> seams)
    {
        Assert.Equal(2, seams.Count);

        foreach (EntitlementDecisionScan.EntitlementSeam seam in seams)
        {
            Assert.True(
                seam.BlockScopedNamespaceFiles.Count == 0,
                $"{seam.Project}: فضاء اسم بقوسين يُزيح عمق الأعضاء فيمرّ المسح فارغاً:\n"
                + string.Join('\n', seam.BlockScopedNamespaceFiles));

            Assert.True(seam.Files.Count >= 5, FormattableString.Invariant($"{seam.Project}: {seam.Files.Count} ملفاً فقط في الحدّ — النطاق ضامر."));
            Assert.True(seam.Members.Count >= 20, FormattableString.Invariant($"{seam.Project}: {seam.Members.Count} عضواً فقط — الماسح لم يعد يفصل الأعضاء."));
            Assert.True(seam.Decisions.Count >= 1, $"{seam.Project}: لا موضع قرار إطلاقاً — الماسح توقّف عن المطابقة.");
        }
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
