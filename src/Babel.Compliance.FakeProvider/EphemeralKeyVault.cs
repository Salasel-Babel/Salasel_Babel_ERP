using System.Collections.Concurrent;
using System.Globalization;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>
/// خزينة مفاتيح في الذاكرة. <b>لا مفتاح ولا شهادة في المستودع</b> — كل شيء يُولَّد
/// عند التشغيل ويموت مع العملية.
/// <para/>
/// <b>مقيس على .NET 10.0.111:</b> منحنى <c>secp256k1</c> يعمل على
/// <c>System.Security.Cryptography</c> القياسية، بما في ذلك <c>CreateSelfSigned</c>
/// وطلب توقيع شهادة بشكل كامل مع امتداد قالب ومعرّفات RDN مخصّصة داخل SAN.
/// <b>BouncyCastle غير مطلوب.</b>
/// </summary>
public sealed class EphemeralKeyVault : IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    private sealed record Entry(ECDsa Key, X509Certificate2? Certificate)
    {
        public X509Certificate2? Certificate { get; set; } = Certificate;
    }

    /// <summary>المنحنى مقيس ويعمل. لا يُغيَّر إلا بمواصفة رسمية.</summary>
    public static ECCurve Curve => ECCurve.CreateFromFriendlyName("secP256k1");

    public CredentialRef Create(TenantId tenant, IssuingUnitId unit, ComplianceEnvironment environment)
    {
        var handle = string.Create(CultureInfo.InvariantCulture,
            $"vault://{environment}/{tenant.Value}/{unit.Value}/{Guid.CreateVersion7():N}");
        _entries[handle] = new Entry(ECDsa.Create(Curve), null);
        return new CredentialRef(handle);
    }

    public ECDsa Key(CredentialRef credential) =>
        _entries.TryGetValue(credential.Value, out var e)
            ? e.Key
            : throw new KeyNotFoundException($"لا مفتاح للمقبض {credential} / no key for handle");

    public void AttachCertificate(CredentialRef credential, X509Certificate2 certificate)
    {
        if (_entries.TryGetValue(credential.Value, out var e)) e.Certificate = certificate;
    }

    public X509Certificate2? Certificate(CredentialRef credential) =>
        _entries.TryGetValue(credential.Value, out var e) ? e.Certificate : null;

    /// <summary>
    /// شهادة موقَّعة ذاتياً للاختبار فقط. <b>ليست شهادة امتثال ولا شهادة إنتاج</b>،
    /// ولا تشبه ما تصدره الجهة إلا في كونها X.509.
    /// </summary>
    public X509Certificate2 IssueSelfSigned(CredentialRef credential, string subject, TimeSpan lifetime)
    {
        var key = Key(credential);
        var request = new CertificateRequest($"CN={subject}", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        var cert = request.CreateSelfSigned(now.AddMinutes(-5), now + lifetime);
        AttachCertificate(credential, cert);
        return cert;
    }

    /// <summary>
    /// طلب توقيع شهادة بالشكل الذي أثبت القياس أنه ممكن على المنصّة القياسية:
    /// امتداد قالب على المعرّف <c>1.3.6.1.4.1.311.20.2</c>، و SAN من نوع
    /// <c>directoryName</c> يحمل معرّفات RDN مخصّصة.
    /// <para/>
    /// <b>ما هو مقيس:</b> أن المنصّة تبنيه.
    /// <b>ما هو غير مُتحقَّق منه:</b> أيّ قيمة تذهب في أيّ معرّف، وأيّها إلزامي.
    /// </summary>
    [Provisional("محتوى طلب توقيع الشهادة: قيم معرّفات RDN المخصّصة، واسم القالب، والحقول الإلزامية",
        DerivedFrom = "الشكل مقيس على .NET 10؛ المحتوى غير مُتحقَّق منه من أي مصدر رسمي",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة CSR المنشورة على بوابة الهيئة")]
    public byte[] BuildSigningRequest(CredentialRef credential, CsrSubject subject)
    {
        var key = Key(credential);
        var dn = $"CN={subject.CommonName}, OU={subject.OrganisationalUnitName}, " +
                 $"O={subject.OrganisationName}, C={subject.CountryCode}";
        var request = new CertificateRequest(new X500DistinguishedName(dn), key, HashAlgorithmName.SHA256);

        // امتداد قالب الشهادة — المعرّف مُتحقَّق منه من تنفيذات مفتوحة المصدر، لا من الهيئة.
        var templateBytes = new AsnWriter(AsnEncodingRules.DER);
        templateBytes.WriteCharacterString(UniversalTagNumber.UTF8String, subject.CertificateTemplateName);
        request.CertificateExtensions.Add(
            new X509Extension(new Oid("1.3.6.1.4.1.311.20.2"), templateBytes.Encode(), critical: false));

        // SAN من نوع directoryName يحمل معرّفات RDN مخصّصة.
        // SubjectAlternativeNameBuilder لا يدعم directoryName، فتُبنى البنية بالترميز مباشرة:
        //   SubjectAltName ::= SEQUENCE OF GeneralName ، و GeneralName directoryName هو [4] Name.
        var rdnBuilder = new X500DistinguishedNameBuilder();
        foreach (var kv in subject.SubjectAlternativeNameRdns.OrderBy(k => k.Key, StringComparer.Ordinal))
            rdnBuilder.Add(kv.Key, kv.Value, UniversalTagNumber.UTF8String);
        var directoryName = rdnBuilder.Build().RawData;

        var sanWriter = new AsnWriter(AsnEncodingRules.DER);
        using (sanWriter.PushSequence())
        using (sanWriter.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true)))
            sanWriter.WriteEncodedValue(directoryName);

        request.CertificateExtensions.Add(
            new X509Extension(new Oid("2.5.29.17"), sanWriter.Encode(), critical: false));

        return request.CreateSigningRequest();
    }

    public void Dispose()
    {
        foreach (var e in _entries.Values)
        {
            e.Key.Dispose();
            e.Certificate?.Dispose();
        }
        _entries.Clear();
    }
}
