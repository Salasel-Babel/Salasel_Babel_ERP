using System.Globalization;

namespace Babel.SharedKernel;

/// <summary>
/// <b>قراءةُ قيمةِ نشرٍ من البيئة — والغيابُ يُرفض ولا يُخمَّن.</b>
/// <para>
/// <b>العطل الذي وُجد هذا الملفّ لإغلاقه:</b> كانت سبع وحدات تكتب نصّ اتصالها
/// افتراضياً في شيفرتها على الشكل <c>الاتصال ?? "…Username=postgres…"</c>. وأثرُ ذلك
/// في نشرةٍ ينقصها المتغيّر ليس تعطُّلاً بل <b>عملاً بصلاحيةٍ كاملة على العنقود كلّه</b>:
/// ‏<c>postgres</c> هو المستخدم الفائق، ومن يحمل اتصاله يستطيع إسقاط أي مشغّل حصانة ثم
/// الكتابة فوق قيدٍ مُرحَّل (‏ADR-0003). ولا سطرَ سجلٍّ واحداً يقول إن قيمةً افتُرضت.
/// </para>
/// <para>
/// <b>والقاعدة هنا واحدة:</b> قيمةُ نشرٍ غائبة <b>فراغ</b>، ومن يحتاجها <b>يرفض التركيب
/// برسالة تسمّي المتغيّر</b> — كما يفعل <c>deploy/compose.yml</c> بصيغة
/// <c>${VAR:?رسالة}</c> بالضبط. ولا يُخترع نصُّ اتصالٍ لأحد.
/// </para>
/// <para>
/// <b>ووضعُ التطوير المحلّي بابٌ صريحٌ باسمه لا ارتدادٌ ضمني:</b>
/// <see cref="LocalDevelopmentVariable"/> — متغيّرٌ واحد يقول «هذا تطوير» — وهو وحده ما
/// يفتح بناءَ نصّ اتصالٍ على المِعوَد. فمن يشغّل على جهازه يضبط متغيّراً واحداً؛ ومن
/// ينشر خادماً لا يضبطه، فيصير كلُّ نقصٍ في إعداده <b>وقفةً بصوت</b> لا عملاً بامتيازٍ
/// لم يقصده أحد. وهذا هو الفارق بين <b>وضعٍ</b> و<b>ارتداد</b> — وهو الفارق نفسه الذي
/// قرّره ADR-0058 في أوضاع الحافة.
/// </para>
/// <para>
/// <b>ولا عنوانَ مضيفٍ في شيفرة هذا المستودع إلا في هذا الملفّ.</b> المِعوَد
/// (‏<c>127.0.0.1</c>) ليس عنوان خادمٍ يُسرَّب: هو <b>جهاز من يشغّل</b> ولا يدلّ على شيء
/// خارجه. وكان مكتوباً ثماني مرات في سبع وحدات، فصار موضعاً واحداً يقرؤه الحارس.
/// </para>
/// </summary>
public static class DeploymentSetting
{
    /// <summary>
    /// المتغيّر الذي يُعلن وضع التطوير المحلّي. <b>قيمته المقبولة</b>: <c>1</c> أو
    /// <c>true</c> أو <c>yes</c> — وما عداها يعني «ليس تطويراً».
    /// </summary>
    public const string LocalDevelopmentVariable = "BABEL_LOCAL_DEV";

    /// <summary>
    /// اسم المِعوَد الذي تُبنى عليه اتصالات التطوير المحلّي. <b>الموضع الوحيد في
    /// <c>src/</c> الذي يُكتب فيه عنوان مضيف</b>، ويحرس وحدانيّتَه اختبارٌ معماري.
    /// </summary>
    public const string LoopbackHost = "127.0.0.1";

    /// <summary>منفذ PostgreSQL القياسي — يُستعمل في وضع التطوير المحلّي وحده.</summary>
    public const int LoopbackPort = 5432;

    /// <summary>
    /// دورُ المالك على جهاز التطوير. <b>الموضع الوحيد في <c>src/</c> الذي يُذكر فيه اسم
    /// المستخدم الفائق</b>، ولا يُبلَغ إلا من <see cref="LocalDevelopmentDeclared"/>.
    /// <para>
    /// <b>ولماذا المالك لا دورُ تطبيق:</b> وحداتُ الدفاتر المساعدة لها اتصالٌ واحد
    /// تَنشر به مخطّطها وتقرأ وتكتب، فلا تقوم على جهاز مطوّرٍ بدورٍ بلا DDL. وهذا
    /// <b>ثمنُ وضعِ تطويرٍ مُعلَن</b>، لا افتراضُ نشرٍ صامت: النشر يُمنع من بلوغه
    /// بالبناء، لا بالانضباط.
    /// </para>
    /// </summary>
    public const string LocalDevelopmentOwnerRole = "postgres";

    /// <summary>
    /// هل أُعلن وضع التطوير المحلّي في بيئة هذه العملية؟
    /// <para>
    /// <b>ويُقرأ عند كل نداء لا مرّةً واحدة:</b> قيمةٌ تُخزَّن في حقلٍ ساكن تجعل اختباراً
    /// يضبط المتغيّر بعد أوّل قراءةٍ يرى قيمةً قديمة — أي حارساً يمرّ لأنه لم يُشغَّل.
    /// </para>
    /// </summary>
    public static bool LocalDevelopmentDeclared() =>
        IsAffirmative(Environment.GetEnvironmentVariable(LocalDevelopmentVariable));

    /// <summary>
    /// هل هذه القيمة إعلانٌ موجب؟ دالّة صافية كي تُختبَر بلا لمس بيئة العملية.
    /// </summary>
    /// <param name="value">القيمة كما وصلت من البيئة.</param>
    public static bool IsAffirmative(string? value) =>
        value is not null
        && (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// نصّ اتصالٍ محلّي على المِعوَد. <b>لا يُنادى إلا في وضع تطويرٍ مُعلَن</b>، ولا
    /// كلمةَ مرور فيه: يعمل مع <c>pg_hba: trust</c> على المِعوَد وحده.
    /// </summary>
    /// <param name="database">اسم القاعدة.</param>
    /// <param name="role">اسم الدور.</param>
    public static string LoopbackConnection(string database, string role) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Host={LoopbackHost};Port={LoopbackPort};Database={database};Username={role};Include Error Detail=true");

    /// <summary>
    /// يحسم قيمة الاتصال: المضبوطة إن وُجدت، ثمّ الافتراضي المحلّي <b>إن أُعلن وضع
    /// التطوير</b>، وإلّا <b>فراغ</b> — ومعناه «لم يُضبط»، ومن يحتاجه يرفض.
    /// <para>دالّة صافية عمداً: هي ما يُختبَر، لا قراءة البيئة.</para>
    /// </summary>
    /// <param name="configured">ما وصل من البيئة أو الإعداد.</param>
    /// <param name="localDevelopmentDeclared">هل أُعلن وضع التطوير المحلّي؟</param>
    /// <param name="database">اسم القاعدة في وضع التطوير.</param>
    /// <param name="role">اسم الدور في وضع التطوير.</param>
    public static string Resolve(string? configured, bool localDevelopmentDeclared, string database, string role)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return localDevelopmentDeclared ? LoopbackConnection(database, role) : string.Empty;
    }

    /// <summary>
    /// يقرأ متغيّر بيئةٍ ويحسم قيمته بـ<see cref="Resolve"/>. هذا ما تناديه صفوف
    /// الإعدادات في مُهيّئات خصائصها.
    /// </summary>
    /// <param name="variable">اسم متغيّر البيئة.</param>
    /// <param name="database">اسم القاعدة في وضع التطوير.</param>
    /// <param name="role">اسم الدور في وضع التطوير.</param>
    public static string Connection(string variable, string database, string role) =>
        Resolve(Environment.GetEnvironmentVariable(variable), LocalDevelopmentDeclared(), database, role);

    /// <summary>
    /// كسابقتها بدور <see cref="LocalDevelopmentOwnerRole"/> — وهو ما تحتاجه وحدةٌ لها
    /// اتصالٌ واحد يَنشر مخطّطها ويقرأ ويكتب به.
    /// </summary>
    /// <param name="variable">اسم متغيّر البيئة.</param>
    /// <param name="database">اسم القاعدة في وضع التطوير.</param>
    public static string Connection(string variable, string database) =>
        Connection(variable, database, LocalDevelopmentOwnerRole);

    /// <summary>
    /// يبني عطلَ «متغيّرٌ غائب» برسالةٍ بلغتين <b>تسمّي المتغيّر</b> ومفتاح الإعداد
    /// البديل ووضعَ التطوير المُعلَن. ولا تُذكر قيمةٌ ولا اعتماد — الاسم وحده.
    /// </summary>
    /// <param name="code">الرمز الثابت الذي يُقرأ آلياً، مثل <c>sales.connection_not_configured</c>.</param>
    /// <param name="variable">اسم متغيّر البيئة الغائب.</param>
    /// <param name="configurationKey">مفتاح الإعداد المكافئ، مثل <c>Babel:Sales:ConnectionString</c>.</param>
    /// <param name="subjectAr">وصفُ ما لم يُضبط بالعربية، مثل «اتصال قاعدة المبيعات».</param>
    /// <param name="subjectEn">وصفُه بالإنجليزية، مثل <c>the Sales database connection</c>.</param>
    public static InvalidOperationException Missing(
        string code,
        string variable,
        string configurationKey,
        string subjectAr,
        string subjectEn) =>
        new(code + " — " + subjectAr + " غير مضبوط. اضبط " + configurationKey + " أو متغيّر البيئة "
            + variable + "؛ ولا نصّ اتصالٍ يُخترع في نشرة: افتراضٌ صامت هنا يعني عملاً "
            + "بصلاحيةٍ لم يقصدها أحد على قاعدةٍ لم يقصدها أحد. وللتطوير على جهازك اضبط "
            + LocalDevelopmentVariable + "=1 فيُبنى اتصالٌ محلّي على المِعوَد. / "
            + code + " — " + subjectEn + " is not configured. Set " + configurationKey
            + " or the " + variable + " environment variable; no connection string is invented for a "
            + "deployment. For local development set " + LocalDevelopmentVariable + "=1.");
}
