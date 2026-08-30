namespace Babel.Api.Ports;

/// <summary>
/// ترجمةُ اسمٍ واحدة على حدّ المنفذ: وسمُ لغتها واسمُها.
/// <para>
/// <b>ولا نصفَ إنجليزي ثابت يعبر هذا الحدّ:</b> العربية هي السجلّ، وكل لغة سواها
/// <b>صفّ</b> — والإنجليزية واحدةٌ من N لا نصف الاثنين (ADR-0021 بند 2). ومستوى التحكّم
/// يحوّل عموده اللاتيني إلى صفوف عند حدّه هو، فلا يصل إلى هنا عمود.
/// </para>
/// </summary>
/// <param name="Tag">وسم اللغة بصيغة BCP-47.</param>
/// <param name="Name">الاسم بتلك اللغة.</param>
internal sealed record FleetNameTranslation(string Tag, string Name);

/// <summary>وحدةٌ في اشتراك، <b>باسم حالتها نصّاً</b>. المفردات مفردات مستوى التحكّم.</summary>
/// <param name="Code">رمز الوحدة في كتالوج مستوى التحكّم — <c>CORE</c> و<c>AR</c> وأخواتها.</param>
/// <param name="NameAr">اسمها بالعربية — السجلّ، ولا نصف ثانياً معه.</param>
/// <param name="State">اسم حالتها حرفاً بحرف.</param>
/// <param name="PostsJournal">هل يبلغ عملُها الدفتر؟</param>
internal sealed record FleetModule(string Code, string NameAr, string State, bool PostsJournal);

/// <summary>
/// اشتراك مستأجر كما يعبر من مستوى التحكّم إلى السطح — <b>وكل مبلغ فيه نصّ وكل تاريخ نصّ</b>.
/// <para>
/// ولا نوع من مستوى التحكّم يعبر هذا الحدّ: المحوّل يُسقط أنواعه على هذا النوع، ونقاط
/// النهاية لا تعرف أن خلفه <c>Npgsql</c> ولا <c>control.subscription</c>.
/// </para>
/// </summary>
/// <param name="TenantId">معرّف المستأجر.</param>
/// <param name="TenantCode">رمزه القصير في سجل الأسطول.</param>
/// <param name="NameAr">اسمه بالعربية — السجلّ.</param>
/// <param name="TenantStatus">حالته في سجل الأسطول.</param>
/// <param name="SubscriptionId">معرّف الاشتراك الجاري.</param>
/// <param name="PlanCode">رمز الخطّة.</param>
/// <param name="PlanNameAr">اسم الخطّة بالعربية.</param>
/// <param name="MonthlyPrice">السعر الشهري نصّاً.</param>
/// <param name="PerUserPrice">سعر المستخدم الواحد بعد المُضمَّن، نصّاً.</param>
/// <param name="IncludedUsers">عدد المستخدمين المُضمَّنين.</param>
/// <param name="Currency">عملة التسعير.</param>
/// <param name="StartedOn">تاريخ البدء بصيغة <c>yyyy-MM-dd</c>.</param>
/// <param name="EndsOn">تاريخ الانتهاء إن وُجد.</param>
/// <param name="State">حالة الاشتراك: <c>Active</c> أو <c>Lapsed</c> أو <c>Cancelled</c>.</param>
/// <param name="RenewsOn">تاريخ التجديد التالي، أو <c>null</c> لاشتراك ليس فعّالاً.</param>
/// <param name="Modules">الوحدات وحالاتها مرتَّبةً برمزها.</param>
internal sealed record FleetSubscription(
    Guid TenantId,
    string TenantCode,
    string NameAr,
    string TenantStatus,
    string SubscriptionId,
    string PlanCode,
    string PlanNameAr,
    string MonthlyPrice,
    string PerUserPrice,
    int IncludedUsers,
    string Currency,
    string StartedOn,
    string? EndsOn,
    string State,
    string? RenewsOn,
    IReadOnlyList<FleetModule> Modules);

/// <summary>
/// <b>منفذ الأسطول</b>: كل ما يحتاجه السطح من مستوى التحكّم، بمفردات نصّية ومعرّفات.
/// <para>
/// <b>ولماذا منفذ لا نداء مباشر:</b> ليس لأجل التبديل — بل لأن نقطة النهاية يجب ألّا
/// تعرف أن خلفها مستوىً آخر له قاعدته ودورُه وأعطالُه. وهذا هو الشكل نفسه الذي يأخذه
/// <c>IJournalEntryReader</c> هنا: عقدٌ منشور، وتنفيذٌ يقول «غير متاح» بصوته حين لا
/// يكون متاحاً، لا مسارٌ يسقط بـ500.
/// </para>
/// <para>
/// <b>والمفردات مفردات مستوى التحكّم عمداً</b> — <c>CORE</c> و<c>AR</c> لا
/// <c>BabelModule</c> — لأن الترجمة بين الكتالوجين قرارٌ يقع في موضعٍ واحد مُسمّى
/// (<c>PlaneTranslation</c>)، لا في كل مستدعٍ.
/// </para>
/// </summary>
internal interface IFleetDirectory
{
    /// <summary>
    /// هل مستوى التحكّم مُهيَّأ لهذا الخادم؟
    /// <para>
    /// وخادمٌ بلا مستوى تحكّم <b>يقول ذلك برمزه</b> ولا يسقط ولا يخترع اشتراكاً: سطح
    /// الاشتراك يردّ <c>503</c> و<c>fleet.unavailable</c>، وسائر السطح يعمل كما كان.
    /// </para>
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>رموز الخطط المعروفة، مرتَّبةً — تُقرأ من الكتالوج فلا تُكتب قائمةً ثانية.</summary>
    IReadOnlyList<string> KnownPlans { get; }

    /// <summary>يقرأ اشتراك مستأجر، أو <c>null</c> إن لم يكن في سجل الأسطول.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<FleetSubscription?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// يفتح مستأجراً واشتراكه على <b>خطّة الدخول</b> — ومُحكَم: النداء الثاني بالمعرّف
    /// نفسه لا يُنشئ مستأجراً ثانياً.
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر المشتقّ من مفتاح الطلب.</param>
    /// <param name="tenantCode">رمزه القصير المشتقّ من المفتاح نفسه.</param>
    /// <param name="nameAr">اسم المنشأة بالعربية — السجلّ.</param>
    /// <param name="translations">
    /// ترجمات الاسم صفوفاً. ومستوى التحكّم يلزمه اسمٌ لاتيني لتقاريره الأسطولية، فيُقرأ
    /// من الوسم <c>en</c> إن وُجد و<b>يرتدّ إلى العربية</b> إن لم يوجد — والارتداد مُعلَن.
    /// </param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<FleetSubscription> OpenAsync(
        Guid tenantId,
        string tenantCode,
        string nameAr,
        IReadOnlyList<FleetNameTranslation> translations,
        CancellationToken cancellationToken = default);

    /// <summary>يغيّر خطّة المستأجر بسندٍ مكتوب.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="planCode">رمز الخطّة الجديدة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="authority">السند: رقم عقد أو حدث سداد أو تذكرة أو قرار مُوثَّق.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<FleetSubscription> ChangePlanAsync(
        Guid tenantId, string planCode, string actor, string authority, string reasonAr,
        CancellationToken cancellationToken = default);

    /// <summary>يُنهي الاشتراك ويهبط بكل وحدة إلى أرضيتها.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="authority">السند.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<FleetSubscription> LapseAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default);

    /// <summary>يستأنف الاشتراك على خطّته ويُعيد وحداتها إلى الاستحقاق.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="authority">السند.</param>
    /// <param name="reasonAr">السبب بالعربية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<FleetSubscription> ResumeAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default);
}
