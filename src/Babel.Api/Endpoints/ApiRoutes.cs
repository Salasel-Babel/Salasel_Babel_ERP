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

    /// <summary>
    /// دليل الحسابات بشروط الترحيل على كل حساب.
    /// <para>
    /// <b>مورد واحد لا موردان، ولا مسار «للقابل للترحيل» بمفرده:</b> الشجرة تُعرَض
    /// بآبائها، والمدخل يحمل <c>postable</c> فيرشّح العميل بلا طلبٍ ثانٍ. ومسارٌ ثانٍ
    /// كان سيجعل «هل هذا الحساب موجود؟» و«هل يقبل الترحيل؟» سؤالين يُجابان بطلبين
    /// يفترقان عند أول تعديل دليل.
    /// </para>
    /// </summary>
    public const string ChartOfAccounts = Company + "/chart-of-accounts";

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

    // ── المبيعات ─────────────────────────────────────────────────────────────
    // ولاحظ شكل الترحيل هنا: **مورد فرعي مستقلّ** ‏`/posting`‏ كما `/reversal` على
    // القيد و`/suspension` على مركز التكلفة. والترحيل فعلٌ يُنشئ قيداً، لا حقلٌ
    // يُعدَّل على المستند — ولو كان `PUT` على المستند لصار «تعديل مستند» أول ما يتبادر،
    // وهو بالضبط ما لا يوجد في هذا النظام.

    /// <summary>العملاء: الإضافة. بيانات أساسية، لا مستند.</summary>
    public const string Customers = Company + "/customers";

    /// <summary>
    /// عميل واحد: القراءة.
    /// <para>
    /// <b>ولاحظ ما ليس هنا: لا حذف — ولا إعادة تسمية ولا إيقاف بعد.</b> غياب الحذف
    /// بنيوي كغيابه على القيود ومراكز التكلفة: عميلٌ تشير إليه قيود سنة سابقة لا يُحذف،
    /// وحذفُه يكسر كل تقرير مُرحَّل. وغياب الإيقاف <b>ليس قراراً بأنه لا يُوقَف</b>،
    /// بل إعلانٌ بأن الوحدة لا تملكه اليوم: العمود <c>is_active</c> يُكتب مرّةً عند
    /// الإنشاء ولا يقرؤه مسار ترحيل واحد. وبابٌ اسمه «إيقاف» لا يمنع فاتورةً واحدة
    /// أسوأ من غيابه — يبدو ضابطاً وليس كذلك.
    /// </para>
    /// </summary>
    public const string Customer = Customers + "/{customerId}";

    /// <summary>فواتير المبيعات: إنشاء <b>مسوّدة</b>. لا قيد ولا أثر في الدفتر.</summary>
    public const string SalesInvoices = Company + "/sales-invoices";

    /// <summary>فاتورة مبيعات واحدة: القراءة بحالتها ومجاميعها.</summary>
    public const string SalesInvoice = SalesInvoices + "/{invoiceId}";

    /// <summary>
    /// ترحيل فاتورة. مورد فرعي مستقلّ، وحصين ضد التكرار: الوصول الثاني بالهوية نفسها
    /// يُرجع المستند ذاته بـ<c>alreadyPosted = true</c> ورمز 200 بدل 201.
    /// </summary>
    public const string SalesInvoicePosting = SalesInvoice + "/posting";

    /// <summary>
    /// الإشعارات الدائنة: إنشاء <b>مسوّدة</b> على فاتورة <b>مُرحَّلة</b>.
    /// <para>وهذا هو الطريق الوحيد إلى تصحيح فاتورة مُرحَّلة — لا تعديل ولا حذف (ADR-0002).</para>
    /// </summary>
    public const string CreditNotes = Company + "/credit-notes";

    /// <summary>ترحيل إشعار دائن.</summary>
    public const string CreditNotePosting = CreditNotes + "/{creditNoteId}/posting";

    /// <summary>
    /// سندات القبض من العملاء: إنشاء <b>مسوّدة</b> بتخصيصاتها على فواتير مُرحَّلة.
    /// <para>ولا أثر على ذمّة العميل قبل الترحيل: التخصيص يُنزَل مع القيد لا قبله.</para>
    /// </summary>
    public const string CustomerReceipts = Company + "/customer-receipts";

    /// <summary>سند قبض واحد: القراءة بحالته ومجاميعه ومعرّف قيده إن رُحّل.</summary>
    public const string CustomerReceipt = CustomerReceipts + "/{receiptId}";

    /// <summary>
    /// ترحيل سند قبض. مورد فرعي مستقلّ وحصين ضد التكرار — والقبض <b>يُسقط من ذمّة
    /// العميل</b> بالمقبوض وخصم التعجيل معاً.
    /// </summary>
    public const string CustomerReceiptPosting = CustomerReceipt + "/posting";

    /// <summary>أعمار الذمم المدينة في تاريخ معلوم.</summary>
    public const string ReceivablesAging = Company + "/receivables-aging";

    // ── المشتريات ────────────────────────────────────────────────────────────

    /// <summary>الموردون: الإضافة.</summary>
    public const string Suppliers = Company + "/suppliers";

    /// <summary>مورد واحد: القراءة. وما غاب عن العميل غائب هنا وللسبب نفسه.</summary>
    public const string Supplier = Suppliers + "/{supplierId}";

    /// <summary>فواتير الموردين: إنشاء فاتورة مصروف <b>مسوّدة</b>.</summary>
    public const string SupplierBills = Company + "/supplier-bills";

    /// <summary>فاتورة مورد واحدة: القراءة.</summary>
    public const string SupplierBill = SupplierBills + "/{billId}";

    /// <summary>ترحيل فاتورة مورد — بالشكل نفسه وبالحصانة نفسها.</summary>
    public const string SupplierBillPosting = SupplierBill + "/posting";

    /// <summary>سندات الصرف للموردين: إنشاء <b>مسوّدة</b> بتخصيصاتها.</summary>
    public const string SupplierPayments = Company + "/supplier-payments";

    /// <summary>سند صرف واحد: القراءة.</summary>
    public const string SupplierPayment = SupplierPayments + "/{paymentId}";

    /// <summary>
    /// ترحيل سند صرف. مورد فرعي مستقلّ وحصين ضد التكرار — والصرف <b>يُسقط من ذمّة
    /// المورد</b> بالمدفوع وحده، والرسوم مصروفٌ على المنشأة لا نقصٌ في ذمّته.
    /// </summary>
    public const string SupplierPaymentPosting = SupplierPayment + "/posting";

    /// <summary>
    /// أوامر الشراء: الإنشاء.
    /// <para>
    /// <b>ولاحظ ما ليس هنا — ولا يجوز أن يوجد: لا مورد <c>…/posting</c>.</b> أمر الشراء
    /// <b>التزام تعاقدي لا حدث محاسبي</b>: لا يُنشئ قيداً، ولا يمسّ حساباً، ولا يُدخل
    /// المنشأة في التزامٍ يُثبَت في الدفتر. والقيد الأول في دورة الشراء هو
    /// <b>الاستلام</b> — لأن البضاعة عندها دخلت والالتزام نشأ فعلاً.
    /// وبابُ ترحيلٍ عليه كان سيكون خطأً محاسبياً مكتوباً في عقد منشور.
    /// </para>
    /// </summary>
    public const string PurchaseOrders = Company + "/purchase-orders";

    /// <summary>أمر شراء واحد: القراءة <b>بسطوره ومعرّفاتها</b> — وهي مدخل الاستلام.</summary>
    public const string PurchaseOrder = PurchaseOrders + "/{orderId}";

    /// <summary>استلامات البضاعة: إنشاء <b>مسوّدة</b> على أمر شراء.</summary>
    public const string GoodsReceipts = Company + "/goods-receipts";

    /// <summary>استلام واحد: القراءة.</summary>
    public const string GoodsReceipt = GoodsReceipts + "/{receiptId}";

    /// <summary>
    /// ترحيل استلام. <b>وهو الباب الوحيد على هذا السطح الذي يمسّ دفتراً مساعداً غير
    /// دفتر الأطراف</b>: يُسجّل الوارد في دفتر المخزون بتكلفته الفعلية ثم يُدين حساب
    /// المراقبة، بهوية ترحيلٍ واحدة للدفترين وبنفس الحصانة.
    /// </summary>
    public const string GoodsReceiptPosting = GoodsReceipt + "/posting";

    /// <summary>
    /// سطور استلامٍ واحد بمعرّفاتها — <b>مدخل الفاتورة المخزنية والمرتجع</b>.
    /// <para>
    /// وهو مورد فرعي لا مورد ثانٍ للمستند: قراءة الاستلام تُرجع رأسه كما نُشرت في
    /// <c>ADR-0047</c> ولا تتغيّر، وسطوره تُقرأ هنا. وبلا هذا الباب يصير
    /// <c>POST /stock-bills</c> و<c>POST /purchase-returns</c> بابين لا يوصل إليهما
    /// بابٌ آخر على هذا السطح — وهو الاعتراض الذي كتبه ADR-0044 بنصّه.
    /// </para>
    /// </summary>
    public const string GoodsReceiptLines = GoodsReceipt + "/lines";

    /// <summary>أعمار الذمم الدائنة في تاريخ معلوم.</summary>
    public const string PayablesAging = Company + "/payables-aging";

    // ── المرفقات ─────────────────────────────────────────────────────────────
    // والشكل هو شكل ADR-0044 حرفياً: **مورد رئيسي وموارد فرعية**، ولا PUT ولا PATCH
    // ولا DELETE على مرفقٍ واحد. والسبب هو السبب نفسه: المرفق **سندُ إثبات** لقيد،
    // فيأخذ انضباط الدفتر — التصحيح إصدارٌ يشير إلى سلفه، والإزالة علامة سحب
    // (ADR-0046). وثلاث طبقات في القاعدة تقول ذلك أيضاً: لا صلاحية UPDATE لدور
    // التطبيق، ومشغّل رفض على كل دور، وجدول سحبٍ ثانٍ لا عمودٌ يُعدَّل.

    /// <summary>المرفقات: الإيداع، والجرد بترشيح على المستند المصدر.</summary>
    public const string Attachments = Company + "/attachments";

    /// <summary>
    /// مرفق واحد: قراءة <b>الوصف بلا بايتة</b> — البصمة والحجم والنوع والمُودِع والزمن
    /// وسلسلة الإصدارات وعلامة السحب. والبايتات باب آخر بتذكرة.
    /// </summary>
    public const string Attachment = Attachments + "/{attachmentId}";

    /// <summary>
    /// إصدار جديد على مرفق. مورد فرعي مستقلّ لا <c>PUT</c>: التصحيح <b>صفٌّ يُضاف</b>
    /// يشير إلى سلفه، لا حقلٌ يُعدَّل. وفهرس فريد جزئي في القاعدة يجعل السلسلة خطّية،
    /// فتصحيحان متزامنان للسلف نفسه يُنتجان فائزاً واحداً وخاسراً بـ409.
    /// </summary>
    public const string AttachmentRevisions = Attachment + "/revisions";

    /// <summary>
    /// سحب مرفق. مورد فرعي مستقلّ لا <c>DELETE</c>: السحب <b>صفٌّ في جدول ثانٍ</b>،
    /// والبايتات تبقى والبصمة تبقى — الاحتفاظ بسند القيد واجب نظامي.
    /// </summary>
    public const string AttachmentWithdrawal = Attachment + "/withdrawal";

    /// <summary>
    /// سكّ تذكرة تنزيل موقّعة وقصيرة الأجل. مورد فرعي لأن التذكرة <b>واقعة تُصدَر</b>
    /// لا حقلٌ يُقرأ على المرفق.
    /// </summary>
    public const string AttachmentDownloadTickets = Attachment + "/download-tickets";

    /// <summary>
    /// بايتات المرفق، <b>بتذكرة موقّعة</b>. والبصمة تُفحص قبل التسليم لا بعده،
    /// و<c>Content-Type</c> من النوع <b>المشموم</b> وحده، و<c>Content-Disposition</c>
    /// بـ<c>attachment</c> لا <c>inline</c>.
    /// </summary>
    public const string AttachmentContent = Attachment + "/content";

    /// <summary>
    /// فواتير الموردين المخزنية: الإنشاء وحده.
    /// <para>
    /// <b>وتُقرأ وتُرحَّل من مورد فاتورة المورد نفسه</b> — <c>/supplier-bills/{billId}</c>
    /// و<c>…/posting</c>: مستندٌ واحد وعنوانٌ واحد. وموردان يقرآن الصفّ نفسه كانا
    /// سيجعلان «أي العنوانين الصحيح؟» سؤالاً يُطرح على كل عميل.
    /// </para>
    /// </summary>
    public const string StockBills = Company + "/stock-bills";

    /// <summary>مرتجعات المشتريات: إنشاء <b>مسوّدة</b> على فاتورة مخزنية مُرحَّلة.</summary>
    public const string PurchaseReturns = Company + "/purchase-returns";

    /// <summary>مرتجع مشتريات واحد: القراءة.</summary>
    public const string PurchaseReturn = PurchaseReturns + "/{returnId}";

    /// <summary>ترحيل مرتجع مشتريات — البضاعة تخرج بتكلفة استلامها، والذمة تنقص.</summary>
    public const string PurchaseReturnPosting = PurchaseReturn + "/posting";

    // ── المخزون ──────────────────────────────────────────────────────────────
    // والشكل هو الشكل نفسه: **إنشاء مسوّدة · قراءة · ترحيل على مورد فرعي**. ولا
    // ‏`PUT` ولا `PATCH` ولا `DELETE` على مستند ولا على صنف.

    /// <summary>
    /// الأصناف: التسجيل والقائمة.
    /// <para>
    /// <b>ولاحظ ما ليس هنا: لا حذف ولا تعديل.</b> رمزُ الصنف هوية تحملها قيود سنةٍ
    /// مضت، وحذفُه يكسر كل تقرير مُرحَّل؛ وتغييرُ وحدة أساسه بعد أن كُتبت عليه حركات
    /// يجعل مجموع حركاته جمعَ أعدادٍ بمقاييس مختلفة. وذلك <b>نقصُ سطحٍ مُعلَن</b>.
    /// </para>
    /// </summary>
    public const string Items = Company + "/items";

    /// <summary>صنف واحد: القراءة بوحدته ومعاملات تحويله.</summary>
    public const string Item = Items + "/{itemId}";

    /// <summary>
    /// حركات المخزون القائمة بذاتها: إنشاء <b>مسوّدة</b> والقائمة.
    /// <para>
    /// وهي تسوية الجرد والرصيد الافتتاحي والإعدام — <b>لا استلام المشتريات ولا صرف
    /// المبيعات</b>: تلك مستنداتٌ في وحدتيهما، وحركتُها أثرٌ لها. وبابٌ ثانٍ لها هنا
    /// كان سيكتب الحركة مرّتين بهويتين.
    /// </para>
    /// </summary>
    public const string StockMovements = Company + "/stock-movements";

    /// <summary>ترحيل حركة مخزون: حركةٌ في الدفتر المساعد وقيدٌ في الدفتر.</summary>
    public const string StockMovementPosting = StockMovements + "/{movementId}/posting";

    /// <summary>أرصدة المخزون: الصنف في موقعه من مستودعه، بكمّيته ووحدتها وقيمتها.</summary>
    public const string StockBalances = Company + "/stock-balances";

    /// <summary>
    /// تقييم المخزون في تاريخ، ومطابقته بحسابه الضابط بثلاثة طرق مستقلّة.
    /// </summary>
    public const string InventoryValuation = Company + "/inventory-valuation";

    // ── الموارد البشرية ──────────────────────────────────────────────────────
    // والشكل هو شكل ADR-0044/0047 حرفياً: **إنشاء مسوّدة · قراءة · ترحيل على مورد
    // فرعي**. ولا `PUT` ولا `PATCH` ولا `DELETE` على مورد واحد منها — والإنهاء نفسه
    // مورد فرعي (`…/termination`) لا حقل حالة يُعدَّل.

    /// <summary>
    /// الموظفون: التسجيل.
    /// <para>
    /// <b>ولا رمز في الحمولة</b>: الخادم يولّد رمزاً <b>معتماً</b> هو وحده ما يعبر إلى
    /// دفتر الأستاذ. ولا هوية وطنية ولا آيبان ولا اسم يعبر إلى <c>ledger.*</c> بحال:
    /// كل ما يدخله يدخل البايتات المُجزَّأة، و<c>REVOKE UPDATE, DELETE</c> يجعله غير
    /// قابل للإزالة، وعلاجُ المحو الموعود في ADR-0046 لا يبلغ سلسلة تجزئة.
    /// </para>
    /// </summary>
    public const string Employees = Company + "/employees";

    /// <summary>موظف واحد: القراءة — والهوية الشخصية <b>مقنَّعة دائماً</b>.</summary>
    public const string Employee = Employees + "/{employeeId}";

    /// <summary>
    /// إنهاء خدمة موظف. <b>مورد فرعي مستقلّ لا <c>PUT</c> بحقل حالة</b>، بسابقة
    /// <c>…/suspension</c> على مركز التكلفة و<c>…/reversal</c> على القيد. وهو ما يفتح
    /// المخالصة.
    /// </summary>
    public const string EmployeeTermination = Employee + "/termination";

    /// <summary>
    /// مكوّنات الأجر: التعريف والقائمة.
    /// <para>
    /// <b>وهذا هو الباب الذي يجعل الأثر التنظيمي بياناتٍ لا شيفرة</b>: وسمُ دخول
    /// المكوّن وعاءَ الاشتراك يملؤه المحاسب. ولا يحمل المكوّن مبلغاً ولا نسبة.
    /// </para>
    /// </summary>
    public const string PayComponents = Company + "/pay-components";

    /// <summary>
    /// عناصر أجر موظف: الإسناد بتاريخ سريان، والقراءة.
    /// <para><b>إنشاء لا تعديل</b>: الزيادة صفٌّ جديد، وإلا استحال إعادة حساب مسيّر
    /// ماضٍ ليطابق قيده المُرحَّل.</para>
    /// </summary>
    public const string PayElements = Employee + "/pay-elements";

    /// <summary>
    /// إعدادات نِسَب التأمينات: إيداع إصدار، وقراءة الإصدارات.
    /// <para>
    /// <b>وهذا هو الموضع الوحيد الذي تدخل منه نسبة إلى هذا النظام.</b> و<c>POST</c> لا
    /// <c>PUT</c>: نسبة فترةٍ ماضية لا تُعدَّل. والجدول يُسلَّم <b>فارغاً</b>، ومسيّرٌ
    /// لفترة لا يغطّيها صفٌّ سارٍ معتمد يُرفض صراحةً — لا قيمة افتراضية واحدة.
    /// </para>
    /// </summary>
    public const string PayrollSettings = Company + "/payroll-settings";

    /// <summary>مسيّرات الرواتب: إنشاء <b>مسوّدة</b>. الوحدة تحسب، ولا مجاميع في الطلب.</summary>
    public const string PayrollRuns = Company + "/payroll-runs";

    /// <summary>مسيّر واحد: القراءة بحالته ومجاميعه.</summary>
    public const string PayrollRun = PayrollRuns + "/{runId}";

    /// <summary>
    /// قسائم مسيّر بمعرّفاتها ومعرّفات قيودها — <b>مدخل باب الدفع</b>، وبلاها يصير
    /// بابٌ لا يوصل إليه بابٌ آخر على هذا السطح.
    /// </summary>
    public const string PayrollRunPayslips = PayrollRun + "/payslips";

    /// <summary>
    /// ترحيل مسيّر. <b>نداءٌ واحد يُصدر قيداً لكل قسيمة</b>، لكلٍّ هويّته السداسية
    /// و<c>DocumentId</c> فيها معرّف القسيمة لا معرّف المسيّر.
    /// </summary>
    public const string PayrollRunPosting = PayrollRun + "/posting";

    /// <summary>قسيمة واحدة: القراءة بمكوّناتها ومعرّف قيدها — <b>وهي مستند الترحيل</b>.</summary>
    public const string Payslip = Company + "/payslips/{payslipId}";

    /// <summary>سندات صرف الرواتب: إنشاء <b>مسوّدة</b> على مسيّر مُرحَّل.</summary>
    public const string PayrollPayments = Company + "/payroll-payments";

    /// <summary>سند صرف واحد: القراءة بسطوره ومعرّفات قيودها.</summary>
    public const string PayrollPayment = PayrollPayments + "/{paymentId}";

    /// <summary>ترحيل سند صرف الرواتب — قيدٌ لكل سطر، ومعه طرف الخزينة.</summary>
    public const string PayrollPaymentPosting = PayrollPayment + "/posting";

    /// <summary>سندات سداد التأمينات: إنشاء <b>مسوّدة</b> للفترة.</summary>
    public const string SocialInsurancePayments = Company + "/social-insurance-payments";

    /// <summary>سند سداد تأمينات واحد: القراءة ومعه ما استُحقّ في فترته.</summary>
    public const string SocialInsurancePayment = SocialInsurancePayments + "/{paymentId}";

    /// <summary>
    /// ترحيل سداد التأمينات — <b>قيدٌ واحد للفترة، وهو الوحيد الذي يجوز فيه ذلك</b>
    /// في هذه الوحدة، لأن سطره الأول على حساب الالتزام بلا دفتر مساعد.
    /// </summary>
    public const string SocialInsurancePaymentPosting = SocialInsurancePayment + "/posting";

    /// <summary>
    /// سجلّ الجزاءات المعتمد: القيد.
    /// <para>
    /// <b>ولاحظ ما ليس هنا ولا يجوز أن يوجد: لا مورد <c>…/posting</c>.</b> الاستقطاع
    /// يُرحَّل <b>داخل المسيّر</b> لا بذاته، وبابٌ يوحي بغير ذلك يُبنى عليه عميل شاشةً
    /// بزرّ ترحيل لا وجود له.
    /// </para>
    /// </summary>
    public const string EmployeeDeductions = Company + "/employee-deductions";

    /// <summary>جزاء واحد: القراءة. <b>وبلا <c>entryId</c> وبلا <c>alreadyPosted</c>.</b></summary>
    public const string EmployeeDeduction = EmployeeDeductions + "/{deductionId}";

    /// <summary>
    /// سلف الموظفين: إنشاء <b>مسوّدة</b> بجدول أقساطها.
    /// <para>
    /// <b>ولا مورد <c>…/posting</c> عليها في هذا التسليم</b>: حدث صرف السلفة
    /// (<c>hr.employee_advance.paid</c>) <b>غير موجود في مصفوفة الترحيل</b>، والمحرك
    /// يرفض رمزاً لا يعرفه ولا يخترع قالباً. وبابٌ يَعِد بدورة لا تكتمل أسوأ من غيابه —
    /// وهو المعيار نفسه الذي مُنع به مورد ترحيل أمر الشراء في ADR-0047.
    /// </para>
    /// </summary>
    public const string EmployeeAdvances = Company + "/employee-advances";

    /// <summary>سلفة واحدة: القراءة بجدول سدادها والمتبقّي منها.</summary>
    public const string EmployeeAdvance = EmployeeAdvances + "/{advanceId}";

    /// <summary>
    /// استحقاق مخصص نهاية الخدمة: إنشاء <b>مسوّدة</b> لفترة.
    /// <para>
    /// <b>ومستندٌ يُنشئه نداءٌ صريح لا مهمّة مجدولة</b>: لا مُشغّل دوري ولا جدول عمل
    /// في هذه الوحدة، والنمط محجوزٌ للانتزاع ولا يُخترع مرّتين (ADR-0048 §2.3).
    /// </para>
    /// </summary>
    public const string EndOfServiceProvisions = Company + "/end-of-service-provisions";

    /// <summary>مستند استحقاق واحد: القراءة بحركاته لكل علاقة عمل.</summary>
    public const string EndOfServiceProvision = EndOfServiceProvisions + "/{provisionId}";

    /// <summary>ترحيل الاستحقاق — قيدٌ لكل علاقة عمل. وتغيير التقدير قيدٌ مستقلّ.</summary>
    public const string EndOfServiceProvisionPosting = EndOfServiceProvision + "/posting";

    /// <summary>مخالصات نهاية الخدمة: إنشاء <b>مسوّدة</b> على علاقة عمل منتهية.</summary>
    public const string EndOfServiceSettlements = Company + "/end-of-service-settlements";

    /// <summary>مخالصة واحدة: القراءة — وهي أكثر مستند في الوحدة عرضةً للنزاع.</summary>
    public const string EndOfServiceSettlement = EndOfServiceSettlements + "/{settlementId}";

    /// <summary>ترحيل المخالصة بسيناريوهاتها الثلاثة.</summary>
    public const string EndOfServiceSettlementPosting = EndOfServiceSettlement + "/posting";

    /// <summary>
    /// مطابقة دفتر الموظف المساعد بنقطة ضبطه — <b>مستنداً بمستند</b>.
    /// <para>
    /// <b>ولا يُنشر فيه رقمٌ واحد اسمه «رصيد الموظف»</b>: نقطة الضبط تجمّع بلا تفصيل
    /// بالحساب، ودفتر الموظف يمتدّ على أصلٍ وثلاثة خصوم — فصافٍ واحد يقاصّ سلفةً
    /// بمخصص خدمة براتب مستحق ويعلن التطابق وهو أعمى.
    /// </para>
    /// </summary>
    public const string EmployeeSubledgerReconciliation = Company + "/employee-subledger-reconciliation";

    /// <summary>
    /// العقد المنشور نفسه، بايتاته كما أُودعت في <c>contracts/openapi/v1.json</c>.
    /// <para>
    /// <b>ولا يُبنى وقت التشغيل.</b> الوثيقة تُولَّد بـ<c>--emit-openapi</c> وتُودَع
    /// ويحرسها <c>PublishedContractTests</c> بايتاً بايت. وخادمٌ يبني وثيقةً من نفسه
    /// عند كل طلب يُنشئ <b>مصدر حقيقة ثانياً يستطيع أن ينحرف</b> — وواجهةٌ تعرض عقداً
    /// لم يولّده أحد أسوأ من غياب الواجهة: تبدو مرجعاً وهي خطأ. وهذا الشكل بعينه —
    /// عقدٌ له أكثر من طرف وحارسٌ على طرفٍ واحد — كلّف هذا المستودع مرّة
    /// (‏<c>traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only</c>).
    /// </para>
    /// </summary>
    public const string OpenApiDocument = "/openapi/" + Version + ".json";

    /// <summary>صفحة استعراض العقد — قائمة بذاتها، بلا أي أصل خارجي.</summary>
    public const string Docs = "/docs";

    /// <summary>حالة الخدمة — خارج النطاق وخارج المصادقة عمداً.</summary>
    public const string Health = "/health";
}
