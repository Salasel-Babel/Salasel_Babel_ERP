using BabelRelationalSpike.Support;

namespace BabelRelationalSpike.Db;

/// <summary>
/// All DDL. Executed by the OWNER (superuser) connection only.
/// The application role never gets DDL rights, and is explicitly stripped of
/// UPDATE/DELETE on every append-only table.
/// كل تعريفات المخطط تُنفَّذ بحساب المالك؛ دور التطبيق لا يملك UPDATE أو DELETE.
/// </summary>
public static class Bootstrap
{
    public static async Task EnsureDatabaseAndRoleAsync()
    {
        await using (var c = await Sql.OpenAsync(Config.Maintenance))
        {
            var exists = await Sql.ScalarAsync<long>(c,
                $"select count(*) from pg_database where datname = '{Config.Database}'");
            if (exists == 0)
                await Sql.ExecAsync(c, $"create database {Config.Database}");

            var roleExists = await Sql.ScalarAsync<long>(c,
                $"select count(*) from pg_roles where rolname = '{Config.AppRole}'");
            if (roleExists == 0)
                await Sql.ExecAsync(c,
                    $"create role {Config.AppRole} login nosuperuser nocreatedb nocreaterole noinherit");
            else
                await Sql.ExecAsync(c,
                    $"alter role {Config.AppRole} login nosuperuser nocreatedb nocreaterole noinherit");
        }

        await Sql.ExecAsync(Config.Admin, $"grant connect on database {Config.Database} to {Config.AppRole}");
    }

    public const string Ddl = """
    drop schema if exists ledger cascade;
    drop schema if exists app cascade;
    create schema ledger;
    create schema app;

    -- ---------------------------------------------------------------------
    -- (E) gapless counter. NOT a sequence: a sequence leaks numbers on
    --     rollback, which an auditor reads as a deleted document.
    --     عدّاد بلا فجوات: لا نستخدم sequence لأنها تُهدر أرقاماً عند التراجع.
    -- ---------------------------------------------------------------------
    create table ledger.entry_counter (
        book_id     text        primary key,
        next_no     bigint      not null,
        next_seq    bigint      not null
    );

    create sequence ledger.leaky_demo_seq;   -- used ONLY to demonstrate the leak

    -- ---------------------------------------------------------------------
    -- (B)/(E) append-only general ledger
    -- ---------------------------------------------------------------------
    create table ledger.journal_entry (
        entry_id      uuid            primary key,
        book_id       text            not null,
        tenant_id     text            not null,
        entry_no      bigint          not null,
        chain_seq     bigint          not null,
        entry_date    date            not null,
        memo          text            not null,
        memo_ar       text            not null,
        posted_at     timestamptz     not null,
        actor         text            not null,
        prev_hash     bytea           not null,
        entry_hash    bytea           not null,
        constraint uq_entry_no  unique (book_id, entry_no),
        constraint uq_chain_seq unique (book_id, chain_seq)
    );

    create table ledger.journal_line (
        line_id       uuid            primary key,
        entry_id      uuid            not null references ledger.journal_entry (entry_id),
        line_no       int             not null,
        account_code  text            not null,
        description   text            not null,
        debit         numeric(19,4)   not null default 0,
        credit        numeric(19,4)   not null default 0,
        constraint uq_line_no unique (entry_id, line_no),
        constraint ck_line_sign check (debit >= 0 and credit >= 0),
        constraint ck_line_one_side check (debit = 0 or credit = 0)
    );
    create index ix_journal_line_entry on ledger.journal_line (entry_id);

    -- ---------------------------------------------------------------------
    -- (B) DEFERRABLE INITIALLY DEFERRED constraint trigger.
    --     Fires at COMMIT, so it holds no matter which code path wrote the
    --     rows: EF Core, raw SQL, psql, a DBA script.
    --     SECURITY DEFINER so it does not depend on the caller's grants.
    -- ---------------------------------------------------------------------
    create or replace function ledger.assert_entry_balanced() returns trigger
    language plpgsql security definer set search_path = ledger, pg_catalog as $fn$
    declare
        d numeric(19,4);
        c numeric(19,4);
        n int;
    begin
        select coalesce(sum(debit), 0), coalesce(sum(credit), 0), count(*)
          into d, c, n
          from ledger.journal_line
         where entry_id = new.entry_id;

        if n < 2 then
            raise exception
              'UNBALANCED_ENTRY entry=% : a journal entry needs at least two lines (found %)',
              new.entry_id, n
              using errcode = 'check_violation';
        end if;

        if d <> c then
            raise exception
              'UNBALANCED_ENTRY entry=% debit=% credit=% difference=%',
              new.entry_id, d, c, (d - c)
              using errcode = 'check_violation';
        end if;
        return null;
    end
    $fn$;

    create constraint trigger trg_line_balanced
        after insert on ledger.journal_line
        deferrable initially deferred
        for each row execute function ledger.assert_entry_balanced();

    create constraint trigger trg_entry_balanced
        after insert on ledger.journal_entry
        deferrable initially deferred
        for each row execute function ledger.assert_entry_balanced();

    -- ---------------------------------------------------------------------
    -- (C) append-only process narrative log with a JSONB payload
    -- ---------------------------------------------------------------------
    create table ledger.process_event (
        event_id       uuid          primary key,
        tenant_id      text          not null,
        stream_type    text          not null,
        stream_id      uuid          not null,
        stream_seq     int           not null,
        event_type     text          not null,
        occurred_at    timestamptz   not null,
        actor          text          not null,
        correlation_id uuid          not null,
        causation_id   uuid          null,
        payload        jsonb         not null,
        constraint uq_stream_seq unique (stream_id, stream_seq)
    );
    create index ix_process_event_payload_gin
        on ledger.process_event using gin (payload jsonb_path_ops);
    create index ix_process_event_stream
        on ledger.process_event (stream_id, stream_seq);
    create index ix_process_event_type
        on ledger.process_event (tenant_id, event_type, occurred_at desc);
    -- expression indexes for the ONE hot scalar field, in both spellings that
    -- EF Core / Npgsql may emit (->> and #>>)
    create index ix_process_event_status_arrow
        on ledger.process_event ((payload ->> 'status'));
    create index ix_process_event_status_path
        on ledger.process_event ((payload #>> '{status}'));

    -- ---------------------------------------------------------------------
    -- (D) per-tenant flexible documents (settings, form definitions,
    --     custom fields, report templates) - MUTABLE by design.
    -- ---------------------------------------------------------------------
    create table app.tenant_document (
        tenant_id   text          not null,
        doc_type    text          not null,
        doc_key     text          not null,
        doc         jsonb         not null,
        updated_at  timestamptz   not null,
        primary key (tenant_id, doc_type, doc_key)
    );
    create index ix_tenant_document_gin
        on app.tenant_document using gin (doc jsonb_path_ops);
    -- both spellings of the expression index so whichever operator EF Core
    -- emits (->> or #>>) can be matched by the planner
    create index ix_tenant_document_locale_arrow
        on app.tenant_document ((doc ->> 'locale'));
    create index ix_tenant_document_locale_path
        on app.tenant_document ((doc #>> '{locale}'));

    -- (D) the STRONGLY TYPED half of the same idea: EF Core 10 maps this whole
    -- document to a POCO graph with ToJson(), while tenants keep adding custom
    -- fields, form definitions and report templates inside it with no migration.
    create table app.tenant_settings (
        tenant_id   text          primary key,
        settings    jsonb         not null,
        updated_at  timestamptz   not null
    );
    create index ix_tenant_settings_gin      on app.tenant_settings using gin (settings jsonb_path_ops);
    create index ix_tenant_settings_loc_a    on app.tenant_settings ((settings ->> 'Locale'));
    create index ix_tenant_settings_loc_b    on app.tenant_settings ((settings #>> '{Locale}'));
    create index ix_tenant_settings_loc_c    on app.tenant_settings ((settings ->> 'locale'));
    create index ix_tenant_settings_zatca_a  on app.tenant_settings ((settings -> 'Zatca' ->> 'Environment'));
    create index ix_tenant_settings_zatca_b  on app.tenant_settings ((settings #>> '{Zatca,Environment}'));

    -- ---------------------------------------------------------------------
    -- GRANTS: the application role is INSERT + SELECT only on everything
    -- append-only, and UPDATE/DELETE/TRUNCATE are explicitly revoked.
    -- ---------------------------------------------------------------------
    revoke all on schema ledger, app from public;
    grant usage on schema ledger, app to APPROLE;

    revoke all on all tables in schema ledger from APPROLE;
    revoke all on all tables in schema app    from APPROLE;

    grant select, insert on
        ledger.journal_entry, ledger.journal_line, ledger.process_event to APPROLE;
    revoke update, delete, truncate on
        ledger.journal_entry, ledger.journal_line, ledger.process_event from APPROLE;

    -- the gapless counter is deliberately mutable: it carries no financial data
    grant select, insert, update on ledger.entry_counter to APPROLE;
    grant usage, select, update on sequence ledger.leaky_demo_seq to APPROLE;

    -- tenant documents are settings, not a ledger: full CRUD
    grant select, insert, update, delete on app.tenant_document to APPROLE;
    grant select, insert, update, delete on app.tenant_settings to APPROLE;
    """;

    public static async Task ApplyDdlAsync()
    {
        await Sql.ExecAsync(Config.Admin, Ddl.Replace("APPROLE", Config.AppRole));
    }

    /// <summary>
    /// Wolverine creates its own envelope tables at startup as the owner; the
    /// application role then needs DML on them so that the outbox insert can
    /// join the very same transaction as the business write.
    /// </summary>
    public static async Task GrantWolverineToAppRoleAsync()
    {
        await Sql.ExecAsync(Config.Admin, $"""
            grant usage on schema {Config.WolverineSchema} to {Config.AppRole};
            grant select, insert, update, delete on all tables in schema {Config.WolverineSchema} to {Config.AppRole};
            grant usage, select, update on all sequences in schema {Config.WolverineSchema} to {Config.AppRole};
            grant execute on all functions in schema {Config.WolverineSchema} to {Config.AppRole};
            """);
    }

    public static async Task<string> ServerFactsAsync()
    {
        await using var c = await Sql.OpenAsync(Config.Admin);
        var version = await Sql.ScalarAsync<string>(c, "select version()");
        var facts = await Sql.ScalarAsync<string>(c, """
            select string_agg(name || '=' || setting, ', ' order by name)
            from pg_settings
            where name in ('synchronous_commit','fsync','full_page_writes','wal_level',
                           'max_connections','shared_buffers','wal_compression','data_checksums')
            """);
        return $"{version}\n{facts}";
    }
}
