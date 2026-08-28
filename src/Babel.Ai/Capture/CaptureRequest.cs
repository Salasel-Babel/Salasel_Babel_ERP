using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Ai.Capture;

/// <summary>
/// <b>طلب التقاط — إشارةٌ إلى مستند مُودَع، لا بايتاته.</b>
/// <para>
/// <b>ما كان قبله ولماذا تغيّر:</b> كانت <c>CaptureAsync</c> تأخذ
/// <c>ExtractionRequest</c> ومعها <c>ReadOnlyMemory&lt;byte&gt;</c>. وحين يصل ذلك من
/// عميل عبر HTTP فالشكل الوحيد الذي تعبر به بايتاتٌ داخل جسم JSON هو <c>base64</c>:
/// صورةُ فاتورة تسافر نصّاً، وتنتفخ الثلث، وتهبط في سجلّات الطلبات كاملةً،
/// <b>ولا تُخزَّن في مكان</b>. فالمستند الذي على القيد أن يستند إليه كان يُقرأ ثم
/// يختفي، ويبقى في السجلّ وحده.
/// </para>
/// <para>
/// <b>وما صار:</b> البايتات تُودَع مرّة عبر <c>IAttachmentStore.PutAsync</c> فيعود
/// <see cref="AttachmentId"/>، ويحمل هذا الطلب المعرّف وحده. فالبايتات تعبر السلك
/// <b>مرّة واحدة وثنائيةً</b>، وتُخزَّن، وتُجزَّأ، وتبقى سنداً قابلاً للعرض على مدقّق.
/// </para>
/// </summary>
public sealed record CaptureRequest
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>
    /// المستند المُودَع. <b>يُقرأ داخل مستأجر هذا الطلب</b> — معرّفٌ من مستأجر آخر
    /// لا يجد شيئاً.
    /// </summary>
    public required AttachmentId Document { get; init; }

    /// <summary>القناة التي وصل منها.</summary>
    public required CaptureChannel Channel { get; init; }

    /// <summary>
    /// حمولة رمز الاستجابة السريعة كما مُسحت، أو <c>null</c>. مسارٌ مستقلّ عن الصورة:
    /// يُفكّ عندنا ولا يُطلب من النموذج تأكيده.
    /// </summary>
    public string? QrPayload { get; init; }
}
