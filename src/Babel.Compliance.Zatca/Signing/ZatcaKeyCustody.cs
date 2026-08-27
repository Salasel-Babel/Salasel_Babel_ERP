using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Onboarding;

namespace Babel.Compliance.Zatca.Signing;

/// <summary>
/// شكل ترميز توقيع ECDSA. <b>ليس تفصيلاً</b>: توقيع سليم رياضياً بترميز غير متوقَّع
/// يُرفض عند المتحقّق بلا رسالة تشرح السبب.
/// </summary>
public enum EcdsaSignatureFormat
{
    /// <summary>تتابع DER: <c>SEQUENCE { INTEGER r, INTEGER s }</c>. <b>طوله متغيّر</b> (‏70–72 بايتاً عادةً).</summary>
    DerSequence,

    /// <summary>‏<c>r‖s</c> بعرض ثابت (‏IEEE P1363) — 64 بايتاً لـsecp256k1. وهو ما يوجبه RFC 4051 لمعرّف الخوارزمية المُصرَّح به.</summary>
    P1363FixedWidth
}

/// <summary>
/// <b>حيازة المفتاح — حدّ واحد لا يعبره مفتاح خاص أبداً.</b>
/// <para/>
/// كل ما يخرج من هذا الحدّ: توقيع، وشهادة عامة، ومفتاح عام. ولا شيء غير ذلك.
/// <b>لا دالة تُعيد مفتاحاً خاصاً، ولا دالة تُصدّره، ولا خاصية تكشفه</b> — وغياب الدالة
/// أقوى من وجودها موثَّقة بـ«لا تستعملها».
/// <para/>
/// <b>النطاق وحدة الإصدار لا المستأجر.</b> خمسون نقطة بيع = خمسون مفتاحاً وخمس وخمسون
/// شهادة وخمسون سلسلة. مفتاحٌ واحد لمستأجر يُجمّع خمسين جهازاً على سلسلة واحدة، فيصير
/// العدّاد صفّاً ساخناً واحداً (‏ADR-0008 دليل 7) ويصير سحب شهادة واحدة إيقافاً لكل الأجهزة.
/// </summary>
public interface IZatcaKeyStore
{
    /// <summary>يولّد مفتاحاً للوحدة ويعيد مقبضاً. المفتاح لا يغادر المخزن.</summary>
    CredentialRef Create(TenantId tenant, IssuingUnitId unit, ComplianceEnvironment environment);

    /// <summary>
    /// يوقّع بصمة <b>محسوبة سلفاً</b>. لا يُجزّئ. الطول مفحوص عند الحدّ لا مفترضاً.
    /// </summary>
    byte[] SignPrecomputedDigest(CredentialRef credential, ReadOnlySpan<byte> digest, EcdsaSignatureFormat format);

    /// <summary>بايتات <c>SubjectPublicKeyInfo</c> بترميز DER. عامّة بالتعريف.</summary>
    byte[] ExportPublicKeyDer(CredentialRef credential);

    /// <summary>يبني طلب توقيع شهادة. المفتاح الخاص يُستعمل داخل المخزن ولا يخرج.</summary>
    byte[] BuildSigningRequest(CredentialRef credential, CsrSubject subject);

    void AttachCertificate(CredentialRef credential, ReadOnlyMemory<byte> certificateDer);

    /// <summary>الشهادة العامة، أو فارغ إن لم تُربط بعد.</summary>
    ReadOnlyMemory<byte> Certificate(CredentialRef credential);
}

/// <summary>
/// <b>حائز المفتاح المحلي فوق مخزن المفاتيح، وهو الحاجز الذي يمنع فخّ التجزئة المزدوجة.</b>
/// <para/>
/// الفخّ: تمرير <b>نصّ</b> البصمة المُرمَّز بـbase64 إلى دالة تُجزّئ ما يصلها من جديد
/// يُنتج توقيعاً على بصمة البصمة — <b>يتحقّق محلياً بنجاح تام ويُرفض عند الجهة</b>
/// (‏<c>docs/evidence/traps.md#fakh-double-hashing</c>).
/// <para/>
/// والمنع هنا <b>بالطول لا بالتوثيق</b>: بصمة SHA-256 اثنان وثلاثون بايتاً، ونصّها
/// المُرمَّز بـbase64 أربعة وأربعون. ففحصٌ واحد على الطول يفصل الحالتين قطعياً،
/// ويسقط قبل أن يقع أي عمل تشفيري.
/// </summary>
public sealed class ZatcaKeyCustodian(IZatcaKeyStore store, EcdsaSignatureFormat format = EcdsaSignatureFormat.DerSequence)
    : ILocalKeyCustodian
{
    /// <summary>طول بصمة SHA-256 بالبايت.</summary>
    public const int Sha256DigestLength = 32;

    public EcdsaSignatureFormat Format => format;

    public ValueTask<CredentialRef> CreateKeyAsync(
        TenantId tenant, IssuingUnitId unit, ComplianceEnvironment environment, CancellationToken ct) =>
        ValueTask.FromResult(store.Create(tenant, unit, environment));

    public ValueTask<SignatureMaterial> SignAsync(SigningInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Form != SigningInputForm.PrecomputedDigestSignDirectly)
        {
            throw new NotSupportedException(
                $"مسار الهيئة يوقّع بصمة محسوبة سلفاً حصراً؛ وصل الشكل «{input.Form}». " +
                "قبول بايتات خام هنا يعني تجزئة ثانية فوق بصمة، وهي تتحقّق محلياً وتُرفض عند الجهة. / " +
                $"the authority path signs a precomputed digest only; got '{input.Form}'.");
        }

        if (input.Payload.Length != Sha256DigestLength)
        {
            throw new ZatcaSigningException(FormattableString.Invariant($"وصل إلى الموقِّع {input.Payload.Length} بايتاً والمطلوب {Sha256DigestLength} بالضبط. ") +
                FormattableString.Invariant($"الطول {Convert.ToBase64String(new byte[Sha256DigestLength]).Length} يعني أن ما وصل هو ") +
                "**نصّ** البصمة بترميز base64 لا البصمة نفسها — وتوقيعه يُنتج توقيعاً على بصمة البصمة، " +
                "يتحقّق محلياً بنجاح تام ويُرفض عند الجهة بلا رسالة تشرح السبب. / " +
                FormattableString.Invariant($"the signer received {input.Payload.Length} bytes; exactly {Sha256DigestLength} are required."));
        }

        byte[] signature = store.SignPrecomputedDigest(input.Credential, input.Payload.Span, format);
        ReadOnlyMemory<byte> certificate = store.Certificate(input.Credential);

        return ValueTask.FromResult(new SignatureMaterial(
            signature,
            input.SignatureAlgorithm,
            certificate,
            DateTimeOffset.UtcNow));
    }

    public ValueTask AttachCertificateAsync(
        CredentialRef credential, ReadOnlyMemory<byte> certificateDer, CancellationToken ct)
    {
        store.AttachCertificate(credential, certificateDer);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadCertificateAsync(CredentialRef credential, CancellationToken ct) =>
        ValueTask.FromResult(store.Certificate(credential));
}

/// <summary>
/// مخزن مفاتيح في الذاكرة. <b>لا مفتاح ولا شهادة في المستودع، ولا على القرص</b> —
/// كل شيء يُولَّد عند التشغيل ويموت مع العملية.
/// <para/>
/// هذا هو المخزن الذي تستعمله الاختبارات، وهو <b>أيضاً</b> الشكل الصحيح لبيئة المحاكاة.
/// ولإنتاج حقيقي يُركَّب تنفيذ آخر لـ<see cref="IZatcaKeyStore"/> يقرأ من خزينة أسرار،
/// و<b>لا يُضاف مسار تحميل من ملف إلى هذا الصنف</b>: مسارٌ يقرأ مفتاحاً من القرص يُغري
/// بإيداع مفتاح اختبار «مؤقتاً».
/// </summary>
public sealed class EphemeralZatcaKeyStore : IZatcaKeyStore, IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private sealed record Entry(ECDsa Key)
    {
        public byte[] Certificate { get; set; } = [];
    }

    /// <summary>المنحنى. <b>مقيس على .NET 10.0.111: يعمل على المنصّة القياسية بلا BouncyCastle.</b></summary>
    public static ECCurve Curve => ECCurve.CreateFromFriendlyName(ZatcaProfile.CurveFriendlyName);

    public CredentialRef Create(TenantId tenant, IssuingUnitId unit, ComplianceEnvironment environment)
    {
        string handle = string.Create(CultureInfo.InvariantCulture,
            $"zatca://{environment}/{tenant.Value}/{unit.Value}/{Guid.CreateVersion7():N}");
        _entries[handle] = new Entry(ECDsa.Create(Curve));
        return new CredentialRef(handle);
    }

    public byte[] SignPrecomputedDigest(CredentialRef credential, ReadOnlySpan<byte> digest, EcdsaSignatureFormat format) =>
        KeyOf(credential).SignHash(digest, format == EcdsaSignatureFormat.DerSequence
            ? DSASignatureFormat.Rfc3279DerSequence
            : DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public byte[] ExportPublicKeyDer(CredentialRef credential) =>
        KeyOf(credential).ExportSubjectPublicKeyInfo();

    public byte[] BuildSigningRequest(CredentialRef credential, CsrSubject subject) =>
        ZatcaCertificateRequest.Build(KeyOf(credential), subject);

    public void AttachCertificate(CredentialRef credential, ReadOnlyMemory<byte> certificateDer)
    {
        if (_entries.TryGetValue(credential.Value, out Entry? entry))
        {
            entry.Certificate = certificateDer.ToArray();
        }
    }

    public ReadOnlyMemory<byte> Certificate(CredentialRef credential) =>
        _entries.TryGetValue(credential.Value, out Entry? entry) ? entry.Certificate : ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// شهادة موقَّعة ذاتياً <b>للاختبار وحده</b>. ليست شهادة امتثال ولا شهادة إنتاج،
    /// ولا تشبه ما تصدره الهيئة إلا في كونها X.509.
    /// </summary>
    public byte[] IssueSelfSignedForTesting(CredentialRef credential, string subject, TimeSpan lifetime, DateTimeOffset now)
    {
        ECDsa key = KeyOf(credential);
        CertificateRequest request = new($"CN={subject}", key, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate = request.CreateSelfSigned(now.AddMinutes(-5), now + lifetime);
        byte[] der = certificate.RawData;
        AttachCertificate(credential, der);
        return der;
    }

    private ECDsa KeyOf(CredentialRef credential) =>
        _entries.TryGetValue(credential.Value, out Entry? entry)
            ? entry.Key
            : throw new KeyNotFoundException($"لا مفتاح للمقبض {credential} / no key for handle {credential}");

    public void Dispose()
    {
        foreach (Entry entry in _entries.Values)
        {
            entry.Key.Dispose();
        }

        _entries.Clear();
    }
}

/// <summary>عطل في مسار التوقيع. يخرج بصوت عالٍ ولا يُنتج توقيعاً «تقريبياً».</summary>
public sealed class ZatcaSigningException(string message) : Exception(message);
