-- ═══════════════════════════════════════════════════════════════════════════
-- ledger.post_entry — الترحيل **كمكالمة خادم واحدة**
--
-- لماذا إجراء مخزَّن ولا شيء أقلّ منه:
--   الشكل «الطبيعي» بلغة ORM (اقرأ العدّاد، اقرأ البصمة السابقة، أدرج، حدّث)
--   يحبس ~4 رحلات ذهاب وإياب **داخل معاملة تملك قفل صفّ متنازعاً عليه**.
--   السقف عندها 1/(4×RTT) وهو **مسطَّح تماماً مع التزامن**: 8.0 قيد/ث عند
--   RTT = 30 مللي‌ثانية، والرقم نفسه عند 8 و32 كاتباً — إضافة خوادم لا تُصلح
--   شيئاً. نفس العمل كمكالمة واحدة قيس عند 1,016.8 قيد/ث بـ32 كاتباً:
--   **فارق 127×** (فخ-14 · measurements.md §3.2).
--   القاعدة القابلة للفحص: **صفر رحلات ذهاب وإياب بين أخذ القفل والـCOMMIT.**
--
-- ─── الشكل القانوني: أين ينتهي C# وأين يبدأ SQL ───────────────────────────
-- البايتات المُجزَّأة تُنتَج **كلها** في C# عبر Babel.Canonicalization، باستثناء
-- ثلاثة سطور لا يمكن معرفتها قبل أخذ قفل العدّاد: chain_seq و prev_hash و entry_no.
-- ولذلك يصل الشكل القانوني إلى هنا **مقطوعاً في ثلاث قطع** بمواضع تلك السطور
-- بالضبط (CanonicalSplit في C#):
--
--     p_canon_prefix   babel.canon/v1 \n  +  سطر kind
--     ── هنا يُركَّب سطرا chain_seq و prev_hash ──
--     p_canon_head     tenant_id · book_id · fiscal_year · entry_id
--     ── هنا يُركَّب سطر entry_no ──
--     p_canon_tail     بقية الحقول والسطور وسطر end
--
-- والقطع الثلاث تُشتق في C# من ناتج المُوحِّد نفسه لا من قالب نصّي، ويثبّتها
-- اختبار تكافؤ بايتي يقارن ناتج هذه الدالة بناتج Canonicalizer.Compute على
-- عشرات التركيبات — أي انحراف يُفشل البناء.
-- ولا يقترب من البايتات هنا مُسلسِل ولا ToString واعٍ باللغة:
--   * bigint::text في PostgreSQL لا يعرف الثقافة ولا فاصل الآلاف.
--   * encode(bytea,'hex') يُنتج hex صغيراً — مطابقاً لما ينتجه C#.
-- والقيَم النقدية والزمنية والنصية كلها مُنسَّقة في C# قبل الوصول إلى هنا.
-- ═══════════════════════════════════════════════════════════════════════════
create or replace function ledger.post_entry(
    p_company_id             uuid,
    p_book_id                text,
    p_fiscal_year            int,
    p_entry_id               uuid,
    p_entry_date             date,
    p_period_code            text,
    p_posted_at              timestamptz,
    p_status                 text,
    p_actor                  text,
    p_actor_search           text,
    p_memo                   text,
    p_memo_ar                text,
    p_memo_ar_search         text,
    p_source_module          text,
    p_source_doc_type        text,
    p_source_doc_id          text,
    p_trigger_code           text,
    p_generation             int,
    p_event_code             text,
    p_idempotency_key        text,
    p_currency               text,
    p_reverses_entry_id      uuid,
    p_reversal_reason_ar     text,
    p_reversal_reason_en     text,
    p_closed_period_permission text,
    p_closed_period_authoriser text,
    p_canon_version          text,
    p_genesis_hash           bytea,
    p_canon_prefix           bytea,
    p_canon_head             bytea,
    p_canon_tail             bytea,
    p_line_id                uuid[],
    p_line_no                int[],
    p_account_code           text[],
    p_role_code              text[],
    p_qualifier              text[],
    p_debit                  numeric[],
    p_credit                 numeric[],
    p_debit_company          numeric[],
    p_credit_company         numeric[],
    p_fx_rate                numeric[],
    p_branch_id              text[],
    p_cost_center_id         text[],
    p_project_id             text[],
    p_property_id            text[],
    p_unit_id                text[],
    p_warehouse_id           text[],
    p_boq_item_id            text[],
    p_subledger_kind         text[],
    p_subledger_party_id     text[],
    p_line_description       text[],
    p_line_description_ar    text[],
    p_line_description_search text[],
    p_bal_account_code       text[],
    p_bal_debit              numeric[],
    p_bal_credit             numeric[]
)
returns table (
    out_entry_id        uuid,
    out_entry_no        bigint,
    out_chain_seq       bigint,
    out_entry_hash      bytea,
    out_prev_hash       bytea,
    out_canonical_bytes bytea,
    out_already_posted  boolean,
    out_line_rows       int,
    out_balance_rows    int
)
language plpgsql
as $fn$
declare
    v_entry_id    uuid;
    v_entry_no    bigint;
    v_chain_seq   bigint;
    v_prev_hash   bytea;
    v_hash        bytea;
    v_canon       bytea;
    v_seq_txt     text;
    v_no_txt      text;
    v_lines       int;
    v_bal_rows    int;
    v_affected    int;
    v_state       text;
    v_prev_entry  uuid;
begin
    -- ── 0-قبل) رمز الحدث جزء من الهوية، فلا يجوز أن يكون فارغاً ──────────
    -- الفحص **هنا** لا في C# وحده: هذه الدالة هي البوابة الوحيدة إلى الدفتر،
    -- وأي مستدعٍ آخر (سكربت، أداة، اختبار) يمرّ بها. ورمزٌ فارغ في مفتاح
    -- الهوية يُعيد حدثين مختلفين حدثاً واحداً فيبتلع الثاني بصمت (D-3).
    if p_event_code is null or length(btrim(p_event_code)) = 0 then
        raise exception
            'MISSING_EVENT_CODE doc=%/% trigger=% : رمز الحدث جزء من هوية الترحيل ولا يجوز أن يكون فارغاً / the event code is part of the posting identity and must not be empty',
            p_source_doc_type, p_source_doc_id, p_trigger_code
            using errcode = 'check_violation';
    end if;

    -- ── 0) فحص الإحكام قبل أي قفل ─────────────────────────────────────────
    -- مفتاح لكل قيد ومستقلّ عن الترتيب. لا مقارنة > ولا < مع تسلسل مُطبَّق:
    -- ذلك الحارس قيس وهو يُسقط بصمت قيداً وصل بعد أحدث منه (فخ-13).
    --
    -- و**رمز الحدث في الشرط** (D-3): بدونه كان هذا الاستعلام نفسه — لا الفهرس
    -- الفريد — هو ما يُسقط الحدث الثاني للمستند الواحد ويُرجع «مُرحَّل سلفاً».
    -- الفهرس لم يكن يُنتهَك أصلاً لأن التنفيذ لم يكن يصل إليه.
    select je.entry_id, je.entry_no, cl.chain_seq, cl.entry_hash, cl.prev_hash, cl.canonical_bytes
      into v_entry_id, v_entry_no, v_chain_seq, v_hash, v_prev_hash, v_canon
      from ledger.journal_entry je
      join ledger.chain_link   cl on cl.entry_id = je.entry_id
     where je.company_id           = p_company_id
       and je.source_doc_type      = p_source_doc_type
       and je.source_doc_id        = p_source_doc_id
       and je.posting_trigger_code = p_trigger_code
       and je.posting_generation   = p_generation
       and je.event_code           = p_event_code;

    if found then
        return query select v_entry_id, v_entry_no, v_chain_seq, v_hash, v_prev_hash, v_canon,
                            true, 0, 0;
        return;
    end if;

    -- ── 0-أ) رقابة الفترة المالية ─────────────────────────────────────────
    -- الترحيل في فترة مقفلة **مرفوض افتراضاً**. والاستثناء ليس علماً منطقياً
    -- (bool force) بل إذن موثَّق: من أذن وبأي صلاحية — ويُكتب في سجل العمليات
    -- داخل المعاملة نفسها. إذنٌ لا أثر له في السجل ليس إذناً بل ثغرة.
    -- والفحص هنا لا في C# وحده: الفحص في العميل يسابق إغلاق الفترة؛ وهنا هو
    -- ذرّي مع الكتابة.
    select fp.state into v_state
      from ledger.fiscal_period fp
     where fp.company_id = p_company_id and fp.period_code = p_period_code;

    if not found then
        raise exception
            'NO_FISCAL_PERIOD company=% period=% : لا فترة مالية بهذا الرمز — الترحيل خارج التقويم المالي مرفوض / no fiscal period with this code',
            p_company_id, p_period_code
            using errcode = 'no_data_found';
    end if;

    if v_state = 'permanently_closed' then
        raise exception
            'PERIOD_PERMANENTLY_CLOSED period=% : الفترة مقفلة نهائياً ولا يفتحها إذن استثنائي / the period is permanently closed and no exceptional permission reopens it',
            p_period_code
            using errcode = 'check_violation';
    end if;

    if v_state = 'closed' then
        if p_closed_period_permission is null or p_closed_period_authoriser is null then
            raise exception
                'PERIOD_CLOSED period=% : الترحيل في فترة مقفلة مرفوض بلا إذن استثنائي موثَّق / posting into a closed period is refused without a documented exceptional permission',
                p_period_code
                using errcode = 'check_violation';
        end if;

        insert into ledger.process_event (
            company_id, kind, outcome, actor, event_code, source_doc_type, source_doc_id,
            reason_code, message_ar, message_en, detail)
        values (p_company_id, 'posting.closed_period', 'allowed_by_permission', p_actor, p_event_code,
                p_source_doc_type, p_source_doc_id, 'CLOSED_PERIOD_OVERRIDE',
                'ترحيل في فترة مقفلة بإذن استثنائي موثَّق',
                'Posting into a closed period under a documented exceptional permission',
                format('period=%s permission=%s authoriser=%s entry=%s',
                       p_period_code, p_closed_period_permission, p_closed_period_authoriser, p_entry_id));
    end if;

    -- ── 0-ب) جيل الترحيل: لا يزيد إلا بعد عكس مشروع ───────────────────────
    -- «ترحيل ← عكس ← تصحيح ← إعادة ترحيل» مسار مشروع؛ أما زيادة الجيل بلا عكس
    -- سابق فهي الالتفاف على مفتاح الإحكام نفسه، وتُنتج قيدين لنفس المستند.
    if p_generation > 1 then
        -- والسلف يُطلب **لنفس الحدث**: جيل جديد لحدث الإيراد لا يُبرّره عكسٌ
        -- وقع على حدث التكلفة، وهما قيدان مستقلان بهويتين مستقلتين (D-3).
        select je.entry_id into v_prev_entry
          from ledger.journal_entry je
         where je.company_id           = p_company_id
           and je.source_doc_type      = p_source_doc_type
           and je.source_doc_id        = p_source_doc_id
           and je.posting_trigger_code = p_trigger_code
           and je.event_code           = p_event_code
           and je.posting_generation   = p_generation - 1;

        if not found then
            raise exception
                'GENERATION_WITHOUT_PREDECESSOR doc=%/% generation=% : لا جيل سابق لهذا المستند / no predecessor generation for this document',
                p_source_doc_type, p_source_doc_id, p_generation
                using errcode = 'check_violation';
        end if;

        if not exists (select 1 from ledger.journal_entry r
                        where r.reverses_entry_id = v_prev_entry and r.status = 'REVERSAL') then
            raise exception
                'GENERATION_WITHOUT_REVERSAL doc=%/% generation=% : الجيل السابق لم يُعكس — الجيل لا يزيد إلا بعد عكس مشروع / the previous generation was never reversed',
                p_source_doc_type, p_source_doc_id, p_generation
                using errcode = 'check_violation';
        end if;
    end if;

    -- ── 1) العدّاد بلا فجوات: قفل صفّ، لا SEQUENCE ────────────────────────
    select pc.next_entry_no, pc.next_chain_seq
      into v_entry_no, v_chain_seq
      from ledger.posting_counter pc
     where pc.company_id  = p_company_id
       and pc.book_id     = p_book_id
       and pc.fiscal_year = p_fiscal_year
       for update;

    if not found then
        raise exception
            'NO_COUNTER_ROW company=% book=% year=% : لا صفّ عدّاد لهذا النطاق / no counter row for this scope',
            p_company_id, p_book_id, p_fiscal_year
            using errcode = 'no_data_found';
    end if;

    -- ── 2) إعادة فحص الإحكام **تحت القفل** ────────────────────────────────
    -- كتّاب الدفتر الواحد يتسلسلون على هذا الصفّ بالتصميم، فالفحص هنا يحسم
    -- السباق. ولم يُمسّ العدّاد بعد: SELECT ... FOR UPDATE وحده لا يزيده،
    -- فالخروج الآن **لا يُحدث فجوة**.
    select je.entry_id, je.entry_no, cl.chain_seq, cl.entry_hash, cl.prev_hash, cl.canonical_bytes
      into v_entry_id, v_entry_no, v_chain_seq, v_hash, v_prev_hash, v_canon
      from ledger.journal_entry je
      join ledger.chain_link   cl on cl.entry_id = je.entry_id
     where je.company_id           = p_company_id
       and je.source_doc_type      = p_source_doc_type
       and je.source_doc_id        = p_source_doc_id
       and je.posting_trigger_code = p_trigger_code
       and je.posting_generation   = p_generation
       and je.event_code           = p_event_code;

    if found then
        return query select v_entry_id, v_entry_no, v_chain_seq, v_hash, v_prev_hash, v_canon,
                            true, 0, 0;
        return;
    end if;

    select pc.next_entry_no, pc.next_chain_seq
      into v_entry_no, v_chain_seq
      from ledger.posting_counter pc
     where pc.company_id  = p_company_id
       and pc.book_id     = p_book_id
       and pc.fiscal_year = p_fiscal_year;

    -- ── 3) البصمة السابقة، تُقرأ تحت القفل نفسه ───────────────────────────
    select cl.entry_hash into v_prev_hash
      from ledger.chain_link cl
     where cl.company_id  = p_company_id
       and cl.book_id     = p_book_id
       and cl.fiscal_year = p_fiscal_year
       and cl.chain_seq   = v_chain_seq - 1;

    if v_prev_hash is null then
        -- بصمة التكوين للنطاق، محسوبة في C# عبر المكتبة نفسها.
        v_prev_hash := p_genesis_hash;
    end if;

    -- ── 4) البايتات القانونية والبصمة ─────────────────────────────────────
    -- chain_seq و prev_hash **داخل** البايتات المُجزَّأة لا في عمودين مجاورين:
    -- الرابط في عمود مجاور يجعل السلسلة زخرفية — يعيد المالك كتابة العمود
    -- وتبقى كل بصمة فردية صحيحة (فخ-22 · ADR-0007).
    -- و entry_no كذلك داخلها: الرقم المتسلسل بلا فجوات هو ما يقرؤه المدقّق،
    -- ورقمٌ خارج التجزئة رقمٌ قابل لإعادة الكتابة.
    v_seq_txt := v_chain_seq::text;
    v_no_txt  := v_entry_no::text;
    v_canon := p_canon_prefix
            || convert_to(
                   'chain_seq' || chr(9) || 'I' || chr(9) || octet_length(v_seq_txt)::text || chr(9) || v_seq_txt || chr(10)
                || 'prev_hash' || chr(9) || 'B' || chr(9) || '64' || chr(9) || encode(v_prev_hash, 'hex') || chr(10),
                   'UTF8')
            || p_canon_head
            || convert_to(
                   'entry_no' || chr(9) || 'I' || chr(9) || octet_length(v_no_txt)::text || chr(9) || v_no_txt || chr(10),
                   'UTF8')
            || p_canon_tail;
    v_hash := sha256(v_canon);

    -- ── 5) رأس القيد ──────────────────────────────────────────────────────
    -- الفاعل والمصدر والمستند أعمدة في الرأس لا بيانات وصفية جانبية (فخ-07).
    insert into ledger.journal_entry (
        entry_id, company_id, book_id, fiscal_year, entry_no, entry_date, period_code,
        posted_at, status, actor, actor_search, memo, memo_ar, memo_ar_search,
        source_module, source_doc_type, source_doc_id, posting_trigger_code, posting_generation,
        event_code, idempotency_key, currency, reverses_entry_id,
        reversal_reason_ar, reversal_reason_en, closed_period_permission, closed_period_authoriser)
    values (
        p_entry_id, p_company_id, p_book_id, p_fiscal_year, v_entry_no, p_entry_date, p_period_code,
        p_posted_at, p_status, p_actor, p_actor_search, p_memo, p_memo_ar, p_memo_ar_search,
        p_source_module, p_source_doc_type, p_source_doc_id, p_trigger_code, p_generation,
        p_event_code, p_idempotency_key, p_currency, p_reverses_entry_id,
        p_reversal_reason_ar, p_reversal_reason_en, p_closed_period_permission, p_closed_period_authoriser);

    get diagnostics v_affected = row_count;
    if v_affected <> 1 then
        raise exception 'ENTRY_ROWCOUNT_MISMATCH expected 1 affected % / عدد صفوف الرأس مخالف', v_affected
            using errcode = 'check_violation';
    end if;

    -- ── 6) السطور ─────────────────────────────────────────────────────────
    insert into ledger.journal_line (
        line_id, entry_id, line_no, company_id, account_code, role_code, qualifier,
        debit, credit, currency, fx_rate, debit_company, credit_company,
        branch_id, cost_center_id, project_id, property_id, unit_id, warehouse_id, boq_item_id,
        subledger_kind, subledger_party_id, description, description_ar, description_ar_search)
    select l.line_id, p_entry_id, l.line_no, p_company_id, l.account_code, l.role_code, l.qualifier,
           l.debit, l.credit, p_currency, l.fx_rate, l.debit_company, l.credit_company,
           l.branch_id, l.cost_center_id, l.project_id, l.property_id, l.unit_id, l.warehouse_id, l.boq_item_id,
           l.subledger_kind, l.subledger_party_id, l.descr, l.descr_ar, l.descr_search
      from unnest(p_line_id, p_line_no, p_account_code, p_role_code, p_qualifier,
                  p_debit, p_credit, p_debit_company, p_credit_company, p_fx_rate,
                  p_branch_id, p_cost_center_id, p_project_id, p_property_id, p_unit_id,
                  p_warehouse_id, p_boq_item_id,
                  p_subledger_kind, p_subledger_party_id,
                  p_line_description, p_line_description_ar, p_line_description_search)
             as l(line_id, line_no, account_code, role_code, qualifier,
                  debit, credit, debit_company, credit_company, fx_rate,
                  branch_id, cost_center_id, project_id, property_id, unit_id,
                  warehouse_id, boq_item_id,
                  subledger_kind, subledger_party_id, descr, descr_ar, descr_search);

    get diagnostics v_lines = row_count;
    if v_lines <> coalesce(array_length(p_line_no, 1), 0) then
        raise exception 'LINE_ROWCOUNT_MISMATCH expected % affected % / عدد صفوف السطور مخالف',
            coalesce(array_length(p_line_no, 1), 0), v_lines
            using errcode = 'check_violation';
    end if;

    -- ── 7) حلقة السلسلة ───────────────────────────────────────────────────
    insert into ledger.chain_link (
        company_id, book_id, fiscal_year, chain_seq, entry_id,
        canon_version, prev_hash, entry_hash, canonical_bytes)
    values (p_company_id, p_book_id, p_fiscal_year, v_chain_seq, p_entry_id,
            p_canon_version, v_prev_hash, v_hash, v_canon);

    get diagnostics v_affected = row_count;
    if v_affected <> 1 then
        raise exception 'CHAIN_ROWCOUNT_MISMATCH expected 1 affected %', v_affected
            using errcode = 'check_violation';
    end if;

    -- ── 8) الأرصدة: **عبارة واحدة بالضبط**، صفوفها مرتّبة، وعددها مؤكَّد ──
    -- ثلاث قواعد مقيسة في عبارة واحدة:
    --  * INSERT ... ON CONFLICT DO UPDATE لا UPDATE مجرّد: الـUPDATE المجرّد على
    --    فترة لم تُنشأ صفوفها يُصيب صفر صفوف، ولا يرفع شيئاً، ويُثبِّت المعاملة —
    --    قيس: 500.0000 ريال رُحّلت و balance_rows = 0 بلا أي خطأ (فخ-09).
    --    وموعده معروف سلفاً: أول ترحيل في كل فترة جديدة لكل مستأجر.
    --  * ORDER BY account_code صراحةً: نفس العبارة بصفوف غير مرتّبة قيست عند
    --    0.161 مقابل 1,841.3 معاملة/ث — انهيار ~11,000× مع 22–35 جموداً (فخ-10).
    --  * ولا UPDATE ... FROM (استعلام فرعي): المخطِّط حرّ في إعادة ترتيب الأقفال
    --    حتى مع ORDER BY داخل الاستعلام الفرعي (فخ-11).
    insert into ledger.account_balance as ab (
        company_id, book_id, period_code, account_code, debit, credit, entry_count, updated_at)
    select p_company_id, p_book_id, p_period_code, b.code, b.d, b.c, 1, p_posted_at
      from unnest(p_bal_account_code, p_bal_debit, p_bal_credit) as b(code, d, c)
     order by b.code
    on conflict (company_id, book_id, period_code, account_code) do update set
        debit       = ab.debit  + excluded.debit,
        credit      = ab.credit + excluded.credit,
        entry_count = ab.entry_count + 1,
        updated_at  = excluded.updated_at;

    get diagnostics v_bal_rows = row_count;
    if v_bal_rows <> coalesce(array_length(p_bal_account_code, 1), 0) then
        raise exception 'BALANCE_ROWCOUNT_MISMATCH expected % affected % / عدد صفوف الأرصدة مخالف — المعاملة تُجهَض',
            coalesce(array_length(p_bal_account_code, 1), 0), v_bal_rows
            using errcode = 'check_violation';
    end if;

    -- ── 9) تقديم العدّاد ──────────────────────────────────────────────────
    update ledger.posting_counter
       set next_entry_no  = next_entry_no  + 1,
           next_chain_seq = next_chain_seq + 1
     where company_id  = p_company_id
       and book_id     = p_book_id
       and fiscal_year = p_fiscal_year;

    get diagnostics v_affected = row_count;
    if v_affected <> 1 then
        raise exception 'COUNTER_ROWCOUNT_MISMATCH expected 1 affected %', v_affected
            using errcode = 'check_violation';
    end if;

    return query select p_entry_id, v_entry_no, v_chain_seq, v_hash, v_prev_hash, v_canon,
                        false, v_lines, v_bal_rows;
end
$fn$;
