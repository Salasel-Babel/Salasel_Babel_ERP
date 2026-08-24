using BabelPosOffline.Support;

namespace BabelPosOffline.Server;

/// <summary>
/// كل تعريفات مخطط الخادم. تُنفَّذ بحساب المالك.
/// دفتر الأستاذ هو نفسه شكل <c>spikes/relational-stack</c>: يُضاف إليه فقط، بعدّاد بلا
/// فجوات <b>لكل دفتر</b> (هنا: دفتر لكل جهاز، لأن كل جهاز وحدة إصدار مستقلة)، ومشغّل
/// توازن مؤجّل يعمل عند الـCOMMIT.
/// </summary>
public static class ServerBootstrap
{
    public static async Task EnsureDatabaseAsync()
    {
        await using var c = await Sql.OpenAsync(Config.Maintenance);
        var exists = await Sql.ScalarAsync<long>(c, $"select count(*) from pg_database where datname = '{Config.Database}'");
        if (exists == 0) await Sql.ExecAsync(c, $"create database {Config.Database}");
    }

    public const string Ddl = """
    drop schema if exists pos cascade;
    drop schema if exists ledger cascade;
    create schema ledger;
    create schema pos;
    create extension if not exists btree_gist;

    -- ═══ دفتر الأستاذ (نفس شكل spikes/relational-stack) ═══════════════════════
    create table ledger.entry_counter (
        book_id  text   primary key,
        next_no  bigint not null,
        next_seq bigint not null
    );

    create table ledger.journal_entry (
        entry_id   uuid        primary key,
        book_id    text        not null,
        tenant_id  text        not null,
        entry_no   bigint      not null,
        chain_seq  bigint      not null,
        entry_date date        not null,
        memo       text        not null,
        memo_ar    text        not null,
        posted_at  timestamptz not null,
        actor      text        not null,
        source_idem_key text   null,
        prev_hash  bytea       not null,
        entry_hash bytea       not null,
        constraint uq_entry_no  unique (book_id, entry_no),
        constraint uq_chain_seq unique (book_id, chain_seq)
    );
    create index ix_je_source on ledger.journal_entry (source_idem_key);

    create table ledger.journal_line (
        line_id      uuid          primary key,
        entry_id     uuid          not null references ledger.journal_entry (entry_id),
        line_no      int           not null,
        account_code text          not null,
        description  text          not null,
        debit        numeric(19,4) not null default 0,
        credit       numeric(19,4) not null default 0,
        constraint uq_line_no unique (entry_id, line_no),
        constraint ck_line_sign check (debit >= 0 and credit >= 0),
        constraint ck_line_one_side check (debit = 0 or credit = 0)
    );
    create index ix_jl_entry on ledger.journal_line (entry_id);

    create or replace function ledger.assert_entry_balanced() returns trigger
    language plpgsql security definer set search_path = ledger, pg_catalog as $fn$
    declare d numeric(19,4); c numeric(19,4); n int;
    begin
        select coalesce(sum(debit),0), coalesce(sum(credit),0), count(*)
          into d, c, n from ledger.journal_line where entry_id = new.entry_id;
        if n < 2 then
            raise exception 'UNBALANCED_ENTRY entry=% : needs at least two lines (found %)', new.entry_id, n
              using errcode = 'check_violation';
        end if;
        if d <> c then
            raise exception 'UNBALANCED_ENTRY entry=% debit=% credit=% diff=%', new.entry_id, d, c, (d-c)
              using errcode = 'check_violation';
        end if;
        return null;
    end $fn$;

    create constraint trigger trg_line_balanced after insert on ledger.journal_line
        deferrable initially deferred for each row execute function ledger.assert_entry_balanced();
    create constraint trigger trg_entry_balanced after insert on ledger.journal_entry
        deferrable initially deferred for each row execute function ledger.assert_entry_balanced();

    -- ═══ سجلّ الأجهزة ═════════════════════════════════════════════════════════
    create table pos.device (
        device_id   text primary key,
        tenant_id   text not null,
        branch_id   text not null,
        state       text not null check (state in ('active','replaced','lost','destroyed')),
        replaced_by text null references pos.device(device_id),
        registered_at timestamptz not null,
        retired_at  timestamptz null,
        note        text null,
        -- علامة المياه العليا: أعلى رقم أصدره الجهاز كما أبلغ عنه في آخر اتصال.
        -- بدونها لا يستطيع الخادم اكتشاف «فواتير صدرت ولم تصل» — لأن الفراغ يقع فوق
        -- أعلى رقم يعرفه، فلا يبدو فراغاً بل يبدو «لم يُصدَر شيء بعد».
        last_reported_next_no bigint null,
        last_contact_at timestamptz null
    );

    -- ═══ مديات الأرقام المحجوزة ═══════════════════════════════════════════════
    -- عدم التداخل يفرضه محرّك قاعدة البيانات بقيد استبعاد، لا شرط في الكود.
    -- Non-overlap is enforced by the ENGINE with an exclusion constraint.
    create table pos.number_range (
        range_id    text   primary key,
        tenant_id   text   not null,
        device_id   text   not null references pos.device(device_id),
        range_start bigint not null,
        range_end   bigint not null,
        state       text   not null check (state in ('active','exhausted','voided')),
        granted_at  timestamptz not null,
        voided_at   timestamptz null,
        constraint ck_range_order check (range_end >= range_start),
        constraint ex_range_no_overlap exclude using gist (
            tenant_id with =, int8range(range_start, range_end + 1) with &&)
    );

    -- الفجوة تُثبَت إيجاباً. المُدقّق لا يستطيع التفريق بين «لم يحدث شيء» و«حُذفت سجلات»
    -- ما لم يُسجَّل الفراغ نفسه بوصفه واقعة.
    create table pos.number_gap_assertion (
        assertion_id uuid primary key,
        tenant_id    text   not null,
        device_id    text   not null,
        from_no      bigint not null,
        to_no        bigint not null,
        reason_code  text   not null,
        reason_ar    text   not null,
        asserted_at  timestamptz not null,
        asserted_by  text   not null,
        evidence_hash bytea not null,
        constraint ck_gap_order check (to_no >= from_no)
    );
    create index ix_gap_device on pos.number_gap_assertion (tenant_id, device_id, from_no);

    -- ═══ صندوق الوارد الحصين ══════════════════════════════════════════════════
    -- المفتاح الأساسي هو مفتاح الحصانة الذي يوفّره العميل — لا حارس تسلسل.
    -- الترتيب لا يهم إطلاقاً: الوصول الثاني بالمفتاح نفسه لا يفعل شيئاً.
    create table pos.sale_inbox (
        idem_key           text primary key,
        tenant_id          text   not null,
        device_id          text   not null,
        sale_id            uuid   not null,
        doc_type           text   not null check (doc_type in ('SALE','RETURN')),
        invoice_no         bigint not null,
        device_seq         bigint not null,
        business_date      date   not null,
        device_clock_at    timestamptz not null,
        server_received_at timestamptz not null,
        clock_skew_ms      bigint not null,
        shift_id           text   not null,
        original_idem_key  text   null,
        total_net          numeric(19,4) not null,
        total_vat          numeric(19,4) not null,
        total_gross        numeric(19,4) not null,
        device_prev_hash   bytea  not null,
        device_entry_hash  bytea  not null,
        payload_hash       bytea  not null,
        past_ceiling       boolean not null default false,
        status             text   not null check (status in ('posted','quarantined','rejected')),
        note               text   null,
        entry_id           uuid   null references ledger.journal_entry (entry_id),
        constraint uq_device_invoice unique (tenant_id, device_id, doc_type, invoice_no)
    );
    create index ix_inbox_device_seq on pos.sale_inbox (tenant_id, device_id, device_seq);
    create index ix_inbox_status on pos.sale_inbox (status) where status <> 'posted';
    create index ix_inbox_orig on pos.sale_inbox (original_idem_key) where original_idem_key is not null;

    -- ═══ طابور الاستثناءات: ما يحتاج قراراً بشرياً يُرى، ولا يُحلّ بصمت ══════════
    create table pos.exception_queue (
        exception_id uuid primary key,
        tenant_id    text not null,
        kind         text not null,
        severity     text not null check (severity in ('info','warn','block')),
        device_id    text null,
        idem_key     text null,
        detail       jsonb not null,
        raised_at    timestamptz not null,
        occurrences  bigint not null default 1,
        resolved_at  timestamptz null,
        resolved_by  text null
    );
    create index ix_exq_open on pos.exception_queue (tenant_id, kind) where resolved_at is null;

    -- الاستثناءات التي تقع «لكل سطر» (تجاوز الرصيد، انحراف السعر) تُجمَّع في صف واحد
    -- مفتوح لكل (مستأجر × نوع × جهاز × صنف) مع عدّاد تكرار. بدون هذا يُنتج صنف واحد
    -- سيّئ الإعداد آلاف الصفوف يومياً فيصير الطابور ضجيجاً لا يقرأه أحد.
    create unique index uq_exq_open_line on pos.exception_queue
        (tenant_id, kind, coalesce(device_id,''), coalesce(detail->>'item_code',''))
        where resolved_at is null and kind in ('STOCK_OVERSELL','PRICE_VARIANCE');

    -- ═══ المخزون والأسعار (مركزيان) ═══════════════════════════════════════════
    create table pos.stock (
        tenant_id text not null,
        item_code text not null,
        on_hand   numeric(19,4) not null,
        primary key (tenant_id, item_code)
    );

    create table pos.price (
        tenant_id      text not null,
        item_code      text not null,
        effective_from timestamptz not null,
        unit_price     numeric(19,4) not null,
        primary key (tenant_id, item_code, effective_from)
    );

    -- تكلفة الوحدة المركزية: الجهاز لا يعرفها دون اتصال، فيكملها الخادم (DESIGN.md §7).
    create table pos.item_cost (
        tenant_id text not null,
        item_code text not null,
        unit_cost numeric(19,4) not null,
        primary key (tenant_id, item_code)
    );

    -- ═══ التصميم المحظور، للإثبات المضاد فقط ══════════════════════════════════
    -- حارس تسلسل متزايد لكل حساب. موجود هنا كي يُثبَت فشله على المدخلات نفسها،
    -- فيبقى في المستودع سببُ القاعدة لا نصُّها فقط.
    -- صندوق وارد ساذج: مفتاح بلا هوية جهاز + ON CONFLICT DO NOTHING بلا حارس محتوى.
    -- موجود ليُثبَت أنه يبتلع عملية بيع حقيقية بلا خطأ.
    create table pos.naive_inbox (
        idem_key text primary key,
        gross    numeric(19,4) not null
    );

    create table pos.forbidden_balance (
        account_code text primary key,
        balance      numeric(19,4) not null default 0,
        applied_seq  bigint not null default 0
    );
    """;

    public const string Functions = """
    -- ══════════════════════════════════════════════════════════════════════════
    -- الترحيل: نداء واحد على الخادم داخل قفل العدّاد (القاعدة 1 من وثيقة المعمارية):
    -- قراءة العدّاد + قراءة البصمة السابقة + إدراج الرأس والسطور + رفع العدّاد،
    -- كلها في استدعاء واحد. لا حلقة foreach تتحدّث مع قاعدة البيانات داخل القفل.
    -- ══════════════════════════════════════════════════════════════════════════
    create or replace function pos.post_entry(
        p_book       text,
        p_tenant     text,
        p_date       date,
        p_memo       text,
        p_memo_ar    text,
        p_actor      text,
        p_idem_key   text,
        p_lines      jsonb        -- [{"line_no":1,"account":"1101","debit":"115.0000","credit":"0.0000","desc":"…"}]
    ) returns table (out_entry_id uuid, out_entry_no bigint, out_chain_seq bigint)
    language plpgsql as $fn$
    declare
        v_no bigint; v_seq bigint; v_prev bytea; v_id uuid := gen_random_uuid();
        v_canon text; v_hash bytea; r jsonb;
    begin
        insert into ledger.entry_counter (book_id, next_no, next_seq) values (p_book, 1, 1)
            on conflict (book_id) do nothing;

        select next_no, next_seq into v_no, v_seq
          from ledger.entry_counter where book_id = p_book for update;

        select entry_hash into v_prev
          from ledger.journal_entry where book_id = p_book and chain_seq = v_seq - 1;
        if v_prev is null then
            v_prev := digest('babel.genesis.v1|' || p_book, 'sha256');
        end if;

        -- الشكل القانوني: مقياس ثابت، UTC، ترتيب حقول ثابت، والتسلسل والبصمة السابقة داخله
        v_canon := 'babel.journal.v1' || E'\n'
                || 'chain_seq=' || v_seq || E'\n'
                || 'prev_hash=' || encode(v_prev, 'hex') || E'\n'
                || 'book_id=' || p_book || E'\n'
                || 'tenant_id=' || p_tenant || E'\n'
                || 'entry_id=' || v_id::text || E'\n'
                || 'entry_no=' || v_no || E'\n'
                || 'entry_date=' || to_char(p_date, 'YYYY-MM-DD') || E'\n'
                || 'source=' || coalesce(p_idem_key, '') || E'\n';
        for r in select * from jsonb_array_elements(p_lines) order by (value->>'line_no')::int loop
            v_canon := v_canon || 'line=' || (r->>'line_no') || '|' || (r->>'account') || '|'
                    || to_char((r->>'debit')::numeric,  'FM9999999999999990.0000') || '|'
                    || to_char((r->>'credit')::numeric, 'FM9999999999999990.0000') || E'\n';
        end loop;
        v_canon := v_canon || 'end' || E'\n';
        v_hash := digest(v_canon, 'sha256');

        insert into ledger.journal_entry
            (entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date, memo, memo_ar,
             posted_at, actor, source_idem_key, prev_hash, entry_hash)
        values (v_id, p_book, p_tenant, v_no, v_seq, p_date, p_memo, p_memo_ar,
                date_trunc('microsecond', now()), p_actor, p_idem_key, v_prev, v_hash);

        insert into ledger.journal_line (line_id, entry_id, line_no, account_code, description, debit, credit)
        select gen_random_uuid(), v_id, (e->>'line_no')::int, e->>'account',
               coalesce(e->>'desc',''), (e->>'debit')::numeric, (e->>'credit')::numeric
        from jsonb_array_elements(p_lines) e;

        update ledger.entry_counter set next_no = next_no + 1, next_seq = next_seq + 1 where book_id = p_book;

        out_entry_id := v_id; out_entry_no := v_no; out_chain_seq := v_seq; return next;
    end $fn$;

    -- ══════════════════════════════════════════════════════════════════════════
    -- الاستيعاب الحصين لعملية بيع واحدة. مستقل عن الترتيب تماماً.
    -- ══════════════════════════════════════════════════════════════════════════
    create or replace function pos.ingest_sale(
        p_idem_key text, p_tenant text, p_device text, p_sale_id uuid, p_doc_type text,
        p_invoice_no bigint, p_device_seq bigint, p_business_date date,
        p_device_clock timestamptz, p_shift text, p_orig_idem text,
        p_net numeric, p_vat numeric, p_gross numeric,
        p_prev_hash bytea, p_entry_hash bytea, p_payload_hash bytea, p_past_ceiling boolean,
        p_lines jsonb, p_journal jsonb, p_orphan_policy text
    ) returns table (outcome text, note text, entry_no bigint)
    language plpgsql as $fn$
    declare
        v_now timestamptz := date_trunc('microsecond', clock_timestamp());
        v_skew bigint;
        v_stored_payload bytea;
        v_entry uuid; v_no bigint; v_seq bigint;
        v_cost numeric(19,4); v_qty numeric(19,4); v_cogs numeric(19,4) := 0;
        v_onhand numeric(19,4); r jsonb; v_in_range boolean; v_central numeric(19,4);
        v_lines jsonb; v_released int := 0;
    begin
        v_skew := (extract(epoch from (v_now - p_device_clock)) * 1000)::bigint;

        -- (1) الحصانة: مفتاح العميل مفتاحاً أساسياً. لا حارس تسلسل.
        insert into pos.sale_inbox (
            idem_key, tenant_id, device_id, sale_id, doc_type, invoice_no, device_seq,
            business_date, device_clock_at, server_received_at, clock_skew_ms, shift_id,
            original_idem_key, total_net, total_vat, total_gross,
            device_prev_hash, device_entry_hash, payload_hash, past_ceiling, status)
        values (p_idem_key, p_tenant, p_device, p_sale_id, p_doc_type, p_invoice_no, p_device_seq,
                p_business_date, p_device_clock, v_now, v_skew, p_shift,
                p_orig_idem, p_net, p_vat, p_gross,
                p_prev_hash, p_entry_hash, p_payload_hash, coalesce(p_past_ceiling,false), 'posted')
        on conflict (idem_key) do nothing;

        if not found then
            select payload_hash into v_stored_payload from pos.sale_inbox where idem_key = p_idem_key;
            if v_stored_payload = p_payload_hash then
                outcome := 'duplicate'; note := 'same key, same content - nothing done'; entry_no := null; return next; return;
            end if;
            -- المفتاح نفسه بمحتوى مختلف: لو صمتنا هنا لضاعت عملية بيع حقيقية بلا أثر
            insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
            values (gen_random_uuid(), p_tenant, 'CONFLICT_MISMATCH', 'block', p_device, p_idem_key,
                    jsonb_build_object('stored_payload_hash', encode(v_stored_payload,'hex'),
                                       'incoming_payload_hash', encode(p_payload_hash,'hex'),
                                       'incoming_gross', p_gross::text,
                                       'reason_ar', 'مفتاح حصانة مُعاد استخدامه بمحتوى مختلف'),
                    v_now);
            outcome := 'conflict_mismatch';
            note := 'idempotency key reused with DIFFERENT content - raised for human decision, NOT swallowed';
            entry_no := null; return next; return;
        end if;

        -- (2) رقم الفاتورة يجب أن يقع داخل مدى مملوك لهذا الجهاز
        select exists (select 1 from pos.number_range nr
                        where nr.tenant_id = p_tenant and nr.device_id = p_device
                          and p_invoice_no between nr.range_start and nr.range_end)
          into v_in_range;
        if not v_in_range then
            update pos.sale_inbox set status = 'rejected',
                   note = 'invoice number outside any range reserved for this device' where idem_key = p_idem_key;
            insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
            values (gen_random_uuid(), p_tenant, 'INVOICE_OUT_OF_RANGE', 'block', p_device, p_idem_key,
                    jsonb_build_object('invoice_no', p_invoice_no, 'reason_ar', 'رقم فاتورة خارج المدى المحجوز للجهاز'), v_now);
            outcome := 'rejected'; note := 'invoice_no outside this device''s reserved ranges'; entry_no := null;
            return next; return;
        end if;

        -- (3) انزياح الساعة يُسجَّل دائماً، ويُصعَّد فوق العتبة
        if abs(v_skew) > 900000 then
            insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
            values (gen_random_uuid(), p_tenant, 'CLOCK_SKEW', 'warn', p_device, p_idem_key,
                    jsonb_build_object('skew_ms', v_skew, 'device_clock_at', p_device_clock,
                                       'server_received_at', v_now,
                                       'reason_ar', 'انزياح ساعة الجهاز عن الخادم فوق العتبة'), v_now);
        end if;

        -- (4) مرتجع بلا بيع أصلي مُزامَن: لا يُرحَّل بصمت أبداً
        if p_doc_type = 'RETURN' and p_orig_idem is not null
           and not exists (select 1 from pos.sale_inbox si where si.idem_key = p_orig_idem and si.status = 'posted') then
            if p_orphan_policy = 'Quarantine' then
                update pos.sale_inbox set status = 'quarantined',
                       note = 'original sale not yet synced' where idem_key = p_idem_key;
                insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
                values (gen_random_uuid(), p_tenant, 'ORPHAN_RETURN', 'block', p_device, p_idem_key,
                        jsonb_build_object('original_idem_key', p_orig_idem, 'gross', p_gross::text,
                                           'reason_ar', 'مرتجع بلا فاتورة بيع أصلية مُزامَنة'), v_now);
                outcome := 'quarantined';
                note := 'orphan return held: the original sale has not arrived (policy = Quarantine)';
                entry_no := null; return next; return;
            else
                insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
                values (gen_random_uuid(), p_tenant, 'ORPHAN_RETURN', 'warn', p_device, p_idem_key,
                        jsonb_build_object('original_idem_key', p_orig_idem, 'posted_anyway', true,
                                           'reason_ar', 'مرتجع يتيم رُحِّل بموجب سياسة PostWithAlarm'), v_now);
            end if;
        end if;

        -- (5) الترحيل: الشق الإيرادي كما حسبه الجهاز
        select out_entry_id, out_entry_no, out_chain_seq into v_entry, v_no, v_seq
          from pos.post_entry('POS:' || p_device, p_tenant, p_business_date,
                              p_doc_type || ' ' || p_invoice_no || ' @ ' || p_device,
                              case when p_doc_type = 'RETURN' then 'مرتجع' else 'مبيعات نقطة بيع' end,
                              p_device, p_idem_key, p_journal);
        update pos.sale_inbox set entry_id = v_entry where idem_key = p_idem_key;

        -- (6) الشق التكلفوي: الجهاز لا يعرف التكلفة المتوسطة دون اتصال، فيكمله الخادم
        for r in select * from jsonb_array_elements(p_lines) loop
            v_qty := (r->>'qty')::numeric;
            select unit_cost into v_cost from pos.item_cost
             where tenant_id = p_tenant and item_code = r->>'item_code';
            if v_cost is not null then v_cogs := v_cogs + round(v_qty * v_cost, 4); end if;

            -- المخزون: البيع دون اتصال لا يُرفض أبداً (البضاعة سُلِّمت والنقد قُبض)
            insert into pos.stock (tenant_id, item_code, on_hand) values (p_tenant, r->>'item_code', 0)
                on conflict (tenant_id, item_code) do nothing;
            update pos.stock
               set on_hand = on_hand + case when p_doc_type = 'RETURN' then v_qty else -v_qty end
             where tenant_id = p_tenant and item_code = r->>'item_code'
            returning on_hand into v_onhand;

            if v_onhand < 0 then
                insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
                values (gen_random_uuid(), p_tenant, 'STOCK_OVERSELL', 'block', p_device, p_idem_key,
                        jsonb_build_object('item_code', r->>'item_code', 'on_hand', v_onhand::text, 'qty', v_qty::text,
                                           'reason_ar', 'بيع تجاوز الرصيد المتاح — تسوية مخزنية مطلوبة'), v_now)
                on conflict (tenant_id, kind, coalesce(device_id,''), coalesce(detail->>'item_code',''))
                    where resolved_at is null and kind in ('STOCK_OVERSELL','PRICE_VARIANCE')
                do update set occurrences = pos.exception_queue.occurrences + 1,
                              raised_at = excluded.raised_at, detail = excluded.detail;
            end if;

            -- انحراف السعر: الفاتورة المسلَّمة للعميل مستند نهائي، لا يُعاد تسعيرها
            select unit_price into v_central from pos.price
             where tenant_id = p_tenant and item_code = r->>'item_code' and effective_from <= p_device_clock
             order by effective_from desc limit 1;
            if v_central is not null and v_central <> (r->>'unit_price')::numeric then
                insert into pos.exception_queue (exception_id, tenant_id, kind, severity, device_id, idem_key, detail, raised_at)
                values (gen_random_uuid(), p_tenant, 'PRICE_VARIANCE',
                        case when abs(v_central - (r->>'unit_price')::numeric) > 5 then 'warn' else 'info' end,
                        p_device, p_idem_key,
                        jsonb_build_object('item_code', r->>'item_code',
                                           'device_price', (r->>'unit_price'), 'central_price', v_central::text,
                                           'variance', (v_central - (r->>'unit_price')::numeric)::text,
                                           'qty', v_qty::text,
                                           'reason_ar', 'سعر الجهاز يخالف السعر المركزي — الفاتورة سارية والفرق يُسجَّل'), v_now)
                on conflict (tenant_id, kind, coalesce(device_id,''), coalesce(detail->>'item_code',''))
                    where resolved_at is null and kind in ('STOCK_OVERSELL','PRICE_VARIANCE')
                do update set occurrences = pos.exception_queue.occurrences + 1,
                              raised_at = excluded.raised_at, detail = excluded.detail;
            end if;
        end loop;

        if v_cogs <> 0 then
            v_lines := jsonb_build_array(
                jsonb_build_object('line_no',1,'account','5101','desc','COGS',
                    'debit',  case when p_doc_type='RETURN' then '0.0000' else to_char(v_cogs,'FM9999999990.0000') end,
                    'credit', case when p_doc_type='RETURN' then to_char(v_cogs,'FM9999999990.0000') else '0.0000' end),
                jsonb_build_object('line_no',2,'account','1301','desc','Inventory',
                    'debit',  case when p_doc_type='RETURN' then to_char(v_cogs,'FM9999999990.0000') else '0.0000' end,
                    'credit', case when p_doc_type='RETURN' then '0.0000' else to_char(v_cogs,'FM9999999990.0000') end));
            perform pos.post_entry('POS:' || p_device, p_tenant, p_business_date,
                                   'COGS ' || p_invoice_no || ' @ ' || p_device, 'تكلفة البضاعة المباعة',
                                   p_device, p_idem_key || '#cogs', v_lines);
        end if;

        -- (7) إطلاق المرتجعات اليتيمة التي كانت تنتظر هذا البيع بالذات
        if p_doc_type = 'SALE' then
            select count(*) into v_released from pos.sale_inbox
             where original_idem_key = p_idem_key and status = 'quarantined';
        end if;

        outcome := 'posted';
        note := case when v_released > 0 then v_released || ' quarantined return(s) are now releasable' else '' end;
        entry_no := v_no; return next;
    end $fn$;

    -- إطلاق مرتجع محجوز بعد وصول بيعه الأصلي (خطوة صريحة، لا أثر جانبي صامت)
    create or replace function pos.release_orphan(p_idem_key text) returns text
    language plpgsql as $fn$
    declare v record; v_journal jsonb; v_entry uuid; v_no bigint; v_seq bigint;
    begin
        select * into v from pos.sale_inbox where idem_key = p_idem_key and status = 'quarantined';
        if not found then return 'not_quarantined'; end if;
        if not exists (select 1 from pos.sale_inbox o where o.idem_key = v.original_idem_key and o.status = 'posted')
            then return 'original_still_missing'; end if;

        v_journal := jsonb_build_array(
            jsonb_build_object('line_no',1,'account','4101','desc','revenue reversal',
                               'debit', to_char(v.total_net,'FM9999999990.0000'),'credit','0.0000'),
            jsonb_build_object('line_no',2,'account','2301','desc','vat reversal',
                               'debit', to_char(v.total_vat,'FM9999999990.0000'),'credit','0.0000'),
            jsonb_build_object('line_no',3,'account','1101','desc','cash out',
                               'debit','0.0000','credit', to_char(v.total_gross,'FM9999999990.0000')));

        select out_entry_id, out_entry_no, out_chain_seq into v_entry, v_no, v_seq
          from pos.post_entry('POS:' || v.device_id, v.tenant_id, v.business_date,
                              'RETURN ' || v.invoice_no || ' @ ' || v.device_id, 'مرتجع مُطلَق بعد وصول أصله',
                              v.device_id, p_idem_key, v_journal);
        update pos.sale_inbox set status = 'posted', entry_id = v_entry,
               note = 'released after the original sale arrived' where idem_key = p_idem_key;
        update pos.exception_queue set resolved_at = date_trunc('microsecond', clock_timestamp()),
               resolved_by = 'auto:original-arrived'
         where idem_key = p_idem_key and kind = 'ORPHAN_RETURN' and resolved_at is null;
        return 'released';
    end $fn$;

    -- ══════════════════════════════════════════════════════════════════════════
    -- التصميم المحظور: حارس تسلسل متزايد لكل حساب. هنا كي يُثبَت فشله، لا ليُستعمل.
    -- FORBIDDEN by design; present so the repository records WHY the rule exists.
    -- ══════════════════════════════════════════════════════════════════════════
    create or replace function pos.forbidden_apply(p_account text, p_amount numeric, p_seq bigint)
    returns int language plpgsql as $fn$
    declare n int;
    begin
        insert into pos.forbidden_balance (account_code, balance, applied_seq)
        values (p_account, 0, 0) on conflict (account_code) do nothing;
        update pos.forbidden_balance
           set balance = balance + p_amount, applied_seq = p_seq
         where account_code = p_account and applied_seq < p_seq;   -- ← الحارس القاتل
        get diagnostics n = row_count;
        return n;   -- صفر يعني: لم يُطبَّق شيء، وبلا أي خطأ
    end $fn$;
    """;

    public static async Task ApplyAsync()
    {
        await Sql.ExecAsync(Config.Admin, "create extension if not exists pgcrypto;");
        await Sql.ExecAsync(Config.Admin, Ddl);
        await Sql.ExecAsync(Config.Admin, Functions);
    }

    public static async Task<string> ServerFactsAsync()
    {
        await using var c = await Sql.OpenAsync(Config.Admin);
        var v = await Sql.ScalarAsync<string>(c, "select version()");
        var f = await Sql.ScalarAsync<string>(c, """
            select string_agg(name || '=' || setting, ', ' order by name) from pg_settings
            where name in ('synchronous_commit','fsync','wal_level','max_connections','shared_buffers','data_checksums')
            """);
        return $"{v}\n{f}";
    }
}
