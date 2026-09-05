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

    /// <summary>
    /// معامِلاتُ المنشأة: قراءةُ الإصدارات وإيداعُ إصدارٍ جديد.
    /// <para>
    /// <b>مسارٌ واحد بفعلين لا مسارَي «تعديل» و«حذف»:</b> الإصدار يُضاف ولا يُعدَّل،
    /// فالتغيير <c>POST</c> بتاريخ سريان جديد. ولا <c>PUT</c> ولا <c>DELETE</c> على
    /// هذا السطح أصلاً — والثابتة مفروضة <b>بغياب العملية</b> لا بفحصٍ عند مستدعٍ.
    /// </para>
    /// </summary>
    public const string Parameters = Company + "/parameters";

    /// <summary>
    /// قائمةُ مراجعة المحاسب القانوني: كلُّ إصدارٍ غير موقَّع ومعه كلُّ مستندٍ مُرحَّلٍ
    /// استعمله — <b>بابُ قراءةٍ حقيقي لا تقريرٌ تحسبه شاشة</b>.
    /// </summary>
    public const string ParameterReview = Company + "/parameter-review";

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
    /// تعديل صنف: الاسم والمجموعة والوحدات — <b>ولا الرمز</b>.
    /// <para>
    /// <b>ومورد فرعي <c>revision</c> لا <c>PUT</c> على الصنف</b>: ‏<c>PUT</c> كان
    /// سيقبل رمزاً جديداً في الجسم بحكم شكله، والرمز هوية تحملها قيود سنةٍ مضت.
    /// </para>
    /// </summary>
    public const string ItemRevision = Item + "/revision";

    /// <summary>
    /// تعطيل صنف. <b>ويُقبل وله رصيد</b> — بخلاف تعطيل موضع التسكين: إيقافُ الصنف
    /// يعني «توقّف عن شرائه وبِع ما بقي»، ورفضُه فوق رصيدٍ يصنع دائرةً مغلقة.
    /// </summary>
    public const string ItemDeactivation = Item + "/deactivation";

    /// <summary>
    /// حالة الصنف في دورة حياته ورصيده المتبقّي.
    /// <para>
    /// <b>ومورد فرعي لا حقلٌ على <c>Item</c></b>: إضافةُ حقلٍ إلى شكل الصنف كانت
    /// ستُغيّر استجابة ثلاث عمليات منشورة يستهلكها عملاء اليوم.
    /// </para>
    /// </summary>
    public const string ItemLifecycle = Item + "/lifecycle";

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

    // ── تسكين المخزون: مستودع ← موقع ← رفّ ───────────────────────────────────
    // **هرمٌ في العنوان لا شرطٌ في الجسم**: الموقع مورد فرعي للمستودع، والرفّ مورد
    // فرعي للموقع — فالانتماء بنيةٌ يقرأها كل عميل، لا حقلٌ يُتحقَّق منه بعد الوصول.
    // وهو الشكل نفسه الذي جعل وحدات العقار `properties/{propertyId}/units`.
    //
    // **وإعادة التسمية والتعطيل موردان فرعيان، لا `PUT` ولا `PATCH` ولا `DELETE`.**
    // ‏`PUT` على موضعٍ كان سيقبل رمزاً جديداً في الجسم — والرمز هوية تحملها حركات
    // مضت، وتغييرُه يقطع كل حركة عن موضعها. والاسم وحده هو ما يُغيَّر، فالمورد الفرعي
    // ‏`/name` يقول ذلك في العنوان بدل أن يقوله في تعليق.

    /// <summary>المستودعات: التسجيل والقائمة.</summary>
    public const string Warehouses = Company + "/warehouses";

    /// <summary>مستودع واحد: القراءة.</summary>
    public const string Warehouse = Warehouses + "/{warehouseId}";

    /// <summary>إعادة تسمية مستودع — <b>الاسم وحده، ولا رمز</b>.</summary>
    public const string WarehouseName = Warehouse + "/name";

    /// <summary>
    /// تعطيل مستودع. <b>ويُرفض إن بقي فيه رصيد</b>: المستودع المُعطَّل لا يُنقَل منه
    /// ولا يُصرف، فتبقى البضاعة بقيمتها في الحساب الضابط بلا بابٍ تخرج منه.
    /// </summary>
    public const string WarehouseDeactivation = Warehouse + "/deactivation";

    /// <summary>مواقع مستودع: التسجيل والقائمة. <b>مورد فرعي لا مورد رئيسي</b>.</summary>
    public const string StorageLocations = Warehouse + "/locations";

    /// <summary>موقع واحد: القراءة.</summary>
    public const string StorageLocation = StorageLocations + "/{locationId}";

    /// <summary>إعادة تسمية موقع.</summary>
    public const string StorageLocationName = StorageLocation + "/name";

    /// <summary>تعطيل موقع — ويُرفض إن بقي فيه رصيد أو رفٌّ عامل.</summary>
    public const string StorageLocationDeactivation = StorageLocation + "/deactivation";

    /// <summary>
    /// أرفف موقع: التسجيل والقائمة.
    /// <para>
    /// <b>ويتفرّع عن الموقع في مسار الموقع نفسه</b>، لا عن عنوانٍ ثانٍ له. وعنوانان
    /// للموقع الواحد — أحدهما تحت مستودعه وآخر تحت الشركة — كانا سيجعلان «أيّهما
    /// الصحيح؟» سؤالاً على كل عميل، ويجعلان الانتماء يُتحقَّق منه في أحد المسارين
    /// دون الآخر.
    /// </para>
    /// </summary>
    public const string StorageBins = StorageLocation + "/bins";

    /// <summary>رفّ واحد: القراءة.</summary>
    public const string StorageBin = StorageBins + "/{binId}";

    /// <summary>إعادة تسمية رفّ.</summary>
    public const string StorageBinName = StorageBin + "/name";

    /// <summary>
    /// تعطيل رفّ. <b>ولا فحص رصيدٍ عليه</b>: الرفّ ليس بُعداً في مفتاح الرصيد، فلا
    /// صفَّ رصيدٍ يُقرأ عنه.
    /// </summary>
    public const string StorageBinDeactivation = StorageBin + "/deactivation";


    /// <summary>
    /// النقل بين موقعين: إنشاء <b>مسوّدة</b> والقائمة.
    /// <para>
    /// <b>ولا ترحيل على هذا المستند إطلاقاً</b>: النقل داخل المنشأة نفسها لا يُغيّر
    /// قيمة المخزون، والصنف واحدٌ على الطرفين فمؤهّل دوره واحد، فالقيد الذي كان
    /// سيُكتب مدينٌ ودائنٌ على الحساب نفسه بالمبلغ نفسه — أي لا شيء، في دفترٍ يُضاف
    /// إليه ولا يُحذف منه.
    /// </para>
    /// </summary>
    public const string StockTransfers = Company + "/stock-transfers";

    /// <summary>مستند نقل واحد: القراءة.</summary>
    public const string StockTransfer = StockTransfers + "/{transferId}";

    /// <summary>
    /// تنفيذ النقل: <b>حركتان في الدفتر المساعد ولا قيد</b>. والمورد الفرعي
    /// <c>movement</c> لا <c>posting</c>: الثاني يعني في هذا العقد «صار له قيد»،
    /// ومسارٌ يحمل الاسم بلا قيد كان سيُرسل كل قارئ يبحث عن قيدٍ لا وجود له.
    /// </summary>
    public const string StockTransferMovement = StockTransfer + "/movement";

    /// <summary>
    /// الأرصدة <b>بتسكينها</b>: الرصيد ومعه اسم مستودعه واسم موقعه من سجلّ التسكين،
    /// ووسمُ ما ليس مسجَّلاً فيه.
    /// </summary>
    public const string PlacementBalances = Company + "/placement-balances";

    // ── وحدات القياس ─────────────────────────────────────────────────────────
    // ‏**والاسم `units-of-measure` لا `units`** — وذلك ليس ذوقاً: المسار
    // ‏`/properties/{propertyId}/units` منشورٌ اليوم و**وحداته عقارية**. ومَورِدٌ باسم
    // ‏`units` تحت الشركة كان سيجعل الكلمة تحمل معنيين في عقدٍ واحد، فيقرأ المطوّر
    // «‏unit» في مخطّطٍ ولا يعرف أيَّهما — وهو فخُّ تسميةٍ يسبق أي تصادم توجيه.

    /// <summary>وحدات القياس: التسجيل والقائمة.</summary>
    public const string UnitsOfMeasure = Company + "/units-of-measure";

    /// <summary>وحدة قياس واحدة: القراءة.</summary>
    public const string UnitOfMeasure = UnitsOfMeasure + "/{unitId}";

    /// <summary>تعطيل وحدة قياس — <b>ولا فحص رصيد</b>: الوحدة مقياسُ ما في الموضع لا الموضع.</summary>
    public const string UnitOfMeasureDeactivation = UnitOfMeasure + "/deactivation";

    /// <summary>
    /// معاملات التحويل بين وحدتين <b>على مستوى المنشأة</b>: التسجيل والقائمة.
    /// <para>
    /// وهي غير معاملات <c>ItemRequest.units</c>: تلك <b>خاصّية تعبئةٍ لصنف</b> — كرتون
    /// هذا الصنف اثنتا عشرة وكرتون ذاك أربع وعشرون — وهذه <b>واقعةٌ فيزيائية</b> تُكتب
    /// مرّةً وتصلح للجميع.
    /// </para>
    /// </summary>
    public const string UnitConversions = Company + "/unit-conversions";

    /// <summary>
    /// <b>مسبار التحويل</b>: يحوّل كمّيةً ولا يكتب شيئاً.
    /// <para>
    /// ‏<c>POST</c> على مورد <b>محاولات</b> لا <c>GET</c> باستعلام: الكمّية تعبر السلك
    /// <b>نصّاً</b>، ونصٌّ عشري في سلسلة الاستعلام يمرّ على ترميزٍ وتحليلٍ لا يحرسهما
    /// محوّل <c>WireDecimal</c> — وهو بالضبط قناة فقدان الدقّة التي أُغلقت في الجسم.
    /// </para>
    /// </summary>
    public const string UnitConversionTrials = UnitConversions + "/trials";

    // ── العقارات ─────────────────────────────────────────────────────────────
    // والشكل هو الشكل نفسه: **إنشاء مسوّدة · قراءة · فعلٌ على مورد فرعي**. ولا `PUT`
    // ولا `PATCH` ولا `DELETE` على عقارٍ ولا وحدةٍ ولا عقدٍ ولا فاتورةٍ ولا سند.
    //
    // ‏**ولاحظ اسم مورد المستأجر: `lessees` لا `tenants`.** المسار `/api/v1/tenants`
    // وأربعة مسارات اشتراك تحته منشورةٌ اليوم **لمستأجر النظام**، ونشرُ الكلمة بمعنيين
    // يجعل العقد يكذب على قارئه قبل أن يتصادم التوجيه.

    /// <summary>العقارات: التسجيل. <b>ويسجّل بُعد العقار في الدفتر في العملية نفسها.</b></summary>
    public const string Properties = Company + "/properties";

    /// <summary>عقار واحد: القراءة بنموذج ملكيته وحصّة مالكه.</summary>
    public const string Property = Properties + "/{propertyId}";

    /// <summary>
    /// وحدات عقار: التسجيل. <b>مورد فرعي لا مورد رئيسي</b> — <c>dimensions.csv</c>
    /// يشترط العقار مع الوحدة، فالشرط بنيةٌ في العنوان لا تحقّقٌ في الجسم.
    /// </summary>
    public const string PropertyUnits = Property + "/units";

    /// <summary>وحدة واحدة: القراءة بتصنيفها — وهو ما يقود شرط الخضوع للضريبة.</summary>
    public const string Unit = Company + "/units/{unitId}";

    /// <summary>المستأجرون العقاريون: التسجيل.</summary>
    public const string Lessees = Company + "/lessees";

    /// <summary>مستأجر واحد: القراءة.</summary>
    public const string Lessee = Lessees + "/{lesseeId}";

    /// <summary>ملّاك العقارات: التسجيل.</summary>
    public const string PropertyOwners = Company + "/property-owners";

    /// <summary>مالك واحد: القراءة.</summary>
    public const string PropertyOwner = PropertyOwners + "/{ownerId}";

    /// <summary>
    /// <b>قيود تسجيل عقود الإيجار</b>: إنشاء <b>مسوّدة قيد</b>.
    /// <para>
    /// <b>والاسم مقصود:</b> ما يُنشئه هذا الباب ليس عقداً بل <b>قيداً أرشيفياً</b> لعقدٍ
    /// حُرِّر في منصّة إيجار الحكومية — وهي الطرف المخوَّل بتحرير عقود الإيجار.
    /// و<c>POST /lease-contracts</c> كان يقول «أنشئ عقد إيجار»، وهو ادّعاءٌ لا يملكه
    /// هذا النظام؛ و<c>POST /lease-registrations</c> يقول «أنشئ قيد تسجيل»، وهو ما يقع.
    /// </para>
    /// <para>
    /// <b>ولا مورد <c>…/posting</c> عليه إطلاقاً ولا يجوز أن يوجد:</b> الحدث
    /// <c>realestate.lease.signed</c> مُعلَنٌ في المصفوفة بـ<c>posts_entry=false</c> —
    /// العقد التزام متبادل مستقبلي لم ينفّذه أي طرف. وغيابُ الباب هو ما يجعل «القيد
    /// لا يُرحّل» مقروءاً من شكل السطح لا من تعليق.
    /// </para>
    /// </summary>
    public const string LeaseRegistrations = Company + "/lease-registrations";

    /// <summary>قيدٌ واحد: القراءة بحالته.</summary>
    public const string LeaseRegistration = LeaseRegistrations + "/{leaseId}";

    /// <summary>
    /// جدول دفعات القيد <b>بمعرّفات سطوره</b> — وهي مدخل الفوترة. وبلا نشرها يصير باب
    /// الفوترة باباً لا يوصل إليه بابٌ آخر (ADR-0047).
    /// </summary>
    public const string LeaseRegistrationSchedule = LeaseRegistration + "/schedule";

    /// <summary>
    /// <b>اعتماد القيد للفوترة.</b> مورد فرعي مستقلّ لا حقلٌ يُعدَّل.
    /// <para>
    /// <b>وليس تفعيلاً:</b> نفاذُ عقد الإيجار من منصّة إيجار لا من هذا الباب. وما يقع
    /// هنا إذنٌ داخلي: من هذه اللحظة تُبنى فواتير الإيجار على هذا القيد، وتدخل مدّته
    /// قيد الاستبعاد الزمني. <b>ولا يُرحّل قيداً محاسبياً، وإعادةُ النداء آمنة.</b>
    /// </para>
    /// </summary>
    public const string LeaseRegistrationBillingApproval = LeaseRegistration + "/billing-approval";

    /// <summary>فواتير الإيجار: إنشاء <b>مسوّدة</b>. والوحدة تختار الحدث من السجلّ لا من الطلب.</summary>
    public const string RentInvoices = Company + "/rent-invoices";

    /// <summary>فاتورة إيجار واحدة: القراءة.</summary>
    public const string RentInvoice = RentInvoices + "/{invoiceId}";

    /// <summary>ترحيل فاتورة إيجار — مورد فرعي مستقلّ وحصين ضد التكرار.</summary>
    public const string RentInvoicePosting = RentInvoice + "/posting";

    /// <summary>سندات القبض من المستأجرين: إنشاء <b>مسوّدة</b>.</summary>
    public const string TenantReceipts = Company + "/tenant-receipts";

    /// <summary>سند قبض واحد: القراءة.</summary>
    public const string TenantReceipt = TenantReceipts + "/{receiptId}";

    /// <summary>ترحيل سند قبض — بالحدث الذي اختاره حضور المرجع أو غيابه.</summary>
    public const string TenantReceiptPosting = TenantReceipt + "/posting";

    /// <summary>
    /// تخصيص سند ورد بلا مرجع. مورد فرعي مستقلّ: التخصيص <b>قيدٌ مستقل لا عكسٌ</b>
    /// للقيد السابق — المال وصل فعلاً، والعكس يجعل الدفتر يقول إنه لم يصل.
    /// </summary>
    public const string TenantReceiptAllocation = TenantReceipt + "/allocation";

    /// <summary>أعمار متأخرات المستأجرين ومطابقتها بنقطة ضبطها في تاريخ معلوم.</summary>
    public const string TenantArrearsAging = Company + "/tenant-arrears-aging";
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
    // ── المقاولات ────────────────────────────────────────────────────────────
    // والشكل هو شكل ADR-0044 و ADR-0047 حرفاً: **إنشاء مسوّدة · قراءة · ترحيل على
    // مورد فرعي**. ولا ‏`PUT` ولا `PATCH` ولا `DELETE` على مستند ولا على مشروع ولا
    // على عقد ولا على مقاول.
    //
    // ‏**وبابان لا ثلاثة في موضعين**: الأمر التغييري وخطاب الضمان. كلاهما التزامٌ أو
    // سجلّ لا واقعة محاسبية، ولا حدث لأيٍّ منهما في المصفوفة — فلا مورد ترحيل لهما،
    // ولا حقل `entryId` ولا `alreadyPosted` في مخطّطي جوابيهما. وحقلٌ فارغ لهما يُقرأ
    // «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً»، وهي حجّة ADR-0047 على أمر الشراء نفسها.

    /// <summary>
    /// المشاريع: التسجيل والقائمة.
    /// <para>
    /// <b>ورمز المشروع هوية لا اسم عرض</b>: هو القيمة الحرفية التي تدخل بُعد المشروع
    /// على سطر القيد. فلا تعديل له ولا حذف بعد أن تحمله قيود سنةٍ مضت — والغياب بنيوي
    /// كغيابه على الأصناف ومراكز التكلفة.
    /// </para>
    /// <para>
    /// والقائمة لازمة لا زينة: باب العقد يحتاج معرّف مشروع، و<b>بابان لا يوصل إليهما
    /// بابٌ آخر</b> اعتراضٌ مكتوب في ADR-0044.
    /// </para>
    /// </summary>
    public const string Projects = Company + "/projects";

    /// <summary>مشروع واحد: القراءة بحالته وعقوده.</summary>
    public const string Project = Projects + "/{projectId}";

    /// <summary>
    /// عقود المقاولة: الإنشاء ببنود جدول الكميات ونسبة المحتجز وفترة الضمان والعملة.
    /// <para>
    /// <b>ولاحظ ما ليس في جسمه: وعاء نسبة المحتجز ولا قاعدة استرداد الدفعة المقدمة.</b>
    /// موضعُهما نفسه قرارُ مالك — حقلٌ على العقد؟ أم جدول قواعد بتاريخ سريان؟ — ونشرُ
    /// أحدهما في عقدٍ منشور اختيارٌ لجوابٍ لم يقله أحد، ولا رجعة فيه بلا إصدار ثانٍ.
    /// </para>
    /// </summary>
    public const string ProjectContracts = Company + "/project-contracts";

    /// <summary>عقد واحد: القراءة ومعه <b>بنوده المعلَّقة</b> التي تمنع ترحيل مستخلصاته.</summary>
    public const string ProjectContract = ProjectContracts + "/{contractId}";

    /// <summary>
    /// بنود جدول الكميات <b>بمعرّفاتها</b> — وهي مدخل سطور المستخلص. والنظير الحرفي
    /// لسطور الاستلام المنشورة في ADR-0047.
    /// </summary>
    public const string BoqItems = ProjectContract + "/boq-items";

    /// <summary>مستخلصات العقد: القائمة — والأساس المطروح منه هو آخر مستخلص مُرحَّل.</summary>
    public const string ContractClientCertificates = ProjectContract + "/client-certificates";

    /// <summary>أوامر العقد التغييرية: القائمة.</summary>
    public const string ContractChangeOrders = ProjectContract + "/change-orders";

    /// <summary>
    /// موقف العقد: المُعتمَد تراكمياً، والمحتجز القائم، والدفعة غير المستنفَدة —
    /// <b>مشتقّاً من المُرحَّل وحده</b>.
    /// <para>
    /// وهو <b>بديلٌ لتقرير ربحية المشروع لا نسخةٌ منه</b>: قاعدة تحميل تكلفة الموظف
    /// والمعدّة على المشروع غير محسومة، وثلاثة حسابات تكلفة مشاريع قائمة في الدليل
    /// بلا كاتب واحد — فرقمُ ربحيةٍ مقنع بلا قاعدة معلنة أسوأ من غيابه.
    /// </para>
    /// </summary>
    public const string ContractPosition = ProjectContract + "/position";

    /// <summary>
    /// الأوامر التغييرية: الإنشاء. <b>بابان لا ثلاثة</b> — لا مورد ترحيل ولا حقل قيد.
    /// </summary>
    public const string ChangeOrders = Company + "/change-orders";

    /// <summary>أمر تغييري واحد: القراءة ببنوده الجديدة.</summary>
    public const string ChangeOrder = ChangeOrders + "/{changeOrderId}";

    /// <summary>
    /// المقاولون من الباطن: التسجيل. طرفٌ في دفتر <c>subcontractor</c> المساعد الذي
    /// لم يكن له مالكٌ في المستودع قبل هذه الوحدة.
    /// </summary>
    public const string Subcontractors = Company + "/subcontractors";

    /// <summary>مقاول واحد: القراءة. وما غاب عن العميل غائب هنا وللسبب نفسه.</summary>
    public const string Subcontractor = Subcontractors + "/{subcontractorId}";

    /// <summary>عقود الباطن: الإنشاء بنسبة محتجزه وفترة ضمانه وبنوده.</summary>
    public const string Subcontracts = Company + "/subcontracts";

    /// <summary>عقد باطن واحد: القراءة.</summary>
    public const string Subcontract = Subcontracts + "/{subcontractId}";

    /// <summary>بنود عقد الباطن بمعرّفاتها — مدخل سطور مستخلصه.</summary>
    public const string SubcontractLines = Subcontract + "/lines";

    /// <summary>
    /// مستخلصات العملاء: إنشاء <b>مسوّدة</b> بالكمّيات التراكمية والسابقة صراحةً.
    /// <para>ولا قيد ولا أثر في الدفتر: الترحيل خطوة مستقلّة على مورد فرعي.</para>
    /// </summary>
    public const string ClientCertificates = Company + "/client-certificates";

    /// <summary>مستخلص عميل واحد: القراءة بحالته وسطوره وبنوده المعلَّقة.</summary>
    public const string ClientCertificate = ClientCertificates + "/{certificateId}";

    /// <summary>
    /// ترحيل مستخلص عميل. مورد فرعي مستقلّ لا <c>PUT</c>: فعلٌ يُنشئ قيداً لا حقلٌ يُعدَّل.
    /// </summary>
    public const string ClientCertificatePosting = ClientCertificate + "/posting";

    /// <summary>
    /// مستخلصات الباطن: إنشاء <b>مسوّدة</b>، ومعها سطور الغرامات والخصومات
    /// <b>مستقلّةً</b> لا مخصومةً من قيمة الأعمال.
    /// </summary>
    public const string SubcontractorCertificates = Company + "/subcontractor-certificates";

    /// <summary>مستخلص باطن واحد: القراءة.</summary>
    public const string SubcontractorCertificate = SubcontractorCertificates + "/{certificateId}";

    /// <summary>ترحيل مستخلص باطن — ويُرفض بمشكلةٍ مُسمّاة إن حمل سطر غرامة.</summary>
    public const string SubcontractorCertificatePosting = SubcontractorCertificate + "/posting";

    /// <summary>دفعات المقاولين المقدمة: إنشاء <b>مسوّدة</b> بطريقة تسويتها ومرجع ضمانها.</summary>
    public const string SubcontractorAdvances = Company + "/subcontractor-advances";

    /// <summary>دفعة مقدمة واحدة: القراءة.</summary>
    public const string SubcontractorAdvance = SubcontractorAdvances + "/{advanceId}";

    /// <summary>
    /// ترحيل دفعة مقدمة لمقاول: <b>أصلٌ لا مصروف</b>، وحصينٌ ضد التكرار —
    /// الوصول الثاني بالهوية نفسها يُرجع معرّف القيد الأول بـ<c>alreadyPosted = true</c>.
    /// </summary>
    public const string SubcontractorAdvancePosting = SubcontractorAdvance + "/posting";

    /// <summary>الإفراج عن المحتجز: إنشاء <b>مسوّدة</b> على دفعة محتجزٍ مُسمّاة باعتماد صريح.</summary>
    public const string RetentionReleases = Company + "/retention-releases";

    /// <summary>مستند إفراج واحد: القراءة.</summary>
    public const string RetentionRelease = RetentionReleases + "/{releaseId}";

    /// <summary>ترحيل الإفراج: <b>قيدٌ مستقلّ لا تعديل لقيد المستخلص</b>.</summary>
    public const string RetentionReleasePosting = RetentionRelease + "/posting";

    /// <summary>تحصيل المحتجز من العميل: إنشاء <b>مسوّدة</b> بطريقة تسوية مُسمّاة.</summary>
    public const string RetentionCollections = Company + "/retention-collections";

    /// <summary>مستند تحصيل واحد: القراءة.</summary>
    public const string RetentionCollection = RetentionCollections + "/{collectionId}";

    /// <summary>ترحيل تحصيل المحتجز — وهو المسار الذي يمارس قدرةً في هذه الوحدة.</summary>
    public const string RetentionCollectionPosting = RetentionCollection + "/posting";

    /// <summary>
    /// سجلّ المحتجزات مدينةً ودائنة بتواريخ استحقاق الإفراج على الطرفين، مشتقّاً من
    /// المُرحَّل — وهو ما تُطابَق به نقطتا الضبط على الجانبين.
    /// </summary>
    public const string RetentionRegister = Company + "/retention-register";

    /// <summary>
    /// كشف المقاولين — وهو المطابقة المُعلَنة نصّاً في بيانات الدفاتر المساعدة:
    /// «كشف المقاولين = رصيد الحساب». وإظهارُ نقطة الضبط عبر منفذها المُعلَن لا تقريرٌ
    /// يُحتسب جانباً.
    /// </summary>
    public const string SubcontractorStatement = Company + "/subcontractor-statement";

    /// <summary>
    /// خطابات الضمان: التسجيل بمرفقها. <b>بابان لا ثلاثة</b> — لا مورد ترحيل، ولا حقل
    /// قيد في مخطّط الجواب، لأنه لا يُرحَّل أبداً.
    /// </summary>
    public const string Guarantees = Company + "/guarantees";

    /// <summary>خطاب ضمان واحد: القراءة.</summary>
    public const string Guarantee = Guarantees + "/{guaranteeId}";

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
