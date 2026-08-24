using BabelDemo.Support;

namespace BabelDemo.Db;

/// <summary>
/// كل تعريفات المخطط تُنفَّذ بحساب المالك فقط. دور التطبيق لا يملك DDL، ولا يملك
/// UPDATE أو DELETE على أي جدول من جداول الدفتر — وهذا ما يجعل الخطوة الخامسة في
/// العرض تفشل داخل PostgreSQL نفسها لا داخل التطبيق.
///
/// All DDL runs as the OWNER. The application role never gets DDL, and is stripped
/// of UPDATE/DELETE on every append-only ledger table.
/// </summary>
public static class Bootstrap
{
    public static async Task EnsureDatabaseAndRoleAsync()
    {
        await using (var c = await Sql.OpenAsync(Config.Maintenance))
        {
            var dbExists = await Sql.ScalarAsync<long>(c,
                $"select count(*) from pg_database where datname = '{Config.Database}'");
            if (dbExists == 0)
                await Sql.ExecAsync(c, $"create database {Config.Database}");

            var roleExists = await Sql.ScalarAsync<long>(c,
                $"select count(*) from pg_roles where rolname = '{Config.AppRole}'");
            if (roleExists == 0)
                await Sql.ExecAsync(c, $"create role {Config.AppRole} login nosuperuser nocreatedb nocreaterole noinherit");
            else
                await Sql.ExecAsync(c, $"alter role {Config.AppRole} login nosuperuser nocreatedb nocreaterole noinherit");
        }

        await Sql.ExecAsync(Config.Owner, $"grant connect on database {Config.Database} to {Config.AppRole}");
    }

    public const string Ddl = """
    drop schema if exists ledger cascade;
    drop schema if exists demo cascade;
    create schema ledger;
    create schema demo;

    -- ---------------------------------------------------------------------
    -- دليل الحسابات: كل صف يحمل name_ar وname_en
    -- ---------------------------------------------------------------------
    create table ledger.account (
        account_code  text    primary key,
        parent_code   text    null references ledger.account (account_code),
        name_ar       text    not null,
        name_en       text    not null,
        account_type  text    not null,
        normal_side   text    not null,
        is_postable   boolean not null,
        sort_order    int     not null,
        constraint ck_account_type check (account_type in ('asset','liability','equity','revenue','expense')),
        constraint ck_normal_side  check (normal_side in ('debit','credit'))
    );

    -- ---------------------------------------------------------------------
    -- عدّاد بلا فجوات. ليس SEQUENCE: الـsequence تُهدر أرقاماً عند التراجع،
    -- والمدقّق يقرأ الرقم المفقود على أنه مستند محذوف.
    -- ---------------------------------------------------------------------
    create table ledger.entry_counter (
        book_id   text   primary key,
        next_no   bigint not null,
        next_seq  bigint not null
    );

    -- ---------------------------------------------------------------------
    -- الدفتر: يُضاف إليه فقط
    -- ---------------------------------------------------------------------
    create table ledger.journal_entry (
        entry_id      uuid          primary key,
        book_id       text          not null,
        tenant_id     text          not null,
        entry_no      bigint        not null,
        chain_seq     bigint        not null,
        entry_date    date          not null,
        memo          text          not null,
        memo_ar       text          not null,
        posted_at     timestamptz   not null,
        actor         text          not null,
        prev_hash     bytea         not null,
        entry_hash    bytea         not null,
        constraint uq_entry_no  unique (book_id, entry_no),
        constraint uq_chain_seq unique (book_id, chain_seq)
    );

    create table ledger.journal_line (
        line_id       uuid          primary key,
        entry_id      uuid          not null references ledger.journal_entry (entry_id),
        line_no       int           not null,
        account_code  text          not null references ledger.account (account_code),
        description   text          not null,
        debit         numeric(19,4) not null default 0,
        credit        numeric(19,4) not null default 0,
        constraint uq_line_no       unique (entry_id, line_no),
        constraint ck_line_sign     check (debit >= 0 and credit >= 0),
        constraint ck_line_one_side check (debit = 0 or credit = 0)
    );
    create index ix_journal_line_entry   on ledger.journal_line (entry_id);
    create index ix_journal_line_account on ledger.journal_line (account_code);

    -- ---------------------------------------------------------------------
    -- قيد مؤجّل إلى لحظة COMMIT: يمنع أي قيد غير متوازن مهما كان مسار الكتابة
    -- (EF Core، SQL خام، psql، سكربت مدير قاعدة بيانات).
    -- DEFERRABLE INITIALLY DEFERRED constraint trigger: fires at COMMIT.
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
    -- إسقاط الأرصدة (ميزان المراجعة): يُحدَّث داخل معاملة الترحيل نفسها.
    -- ---------------------------------------------------------------------
    create table ledger.account_balance (
        book_id       text          not null,
        period        text          not null,
        account_code  text          not null references ledger.account (account_code),
        debit         numeric(19,4) not null default 0,
        credit        numeric(19,4) not null default 0,
        updated_at    timestamptz   not null,
        primary key (book_id, period, account_code)
    );

    -- ---------------------------------------------------------------------
    -- سجل العبث: يخصّ العرض التوضيحي وحده، ويُكتب بحساب المالك فقط.
    -- ---------------------------------------------------------------------
    create table demo.tamper_log (
        tamper_id    bigserial     primary key,
        entry_no     bigint        not null,
        applied_at   timestamptz   not null,
        forward_sql  text          not null,
        restore_sql  text          not null,
        undone       boolean       not null default false
    );

    -- ---------------------------------------------------------------------
    -- الصلاحيات: دور التطبيق INSERT + SELECT فقط على الدفتر،
    -- وUPDATE/DELETE/TRUNCATE مسحوبة صراحةً.
    -- ---------------------------------------------------------------------
    revoke all on schema ledger, demo from public;
    grant usage on schema ledger to APPROLE;

    revoke all on all tables in schema ledger from APPROLE;

    grant select, insert on ledger.journal_entry, ledger.journal_line to APPROLE;
    revoke update, delete, truncate on ledger.journal_entry, ledger.journal_line from APPROLE;

    -- دليل الحسابات: قراءة فقط لدور التطبيق (بذْره يتم بحساب المالك)
    grant select on ledger.account to APPROLE;

    -- العدّاد وإسقاط الأرصدة قابلان للتحديث عمداً: ليسا دفتراً، والأرصدة يمكن إعادة بنائها
    grant select, insert, update on ledger.entry_counter   to APPROLE;
    grant select, insert, update on ledger.account_balance to APPROLE;
    """;

    public static async Task ApplyDdlAsync()
        => await Sql.ExecAsync(Config.Owner, Ddl.Replace("APPROLE", Config.AppRole));

    /// <summary>لقطة صلاحيات حيّة تُعرض في الشاشة، مقروءة من information_schema لا من ذاكرة التطبيق.</summary>
    public const string GrantsQuery = """
        select table_name, privilege_type
        from information_schema.table_privileges
        where grantee = 'APPROLE' and table_schema = 'ledger'
        order by table_name, privilege_type
        """;
}
