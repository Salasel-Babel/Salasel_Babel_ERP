namespace Babel.Canonicalization.Schemas;

/// <summary>
/// المخطّط المرجعي: قيد اليومية، إصدار v1.
///
/// المكتبة نفسها لا تعرف شيئاً عن المحاسبة ولا عن قاعدة البيانات؛ هذا الملف هو
/// <b>مثال مواصفة</b> مكتوب بمفردات المكتبة، وهو الذي تبنى عليه المتجهات الذهبية.
/// الوحدات المحاسبية تُعرّف مخطّطاتها بالطريقة نفسها.
///
/// <b>مجموعة الاستثناء أدناه جزء من المواصفة المُوقَّعة، لا تعليق.</b> بصمة المخطّط
/// (<see cref="CanonicalSchema.Fingerprint"/>) تشمل الاستثناءات، واختبار ذهبي يثبّتها،
/// فإضافة حقل أو إخراجه من الاستثناء تُسقط البناء فوراً.
/// </summary>
public static class JournalEntrySchema
{
    /// <summary>نوع المستند.</summary>
    public const string Kind = "babel.journal.entry";

    /// <summary>المخطّط المجمَّد للإصدار v1.</summary>
    public static readonly CanonicalSchema V1 = new(
        Kind,
        "v1",
        fields:
        [
            // --- هوية القيد ---
            new SchemaField("tenant_id", CanonicalKind.Text),
            new SchemaField("book_id", CanonicalKind.Text),
            new SchemaField("fiscal_year", CanonicalKind.Integer),
            new SchemaField("entry_id", CanonicalKind.Uuid),
            new SchemaField("entry_no", CanonicalKind.Integer),

            // --- التواريخ ---
            // entry_date تاريخ محاسبي مجرّد بلا منطقة زمنية.
            // posted_at لحظة UTC مُلتقطة مرّة واحدة وغير قابلة للتغيير.
            new SchemaField("entry_date", CanonicalKind.Date),
            new SchemaField("posted_at", CanonicalKind.Instant),

            // --- الحالة والفاعل ---
            new SchemaField("status", CanonicalKind.Token),
            new SchemaField("actor", CanonicalKind.Text),

            // --- الوصف ---
            // memo_ar هو الحقل المُوقَّع. مقابله memo_ar_search مستثنى (انظر أدناه).
            new SchemaField("memo", CanonicalKind.Text, Required: false),
            new SchemaField("memo_ar", CanonicalKind.Text, Required: false),

            // --- المرجع الخارجي وحصانة التكرار ---
            new SchemaField("source_ref", CanonicalKind.Text, Required: false),
            new SchemaField("idempotency_key", CanonicalKind.Text),

            new SchemaField("currency", CanonicalKind.Token),

            // --- السطور ---
            new SchemaField("lines", CanonicalKind.Group, Required: true, GroupFields:
            [
                new SchemaField("line_no", CanonicalKind.Integer),
                new SchemaField("account_code", CanonicalKind.Text),
                new SchemaField("debit", CanonicalKind.Amount),
                new SchemaField("credit", CanonicalKind.Amount),
                new SchemaField("cost_center", CanonicalKind.Text, Required: false),
                new SchemaField("description", CanonicalKind.Text, Required: false)
            ])
        ],
        exclusions:
        [
            // ---------------- بصمة السجل نفسه ----------------
            new ExcludedField("entry_hash", ExclusionReason.SelfHash,
                "بصمة السجل لا تستطيع أن تجزّئ نفسها. تُخزَّن بجوار canon_version وتُعاد حسابها عند التحقق."),
            new ExcludedField("canon_version", ExclusionReason.SelfHash,
                "يُحمل في ترويسة الشكل السلكي نفسه (babel.canon/v1) لا كحقل، ويُخزَّن بجوار البصمة في عمود مستقلّ."),

            // ---------------- الأعمدة المشتقّة للبحث — الأخطر ----------------
            new ExcludedField("memo_ar_search", ExclusionReason.SearchNormalised,
                "ناتج ArabicSearch.Normalize: يزيل التطويل ويوحّد أشكال الألف والتاء المربوطة. " +
                "تجزئته تعني أن تغيير قواعد البحث بعد سنتين يُبطل كل تحقّق سابق."),
            new ExcludedField("account_name_search", ExclusionReason.SearchNormalised,
                "المصدر نفسه والخطر نفسه: عمود مشتقّ يُعاد بناؤه عند تغيّر قواعد البحث."),
            new ExcludedField("actor_search", ExclusionReason.SearchNormalised,
                "مطبَّع للبحث؛ مشتقّ من actor المُوقَّع."),
            new ExcludedField("search_vector", ExclusionReason.SearchNormalised,
                "tsvector يتغيّر بتغيّر إعداد النص الكامل في PostgreSQL ومع ترقيات الخادم."),

            // ---------------- قيم الإسقاط ----------------
            new ExcludedField("running_balance", ExclusionReason.ProjectionDerived,
                "رصيد متحرّك محسوب من السطور؛ يُعاد بناؤه، فلا يجوز أن يقيّد السلسلة."),
            new ExcludedField("total_debit", ExclusionReason.ProjectionDerived,
                "مجموع مشتقّ من السطور المُجزَّأة أصلاً. تجزئته تكرار يخلق مصدرين للحقيقة."),
            new ExcludedField("total_credit", ExclusionReason.ProjectionDerived,
                "كسابقه."),
            new ExcludedField("line_count", ExclusionReason.ProjectionDerived,
                "عدد السطور مكتوب أصلاً في سطر المجموعة G داخل البايتات القانونية."),
            new ExcludedField("account_balance_snapshot", ExclusionReason.ProjectionDerived,
                "لقطة إسقاط تُعاد بناؤها عند إعادة تشغيل الإسقاطات."),

            // ---------------- بيانات تشغيلية متغيّرة ----------------
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
                "عدّاد إعادة المحاولة."),

            // ---------------- قياس عن بُعد ----------------
            new ExcludedField("client_ip", ExclusionReason.Telemetry,
                "قد يتغيّر بإعادة كتابة السجلات عند تصحيح خصوصية، ويُخفى في التصدير."),
            new ExcludedField("user_agent", ExclusionReason.Telemetry,
                "سلسلة عميل متغيّرة ولا علاقة لها بالحقيقة المحاسبية."),
            new ExcludedField("session_id", ExclusionReason.Telemetry,
                "معرّف جلسة عابر."),
            new ExcludedField("trace_id", ExclusionReason.Telemetry,
                "معرّف تتبّع موزّع؛ يتغيّر عند إعادة المعالجة."),

            // ---------------- عرض ----------------
            new ExcludedField("display_order", ExclusionReason.Presentation,
                "ترتيب عرض في الواجهة؛ ترتيب السطور المُجزَّأ هو line_no."),
            new ExcludedField("printed_count", ExclusionReason.Presentation,
                "عدّاد طباعة."),
            new ExcludedField("attachment_thumbnail", ExclusionReason.Presentation,
                "صورة مصغّرة يعاد توليدها بترقية مكتبة الصور.")
        ]);

    /// <summary>نطاق السلسلة = نطاق الترقيم: مستأجر × دفتر × سنة مالية.</summary>
    public static string ChainScope(string tenantId, string bookId, int fiscalYear)
        => $"{Kind}|{tenantId}|{bookId}|{fiscalYear.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>بصمة تكوين النطاق.</summary>
    public static byte[] Genesis(string tenantId, string bookId, int fiscalYear)
        => Canonicalizer.Genesis(ChainScope(tenantId, bookId, fiscalYear));
}
