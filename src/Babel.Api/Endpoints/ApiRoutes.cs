namespace Babel.Api.Endpoints;

/// <summary>
/// مسارات السطح، معلنة مرّة واحدة.
/// <para>
/// المسار الحرفي لا يُكتب في موضعين: التسجيل يقرأ من هنا، والمولّد يقرأ من هنا، والاختبار
/// الذي يقارن المستند المُودَع بالمُولَّد يقرأ من هنا. مسارٌ مكتوب مرتين ينحرف في أحدهما،
/// فيصير العقد المنشور يصف بابًا لا وجود له.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذه القائمة: مسار حذف.</b> غيابه بنيوي لا اتفاقي — لا توجد دالة
/// حذف على <c>IPostingService</c> أصلاً، ولا صلاحية <c>DELETE</c> لدور التطبيق في
/// PostgreSQL. الطبقات الثلاث تقول الشيء نفسه (ADR-0002 · ADR-0003).
/// </para>
/// </summary>
internal static class ApiRoutes
{
    /// <summary>إصدار السطح. الرقم في المسار لا في ترويسة: العنوان وحده يُميّز العقد.</summary>
    public const string Version = "v1";

    /// <summary>جذر السطح المُصدَّر.</summary>
    public const string Base = "/api/" + Version;

    /// <summary>
    /// الجلسة: من يقف خلف الاعتماد، وأي الشركات يبلغ.
    /// <para>
    /// <b>المسار الوحيد خارج نطاق الشركة بعد نقطة الصحّة، وغيابُ النطاق منه بنيوي:</b>
    /// من لا يعرف معرّف شركته لا يستطيع أن يضعه في المسار ليسأل عن شركاته. ولا يعني ذلك
    /// انفتاحاً — المصادقة إلزامية عليه كأي مسار آخر، والقائمة هي مجموعة الاعتماد نفسها.
    /// </para>
    /// </summary>
    public const string Session = Base + "/session";

    /// <summary>نطاق الشركة. كل قراءة وكل كتابة تمرّ به — لا مسار خارج نطاق.</summary>
    public const string Company = Base + "/companies/{companyId}";

    /// <summary>ترحيل قيد.</summary>
    public const string PostJournalEntry = Company + "/journal-entries";

    /// <summary>قراءة قيد بسطوره.</summary>
    public const string ReadJournalEntry = Company + "/journal-entries/{entryId}";

    /// <summary>عكس قيد. مورد فرعي مستقل: العكس فعلٌ يُنشئ قيداً، لا تعديلٌ على قيد.</summary>
    public const string ReverseJournalEntry = Company + "/journal-entries/{entryId}/reversal";

    /// <summary>ميزان المراجعة.</summary>
    public const string TrialBalance = Company + "/trial-balance";

    /// <summary>إعادة التحقق من سلسلة البصمات.</summary>
    public const string ChainVerification = Company + "/ledger-chain/verification";

    /// <summary>
    /// ملفّ قدرات المستأجر: القراءة والحفظ.
    /// <para>
    /// مورد واحد لا موردان: الملفّ كلٌّ واحد يُقرأ ويُستبدل. وتعديل نوع مستند بمفرده
    /// كان سيجعل «ما الحالة الآن؟» سؤالاً يُجاب بتجميع طلبات، وهو ما يجعل تعارض
    /// التحديثات صامتاً.
    /// </para>
    /// </summary>
    public const string CapabilityProfile = Company + "/capability-profile";

    /// <summary>شكل مستند مُشتقّاً من (العقد المنشور × ملفّ القدرات) — مقروء لا مؤلَّف.</summary>
    public const string DocumentShape = Company + "/document-shapes/{documentType}";

    /// <summary>
    /// عرضُ مستند على الملفّ ليُقبل أو يُرفض. مورد فرعي مستقل: القبول حكمٌ يُطلب،
    /// لا حقلٌ يُقرأ من الشكل.
    /// </summary>
    public const string DocumentAdmission = DocumentShape + "/admissions";

    /// <summary>
    /// تأسيس المنشأة: القراءة، والتأسيس <b>مرّة واحدة</b>.
    /// <para>
    /// مورد واحد لا موردان، و<c>PUT</c> لا <c>POST</c> على مورد معلوم العنوان: التأسيس
    /// حالةُ المنشأة الابتدائية كلّها — اسمها، وعدد خاناتها العشرية، ومركز تكلفتها الأول —
    /// وتفريقها على ثلاثة موارد كان سيجعل «هل اكتمل التأسيس؟» سؤالاً يُجاب بتجميع طلبات.
    /// والوصول الثاني يُرفض بـ409 مهما تغيّرت حمولته.
    /// </para>
    /// </summary>
    public const string CompanySetup = Company + "/setup";

    /// <summary>
    /// مراكز التكلفة: القائمة والإضافة.
    /// <para>
    /// <b>ولاحظ ما ليس هنا: مسار حذف.</b> غيابه بنيوي كغيابه على القيود — لا دالة حذف على
    /// <c>CostCenterRegister</c> أصلاً. والمركز الذي يخرج من الاستعمال <b>يُوقَف</b>،
    /// فيبقى تاريخه المُرحَّل مقروءاً ومُبوَّباً (ADR-0002 · ADR-0006).
    /// </para>
    /// </summary>
    public const string CostCenters = Company + "/cost-centers";

    /// <summary>مركز تكلفة واحد: إعادة التسمية. الهوية هي الرمز، والاسم عرضٌ يتغيّر.</summary>
    public const string CostCenter = CostCenters + "/{costCenterCode}";

    /// <summary>
    /// إيقاف مركز تكلفة. مورد فرعي مستقل: الإيقاف حالة عملٍ تُطلب بسبب مكتوب،
    /// لا حقلٌ يُعدَّل على المركز.
    /// </summary>
    public const string CostCenterSuspension = CostCenter + "/suspension";

    /// <summary>حالة الخدمة — خارج النطاق وخارج المصادقة عمداً.</summary>
    public const string Health = "/health";
}
