using Babel.Core.CompanySetup;

namespace BabelDemoCompany;

/// <summary>عميل في البيانات التجريبية.</summary>
/// <param name="Code">رمزه.</param>
/// <param name="Arabic">اسمه العربي — وهو السجلّ (ADR-0021).</param>
/// <param name="English">ترجمته الإنجليزية — عرضٌ لا سجلّ.</param>
/// <param name="CreditLimit">حدّ الائتمان بالريال.</param>
/// <param name="TermsDays">مهلة السداد بالأيام — وهي ما يُبنى عليه تقرير الأعمار.</param>
internal sealed record DemoCustomer(string Code, string Arabic, string English, decimal CreditLimit, int TermsDays);

/// <summary>مورد في البيانات التجريبية.</summary>
/// <param name="Code">رمزه.</param>
/// <param name="Arabic">اسمه العربي.</param>
/// <param name="English">ترجمته.</param>
/// <param name="TermsDays">مهلة السداد.</param>
/// <param name="ExpenseCategory">تصنيف المصروف الغالب عنده — وهو مؤهّل دور ترحيل لا رمز حساب.</param>
internal sealed record DemoSupplier(string Code, string Arabic, string English, int TermsDays, string ExpenseCategory);

/// <summary>بند خدمة أو صنف يُباع.</summary>
/// <param name="Arabic">بيانه العربي.</param>
/// <param name="English">ترجمته.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="TaxClassification">التصنيف الضريبي: <c>standard</c> أو <c>zero</c> أو <c>exempt</c>.</param>
internal sealed record DemoItem(string Arabic, string English, decimal UnitPrice, string TaxClassification);

/// <summary>
/// المنشأة التجريبية: <b>مؤسسة نخيل الشرقية للتجارة والمقاولات</b>.
/// <para>
/// <b>وكلّها مُفتعَلة بوضوح ومُعلَنة كذلك</b> — أسماءٌ لا تخصّ منشأة قائمة، وأرقامٌ
/// مُختارة لتُظهر شرائح أعمار مختلفة، وأرقام تسجيل ضريبي من نطاق الاختبار. هذا شرطٌ
/// لا زينة: بياناتٌ تجريبية تشبه بيانات عميل حقيقي تُنسخ يوماً إلى تقرير حقيقي.
/// </para>
/// </summary>
internal static class Company
{
    /// <summary>اسم المنشأة العربي.</summary>
    public const string NameArabic = "مؤسسة نخيل الشرقية للتجارة والمقاولات";

    /// <summary>الفرع المستعمل بُعداً تحليلياً على الإيراد.</summary>
    public const string Branch = "BR-01";

    /// <summary>
    /// مسوّدة تأسيس المنشأة التجريبية — <b>الإعلان الوحيد لها في الشجرة</b>.
    /// <para>
    /// ‏<see cref="CostCenterPlan.One"/>: منشأةٌ بمركز تكلفة واحد اسمه اسمها، وهو ما يصف
    /// هذه المنشأة فعلاً. و<b>رمز المركز لا يُكتب هنا</b>: السجلّ يسكّه عند التأسيس
    /// (<c>cc.001</c>)، وثابتةٌ مكتوبة بيد تُوافقه اليوم هي ثابتةٌ تخالفه يوم يتغيّر
    /// السكّ — وقد كان في هذا الملف <c>CC-01</c> لا يوافق أي رمز يسكّه السجلّ.
    /// فمن يحتاج الرمز يقرؤه من <see cref="FoundedCompany.CostCenters"/>.
    /// </para>
    /// <para>خانتان عشريتان: الريال السعودي، ولا يتغيّر المقياس بعد التأسيس.</para>
    /// </summary>
    public static CompanySetupDraft SetupDraft { get; } = new(
        CompanyNameAr: NameArabic,
        CompanyNameTranslations: null,
        CostCenters: CostCenterPlan.One,
        FirstCostCenterNameAr: null,
        FirstCostCenterTranslations: null,
        DecimalPlaces: 2);

    /// <summary>حساب البنك في دفتر الخزينة المساعد.</summary>
    public const string Bank = "BANK-01";

    /// <summary>الصندوق النقدي.</summary>
    public const string Cash = "CASH-01";

    /// <summary>العملاء الثمانية.</summary>
    public static IReadOnlyList<DemoCustomer> Customers { get; } =
    [
        new("CUS-001", "شركة الفيصلية للمقاولات المحدودة", "Al-Faisaliah Contracting Ltd.", 500_000m, 30),
        new("CUS-002", "مؤسسة درّة الخليج للتجارة", "Durrat Al-Khaleej Trading Est.", 250_000m, 45),
        new("CUS-003", "شركة الرياض للتطوير العمراني", "Riyadh Urban Development Co.", 1_000_000m, 60),
        new("CUS-004", "مصنع الجزيرة للمنتجات البلاستيكية", "Al-Jazeera Plastics Factory", 300_000m, 30),
        new("CUS-005", "مؤسسة بحر العرب للنقل البرّي", "Bahr Al-Arab Land Transport Est.", 150_000m, 15),
        new("CUS-006", "شركة واحة الظهران التجارية", "Dhahran Oasis Trading Co.", 400_000m, 45),
        new("CUS-007", "مجموعة السلام الطبية", "Al-Salam Medical Group", 750_000m, 60),
        new("CUS-008", "مؤسسة نور المدينة للمقاولات", "Nour Al-Madinah Contracting Est.", 200_000m, 30),
    ];

    /// <summary>الموردون الستة.</summary>
    public static IReadOnlyList<DemoSupplier> Suppliers { get; } =
    [
        new("SUP-001", "شركة الخليج لمواد البناء", "Gulf Building Materials Co.", 45, "repairs"),
        new("SUP-002", "مؤسسة الشرق للتوريدات الصناعية", "Al-Sharq Industrial Supplies Est.", 30, "office"),
        new("SUP-003", "شركة المرافق الوطنية للخدمات", "National Utilities Services Co.", 15, "utilities"),
        new("SUP-004", "مكتب الأمانة للاستشارات المحاسبية", "Al-Amanah Accounting Consultancy", 30, "professional_fees"),
        new("SUP-005", "شركة رواسي للنقل والشحن", "Rawasi Freight & Logistics Co.", 30, "fuel"),
        new("SUP-006", "مؤسسة البيان للدعاية والإعلان", "Al-Bayan Advertising Est.", 20, "marketing"),
    ];

    /// <summary>ما تبيعه المنشأة.</summary>
    public static IReadOnlyList<DemoItem> Items { get; } =
    [
        new("توريد وتركيب أعمال كهربائية", "Electrical works supply and installation", 4_500m, "standard"),
        new("أعمال تشطيبات داخلية", "Interior finishing works", 7_250m, "standard"),
        new("توريد مواد عزل مائي", "Waterproofing materials supply", 1_875m, "standard"),
        new("خدمات إشراف هندسي", "Engineering supervision services", 12_000m, "standard"),
        new("صيانة دورية — عقد سنوي", "Periodic maintenance — annual contract", 9_400m, "standard"),
        new("توريد معدات للتصدير", "Equipment supply for export", 22_500m, "zero"),
        new("أعمال حفر وردم", "Excavation and backfilling works", 3_300m, "standard"),
        new("توريد أدوات صحّية", "Sanitary ware supply", 2_150m, "standard"),
    ];

    /// <summary>بنود المصروف التي تصل فواتير الموردين بها.</summary>
    public static IReadOnlyList<DemoItem> Expenses { get; } =
    [
        new("مواد بناء متنوّعة", "Assorted building materials", 6_800m, "standard"),
        new("قرطاسية ومستلزمات مكتبية", "Stationery and office supplies", 1_450m, "standard"),
        new("استهلاك كهرباء ومياه", "Electricity and water consumption", 3_900m, "standard"),
        new("أتعاب مراجعة حسابات ربع سنوية", "Quarterly audit fees", 15_000m, "standard"),
        new("وقود ونقل بضائع", "Fuel and freight", 2_600m, "standard"),
        new("حملة إعلانية", "Advertising campaign", 8_500m, "standard"),
    ];
}
