using Babel.SharedKernel;

namespace Babel.Ai.Attestation;

/// <summary>
/// وقائع مُصدَّقة مأخوذة من رمز الفاتورة. <b>ليست قراءة ضوئية</b>: البائع ورقمه الضريبي
/// والطابع الزمني والإجمالي والضريبة كتبها المُصدِر داخل الرمز، وفي المرحلة الثانية
/// وقّعها بمفتاحه.
/// </summary>
public sealed record AttestedInvoiceFacts
{
    /// <summary>اسم البائع كما كتبه — سجلٌّ لا ترجمة (‏ADR-0021).</summary>
    public required string SellerName { get; init; }

    /// <summary>رقم التسجيل الضريبي للبائع.</summary>
    public required string SellerVatNumber { get; init; }

    /// <summary>لحظة الإصدار.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public required decimal GrossTotal { get; init; }

    /// <summary>مبلغ الضريبة.</summary>
    public required decimal TaxTotal { get; init; }

    /// <summary>
    /// هل يحمل الرمز مادة تشفيرية (بصمة وتوقيع ومفتاح عام)؟
    /// <b>رمز المرحلة الأولى مُصدَّق بمعنى «كتبه المُصدِر» لا بمعنى «وقّعه»</b>، والفرق
    /// يُعرض للإنسان ولا يُطمس تحت كلمة واحدة.
    /// </summary>
    public required bool CarriesSignature { get; init; }
}

/// <summary>
/// حدّ قراءة الرمز — <b>واجهة نملكها</b>، ووراءها فاكّ ترميز الهيئة في
/// <c>Babel.Compliance.Zatca</c>.
/// <para>
/// <b>ولماذا واجهة لا نداء مباشر:</b> وحدة <c>Babel.Ai</c> وحدة أفقية، ولا يجوز لها أن
/// تعتمد على مزوّد الالتزام ولا على وحدة أفقية أخرى (القاعدة 3). فالوصلة تُركَّب في
/// الجذر التركيبي، ويبقى الفاكّ حيث يجب أن يكون: عند حدّ الالتزام مع مُرمِّزه.
/// </para>
/// </summary>
public interface IAttestedQrReader
{
    /// <summary>يقرأ حمولة رمز. الفشل قيمة لا استثناء — ورمزٌ معطوب حالة متوقّعة لا خلل برمجي.</summary>
    /// <param name="qrPayload">حمولة الرمز كما مُسحت.</param>
    Result<AttestedInvoiceFacts> Read(string qrPayload);
}
