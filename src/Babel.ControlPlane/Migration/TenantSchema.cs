using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Migration;

/// <summary>
/// ترحيلة واحدة على قاعدة مستأجر واحد.
/// <para><c>VacuumAfter</c>: <c>VACUUM ANALYZE</c> بند إلزامي في نهاية كل
/// سكربت استيراد أو ترحيل (فخ-33) — ولا يمكن تنفيذه داخل معاملة، فيُنفَّذ
/// بعد الإيداع.</para>
/// </summary>
public sealed record TenantMigration(
    int Version, string NameAr, string NameEn, string Sql, bool VacuumAfter = false);

/// <summary>
/// كتالوج مخطط المستأجر. المخطط <b>مُصدَّر برقم</b>، وكل ترحيلة تُطبَّق داخل
/// معاملة واحدة مع سطر تسجيلها — فإما أن تُطبَّق وتُسجَّل معاً أو لا شيء.
/// (‏PostgreSQL يجعل الـDDL معاملاتياً، وهذه واحدة من أهم خصائصه هنا.)
/// </summary>
public static class TenantSchema
{
    /// <summary>إصدار الأساس الذي تبدأ منه كل قاعدة مستأجر جديدة.</summary>
    public const int BaselineVersion = 1;

    /// <summary>الإصدار الذي يجري عليه قياس الأسطول (‏v1 ⇒ v2).</summary>
    public const int FleetDemoVersion = 2;

    /// <summary>مرحلة <b>التوسيع</b>: العمودان معاً، والتطبيق القديم ما يزال يعمل.</summary>
    public const int ExpandVersion = 3;

    /// <summary>مرحلة <b>الانكماش</b>: العمود القديم يُحذف — بعد ترقية كل النُسخ.</summary>
    public const int ContractVersion = 4;

    /// <summary>أحدث إصدار مخطط مُعتمَد — وهو هدف التزويد الافتراضي.</summary>
    public const int LatestVersion = ContractVersion;

    /// <summary>
    /// إصداران إضافيان يُستعملان <b>لقياس الأسطول وحده</b> (تشكيلة عامل واحد
    /// مقابل أربعة). ليسا جزءاً من <see cref="LatestVersion"/>، ولا يُطبَّقان
    /// إلا إن طُلبا صراحةً — وشكلهما مطابق لترحيلة إنتاج معتادة:
    /// عمود + ملء رجعي + فهرس + <c>VACUUM ANALYZE</c>.
    /// </summary>
    public const int BenchVersionA = 5;
    /// <summary>إصدار قياس أداء ثانٍ — لقياس الإنتاجية لا للإنتاج.</summary>
    public const int BenchVersionB = 6;

    /// <summary>كل الترحيلات مرتّبةً بإصدارها. الترتيب جزء من العقد.</summary>
    public static readonly IReadOnlyList<TenantMigration> All =
    [
        new(1, "المخطط الأساس", "baseline schema", V1, VacuumAfter: true),
        new(2, "وسم الوحدة على الحساب + فهرس الفترة", "account module tag + period index", V2, VacuumAfter: true),
        new(3, "توسيع: memo_ar بجانب description_ar", "expand: memo_ar beside description_ar", V3),
        new(4, "انكماش: حذف description_ar", "contract: drop description_ar", V4, VacuumAfter: true),
        new(5, "قياس أ: وسم مصدر القيد", "bench A: entry source tag", V5, VacuumAfter: true),
        new(6, "قياس ب: وسم مرجع المستند", "bench B: document reference tag", V6, VacuumAfter: true),
    ];

    // =====================================================================
    private const string V1 = """
    create schema if not exists app;
    create schema if not exists ledger;

    create table if not exists app.schema_migration (
        version     int         primary key,
        name_ar     text        not null,
        name_en     text        not null,
        applied_at  timestamptz not null
    );

    create table if not exists app.tenant_meta (
        singleton   boolean     primary key default true,
        tenant_id   uuid        not null,
        tenant_code text        not null,
        name_ar     text        not null,
        name_en     text        not null,
        created_at  timestamptz not null,
        constraint ck_meta_singleton check (singleton)
    );

    create table if not exists app.role (
        role_code   text        primary key,
        name_ar     text        not null,
        name_en     text        not null,
        is_admin    boolean     not null default false,
        sort_order  int         not null default 0
    );

    create table if not exists app.app_user (
        user_ref    text        primary key,
        name_ar     text        not null,
        name_en     text        not null,
        email       text        not null,
        role_code   text        not null references app.role (role_code),
        state       text        not null,
        created_at  timestamptz not null,
        constraint ck_user_state check (state in ('Active','Disabled'))
    );

    -- دليل الحسابات. كل حساب يحمل الاسمين، ووحدة الأستاذ المساعد التي يخصّها
    -- (تُستعمل في الفحص السابق للأرشفة).
    create table if not exists ledger.account (
        account_code   text          primary key,
        name_ar        text          not null,
        name_en        text          not null,
        account_type   text          not null,
        subledger      text,
        is_postable    boolean       not null default true,
        constraint ck_account_type check
            (account_type in ('Asset','Liability','Equity','Revenue','Expense'))
    );

    create table if not exists ledger.period (
        period_code text        primary key,   -- YYYY-MM
        starts_on   date        not null,
        ends_on     date        not null,
        state       text        not null,
        closed_at   timestamptz,
        constraint ck_period_state check (state in ('Open','Closed'))
    );

    create table if not exists ledger.entry_counter (
        book_code   text   not null,
        fiscal_year int    not null,
        next_no     bigint not null,
        primary key (book_code, fiscal_year)
    );

    create table if not exists ledger.journal_entry (
        entry_id        uuid          primary key,
        book_code       text          not null,
        entry_no        bigint        not null,
        period_code     text          not null references ledger.period (period_code),
        entry_date      date          not null,
        module_code     text          not null,
        description_ar  text          not null,
        description_en  text          not null,
        actor           text          not null,
        posted_at       timestamptz   not null,
        constraint uq_entry_no unique (book_code, entry_no)
    );

    create table if not exists ledger.journal_line (
        line_id      uuid          primary key,
        entry_id     uuid          not null references ledger.journal_entry (entry_id),
        line_no      int           not null,
        account_code text          not null references ledger.account (account_code),
        debit        numeric(19,4) not null default 0,
        credit       numeric(19,4) not null default 0,
        constraint uq_line_no unique (entry_id, line_no),
        constraint ck_line_sign check (debit >= 0 and credit >= 0),
        constraint ck_line_one_side check (debit = 0 or credit = 0)
    );
    create index if not exists ix_line_account on ledger.journal_line (account_code);

    -- الأرصدة الفورية. صفوفها تُكتب دائماً بـINSERT … ON CONFLICT (فخ-09).
    create table if not exists ledger.account_balance (
        account_code text          not null references ledger.account (account_code),
        period_code  text          not null references ledger.period (period_code),
        debit        numeric(19,4) not null default 0,
        credit       numeric(19,4) not null default 0,
        primary key (account_code, period_code)
    );

    -- مُشغّل قيد مؤجَّل إلى الإيداع: القيد غير المتوازن يُرفض مهما كان مسار
    -- الكتابة — EF Core أو SQL خام أو psql أو سكربت مدير قاعدة بيانات.
    -- (ADR-0002/0003: الإنفاذ في المحرّك، لا في الكود.)
    create or replace function ledger.assert_entry_balanced() returns trigger
    language plpgsql security definer set search_path = ledger, pg_catalog as $fn$
    declare d numeric(19,4); c numeric(19,4); n int;
    begin
        select coalesce(sum(debit),0), coalesce(sum(credit),0), count(*)
          into d, c, n from ledger.journal_line where entry_id = new.entry_id;
        if n < 2 then
            raise exception 'UNBALANCED_ENTRY entry=% : سطران على الأقل (وُجد %)',
                new.entry_id, n using errcode = 'check_violation';
        end if;
        if d <> c then
            raise exception 'UNBALANCED_ENTRY entry=% مدين=% دائن=% فرق=%',
                new.entry_id, d, c, (d - c) using errcode = 'check_violation';
        end if;
        return null;
    end;
    $fn$;

    drop trigger if exists trg_entry_balanced on ledger.journal_entry;
    create constraint trigger trg_entry_balanced
        after insert on ledger.journal_entry
        deferrable initially deferred
        for each row execute function ledger.assert_entry_balanced();

    -- المستندات المفتوحة: يقرؤها الفحص السابق للأرشفة.
    create table if not exists app.document (
        document_id  uuid        primary key,
        module_code  text        not null,
        doc_no       text        not null,
        state        text        not null,
        created_at   timestamptz not null,
        constraint ck_doc_state check (state in ('Draft','Open','Posted','Cancelled'))
    );
    create index if not exists ix_doc_module on app.document (module_code, state);
    """;

    // =====================================================================
    private const string V2 = """
    alter table ledger.account add column if not exists module_code text;

    -- ملء رجعي: عبارة واحدة، بلا رحلات ذهاب وإياب داخل المعاملة (فخ-14).
    update ledger.account
       set module_code = coalesce(subledger, 'CORE')
     where module_code is null;

    create index if not exists ix_account_module on ledger.account (module_code);
    create index if not exists ix_balance_period on ledger.account_balance (period_code, account_code);
    """;

    // =====================================================================
    //  التوسيع: العمود الجديد يُضاف، ويُملأ رجعياً، ويُربط بمُشغّل مزامنة
    //  ثنائي الاتجاه. النتيجة: شيفرة الإصدار السابق تكتب description_ar
    //  فتظهر القيمة في memo_ar، والعكس. لا نافذة يفقد فيها أحدهما القيمة.
    // =====================================================================
    private const string V3 = """
    alter table ledger.journal_entry add column if not exists memo_ar text;

    update ledger.journal_entry
       set memo_ar = description_ar
     where memo_ar is null;

    create or replace function ledger.sync_memo() returns trigger
    language plpgsql as $fn$
    begin
        -- أيّ العمودين ورّده النداء؟ المُورَّد هو المصدر، والآخر يُشتقّ منه.
        if new.memo_ar is null and new.description_ar is not null then
            new.memo_ar := new.description_ar;
        elsif new.description_ar is null and new.memo_ar is not null then
            new.description_ar := new.memo_ar;
        elsif tg_op = 'UPDATE'
              and new.memo_ar is distinct from old.memo_ar
              and new.description_ar is not distinct from old.description_ar then
            new.description_ar := new.memo_ar;
        end if;
        return new;
    end;
    $fn$;

    drop trigger if exists trg_sync_memo on ledger.journal_entry;
    create trigger trg_sync_memo
        before insert or update on ledger.journal_entry
        for each row execute function ledger.sync_memo();

    -- بعد المُشغّل: العمود القديم يصير قابلاً لأن يُترك فارغاً من الشيفرة الجديدة.
    alter table ledger.journal_entry alter column description_ar drop not null;
    """;

    // =====================================================================
    private const string V4 = """
    drop trigger if exists trg_sync_memo on ledger.journal_entry;
    drop function if exists ledger.sync_memo();
    alter table ledger.journal_entry drop column if exists description_ar;
    alter table ledger.journal_entry alter column memo_ar set not null;
    """;

    // =====================================================================
    private const string V5 = """
    alter table ledger.journal_entry add column if not exists source_tag text;
    update ledger.journal_entry set source_tag = coalesce(actor, 'unknown') where source_tag is null;
    create index if not exists ix_entry_source on ledger.journal_entry (source_tag, entry_date);
    """;

    private const string V6 = """
    alter table ledger.journal_entry add column if not exists document_ref text;
    update ledger.journal_entry set document_ref = book_code || '-' || entry_no::text
     where document_ref is null;
    create index if not exists ix_entry_docref on ledger.journal_entry (document_ref);
    """;

    // =====================================================================

    /// <summary>
    /// إصدار المخطط المُطبَّق فعلاً على هذه القاعدة، مقروءاً من القاعدة نفسها
    /// لا من سجل التحكّم — التحقّق المباشر هو ما يكشف انحراف السجل عن الواقع.
    /// </summary>
    /// <param name="c">اتصال بقاعدة المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>رقم الإصدار.</returns>
    public static async Task<int> CurrentVersionAsync(NpgsqlConnection c, CancellationToken ct = default)
    {
        var present = await Db.ScalarAsync<bool>(c,
            "select to_regclass('app.schema_migration') is not null", null, null, ct);
        if (!present) return 0;
        return (int)(await Db.ScalarAsync<long>(c,
            "select coalesce(max(version), 0) from app.schema_migration", null, null, ct));
    }

    /// <summary>
    /// يطبّق ترحيلة واحدة داخل معاملة، ويسجّلها في نفس المعاملة.
    /// إعادة التشغيل بعد قتل المُرحِّل لا يمكن أن تجد «مُطبَّقة وغير مسجَّلة».
    /// يُرجِع <c>true</c> إن طُبِّقت الآن، و<c>false</c> إن كانت مُطبَّقة سلفاً.
    /// </summary>
    public static async Task<bool> ApplyAsync(NpgsqlConnection c, TenantMigration m,
        CancellationToken ct = default)
    {
        await using (var tx = await c.BeginTransactionAsync(ct))
        {
            // الجدول نفسه قد لا يكون موجوداً بعد (الترحيلة الأساس تُنشئه).
            var journalExists = await Db.ScalarAsync<bool>(c,
                "select to_regclass('app.schema_migration') is not null", null, tx, ct);

            if (journalExists)
            {
                var already = await Db.ScalarAsync<bool>(c,
                    "select exists (select 1 from app.schema_migration where version = @v)",
                    p => p.AddWithValue("v", m.Version), tx, ct);
                if (already)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }
            }

            await Db.ExecAsync(c, m.Sql, tx, ct);

            var inserted = await Db.WriteIdempotentAsync(c, """
                insert into app.schema_migration (version, name_ar, name_en, applied_at)
                values (@v, @ar, @en, @t)
                on conflict (version) do nothing
                """, p =>
                {
                    p.AddWithValue("v", m.Version);
                    p.AddWithValue("ar", m.NameAr);
                    p.AddWithValue("en", m.NameEn);
                    p.AddWithValue("t", Canon.Now());
                }, tx, ct);

            await tx.CommitAsync(ct);
            if (inserted == 0) return false;
        }

        // فخ-33: خارج المعاملة لأن VACUUM لا يعمل داخلها.
        if (m.VacuumAfter)
            await Db.ExecAsync(c, "vacuum analyze", null, ct);

        return true;
    }

    /// <summary>يرفع قاعدة مستأجر إلى إصدار مستهدف. يُرجِع عدد الترحيلات المُطبَّقة فعلاً.</summary>
    public static async Task<int> MigrateToAsync(NpgsqlConnection c, int targetVersion,
        CancellationToken ct = default)
    {
        var applied = 0;
        foreach (var m in All.Where(x => x.Version <= targetVersion).OrderBy(x => x.Version))
            if (await ApplyAsync(c, m, ct)) applied++;
        return applied;
    }
}
