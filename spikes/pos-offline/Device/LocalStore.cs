using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BabelPosOffline.Device;

/// <summary>
/// المخزن المحلي للجهاز: <b>SQLite</b> بنمط WAL و<c>synchronous=FULL</c>.
///
/// لماذا SQLite ولماذا هذه الإعدادات بالذات (التبرير الكامل في DESIGN.md §2):
///  • ملف واحد، بلا خدمة، بلا مُشرف — الكاشير لا يستطيع «إيقاف الخدمة».
///  • معاملات ACID حقيقية مع <c>fsync</c> عند كل <c>COMMIT</c> تحت <c>synchronous=FULL</c>؛
///    وهذا هو الفارق بين النجاة من انهيار العملية والنجاة من انقطاع الكهرباء.
///  • مشغّلات (triggers) داخل المحرّك تفرض التوازن والحصانة، فلا تعتمد الحماية على مسار الشيفرة.
///  • أعداد صحيحة 64-bit للمبالغ ⇒ <b>لا float في أي مكان</b>، ويُفرض ذلك بـ<c>typeof()</c>.
///
/// الاعتراف الصريح: على الجهاز <b>لا توجد صلاحيات قاعدة بيانات</b> تُسحب كما في PostgreSQL
/// (§3.2 أ من وثيقة المعمارية). من يملك الملف يملك الكتابة فيه. لذلك حصانة الجهاز
/// <b>أضعف بنيوياً</b> من حصانة الخادم، وسلسلة التجزئة هي ما ينقل الاكتشاف إلى الخادم.
/// </summary>
public sealed class LocalStore : IDisposable
{
    public string Path { get; }
    private readonly SqliteConnection _conn;

    public LocalStore(string path, bool fullSync = true)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        _conn = new SqliteConnection(cs);
        _conn.Open();
        Exec("pragma journal_mode = WAL;");
        Exec($"pragma synchronous = {(fullSync ? "FULL" : "NORMAL")};");
        Exec("pragma foreign_keys = ON;");
        Exec("pragma busy_timeout = 15000;");
    }

    public SqliteConnection Connection => _conn;

    public void Exec(string sql, params (string, object?)[] ps)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = sql;
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        c.ExecuteNonQuery();
    }

    public T? Scalar<T>(string sql, params (string, object?)[] ps)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = sql;
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        var v2 = c.ExecuteScalar();
        if (v2 is null or DBNull) return default;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        // قيمة قادمة من SQLite (تسلسل، رقم فاتورة، بصمة): بيانات آلة ⇒ ثقافة ثابتة.
        // Values read back out of SQLite are machine data: convert invariantly.
        return (T)Convert.ChangeType(v2, target, CultureInfo.InvariantCulture);
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object?)[] ps)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = sql;
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        using var r = c.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    public string Pragma(string name) => Scalar<string>($"pragma {name};") ?? "";

    // ─────────────────────────────────────────────────────────────────────────
    public const string Schema = """
    create table if not exists device_identity (
        singleton     integer primary key check (singleton = 1),
        device_id     text not null,
        tenant_id     text not null,
        branch_id     text not null,
        registered_at text not null
    );

    -- مدى الأرقام المحجوز، كما منحه الخادم. الجهاز لا يخترع أرقاماً أبداً.
    create table if not exists number_range (
        range_id    text primary key,
        range_start integer not null check (typeof(range_start) = 'integer'),
        range_end   integer not null check (typeof(range_end)   = 'integer'),
        state       text not null check (state in ('active','exhausted','voided')),
        granted_at  text not null,
        check (range_end >= range_start)
    );

    -- العدّاد: صف واحد يُقرأ ويُحدَّث داخل معاملة البيع نفسها. لا AUTOINCREMENT:
    -- AUTOINCREMENT في SQLite (مثل SEQUENCE في PostgreSQL) يُهدر أرقاماً عند التراجع.
    create table if not exists device_counter (
        singleton integer primary key check (singleton = 1),
        next_no   integer not null check (typeof(next_no)  = 'integer'),
        next_seq  integer not null check (typeof(next_seq) = 'integer')
    );

    create table if not exists sale (
        sale_id           text primary key,
        idem_key          text not null unique,
        device_id         text not null,
        doc_type          text not null check (doc_type in ('SALE','RETURN')),
        invoice_no        integer not null unique check (typeof(invoice_no) = 'integer'),
        chain_seq         integer not null unique check (typeof(chain_seq)  = 'integer'),
        business_date     text not null,
        device_clock_at   text not null,
        monotonic_ms      integer not null,
        boot_id           text not null,
        shift_id          text not null,
        original_idem_key text null,
        currency          text not null,
        total_net_minor   integer not null check (typeof(total_net_minor)   = 'integer'),
        total_vat_minor   integer not null check (typeof(total_vat_minor)   = 'integer'),
        total_gross_minor integer not null check (typeof(total_gross_minor) = 'integer'),
        prev_hash         text not null,
        entry_hash        text not null,
        payload_hash      text not null,
        past_ceiling      integer not null default 0,
        sealed            integer not null default 0,
        sync_state        text not null default 'pending'
                          check (sync_state in ('pending','inflight','acked','quarantined','rejected')),
        acked_at          text null,
        server_note       text null
    );
    create index if not exists ix_sale_pending on sale (sync_state, chain_seq);

    create table if not exists sale_line (
        sale_id         text not null references sale(sale_id),
        line_no         integer not null,
        item_code       text not null,
        qty_minor       integer not null check (typeof(qty_minor)        = 'integer'),
        unit_price_minor integer not null check (typeof(unit_price_minor) = 'integer'),
        line_net_minor  integer not null check (typeof(line_net_minor)   = 'integer'),
        line_vat_minor  integer not null check (typeof(line_vat_minor)   = 'integer'),
        primary key (sale_id, line_no)
    );

    -- الشق الإيرادي من القيد، وهو ما يستطيع الجهاز حسابه دون اتصال.
    -- الشق التكلفوي (تكلفة البضاعة المباعة) يكمله الخادم — انظر DESIGN.md §7.
    create table if not exists journal_line (
        sale_id      text not null references sale(sale_id),
        line_no      integer not null,
        account_code text not null,
        debit_minor  integer not null check (typeof(debit_minor)  = 'integer' and debit_minor  >= 0),
        credit_minor integer not null check (typeof(credit_minor) = 'integer' and credit_minor >= 0),
        check (debit_minor = 0 or credit_minor = 0),
        primary key (sale_id, line_no)
    );

    create table if not exists sync_checkpoint (
        singleton                 integer primary key check (singleton = 1),
        last_contact_server_utc   text null,
        last_contact_monotonic_ms integer null,
        last_contact_boot_id      text null,
        server_skew_ms            integer null,
        batches_sent              integer not null default 0
    );

    -- دفتر أزمنة التشغيل: يجعل عمر التراكم قابلاً للتقدير عبر إعادات الإقلاع.
    create table if not exists uptime_ledger (
        boot_id      text primary key,
        started_wall text not null,
        accum_ms     integer not null,
        seq          integer not null,
        last_wall    text null,
        last_mono_ms integer null
    );

    -- الشذوذات الزمنية تُسجَّل إيجاباً، ولا تُصحَّح بصمت.
    create table if not exists clock_event (
        event_id    text primary key,
        detected_at text not null,
        boot_id     text not null,
        kind        text not null,
        delta_ms    integer not null,
        detail      text not null
    );

    -- طابور استثناءات محلي (يُرفع مع المزامنة).
    create table if not exists local_exception (
        exception_id text primary key,
        raised_at    text not null,
        kind         text not null,
        severity     text not null,
        detail       text not null,
        cleared_at   text null
    );

    -- ── المشغّلات: التوازن والحصانة داخل المحرّك، لا في مسار الشيفرة ──────────
    create trigger if not exists trg_sale_seal_balanced
    before update of sealed on sale
    when new.sealed = 1 and old.sealed = 0
    begin
        select case
            when (select count(*) from journal_line where sale_id = new.sale_id) < 2
                then raise(abort, 'UNBALANCED_ENTRY: a journal entry needs at least two lines')
            when (select coalesce(sum(debit_minor),0) - coalesce(sum(credit_minor),0)
                    from journal_line where sale_id = new.sale_id) <> 0
                then raise(abort, 'UNBALANCED_ENTRY: sum(debit) <> sum(credit)')
            when (select coalesce(sum(line_net_minor),0) + coalesce(sum(line_vat_minor),0)
                    from sale_line where sale_id = new.sale_id) <> new.total_gross_minor
                then raise(abort, 'TOTALS_MISMATCH: sum(lines) <> total_gross')
        end;
    end;

    create trigger if not exists trg_sale_immutable
    before update of sale_id, idem_key, doc_type, invoice_no, chain_seq, business_date,
                     device_clock_at, original_idem_key, currency, total_net_minor,
                     total_vat_minor, total_gross_minor, prev_hash, entry_hash, payload_hash, sealed
    on sale when old.sealed = 1
    begin select raise(abort, 'SEALED_SALE_IMMUTABLE: a posted sale is corrected by a reversing entry only'); end;

    create trigger if not exists trg_sale_no_delete
    before delete on sale when old.sealed = 1
    begin select raise(abort, 'SEALED_SALE_UNDELETABLE: a posted sale is never deleted'); end;

    create trigger if not exists trg_sale_line_immutable
    before update on sale_line
    when (select sealed from sale where sale_id = old.sale_id) = 1
    begin select raise(abort, 'SEALED_LINE_IMMUTABLE'); end;

    create trigger if not exists trg_sale_line_no_delete
    before delete on sale_line
    when (select sealed from sale where sale_id = old.sale_id) = 1
    begin select raise(abort, 'SEALED_LINE_UNDELETABLE'); end;

    create trigger if not exists trg_journal_line_immutable
    before update on journal_line
    when (select sealed from sale where sale_id = old.sale_id) = 1
    begin select raise(abort, 'SEALED_JOURNAL_LINE_IMMUTABLE'); end;

    create trigger if not exists trg_journal_line_no_delete
    before delete on journal_line
    when (select sealed from sale where sale_id = old.sale_id) = 1
    begin select raise(abort, 'SEALED_JOURNAL_LINE_UNDELETABLE'); end;
    """;

    public void ApplySchema() => Exec(Schema);

    public void Dispose()
    {
        try { Exec("pragma wal_checkpoint(TRUNCATE);"); } catch { /* الملف قد يكون مقفلاً */ }
        _conn.Dispose();
    }

    public static void Delete(string path)
    {
        foreach (var p in new[] { path, path + "-wal", path + "-shm" })
            if (File.Exists(p)) File.Delete(p);
    }
}
