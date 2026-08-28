using System.Security.Cryptography;
using System.Text;
using Babel.Core.Tenancy;
using Babel.SharedKernel;

namespace Babel.Api.Security;

/// <summary>
/// هوية الطلب: المستأجر، والمستخدم، والشركات التي يبلغها هذا الاعتماد.
/// <para>
/// <b>ولا شيء من هذه الثلاثة يأتي من جسم الطلب ولا من ترويسة يكتبها العميل.</b> كلها
/// مشتقّة من الاعتماد نفسه. وهذا هو الفرق بين عزل مستأجرين وبين اتفاق: ترويسة
/// <c>X-Tenant-Id</c> يكتبها من يشاء.
/// </para>
/// </summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Companies">الشركات التي يبلغها هذا الاعتماد. الفراغ يعني لا شيء — والفشل مغلق.</param>
/// <param name="NotAfter">
/// اللحظة التي ينقضي عندها الاعتماد، أو <c>null</c> لاعتماد بلا انقضاء معلن.
/// <para>
/// <b>ولماذا يُفصَل الانقضاء عن الرفض:</b> اعتمادٌ منقضٍ اعتمادٌ <b>يملكه صاحبه</b>، فإخباره
/// أنه انقضى لا يكشف له شيئاً لا يعرفه؛ أما اعتماد مختلَق فإخبار مقدّمه بأي شيء عن سبب
/// الرفض يجعل السطح عدّاد وجود. ولذلك رمزان مختلفان لحالتين مختلفتين، وليس رمزاً واحداً
/// يجعل من انقضت جلسته يظنّ أن اعتماده سُحب منه فيفتح تذكرة دعم بدل أن يدخل من جديد.
/// </para>
/// </param>
/// <param name="Session">
/// عائلة الجلسة التي أُصدر منها هذا الاعتماد، أو <c>null</c> لاعتماد التزويد المُهيَّأ من
/// الإعداد — وهو الاعتماد الوحيد الذي لا عائلة له، فلا شيء فيه يُبطَل من HTTP.
/// </param>
/// <param name="ReadOnlyCompanies">
/// الشركات التي دور صاحب هذا الاعتماد فيها <b>قراءةٌ فقط</b>. و<c>null</c> تعني «لا واحدة» —
/// وهي حال اعتماد التزويد، فسلوكه لا يتغيّر بوجود هذا الحقل.
/// </param>
internal sealed record ApiPrincipal(
    TenantId Tenant,
    UserId User,
    IReadOnlySet<Guid> Companies,
    DateTimeOffset? NotAfter = null,
    Guid? Session = null,
    IReadOnlySet<Guid>? ReadOnlyCompanies = null)
{
    /// <summary>هل يبلغ هذا الاعتماد الشركة المطلوبة؟</summary>
    /// <param name="companyId">معرّف الشركة من المسار.</param>
    public bool Reaches(Guid companyId) => Companies.Contains(companyId);

    /// <summary>
    /// هل دور صاحب هذا الاعتماد في هذه الشركة <b>قراءةٌ فقط</b>؟
    /// <para>
    /// وهو سؤالٌ غير سؤال الاستحقاق ولا يُخلط به: الاستحقاق يسأل «أدُفع ثمن هذه الوحدة؟»
    /// ويجيب عن المستأجر كلّه؛ وهذا يسأل «من هذا الإنسان في هذه المنشأة؟». وخلطهما يجعل
    /// قارئاً يُقال له «اشتراكك منقطع» فيتّصل بالمحاسبة بلا سبب (ADR-0034 · ADR-0036).
    /// </para>
    /// </summary>
    /// <param name="companyId">معرّف الشركة من المسار.</param>
    public bool ReadsOnlyIn(Guid companyId) => ReadOnlyCompanies is { } readOnly && readOnly.Contains(companyId);

    /// <summary>هل انقضى هذا الاعتماد عند اللحظة المعطاة؟</summary>
    /// <param name="now">اللحظة الجارية كما يقرؤها مصدر الوقت المحقون.</param>
    public bool HasExpiredAt(DateTimeOffset now) => NotAfter is { } limit && now >= limit;
}

/// <summary>يحلّ اعتماد الطلب إلى هوية، أو لا يحلّه فيُغلق الباب.</summary>
internal interface IApiPrincipalResolver
{
    /// <summary>يحلّ نصّ الاعتماد الوارد. <c>null</c> يعني رفضاً — ولا يعني «ضيف».</summary>
    /// <param name="presentedToken">النصّ المقدَّم بعد <c>Bearer</c>.</param>
    ApiPrincipal? Resolve(string presentedToken);

    /// <summary>
    /// يحلّ الاعتماد بحكمٍ <b>يفرّق بين الرفض والانقضاء والإبطال</b>.
    /// <para>
    /// ولماذا نسخةٌ لا متزامنة: الإبطال يجب أن يُقرأ <b>عند الطلب التالي</b> لا عند
    /// الانقضاء، وقراءتُه تعني بلوغ مخزنٍ مشترك. ودليلُ الإعداد لا يبلغ شيئاً فتنفيذه
    /// الافتراضي هنا يلفّ <see cref="Resolve"/> ولا يُنشئ مساراً ثانياً.
    /// </para>
    /// </summary>
    /// <param name="presentedToken">النصّ المقدَّم بعد <c>Bearer</c>.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<CredentialVerdict> ResolveAsync(string presentedToken, CancellationToken cancellationToken)
        => ValueTask.FromResult(
            Resolve(presentedToken) is { } principal
                ? CredentialVerdict.Accepted(principal)
                : CredentialVerdict.Rejected);

    /// <summary>عدد الاعتمادات المُهيّأة — يُقرأ عند الإقلاع للتحقق من أن الإعداد وصل.</summary>
    int Count { get; }
}

/// <summary>
/// دليل اعتمادات مقروء من الإعداد، <b>مخزَّناً بالبصمة لا بالنصّ</b>.
/// <para>
/// الإعداد يحمل <c>SHA-256</c> للاعتماد فقط، فلا يوجد في أي ملف إعداد ولا في أي متغيّر
/// بيئة قيمةٌ تصلح للاستعمال. ومن يقرأ الإعداد لا يستطيع أن ينتحل به.
/// </para>
/// <para>
/// <b>وهذا حدٌّ مؤقّت بحكم تصميمه:</b> الشكل النهائي مزوّد هوية خارجي، وموضع الاستبدال
/// هو <see cref="IApiPrincipalResolver"/> وحده — لا نقطة نهاية واحدة تعرف من أين جاءت
/// الهوية.
/// </para>
/// </summary>
internal sealed class ConfiguredPrincipalResolver : IApiPrincipalResolver
{
    private readonly IReadOnlyDictionary<string, ApiPrincipal> _byDigest;

    /// <summary>ينشئ الدليل من مدخلات الإعداد.</summary>
    /// <param name="entries">البصمة السداسية عشرية الصغيرة مقابل الهوية.</param>
    public ConfiguredPrincipalResolver(IReadOnlyDictionary<string, ApiPrincipal> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byDigest = entries;
    }

    /// <inheritdoc />
    public int Count => _byDigest.Count;

    /// <inheritdoc />
    public ApiPrincipal? Resolve(string presentedToken)
    {
        if (string.IsNullOrEmpty(presentedToken))
        {
            return null;
        }

        return _byDigest.TryGetValue(Digest(presentedToken), out ApiPrincipal? principal) ? principal : null;
    }

    /// <summary>بصمة الاعتماد: <c>SHA-256</c> بترميز سداسي عشري صغير، بلا ثقافة ولا حالة أحرف متغيّرة.</summary>
    /// <param name="token">النصّ.</param>
    public static string Digest(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>
/// سياق المستأجر لهذا الطلب. يُملأ من الهوية المحلولة، ويبقى <see cref="TenantId.None"/>
/// إن لم تُحلّ — فأي مسار يقرؤه بلا مصادقة يجد قيمة غير مخصّصة، لا قيمة مقبولة.
/// </summary>
internal sealed class RequestTenantContext : ITenantContext
{
    /// <inheritdoc />
    public TenantId Tenant { get; private set; } = TenantId.None;

    /// <inheritdoc />
    public UserId User { get; private set; } = UserId.None;

    /// <summary>يثبّت هوية هذا الطلب. يُستدعى مرّة واحدة من وسيط المصادقة.</summary>
    /// <param name="principal">الهوية المحلولة.</param>
    public void Bind(ApiPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        Tenant = principal.Tenant;
        User = principal.User;
    }
}

/// <summary>
/// حكمُ حدّ المصادقة على اعتماد مُقدَّم — <b>ثلاثة رفوض متمايزة لا رفضٌ واحد</b>.
/// <para>
/// اعتمادٌ منقضٍ واعتمادٌ مُبطَل <b>يملكهما صاحبهما</b>، فإخباره لا يكشف له شيئاً لا
/// يعرفه، ويوفّر عليه تشخيصاً خاطئاً ومكالمةَ دعم. أمّا المختلَق فلا يتعلّم منه مقدّمُه
/// شيئاً: رمزٌ واحد لكل ما ليس في الدليل.
/// </para>
/// </summary>
/// <param name="Outcome">الحكم.</param>
/// <param name="Principal">الهوية عند القبول وحده.</param>
internal readonly record struct CredentialVerdict(CredentialOutcome Outcome, ApiPrincipal? Principal)
{
    /// <summary>رفضٌ لا يُفرَّق فيه المختلَق عن غيره.</summary>
    public static CredentialVerdict Rejected => new(CredentialOutcome.Rejected, null);

    /// <summary>انقضاء.</summary>
    public static CredentialVerdict Expired => new(CredentialOutcome.Expired, null);

    /// <summary>إبطال.</summary>
    public static CredentialVerdict Revoked => new(CredentialOutcome.Revoked, null);

    /// <summary>قبولٌ بهوية.</summary>
    /// <param name="principal">الهوية المحلولة.</param>
    public static CredentialVerdict Accepted(ApiPrincipal principal) => new(CredentialOutcome.Accepted, principal);
}

/// <summary>أحكام حدّ المصادقة الأربعة.</summary>
internal enum CredentialOutcome
{
    /// <summary>لا يقابله شيء.</summary>
    Rejected = 0,

    /// <summary>انقضى.</summary>
    Expired = 1,

    /// <summary>أُبطلت جلسته — فوراً، لا عند الانقضاء.</summary>
    Revoked = 2,

    /// <summary>مقبول.</summary>
    Accepted = 3,
}
