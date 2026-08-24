namespace Babel.Canonicalization.Schemas;

/// <summary>
/// المخطّط المرجعي: قيد اليومية، إصدار <b>v2</b>.
///
/// <para>
/// <b>الثغرة التي أُغلقت.</b> مخطّط v1 كان يغطّي من السطر: رقمه، والحساب، والمدين،
/// والدائن، و<c>cost_center</c>، والوصف. ولا شيء غير ذلك. أي أن مالك قاعدة
/// البيانات كان يستطيع:
/// <list type="bullet">
///   <item>‏<c>update ledger.journal_line set property_id = 'P-B' where property_id = 'P-A'</c>
///         — فتنتقل حركة من عقار إلى عقار، وتنقلب ربحية اثنين، ويتغيّر كشف مالك،
///         <b>و<c>VerifyChain</c> يقول «سليمة»</b>؛</item>
///   <item>والشيء نفسه في <c>warehouse_id</c> و<c>project_id</c> و<c>branch_id</c>
///         و<c>boq_item_id</c> و<c>unit_id</c>؛</item>
///   <item>و<c>role_code</c> — أي إعادة كتابة «لماذا وُلِّد هذا السطر»، وهي الحقيقة
///         التي تُفرض عليها GR-RE-001 في قاعدة البيانات؛</item>
///   <item>و<c>debit_company</c>/<c>credit_company</c> — <b>المبالغ التي يُبنى منها
///         ميزان المراجعة فعلاً</b> ويفحصها المشغّل المؤجَّل، لا مبالغ عملة الحركة.</item>
/// </list>
/// وكلها تُبقي <c>debit = credit</c>، فلا يطلق أي فحص محاسبي ولا أي قيد قاعدة بيانات.
/// </para>
///
/// <para>
/// <b>المبدأ المطبَّق هنا</b>: كل حقل تغييرُه يغيّر <b>المعنى المحاسبي</b> للقيد
/// يدخل البايتات المُجزَّأة. وقائمة الحقول مشتقّة من <b>أعمدة الجدولين الفعليين</b>
/// (<c>ledger.journal_entry</c> و<c>ledger.journal_line</c>) عموداً عموداً، لا من
/// قائمة v1. ومجموعة الاستثناء أدناه <b>أُعيد اشتقاقها من الصفر</b>: كل عمود لم
/// يدخل مذكور فيها باسمه وسببه، والسؤال المطبَّق على كل واحد هو:
/// <i>لو غيّره مالك قاعدة البيانات وبقيت السلسلة خضراء، هل يهمّ ذلك؟</i>
/// </para>
///
/// <para>
/// <b>الفروق البنيوية عن v1</b>، وكلها تشديد لا تخفيف:
/// <list type="number">
///   <item><b>الاختياري يُضبط صراحةً</b> (<see cref="CanonicalSchema.RequireExplicitOptionals"/>).
///         في v1 كان الحقل الاختياري غير المضبوط يصير <c>Null</c> ضمناً، فلا يُفرَّق
///         «بُعدٌ نُسِي في مسار البناء» عن «بُعدٌ غائب فعلاً». في v2 الأول <b>يُرفض</b>.</item>
///   <item><b>نوع <c>R</c> لسعر الصرف</b> بمقياس 8، مطابق لـ<c>numeric(19,8)</c>.
///         تمريره عبر قواعد المبلغ (مقياس 4) كان سيرفض <c>3.75123456</c> أو يقرّبه،
///         وكلاهما عطب.</item>
///   <item><b>اختيارية الحقل تعكس قابلية العمود للغياب حرفياً</b>: عمود
///         <c>NOT NULL</c> يقابله حقل مطلوب، وعمود <c>NULL</c>-able يقابله حقل
///         اختياري يُضبط صراحةً. فلا توجد قيمة قانونية لا تستطيع قاعدة البيانات
///         تخزينها.</item>
///   <item><b><c>source_ref</c> المدموج انقسم</b> إلى <c>source_doc_type</c> و
///         <c>source_doc_id</c> منفصلين — كما هما في الجدول. الدمج بشرطة مائلة في v1
///         كان يجعل <c>("A/B","C")</c> و<c>("A","B/C")</c> يعطيان البايتات نفسها.</item>
/// </list>
/// </para>
/// </summary>
public static partial class JournalEntrySchema
{
    /// <summary>المخطّط المجمَّد للإصدار v2.</summary>
    public static readonly CanonicalSchema V2 = new(
        Kind,
        CanonicalV2.Version,
        fields:
        [
            // ═══════════ هوية القيد ونطاق السلسلة ═══════════
            // tenant_id = ledger.journal_entry.company_id. تغييره ينقل القيد كلّه
            // إلى شركة أخرى؛ وهو أيضاً جزء من نطاق السلسلة وبصمة التكوين.
            new SchemaField("tenant_id", CanonicalKind.Text),
            new SchemaField("book_id", CanonicalKind.Text),
            new SchemaField("fiscal_year", CanonicalKind.Integer),
            new SchemaField("entry_id", CanonicalKind.Uuid),

            // entry_no: الرقم المتسلسل بلا فجوات الذي يقرؤه المدقّق. يُركَّب في
            // الخادم تحت قفل العدّاد، ولذلك موضعه هنا — قبل بقية الحقول مباشرة —
            // هو موضع القطع الثالث في CanonicalSplit.
            new SchemaField("entry_no", CanonicalKind.Integer),

            // ═══════════ التواريخ والفترة ═══════════
            // entry_date: التاريخ المحاسبي (متى وقع الحدث).
            new SchemaField("entry_date", CanonicalKind.Date),
            // period_code: الفترة التي وقع فيها القيد. نقل قيد من فترة إلى فترة
            // ينقل ربحاً بين شهرين وبين إقرارين ضريبيين، والمبالغ لا تتغيّر.
            new SchemaField("period_code", CanonicalKind.Text),
            // posted_at: لحظة العلم (متى عرفنا)، مقصوصة إلى الميكروثانية.
            new SchemaField("posted_at", CanonicalKind.Instant),

            // ═══════════ الحالة ورابط العكس ═══════════
            new SchemaField("status", CanonicalKind.Token),
            // reverses_entry_id: القيد الذي يعكسه هذا. إعادة توجيهه تجعل قيد عكس
            // «يعكس» قيداً آخر، فيبدو الأصل قائماً وقد أُلغي فعلاً.
            new SchemaField("reverses_entry_id", CanonicalKind.Uuid, Required: false),
            new SchemaField("reversal_reason_ar", CanonicalKind.Text, Required: false),
            new SchemaField("reversal_reason_en", CanonicalKind.Text, Required: false),

            // ═══════════ المصدر والسببية ═══════════
            // من أين جاء القيد ولماذا. هذه هي إجابة «لماذا هذا القيد موجود؟»،
            // وإعادة كتابتها تفصل القيد عن مستنده الأصلي بلا أثر.
            new SchemaField("source_module", CanonicalKind.Text),
            new SchemaField("source_doc_type", CanonicalKind.Text),
            new SchemaField("source_doc_id", CanonicalKind.Text),
            new SchemaField("posting_trigger_code", CanonicalKind.Text),
            // posting_generation: الجيل. مع (المستند × الإطلاق) يُكوِّن مفتاح
            // الإحكام نفسه — وهو ما يمنع قيدين لمستند واحد.
            new SchemaField("posting_generation", CanonicalKind.Integer),
            // event_code: نوع الحدث المحاسبي الذي وُلِّد منه القيد عبر المصفوفة.
            new SchemaField("event_code", CanonicalKind.Text),
            new SchemaField("idempotency_key", CanonicalKind.Text),

            // ═══════════ العملة ═══════════
            new SchemaField("currency", CanonicalKind.Token),

            // ═══════════ الفاعل والإذن ═══════════
            // actor: من أنشأ القيد (created_by). مقابله actor_search مستثنى.
            new SchemaField("actor", CanonicalKind.Text),
            // closed_period_*: الإذن الاستثنائي بالترحيل في فترة مقفلة ومن أذن به
            // (approved_by). إذنٌ يُعاد كتابته بعد الترحيل ليس إذناً بل غطاء.
            new SchemaField("closed_period_permission", CanonicalKind.Text, Required: false),
            new SchemaField("closed_period_authoriser", CanonicalKind.Text, Required: false),

            // ═══════════ البيان ═══════════
            // العمودان NOT NULL DEFAULT '' في الجدول، فالحقلان **مطلوبان** هنا:
            // اختيارية الحقل في v2 تعكس قابلية العمود للغياب حرفياً، فلا توجد قيمة
            // قانونية لا تُخزَّن (SPEC §7.4: قيمة لا تُخزَّن لا يجوز أن تُجزَّأ).
            new SchemaField("memo", CanonicalKind.Text),
            new SchemaField("memo_ar", CanonicalKind.Text),

            // ═══════════ السطور ═══════════
            new SchemaField("lines", CanonicalKind.Group, Required: true, GroupFields:
            [
                new SchemaField("line_no", CanonicalKind.Integer),

                // account_code: الحساب الذي حُلَّ من الدور.
                new SchemaField("account_code", CanonicalKind.Text),
                // role_code + qualifier: **لماذا** وُلِّد السطر وبأي مؤهّل حُلَّ
                // الحساب. الزوج (دور × مؤهّل) هو مفتاح role_account_map، وإعادة
                // كتابته تجعل السطر يدّعي أصلاً غير أصله — وعليه تُفرض GR-RE-001.
                new SchemaField("role_code", CanonicalKind.Text),
                new SchemaField("qualifier", CanonicalKind.Text),

                // المبالغ بعملة الحركة.
                new SchemaField("debit", CanonicalKind.Amount),
                new SchemaField("credit", CanonicalKind.Amount),
                new SchemaField("currency", CanonicalKind.Token),
                new SchemaField("fx_rate", CanonicalKind.Rate),

                // المبالغ **بعملة الشركة** — وهي التي يُبنى منها ميزان المراجعة
                // ويفحصها المشغّل المؤجَّل عند COMMIT. تركها خارج التجزئة في v1
                // كان يعني أن الأرقام التي تُقرأ في القوائم المالية غير موقَّعة.
                new SchemaField("debit_company", CanonicalKind.Amount),
                new SchemaField("credit_company", CanonicalKind.Amount),

                // ═══ الأبعاد التحليلية ═══
                // كل واحد منها يجيب «على مَن يقع هذا المبلغ؟». تغيير أيٍّ منها
                // ينقل المبلغ من تقرير إلى تقرير بلا كسر أي توازن.
                new SchemaField("branch_id", CanonicalKind.Text, Required: false),
                new SchemaField("cost_center_id", CanonicalKind.Text, Required: false),
                new SchemaField("project_id", CanonicalKind.Text, Required: false),
                new SchemaField("property_id", CanonicalKind.Text, Required: false),
                new SchemaField("unit_id", CanonicalKind.Text, Required: false),
                new SchemaField("warehouse_id", CanonicalKind.Text, Required: false),
                new SchemaField("boq_item_id", CanonicalKind.Text, Required: false),

                // tax_code: رمز المعالجة الضريبية للسطر. **الفتحة محجوزة ومُجزَّأة
                // من اليوم الأول**: لا عمود له في ledger.journal_line اليوم، فيُكتب
                // Null صراحةً. ولو أُضيف العمود لاحقاً دخلت قيمته البايتات المُوقَّعة
                // بلا حاجة إلى v3 — والبديل (تأجيل الحقل) يعني إصداراً ثالثاً كاملاً
                // لأول رمز ضريبي يُخزَّن.
                new SchemaField("tax_code", CanonicalKind.Text, Required: false),

                // ═══ الدفتر المساعد ═══
                // «هذا المبلغ على مَن؟» — عميل بعينه أو مورّد بعينه. تغيير الطرف
                // ينقل ديناً من ذمّة إلى ذمّة.
                new SchemaField("subledger_kind", CanonicalKind.Text),
                new SchemaField("subledger_party_id", CanonicalKind.Text, Required: false),

                // ═══ بيان السطر بلغتيه ═══ (العمودان NOT NULL DEFAULT '')
                new SchemaField("description", CanonicalKind.Text),
                new SchemaField("description_ar", CanonicalKind.Text)
            ])
        ],
        exclusions:
        [
            // ═══════════════════════════════════════════════════════════════
            //  مجموعة الاستثناء — **مُشتقّة من الصفر لـv2، لا موروثة عن v1**
            //
            //  الاختبار المطبَّق على كل عمود من عمودَي الجدولين:
            //     «لو غيّره مالك قاعدة البيانات وبقيت السلسلة خضراء، هل يهمّ؟»
            //  إن كانت الإجابة نعم فهو **في** المخطّط أعلاه. وما دون ذلك هنا،
            //  باسمه وسببه، وبصمة المخطّط تشمله فلا يُحذف ولا يُضاف صامتاً.
            // ═══════════════════════════════════════════════════════════════

            // ── 1) بصمة السجل نفسه وحلقة السلسلة ──────────────────────────
            new ExcludedField("entry_hash", ExclusionReason.SelfHash,
                "بصمة السجل لا تستطيع أن تجزّئ نفسها. تُخزَّن بجوار canon_version وتُعاد حسابها عند التحقق."),
            new ExcludedField("canon_version", ExclusionReason.SelfHash,
                "يُحمل في ترويسة الشكل السلكي نفسه (babel.canon/v2) لا كحقل، ويُخزَّن بجوار البصمة في عمود مستقلّ. " +
                "وهو ما يوزّع كل سجل على مُوحِّد إصداره عند التحقق."),
            new ExcludedField("prev_hash", ExclusionReason.SelfHash,
                "مكتوب في **ترويسة** البايتات المُجزَّأة (سطر prev_hash) لا كحقل مستند. " +
                "إدراجه حقلاً أيضاً يكتبه مرّتين ويوهم بحمايةٍ لا يضيفها."),
            new ExcludedField("chain_seq", ExclusionReason.SelfHash,
                "مكتوب في ترويسة البايتات المُجزَّأة (سطر chain_seq)، كما يحمل ZATCA عدّاد الفاتورة داخل الجسم الموقَّع."),
            new ExcludedField("canonical_bytes", ExclusionReason.SelfHash,
                "البايتات القانونية نفسها. تخزينها للحفظ لا للتجزئة؛ وإعادة التحقق تُعيد بناء المستند " +
                "من الحقيقة المجالية لا من هذا العمود — مقارنة البايتات بنفسها لا تُثبت شيئاً."),

            // ── 2) الأعمدة المشتقّة للبحث — الأخطر في القائمة كلها ─────────
            new ExcludedField("memo_ar_search", ExclusionReason.SearchNormalised,
                "ناتج ArabicSearch.Normalize: يزيل التطويل ويوحّد أشكال الألف والتاء المربوطة. " +
                "تجزئته تعني أن تغيير قواعد البحث بعد سنتين يُبطل كل تحقّق سابق."),
            new ExcludedField("actor_search", ExclusionReason.SearchNormalised,
                "مطبَّع للبحث؛ مشتقّ من actor المُوقَّع. الأصل مُجزَّأ والمشتقّ حرّ في التغيّر."),
            new ExcludedField("description_ar_search", ExclusionReason.SearchNormalised,
                "مشتقّ من description_ar المُوقَّع على مستوى السطر — نفس المصدر ونفس الخطر."),
            new ExcludedField("account_name_search", ExclusionReason.SearchNormalised,
                "مشتقّ من اسم الحساب في جدول مرجعي؛ يُعاد بناؤه عند تغيّر قواعد البحث."),
            new ExcludedField("search_vector", ExclusionReason.SearchNormalised,
                "tsvector يتغيّر بتغيّر إعداد النص الكامل في PostgreSQL ومع ترقيات الخادم."),

            // ── 3) مفاتيح بديلة وتكرار بنيوي ──────────────────────────────
            new ExcludedField("line_id", ExclusionReason.SurrogateKey,
                "معرّف صفّ السطر (uuid v7) يُولَّد عند الإدراج ولا يحمل معنى محاسبياً. " +
                "هوية السطر المحاسبية هي (entry_id × line_no)، وكلاهما مُجزَّأ: entry_id في الرأس، " +
                "و line_no في السطر، وترتيب السطور نفسه جزء من البايتات. " +
                "وإعادة كتابته لا تغيّر أي رقم ولا أي بُعد ولا أي حساب."),
            new ExcludedField("line_entry_id", ExclusionReason.DenormalisedDuplicate,
                "ledger.journal_line.entry_id مكرّر من رأس القيد ومن بنية المجموعة نفسها: " +
                "السطر مُجزَّأ **داخل** مستند القيد، فانتماؤه مكتوب في الشكل السلكي بالبناء. " +
                "ونقله إلى قيد آخر يُسقطه من بايتات قيده الأصلي فيُكشف فوراً بـCHAIN-CONTENT-TAMPERED."),
            new ExcludedField("line_company_id", ExclusionReason.DenormalisedDuplicate,
                "ledger.journal_line.company_id مكرّر من رأس القيد (tenant_id المُجزَّأ) ووجوده " +
                "لأجل المفتاح الأجنبي المركّب على الحساب وحده. الشركة الحقيقية للسطر هي شركة قيده."),

            // ── 4) قيم الإسقاط: تُعاد بناؤها من السطور المُجزَّأة ───────────
            new ExcludedField("running_balance", ExclusionReason.ProjectionDerived,
                "رصيد متحرّك محسوب من السطور؛ يُعاد بناؤه، فلا يجوز أن يقيّد السلسلة."),
            new ExcludedField("total_debit", ExclusionReason.ProjectionDerived,
                "مجموع مشتقّ من السطور المُجزَّأة أصلاً. تجزئته تكرار يخلق مصدرين للحقيقة."),
            new ExcludedField("total_credit", ExclusionReason.ProjectionDerived,
                "مجموع مشتقّ من السطور المُجزَّأة أصلاً، كسابقه: مصدر الحقيقة هو السطور، والمجموع يُعاد حسابه منها."),
            new ExcludedField("line_count", ExclusionReason.ProjectionDerived,
                "عدد السطور مكتوب أصلاً في سطر المجموعة G داخل البايتات القانونية."),
            new ExcludedField("account_balance_snapshot", ExclusionReason.ProjectionDerived,
                "لقطة إسقاط تُعاد بناؤها عند إعادة تشغيل الإسقاطات."),
            new ExcludedField("entry_count", ExclusionReason.ProjectionDerived,
                "عدّاد في ledger.account_balance؛ إسقاط لا حقيقة."),

            // ── 5) بيانات تشغيلية متغيّرة بطبيعتها ────────────────────────
            new ExcludedField("row_version", ExclusionReason.OperationalMetadata,
                "xmin أو عمود تزامن متفائل؛ يتغيّر مع كل VACUUM FULL و pg_dump/restore."),
            new ExcludedField("db_inserted_at", ExclusionReason.OperationalMetadata,
                "ساعة الخادم لا ساعة الحدث. posted_at هو المُوقَّع، ويُلتقط مرّة واحدة."),
            new ExcludedField("updated_at", ExclusionReason.OperationalMetadata,
                "سجل يُضاف إليه فقط لا يُحدَّث؛ وجود العمود لأغراض تشغيلية لا يجعله جزءاً من الحقيقة."),
            new ExcludedField("outbox_status", ExclusionReason.OperationalMetadata,
                "حالة صندوق الصادر تتغيّر بعد الترحيل بطبيعتها."),
            new ExcludedField("sync_state", ExclusionReason.OperationalMetadata,
                "حالة مزامنة نقاط البيع؛ متغيّرة عمداً."),
            new ExcludedField("zatca_submission_status", ExclusionReason.OperationalMetadata,
                "حالة الإرسال تتغيّر بعد الترحيل؛ المصنوع المختوم يُخزَّن كما أُرسل ولا يُعاد توليده."),
            new ExcludedField("retry_count", ExclusionReason.OperationalMetadata,
                "عدّاد إعادة محاولة التسليم؛ يتغيّر بعد الترحيل بطبيعته ولا يمسّ أي رقم محاسبي."),
            new ExcludedField("process_event_id", ExclusionReason.OperationalMetadata,
                "معرّف سجل العمليات (identity always). سجلّ الرقابة يجاور القيد ولا يدخله."),

            // ── 6) قياس عن بُعد ───────────────────────────────────────────
            new ExcludedField("client_ip", ExclusionReason.Telemetry,
                "قد يتغيّر بإعادة كتابة السجلات عند تصحيح خصوصية، ويُخفى في التصدير."),
            new ExcludedField("user_agent", ExclusionReason.Telemetry,
                "سلسلة عميل متغيّرة ولا علاقة لها بالحقيقة المحاسبية."),
            new ExcludedField("session_id", ExclusionReason.Telemetry,
                "معرّف جلسة عابر ينتهي بانتهائها؛ لا يُعاد إنتاجه ولا يحمل معنى بعد ساعة."),
            new ExcludedField("trace_id", ExclusionReason.Telemetry,
                "معرّف تتبّع موزّع؛ يتغيّر عند إعادة المعالجة."),

            // ── 7) عرض ───────────────────────────────────────────────────
            new ExcludedField("display_order", ExclusionReason.Presentation,
                "ترتيب عرض في الواجهة؛ ترتيب السطور المُجزَّأ هو ترتيب المجموعة و line_no."),
            new ExcludedField("printed_count", ExclusionReason.Presentation,
                "عدّاد مرّات الطباعة؛ يزيد بمجرّد فتح تقرير، ولا علاقة له بمضمون القيد."),
            new ExcludedField("attachment_thumbnail", ExclusionReason.Presentation,
                "صورة مصغّرة يعاد توليدها بترقية مكتبة الصور."),

            // ── 8) اسم مُهجَر من v1 ───────────────────────────────────────
            new ExcludedField("source_ref", ExclusionReason.DenormalisedDuplicate,
                "‏v1 كان يدمج نوع المستند ومعرّفه في نصّ واحد بشرطة مائلة، فكان (\"A/B\",\"C\") " +
                "و(\"A\",\"B/C\") يعطيان البايتات نفسها. في v2 هما حقلان منفصلان مُجزَّآن؛ " +
                "والاسم المدموج مستثنى صراحةً كي لا يعود من باب خلفي.")
        ],
        requireExplicitOptionals: true);
}
