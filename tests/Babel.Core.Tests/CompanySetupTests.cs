using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Babel.Contracts.Posting;
using Babel.Core.Audit;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>تأسيس المنشأة — قراران للمالك، كلاهما يقع مرّة واحدة.</b>
/// <para>
/// (١) لكل منشأة مركز تكلفة واحد على الأقل، ولا يخلو منه أي مستند.
/// (٢) عدد الخانات العشرية المعروضة يُسنَد عند أول تأسيس ولا يُعدَّل بعده.
/// </para>
/// <para>
/// وكل اختبار هنا يبذر حالته بنفسه: لا مخزن مشترك بين اختبارين، ولا ترتيب تنفيذ يُعتمد عليه.
/// </para>
/// </summary>
public sealed class CompanySetupTests
{
    private static readonly TenantId Company = new(Guid.Parse("cccccccc-1111-4111-8111-cccccccccccc"));
    private static readonly UserId Actor = new(Guid.Parse("dddddddd-2222-4222-8222-dddddddddddd"));

    // ── (١) المنشأة لا تخلو من مركز تكلفة ───────────────────────────────────

    [Fact]
    public void المنشأة_التي_لا_تعنيها_مراكز_التكلفة_تخرج_من_التأسيس_بمركز_اسمه_اسمها()
    {
        FoundedCompany setup = Founded(CostCenterPlan.One, firstCostCenter: null);

        Assert.Equal(1, setup.CostCenters.Count);
        Assert.Equal(1, setup.CostCenters.ActiveCount);
        Assert.Equal("مؤسسة سلاسل بابل", setup.CostCenters.DefaultCenter.NameAr);
        Assert.Equal("cc.001", setup.CostCenters.Default.Value);
        Assert.True(setup.CostCenters.DefaultCenter.IsActive);

        // ولا يرى صاحب هذا الجواب المفهوم أبداً: الحلّ يُعيد الافتراضي بلا أن يُسأل.
        Assert.Equal("cc.001", setup.CostCenters.Resolve(null).Value.Value);
    }

    [Fact]
    public void الجواب_متعدّد_بلا_اسم_أول_مركز_يُرفض_ولا_يُخترَع_له_اسم()
    {
        Result<FoundedCompany> refused = FoundedCompany.Found(Company, Draft(CostCenterPlan.Multiple, firstCostCenter: null));

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "company_setup.first_cost_center_name_required");
    }

    [Fact]
    public void الجواب_واحد_مع_اسم_أول_مركز_يُرفض_لأن_الاسمين_سيختلفان()
    {
        Result<FoundedCompany> refused = FoundedCompany.Found(Company, Draft(CostCenterPlan.One, "إدارة"));

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "company_setup.first_cost_center_name_not_expected");
    }

    [Fact]
    public void كل_أسباب_الرفض_تُرجع_مجتمعة_لا_أوّلها()
    {
        Result<FoundedCompany> refused = FoundedCompany.Found(
            Company,
            new CompanySetupDraft(
                CompanyNameAr: "   ",
                CompanyNameTranslations: null,
                CostCenters: CostCenterPlan.Multiple,
                FirstCostCenterNameAr: null,
                FirstCostCenterTranslations: null,
                DecimalPlaces: 9));

        Assert.True(refused.IsFailure);
        Assert.Equal(
            ["company_setup.decimal_places_out_of_range", "company_setup.first_cost_center_name_required", "company_setup.name_missing"],
            refused.Errors.Select(static error => error.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void المركز_الافتراضي_لا_يُوقَف_ولا_يُحذف()
    {
        CostCenterRegister register = Register("الإدارة العامة");

        Result<CostCenterRegister> refused = register.Suspend(register.Default, "سبب مكتوب كافٍ");

        Assert.True(refused.IsFailure);
        Assert.Equal("cost_center.default_cannot_be_suspended", Assert.Single(refused.Errors).Code);

        // والسجلّ لم يتغيّر: الرفض رفضٌ لا نصف تعديل.
        Assert.Equal(1, register.ActiveCount);
    }

    [Fact]
    public void حارس_الافتراضي_ليس_ضامراً_لأن_الحالة_التي_يمنعها_تُبلَغ_بخطوتين()
    {
        // لو كان الحارس ضامراً لما وُجد مسار مشروع يجعل المركز الأول قابلاً للإيقاف.
        CostCenterRegister register = Register("الإدارة العامة");
        CostCenterCode first = register.Default;

        register = register.Add("فرع جدة", null).Value;
        CostCenterCode second = new("cc.002");

        // ١ — ما دام الأول افتراضياً فالإيقاف مرفوض.
        Assert.True(register.Suspend(first, "إعادة هيكلة الإدارة").IsFailure);

        // ٢ — ينتقل الافتراضي إلى مركز عامل آخر…
        register = register.MoveDefault(second).Value;
        Assert.Equal(second, register.Default);

        // ٣ — …فيصير الإيقاف مشروعاً، والمنشأة تبقى بمركز عامل واحد على الأقل.
        register = register.Suspend(first, "إعادة هيكلة الإدارة").Value;

        Assert.Equal(2, register.Count);
        Assert.Equal(1, register.ActiveCount);
        Assert.Equal(CostCenterState.Suspended, register.Find(first)!.State);
        Assert.Equal("إعادة هيكلة الإدارة", register.Find(first)!.SuspensionReason);
    }

    [Fact]
    public void المركز_الموقوف_يبقى_في_السجلّ_مقروءاً_فالتاريخ_المُرحَّل_عليه_لا_يفقد_اسمه()
    {
        CostCenterRegister register = Register("الإدارة العامة");
        register = register.Add("فرع جدة", null).Value;
        register = register.MoveDefault(new CostCenterCode("cc.002")).Value;
        register = register.Suspend(new CostCenterCode("cc.001"), "أُغلق الفرع").Value;

        CostCenter suspended = register.Find(new CostCenterCode("cc.001"))!;

        Assert.Equal(2, register.Count);
        Assert.Equal("الإدارة العامة", suspended.NameAr);
        Assert.Contains(register.All, center => center.Code.Value == "cc.001");

        // وما يُمنع هو الاستعمال الجديد وحده — لا القراءة.
        Assert.True(register.Resolve("cc.001").IsFailure);
        Assert.Equal("cost_center.already_suspended", Assert.Single(register.Resolve("cc.001").Errors).Code);
    }

    [Fact]
    public void إعادة_التسمية_لا_تمسّ_الرمز_فسطور_القيود_تبقى_مربوطة()
    {
        CostCenterRegister register = Register("الإدارة العامة");
        CostCenterCode code = register.Default;

        register = register.Rename(code, "الإدارة المالية", new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "Finance" }).Value;

        Assert.Equal(code, register.Default);
        Assert.Equal("الإدارة المالية", register.DefaultCenter.NameAr);
        Assert.Equal("Finance", register.DefaultCenter.NameIn("en-GB"));
        Assert.Equal("الإدارة المالية", register.DefaultCenter.NameIn("ur-PK"));
    }

    [Fact]
    public void الحلّ_لا_يُرجع_مركزاً_فارغاً_أبداً()
    {
        CostCenterRegister register = Register("الإدارة العامة");

        foreach (string? absent in new[] { null, string.Empty, "   " })
        {
            Result<CostCenterCode> resolved = register.Resolve(absent);
            Assert.True(resolved.IsSuccess);
            Assert.True(resolved.Value.IsAssigned);
            Assert.Equal(register.Default, resolved.Value);
        }

        Assert.Equal("cost_center.not_found", Assert.Single(register.Resolve("cc.999").Errors).Code);
    }

    // ── الحارس البنيوي على الثابتة الأولى ───────────────────────────────────

    [Fact]
    public void لا_عملية_حذف_على_سجلّ_مراكز_التكلفة_إطلاقاً()
    {
        MethodInfo[] surface = [.. typeof(CostCenterRegister)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)];

        // غير ضامر: الماسح يرى سطحاً حقيقياً قبل أن يحكم عليه.
        Assert.True(surface.Length >= 6, $"الماسح رأى {surface.Length} دالة فقط — الحكم يمرّ فراغاً.");

        string[] removal = [.. surface
            .Select(static method => method.Name)
            .Where(static name =>
                name.Contains("Remove", StringComparison.Ordinal)
                || name.Contains("Delete", StringComparison.Ordinal)
                || name.Contains("Clear", StringComparison.Ordinal))];

        Assert.True(removal.Length == 0, "ظهرت عملية حذف على سجلّ مراكز التكلفة: " + string.Join("، ", removal));

        // ولا مُنشئ عام: السجلّ لا يُبنى من خارج مصنعه.
        Assert.Empty(typeof(CostCenterRegister).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(FoundedCompany).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void الحاجز_الأخير_يرمي_فعلاً_حين_تُحقَن_مخالفة_حقيقية()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(CostCenterRegister).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));

        CostCenter one = Mint("cc.001", "الإدارة العامة", CostCenterState.Active);
        CostCenter suspended = Mint("cc.001", "الإدارة العامة", CostCenterState.Suspended);

        // ١ — سجلّ فارغ.
        Assert.Contains("فارغ", Throw(constructor, ImmutableArray<CostCenter>.Empty, new CostCenterCode("cc.001")), StringComparison.Ordinal);

        // ٢ — افتراضي ليس في السجلّ.
        Assert.Contains("ليس في السجلّ", Throw(constructor, [one], new CostCenterCode("cc.404")), StringComparison.Ordinal);

        // ٣ — افتراضي موقوف.
        Assert.Contains("موقوف", Throw(constructor, [suspended], new CostCenterCode("cc.001")), StringComparison.Ordinal);

        // ٤ — رمز مكرَّر.
        Assert.Contains("مكرَّر", Throw(constructor, [one, one], new CostCenterCode("cc.001")), StringComparison.Ordinal);

        // والحاجز يقبل المُدخَل السليم — وإلا لكان يرمي دائماً فيمرّ الاختبار كذباً.
        object accepted = constructor.Invoke([ImmutableArray.Create(one), new CostCenterCode("cc.001")]);
        Assert.Equal(1, ((CostCenterRegister)accepted).Count);
    }

    // ── (٢) مقياس العرض: يُسنَد مرّة ولا يُعدَّل ──────────────────────────────

    [Fact]
    public async Task التأسيس_الثاني_يُرفض_ومقياس_الخانات_يبقى_كما_أُسنِد_أول_مرّة()
    {
        (CompanySetupService service, InMemoryAuditLog audit) = NewService();

        Result<FoundedCompany> first = await service.InitialiseAsync(
            new CompanyInitialisationRequest(Company, Actor, Draft(CostCenterPlan.One, null, places: 2)),
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.DisplayScale.Places);

        Result<FoundedCompany> second = await service.InitialiseAsync(
            new CompanyInitialisationRequest(Company, Actor, Draft(CostCenterPlan.One, null, places: 4)),
            TestContext.Current.CancellationToken);

        Assert.True(second.IsFailure);
        Assert.Equal("company_setup.already_initialised", Assert.Single(second.Errors).Code);

        // والمخزَّن لم يتحرّك خانةً واحدة.
        Result<FoundedCompany> read = await service.GetAsync(Company, Actor, TestContext.Current.CancellationToken);
        Assert.Equal(2, read.Value.DisplayScale.Places);

        // ووقعة التأسيس مسجَّلة مرّة واحدة لا مرّتين.
        IReadOnlyList<AuditEntry> entries = await audit.ReadAsync(Company, TestContext.Current.CancellationToken);
        Assert.Single(entries, entry => entry.Action == "company_setup.founded");
    }

    [Fact]
    public void لا_توقيع_في_الشجرة_يحمل_مقياساً_ثانياً_إلى_منشأة_قائمة()
    {
        MethodInfo[] store = [.. typeof(ICompanySetupStore).GetMethods()];

        Assert.True(store.Length >= 3, $"واجهة المخزن فيها {store.Length} دالة — الفحص يمرّ فراغاً.");

        // الدالة الوحيدة التي تحمل تأسيساً كاملاً — ومعه مقياسه — هي دالة التأسيس الأول،
        // وهي تُرجع bool أي «قُبل أو رُفض»، لا void أي «كُتب».
        MethodInfo[] carryingSetup = [.. store.Where(static method =>
            method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(FoundedCompany)))];

        MethodInfo found = Assert.Single(carryingSetup);
        Assert.Equal(nameof(ICompanySetupStore.TryFoundAsync), found.Name);
        Assert.Equal(typeof(ValueTask<bool>), found.ReturnType);

        // ولا دالة على المخزن تقبل مقياساً بذاته.
        Assert.DoesNotContain(store, static method =>
            method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(DisplayScale)));

        // ولا موضع على المنشأة المؤسَّسة يكتب المقياس بعد بنائها.
        PropertyInfo scale = typeof(FoundedCompany).GetProperty(nameof(FoundedCompany.DisplayScale))!;
        Assert.Null(scale.SetMethod);

        // والاشتقاق الوحيد ينسخ المقياس ولا يقبله.
        MethodInfo with = typeof(FoundedCompany).GetMethod(nameof(FoundedCompany.WithCostCenters))!;
        Assert.Equal([typeof(CostCenterRegister)], with.GetParameters().Select(static parameter => parameter.ParameterType));
    }

    // ── (٢-ب) وهل يحكم المقياس المبالغ المحسوبة؟ لا. ─────────────────────────

    [Fact]
    public void المقياس_يحكم_ما_يكتبه_إنسان_ولا_يحكم_ما_يحسبه_النظام()
    {
        DisplayScale two = DisplayScale.Of(2).Value;

        // صافٍ فردي وضريبة 15٪: النتيجة أربع خانات بطبيعتها، لا بخيار أحد.
        decimal net = 33.33m;
        decimal vat = net * 0.15m;

        Assert.Equal("4.9995", vat.ToString("0.0000", CultureInfo.InvariantCulture));

        // ١ — إنسانٌ لا يستطيع كتابة هذا الرقم في منشأة بخانتين: لا يرى ما يراجعه.
        Assert.False(two.AcceptsTypedAmount(vat));

        // ٢ — والنظام يخزّنه كما هو: التخزين NUMERIC(19,4) بحكم ADR-0002، ولا يستشير المقياس.
        Money stored = Money.Of(vat, new CurrencyCode("SAR"));
        Assert.Equal("4.9995", stored.ToCanonicalString());

        // ٣ — والعرض يقرّب، **ويقول إنه قرّب**، ويحمل معه النصّ القانوني.
        RenderedAmount shown = two.Render(vat);
        Assert.Equal("5.00", shown.Text);
        Assert.False(shown.IsExact);
        Assert.Equal("4.9995", shown.CanonicalText);

        // ٤ — ولو رفض المقياسُ المحسوبَ لصارت الفاتورة العادية مستحيلة. هذا هو الفرق.
        Assert.EndsWith("4.9995", two.Render(vat).CanonicalText, StringComparison.Ordinal);
    }

    [Fact]
    public void المقياس_يقبل_ما_يساويه_عدد_خاناته_ولو_كُتب_بخانات_أكثر()
    {
        DisplayScale two = DisplayScale.Of(2).Value;

        Assert.True(two.AcceptsTypedAmount(5m));
        Assert.True(two.AcceptsTypedAmount(5.0000m));
        Assert.True(two.AcceptsTypedAmount(-1250.5000m));
        Assert.False(two.AcceptsTypedAmount(12.345m));
        Assert.False(two.AcceptsTypedAmount(0.0001m));

        DisplayScale zero = DisplayScale.Of(0).Value;
        Assert.True(zero.AcceptsTypedAmount(1000m));
        Assert.False(zero.AcceptsTypedAmount(1000.50m));

        DisplayScale four = DisplayScale.Of(4).Value;
        Assert.True(four.AcceptsTypedAmount(4.9995m));
    }

    [Fact]
    public void مدى_المقياس_محصور_بمقياس_التخزين_نفسه()
    {
        Assert.Equal(4, DisplayScale.Maximum);
        Assert.Equal(Money.CanonicalScale, DisplayScale.Maximum);

        foreach (int places in new[] { -1, 5, 9 })
        {
            Result<DisplayScale> refused = DisplayScale.Of(places);
            Assert.True(refused.IsFailure);
            Assert.Equal("company_setup.decimal_places_out_of_range", Assert.Single(refused.Errors).Code);
        }

        foreach (int places in new[] { 0, 1, 2, 3, 4 })
        {
            Assert.Equal(places, DisplayScale.Of(places).Value.Places);
        }
    }

    [Fact]
    public void العرض_لا_يقرأ_ثقافة_العملية_ولا_تقويمها()
    {
        DisplayScale two = DisplayScale.Of(2).Value;
        List<string> rendered = [];

        foreach (string culture in new[] { "en-US", "ar-SA", "tr-TR", "hi-IN", "de-DE", "fa-IR" })
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                rendered.Add(two.Render(-1234.5678m).Text + "|" + two.Render(-1234.5678m).CanonicalText);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        Assert.Equal(6, rendered.Count);
        Assert.Equal("-1234.57|-1234.5678", Assert.Single(rendered.Distinct(StringComparer.Ordinal)));
    }

    [Fact]
    public void لا_نوع_خارج_فضاء_التأسيس_يرى_مقياس_العرض()
    {
        // الادّعاء: المقياس عرضٌ وإدخال، ولا يدخل الحساب ولا العقد المُرحَّل ولا أي وحدة
        // أخرى. وهذا يُفحص لا يُفترض.
        //
        // ⚠️ **ونطاق المسح اختير ليكون قابلاً للانتهاك فعلاً.** مسحُ Babel.SharedKernel
        // وBabel.Contracts وحدهما كان سيمرّ **فراغاً** مهما ساءت الشيفرة: اتجاه الاعتماد
        // إلى الأسفل (القاعدة 3) يجعلهما عاجزين بنيوياً عن رؤية نوع في Babel.Core أصلاً.
        // ولذلك يشمل المسح **Babel.Core نفسها خارج فضاء التأسيس** — وهناك المخالفة ممكنة
        // بسطر واحد، فالحكم عليها حكمٌ حقيقي.
        Assembly core = typeof(DisplayScale).Assembly;
        Assembly[] scanned = [core, typeof(Money).Assembly, typeof(PostingLine).Assembly];

        List<string> offenders = [];
        int membersOutside = 0;
        int mentionsInside = 0;

        foreach (Assembly assembly in scanned)
        {
            foreach (Type type in assembly.GetTypes())
            {
                bool inside = type.Namespace?.StartsWith("Babel.Core.CompanySetup", StringComparison.Ordinal) == true;

                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (inside)
                    {
                        if (Mentions(member))
                        {
                            mentionsInside++;
                        }

                        continue;
                    }

                    membersOutside++;

                    if (Mentions(member))
                    {
                        offenders.Add(type.FullName + "." + member.Name);
                    }
                }
            }
        }

        // ١ — الماسح يرى سطحاً حقيقياً خارج فضاء التأسيس.
        Assert.True(membersOutside > 200, $"مُسح {membersOutside} عضواً خارج فضاء التأسيس فقط — الفحص يمرّ فراغاً.");

        // ٢ — والمُطابِق يعمل: داخل فضاء التأسيس يجد إشارات كثيرة. ولولا هذا الشاهد الموجب
        //     لكان «صفر مخالفات» يعني «الماسح أعمى» بالقدر نفسه الذي يعني به «لا مخالفة».
        Assert.True(mentionsInside >= 10, $"المُطابِق وجد {mentionsInside} إشارة داخل فضاء التأسيس — أي أنه لا يطابق شيئاً.");

        // ٣ — والحكم.
        Assert.True(offenders.Count == 0, "نوع خارج فضاء التأسيس يرى مقياس العرض:\n" + string.Join('\n', offenders));

        // ٤ — ولا Money ولا سطر الترحيل يعرفانه بالاسم كذلك.
        Assert.DoesNotContain(
            typeof(Money).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
            Mentions);
        Assert.DoesNotContain(
            typeof(PostingLine).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
            Mentions);
    }

    // ── الخدمة: الاستحقاق، والتدقيق، والثابتة عبر المخزن ────────────────────

    [Fact]
    public async Task إضافة_مركز_وإيقافه_تمرّان_بالاستحقاق_وتُسجَّلان_في_التدقيق()
    {
        (CompanySetupService service, InMemoryAuditLog audit) = NewService();

        await service.InitialiseAsync(
            new CompanyInitialisationRequest(Company, Actor, Draft(CostCenterPlan.Multiple, "الإدارة العامة")),
            TestContext.Current.CancellationToken);

        Result<FoundedCompany> added = await service.AddCostCenterAsync(
            Company, Actor, "فرع جدة", null, TestContext.Current.CancellationToken);

        Assert.True(added.IsSuccess);
        Assert.Equal(2, added.Value.CostCenters.Count);

        Result<FoundedCompany> refused = await service.SuspendCostCenterAsync(
            Company, Actor, added.Value.CostCenters.Default, "سبب مكتوب كافٍ", TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Equal("cost_center.default_cannot_be_suspended", Assert.Single(refused.Errors).Code);

        Result<FoundedCompany> suspended = await service.SuspendCostCenterAsync(
            Company, Actor, new CostCenterCode("cc.002"), "أُغلق الفرع نهائياً", TestContext.Current.CancellationToken);

        Assert.True(suspended.IsSuccess);
        Assert.Equal(2, suspended.Value.CostCenters.Count);
        Assert.Equal(1, suspended.Value.CostCenters.ActiveCount);

        IReadOnlyList<AuditEntry> entries = await audit.ReadAsync(Company, TestContext.Current.CancellationToken);
        string[] actions = [.. entries.Select(static entry => entry.Action)];

        Assert.Contains("company_setup.founded", actions, StringComparer.Ordinal);
        Assert.Contains("cost_center.added", actions, StringComparer.Ordinal);
        Assert.Contains("cost_center.suspended", actions, StringComparer.Ordinal);
        Assert.Contains(entries, entry => entry.Details?.Contains("أُغلق الفرع نهائياً", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task العمل_على_منشأة_غير_مؤسَّسة_يُرفض_ولا_يُنشئ_شيئاً_ضمناً()
    {
        (CompanySetupService service, _) = NewService();

        Result<FoundedCompany> read = await service.GetAsync(Company, Actor, TestContext.Current.CancellationToken);
        Assert.Equal("company_setup.not_found", Assert.Single(read.Errors).Code);

        Result<FoundedCompany> added = await service.AddCostCenterAsync(
            Company, Actor, "فرع جدة", null, TestContext.Current.CancellationToken);
        Assert.Equal("company_setup.not_found", Assert.Single(added.Errors).Code);

        Result<CostCenterCode> resolved = await service.ResolveCostCenterAsync(
            Company, Actor, null, TestContext.Current.CancellationToken);
        Assert.True(resolved.IsFailure);
    }

    [Fact]
    public async Task الإيقاف_بلا_سبب_مكتوب_مرفوض()
    {
        (CompanySetupService service, _) = NewService();

        await service.InitialiseAsync(
            new CompanyInitialisationRequest(Company, Actor, Draft(CostCenterPlan.Multiple, "الإدارة العامة")),
            TestContext.Current.CancellationToken);

        await service.AddCostCenterAsync(Company, Actor, "فرع جدة", null, TestContext.Current.CancellationToken);

        foreach (string? reason in new[] { null, string.Empty, "قصير" })
        {
            Result<FoundedCompany> refused = await service.SuspendCostCenterAsync(
                Company, Actor, new CostCenterCode("cc.002"), reason, TestContext.Current.CancellationToken);

            Assert.True(refused.IsFailure);
            Assert.Contains(refused.Errors, error => error.Code == "cost_center.suspension_reason_required");
        }
    }

    // ── أدوات ───────────────────────────────────────────────────────────────

    private static bool Mentions(MemberInfo member) => member switch
    {
        MethodBase method => method.GetParameters().Any(static parameter => Mentions(parameter.ParameterType))
            || (method is MethodInfo info && Mentions(info.ReturnType)),
        PropertyInfo property => Mentions(property.PropertyType),
        FieldInfo field => Mentions(field.FieldType),
        _ => false,
    };

    private static bool Mentions(Type type)
        => type == typeof(DisplayScale)
            || type == typeof(RenderedAmount)
            || (type.IsGenericType && type.GetGenericArguments().Any(Mentions));

    private static CostCenter Mint(string code, string nameAr, CostCenterState state)
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(CostCenter).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance),
            static candidate => candidate.GetParameters().Length == 5);

        return (CostCenter)constructor.Invoke(
        [
            new CostCenterCode(code),
            nameAr,
            ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal),
            state,
            state == CostCenterState.Suspended ? "سبب مكتوب كافٍ" : string.Empty,
        ]);
    }

    private static string Throw(ConstructorInfo constructor, ImmutableArray<CostCenter> centers, CostCenterCode defaultCode)
    {
        TargetInvocationException raised = Assert.Throws<TargetInvocationException>(
            () => constructor.Invoke([centers, defaultCode]));

        InvalidOperationException inner = Assert.IsType<InvalidOperationException>(raised.InnerException);
        return inner.Message;
    }

    private static CompanySetupDraft Draft(CostCenterPlan plan, string? firstCostCenter, int places = 2)
        => new(
            CompanyNameAr: "مؤسسة سلاسل بابل",
            CompanyNameTranslations: null,
            CostCenters: plan,
            FirstCostCenterNameAr: firstCostCenter,
            FirstCostCenterTranslations: null,
            DecimalPlaces: places);

    private static FoundedCompany Founded(CostCenterPlan plan, string? firstCostCenter)
        => FoundedCompany.Found(Company, Draft(plan, firstCostCenter)).Value;

    private static CostCenterRegister Register(string nameAr) => CostCenterRegister.Open(nameAr, null).Value;

    private static (CompanySetupService Service, InMemoryAuditLog Audit) NewService()
    {
        InMemoryUsageStore usage = new();
        InMemoryAuditLog audit = new();
        TimeProvider clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero));
        InMemoryEntitlementService entitlements = new(audit, clock);
        EntitlementEnforcer enforcer = new(entitlements, usage, clock);

        return (new CompanySetupService(new InMemoryCompanySetupStore(), enforcer, audit, clock), audit);
    }
}
