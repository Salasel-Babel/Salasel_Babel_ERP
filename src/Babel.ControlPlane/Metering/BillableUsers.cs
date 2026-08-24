using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Metering;

/// <summary>عدد المستخدمين القابلين للفوترة بحسب تعريف واحد بعينه.</summary>
/// <param name="StrategyCode">رمز التعريف المستعمَل (‏<c>Named</c> أو <c>Concurrent</c> أو <c>ActiveInPeriod</c>).</param>
/// <param name="NameAr">اسم التعريف بالعربية.</param>
/// <param name="NameEn">اسم التعريف بالإنجليزية.</param>
/// <param name="Count">العدد الناتج عن هذا التعريف لهذه الفترة.</param>
public sealed record BillableUserCount(string StrategyCode, string NameAr, string NameEn, int Count);

/// <summary>
/// <b>سؤال عمل مفتوح — لم يُجَب عليه، ولم يُحسم هنا.</b>
///
/// <para>«المستخدم القابل للفوترة» ثلاثة تعريفات مختلفة تُنتج ثلاثة أرقام
/// مختلفة من نفس البيانات، والفرق بينها <b>مادّي في الفاتورة</b>:</para>
///
/// <list type="bullet">
/// <item><b>مُسمّى (Named)</b> — كل حساب مستخدم غير معطّل. الأكبر رقماً،
/// والأسهل شرحاً للعميل، و<b>يُفوتر مقاعد لم يستعملها أحد</b>.</item>
/// <item><b>متزامن (Concurrent)</b> — ذروة الجلسات في آن واحد خلال الفترة.
/// الأصغر رقماً غالباً، و<b>يحتاج تتبّع جلسات موثوقاً</b> (انقطاع الشبكة
/// وإغلاق المتصفّح يجعلان «الجلسة النشِطة» تقديراً لا حقيقة).</item>
/// <item><b>نشِط في الفترة (ActiveInPeriod)</b> — من قام بعمل واحد على الأقل.
/// وسط، ومشتقّ من نفس أحداث القياس، و<b>لا يُفوتر مقعداً لم يُستعمل</b>.</item>
/// </list>
///
/// <para><b>الافتراضي هنا:</b> <c>ActiveInPeriod</c> — <b>الأكثر تحفّظاً تجاه
/// العميل</b> بين الثلاثة (لا يُفوتر ما لم يُستعمل)، والوحيد المشتقّ من بيانات
/// نلتقطها فعلاً بلا بنية إضافية. <b>هذا اختيار افتراضي هندسي لا قرار تجاري</b>:
/// الثلاثة مُنفَّذة، والتبديل إعداد لا إعادة كتابة، والحسم على المالك.</para>
///
/// <para><b>والأهم:</b> المادّة الخام للتعريفات الثلاثة تُلتقط <b>كلها</b> من
/// اليوم الأول — القائمة الاسمية، وعيّنات التزامن، وأحداث النشاط. فأي تعريف
/// يُختار لاحقاً يمكن حسابه <b>بأثر رجعي</b> على شهور مضت. اختيار تعريف واحد
/// اليوم والتقاط بياناته وحدها هو الخطأ الذي لا يُصلَح.</para>
/// </summary>
public interface IBillableUserStrategy
{
    /// <summary>الرمز الثابت للتعريف — يُخزَّن مع الرقم حتى يُعرَف بأي تعريف حُسب.</summary>
    string Code { get; }

    /// <summary>اسم التعريف بالعربية.</summary>
    string NameAr { get; }

    /// <summary>اسم التعريف بالإنجليزية.</summary>
    string NameEn { get; }

    /// <summary>يحسب عدد المستخدمين القابلين للفوترة لمستأجر في فترة فوترة.</summary>
    /// <param name="control">اتصال مفتوح بقاعدة التحكّم.</param>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="periodCode">رمز فترة الفوترة (‏<c>YYYY-MM</c>).</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>العدد بحسب هذا التعريف.</returns>
    Task<int> CountAsync(NpgsqlConnection control, Guid tenantId, string periodCode,
        CancellationToken ct = default);
}

/// <summary>كل حساب مستخدم غير معطّل خلال الفترة. الأكبر رقماً، ويُفوتر مقاعد لم يستعملها أحد.</summary>
public sealed class NamedUserStrategy : IBillableUserStrategy
{
    /// <inheritdoc />
    public string Code => "Named";
    /// <inheritdoc />
    public string NameAr => "المستخدم المُسمّى";
    /// <inheritdoc />
    public string NameEn => "Named user";

    /// <inheritdoc />
    public async Task<int> CountAsync(NpgsqlConnection control, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(control, """
            select count(*)
              from control.tenant_user u
              join control.billing_period p on p.period_code = @p
             where u.tenant_id = @t
               and u.created_at::date <= p.ends_on
               and (u.disabled_at is null or u.disabled_at::date > p.starts_on)
            """, x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", periodCode);
            }, null, ct));
}

/// <summary>ذروة الجلسات المتزامنة خلال الفترة. الأصغر غالباً، ويعتمد على تتبّع جلسات موثوق.</summary>
public sealed class ConcurrentUserStrategy : IBillableUserStrategy
{
    /// <inheritdoc />
    public string Code => "Concurrent";
    /// <inheritdoc />
    public string NameAr => "المستخدم المتزامن (الذروة)";
    /// <inheritdoc />
    public string NameEn => "Concurrent user (peak)";

    /// <inheritdoc />
    public async Task<int> CountAsync(NpgsqlConnection control, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(control, """
            select coalesce(max(active_users), 0)
              from control.concurrency_sample
             where tenant_id = @t and period_code = @p
            """, x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", periodCode);
            }, null, ct));
}

/// <summary>من قام بعمل واحد على الأقل خلال الفترة — الافتراضي المُتحفَّظ.</summary>
public sealed class ActiveInPeriodStrategy : IBillableUserStrategy
{
    /// <inheritdoc />
    public string Code => "ActiveInPeriod";
    /// <inheritdoc />
    public string NameAr => "المستخدم النشِط في الفترة";
    /// <inheritdoc />
    public string NameEn => "Active-in-period user";

    /// <inheritdoc />
    public async Task<int> CountAsync(NpgsqlConnection control, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(control, """
            select count(distinct user_ref)
              from control.usage_event
             where tenant_id = @t and period_code = @p and user_ref is not null
            """, x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", periodCode);
            }, null, ct));
}

/// <summary>التعريفات الثلاثة مُنفَّذة ومتاحة، والافتراضي المُتحفَّظ بينها.</summary>
public static class BillableUserStrategies
{
    /// <summary>تعريف «المستخدم المُسمّى».</summary>
    public static readonly IBillableUserStrategy Named = new NamedUserStrategy();
    /// <summary>تعريف «المستخدم المتزامن (الذروة)».</summary>
    public static readonly IBillableUserStrategy Concurrent = new ConcurrentUserStrategy();
    /// <summary>تعريف «المستخدم النشِط في الفترة».</summary>
    public static readonly IBillableUserStrategy ActiveInPeriod = new ActiveInPeriodStrategy();

    /// <summary>الافتراضي المُتحفَّظ. قابل للتبديل بإعداد، ومُدرَج كسؤال مفتوح في التقرير.</summary>
    public static IBillableUserStrategy Default { get; set; } = ActiveInPeriod;

    /// <summary>التعريفات الثلاثة مجتمعة — تُحسب كلها جنباً إلى جنب في التقارير.</summary>
    public static readonly IReadOnlyList<IBillableUserStrategy> All =
        [Named, Concurrent, ActiveInPeriod];

    /// <summary>يُرجِع تعريفاً برمزه، ويرمي على رمز غير معروف بدل أن يُرجِع افتراضاً صامتاً.</summary>
    /// <param name="code">رمز التعريف.</param>
    /// <returns>التعريف المطابق.</returns>
    /// <exception cref="ArgumentException">الرمز غير معروف.</exception>
    public static IBillableUserStrategy ByCode(string code) =>
        All.FirstOrDefault(s => s.Code == code)
        ?? throw new ArgumentException($"تعريف مستخدم قابل للفوترة غير معروف: «{code}»", nameof(code));
}
