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

    /// <summary>حالة الخدمة — خارج النطاق وخارج المصادقة عمداً.</summary>
    public const string Health = "/health";
}
