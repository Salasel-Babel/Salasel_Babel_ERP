namespace Babel.Api.Wire;

/// <summary>
/// شركة يبلغها هذا الاعتماد، كما تُعرض في شاشة اختيار الشركة.
/// <para>
/// <b>والاسم العربي هو السجلّ، و<see cref="NameTranslations"/> ترجماته — أيّاً كان عددها</b>
/// (ADR-0021). و<b>لا حقل ثابت للإنجليزية هنا</b> كما لا حقل لها في صفّ ميزان المراجعة:
/// الإنجليزية واحدة من N لا نصف الاثنين، ومدخلها في الترجمات كمدخل غيرها.
/// </para>
/// <para>
/// <b>ولماذا تظهر المنشأة غير المؤسَّسة في القائمة بدل أن تُحذف منها:</b> إخفاؤها يجعل
/// اعتماداً يبلغ شركةً واحدة يرى قائمة فارغة ويقرؤها «اعتمادي لا يصلح» — وهو تشخيص خاطئ
/// يكلّف مكالمة دعم. وظهورها بـ<c>state = NotSetUp</c> يقول ما ينقص بالضبط: تأسيسٌ لم يقع.
/// </para>
/// </summary>
/// <param name="CompanyId">معرّف الشركة كما يُكتب في المسار.</param>
/// <param name="State">‏<c>Ready</c> لمنشأة مؤسَّسة، و<c>NotSetUp</c> لمنشأة لم تُؤسَّس بعد.</param>
/// <param name="NameAr">الاسم العربي — السجلّ. <c>null</c> حين لا تأسيس، لأن الاسم يُسنَد عند التأسيس.</param>
/// <param name="NameTranslations">ترجمات الاسم بوسم اللغة BCP-47، مرتَّبة، وفارغة حين لا تأسيس.</param>
/// <param name="DecimalPlaces">عدد الخانات العشرية المعروضة. <c>null</c> حين لا تأسيس.</param>
/// <param name="DefaultCostCenter">رمز مركز التكلفة الافتراضي. <c>null</c> حين لا تأسيس.</param>
internal sealed record SessionCompanyDto(
    string CompanyId,
    string State,
    string? NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    int? DecimalPlaces,
    string? DefaultCostCenter);

/// <summary>
/// الهوية خلف الاعتماد، والشركات التي يبلغها — <b>أول ما يحتاجه مستخدم حقيقي</b>.
/// <para>
/// ولا شيء منها يأتي من جسم الطلب ولا من ترويسة يكتبها العميل: الثلاثة مشتقّة من
/// الاعتماد وحده، تماماً كما في كل مسار آخر على هذا السطح. وهذه النقطة هي التي تجعل
/// «معرّف الشركة» شيئاً <b>يُختار</b> لا شيئاً <b>يُكتب</b> — ومعرّفٌ بصيغة 8-4-4-4-12
/// لا يُكتب بيد إنسان.
/// </para>
/// </summary>
/// <param name="TenantId">المستأجر خلف الاعتماد.</param>
/// <param name="UserId">المستخدم خلف الاعتماد.</param>
/// <param name="CompanyCount">عدد الشركات التي يبلغها. لا يكون صفراً أبداً — الصفر رفضٌ لا قائمة.</param>
/// <param name="Companies">الشركات، مرتَّبة بمعرّفها ترتيباً حرفياً ثابتاً.</param>
internal sealed record SessionDto(
    string TenantId,
    string UserId,
    int CompanyCount,
    IReadOnlyList<SessionCompanyDto> Companies);
