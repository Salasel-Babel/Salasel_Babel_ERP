using System.Globalization;
using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Core.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>كل وحدة منتج إمّا وحدةٌ حقيقية وإمّا هيكلٌ مُعلَن ومعه سببه.</b>
/// <para>
/// <b>رابعةُ القواعد 15 و16 و19، ونفس العطل نُقل من البوّابة إلى المنتج.</b> تلك الثلاث
/// تضمن أن البوّابة تبني ما تدّعي تغطيته وأن ما لا تُشغّله يبقى <b>مكتوباً باسمه</b>.
/// وهذه تضمن الشيء نفسه عن <b>المنتج</b>: ‏<c>BabelModule</c> يُعلن ثلاث عشرة وحدة،
/// ومنها اليوم <b>خمسٌ</b> ليست إلا بطاقة وحدة من سبعة عشر سطراً — والبناء أخضر،
/// وكل حارس في هذا المجلد أخضر، <b>ولا سطر واحد في المستودع يقول ذلك</b>.
/// </para>
/// <para>
/// <b>ولماذا هذا الصمت هو العطل نفسه لا وصفاً له:</b> الوحدة الهيكل تجتاز كل قاعدة هنا
/// <b>لأنها فارغة</b>، لا لأنها سليمة. القاعدة 6 تجمع خدمات التطبيق بتجميعتها، فتجميعةٌ
/// بلا خدمة تطبيق تخرج من التجميع وتمرّ صامتة (وهي الحجّة المكتوبة في ADR-0042 §4).
/// والقاعدة 5 لا ترى سياق EF لأنه غير موجود. والقاعدة 3 لا ترى مرجعاً ممنوعاً لأنه لا
/// مرجع. فالخُضرة هنا <b>لا تعني «صحيح»، بل «لا شيء يُفحَص»</b> — وهو بالضبط الفرق الذي
/// وُضعت له <c>docs/evidence/traps.md</c> كلّها.
/// </para>
/// <para>
/// <b>والقائمة أدناه ليست إعفاءً، بل إعلان</b> — نفس عقد
/// <see cref="Rule19_TheGateNamesWhatItDoesNotCover"/> حرفاً بحرف: كل وحدة إمّا تحمل
/// <b>مادّة</b> (خدمة تطبيق، أو استمرارية، أو سطحٌ منشور)، وإمّا تُكتب هنا <b>ومعها سبب</b>.
/// <b>وسببٌ فارغ يُفشل البناء</b>، لأن تصريحاً بلا سبب ليس تصريحاً.
/// </para>
/// <para>
/// <b>والاتجاه الثاني هو ما يمنع القائمة من أن تكذب بعد أن تُبنى الوحدة:</b> هيكلٌ
/// اكتسب مادّةً <b>يُحمِّر الحارس</b> حتى يُحذف من قائمته. فلا يبقى إعلانٌ يقول «هذه
/// لا تفعل شيئاً» فوق وحدة صارت تُرحّل قيوداً — وهو الشكل الذي وقع في هذا المستودع
/// مرّتين: قائمةٌ صحيحة يوم كُتبت وكاذبة بعد شهر
/// (‏<c>docs/evidence/traps.md#fakh-a-convention-guarded-in-one-register-and-not-in-its-sibling</c>).
/// </para>
/// <para>
/// <b>والعطل نفسه مُسجَّل بحقوله الستّة</b> في
/// <c>docs/evidence/traps.md#fakh-an-empty-module-passes-every-guard-because-it-is-empty</c>،
/// وفيه لماذا تمرّ القواعد 3 و5 و6 و9 على الوحدة الفارغة واحدةً واحدة.
/// </para>
/// <para>
/// <b>والقرار الذي يحمل هذه الحدود ليس هذا التعليق</b>، بل وثيقة القرار ذات المفتاح
/// <c>the-minimum-is-defined-by-what-it-excludes</c> في <c>docs/decisions/</c>: فيها
/// الحدث المحاسبي لكل هيكل، والحساب الذي يمسّه، وترتيب الأربعة <b>بمعيار الهجرة
/// المفروضة</b>. والفحص الأخير أدناه يربط الاثنين، فلا يُحذف أحدهما ويبقى الآخر.
/// </para>
/// </summary>
public sealed class EveryModuleIsRealOrDeclaredASkeleton
{
    /// <summary>
    /// أقصر سبب مقبول. <b>سببٌ من كلمة واحدة ليس سبباً</b>، والحدّ مكتوب رقماً كي يكون
    /// الرفض مُتماثلاً على كل مؤلّف — نفس منطق <see cref="Support.CultureScan.MinimumReasonLength"/>.
    /// </summary>
    private const int MinimumReasonLength = 40;

    /// <summary>
    /// مفتاح وثيقة القرار التي تحمل النطاق. <b>المفتاح لا الرقم</b>: الرقم يُخصَّص عند
    /// الإنزال (‏<c>docs/decisions/README.md</c> §0.0)، والبحث أدناه بالنمط
    /// <c>ADR-*-&lt;المفتاح&gt;.md</c> كي لا يُكسَر هذا الحارس يوم يأخذ القرارُ رقمه.
    /// </summary>
    private const string ScopeDecisionSlug = "the-minimum-is-defined-by-what-it-excludes";

    /// <summary>مجلد سجل القرارات.</summary>
    private const string DecisionsFolder = "docs/decisions";

    /// <summary>
    /// الوحدات المُعلَنة <b>هياكل</b>: مشروعٌ في ملف الحلّ، وبطاقةُ وحدة، ولا شيء بعدها.
    /// <b>ما دامت وحدةٌ هنا فالمنتج لا يفعل شيئاً باسمها</b> — ومن يحذف السبب يحذف
    /// التصريح معه.
    /// <para>
    /// وكل سبب أدناه يقول ثلاثة أشياء لا اثنين: <b>ما الذي ينقصها</b>، و<b>ما الذي
    /// يحجزها</b>، و<b>هل تُباع اليوم</b> — لأن الهيكل الذي يُباع ليس نقصاً في المنتج
    /// بل وعدٌ لعميل، وهو أخطر صنفَي هذه القائمة.
    /// </para>
    /// </summary>
    private static readonly (BabelModule Module, string Why)[] DeclaredSkeletons =
    [
        (BabelModule.Pos,
            "نقاط البيع: حدثان في data/posting-matrix/events/pos.json (pos.shift.close · pos.shift.cost_of_sales) "
            + "بلا كاتب واحد. ونموذج الاتصال — متصل أم دون إنترنت — سؤالٌ مفتوح على المالك في docs/RECORD.md §7، "
            + "وهو ما يحجزها لا حجم العمل. وتُباع اليوم برمز POS ضمن خطّتَي RETAIL وFULL."),

        (BabelModule.Hr,
            "الموارد البشرية: خمسة أحداث في data/posting-matrix/events/hr.json بلا كاتب، ونسب التأمينات "
            + "غير مُتحقَّق منها ولا تُكتب في شيفرة، وقاعدة توزيع تكلفة الموظف على المشاريع غير محسومة "
            + "(‏caveat مكتوب داخل hr.payroll.accrual). وبياناتها شخصية، وADR-0003 يمنع الحذف من الدفتر. "
            + "وتُباع اليوم برمز PAY ضمن خطة FULL."),

        (BabelModule.Projects,
            "المقاولات: خمسة أحداث في data/posting-matrix/events/projects.json بلا كاتب — والمستخلص "
            + "مستندها الأساسي بمحتجز ودفعة مقدمة تُستنفد. وبُعدا project وboq_item موجودان على سطر القيد "
            + "اليوم، فلا هجرة على مفتاح قائم تنتظرها. وتُباع اليوم برمز PRJ ضمن خطة FULL."),

        (BabelModule.Assets,
            "الأصول الثابتة: ثلاثة أحداث في data/posting-matrix/events/assets.json بلا كاتب، ولا سجلّ أصول "
            + "ولا جدول إهلاك ولا حساب مجمَّع. وتُباع اليوم برمز FA ضمن خطّتَي GROWTH وFULL — وهي الوحدة "
            + "الوحيدة في هذه القائمة التي لم تُذكر في طلب المالك أصلاً، فوجودها هنا هو الفائدة."),

        (BabelModule.Portals,
            "البوّابات: سطح عرض للعميل والمورد والمقاول من الباطن فوق وحدات لم تُبنَ بعد، ولا حدث محاسبي "
            + "واحد لها في مصفوفة الترحيل إطلاقاً — فهي الوحيدة هنا التي لا تُرحّل شيئاً بحكم طبيعتها. "
            + "ولا تُباع في أي خطة اليوم."),
    ];

    /// <summary>
    /// كل وحدة منتج إمّا تحمل مادّة وإمّا مُعلَنة هيكلاً. <b>والاتجاهان كلاهما يُفشل البناء.</b>
    /// </summary>
    [Fact]
    public void EveryProductModuleIsSubstantiveOrDeclaredASkeletonWithAReason()
    {
        List<string> problems = [];

        foreach (BabelModule module in Enum.GetValues<BabelModule>())
        {
            Assembly assembly = BabelAssemblies.Named(ModuleMap.ProjectOf(module));
            bool declared = DeclaredSkeletons.Any(entry => entry.Module == module);

            if (Substance(assembly).Count == 0 && !declared)
            {
                problems.Add(
                    $"‏{ModuleMap.ProjectOf(module)} بلا خدمة تطبيق ولا استمرارية ولا سطح منشور، وليست في DeclaredSkeletons.\n"
                    + "  → إمّا أن تُبنى، وإمّا أن تُعلَن هيكلاً **ومعها سبب**: وحدةٌ تُباع ولا تفعل شيئاً "
                    + "تمرّ على كل حارس هنا لأنها فارغة، لا لأنها سليمة.");
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>
    /// <b>هيكلٌ صار وحدة يُحمِّر هذا الفحص حتى يُحذف من قائمته.</b> وهذا هو نصف الحارس
    /// الذي يمنع القائمة من أن تكذب: إعلانٌ صحيح يوم كُتب يبقى معلَّقاً فوق وحدةٍ تُرحّل
    /// قيوداً، فيقرؤه قادمٌ جديد على أنه وصفٌ للحال.
    /// </summary>
    [Fact]
    public void NoDeclaredSkeletonHasQuietlyBecomeAModule()
    {
        List<string> graduated = [];

        foreach ((BabelModule module, _) in DeclaredSkeletons)
        {
            List<string> substance = Substance(BabelAssemblies.Named(ModuleMap.ProjectOf(module)));

            if (substance.Count > 0)
            {
                graduated.Add(
                    $"‏{ModuleMap.ProjectOf(module)} مُعلَنة هيكلاً وفيها الآن: {string.Join(" · ", substance)}.\n"
                    + "  → احذفها من DeclaredSkeletons. الإعلان وصفُ حالٍ لا إعفاءٌ دائم، "
                    + "وقائمةٌ تبقى بعد أن تُبنى الوحدة تكذب على قارئها.");
            }
        }

        Assert.True(graduated.Count == 0, string.Join('\n', graduated));
    }

    /// <summary>
    /// كل إعلان يحمل وحدةً معروفة مرّةً واحدة، <b>وسبباً غير فارغ ولا مقتضب</b>.
    /// </summary>
    [Fact]
    public void EveryDeclarationCarriesADistinctModuleAndAWrittenReason()
    {
        List<string> problems = [];

        List<BabelModule> repeated = [.. DeclaredSkeletons
            .GroupBy(static entry => entry.Module)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)];

        foreach (BabelModule module in repeated)
        {
            problems.Add($"‏{module} مُعلَنة أكثر من مرّة — إعلانان لوحدة واحدة يُحرَّر أحدهما ويُنسى الآخر.");
        }

        foreach ((BabelModule module, string why) in DeclaredSkeletons)
        {
            if (string.IsNullOrWhiteSpace(why))
            {
                problems.Add($"‏{module} مُعلَنة بلا سبب — تصريحٌ بلا سبب ليس تصريحاً.");
                continue;
            }

            if (why.Trim().Length < MinimumReasonLength)
            {
                problems.Add(
                    $"سبب «{module}» أقصر من "
                    + MinimumReasonLength.ToString(CultureInfo.InvariantCulture)
                    + " محرفاً — سببٌ من كلمة واحدة يُقرأ ولا يُفحَص.");
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>
    /// <b>الشاهد الموجب — كي لا تُلتبَس الخُضرة بالفراغ.</b> ماسحُ المادّة لو ضمر لأعاد
    /// «لا مادّة» عن كل وحدة، فمرّ الفحص الأول <b>على المستودع كلّه</b> بلا أن يقيس شيئاً.
    /// هذه الحقائق الثلاث مقيسة على الشجرة اليوم، وكلٌّ منها يقابل كاشفاً واحداً:
    /// خدمة تطبيق في النواة · استمرارية في الدفتر · سطحٌ منشور في المبيعات.
    /// </summary>
    [Fact]
    public void TheSubstanceScanBitesOnItsOwnControls()
    {
        Assert.Contains(
            "خدمة تطبيق",
            Substance(BabelAssemblies.Named(ModuleMap.Core)),
            StringComparer.Ordinal);

        Assert.Contains(
            "استمرارية",
            Substance(BabelAssemblies.Named(ModuleMap.Ledger)),
            StringComparer.Ordinal);

        Assert.Contains(
            "سطح منشور",
            Substance(BabelAssemblies.Named("Babel.Sales")),
            StringComparer.Ordinal);

        int substantive = Enum.GetValues<BabelModule>()
            .Count(module => Substance(BabelAssemblies.Named(ModuleMap.ProjectOf(module))).Count > 0);

        Assert.True(
            substantive >= 5,
            "عدد الوحدات ذات المادّة "
            + substantive.ToString(CultureInfo.InvariantCulture)
            + " — المُحلِّل ضامر والفحص الأول يمرّ فراغاً.");
    }

    /// <summary>
    /// <b>الإعلان في الشيفرة والقرار في السجلّ لا ينفصلان.</b> وثيقةُ النطاق موجودة،
    /// وكل وحدة مُعلَنة هيكلاً مذكورة فيها باسم مشروعها. فمن يحذف الوثيقة أو يُسقط منها
    /// وحدةً يحمرّ عنده الحارس، ومن يضيف هيكلاً بلا أن يكتب ثمنه في القرار كذلك.
    /// </summary>
    [Fact]
    public void TheScopeDecisionExistsAndNamesEveryDeclaredSkeleton()
    {
        string folder = Path.Combine(RepositoryLayout.Root, DecisionsFolder);

        string[] candidates = [.. Directory
            .EnumerateFiles(folder, "ADR-*-" + ScopeDecisionSlug + ".md")
            .Order(StringComparer.Ordinal)];

        Assert.True(
            candidates.Length == 1,
            "المتوقَّع وثيقةُ قرارٍ واحدة بالمفتاح «" + ScopeDecisionSlug + "» في " + DecisionsFolder
            + FormattableString.Invariant($" — ووُجد {candidates.Length}.")
            + "\n  → القائمة في هذا الحارس تصف الحال، والقرار وحده يحمل ثمن تأجيل كلٍّ منها. "
            + "أحدهما بلا الآخر إعلانٌ بلا حجّة أو حجّةٌ بلا إنفاذ.");

        string decision = File.ReadAllText(candidates[0]);
        List<string> missing = [.. DeclaredSkeletons
            .Select(static entry => ModuleMap.ProjectOf(entry.Module))
            .Where(project => !decision.Contains(project, StringComparison.Ordinal))];

        Assert.True(
            missing.Count == 0,
            "وحدات مُعلَنة هياكل ولا تُسمّيها وثيقة النطاق:\n"
            + string.Join('\n', missing.Select(static p => $"  {p}\n    → اكتب في القرار ما الذي يجعلها وحدةً، وثمن تأجيلها.")));
    }

    /// <summary>
    /// مادّة التجميعة: ما يجعلها وحدةً لا بطاقة. ثلاثة كواشف لا واحد، لأن الوحدة قد
    /// تبدأ بأيٍّ منها: خدمةٌ تُنادى، أو جداولُ تُملَك، أو سطحٌ يُنشَر.
    /// </summary>
    private static List<string> Substance(Assembly assembly)
    {
        List<Type> types = [.. BabelAssemblies.TypesOf(assembly).Where(static type => !TypeShapes.IsCompilerGenerated(type))];
        string surfaceNamespace = assembly.GetName().Name + ".Surface";

        List<string> found = [];

        if (types.Any(static type => type is { IsClass: true, IsAbstract: false } && typeof(IApplicationService).IsAssignableFrom(type)))
        {
            found.Add("خدمة تطبيق");
        }

        if (types.Any(TypeShapes.IsDbContext))
        {
            found.Add("استمرارية");
        }

        if (types.Any(type => type.Namespace?.StartsWith(surfaceNamespace, StringComparison.Ordinal) == true))
        {
            found.Add("سطح منشور");
        }

        return found;
    }
}
