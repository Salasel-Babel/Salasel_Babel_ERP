using System.Collections.Concurrent;
using System.Globalization;
using Babel.Api.Endpoints;
using Babel.Api.Errors;

namespace Babel.Api.Security;

/// <summary>
/// <b>حدّ معدّل على الأبواب المفتوحة</b> — بسيطٌ وصريح، ونافذةٌ ثابتة لكل مفتاح.
/// <para>
/// <b>ولماذا وُجد:</b> ‏<c>POST /api/v1/access/sessions</c> و<c>…/renewal</c> و
/// <c>POST /api/v1/tenants</c> أبوابٌ <b>يطرقها من لا اعتماد له</b>، وكلٌّ منها يبلغ
/// قاعدة البيانات ويسكّ سرّاً. وغيابُ الحدّ كان دَيناً مُعلَناً في ADR-0045 §7 بند ٤
/// ولم يُبنَ. والاعتماد 256 بتاً فالتخمين غير عملي — <b>وليس هذا ما يشتريه حدّ
/// المعدّل</b>: ما يشتريه أن لا يستطيع طارقٌ واحد أن يفتح ألف مستأجر في دقيقة، ولا أن
/// يستهلك مجمّع الاتصالات بمحاولات دخول متتابعة فيُسقط الخدمة عن أصحابها.
/// </para>
/// <para>
/// <b>ومفتاحان لا واحد:</b> <b>لكل عنوان</b> — فطارقٌ واحد لا يزيح غيره — و<b>لكل
/// معرّف</b>: بصمة الاعتماد المُقدَّم على بابَي الجلسة، ومفتاحُ الطلب على باب التسجيل.
/// والحدُّ بالعنوان وحده يُلتَفّ عليه بشبكة عناوين، والحدُّ بالمعرّف وحده يُلتَفّ عليه
/// بتغيير المعرّف في كل محاولة. وواحدٌ منهما يكفي لِرَدّ الطلب.
/// </para>
/// <para>
/// <b>ولا سرّ يُخزَّن في هذا الملفّ</b>: مفتاح المعرّف الذي يصله <b>بصمة</b> يحسبها
/// المستدعي، لا نصّ اعتماد. وذاكرةُ حدٍّ تحمل اعتمادات صالحة أسوأ من غياب الحدّ.
/// </para>
/// <para>
/// <b>وهو في ذاكرة العملية</b>: خادمان خلف موزّع حِمل يحملان عدّادين مستقلّين، فالحدّ
/// الفعلي مضروبٌ في عدد الخوادم. وهذا <b>مُعلَن لا مُخفى</b>، والعلاج عدّاد مشترك
/// (Redis وأشباهه) وهو قرارُ بنيةٍ لا سطر — ودَينُه مكتوب في القرار.
/// </para>
/// </summary>
internal sealed class OpenDoorRateGuard
{
    /// <summary>
    /// الحدّ الافتراضي: عدد الطلبات المقبولة على الباب الواحد من المفتاح الواحد في
    /// الدقيقة.
    /// <para>
    /// وهو <b>مكبحٌ لا حصّة عمل</b>: الرقم واسعٌ عمداً كي لا يُسقط استعمالاً مشروعاً
    /// من خلف بوّابة تشترك في عنوان واحد، وضيّقٌ بما يكفي لأن يجعل الطرق الآلي مكلفاً.
    /// ويُضبَط من الإعداد لأن العدد الصحيح يعتمد على النشر لا على الشيفرة.
    /// </para>
    /// </summary>
    public const int DefaultPerMinute = 300;

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private readonly int _perMinute;

    /// <summary>ينشئ الحارس.</summary>
    /// <param name="clock">مصدر الوقت — محقونٌ كي تكون النافذة قابلة للتحريك في اختبار.</param>
    /// <param name="perMinute">الحدّ لكل مفتاح في الدقيقة.</param>
    public OpenDoorRateGuard(TimeProvider clock, int perMinute = DefaultPerMinute)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThan(perMinute, 1);

        _clock = clock;
        _perMinute = perMinute;
    }

    /// <summary>طول النافذة الثابتة.</summary>
    public static TimeSpan Window60 { get; } = TimeSpan.FromMinutes(1);

    /// <summary>الحدّ المُطبَّق فعلاً — يُقرأ في الاختبار ولا يُخمَّن.</summary>
    public int PerMinute => _perMinute;

    /// <summary>
    /// يحاول أن يحجز طلباً على مفتاح. <c>false</c> يعني تجاوز الحدّ، ومعه ثوانٍ حتى
    /// انتهاء النافذة.
    /// </summary>
    /// <param name="door">المسار — جزءٌ من المفتاح، فلا يستهلك بابٌ حصّة بابٍ آخر.</param>
    /// <param name="key">العنوان أو المعرّف.</param>
    /// <param name="retryAfterSeconds">الثواني المتبقّية من النافذة عند الرفض.</param>
    public bool TryAcquire(string door, string key, out int retryAfterSeconds)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        long slot = now.ToUnixTimeSeconds() / (long)Window60.TotalSeconds;
        string composed = door + "\n" + key;

        Window window = _windows.AddOrUpdate(
            composed,
            _ => new Window(slot, 1),
            (_, existing) => existing.Slot == slot
                ? existing with { Count = existing.Count + 1 }
                : new Window(slot, 1));

        // الكنس عند الكتابة لا بمؤقّت: قاموسٌ ينمو بمفتاح لكل عنوان طارق هو تسريب
        // ذاكرة يفتحه الطارق نفسه — وهو أسهل ما يُهاجَم به حارسٌ وُجد لصدّ الطرق.
        if (_windows.Count > MaximumTrackedKeys)
        {
            Sweep(slot);
        }

        if (window.Count <= _perMinute)
        {
            retryAfterSeconds = 0;
            return true;
        }

        long endsAt = (slot + 1) * (long)Window60.TotalSeconds;
        retryAfterSeconds = (int)Math.Max(1, endsAt - now.ToUnixTimeSeconds());
        return false;
    }

    /// <summary>سقفُ المفاتيح المتتبَّعة قبل كنس النوافذ المنقضية.</summary>
    private const int MaximumTrackedKeys = 20_000;

    private void Sweep(long slot)
    {
        foreach (KeyValuePair<string, Window> entry in _windows)
        {
            if (entry.Value.Slot < slot)
            {
                _windows.TryRemove(entry);
            }
        }
    }

    private readonly record struct Window(long Slot, int Count);
}

/// <summary>
/// وسيط حدّ المعدّل — <b>قبل التوجيه وقبل المصادقة</b>، على الأبواب المُعلَنة وحدها.
/// </summary>
internal static class OpenDoorRateLimiting
{
    /// <summary>ترويسة المهلة كما يعرّفها <c>RFC 9110 §10.2.3</c>.</summary>
    public const string RetryAfterHeader = "Retry-After";

    /// <summary>الرمز الثابت الذي يقرؤه العميل — لا نصّ الرسالة.</summary>
    public const string Code = "rate.too_many_requests";

    /// <summary>يضيف وسيط حدّ المعدّل إلى خط المعالجة.</summary>
    /// <param name="app">التطبيق.</param>
    public static void UseOpenDoorRateLimit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(static async (context, next) =>
        {
            string path = context.Request.Path.Value ?? "/";

            if (!OpenDoors.IsRateLimited(path))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            OpenDoorRateGuard guard = context.RequestServices.GetRequiredService<OpenDoorRateGuard>();

            if (guard.TryAcquire(path, AddressOf(context), out int retryAfter))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            await RefuseAsync(context, retryAfter).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// يكتب رفض التجاوز: <c>429</c>، وترويسة <c>Retry-After</c>، وجسم مشكلة بلغتين.
    /// </summary>
    /// <param name="context">سياق الطلب.</param>
    /// <param name="retryAfterSeconds">الثواني حتى انتهاء النافذة.</param>
    public static async Task RefuseAsync(HttpContext context, int retryAfterSeconds)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers[RetryAfterHeader] =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        await HttpProblemResults
            .Code(
                context,
                Code,
                "طلباتٌ أكثر ممّا يقبله هذا الباب في الدقيقة. وهذا بابٌ يُخدَم بلا اعتماد، فحدُّ معدّله يحمي "
                + "من يستعمله من طارقٍ آلي. أعِد المحاولة بعد المدّة في ترويسة Retry-After، ولم يتغيّر شيء في "
                + "بياناتك ولم يُنشَأ شيء.",
                "More requests than this door accepts per minute. This door is served without a credential, so its "
                + "rate limit protects its users from an automated knocker. Retry after the interval in the "
                + "Retry-After header; nothing in your data changed and nothing was created.",
                status: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(context)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// عنوان الطالب.
    /// <para>
    /// <b>ولا تُقرأ ترويسة <c>X-Forwarded-For</c> هنا</b>: يكتبها العميل، فالاعتماد
    /// عليها بلا وكيلٍ موثوق مُهيَّأ يجعل الحدّ يُلتَفّ عليه بترويسة مختلَقة في كل طلب —
    /// أي حدّاً يبدو موجوداً وليس كذلك. ووراء وكيلٍ عكسي تُضبَط الترويسات المُوثَّقة في
    /// إعداد النشر (‏<c>ForwardedHeaders</c>) فيصل العنوان الصحيح إلى هنا من طريقه.
    /// </para>
    /// </summary>
    private static string AddressOf(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
