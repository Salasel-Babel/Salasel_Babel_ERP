using System.Globalization;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Npgsql;
using NpgsqlTypes;

namespace Babel.Core.Persistence;

/// <summary>
/// مخزن الاستخدام فوق PostgreSQL — <b>وهو ما يمنع تجاوز سقف الإنفاق بإعادة تشغيل</b>.
/// <para>
/// عدّادٌ في ذاكرة العملية يُصفَّر عند كل نشرة: فمستأجرٌ بلغ سقفه ينتظر إعادة إقلاع
/// ويعود من الصفر، وخادمان خلف موزّع يعدّان نصفين ولا أحد يجمعهما. والمحوران معاً —
/// الوحدة والمستخدم — <b>محورا تسعير</b>، فما يضيع منهما لا يُعاد بناؤه من شيء:
/// لا استعلام يستخرج ما لم يُكتب.
/// </para>
/// <para>
/// <b>ولماذا سجلٌّ مُلحَق لا صفُّ عدّاد — والقرار بعد قراءة <c>InMemoryUsageStore</c>:</b>
/// المخزن في الذاكرة ليس عدّاداً بنافذة زمنية. هو كيسا أحداثٍ
/// (<c>ConcurrentBag&lt;ModuleUsageEvent&gt;</c> و<c>ConcurrentBag&lt;UserActivityEvent&gt;</c>)
/// و<b>الجمع يقع عند القراءة</b> على (المستأجر × شهر الفوترة). فالنظير الدائم لكيس
/// أحداثٍ جدولُ أحداث، لا صفُّ عدّادٍ يُزاد بـ<c>update</c>. وثلاثةُ فوارق تجعل الفرق
/// جوهرياً لا أسلوبياً:
/// <list type="number">
///   <item>العدّاد يُزاد بـ<c>update</c>، فيحتاج دور التطبيق صلاحية <c>UPDATE</c> —
///         وهي الصلاحية نفسها التي تُعيد كتابة تاريخ الفوترة. والسجلّ المُلحَق يكتفي
///         بـ<c>INSERT</c>، فتُسحب <c>UPDATE</c> و<c>DELETE</c> كما في سجلّ التدقيق.</item>
///   <item>العدّاد يُسقط «مَن ومتى وأيّ عملية» — وهي بالضبط ما يجعل فاتورةً متنازَعاً
///         عليها قابلةً للتفصيل أمام العميل.</item>
///   <item>«المستخدم الفعّال» تعريفٌ تجاريّ <b>غير محسوم</b> (انظر <c>UserActivityEvent</c>)،
///         وحدُّ العدّاد يحسمه وقت الكتابة حسماً نهائياً. والسجلّ يُبقي كل تعريف قابلاً
///         للحساب بأثرٍ رجعي على شهورٍ مضت.</item>
/// </list>
/// وثمنُ ذلك نموٌّ في الصفوف، وهو ثمنٌ يُدفع بالتقسيم بالفترة لاحقاً — لا بحذف صفوف.
/// </para>
/// </summary>
internal sealed class PostgresUsageStore : IUsageStore, IUsageReader, IUsageMeter
{
    private readonly string _connectionString;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    public PostgresUsageStore(CoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.AppConnectionString;
    }

    /// <inheritdoc />
    public ValueTask RecordModuleUsageAsync(ModuleUsageEvent usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);
        return AppendModuleUsageAsync([usage], cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordUserActivityAsync(UserActivityEvent activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return AppendUserActivityAsync([activity], cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask AppendModuleUsageAsync(
        IReadOnlyList<ModuleUsageEvent> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Count == 0)
        {
            return;
        }

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**الدفعة جملةٌ واحدة بـ`unnest`** لا حلقةُ نداءات: الحدّ يَعِد بأن التخزين
        // «قد يصير دفعات أو طابوراً لاحقاً»، ودفعةٌ تُكتب في n رحلات ذهاب وإياب تجعل
        // انقطاعاً في المنتصف يترك نصف الدفعة مكتوباً — أي قياساً ناقصاً على فاتورة.
        await using NpgsqlCommand insert = new(
            """
            insert into core.module_usage (tenant_id, module, operation, actor_id, occurred_at, quantity)
            select * from unnest($1::uuid[], $2::integer[], $3::varchar[], $4::uuid[], $5::timestamptz[], $6::bigint[])
            """,
            connection);

        insert.Parameters.Add(Array(NpgsqlDbType.Uuid, [.. batch.Select(static e => e.Tenant.Value)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Integer, [.. batch.Select(static e => (int)e.Module)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Varchar, [.. batch.Select(static e => e.Operation)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Uuid, [.. batch.Select(static e => e.Actor.Value)]));
        insert.Parameters.Add(Array(NpgsqlDbType.TimestampTz, [.. batch.Select(static e => e.OccurredAt.ToUniversalTime())]));
        insert.Parameters.Add(Array(NpgsqlDbType.Bigint, [.. batch.Select(static e => e.Quantity)]));

        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AppendUserActivityAsync(
        IReadOnlyList<UserActivityEvent> batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Count == 0)
        {
            return;
        }

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand insert = new(
            """
            insert into core.user_activity (tenant_id, user_id, module, activity, occurred_at, entitlement_state)
            select * from unnest($1::uuid[], $2::uuid[], $3::integer[], $4::varchar[], $5::timestamptz[], $6::varchar[])
            """,
            connection);

        insert.Parameters.Add(Array(NpgsqlDbType.Uuid, [.. batch.Select(static e => e.Tenant.Value)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Uuid, [.. batch.Select(static e => e.User.Value)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Integer, [.. batch.Select(static e => (int)e.Module)]));
        insert.Parameters.Add(Array(NpgsqlDbType.Varchar, [.. batch.Select(static e => e.Activity)]));
        insert.Parameters.Add(Array(NpgsqlDbType.TimestampTz, [.. batch.Select(static e => e.OccurredAt.ToUniversalTime())]));
        insert.Parameters.Add(Array(NpgsqlDbType.Varchar, [.. batch.Select(static e => EntitlementStates.ToColumn(e.State))]));

        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<BabelModule, long>> GetModuleUsageAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default)
    {
        (DateTimeOffset from, DateTimeOffset until) = Window(period);

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**المدى نصفُ مفتوح ومحسوبٌ في .NET**، لا `date_trunc` ولا `extract` على العمود:
        // دالّةٌ على العمود تُلغي الفهرس وتجعل تبويب الشهر تابعاً لـ`TimeZone` الجلسة —
        // وشهرُ الفوترة مُعرَّف بتوقيت UTC (‏BillingPeriod.FromInstant).
        await using NpgsqlCommand command = new(
            """
            select module, sum(quantity)::bigint
            from core.module_usage
            where tenant_id = $1 and occurred_at >= $2 and occurred_at < $3
            group by module
            order by module
            """,
            connection);

        command.Parameters.Add(Uuid(tenant.Value));
        command.Parameters.Add(Instant(from));
        command.Parameters.Add(Instant(until));

        Dictionary<BabelModule, long> totals = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            totals[Module(reader.GetInt32(0))] = reader.GetInt64(1);
        }

        return totals;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<UserId>> GetActiveUsersAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default)
    {
        (DateTimeOffset from, DateTimeOffset until) = Window(period);

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**العدّ الافتراضي لم يتغيّر**: كلُّ من عمل شيئاً. وحالة الاستحقاق مُلتقَطة في
        // العمود ولا تدخل هذا الشرط — «هل يُعدّ من يقرأ فقط مستخدماً قابلاً للفوترة؟»
        // سؤالٌ تجاريّ جوابه للمالك، ووجودُ العمود هو ما يُبقيه قابلاً للحساب لاحقاً.
        await using NpgsqlCommand command = new(
            """
            select distinct user_id
            from core.user_activity
            where tenant_id = $1 and occurred_at >= $2 and occurred_at < $3
            """,
            connection);

        command.Parameters.Add(Uuid(tenant.Value));
        command.Parameters.Add(Instant(from));
        command.Parameters.Add(Instant(until));

        HashSet<UserId> users = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            users.Add(new UserId(reader.GetFieldValue<Guid>(0)));
        }

        return users;
    }

    /// <summary>مدى شهر الفوترة بتوقيت UTC — نصفُ مفتوح: <c>[البداية، بداية التالي)</c>.</summary>
    private static (DateTimeOffset From, DateTimeOffset Until) Window(BillingPeriod period)
    {
        DateTimeOffset from = new(period.Year, period.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (from, from.AddMonths(1));
    }

    /// <summary>
    /// يقرأ قيمة الوحدة — و<b>قيمةٌ لا يعرفها التعداد تُرفض ولا تُقرأ صامتةً</b>.
    /// <para>
    /// قيدُ المخطّط <c>module &gt;= 1</c> عمداً بلا حدٍّ أعلى (كي لا يرفض وحدةً جديدة)،
    /// فحارسُ المجموعة المغلقة هنا. وطبقةُ استمرارية تُرجع <c>(BabelModule)99</c> بصمت
    /// تكون قد بنت فاتورةً على وحدةٍ لا وجود لها.
    /// </para>
    /// </summary>
    private static BabelModule Module(int value) =>
        Enum.IsDefined(typeof(BabelModule), value)
            ? (BabelModule)value
            : throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"قياسُ استخدامٍ مخزَّن على وحدةٍ لا يعرفها التعداد: {value}."));

    private static NpgsqlParameter Uuid(Guid value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Uuid };

    private static NpgsqlParameter Instant(DateTimeOffset value) =>
        new() { Value = value.ToUniversalTime(), NpgsqlDbType = NpgsqlDbType.TimestampTz };

    private static NpgsqlParameter Array<T>(NpgsqlDbType element, T[] values) =>
        new() { Value = values, NpgsqlDbType = NpgsqlDbType.Array | element };
}
