using Babel.Ai.Attestation;
using Babel.Compliance.Zatca.Qr;
using Babel.SharedKernel;

namespace Babel.Ai.Tests.Support;

/// <summary>
/// <b>الوصلة بين حدّ الالتقاط وفاكّ رمز الهيئة.</b> عشرون سطراً، وموضعها الطبيعي هو
/// الجذر التركيبي (‏<c>Babel.Api</c>) — وهي هنا لأن ذلك المشروع يملكه فرعٌ آخر اليوم.
/// <para>
/// <b>ووجودها خارج <c>Babel.Ai</c> ليس تفصيلاً:</b> الوحدة الأفقية لا تعتمد على مزوّد
/// الالتزام (القاعدة 3)، فلو سكنت هذه الوصلة داخل الوحدة لكسرت الحدّ الذي يجعل تبديل
/// المزوّد ممكناً. الفاكّ يبقى مع مُرمِّزه، والوحدة ترى واجهةً تملكها.
/// </para>
/// </summary>
internal sealed class ZatcaQrAttestationReader : IAttestedQrReader
{
    /// <inheritdoc />
    public Result<AttestedInvoiceFacts> Read(string qrPayload)
    {
        ArgumentNullException.ThrowIfNull(qrPayload);

        try
        {
            ZatcaQrContents contents = ZatcaQrReader.Read(qrPayload);

            return Result<AttestedInvoiceFacts>.Success(new AttestedInvoiceFacts
            {
                SellerName = contents.SellerName,
                SellerVatNumber = contents.SellerVatNumber,
                IssuedAt = contents.IssuedAt,
                GrossTotal = contents.GrossTotal,
                TaxTotal = contents.TaxTotal,
                CarriesSignature = contents.IsCryptographicallyAttested,
            });
        }
        catch (ZatcaQrException error)
        {
            // الرمز المعطوب حالة متوقّعة لا خلل برمجي: يُعاد قيمةً، ونصّ الرفض يعبر كما هو
            // لأنه يسمّي الوسم والعطل — وهو ما يحتاجه من ينظر في الشاشة.
            return Result<AttestedInvoiceFacts>.Failure(new Error(
                "ai.capture.qr_unreadable",
                "رمز الفاتورة غير مقروء: " + error.Message,
                "The invoice QR could not be read: " + error.Message));
        }
    }
}
