using Babel.Core.Access;
using Babel.SharedKernel;
using Npgsql;
using NpgsqlTypes;

namespace Babel.Core.Persistence;

/// <summary>
/// دليل المصادقة فوق PostgreSQL — <b>وهو ما يجعل جلسةً تنجو من نشرٍ جديد</b>.
/// <para>
/// دليلٌ في ذاكرة العملية كان سيعني أن كل مستخدم يخرج عند كل إقلاع، وأن «أبطلتُ جلسة
/// هذا الموظّف» جملةٌ صحيحة على خادمٍ واحد من ثلاثة. وهو بعينه العطل الذي أُصلح حين
/// انتقل مخزن التأسيس من الذاكرة إلى قاعدة البيانات.
/// </para>
/// <para>
/// <b>وثلاث عمليات هنا ذرّية بالمعاملة و<c>FOR UPDATE</c> لا بانضباط المستدعي:</b>
/// استهلاك الانتساب، وتدوير التجديد، ومنح العضوية. وتنفيذٌ يقرأ ثم يكتب في ندائين
/// يجعل طلبين متزامنين باعتماد تجديد واحد <b>يفوزان معاً</b> — أي يجعل كشف السرقة
/// يعتمد على التوقيت.
/// </para>
/// <para>
/// <b>ولا نصّ اعتماد يعبر هذا الملف:</b> كل وسيط هنا بصمة سداسية عشرية، والبحث بالمفتاح
/// عليها لا مقارنةَ سرٍّ — والفرق مكتوب في <c>AccessCredentials.DigestsMatch</c>.
/// </para>
/// </summary>
internal sealed class PostgresAccessDirectory : IAccessDirectory
{
    private readonly string _connectionString;

    /// <summary>ينشئ الدليل.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    public PostgresAccessDirectory(CoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.AppConnectionString;
    }

    /// <inheritdoc />
    public async Task<bool> TryGrantAsync(
        TenantId tenant,
        Membership membership,
        UserId grantedBy,
        string enrolmentDigest,
        DateTimeOffset enrolmentExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand insert = new(
            """
            insert into core.access_membership
                (company_id, user_id, tenant_id, role, display_name_ar, granted_at, granted_by)
            values ($1, $2, $3, $4, $5, $6, $7)
            on conflict (company_id, user_id) do nothing
            """,
            connection,
            transaction))
        {
            insert.Parameters.Add(Uuid(membership.Company));
            insert.Parameters.Add(Uuid(membership.User.Value));
            insert.Parameters.Add(Uuid(tenant.Value));
            insert.Parameters.Add(Text(MembershipRoles.ToColumn(membership.Role)));
            insert.Parameters.Add(Text(membership.DisplayNameAr));
            insert.Parameters.Add(Instant(membership.GrantedAt));
            insert.Parameters.Add(Uuid(grantedBy.Value));

            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        await using (NpgsqlCommand enrol = new(
            """
            insert into core.access_enrolment (digest, tenant_id, user_id, issued_at, expires_at, consumed_at)
            values ($1, $2, $3, $4, $5, null)
            """,
            connection,
            transaction))
        {
            enrol.Parameters.Add(Text(enrolmentDigest));
            enrol.Parameters.Add(Uuid(tenant.Value));
            enrol.Parameters.Add(Uuid(membership.User.Value));
            enrol.Parameters.Add(Instant(membership.GrantedAt));
            enrol.Parameters.Add(Instant(enrolmentExpiresAt));
            await enrol.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<Membership?> FindMembershipAsync(Guid company, UserId user, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            select company_id, user_id, role, display_name_ar, granted_at
            from core.access_membership
            where company_id = $1 and user_id = $2
            """,
            connection);

        command.Parameters.Add(Uuid(company));
        command.Parameters.Add(Uuid(user.Value));

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Membership>> ListMembershipsAsync(Guid company, CancellationToken cancellationToken = default) =>
        ListAsync(
            """
            select company_id, user_id, role, display_name_ar, granted_at
            from core.access_membership
            where company_id = $1
            order by user_id::text
            """,
            [Uuid(company)],
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Membership>> MembershipsOfAsync(TenantId tenant, UserId user, CancellationToken cancellationToken = default) =>
        ListAsync(
            """
            select company_id, user_id, role, display_name_ar, granted_at
            from core.access_membership
            where tenant_id = $1 and user_id = $2
            order by company_id::text
            """,
            [Uuid(tenant.Value), Uuid(user.Value)],
            cancellationToken);

    /// <inheritdoc />
    public async Task<EnrolmentClaim> ConsumeEnrolmentAsync(
        string enrolmentDigest,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        TenantId tenant;
        UserId user;
        DateTimeOffset expiresAt;
        bool consumed;

        await using (NpgsqlCommand select = new(
            "select tenant_id, user_id, expires_at, consumed_at from core.access_enrolment where digest = $1 for update",
            connection,
            transaction))
        {
            select.Parameters.Add(Text(enrolmentDigest));
            await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new EnrolmentClaim(EnrolmentOutcome.Rejected, TenantId.None, UserId.None);
            }

            tenant = new TenantId(reader.GetFieldValue<Guid>(0));
            user = new UserId(reader.GetFieldValue<Guid>(1));
            expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
            consumed = !reader.IsDBNull(3);
        }

        if (consumed)
        {
            return new EnrolmentClaim(EnrolmentOutcome.AlreadyConsumed, tenant, user);
        }

        if (now >= expiresAt)
        {
            return new EnrolmentClaim(EnrolmentOutcome.Expired, tenant, user);
        }

        await using (NpgsqlCommand claim = new(
            "update core.access_enrolment set consumed_at = $2 where digest = $1",
            connection,
            transaction))
        {
            claim.Parameters.Add(Text(enrolmentDigest));
            claim.Parameters.Add(Instant(now));
            await claim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new EnrolmentClaim(EnrolmentOutcome.Accepted, tenant, user);
    }

    /// <inheritdoc />
    public async Task OpenSessionAsync(
        Guid sessionId,
        TenantId tenant,
        UserId user,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string refreshDigest,
        DateTimeOffset refreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand open = new(
            """
            insert into core.access_session
                (session_id, tenant_id, user_id, opened_at, generation, revoked_at, revoked_reason)
            values ($1, $2, $3, $4, 1, null, '')
            """,
            connection,
            transaction))
        {
            open.Parameters.Add(Uuid(sessionId));
            open.Parameters.Add(Uuid(tenant.Value));
            open.Parameters.Add(Uuid(user.Value));
            open.Parameters.Add(Instant(now));
            await open.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await IssueAsync(connection, transaction, sessionId, 1, accessDigest, accessExpiresAt, refreshDigest, refreshExpiresAt, now, cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RotationResult> RotateAsync(
        string refreshDigest,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string nextRefreshDigest,
        DateTimeOffset nextRefreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        Guid sessionId;
        TenantId tenant;
        UserId user;
        string kind;
        DateTimeOffset expiresAt;
        bool consumed;
        bool revoked;
        int generation;

        await using (NpgsqlCommand select = new(
            """
            select c.session_id, c.kind, c.expires_at, c.consumed_at,
                   s.tenant_id, s.user_id, s.revoked_at, s.generation
            from core.access_credential c
            join core.access_session s on s.session_id = c.session_id
            where c.digest = $1
            for update of c, s
            """,
            connection,
            transaction))
        {
            select.Parameters.Add(Text(refreshDigest));
            await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Nothing(RotationOutcome.Rejected);
            }

            sessionId = reader.GetFieldValue<Guid>(0);
            kind = reader.GetString(1);
            expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
            consumed = !reader.IsDBNull(3);
            tenant = new TenantId(reader.GetFieldValue<Guid>(4));
            user = new UserId(reader.GetFieldValue<Guid>(5));
            revoked = !reader.IsDBNull(6);
            generation = reader.GetInt32(7);
        }

        // اعتمادٌ فاعل قُدِّم في موضع اعتماد التجديد لا يُميَّز عن اعتماد مختلَق: تمييزه
        // كان سيجعل السطح يقول لمن يجرّب «هذا اعتماد موجود، ولكن نوعه غير المطلوب».
        if (!string.Equals(kind, CredentialKinds.Refresh, StringComparison.Ordinal))
        {
            return Nothing(RotationOutcome.Rejected);
        }

        // ‏**إعادة الاستعمال أولاً** — قبل الانقضاء وقبل الإبطال. والجواب إسقاط العائلة.
        if (consumed)
        {
            await using (NpgsqlCommand revoke = new(
                """
                update core.access_session
                set revoked_at = $2, revoked_reason = $3
                where session_id = $1 and revoked_at is null
                """,
                connection,
                transaction))
            {
                revoke.Parameters.Add(Uuid(sessionId));
                revoke.Parameters.Add(Instant(now));
                revoke.Parameters.Add(Text(RevocationReasons.RefreshReplayed));
                await revoke.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new RotationResult(RotationOutcome.Replayed, sessionId, tenant, user, 0);
        }

        if (revoked)
        {
            return new RotationResult(RotationOutcome.SessionRevoked, sessionId, tenant, user, 0);
        }

        if (now >= expiresAt)
        {
            return new RotationResult(RotationOutcome.Expired, sessionId, tenant, user, 0);
        }

        int next = generation + 1;

        await using (NpgsqlCommand consume = new(
            "update core.access_credential set consumed_at = $2 where digest = $1",
            connection,
            transaction))
        {
            consume.Parameters.Add(Text(refreshDigest));
            consume.Parameters.Add(Instant(now));
            await consume.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (NpgsqlCommand advance = new(
            "update core.access_session set generation = $2 where session_id = $1",
            connection,
            transaction))
        {
            advance.Parameters.Add(Uuid(sessionId));
            advance.Parameters.Add(Integer(next));
            await advance.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await IssueAsync(connection, transaction, sessionId, next, accessDigest, accessExpiresAt, nextRefreshDigest, nextRefreshExpiresAt, now, cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new RotationResult(RotationOutcome.Rotated, sessionId, tenant, user, next);
    }

    /// <inheritdoc />
    public async Task<AccessLookup> LookupAccessAsync(
        string accessDigest,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // ‏**الإبطال يُقرأ في هذا الاستعلام نفسه** — لا في مهمّة تنظيف ولا عند الانقضاء.
        // فالجملة «سُحب هذا الاعتماد» تصير صحيحة عند الطلب التالي مباشرة.
        await using NpgsqlCommand command = new(
            """
            select c.session_id, c.kind, c.expires_at, s.tenant_id, s.user_id, s.revoked_at
            from core.access_credential c
            join core.access_session s on s.session_id = c.session_id
            where c.digest = $1
            """,
            connection);

        command.Parameters.Add(Text(accessDigest));

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AccessLookup(AccessOutcome.Rejected, Guid.Empty, TenantId.None, UserId.None, default);
        }

        Guid sessionId = reader.GetFieldValue<Guid>(0);
        string kind = reader.GetString(1);
        DateTimeOffset expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
        TenantId tenant = new(reader.GetFieldValue<Guid>(3));
        UserId user = new(reader.GetFieldValue<Guid>(4));
        bool revoked = !reader.IsDBNull(5);

        if (!string.Equals(kind, CredentialKinds.Access, StringComparison.Ordinal))
        {
            // اعتماد تجديد مُقدَّم في ترويسة التصريح: يُرفض كأي اعتماد غير مقبول. وقبولُه
            // كان سيجعل الاعتماد طويل العمر اعتمادَ استعمال، فيسقط معنى التدوير كلّه.
            return new AccessLookup(AccessOutcome.Rejected, Guid.Empty, TenantId.None, UserId.None, default);
        }

        AccessOutcome outcome = revoked
            ? AccessOutcome.Revoked
            : now >= expiresAt ? AccessOutcome.Expired : AccessOutcome.Live;

        return new AccessLookup(outcome, sessionId, tenant, user, expiresAt);
    }

    /// <inheritdoc />
    public async Task<SessionRevocation?> RevokeSessionAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            with claimed as (
                update core.access_session
                set revoked_at = $2, revoked_reason = $3
                where session_id = $1 and revoked_at is null
                returning revoked_at, revoked_reason
            )
            select revoked_at, revoked_reason from claimed
            union all
            select revoked_at, revoked_reason from core.access_session
            where session_id = $1 and revoked_at is not null
            limit 1
            """,
            connection);

        command.Parameters.Add(Uuid(sessionId));
        command.Parameters.Add(Instant(now));
        command.Parameters.Add(Text(reason));

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new SessionRevocation(sessionId, reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1))
            : null;
    }

    private static RotationResult Nothing(RotationOutcome outcome) =>
        new(outcome, Guid.Empty, TenantId.None, UserId.None, 0);

    private static Membership Read(NpgsqlDataReader reader) => new(
        reader.GetFieldValue<Guid>(0),
        new UserId(reader.GetFieldValue<Guid>(1)),
        MembershipRoles.FromColumn(reader.GetString(2)),
        reader.GetString(3),
        reader.GetFieldValue<DateTimeOffset>(4));

    private static async Task IssueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        int generation,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string refreshDigest,
        DateTimeOffset refreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand issue = new(
            """
            insert into core.access_credential (digest, session_id, kind, generation, issued_at, expires_at, consumed_at)
            values ($1, $3, 'access', $4, $5, $6, null),
                   ($2, $3, 'refresh', $4, $5, $7, null)
            """,
            connection,
            transaction);

        issue.Parameters.Add(Text(accessDigest));
        issue.Parameters.Add(Text(refreshDigest));
        issue.Parameters.Add(Uuid(sessionId));
        issue.Parameters.Add(Integer(generation));
        issue.Parameters.Add(Instant(now));
        issue.Parameters.Add(Instant(accessExpiresAt));
        issue.Parameters.Add(Instant(refreshExpiresAt));

        await issue.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Membership>> ListAsync(
        string sql,
        NpgsqlParameter[] parameters,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, connection);

        foreach (NpgsqlParameter parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        List<Membership> memberships = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            memberships.Add(Read(reader));
        }

        return memberships;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static NpgsqlParameter Uuid(Guid value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Uuid };

    private static NpgsqlParameter Text(string value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Varchar };

    private static NpgsqlParameter Integer(int value) => new() { Value = value, NpgsqlDbType = NpgsqlDbType.Integer };

    private static NpgsqlParameter Instant(DateTimeOffset value) =>
        new() { Value = value.ToUniversalTime(), NpgsqlDbType = NpgsqlDbType.TimestampTz };
}
