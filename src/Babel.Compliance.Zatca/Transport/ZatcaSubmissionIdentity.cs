using System.Globalization;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Transport;

/// <summary>
/// <b>هوية الإرسال — تُعلَن مرة واحدة، وتُقرأ من هنا في كل موضع يسأل عنها.</b>
/// <para/>
/// الدرس الذي دفعه هذا المشروع ثمنه في محرّك الترحيل، ويقع هنا بالضبط مرة أخرى:
/// <b>سؤال الهوية يُطرح في مواضع أكثر مما تُعلَن فيه الهوية.</b> في الإرسال إلى الجهة
/// يُطرح في أربعة مواضع على الأقل:
/// <list type="number">
///   <item>معرّف المستند داخل جسم الطلب (‏<c>uuid</c>).</item>
///   <item>بصمة الفاتورة داخل جسم الطلب (‏<c>invoiceHash</c>).</item>
///   <item>ترويسة الإحكام التي نُرسلها من جانبنا.</item>
///   <item>بصمة المحتوى التي نطابق بها لاحقاً عند حسم مهلة غامضة.</item>
/// </list>
/// فلو اشتُقّ كل واحد منها من مصدره الخاص لأمكن أن تتفق ثلاثة وتخالف الرابعة — وتلك
/// المخالفة <b>لا تُرى</b>: الطلب ينجح، والمطابقة اللاحقة تفشل بهدوء، فتصير الفاتورة
/// «غير معروفة» إلى الأبد أو تُرسَل مرتين
/// (‏<c>docs/evidence/traps.md#fakh-at-least-once-delivery-without-an-idempotency-key</c>).
/// <para/>
/// <b>العلاج بنيوي:</b> هذا النوع هو <b>المُنشئ الوحيد</b> لكل الأربعة، ولا يبنيه أحد
/// من مكوّناته المفردة — يُبنى من طلب مقاصة أو إرسال إبلاغ، فيستحيل أن يُبنى ناقصاً.
/// </summary>
public sealed record ZatcaSubmissionIdentity
{
    private ZatcaSubmissionIdentity(
        Guid documentUuid,
        IssuingUnitId issuingUnit,
        long counter,
        int attemptNo,
        string payloadFingerprint,
        ReadOnlyMemory<byte> payload)
    {
        DocumentUuid = documentUuid;
        IssuingUnit = issuingUnit;
        Counter = counter;
        AttemptNo = attemptNo;
        PayloadFingerprint = payloadFingerprint;
        Payload = payload;
    }

    public Guid DocumentUuid { get; }

    public IssuingUnitId IssuingUnit { get; }

    public long Counter { get; }

    public int AttemptNo { get; }

    /// <summary>بصمة البايتات المُرسَلة كما حسبها المُنسِّق. <b>لا تُعاد حسابها هنا.</b></summary>
    public string PayloadFingerprint { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>‏<c>uuid</c> كما يُكتب في جسم الطلب.</summary>
    public string BodyUuid => DocumentUuid.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>بايتات المستند بترميز base64، كما تُكتب في جسم الطلب.</summary>
    public string BodyInvoiceBase64 => Convert.ToBase64String(Payload.Span);

    /// <summary>
    /// مفتاح الإحكام الذي <b>نرسله نحن</b>.
    /// <para/>
    /// <b>ولا يُفترض أن الجهة تحترمه.</b> لا يوجد مفتاح إحكام موثَّق من جانبها، ولذلك
    /// <c>ProviderCapabilities.DeduplicatesBySubmissionFingerprint</c> يبقى <c>false</c>
    /// في هذا المزوّد حتى يُثبَت العكس <b>في البيئة الاختبارية</b>. إرساله رغم ذلك ليس
    /// عبثاً: يوم يُدعَم، تعمل الحماية بلا تغيير في الشيفرة؛ وهو اليوم أثر يُقرأ في سجلّ
    /// الجهة عند التحقيق في تكرار.
    /// <para/>
    /// المفتاح <b>ثابت عبر المحاولات</b> على المستند نفسه — لأنه مشتقّ من هوية المستند
    /// وموضعه في السلسلة، لا من رقم المحاولة. مفتاح يتغيّر مع كل محاولة ليس مفتاح إحكام.
    /// </summary>
    [Provisional("هل تقبل الجهة ترويسة إحكام أصلاً، وباسم أي ترويسة، وعلى أي مفتاح",
        DerivedFrom = "لا مصدر — لا يوجد مفتاح إحكام موثَّق من جانب الجهة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة الواجهة: هل توجد ترويسة إحكام، وهل يُرفض الإرسال المطابق")]
    public string IdempotencyKey => string.Create(
        CultureInfo.InvariantCulture,
        $"{IssuingUnit.Value}:{Counter}:{BodyUuid}");

    public static ZatcaSubmissionIdentity From(ClearanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ZatcaSubmissionIdentity(
            request.DocumentUuid, request.IssuingUnit, request.Chain.Counter,
            request.AttemptNo, request.SubmissionFingerprint, request.Payload.Bytes);
    }

    public static ZatcaSubmissionIdentity From(ReportingSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        return new ZatcaSubmissionIdentity(
            submission.DocumentUuid, submission.IssuingUnit, submission.Chain.Counter,
            submission.AttemptNo, submission.SubmissionFingerprint, submission.Payload.Bytes);
    }

    /// <summary>
    /// بصمة الفاتورة كما تُكتب في جسم الطلب.
    /// <b>تُقرأ من المستند المختوم، لا تُعاد حسابها من بيانات المستند</b> — إعادة الحساب
    /// من مصدر ثانٍ هي بالضبط ما يجعل جسم الطلب يخالف ما وُقِّع عليه.
    /// </summary>
    public string InvoiceHash(Func<ReadOnlyMemory<byte>, string> readFromSealedDocument)
    {
        ArgumentNullException.ThrowIfNull(readFromSealedDocument);
        return readFromSealedDocument(Payload);
    }
}
