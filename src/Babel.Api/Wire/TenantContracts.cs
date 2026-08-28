namespace Babel.Api.Wire;

/// <summary>
/// طلب التسجيل الأول.
/// <para>
/// <b>ولا حقل خطّة فيه.</b> هذا بابٌ يُخدَم بلا اعتماد، وحقلٌ يختار منه الطالب حزمته
/// هو حقلٌ يمنح الحزمة الشاملة لمن كتب اسمها. فالاشتراك يُفتح على <b>خطّة الدخول</b>
/// وحدها، وتغييرُها فعلٌ آخر باعتماد.
/// </para>
/// <para>
/// <b>ولا حقل مستأجر ولا معرّف منشأة:</b> الثلاثة تُشتقّ من <c>requestKey</c> اشتقاقاً
/// حتمياً، وهو ما يجعل إعادة الإرسال تصل إلى المستأجر نفسه لا إلى ثانٍ.
/// </para>
/// </summary>
/// <param name="RequestKey">
/// مفتاح الطلب: قيمة <b>عشوائية</b> يولّدها العميل ويحتفظ بها. إعادةُ الإرسال به تردّ
/// المستأجر نفسه ولا تُنشئ ثانياً.
/// </param>
/// <param name="CompanyNameAr">اسم المنشأة بالعربية — <b>وهو السجلّ</b> (ADR-0021).</param>
/// <param name="OwnerNameAr">اسم أول مالك بالعربية.</param>
/// <param name="NameTranslations">
/// ترجمات اسم المنشأة، مفاتيحها أوسمة BCP-47. <b>ولا حقل إنجليزي ثابت هنا</b>: تعدّد
/// اللغات قابليةُ الترجمة إلى أيّ عدد من اللغات، والإنجليزية واحدةٌ من N لا نصف الاثنين.
/// <para>
/// ومستوى التحكّم يلزمه اسمٌ لاتيني لتقاريره الأسطولية، فيُقرأ من الوسم <c>en</c> إن
/// وُجد، و<b>يرتدّ إلى العربية</b> إن لم يوجد — والارتداد مُعلَن لا صامت: اسمٌ عربي في
/// عمود لاتيني أصدق من عمودٍ يخترع اسماً.
/// </para>
/// </param>
internal sealed record RegisterTenantRequestDto(
    string RequestKey,
    string CompanyNameAr,
    string OwnerNameAr,
    IReadOnlyList<NameValueDto>? NameTranslations = null);

/// <summary>حالة وحدة في الاشتراك، برمز كتالوج مستوى التحكّم.</summary>
/// <param name="Code">رمز الوحدة.</param>
/// <param name="NameAr">اسمها بالعربية — السجلّ.</param>
/// <param name="NameTranslations">ترجمات اسمها، مفاتيحها أوسمة BCP-47.</param>
/// <param name="State">‏<c>Entitled</c> أو <c>ReadOnly</c> أو <c>NotEntitled</c>.</param>
/// <param name="PostsJournal">
/// هل يبلغ عملُ هذه الوحدة الدفتر؟ وهو ما يجعل أرضيتها <b>قراءةً</b> لا نزعاً عند
/// الانقطاع: منشأةٌ رحّلت قيداً واحداً لها دفتر، ولا يُنتزع منها (ADR-0034).
/// </param>
internal sealed record SubscriptionModuleDto(
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string State,
    bool PostsJournal);

/// <summary>
/// اشتراك المستأجر: الخطّة، والحالة، والوحدات، وتاريخ التجديد.
/// <para><b>وكل مبلغ نصّ</b> بأربع خانات عشرية — لا رمز رقمي في JSON.</para>
/// </summary>
/// <param name="TenantId">المستأجر.</param>
/// <param name="TenantCode">رمزه القصير في سجل الأسطول.</param>
/// <param name="NameAr">اسمه بالعربية — السجلّ.</param>
/// <param name="NameTranslations">ترجمات اسمه.</param>
/// <param name="TenantStatus">حالته: <c>Provisioning</c> أو <c>Active</c> أو <c>Suspended</c> أو <c>Archived</c>.</param>
/// <param name="SubscriptionId">معرّف الاشتراك الجاري.</param>
/// <param name="PlanCode">رمز الخطّة.</param>
/// <param name="PlanNameAr">اسمها بالعربية.</param>
/// <param name="PlanNameTranslations">ترجمات اسم الخطّة.</param>
/// <param name="MonthlyPrice">السعر الشهري نصّاً.</param>
/// <param name="PerUserPrice">سعر المستخدم الواحد بعد المُضمَّن، نصّاً.</param>
/// <param name="IncludedUsers">عدد المستخدمين المُضمَّنين في السعر الشهري.</param>
/// <param name="Currency">عملة التسعير.</param>
/// <param name="StartedOn">تاريخ بدء الاشتراك الجاري بصيغة <c>yyyy-MM-dd</c>.</param>
/// <param name="EndsOn">تاريخ انتهائه، أو <c>null</c> لاشتراك جارٍ بلا نهاية معلومة.</param>
/// <param name="State">‏<c>Active</c> أو <c>Lapsed</c> أو <c>Cancelled</c>.</param>
/// <param name="RenewsOn">
/// تاريخ التجديد التالي، أو <c>null</c> لاشتراك ليس فعّالاً — وتاريخٌ يُعرض على اشتراك
/// منقطع يُقرأ وعداً بأن الخدمة ستعود من تلقاء نفسها، وهي لا تعود.
/// </param>
/// <param name="Modules">الوحدات وحالاتها، مرتَّبةً برمزها ترتيباً حرفياً ثابتاً.</param>
internal sealed record SubscriptionDto(
    string TenantId,
    string TenantCode,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string TenantStatus,
    string SubscriptionId,
    string PlanCode,
    string PlanNameAr,
    IReadOnlyList<NameValueDto> PlanNameTranslations,
    string MonthlyPrice,
    string PerUserPrice,
    int IncludedUsers,
    string Currency,
    string StartedOn,
    string? EndsOn,
    string State,
    string? RenewsOn,
    IReadOnlyList<SubscriptionModuleDto> Modules);

/// <summary>
/// مستأجر سُجِّل للتوّ، ومعه ما يفتح به مالكُه جلسته.
/// <para>
/// <b>و<c>enrolmentCredential</c> يخرج مرّة واحدة</b>: المُودَع بصمته، فلا يوجد في
/// الخادم من يعيده. وعند إعادة الإرسال بالمفتاح نفسه يكون <c>alreadyRegistered</c>
/// صحيحاً و<c>enrolmentCredential</c> <b>معدوماً</b> — لا لأن النتيجة اختلفت، بل لأن
/// السرّ يُسكّ مرّة بحكم القرار (ADR-0045 §٢٫٣)، وسكُّ سرٍّ ثانٍ عند كل إعادة إرسال
/// يجعل الباب المفتوح مصنعَ اعتمادات.
/// </para>
/// </summary>
/// <param name="TenantId">المستأجر المُنشأ.</param>
/// <param name="TenantCode">رمزه القصير.</param>
/// <param name="CompanyId">أول منشأة له — وهي التي يُؤسَّس بها ويُرحَّل فيها.</param>
/// <param name="AlreadyRegistered">‏<c>true</c> حين ردّ هذا الطلبُ تسجيلاً سابقاً بالمفتاح نفسه.</param>
/// <param name="Owner">أول عضوية مالكة.</param>
/// <param name="EnrolmentCredential">اعتماد الانتساب، أو <c>null</c> عند إعادة الإرسال.</param>
/// <param name="EnrolmentExpiresAt">لحظة انقضائه، أو <c>null</c> عند إعادة الإرسال.</param>
/// <param name="Subscription">الاشتراك المفتوح على خطّة الدخول.</param>
internal sealed record RegisteredTenantDto(
    string TenantId,
    string TenantCode,
    string CompanyId,
    bool AlreadyRegistered,
    MembershipDto Owner,
    string? EnrolmentCredential,
    string? EnrolmentExpiresAt,
    SubscriptionDto Subscription);

/// <summary>طلب تغيير الخطّة.</summary>
/// <param name="PlanCode">رمز الخطّة الجديدة من مجموعة الخطط المعروفة.</param>
/// <param name="Authority">
/// السند: رقم عقد، أو حدث سداد، أو تذكرة، أو قرار مُوثَّق. <b>إلزامي</b> — لأن تغيير
/// الخطّة يحكم أي بيانات مالية يجوز إنشاؤها، فهو حدث تدقيقي لا إعداد واجهة.
/// </param>
/// <param name="ReasonAr">السبب بالعربية — يُكتب في سجلّ تدقيق الاستحقاق.</param>
internal sealed record ChangePlanRequestDto(string PlanCode, string Authority, string ReasonAr);

/// <summary>طلب انقطاع أو استئناف — بالسند نفسه وللسبب نفسه.</summary>
/// <param name="Authority">السند. إلزامي.</param>
/// <param name="ReasonAr">السبب بالعربية.</param>
internal sealed record SubscriptionTransitionRequestDto(string Authority, string ReasonAr);
