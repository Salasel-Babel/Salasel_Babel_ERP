using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Ai.Lookup;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Core.NameRegister;
using Babel.Sales;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>بيئة إثبات البحث المحلّي: PostgreSQL حقيقية، ومخطّط مبيعات حقيقي.</b>
/// <para>
/// <b>ولماذا جدول المبيعات لا جدولٌ يخترعه الإثبات:</b> المطلوب إثباته أن الطيّ والفهرس
/// يعملان على <c>sales.customer</c> كما ينشئه EF فعلاً — بأعمدته المقتبَسة بحالة الجمل
/// (<c>"TenantId"</c> · <c>"NameAr"</c>) وبغياب عمود شركةٍ فيه. وجدولٌ يكتبه الإثبات بيده
/// يُثبت أن الإثبات يعمل.
/// </para>
/// <para>
/// <b>ولا دفتر أستاذ هنا:</b> لا شيء في هذا المسار يُرحَّل، و<c>SalesSchemaDeployer</c>
/// يحتاج نصّ اتصال وحده.
/// </para>
/// <para>
/// <b>واسم القاعدة خاصّ بهذه العملية</b> — نفس علّة <c>TestRunScope</c> في كل مجموعة
/// أخرى: عمليتان متزامنتان باسمٍ ثابت تُسقط كلٌّ منهما قاعدة الأخرى في منتصف تشغيلها.
/// </para>
/// </summary>
internal static class LookupTestEnvironment
{
    /// <summary>الجذع الثابت لاسم القاعدة.</summary>
    public const string DatabaseStem = "babel_lookup";

    /// <summary>حدّ المعرّف في PostgreSQL: ‏<c>NAMEDATALEN − 1</c> بايتاً.</summary>
    private const int MaxIdentifierBytes = 63;

    /// <summary>قاعدة هذه العملية وحدها.</summary>
    public static string Database { get; } = ProcessScopedName(DatabaseStem);

    /// <summary>نصّ اتصال المالك — النشر والقراءة معاً في هذه المجموعة.</summary>
    public static string ConnectionString { get; } =
        "Host=127.0.0.1;Port=5432;Database=" + Database + ";Username=postgres;Include Error Detail=true";

    /// <summary>اتصال الصيانة.</summary>
    public static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_ARAP_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    // ── منشآت الإثباتات — **لكل إثباتٍ منشأتُه** (فخ-132، ويفرضه NoTestTenantIsSharedByTwoProofs) ──

    /// <summary>منشأة إثبات «لا مطابق».</summary>
    public static TenantId NoMatchTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000001"));

    /// <summary>منشأة إثبات «مطابقٌ واحد».</summary>
    public static TenantId SingleMatchTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000002"));

    /// <summary>منشأة إثبات «اثنان فأكثر ⇒ سؤال».</summary>
    public static TenantId AmbiguousTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000003"));

    /// <summary>
    /// منشأة إثبات «الغموض لا يُقاس منه عدد» — يُزرع فيها 2 ثم 3 ثم 7 ثم 50 صفّاً.
    /// وعزلها ليس ترفاً: هي المنشأة الوحيدة التي <b>يتغيّر عدد صفوفها أثناء الإثبات</b>.
    /// </summary>
    public static TenantId ScaleTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000004"));

    /// <summary>المنشأة التي يُصدَر فيها المِقبض في إثبات العبور بين المنشآت.</summary>
    public static TenantId MintedHereTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000005"));

    /// <summary>المنشأة التي يُحاوَل فيها فكّ ذلك المِقبض — والصفّ فيها موجودٌ باسمٍ مطابق.</summary>
    public static TenantId MintedElsewhereTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000006"));

    /// <summary>منشأة إثبات «الطرف المُوقَف ليس مرشّحاً».</summary>
    public static TenantId InactiveTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000007"));

    /// <summary>منشأة إثبات ورقة السؤال — المنفذ الذي يُعيد أسماءً.</summary>
    public static TenantId SheetTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000008"));

    /// <summary>منشأة إثبات اتّفاق الطيّين على متن مقيس.</summary>
    public static TenantId FoldAgreementTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000009"));

    /// <summary>منشأة إثبات الطبقة الثانية — الجهة التي أُصدر فيها المِقبض.</summary>
    public static TenantId SecondLayerHereTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000a"));

    /// <summary>منشأة إثبات الطبقة الثانية — الجهة التي تحمل الاسم نفسه ولا يبلغها المِقبض.</summary>
    public static TenantId SecondLayerElsewhereTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000b"));

    /// <summary>منشأة إثبات «طول المِقبض ثابت».</summary>
    public static TenantId HandleLengthTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000c"));

    /// <summary>منشأة إثبات «مِقبضٌ عُبث به لا يُفكّ».</summary>
    public static TenantId TamperedHandleTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000d"));

    /// <summary>منشأة إثبات «مدّةٌ فوق السقف تُرفض».</summary>
    public static TenantId LifetimeCapTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000e"));

    /// <summary>منشأة إثبات «سجلٌّ غير مسجَّل يُرفض» — ولا صفّ يُزرع فيها إطلاقاً.</summary>
    public static TenantId UnknownRegisterTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-00000000000f"));

    /// <summary>منشأة إثبات «نصٌّ يطوى إلى فراغ يُرفض» — ولا صفّ يُزرع فيها إطلاقاً.</summary>
    public static TenantId EmptyTextTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000010"));

    /// <summary>منشأة إثبات «سقف الورقة يُفرض في المحوّل» — تُزرع فيها صفوفٌ فوق السقف.</summary>
    public static TenantId CeilingTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000011"));

    /// <summary>منشأة إثبات «الطرف الثالث لا يُختار» — يُزرع فيها الطرفان معاً.</summary>
    public static TenantId ThirdPartyTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000012"));

    /// <summary>منشأة إثبات «بعد السؤال يمرّ مِقبض» — <b>وحدها</b>، فلا يزرع فيها إثباتٌ آخر.</summary>
    public static TenantId ThirdPartyAnsweredTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000017"));

    /// <summary>منشأة إثبات «متجه الانحدار يبقى عميلاً» — صفٌّ واحد فيها.</summary>
    public static TenantId RegressionOneTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000013"));

    /// <summary>منشأة إثبات «الثمن الأمين» — صفّان متقاربان فيُسأل ولا يُرجَّح أحدهما.</summary>
    public static TenantId RegressionManyTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000014"));

    /// <summary>منشأة إثبات الهزائم الأربع — الطرفان المشروعان مزروعان فيها.</summary>
    public static TenantId DefeatsTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000015"));

    /// <summary>منشأة إثبات «الاسم المشروع فيه كلمةٌ ذاتُ دلالة لا يُقصّ».</summary>
    public static TenantId SignificantWordTenant { get; } = new(new Guid("100c0a5e-0000-4000-8000-000000000016"));

    /// <summary>وصف <c>sales.customer</c> كما ينشئه EF فعلاً — <b>ولا عمود شركة فيه</b>.</summary>
    public static NameRegisterTable CustomerRegister { get; } = new(
        registerKey: "customer",
        schema: "sales",
        table: "customer",
        idColumn: "Id",
        nameColumn: "NameAr",
        tenantColumn: "TenantId",
        activeColumn: "IsActive",
        subtitleColumn: "Code");

    /// <summary>الإعدادات المستعملة في كل الإثباتات.</summary>
    public static LookupOptions Options { get; } = new();

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;
    private static Exception? _failure;
    private static int _cleanupRegistered;

    /// <summary>ينشئ القاعدة، وينشر مخطّط المبيعات، ثم يربط سجلّ الأسماء.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (_ready)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
            {
                return;
            }

            // فشلٌ جزئي واحد يكفي: لا تُعاد التهيئة على قاعدة نصف مبنيّة.
            if (_failure is not null)
            {
                throw new InvalidOperationException(
                    "فشلت تهيئة بيئة إثبات البحث مرّة واحدة في هذه العملية، ولن يُعاد بناؤها.",
                    _failure);
            }

            try
            {
                RegisterCleanup();
                await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);

                await SalesSchemaDeployer.DeployAsync(
                    new SalesOptions { ConnectionString = ConnectionString, CompanyCurrency = "SAR" },
                    cancellationToken).ConfigureAwait(false);

                await NameRegisterSchema.DeployAsync(ConnectionString, cancellationToken).ConfigureAwait(false);
                await NameRegisterSchema.AttachAsync(ConnectionString, CustomerRegister, cancellationToken)
                    .ConfigureAwait(false);

                _ready = true;
            }
            catch (Exception failure)
            {
                _failure = failure;
                throw;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>يُنشئ محوّل السبر على العتبة المقيسة.</summary>
    public static PostgresNameRegister Register()
        => new(ConnectionString, CustomerRegister, Options.SimilarityThreshold);

    /// <summary>
    /// يُنشئ محوّل الجَرد — <b>كائنٌ آخر</b>، وسقفُه سقفُ الورقة المُعلَن.
    /// </summary>
    public static PostgresNameSheet Sheet()
        => new(ConnectionString, CustomerRegister, Options.SimilarityThreshold, Options.QuestionSheetCap);

    /// <summary>مُصدِر مقابض بمفتاحٍ ثابت داخل الإثبات — والمفتاح لا يُقرأ من بيئةٍ هنا.</summary>
    public static SignedLookupHandles Handles()
        => new(Encoding.UTF8.GetBytes("مفتاح إثباتٍ طوله أكثر من اثنتين وثلاثين بايتاً بلا شكّ"), Options, TimeProvider.System);

    /// <summary>
    /// <b>يزرع عميلاً بواجهة وحدة المبيعات نفسها، لا بـ<c>insert</c> يكتبه الإثبات.</b>
    /// <para>
    /// وذلك ليس ذوقاً: الصفّ الذي يُطابَق يجب أن يكون الصفّ الذي <b>تكتبه الوحدة المالكة</b>
    /// بكل ما تضعه فيه. وإثباتٌ يكتب صفّه بيده يُثبت أن الإثبات يعمل.
    /// </para>
    /// <para>
    /// والرمز يُشتقّ من معرّفٍ جديد في كل نداء، فلا يتصادم إثباتان على <c>uq_sales_customer_code</c>.
    /// </para>
    /// </summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="nameAr">الاسم العربي — وهو السجلّ، وهو ما يُطوى.</param>
    /// <param name="isActive">هل الطرف سارٍ؟ الوحدة تُنشئه سارياً، والإيقاف تعديلٌ بعده.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Guid> SeedCustomerAsync(
        TenantId tenant,
        string nameAr,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        using SalesRuntime runtime = new(
            new SalesOptions { ConnectionString = ConnectionString, CompanyCurrency = "SAR" },
            DefaultCostCenter.Instance);

        CustomerService customers = new(AlwaysEntitled.Instance, runtime);

        string code = "C-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];

        Result<CustomerView> created = await customers.CreateAsync(
            tenant,
            new UserId(Guid.CreateVersion7()),
            new CustomerDraft(code, new LocalizedName(nameAr, code), Money.Zero(new CurrencyCode("SAR")), 30),
            cancellationToken).ConfigureAwait(false);

        if (created.IsFailure)
        {
            throw new InvalidOperationException(
                "تعذّر زرع عميل الإثبات: " + string.Join(" · ", created.Errors.Select(static error => error.MessageAr)));
        }

        if (!isActive)
        {
            await using NpgsqlConnection connection = new(ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlCommand stop = new(
                "update sales.customer set \"IsActive\" = false where \"Id\" = @id", connection);
            stop.Parameters.AddWithValue("id", created.Value.Id);
            await stop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return created.Value.Id;
    }

    /// <summary>يقرأ قيمةً نصّية واحدة من القاعدة — لمقارنة الطيّين.</summary>
    /// <param name="sql">النصّ.</param>
    /// <param name="value">المعامل <c>@value</c>.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<string> ScalarAsync(string sql, string value, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("value", value);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (string)result!;
    }

    /// <summary>يفتح اتصالاً مباشراً — لإثباتات ترفع استثناءً من القاعدة نفسها.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection admin = new(Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        await SweepAbandonedAsync(admin, cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand create = new("create database \"" + Database + "\"", admin);
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يكنس قواعد تشغيلاتٍ <b>ثبت موتها</b> — ولا يمسّ قاعدةً لعمليةٍ حيّة.</summary>
    private static async Task SweepAbandonedAsync(NpgsqlConnection admin, CancellationToken cancellationToken)
    {
        List<string> abandoned = [];

        await using (NpgsqlCommand list = new(
            "select datname from pg_database where datname like @pattern", admin))
        {
            list.Parameters.AddWithValue("pattern", DatabaseStem + "\\_p%");

            await using NpgsqlDataReader reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string name = reader.GetString(0);
                if (OwnerProcessId(name) is int owner && !OwnerIsAlive(owner))
                {
                    abandoned.Add(name);
                }
            }
        }

        foreach (string name in abandoned)
        {
            try
            {
                await using NpgsqlCommand drop = new("drop database if exists \"" + name + "\" with (force)", admin);
                await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException)
            {
                // قاعدةٌ صار لها مستعمِل بين القراءة والحذف تُترك — الكنس تنظيفٌ لا واجب.
            }
        }
    }

    private static void RegisterCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) =>
        {
            try
            {
                using NpgsqlConnection admin = new(Maintenance);
                admin.Open();
                using NpgsqlCommand drop = new("drop database if exists \"" + Database + "\" with (force)", admin);
                drop.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // الخروج لا يُفشله تعذّرُ كنسٍ — والكنس التالي يلتقطها بشرطه: مالكٌ ثبت موته.
            }
        };
    }

    private static string ProcessScopedName(string stem)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        string name = stem + "_p" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "_" + token;

        // القصّ الصامت عند 63 بايتاً يجعل اسمين مختلفين قاعدةً واحدة — فلا يُقصّ شيء.
        int bytes = Encoding.UTF8.GetByteCount(name);
        return bytes <= MaxIdentifierBytes
            ? name
            : throw new InvalidOperationException(
                "اسم قاعدة الإثبات «" + name + "» طوله "
                + bytes.ToString(CultureInfo.InvariantCulture) + " بايتاً ويتجاوز الحدّ.");
    }

    private static int? OwnerProcessId(string database)
    {
        string head = DatabaseStem + "_p";
        if (!database.StartsWith(head, StringComparison.Ordinal))
        {
            return null;
        }

        string rest = database[head.Length..];
        int separator = rest.IndexOf('_', StringComparison.Ordinal);
        return separator > 0
            && int.TryParse(rest[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            ? pid
            : null;
    }

    private static bool OwnerIsAlive(int processId)
    {
        if (processId == Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}

/// <summary>منفِّذ استحقاق يسمح دائماً — الاستحقاق نفسه مُختبَر في <c>Babel.Core.Tests</c>.</summary>
internal sealed class AlwaysEntitled : IEntitlementEnforcer
{
    /// <summary>النسخة الوحيدة.</summary>
    public static AlwaysEntitled Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success());
}

/// <summary>
/// حالُّ مركز تكلفة يعيد الافتراضي — ولا يُقرأ في هذا المسار إطلاقاً: تسجيل عميل لا يُرحَّل.
/// </summary>
internal sealed class DefaultCostCenter : ICostCenterResolver
{
    /// <summary>النسخة الوحيدة.</summary>
    public static DefaultCostCenter Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<Result<string>> ResolveAsync(
        TenantId company,
        string? requested,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result<string>.Success(requested ?? "DEFAULT"));
}
