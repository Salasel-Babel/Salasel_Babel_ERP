using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Metering;

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
    string Code { get; }
    string NameAr { get; }
    string NameEn { get; }
    Task<int> CountAsync(NpgsqlConnection control, Guid tenantId, string periodCode,
        CancellationToken ct = default);
}

public sealed class NamedUserStrategy : IBillableUserStrategy
{
    public string Code => "Named";
    public string NameAr => "المستخدم المُسمّى";
    public string NameEn => "Named user";

    public async Task<int> CountAsync(NpgsqlConnection c, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(c, """
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

public sealed class ConcurrentUserStrategy : IBillableUserStrategy
{
    public string Code => "Concurrent";
    public string NameAr => "المستخدم المتزامن (الذروة)";
    public string NameEn => "Concurrent user (peak)";

    public async Task<int> CountAsync(NpgsqlConnection c, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(c, """
            select coalesce(max(active_users), 0)
              from control.concurrency_sample
             where tenant_id = @t and period_code = @p
            """, x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", periodCode);
            }, null, ct));
}

public sealed class ActiveInPeriodStrategy : IBillableUserStrategy
{
    public string Code => "ActiveInPeriod";
    public string NameAr => "المستخدم النشِط في الفترة";
    public string NameEn => "Active-in-period user";

    public async Task<int> CountAsync(NpgsqlConnection c, Guid tenantId, string periodCode,
        CancellationToken ct = default) =>
        (int)(await Db.ScalarAsync<long>(c, """
            select count(distinct user_ref)
              from control.usage_event
             where tenant_id = @t and period_code = @p and user_ref is not null
            """, x =>
            {
                x.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                x.AddWithValue("p", periodCode);
            }, null, ct));
}

public static class BillableUserStrategies
{
    public static readonly IBillableUserStrategy Named = new NamedUserStrategy();
    public static readonly IBillableUserStrategy Concurrent = new ConcurrentUserStrategy();
    public static readonly IBillableUserStrategy ActiveInPeriod = new ActiveInPeriodStrategy();

    /// <summary>الافتراضي المُتحفَّظ. قابل للتبديل بإعداد، ومُدرَج كسؤال مفتوح في التقرير.</summary>
    public static IBillableUserStrategy Default { get; set; } = ActiveInPeriod;

    public static readonly IReadOnlyList<IBillableUserStrategy> All =
        [Named, Concurrent, ActiveInPeriod];

    public static IBillableUserStrategy ByCode(string code) =>
        All.FirstOrDefault(s => s.Code == code)
        ?? throw new ArgumentException($"تعريف مستخدم قابل للفوترة غير معروف: «{code}»", nameof(code));
}
