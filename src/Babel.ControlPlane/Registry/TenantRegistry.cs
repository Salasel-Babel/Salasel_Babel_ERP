using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Registry;

/// <summary>
/// سجل المستأجرين. كل كتابة هنا <c>INSERT … ON CONFLICT DO UPDATE</c> مع
/// تأكيد عدد الصفوف — لا <c>UPDATE</c> مجرّد على صفّ قد لا يكون موجوداً
/// (فخ-09)، ولو بدا «موجوداً بالتأكيد» في هذا المسار تحديداً.
/// </summary>
public sealed class TenantRegistry(ControlPlaneOptions options)
{
    /// <summary>إعدادات مستوى التحكّم.</summary>
    public ControlPlaneOptions Options { get; } = options;

    private const string Columns = """
        tenant_id, tenant_code, name_ar, name_en, status, isolation_model, residency,
        host, port, database_name, schema_version, created_at, activated_at,
        archived_at, archive_reason, archive_actor
        """;

    private static TenantRecord Map(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
        Enum.Parse<TenantStatus>(r.GetString(4)),
        r.GetString(5) == "shared_schema" ? IsolationModel.SharedSchema : IsolationModel.DatabasePerTenant,
        r.GetString(6) == "customer" ? Residency.Customer : Residency.Provider,
        r.GetString(7), r.GetInt32(8), r.GetString(9), r.GetInt32(10),
        r.GetFieldValue<DateTimeOffset>(11),
        r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12),
        r.IsDBNull(13) ? null : r.GetFieldValue<DateTimeOffset>(13),
        r.IsDBNull(14) ? null : r.GetString(14),
        r.IsDBNull(15) ? null : r.GetString(15));

    private static string IsoText(IsolationModel m) =>
        m == IsolationModel.SharedSchema ? "shared_schema" : "database_per_tenant";

    /// <summary>
    /// يُسجّل مستأجراً في حالة <c>Provisioning</c>، أو يُعيد الموجود بلا تغيير.
    /// مُحكَم: النداء الثاني بنفس الرمز لا يُنشئ صفّاً ثانياً ولا يدهس حالة أحدث.
    /// </summary>
    public async Task<TenantRecord> RegisterAsync(NpgsqlConnection c, Guid tenantId,
        string tenantCode, BilingualName name, IsolationModel isolation = IsolationModel.DatabasePerTenant,
        Residency residency = Residency.Provider, NpgsqlTransaction? tx = null,
        CancellationToken ct = default)
    {
        Db.Ident(tenantCode);
        var dbName = Options.TenantDatabaseName(tenantCode);
        Db.Ident(dbName);

        await Db.WriteIdempotentAsync(c, $"""
            insert into control.tenant
                ({Columns})
            values (@id, @code, @ar, @en, 'Provisioning', @iso, @res,
                    @host, @port, @db, 0, @now, null, null, null, null)
            on conflict (tenant_code) do nothing
            """, p =>
            {
                p.Add(Db.P("id", tenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("code", tenantCode);
                p.AddWithValue("ar", name.Ar);
                p.AddWithValue("en", name.En);
                p.AddWithValue("iso", IsoText(isolation));
                p.AddWithValue("res", residency == Residency.Customer ? "customer" : "provider");
                p.AddWithValue("host", Options.AdminHost);
                p.AddWithValue("port", Options.AdminPort);
                p.AddWithValue("db", dbName);
                p.AddWithValue("now", Canon.Now());
            }, tx, ct);

        return await RequireByCodeAsync(c, tenantCode, tx, ct);
    }

    /// <summary>يبحث عن مستأجر برمزه على اتصال قائم.</summary>
    /// <param name="c">اتصال بقاعدة التحكّم.</param>
    /// <param name="tenantCode">رمز المستأجر.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الصفّ، أو <c>null</c> إن لم يوجد.</returns>
    public static async Task<TenantRecord?> FindByCodeAsync(NpgsqlConnection c, string tenantCode,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        var rows = await Db.QueryAsync(c,
            $"select {Columns} from control.tenant where tenant_code = @code", Map,
            p => p.AddWithValue("code", tenantCode), tx, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>مثل <c>FindByCodeAsync</c> لكنه يرمي بدل أن يُرجِع <c>null</c>.</summary>
    /// <param name="c">اتصال بقاعدة التحكّم.</param>
    /// <param name="tenantCode">رمز المستأجر.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الصفّ.</returns>
    /// <exception cref="TenantNotFoundException">لا مستأجر بهذا الرمز.</exception>
    public static async Task<TenantRecord> RequireByCodeAsync(NpgsqlConnection c, string tenantCode,
        NpgsqlTransaction? tx = null, CancellationToken ct = default) =>
        await FindByCodeAsync(c, tenantCode, tx, ct) ?? throw new TenantNotFoundException(tenantCode);

    /// <summary>يبحث عن مستأجر برمزه على اتصال جديد من إعدادات هذا السجل.</summary>
    /// <param name="tenantCode">رمز المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الصفّ، أو <c>null</c> إن لم يوجد.</returns>
    public async Task<TenantRecord?> FindByCodeAsync(string tenantCode, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        return await FindByCodeAsync(c, tenantCode, null, ct);
    }

    /// <summary>
    /// كل المستأجرين القابلين للوصول، <b>مرتَّبين بالرمز</b>. الترتيب ليس تجميلاً:
    /// أي عملية أسطولية تلمس صفوفاً كثيرة تأخذ أقفالها بترتيب كلّي ثابت (فخ-10).
    /// </summary>
    public async Task<List<TenantRecord>> ListAsync(TenantStatus? status = null,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        var sql = status is null
            ? $"select {Columns} from control.tenant order by tenant_code asc"
            : $"select {Columns} from control.tenant where status = @s order by tenant_code asc";
        return await Db.QueryAsync(c, sql, Map,
            p => { if (status is not null) p.AddWithValue("s", status.Value.ToString()); }, null, ct);
    }

    /// <summary>
    /// ينقل حالة المستأجر. الشرط <c>where tenant_id = @id</c> وحده لا يكفي:
    /// نؤكّد صفّاً واحداً بالضبط، فالمستأجر المحذوف أو المُعاد ترميزه
    /// يجب أن يُفشِل النقلة لا أن يمرّ بصمت.
    /// </summary>
    public static async Task SetStatusAsync(NpgsqlConnection c, Guid tenantId, TenantStatus status,
        DateTimeOffset? activatedAt = null, NpgsqlTransaction? tx = null,
        CancellationToken ct = default)
    {
        await Db.WriteAsync(c, """
            update control.tenant
               set status = @s,
                   activated_at = coalesce(@act, activated_at)
             where tenant_id = @id
            """, 1, p =>
            {
                p.AddWithValue("s", status.ToString());
                p.Add(Db.P("act", activatedAt, NpgsqlDbType.TimestampTz));
                p.Add(Db.P("id", tenantId, NpgsqlDbType.Uuid));
            }, tx, ct);
    }

    /// <summary>يُحدّث إصدار المخطط المُسجَّل، ويؤكّد صفّاً واحداً بالضبط.</summary>
    /// <param name="c">اتصال بقاعدة التحكّم.</param>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="version">الإصدار الجديد.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    public static async Task SetSchemaVersionAsync(NpgsqlConnection c, Guid tenantId, int version,
        NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await Db.WriteAsync(c,
            "update control.tenant set schema_version = @v where tenant_id = @id", 1,
            p => { p.AddWithValue("v", version); p.Add(Db.P("id", tenantId, NpgsqlDbType.Uuid)); },
            tx, ct);
    }

    /// <summary>يُعلّم المستأجر مؤرشفاً بسببه وفاعله. <b>لا حذف صفّ.</b></summary>
    /// <param name="c">اتصال بقاعدة التحكّم.</param>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="actor">من نفّذ الأرشفة.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="tx">المعاملة إن وُجدت.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    public static async Task MarkArchivedAsync(NpgsqlConnection c, Guid tenantId, string actor,
        string reasonAr, NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await Db.WriteAsync(c, """
            update control.tenant
               set status = 'Archived', archived_at = @t,
                   archive_reason = @r, archive_actor = @a
             where tenant_id = @id
            """, 1, p =>
            {
                p.AddWithValue("t", Canon.Now());
                p.AddWithValue("r", reasonAr);
                p.AddWithValue("a", actor);
                p.Add(Db.P("id", tenantId, NpgsqlDbType.Uuid));
            }, tx, ct);
    }
}
