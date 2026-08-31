using System.Reflection;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>التحميل من مخزن يمرّ بالمُنشئ نفسه — لا بجواره.</b>
/// <para>
/// هذا هو الموضع الذي تُزال فيه الثابتات صامتةً عادةً. النوع يقول «صالحٌ بحكم وجوده»
/// لأن لا مُنشئ عام له، ثم تأتي طبقة استمرارية فتبني الكائن من صفوف بمُهيّئ خصائص أو
/// بمصنعٍ ثانٍ متساهل — فيبقى التوثيق كما هو، ويصير الوعد كاذباً.
/// </para>
/// <para>
/// وما يُفحص هنا شيئان: أن <b>الكاشف يلتقط مخالفة حقيقية</b> (كل حالة أدناه هي صفٌّ
/// يمكن أن يوجد فعلاً في القاعدة)، وأن <b>مجموعة المنتِجين مغلقة</b> — فلا دالّة في
/// النواة تُرجع <see cref="FoundedCompany"/> أو <see cref="CostCenterRegister"/> إلا
/// وهي المصنع أو تمرّ به.
/// </para>
/// </summary>
public sealed class LoadingCannotBypassTheInvariantTests
{
    private static readonly TenantId Tenant = new(new Guid("0f0f0f0f-0000-4000-8000-000000000001"));

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · صفوفٌ مخالفة لا تُنتج سجلّاً — والحالات كلّها صفوفٌ ممكنة فعلاً
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void سجلٌّ_بلا_مركز_واحد_يُرفض()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => CostCenterRegister.Rehydrate([], new CostCenterCode("cc.001")));

        Assert.Contains("فارغ", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void افتراضيٌّ_ليس_في_السجلّ_يُرفض()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => CostCenterRegister.Rehydrate([Active("cc.001", "المركز الرئيس")], new CostCenterCode("cc.999")));

        Assert.Contains("cc.999", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void افتراضيٌّ_موقوف_يُرفض()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => CostCenterRegister.Rehydrate(
                [Suspended("cc.001", "المركز الرئيس", "أُغلق")], new CostCenterCode("cc.001")));

        Assert.Contains("موقوف", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void رمزٌ_مكرَّر_يُرفض()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => CostCenterRegister.Rehydrate(
                [Active("cc.001", "الأول"), Active("cc.001", "الثاني")], new CostCenterCode("cc.001")));

        Assert.Contains("مكرَّر", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void رمزٌ_مُشوَّه_يُرفض_ولو_كان_الباقي_سليماً()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => CostCenterRegister.Rehydrate([Active("CC 001", "المركز")], new CostCenterCode("CC 001")));

        Assert.Contains("مُشوَّه", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void مقياسٌ_مخزَّن_خارج_المدى_يُرفض()
    {
        CostCenterRegister register = CostCenterRegister.Rehydrate(
            [Active("cc.001", "المركز الرئيس")], new CostCenterCode("cc.001"));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => FoundedCompany.Rehydrate(Tenant, new TranslatedName("منشأة"), 9, register));

        Assert.Contains("مقياس عرض", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والكاشف يميّز:</b> صفوفٌ سليمة تُنتج منشأةً كاملة. حارسٌ يرفض كل شيء لا
    /// يُثبت شيئاً — كما أن حارساً يقبل كل شيء لا يحرس شيئاً.
    /// </summary>
    [Fact]
    public void وصفوفٌ_سليمة_تُنتج_منشأةً_كاملة()
    {
        CostCenterRegister register = CostCenterRegister.Rehydrate(
            [Active("cc.002", "فرع الدمام"), Active("cc.001", "المركز الرئيس")],
            new CostCenterCode("cc.001"));

        FoundedCompany company = FoundedCompany.Rehydrate(Tenant, new TranslatedName("منشأة نخيل"), 2, register);

        Assert.Equal("منشأة نخيل", company.NameAr);
        Assert.Equal(2, company.DisplayScale.Places);
        Assert.Equal(2, company.CostCenters.Count);

        // والترتيب حرفيٌّ ثابت لا ترتيب القراءة من القاعدة: قائمةٌ تتبع ترتيب الصفوف
        // تجعل شاشةً تُعيد ترتيب نفسها بين طلبين بلا سبب يراه المستخدم.
        Assert.Equal(["cc.001", "cc.002"], company.CostCenters.All.Select(static c => c.Code.Value));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · ولا مصنع ثالث: مجموعة المنتِجين مغلقة ومُسمّاة
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void لا_طريق_في_النواة_إلى_منشأة_مؤسَّسة_يتجاوز_المُنشئ()
    {
        Assembly core = typeof(FoundedCompany).Assembly;

        // لا مُنشئ عام على أيٍّ من النوعين: القيمة لا تُبنى من خارج المصنع.
        Assert.Empty(typeof(FoundedCompany).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(CostCenterRegister).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        string[] producers =
        [
            .. core.GetTypes()
                .Where(static type => !type.Name.StartsWith('<'))
                .SelectMany(static type => type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(static method => !method.IsSpecialName)
                    .Where(static method => Produces(method.ReturnType))
                    .Select(method => type.Name + "." + method.Name))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(producers.Length >= 6, $"المسح وجد {producers.Length} منتِجاً — الحارس ضامر.");

        Assert.Equal(
            [
                // الخدمة: تنادي المصنع ثم المخزن، ولا تبني شيئاً بنفسها.
                "CompanySetupService.AddCostCenterAsync",
                "CompanySetupService.GetAsync",
                "CompanySetupService.InitialiseAsync",
                "CompanySetupService.MoveDefaultCostCenterAsync",
                "CompanySetupService.MutateAsync",
                "CompanySetupService.ReinstateCostCenterAsync",
                "CompanySetupService.RenameCostCenterAsync",
                "CompanySetupService.SuspendCostCenterAsync",

                // السجلّ: كل عملية تُرجع سجلّاً جديداً من المُنشئ الخاصّ نفسه.
                "CostCenterRegister.Add",
                "CostCenterRegister.MoveDefault",
                "CostCenterRegister.Open",
                "CostCenterRegister.Rehydrate",
                "CostCenterRegister.Reinstate",
                "CostCenterRegister.Rename",
                "CostCenterRegister.Replace",
                "CostCenterRegister.Suspend",

                // المصنع، والاشتقاق الذي ينسخ المقياس ولا يقبله، والإحياء من صفوف.
                "FoundedCompany.Found",
                "FoundedCompany.Rehydrate",
                "FoundedCompany.WithCostCenters",

                // المخازن: تُرجع ما بنته المصانع أعلاه، ولا تملك طريقاً غيرها.
                "ICompanySetupStore.FindAsync",
                "InMemoryCompanySetupStore.FindAsync",
                "PostgresCompanySetupStore.FindAsync",
                "PostgresCompanySetupStore.Materialise",
                "PostgresCompanySetupStore.ReadAsync",
            ],
            producers);
    }

    private static bool Produces(Type returnType)
    {
        if (returnType == typeof(FoundedCompany) || returnType == typeof(CostCenterRegister))
        {
            return true;
        }

        if (!returnType.IsGenericType)
        {
            return false;
        }

        Type[] arguments = returnType.GetGenericArguments();
        return arguments.Length == 1 && Produces(arguments[0]);
    }

    private static CostCenter Active(string code, string nameAr)
        => Build(code, nameAr, CostCenterState.Active, string.Empty);

    private static CostCenter Suspended(string code, string nameAr, string reason)
        => Build(code, nameAr, CostCenterState.Suspended, reason);

    /// <summary>
    /// يبني مركزاً بالمُنشئ الداخلي — وهو ما تفعله طبقة الاستمرارية بالضبط، فالحالة
    /// المفحوصة هنا هي حالتُها لا حالةٌ مصطنعة.
    /// </summary>
    private static CostCenter Build(string code, string nameAr, CostCenterState state, string reason)
        => new(new CostCenterCode(code), new TranslatedName(nameAr), state, reason);
}
