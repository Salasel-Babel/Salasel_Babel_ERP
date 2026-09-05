using System.Net;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// الخوادم المشتركة بين الاختبارات، واعتماداتها، وإعدادها.
/// <para>
/// خادم واحد يخدم أغلب المجموعة، وخوادم إضافية تُقلع عند الحاجة إلى ثقافة أخرى أو إلى
/// إعداد معطوب عمداً. وكلها تُقلع من الثنائي المبنيّ نفسه.
/// </para>
/// </summary>
internal static class ApiFixture
{
    /// <summary>اعتماد مستأجر «أ» — يبلغ الشركة «أ» وحدها.</summary>
    public static TestCredential TokenA { get; } = TestCredential.Create(
        ApiTestDatabase.CompanyA, new Guid("11111111-1111-4111-8111-111111111111"), ApiTestDatabase.CompanyA);

    /// <summary>اعتماد مستأجر «ب» — يبلغ الشركة «ب» وحدها.</summary>
    public static TestCredential TokenB { get; } = TestCredential.Create(
        ApiTestDatabase.CompanyB, new Guid("22222222-2222-4222-8222-222222222222"), ApiTestDatabase.CompanyB);

    /// <summary>اعتماد مستأجر «ج» — المبيعات عنده «للقراءة فقط».</summary>
    public static TestCredential TokenC { get; } = TestCredential.Create(
        ApiTestDatabase.CompanyC, new Guid("33333333-3333-4333-8333-333333333333"), ApiTestDatabase.CompanyC);

    /// <summary>
    /// شركات مخصّصة لاختبارات التأسيس — <b>واحدة لكل اختبار</b>.
    /// <para>
    /// التأسيس يُقبل مرّة واحدة لكل منشأة بحكم القرار نفسه، فمنشأةٌ مشتركة بين اختبارين
    /// تجعل الثاني يمرّ أو يسقط بحسب من سبقه — وهو بالضبط العطل الذي وُجد مسح العزل
    /// لأجله. ولذلك لكل اختبار منشأته، ولا اختبار يقرأ حالةً كتبها غيره.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Guid> SetupCompanies { get; } =
    [
        .. Enumerable.Range(1, 16).Select(static index =>
            new Guid(string.Create(CultureInfo.InvariantCulture, $"5e700000-0000-4000-8000-{index:D12}"))),
    ];

    /// <summary>اعتماد اختبارات التأسيس — يبلغ منشآته وحدها.</summary>
    public static TestCredential TokenS { get; } = TestCredential.Create(
        SetupCompanies[0], new Guid("55555555-5555-4555-8555-555555555555"), [.. SetupCompanies]);

    /// <summary>
    /// اعتماد صحيح <b>لا يبلغ شركةً واحدة</b> — حالة «اشتُرك ولم يُربط بمنشأة».
    /// <para>
    /// وهي ليست حالة نظرية: هي أول حالة يقع فيها كل عميل جديد بين لحظة إنشاء اعتماده
    /// ولحظة ربطه بمنشأته. وما يراه اليوم يقرّر إن كان سيفتح تذكرة دعم أم لا.
    /// </para>
    /// </summary>
    public static TestCredential TokenNoCompany { get; } = TestCredential.Create(
        new Guid("99999999-9999-4999-8999-999999999999"),
        new Guid("99999999-9999-4999-8999-99999999990a"));

    /// <summary>
    /// اعتماد <b>منقضٍ</b> — لحظة انقضائه في الماضي البعيد فلا تعتمد على ساعة تشغيل الاختبار.
    /// </summary>
    public static TestCredential TokenExpired { get; } = TestCredential.Create(
        ApiTestDatabase.CompanyA, new Guid("44444444-4444-4444-8444-444444444444"), ApiTestDatabase.CompanyA);

    /// <summary>لحظة انقضاء الاعتماد المنقضي، بصيغة ISO 8601 الدوّارة كما يقرؤها الخادم.</summary>
    public const string ExpiredAt = "2020-01-01T00:00:00.0000000+00:00";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, ApiProcess> ByCulture = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ApiProcess> ByRateLimit = new(StringComparer.Ordinal);
    private static ApiProcess? _default;

    static ApiFixture() =>
        AppDomain.CurrentDomain.ProcessExit += static (_, _) =>
        {
            foreach (ApiProcess process in ByCulture.Values.Concat(ByRateLimit.Values))
            {
                process.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            _default?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };

    /// <summary>رمز إلغاء الاختبار الجاري — تمريره في كل نداء شرطُ استجابة الإلغاء.</summary>
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>الخادم المشترك: ثقافة <c>en-US</c> وقاعدة بيانات سليمة.</summary>
    public static async Task<ApiProcess> DefaultAsync()
    {
        CancellationToken cancellationToken = Token;

        if (_default is not null)
        {
            return _default;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_default is null)
            {
                await ApiTestDatabase.EnsureAsync(cancellationToken).ConfigureAwait(false);
                _default = await StartAndFoundAsync(
                    ApiTestDatabase.Options.AppConnectionString, "en_US.UTF-8", cancellationToken)
                    .ConfigureAwait(false);
            }

            return _default;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// خادم بثقافة نظام محدّدة، <b>مُعاد استعماله</b> لكل حالات الثقافة نفسها.
    /// <para>
    /// إقلاع عملية لكل حالة نظرية كان يعني أربعة عشر خادماً في مجموعة واحدة، وأربعة عشر
    /// مجمّع اتصالات — وهو حِمل يكشف هشاشة تهيئة مجموعات أخرى تعمل بالتوازي. الخوادم هنا
    /// خمسة لا أكثر، وتُقتل عند خروج العملية.
    /// </para>
    /// </summary>
    /// <param name="culture">اسم الموضع النظامي.</param>
    public static async Task<ApiProcess> WithCultureAsync(string culture)
    {
        if (ByCulture.TryGetValue(culture, out ApiProcess? existing))
        {
            return existing;
        }

        CancellationToken cancellationToken = Token;
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ByCulture.TryGetValue(culture, out ApiProcess? found))
            {
                return found;
            }

            await ApiTestDatabase.EnsureAsync(cancellationToken).ConfigureAwait(false);

            ApiProcess started = await StartAndFoundAsync(
                ApiTestDatabase.Options.AppConnectionString, culture, cancellationToken)
                .ConfigureAwait(false);

            ByCulture[culture] = started;
            return started;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// خادمٌ بحدّ معدّل ضيّق على الأبواب المفتوحة — <b>وحده، ولا يتقاسمه أحد</b>.
    /// <para>
    /// والحدّ عدّادٌ في ذاكرة العملية، فخادمٌ مشترك بين اختبارات كثيرة يجعل رقم العدّاد
    /// يعتمد على من سبق — وهو بعينه العطل الذي وُجد مسح العزل لأجله. فهذه المجموعة
    /// تملك خادمها، وتُقلعه بحدّ صغير كي يُبلَغ في طلبات معدودة بدل ثلاثمئة.
    /// </para>
    /// </summary>
    /// <param name="perMinute">الحدّ لكل مفتاح في الدقيقة.</param>
    public static async Task<ApiProcess> WithRateLimitAsync(int perMinute)
    {
        string key = perMinute.ToString(CultureInfo.InvariantCulture);

        if (ByRateLimit.TryGetValue(key, out ApiProcess? existing))
        {
            return existing;
        }

        CancellationToken cancellationToken = Token;
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ByRateLimit.TryGetValue(key, out ApiProcess? found))
            {
                return found;
            }

            await ApiTestDatabase.EnsureAsync(cancellationToken).ConfigureAwait(false);

            Dictionary<string, string> environment = Environment(ApiTestDatabase.Options.AppConnectionString);
            environment["Babel__RateLimit__PerMinute"] = key;

            // ‏**ولا يُتخلَّص منه في نهاية الاختبار**: التخلّص وسط تشغيلٍ يقتل عملية
            // خادم بينما خيط تجميع مُخرَجها ما يزال حيّاً، فتخرج المجموعة بـ«خيوط
            // أمامية بقيت تعمل» — عطلٌ يُقرأ في السطح وهو في التصريف. والخوادم كلّها
            // تُقتل عند خروج العملية، وهو الموضع الوحيد الذي لا يتقاطع مع اختبار جارٍ.
            ApiProcess started = await ApiProcess
                .StartAsync(environment, "en_US.UTF-8", cancellationToken)
                .ConfigureAwait(false);

            ByRateLimit[key] = started;
            return started;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// خادم موجَّه إلى قاعدة <b>دفتر</b> غير موجودة — لإثبات أن العطل التشغيلي لا يتسرّب.
    /// <para>
    /// <b>⚠ و<c>EnsureAsync</c> هنا ليست زيادة:</b> هذا الخادم يحمل اتصال دفتر معطوباً
    /// <b>عمداً</b> واتصال نواة <b>سليماً</b> — وتأسيس المنشآت في
    /// <see cref="StartAndFoundAsync"/> يمرّ بالنواة. فبلا تهيئة القواعد أولاً لا تكون
    /// قاعدة النواة موجودة أصلاً، فيردّ التأسيس <c>500</c> ويسقط الاختبار عند التهيئة
    /// لا عند ما يفحصه.
    /// </para>
    /// <para>
    /// وكان ذلك يمرّ لأن <see cref="DefaultAsync"/> يسبقه في التشغيل الكامل فيهيّئ القواعد
    /// نيابةً عنه — أي أن الاختبار كان <b>يعتمد على ترتيب التنفيذ</b>: يمرّ في المجموعة
    /// كاملةً، ويسقط وحده. ومسح العزل هو ما كشفه.
    /// </para>
    /// </summary>
    public static async Task<ApiProcess> WithUnreachableDatabaseAsync()
    {
        CancellationToken cancellationToken = Token;
        await ApiTestDatabase.EnsureAsync(cancellationToken).ConfigureAwait(false);

        return await StartAndFoundAsync(
            "Host=127.0.0.1;Port=5432;Database=babel_api_tests_no_such_database;Username="
                + ApiTestDatabase.AppRole + ";Include Error Detail=true",
            "en_US.UTF-8",
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>يُقلع خادماً ثم يؤسّس منشآته — بهذا الترتيب، ولا خادم بلا تأسيس.</b>
    /// <para>
    /// ‏ADR-0026: مركز التكلفة يُنشأ عند التأسيس، والترحيل يسأل عنه قبل أن يبني طلباً.
    /// فمنشأةٌ غير مؤسَّسة تُرفض بـ<c>company_setup.not_found</c> — <b>وذلك هو السلوك
    /// الصحيح</b>: دفترٌ لمنشأة بلا مقياس عرض ولا مركز تكلفة ليس دفتراً.
    /// </para>
    /// <para>
    /// <b>ولماذا هنا لا في كل اختبار:</b> ليجعل الشرط <b>خاصية الخادم</b> لا شيئاً
    /// يتذكّره كل اختبار — واختبارات التأسيس نفسها تملك منشآتها المستقلّة
    /// (<see cref="SetupCompanies"/>) فلا تتصادم مع هذا.
    /// </para>
    /// <para>
    /// وقد كان مخزن التأسيس <b>في ذاكرة العملية</b>، فكان كل خادم يُقلَع يبدأ بلا منشأة
    /// واحدة، وكان هذا التأسيس هو ما يخفي ذلك. وصار المخزن على PostgreSQL: فالخادم
    /// الثاني على القاعدة نفسها يجد المنشأة مؤسَّسة ويردّ <c>409</c> — وهو الجواب
    /// المقبول أدناه، وهو <b>نفسه</b> الدليل على أن التأسيس لم يعد يموت مع العملية.
    /// </para>
    /// </summary>
    private static async Task<ApiProcess> StartAndFoundAsync(
        string ledgerConnection,
        string culture,
        CancellationToken cancellationToken)
    {
        ApiProcess started = await ApiProcess
            .StartAsync(Environment(ledgerConnection), culture, cancellationToken)
            .ConfigureAwait(false);

        foreach ((Guid company, TestCredential credential) in new[]
                 {
                     (ApiTestDatabase.CompanyA, TokenA),
                     (ApiTestDatabase.CompanyB, TokenB),
                     (ApiTestDatabase.CompanyC, TokenC),
                 })
        {
            using HttpResponseMessage founded = await started.Call(Http.Request(
                HttpMethod.Put,
                string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/setup"),
                credential,
                """{"companyNameAr":"منشأة اختبار سطح HTTP","costCenters":"One","decimalPlaces":2}"""))
                .ConfigureAwait(false);

            // ‏201 أول مرّة، و409 إن كان خادمٌ آخر أسّسها في القاعدة نفسها. وما عداهما
            // خللٌ يُرمى الآن بنصّه، لا يُترك ليظهر بعد عشرين اختباراً بـ«404 غير مفهوم».
            if (founded.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Conflict))
            {
                string body = await founded.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"تعذّر تأسيس منشأة الاختبار {company:D}: {(int)founded.StatusCode} — {body}"));
            }
        }

        return started;
    }

    /// <summary>إعداد الخادم كاملاً بمتغيّرات البيئة — لا ملف إعداد ولا سرّ في المستودع.</summary>
    /// <param name="ledgerConnection">اتصال دور التطبيق.</param>
    public static Dictionary<string, string> Environment(string ledgerConnection)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["Babel__Ledger__AppConnectionString"] = ledgerConnection,
            ["Babel__Ledger__OwnerConnectionString"] = ApiTestDatabase.Options.OwnerConnectionString,
            ["Babel__Ledger__CompanyCurrency"] = "SAR",

            // النواة: **اتصال دور التطبيق وحده**. ولا مفتاح لاتصال المالك هنا ولا في
            // الخادم أصلاً — خادمٌ يحمله يستطيع إسقاط مشغّل ثبات المقياس (ADR-0003).
            ["Babel__Core__AppConnectionString"] = ApiTestDatabase.Core.AppConnectionString,
            ["Babel__Core__AppRole"] = ApiTestDatabase.Core.AppRole,

            // وحدتا المستندات: اتصالٌ لكلٍّ إلى قاعدتها. وبدون هذين المفتاحين يقلع
            // الخادم على الافتراضيّين — `babel_sales` و`babel_purchasing` على المضيف
            // المحلي — أيّاً كان النشر، فيكتب في قاعدة ليست قاعدة هذا التشغيل أو يسقط.
            // وكان ذلك غير مرئي ما دام لا باب HTTP يبلغ الوحدتين.
            ["Babel__Sales__ConnectionString"] = ApiTestDatabase.Sales.ConnectionString,
            ["Babel__Purchasing__ConnectionString"] = ApiTestDatabase.Purchasing.ConnectionString,

            // ووحدة المخزون: ترحيل استلام البضاعة يسجّل الوارد في دفترها المساعد
            // **قبل** أن يُدين حساب المراقبة، فقاعدتها في مسار الطلب لا خارجه. وبدون
            // هذا المفتاح يقلع الخادم على `babel_inventory` على المضيف المحلي — وهو
            // نفس عطل المبيعات والمشتريات، باقياً في وحدة ثالثة لأن لا باب كان يبلغها.
            ["Babel__Inventory__ConnectionString"] = ApiTestDatabase.Inventory.ConnectionString,

            // ووحدة الموارد البشرية: **الوحيدة التي يرفض الخادم الإقلاع بلا اتصالها**.
            // ولا افتراضي لها عمداً — وهذا هو الفارق عن الثلاث أعلاه: خادمٌ يشير بأثقل
            // جدول بيانات شخصية في المنتج إلى قاعدة أخرى بصمت ليس عطلَ إعدادٍ بل حادثة
            // بيانات. فحذفُ هذا السطر يُسقط الإقلاع بصوته، وهو المطلوب.
            ["Babel__Hr__ConnectionString"] = ApiTestDatabase.Hr.ConnectionString,
            // ووحدة المقاولات: قاعدتها في مسار الطلب كسائرها. و**سُجّلت باتصالها من
            // الإعداد منذ سطرها الأول** — لا بعد تسليمٍ كامل كما وقع للثلاث قبلها.
            ["Babel__Projects__ConnectionString"] = ApiTestDatabase.Projects.ConnectionString,
            // ووحدة العقارات: **كان هذا المفتاح غائباً**، فيقلع خادم الاختبار على
            // ارتدادٍ صامت إلى `babel_realestate` بالمستخدم الفائق — قاعدةٌ خارج هذا
            // التشغيل لا يملكها ولا يكنسها أحد. ولم يظهر ذلك لأن لا اختبار هنا يطرق
            // باباً عقارياً، و**مسارٌ لا يُسلَك لا يُظهر إعداداً خاطئاً**.
            ["Babel__RealEstate__ConnectionString"] = ApiTestDatabase.RealEstate.ConnectionString,
            // ── سطح الاشتراك: مُهيَّأ صراحةً، وبدور سطحٍ لا بمستخدم إدارة ────────
            // والتهيئة مفتاحٌ صريح لا استنتاج من وجود قاعدة: خادمٌ على آلة فيها قاعدة
            // تحكّم لغرضٍ آخر لا يفتح سطح الاشتراك عليها إلا بقرار.
            //
            // وكل قيمة من البيئة ولا سرّ في المستودع: الاتصال محلّي بلا كلمة مرور،
            // وكلمة المرور — حين تلزم — تُقرأ من BABEL_CP_SURFACE_PASSWORD وحدها.
            ["Babel__Fleet__Enabled"] = "true",
            ["BABEL_CP_CONTROL_DB_NAME"] = ApiTestDatabase.ControlDatabase,
            ["BABEL_CP_APP_ROLE"] = ApiTestDatabase.ControlAppRole,
            ["BABEL_CP_SURFACE_ROLE"] = ApiTestDatabase.SurfaceRole,
            // ── مخزن المرفقات: اتصال دور التطبيق، وجذرٌ على القرص، ومفتاح توقيع ──
            //
            // ‏**والمفتاح يُولَّد لهذه العملية ولا يُودَع.** غيابُه عطلٌ يُعلَن عند
            // التركيب لا مفتاحٌ يُخترع (ADR-0046 دليل 14): مُصدِرٌ يولّد لنفسه مفتاحاً
            // عند الإقلاع يجعل كل تذكرة صالحةً قبل إعادة التشغيل ومرفوضةً بعدها،
            // والفشل يُقرأ «انتهت الصلاحية» لا «لا مفتاح».
            //
            // ‏**ولا اتصال مالك هنا** كما في النواة والدفتر: خادمٌ يحمله يستطيع إسقاط
            // مشغّل «يُضاف ولا يُعدَّل» ثم الكتابة فوق سند إثبات.
            ["Babel__Storage__AppConnectionString"] = ApiTestDatabase.Storage.AppConnectionString,
            ["Babel__Storage__RootPath"] = ApiTestDatabase.StorageRoot,
            ["Babel__Storage__TicketSigningKey"] = ApiTestDatabase.StorageTicketKeyHex,
            ["Babel__Storage__MaximumBytes"] =
                ApiTestDatabase.StorageMaximumBytes.ToString(CultureInfo.InvariantCulture),
        };

        int index = 0;
        foreach (TestCredential credential in new[] { TokenA, TokenB, TokenC, TokenS, TokenNoCompany, TokenExpired })
        {
            string prefix = string.Create(CultureInfo.InvariantCulture, $"Babel__Api__Tokens__{index}__");
            environment[prefix + "Sha256"] = credential.Digest;
            environment[prefix + "Tenant"] = credential.Tenant.ToString("D", CultureInfo.InvariantCulture);
            environment[prefix + "User"] = credential.User.ToString("D", CultureInfo.InvariantCulture);

            for (int c = 0; c < credential.Companies.Count; c++)
            {
                environment[string.Create(CultureInfo.InvariantCulture, $"{prefix}Companies__{c}")] =
                    credential.Companies[c].ToString("D", CultureInfo.InvariantCulture);
            }

            if (credential == TokenExpired)
            {
                environment[prefix + "NotAfter"] = ExpiredAt;
            }

            index++;
        }

        // مستأجر «ج»: اشتراكٌ انقضى. العقارات «للقراءة فقط»، والأصول لم تُشترَ قط.
        environment[Entitlement(ApiTestDatabase.CompanyC, "RealEstate")] = "ReadOnly";

        // ── والمبيعات والمشتريات كذلك — **وهذا تصحيح لما كان مكتوباً هنا** ────────
        // كان في هذا الموضع أنّ الوحدات الإلزامية «لا تقبل حالة غير Entitled إطلاقاً»،
        // وأنّ «عميلاً توقّف عن الدفع» حالة غير قابلة للتمثيل على أهمّ وحدات المنتج.
        // وقد قُرئ `EntitlementSet.Validate` فوُجد أنّ ذلك **لم يعد صحيحاً**: المرفوض
        // على الإلزامية هو `NotEntitled` وحده، و`ReadOnly` مقبولة صراحةً — وذلك هو
        // مضمون `traps.md#fakh-mandatory-module-cannot-be-read-only`، فُتِح ثم أُغلق،
        // وبقي هذا التعليق يصف الحال قبل إغلاقه. وتعليقٌ يصف قيداً زال أسوأ من غيابه:
        // يُقرأ فيُصرَف الناظر عن اختبارٍ صار ممكناً.
        //
        // والالتزام معهما بالضرورة لا بالاختيار: `Compliance` يعتمد على `Sales` في
        // ‏`ModuleDependencyGraph`، و«قدرة الوحدة لا تتجاوز قدرة ما تعتمد عليه» — فمجموعةٌ
        // فيها التزامٌ فاعل فوق مبيعاتٍ للقراءة تُرفض عند الإقلاع، ويسقط الخادم بصوته.
        // ── والمخزون على الشركة «ب» وحدها ────────────────────────────────────────
        // ‏**والفارق مقصود ومُختبَر:** المخزون وحدة اختيارية لا إلزامية، فالافتراضي
        // على كل منشأة `NotEntitled`. والشركة «ب» تشتريه فيعمل عندها ترحيل الاستلام؛
        // والشركة «أ» لا تشتريه، فترحيل الاستلام عندها يُرفض بـ403
        // `entitlement.not_entitled` — **وهو الجواب الصحيح لا عطل**: باب الاستلام
        // يمسّ دفتر المخزون المساعد، فمنشأةٌ لم تشترِ المخزون لا تملكه.
        environment[Entitlement(ApiTestDatabase.CompanyB, "Inventory")] = "Entitled";

        // ── والمقاولات على الشركة «ب» وحدها كذلك ─────────────────────────────────
        // ‏`Projects` وحدة اختيارية، فالافتراضي `NotEntitled` على كل منشأة، ولا
        // يُشترى إلا حيث اشتُري ما تعتمد عليه: `ModuleDependencyGraph` يجعل
        // المقاولات فوق `Core` و`Ledger` و`Inventory`، و«قدرة الوحدة لا تتجاوز
        // قدرة ما تعتمد عليه» — فالشركة «ب» وحدها تصلح لها لأنّها وحدها اشترت
        // المخزون أعلاه. والشركة «أ» تبقى بلا مقاولات، فكل باب من أبواب المقاولات
        // عندها يُرفض بـ403 `entitlement.not_entitled` — **وهو الجواب الصحيح لا عطل**.
        environment[Entitlement(ApiTestDatabase.CompanyB, "Projects")] = "Entitled";

        environment[Entitlement(ApiTestDatabase.CompanyC, "Sales")] = "ReadOnly";
        environment[Entitlement(ApiTestDatabase.CompanyC, "Purchasing")] = "ReadOnly";
        environment[Entitlement(ApiTestDatabase.CompanyC, "Compliance")] = "ReadOnly";

        return environment;
    }

    private static string Entitlement(Guid tenant, string module) =>
        "Babel__Entitlements__" + tenant.ToString("D", CultureInfo.InvariantCulture) + "__" + module;
}

/// <summary>أدوات مخاطبة السطح: بناء الطلب، وقراءة الجسم، وقراءة رمز الخطأ الثابت.</summary>
internal static class Http
{
    /// <summary>يبني طلباً باعتماد وجسم نصّي خام — الخام مقصود: بعض الاختبارات تُرسل ما لا يُنتجه مُسلسِل.</summary>
    /// <param name="method">الفعل.</param>
    /// <param name="path">المسار.</param>
    /// <param name="credential">الاعتماد، أو <c>null</c> لطلب بلا اعتماد.</param>
    /// <param name="json">الجسم كنصّ JSON خام.</param>
    public static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        TestCredential? credential,
        string? json = null)
    {
        HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));

        if (credential is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", credential.Header);
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, new UTF8Encoding(false), "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        return request;
    }

    /// <summary>مسار الجلسة — خارج نطاق الشركة، وداخل المصادقة.</summary>
    public const string Session = "/api/v1/session";

    /// <summary>مسار ترحيل قيد لشركة.</summary>
    /// <param name="company">الشركة.</param>
    public static string PostEntry(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/journal-entries");

    /// <summary>مسار قراءة قيد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="entry">القيد.</param>
    public static string ReadEntry(Guid company, Guid entry) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/journal-entries/{entry:D}");

    /// <summary>مسار عكس قيد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="entry">القيد.</param>
    public static string Reverse(Guid company, Guid entry) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/journal-entries/{entry:D}/reversal");

    /// <summary>مسار ميزان المراجعة.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="period">الفترة، أو <c>null</c>.</param>
    public static string TrialBalance(Guid company, string book, string? period = null) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/trial-balance?book={book}")
        + (period is null ? string.Empty : "&period=" + period);

    /// <summary>مسار دليل الحسابات بشروط الترحيل.</summary>
    /// <param name="company">الشركة.</param>
    public static string ChartOfAccounts(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/chart-of-accounts");

    /// <summary>مسار إعادة التحقق من السلسلة.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="fiscalYear">السنة المالية.</param>
    public static string ChainVerification(Guid company, string book, int fiscalYear) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/ledger-chain/verification?book={book}&fiscalYear={fiscalYear}");

    /// <summary>ينادي الخادم بالطلب المعطى، برمز إلغاء الاختبار الجاري.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="request">الطلب.</param>
    public static Task<HttpResponseMessage> Call(this ApiProcess api, HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(api);
        return api.Client.SendAsync(request, ApiFixture.Token);
    }

    /// <summary>يقرأ ملفاً نصّياً برمز إلغاء الاختبار الجاري.</summary>
    /// <param name="path">المسار.</param>
    public static Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path, ApiFixture.Token);

    /// <summary>يقرأ ملفاً بايتات برمز إلغاء الاختبار الجاري.</summary>
    /// <param name="path">المسار.</param>
    public static Task<byte[]> ReadBytesAsync(string path) => File.ReadAllBytesAsync(path, ApiFixture.Token);

    /// <summary>يقرأ الجسم نصّاً ثم يحلّله مستنداً.</summary>
    /// <param name="response">الاستجابة.</param>
    public static async Task<(string Text, JsonElement Json)> BodyAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        string text = await response.Content.ReadAsStringAsync(ApiFixture.Token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(text);
        return (text, document.RootElement.Clone());
    }

    /// <summary>الرمز الثابت في تفاصيل المشكلة.</summary>
    /// <param name="problem">جسم المشكلة.</param>
    public static string CodeOf(JsonElement problem) => problem.GetProperty("code").GetString()!;
}
