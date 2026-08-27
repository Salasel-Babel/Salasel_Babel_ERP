using System.Globalization;
using System.Reflection;
using Babel.Core.Entitlement;
using Babel.Purchasing.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>
/// <b>رقم التسجيل الضريبي للمورد — الطرف الثاني من مطابقة مُصدَّقة.</b>
/// <para>
/// رمز فاتورة المورد يحمل رقم تسجيل البائع <b>مُصدَّقاً</b>: كتبه المُصدِر داخل الرمز،
/// لا قارئٌ ضوئي. وما لم يقابله في جدول الموردين عمودٌ يُطابَق عليه، فالمُصدَّق يصل
/// ولا يُطابقه شيء — والأثر ليس بطئاً في الإدخال وحده: فاتورة مُصدَّقة تُسند إلى
/// المورد الخطأ بلا أن يعترض شيء، فينتج المعرّف <b>مظهر التحقّق</b> بلا تحقّق.
/// </para>
/// </summary>
[Collection("payables")]
public sealed class SupplierVatNumberTests : IAsyncLifetime
{
    /// <summary>رقم صالح الشكل — خمس عشرة خانة تبدأ بـ3 وتنتهي بـ3.</summary>
    private const string Valid = "310000000000003";

    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>رقم فريد لهذا البند — كل بند يبني حالته بنفسه ولا يقرأ ما كتبه غيره.</summary>
    private static string NextVat()
    {
        int serial = Interlocked.Increment(ref _sequence);
        return "3" + serial.ToString("D13", CultureInfo.InvariantCulture) + "3";
    }

    private static string NextCode(string prefix)
        => prefix + "-VAT-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الثغرة نفسها: المورد يحمل الرقم، والوحدة تعرف كيف تجد مورداً به
    // ═══════════════════════════════════════════════════════════════════════
    //
    // بندٌ بنيويّ لا سلوكيّ عمداً: قبل هذا التغيير كان صفّ المورد بلا عمود رقم ضريبي
    // وكانت الخدمة بلا بحث إلا بالمعرّف، فكان المُصدَّق يصل ولا يُطابقه شيء. والبند
    // يقرأ الشكل من التجميعة ومن كتالوج PostgreSQL معاً: عمودٌ في النموذج بلا عمود
    // في القاعدة ثغرةٌ كاملة.
    [Fact]
    public async Task The_supplier_carries_a_vat_number_and_the_module_can_find_a_supplier_by_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        Type row = typeof(SupplierService).Assembly.GetType("Babel.Purchasing.Persistence.SupplierRow")
            ?? throw new InvalidOperationException("لم يُعثر على SupplierRow.");

        PropertyInfo? column = row.GetProperty("VatNumber", BindingFlags.Public | BindingFlags.Instance);

        MethodInfo? lookup = typeof(SupplierService).GetMethod(
            "FindByVatNumberAsync", BindingFlags.Public | BindingFlags.Instance);

        RequiresEntitlementAttribute? entitlement = lookup?.GetCustomAttribute<RequiresEntitlementAttribute>();

        await using NpgsqlConnection connection = new(PurchasingTestEnvironment.Purchasing.ConnectionString);
        await connection.OpenAsync(token);

        string columnType = await ScalarTextAsync(
            connection,
            """
            select format_type(a.atttypid, a.atttypmod)
              from pg_attribute a
             where a.attrelid = 'purchasing.supplier'::regclass and a.attname = 'VatNumber' and a.attnum > 0
            """,
            token);

        string indexDefinition = await ScalarTextAsync(
            connection,
            "select indexdef from pg_indexes where schemaname = 'purchasing' and indexname = 'ix_purchasing_supplier_vat_number'",
            token);

        Proof.Require(
            column is not null
            && column.PropertyType == typeof(string)
            && lookup is not null
            && lookup.GetParameters().Any(static parameter => parameter.ParameterType == typeof(string))
            && entitlement is { Module: BabelModule.Purchasing, Access: EntitlementAccess.Read }
            && columnType != "(لا شيء)"
            && indexDefinition.Contains("VatNumber", StringComparison.Ordinal),
            "صفّ المورد يحمل رقم التسجيل الضريبي، والخدمة تبحث به تحت استحقاق قراءة، والعمود والفهرس حيّان في القاعدة",
            "الخاصية=" + (column?.PropertyType.Name ?? "(معدومة)")
            + " · البحث=" + (lookup?.Name ?? "(غير موجود)")
            + " · الاستحقاق=" + (entitlement is null ? "(بلا سمة)" : entitlement.Module + "/" + entitlement.Access)
            + " · نوع العمود=" + columnType
            + " · الفهرس=" + indexDefinition);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · الشكل يُفرض، والأرقام غير اللاتينية تُرفض ولا تُحوَّل
    // ═══════════════════════════════════════════════════════════════════════
    //
    // المجموعة المفحوصة غير فارغة بالبناء، ولكل مُدخَل رمز رفض **مُسمّى** لا مجرّد
    // «فشل»: رسالةٌ عامّة على رقم عربي-هندي تُرسل المحاسب إلى عدّ الخانات وهي خمس عشرة.
    [Fact]
    public async Task The_shape_is_enforced_and_non_latin_digits_are_refused_never_converted()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        (string Value, string Expected, string Title)[] cases =
        [
            ("٣١٠٠٠٠٠٠٠٠٠٠٠٠٣", "purchasing.supplier.vat_number_non_ascii_digits", "أرقام عربية-هندية U+0660"),
            ("۳۱۰۰۰۰۰۰۰۰۰۰۰۰۳", "purchasing.supplier.vat_number_non_ascii_digits", "أرقام شرقية U+06F0"),
            ("३१००००००००००००३", "purchasing.supplier.vat_number_non_ascii_digits", "أرقام ديفاناغارية U+0966"),
            ("31000000000000۳", "purchasing.supplier.vat_number_non_ascii_digits", "خانة أخيرة شرقية وسط أرقام لاتينية"),
            ("‏310000000000003", "purchasing.supplier.vat_number_invisible_control", "علامة اتجاه غير مرئية"),
            ("310000000000003​", "purchasing.supplier.vat_number_invisible_control", "فاصل عرض صفر"),
            ("310-000000000003", "purchasing.supplier.vat_number_not_digits", "شَرطة"),
            (" 310000000000003", "purchasing.supplier.vat_number_not_digits", "فراغ بادئ"),
            ("31000000000003", "purchasing.supplier.vat_number_length", "أربع عشرة خانة"),
            ("3100000000000003", "purchasing.supplier.vat_number_length", "ست عشرة خانة"),
            ("410000000000003", "purchasing.supplier.vat_number_prefix", "لا تبدأ بـ3"),
            ("310000000000001", "purchasing.supplier.vat_number_suffix", "لا تنتهي بـ3"),
        ];

        List<string> wrong = [];

        foreach ((string value, string expected, string title) in cases)
        {
            Result<SupplierView> created = await _harness.Suppliers.CreateAsync(
                PurchasingTestEnvironment.Tenant,
                Harness.Actor,
                new SupplierDraft(
                    NextCode("SUP"),
                    new LocalizedName("مورد شكل", "Shape supplier"),
                    Harness.Sar(0m),
                    30,
                    value),
                token);

            string actual = created.IsFailure ? created.Errors[0].Code : "(قُبل!)";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                wrong.Add(title + ": توقّع " + expected + " وجاء " + actual);
            }
        }

        // والصحيح يُقبل — وإلا لكان الحارس يرفض كل شيء ويبدو ناجحاً.
        Result<SupplierView> accepted = await _harness.Suppliers.CreateAsync(
            PurchasingTestEnvironment.Tenant,
            Harness.Actor,
            new SupplierDraft(NextCode("SUP"), new LocalizedName("مورد صالح", "Valid supplier"), Harness.Sar(0m), 30, Valid),
            token);

        // والفراغ يُقبل: «لم يُسجَّل» حالة مشروعة لا نقص بيانات.
        Result<SupplierView> blank = await _harness.Suppliers.CreateAsync(
            PurchasingTestEnvironment.Tenant,
            Harness.Actor,
            new SupplierDraft(NextCode("SUP"), new LocalizedName("مورد بلا رقم", "Unregistered supplier"), Harness.Sar(0m), 30),
            token);

        Proof.Require(
            cases.Length > 0
            && wrong.Count == 0
            && accepted.IsSuccess
            && accepted.Value.VatNumber == Valid
            && blank.IsSuccess
            && blank.Value.VatNumber.Length == 0,
            "كل صيغة مشوَّهة تُرفض برمزها المُسمّى، والصالح يُقبل كما ورد، والفراغ يمرّ",
            "حالات مفحوصة=" + cases.Length.ToString(CultureInfo.InvariantCulture)
            + " · مخالفات=" + (wrong.Count == 0 ? "لا شيء" : string.Join(" | ", wrong))
            + " · الصالح=" + (accepted.IsSuccess ? accepted.Value.VatNumber : "(رُفض)")
            + " · الفراغ=" + (blank.IsSuccess ? "«" + blank.Value.VatNumber + "»" : "(رُفض)"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · البحث يجد الواحد، ولا يعبر حدّ المستأجر
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_single_active_supplier_is_found_by_its_vat_number_and_never_across_tenants()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string vat = NextVat();
        string code = NextCode("SUP");

        Guid id = await CreateAsync(PurchasingTestEnvironment.Tenant, code, vat, token);

        Result<SupplierView> found = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.Tenant, Harness.Actor, vat, token);

        // المستأجر الثاني يحمل الرقم نفسه — والبحث فيه يجد مورده هو، لا مورد الأول.
        string otherCode = NextCode("SUP");
        Guid otherId = await CreateAsync(PurchasingTestEnvironment.GatewayTenant, otherCode, vat, token);

        Result<SupplierView> otherFound = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.GatewayTenant, Harness.Actor, vat, token);

        Result<SupplierView> missing = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.InjectedTenant, Harness.Actor, vat, token);

        Proof.Require(
            found.IsSuccess
            && found.Value.Id == id
            && found.Value.Code == code
            && otherFound.IsSuccess
            && otherFound.Value.Id == otherId
            && otherId != id
            && missing.IsFailure
            && missing.Errors[0].Code == "purchasing.supplier.vat_number_not_found",
            "الرقم يقود إلى مورده داخل مستأجره، والرقم نفسه في مستأجر آخر مورد آخر، وثالثٌ لا يجده",
            "المستأجر الأول=" + (found.IsSuccess ? found.Value.Code : "(رُفض)")
            + " · المستأجر الثاني=" + (otherFound.IsSuccess ? otherFound.Value.Code : "(رُفض)")
            + " · المستأجر الثالث=" + (missing.IsFailure ? missing.Errors[0].Code : "(وجد!)"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · مورّدان فعّالان برقم واحد: يُسجَّلان، ولا يُختار أحدهما أبداً
    // ═══════════════════════════════════════════════════════════════════════
    //
    // هذا هو موضع القرار كلّه. المجموعة الضريبية الواحدة تحمل رقم تسجيل واحداً لعدّة
    // منشآت، فالتسجيل مشروع — وفهرس فريد كان سيمنع **تسجيل الواقع**. والحارس هنا:
    // البحث يرفض ويُسمّي المرشّحين، ولا يُرجع «الأول».
    [Fact]
    public async Task Two_active_suppliers_may_share_one_vat_number_and_the_lookup_refuses_to_choose()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string vat = NextVat();
        string first = NextCode("GRP-A");
        string second = NextCode("GRP-B");

        Guid firstId = await CreateAsync(PurchasingTestEnvironment.Tenant, first, vat, token);
        Guid secondId = await CreateAsync(PurchasingTestEnvironment.Tenant, second, vat, token);

        Result<SupplierView> ambiguous = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.Tenant, Harness.Actor, vat, token);

        // ثم يُوقف أحدهما — وهو ما يفعله المستأجر بعد اندماج أو إعادة إنشاء —
        // فيعود الجواب واحداً بلا لبس.
        await ExecuteAsync(
            PurchasingTestEnvironment.Purchasing.ConnectionString,
            $"""update purchasing.supplier set "IsActive" = false where "Id" = '{secondId:D}'""",
            token);

        Result<SupplierView> resolved = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.Tenant, Harness.Actor, vat, token);

        // ثم يُوقف الثاني أيضاً: لا يُقال «غير موجود» — تلك الرسالة تدفع إلى إنشاء ثالث.
        await ExecuteAsync(
            PurchasingTestEnvironment.Purchasing.ConnectionString,
            $"""update purchasing.supplier set "IsActive" = false where "Id" = '{firstId:D}'""",
            token);

        Result<SupplierView> allInactive = await _harness.Suppliers.FindByVatNumberAsync(
            PurchasingTestEnvironment.Tenant, Harness.Actor, vat, token);

        Proof.Require(
            firstId != secondId
            && ambiguous.IsFailure
            && ambiguous.Errors[0].Code == "purchasing.supplier.vat_number_ambiguous"
            && ambiguous.Errors[0].MessageAr.Contains(first, StringComparison.Ordinal)
            && ambiguous.Errors[0].MessageAr.Contains(second, StringComparison.Ordinal)
            && resolved.IsSuccess
            && resolved.Value.Id == firstId
            && allInactive.IsFailure
            && allInactive.Errors[0].Code == "purchasing.supplier.vat_number_only_inactive",
            "الرقم المشترك يُسجَّل، والبحث يرفض بالغموض ويُسمّي المرشّحين، ثم يحسم بعد الإيقاف، ولا يقول «غير موجود» على موقوف",
            "الغموض=" + (ambiguous.IsFailure ? ambiguous.Errors[0].Code : "(اختار!)")
            + " · المرشّحون في الرسالة=" + (ambiguous.IsFailure ? ambiguous.Errors[0].MessageAr : "—")
            + " · بعد إيقاف الثاني=" + (resolved.IsSuccess ? resolved.Value.Code : "(رُفض)")
            + " · بعد إيقاف الاثنين=" + (allInactive.IsFailure ? allInactive.Errors[0].Code : "(وجد!)"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · قيد الشكل في القاعدة ليس زخرفاً — انتهاك حقيقي يُلتقط
    // ═══════════════════════════════════════════════════════════════════════
    //
    // الشيفرة تفحص، لكن الشيفرة ليست الطريق الوحيد إلى الجدول: هجرةٌ يدوية، ونصّ
    // إصلاح، وأداة استيراد. القيد في القاعدة هو ما يجعل الشكل صحيحاً **مهما كان
    // الطريق**. والمجموعة المفحوصة غير فارغة بالبناء: البند يبني صفّه أولاً.
    [Fact]
    public async Task The_database_check_constraint_catches_a_shape_violation_written_around_the_service()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid id = await CreateAsync(PurchasingTestEnvironment.Tenant, NextCode("SUP"), NextVat(), token);

        await using NpgsqlConnection connection = new(PurchasingTestEnvironment.Purchasing.ConnectionString);
        await connection.OpenAsync(token);

        long population = await ScalarLongAsync(
            connection, "select count(*) from purchasing.supplier where \"VatNumber\" <> ''", token);

        string arabicIndic = await ViolationAsync(
            connection, $"""update purchasing.supplier set "VatNumber" = '٣١٠٠٠٠٠٠٠٠٠٠٠٠٣' where "Id" = '{id:D}'""", token);

        string tooShort = await ViolationAsync(
            connection, $"""update purchasing.supplier set "VatNumber" = '31000' where "Id" = '{id:D}'""", token);

        string badPrefix = await ViolationAsync(
            connection, $"""update purchasing.supplier set "VatNumber" = '410000000000003' where "Id" = '{id:D}'""", token);

        string nulled = await ViolationAsync(
            connection, $"""update purchasing.supplier set "VatNumber" = null where "Id" = '{id:D}'""", token);

        Proof.Require(
            population > 0
            && arabicIndic == "23514"
            && tooShort == "23514"
            && badPrefix == "23514"
            && nulled == "23502",
            "القيد في القاعدة يرفض الرقم العربي-الهندي والطول الخطأ والبادئة الخطأ، والعمود يرفض القيمة المعدومة",
            "صفوف بأرقام=" + population.ToString(CultureInfo.InvariantCulture)
            + " · عربي-هندي=" + arabicIndic
            + " · طول قصير=" + tooShort
            + " · بادئة خطأ=" + badPrefix
            + " · معدوم=" + nulled);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · الترقية على قاعدة **قائمة مملوءة** — لا على قاعدة فارغة
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏EnsureCreated ينشئ الشكل الصحيح في قاعدة فارغة **ولا يفعل شيئاً في قاعدة
    // قائمة**. فقاعدة عميل مُنشأة قبل هذا التغيير لن ترى العمود ولا الفهرس ولا القيد
    // ما لم يُشغَّل نصّ الترقية. البند يبني قاعدة بشكل ما قبل التغيير بالضبط، يملؤها
    // بموردين، ثم يستدعي الناشر ويقرأ النتيجة من كتالوج PostgreSQL.
    [Fact]
    public async Task The_deployer_adds_the_column_to_an_existing_populated_database_without_losing_a_row()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string probeDatabase = TestRunScope.Name(PurchasingTestEnvironment.VatProbeDatabaseStem);
        string admin = PurchasingTestEnvironment.Maintenance;
        string probeConnection =
            $"Host=127.0.0.1;Port=5432;Database={probeDatabase};Username=postgres;Include Error Detail=true";

        await using (NpgsqlConnection maintenance = new(admin))
        {
            await maintenance.OpenAsync(token);
            await ExecuteAsync(maintenance, $"create database {probeDatabase}", token);
        }

        try
        {
            PurchasingOptions options = new() { ConnectionString = probeConnection, CompanyCurrency = "SAR" };

            // (أ) الشكل الحالي، ثم **إرجاعه إلى شكل ما قبل التغيير بالضبط**:
            //     لا عمود، ولا فهرس، ولا قيد.
            await PurchasingSchemaDeployer.DeployAsync(options, token);

            await using NpgsqlConnection probe = new(probeConnection);
            await probe.OpenAsync(token);

            await ExecuteAsync(
                probe,
                """
                alter table purchasing.supplier drop constraint ck_purchasing_supplier_vat_number;
                drop index purchasing.ix_purchasing_supplier_vat_number;
                alter table purchasing.supplier drop column "VatNumber";
                """,
                token);

            // (ب) موردون حقيقيون بالشكل القديم — قاعدة مملوءة لا فارغة.
            for (int index = 1; index <= 4; index++)
            {
                await ExecuteAsync(
                    probe,
                    $"""
                    insert into purchasing.supplier
                        ("Id", "TenantId", "Code", "NameAr", "NameEn", "CreditLimit", "PaymentTermsDays", "IsActive")
                    values (gen_random_uuid(), gen_random_uuid(), 'OLD-{index}', 'مورد قديم {index}',
                            'Legacy supplier {index}', 0, 30, true)
                    """,
                    token);
            }

            long before = await ScalarLongAsync(probe, "select count(*) from purchasing.supplier", token);
            string columnBefore = await ScalarTextAsync(probe, ColumnQuery, token);

            // (ج) الناشر يُستدعى على القاعدة القائمة — وهذا بالضبط ما يفعله النشر.
            await PurchasingSchemaDeployer.DeployAsync(options, token);

            long after = await ScalarLongAsync(probe, "select count(*) from purchasing.supplier", token);
            string columnAfter = await ScalarTextAsync(probe, ColumnQuery, token);
            bool notNull = await ScalarBoolAsync(
                probe,
                """
                select a.attnotnull from pg_attribute a
                 where a.attrelid = 'purchasing.supplier'::regclass and a.attname = 'VatNumber'
                """,
                token);

            // كل صفّ قائم يحمل الاصطلاح الواحد للخواء: نصّ فارغ، ولا قيمة معدومة.
            long blanks = await ScalarLongAsync(
                probe, """select count(*) from purchasing.supplier where "VatNumber" = ''""", token);

            string indexAfter = await ScalarTextAsync(
                probe,
                "select indexdef from pg_indexes where schemaname = 'purchasing' and indexname = 'ix_purchasing_supplier_vat_number'",
                token);

            string constraintAfter = await ScalarTextAsync(
                probe,
                """
                select pg_get_constraintdef(oid) from pg_constraint
                 where conname = 'ck_purchasing_supplier_vat_number'
                   and conrelid = 'purchasing.supplier'::regclass
                """,
                token);

            // والترقية تُعاد بلا أثر: النشر يقع مرّتين في الحياة الواقعية.
            await PurchasingSchemaDeployer.DeployAsync(options, token);
            long afterTwice = await ScalarLongAsync(probe, "select count(*) from purchasing.supplier", token);
            long indexes = await ScalarLongAsync(
                probe,
                "select count(*) from pg_indexes where schemaname = 'purchasing' and indexname = 'ix_purchasing_supplier_vat_number'",
                token);

            Proof.Require(
                before == 4
                && after == 4
                && afterTwice == 4
                && columnBefore == "(لا شيء)"
                && columnAfter == "character varying(15)"
                && notNull
                && blanks == 4
                && indexAfter.Contains("VatNumber", StringComparison.Ordinal)
                && indexAfter.Contains("<> ''::text", StringComparison.Ordinal)
                && !indexAfter.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                && constraintAfter.Contains("^3[0-9]{13}3$", StringComparison.Ordinal)
                && indexes == 1,
                "الترقية تعمل على قاعدة قائمة مملوءة: الصفوف كما هي، والعمود والفهرس الجزئي والقيد صاروا أحياء، وإعادتها بلا أثر",
                "الصفوف قبل=" + before.ToString(CultureInfo.InvariantCulture)
                + " وبعد=" + after.ToString(CultureInfo.InvariantCulture)
                + " وبعد إعادة النشر=" + afterTwice.ToString(CultureInfo.InvariantCulture)
                + " · العمود قبل=" + columnBefore
                + " · العمود بعد=" + columnAfter + " not null=" + notNull
                + " · صفوف بفراغ=" + blanks.ToString(CultureInfo.InvariantCulture)
                + " · الفهرس=" + indexAfter
                + " · القيد=" + constraintAfter);
        }
        finally
        {
            await using NpgsqlConnection maintenance = new(admin);
            await maintenance.OpenAsync(token);
            await ExecuteAsync(maintenance, $"drop database if exists {probeDatabase} with (force)", token);
        }
    }

    private const string ColumnQuery =
        """
        select format_type(a.atttypid, a.atttypmod) from pg_attribute a
         where a.attrelid = 'purchasing.supplier'::regclass and a.attname = 'VatNumber' and a.attnum > 0
        """;

    private async Task<Guid> CreateAsync(TenantId tenant, string code, string vatNumber, CancellationToken token)
    {
        Result<SupplierView> created = await _harness.Suppliers.CreateAsync(
            tenant,
            Harness.Actor,
            new SupplierDraft(code, new LocalizedName("مورد " + code, "Supplier " + code), Harness.Sar(0m), 30, vatNumber),
            token);

        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Errors[0].ToString());
        }

        return created.Value.Id;
    }

    /// <summary>ينفّذ انتهاكاً داخل معاملة تُلغى دائماً، ويُعيد SQLSTATE الملتقَط.</summary>
    private static async Task<string> ViolationAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        try
        {
            await using NpgsqlCommand command = new(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(token);
            return "(لم يُرفض)";
        }
        catch (PostgresException failure)
        {
            return failure.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    private static async Task<string> ScalarTextAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (await command.ExecuteScalarAsync(token)) as string ?? "(لا شيء)";
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (await command.ExecuteScalarAsync(token)) is true;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(token);
        await ExecuteAsync(connection, sql, token);
    }
}
