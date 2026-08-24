using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Registry;

/// <summary>
/// مخطط قاعدة التحكّم. قاعدة واحدة تحمل: سجل المستأجرين، ودفتر التزويد،
/// وحالة الترحيل الأسطولي، والخطط والاستحقاقات، وسجل التدقيق، والقياس،
/// وسِرد العمليات.
///
/// <para><b>ما ليس فيها:</b> ولا صفّ بيانات محاسبية لمستأجر. حدّ قاعدة البيانات
/// هو الحدّ الذي لا يعبره شرط <c>WHERE</c> منسيّ (ADR-0009 §9.2).</para>
/// </summary>
public static class ControlSchema
{
    /// <summary>إصدار مخطط قاعدة التحكّم نفسه — منفصل عن إصدار مخطط المستأجر.</summary>
    public const int Version = 1;

    /// <summary>تعريف مخطط قاعدة التحكّم كاملاً. مصدر واحد، يُطبَّق مُحكَماً.</summary>
    public const string Ddl = """
    create schema if not exists control;

    -- =====================================================================
    -- 1 · سجل المستأجرين
    -- =====================================================================
    create table if not exists control.tenant (
        tenant_id        uuid        primary key,
        tenant_code      text        not null unique,
        name_ar          text        not null,
        name_en          text        not null,
        status           text        not null,
        -- بذرة ADR-0009: نموذج العزل عمود، لا افتراض مطبوع في الشيفرة.
        isolation_model  text        not null default 'database_per_tenant',
        -- بذرة ADR-0010: الاستضافة لدى العميل مؤجَّلة، لا مستحيلة.
        residency        text        not null default 'provider',
        host             text        not null,
        port             int         not null,
        database_name    text        not null,
        schema_version   int         not null default 0,
        created_at       timestamptz not null,
        activated_at     timestamptz,
        archived_at      timestamptz,
        archive_reason   text,
        archive_actor    text,
        constraint uq_tenant_db unique (host, port, database_name),
        constraint ck_tenant_status check
            (status in ('Provisioning','Active','Suspended','Archived')),
        constraint ck_tenant_isolation check
            (isolation_model in ('database_per_tenant','shared_schema')),
        constraint ck_tenant_residency check
            (residency in ('provider','customer')),
        -- الأرشفة حالة مكتملة أو غير موجودة: لا مستأجر «مؤرشف بلا تاريخ ولا سبب»
        constraint ck_tenant_archive_complete check
            (status <> 'Archived'
             or (archived_at is not null and archive_reason is not null and archive_actor is not null))
    );

    -- =====================================================================
    -- 2 · دفتر التزويد — الإحكام ليس نيّة بل جدول
    -- =====================================================================
    create table if not exists control.provisioning_run (
        run_id           uuid        primary key,
        idempotency_key  text        not null unique,   -- يورّده النداء (فخ-13)
        tenant_id        uuid        not null,
        tenant_code      text        not null,
        requested_by     text        not null,
        requested_at     timestamptz not null,
        completed_at     timestamptz,
        outcome          text,
        constraint ck_run_outcome check (outcome is null or outcome in ('Completed','Failed'))
    );

    create table if not exists control.provisioning_step (
        run_id       uuid        not null references control.provisioning_run (run_id),
        step_ordinal int         not null,
        step_name    text        not null,
        state        text        not null,
        attempts     int         not null default 0,
        started_at   timestamptz not null,
        finished_at  timestamptz,
        detail       jsonb       not null default '{}'::jsonb,
        primary key (run_id, step_name),
        constraint ck_step_state check (state in ('Started','Completed','Failed'))
    );
    create index if not exists ix_step_run on control.provisioning_step (run_id, step_ordinal);

    -- =====================================================================
    -- 3 · الترحيل الأسطولي
    -- =====================================================================
    create table if not exists control.fleet_migration (
        migration_id   uuid        primary key,
        -- مفتاح إحكام يورّده النداء: إعادة التخطيط بنفس المفتاح تستأنف الخطة
        -- نفسها ولا تُنشئ ثانية (فخ-13 — الإحكام بمفتاح، لا بمقارنة تسلسل).
        plan_key       text        not null unique,
        target_version int         not null,
        name_ar        text        not null,
        name_en        text        not null,
        created_at     timestamptz not null,
        created_by     text        not null
    );

    create table if not exists control.fleet_migration_target (
        migration_id  uuid        not null references control.fleet_migration (migration_id),
        tenant_id     uuid        not null,
        tenant_code   text        not null,
        database_name text        not null,
        state         text        not null,
        from_version  int,
        to_version    int,
        attempts      int         not null default 0,
        lease_owner   text,
        lease_until   timestamptz,
        started_at    timestamptz,
        finished_at   timestamptz,
        duration_ms   int,
        last_error    text,
        primary key (migration_id, tenant_id),
        constraint ck_target_state check
            (state in ('Pending','Leased','Done','Failed','Skipped'))
    );
    create index if not exists ix_target_state
        on control.fleet_migration_target (migration_id, state, tenant_id);

    -- =====================================================================
    -- 4 · الوحدات والخطط والاستحقاق
    -- =====================================================================
    create table if not exists control.module (
        module_code   text        primary key,
        name_ar       text        not null,
        name_en       text        not null,
        posts_journal boolean     not null,      -- هل تُرحّل قيوداً؟ (ADR-0014 «ما لا ينقضه»)
        sort_order    int         not null
    );

    create table if not exists control.module_dependency (
        module_code    text not null references control.module (module_code),
        depends_on     text not null references control.module (module_code),
        primary key (module_code, depends_on),
        constraint ck_dep_not_self check (module_code <> depends_on)
    );

    create table if not exists control.plan (
        plan_code        text          primary key,
        name_ar          text          not null,
        name_en          text          not null,
        monthly_price    numeric(19,4) not null,
        per_user_price   numeric(19,4) not null,
        currency         text          not null default 'SAR',
        included_users   int           not null default 0,
        constraint ck_plan_prices check (monthly_price >= 0 and per_user_price >= 0)
    );

    create table if not exists control.plan_module (
        plan_code   text not null references control.plan (plan_code),
        module_code text not null references control.module (module_code),
        primary key (plan_code, module_code)
    );

    create table if not exists control.subscription (
        subscription_id uuid        primary key,
        tenant_id       uuid        not null references control.tenant (tenant_id),
        plan_code       text        not null references control.plan (plan_code),
        started_on      date        not null,
        ends_on         date,
        state           text        not null,
        constraint ck_sub_state check (state in ('Active','Lapsed','Cancelled'))
    );
    create index if not exists ix_sub_tenant on control.subscription (tenant_id, started_on);

    -- الحالات الثلاث. لا حالة رابعة، ولا NULL.
    create table if not exists control.tenant_module_entitlement (
        tenant_id      uuid        not null references control.tenant (tenant_id),
        module_code    text        not null references control.module (module_code),
        state          text        not null,
        effective_from timestamptz not null,
        updated_at     timestamptz not null,
        primary key (tenant_id, module_code),
        constraint ck_ent_state check (state in ('NotEntitled','Entitled','ReadOnly'))
    );

    -- كل تغيير استحقاق: من، ومتى، وبأي سند. لا تغيير بلا سند.
    create table if not exists control.entitlement_audit (
        audit_id     bigint      generated always as identity primary key,
        tenant_id    uuid        not null,
        module_code  text        not null,
        old_state    text,
        new_state    text        not null,
        actor        text        not null,
        authority    text        not null,
        reason_ar    text        not null,
        occurred_at  timestamptz not null,
        constraint ck_audit_authority check (length(btrim(authority)) > 0)
    );
    create index if not exists ix_ent_audit on control.entitlement_audit (tenant_id, occurred_at);

    -- =====================================================================
    -- 5 · أرشفة الوحدات — لا «إلغاء تركيب» (ADR-0014)
    -- =====================================================================
    create table if not exists control.module_archive_request (
        request_id     uuid        primary key,
        tenant_id      uuid        not null references control.tenant (tenant_id),
        module_code    text        not null references control.module (module_code),
        requested_by   text        not null,
        requested_at   timestamptz not null,
        approved_by    text,
        approval_ref   text,
        decision       text        not null,
        decided_at     timestamptz not null,
        constraint ck_archive_decision check (decision in ('Approved','Refused'))
    );

    create table if not exists control.module_archive_check (
        request_id   uuid        not null references control.module_archive_request (request_id),
        check_code   text        not null,
        name_ar      text        not null,
        name_en      text        not null,
        passed       boolean     not null,
        detail       jsonb       not null default '{}'::jsonb,
        primary key (request_id, check_code)
    );

    -- =====================================================================
    -- 6 · القياس — المحور الأول: الوحدة، والثاني: المستخدم
    -- =====================================================================
    create table if not exists control.billing_period (
        period_code text not null primary key,   -- YYYY-MM
        starts_on   date not null,
        ends_on     date not null,
        closed_at   timestamptz,
        constraint ck_period_range check (ends_on >= starts_on)
    );

    -- حدث القياس الخام. الإحكام = مفتاح يورّده المنتِج + ON CONFLICT DO NOTHING
    -- (فخ-13: ممنوع أي حارس يقارن تسلسلاً مُطبَّقاً بـ> أو <).
    create table if not exists control.usage_event (
        tenant_id       uuid          not null,
        idempotency_key text          not null,
        period_code     text          not null,
        module_code     text          not null,
        user_ref        text,
        event_kind      text          not null,
        quantity        numeric(19,4) not null default 1,
        occurred_at     timestamptz   not null,
        recorded_at     timestamptz   not null,
        source          text          not null,
        primary key (tenant_id, idempotency_key),
        constraint ck_usage_qty check (quantity >= 0)
    );
    create index if not exists ix_usage_period
        on control.usage_event (period_code, tenant_id, module_code);
    create index if not exists ix_usage_user
        on control.usage_event (period_code, tenant_id, user_ref);

    -- المستخدمون المُسمَّون: المحور الثاني يحتاج القائمة الاسمية لا الأحداث فقط.
    create table if not exists control.tenant_user (
        tenant_id   uuid        not null references control.tenant (tenant_id),
        user_ref    text        not null,
        name_ar     text        not null,
        name_en     text        not null,
        state       text        not null,
        created_at  timestamptz not null,
        disabled_at timestamptz,
        primary key (tenant_id, user_ref),
        constraint ck_user_state check (state in ('Active','Disabled'))
    );

    -- ذروة التزامن: يحتاجها تعريف «المستخدم المتزامن» — يُلتقط ولو لم يُعتمد
    -- التعريف بعد، لأن ما لا يُلتقط اليوم لا يُستعاد غداً.
    create table if not exists control.concurrency_sample (
        tenant_id    uuid        not null references control.tenant (tenant_id),
        period_code  text        not null,
        sampled_at   timestamptz not null,
        active_users int         not null,
        primary key (tenant_id, sampled_at),
        constraint ck_sample_nonneg check (active_users >= 0)
    );
    create index if not exists ix_sample_period on control.concurrency_sample (period_code, tenant_id);

    -- =====================================================================
    -- 7 · سِرد العمليات — يسجّل ما فشل (فخ-08)
    -- =====================================================================
    create table if not exists control.operation_log (
        log_id      bigint      generated always as identity primary key,
        occurred_at timestamptz not null,
        tenant_id   uuid,
        actor       text        not null,
        operation   text        not null,
        outcome     text        not null,
        reason_ar   text        not null,
        payload     jsonb       not null default '{}'::jsonb,
        constraint ck_oplog_outcome check (outcome in ('Allowed','Refused','Failed','Recorded'))
    );
    create index if not exists ix_oplog on control.operation_log (tenant_id, occurred_at);

    create table if not exists control.schema_meta (
        singleton  boolean primary key default true,
        version    int     not null,
        applied_at timestamptz not null,
        constraint ck_singleton check (singleton)
    );
    """;

    /// <summary>
    /// يُنشئ قاعدة التحكّم ودورَ التطبيق إن لم يوجدا، ثم يطبّق المخطط.
    /// عملية <b>مُحكَمة</b>: تشغيلها مرتين لا يُنتج شيئاً مختلفاً.
    /// </summary>
    public static async Task EnsureAsync(ControlPlaneOptions o, CancellationToken ct = default)
    {
        var db = Db.Ident(o.ControlDatabase);
        var role = Db.Ident(o.AppRole);

        await using (var maint = await Db.OpenAsync(o.MaintenanceConnectionString, ct))
        {
            var exists = await Db.ScalarAsync<long>(maint,
                "select count(*) from pg_database where datname = @d",
                p => p.AddWithValue("d", o.ControlDatabase), null, ct);
            if (exists == 0)
            {
                try { await Db.ExecAsync(maint, $"create database {db}", null, ct); }
                catch (PostgresException ex) when (ex.SqlState == "42P04") { /* سبقنا إليها نداء متزامن */ }
            }

            var roleExists = await Db.ScalarAsync<long>(maint,
                "select count(*) from pg_roles where rolname = @r",
                p => p.AddWithValue("r", o.AppRole), null, ct);
            if (roleExists == 0)
            {
                try
                {
                    await Db.ExecAsync(maint,
                        $"create role {role} login nosuperuser nocreatedb nocreaterole noinherit",
                        null, ct);
                }
                catch (PostgresException ex) when (ex.SqlState == "42710") { }
            }
        }

        await using var c = await Db.OpenAsync(o.ControlConnectionString, ct);
        await Db.ExecAsync(c, Ddl, null, ct);
        await Db.WriteIdempotentAsync(c, """
            insert into control.schema_meta (singleton, version, applied_at)
            values (true, @v, @t)
            on conflict (singleton) do update
               set version = excluded.version, applied_at = excluded.applied_at
            """,
            p => { p.AddWithValue("v", Version); p.AddWithValue("t", Canon.Now()); },
            null, ct);

        // دور التطبيق لا يرى قاعدة التحكّم إطلاقاً: مستوى التحكّم عملياتي، لا
        // يُقرأ من مسار طلب المستأجر (فخ-30 — لا نعتمد على شرط WHERE للفصل).
        await Db.ExecAsync(c, $"revoke all on schema control from {role}", null, ct);
    }
}
