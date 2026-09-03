using Babel.Core.Audit;
using Babel.SharedKernel;
using Npgsql;
using NpgsqlTypes;

namespace Babel.Core.Persistence;

/// <summary>
/// سجلّ التدقيق فوق PostgreSQL — <b>وهو ما يجعل «من فعل ماذا ومتى» ينجو من نشرة</b>.
/// <para>
/// سجلٌّ في ذاكرة العملية كان يعني أن كلّ نشرة تمحو الأثر كلّه في <b>نظامٍ محاسبي</b>،
/// وأن خادمين خلف موزّع يريان سجلّين مختلفين، وأن سؤال «من غيّر استحقاق هذا المستأجر
/// الشهر الماضي؟» لا جواب له أصلاً. وصنفُ <c>InMemoryAuditLog</c> يقولها عن نفسه:
/// «المخزن الدائم يأتي في موجة الاستمرارية». هذا هو.
/// </para>
/// <para>
/// <b>والإلحاق هنا مفروضٌ بالبناء لا بالنيّة:</b> لا دالّة تعديل ولا دالّة حذف على
/// <see cref="IAuditLog"/> — وذلك <b>غيابُ بابٍ في الشجرة وحدها</b>. أمّا الصفّ فهو في
/// قاعدة بيانات ولها أبوابٌ أخرى: سكربت صيانة، أداة إدارة، تصحيحٌ يدوي في الثانية
/// صباحاً. فالحصانة في موضعين خارج هذا الملفّ تماماً — <c>CoreGrants.sql</c> يمنح دور
/// التطبيق <c>SELECT</c> و<c>INSERT</c> فقط، و<c>CoreAppendOnlyTriggers.sql</c> يرفض
/// <c>UPDATE</c> و<c>DELETE</c> و<c>TRUNCATE</c> <b>ولو كان الفاعل هو المالك</b>
/// (ADR-0002 · ADR-0003).
/// </para>
/// <para>
/// <b>ونطاق المستأجر شرطٌ في الاستعلام لا ترشيحٌ بعده:</b> قراءةٌ تجلب الجدول ثم ترشّح
/// في الذاكرة تكون قد حمّلت أثر مستأجرٍ آخر إلى عملية تخدم هذا — والفرق يظهر يوم يُنسى
/// سطرُ الترشيح، لا يوم يُكتب.
/// </para>
/// </summary>
internal sealed class PostgresAuditLog : IAuditLog
{
    private readonly string _connectionString;

    /// <summary>ينشئ السجلّ.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    public PostgresAuditLog(CoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.AppConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand insert = new(
            """
            insert into core.audit_entry (tenant_id, actor_id, occurred_at, action, subject, details)
            values ($1, $2, $3, $4, $5, $6)
            """,
            connection);

        insert.Parameters.Add(Uuid(entry.Tenant.Value));
        insert.Parameters.Add(Uuid(entry.Actor.Value));
        insert.Parameters.Add(Instant(entry.OccurredAt));
        insert.Parameters.Add(Varchar(entry.Action));
        insert.Parameters.Add(Text(entry.Subject));
        insert.Parameters.Add(NullableText(entry.Details));

        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuditEntry>> ReadAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**الترتيب باللحظة ثم بالتسلسل.** قيدان في الميكروثانية نفسها ممكنان، وترتيبٌ
        // على اللحظة وحدها كان سيجعل ترتيبهما ما يشاء المُخطِّط — أي يجعل قراءتين
        // متتاليتين تعطيان سردين مختلفين للحدث نفسه.
        await using NpgsqlCommand command = new(
            """
            select tenant_id, actor_id, occurred_at, action, subject, details
            from core.audit_entry
            where tenant_id = $1
            order by occurred_at, sequence_no
            """,
            connection);

        command.Parameters.Add(Uuid(tenant.Value));

        List<AuditEntry> entries = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new AuditEntry(
                new TenantId(reader.GetFieldValue<Guid>(0)),
                new UserId(reader.GetFieldValue<Guid>(1)),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return entries;
    }

    private static NpgsqlParameter Uuid(Guid value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Uuid };

    private static NpgsqlParameter Varchar(string value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Varchar };

    private static NpgsqlParameter Text(string value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Text };

    private static NpgsqlParameter NullableText(string? value) =>
        new() { Value = value is null ? DBNull.Value : value, NpgsqlDbType = NpgsqlDbType.Text };

    private static NpgsqlParameter Instant(DateTimeOffset value) =>
        new() { Value = value.ToUniversalTime(), NpgsqlDbType = NpgsqlDbType.TimestampTz };
}
