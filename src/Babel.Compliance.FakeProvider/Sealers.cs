using System.Security.Cryptography;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>
/// <b>شكل «نحن نحوز المفتاح».</b> الختم يقع عندنا، والبايتات تُجمَّد ولا تتغيّر بعدها أبداً.
/// <para/>
/// نتيجة بنيوية مجانية: كل إعادة إرسال <b>مطابقة بايتياً</b> للأولى، لأن التوقيع
/// حُسب مرة واحدة وخُزِّن. هذه الخاصية لا تحتاج وعداً من أحد.
/// </summary>
public sealed class SelfHeldSealer(ILocalKeyCustodian custodian) : IDocumentSealer
{
    public KeyCustody Custody => KeyCustody.SelfHeld;

    public async ValueTask<SealedPayload> SealAsync(
        SealingContext context, RenderedDocument document, CancellationToken cancellationToken)
    {
        // البصمة محسوبة سلفاً، والتصريح بذلك صريح: الموقِّع ملزم بألّا يُجزّئ مرة أخرى.
        // هذا هو الفخّ الأول من فخّي هذا المجال، ومنعُه هنا بالنوع لا بالتعليق.
        var signature = await custodian.SignAsync(new SigningInput(
            context.IssuingUnit,
            context.Credential,
            document.SigningInputDigest,
            SigningInputForm.PrecomputedDigestSignDirectly,
            "SHA-256",
            "ECDSA-secp256k1"), cancellationToken);

        var sealed_ = EmbedSignature(document.Body.Span, signature);
        return new SealedPayload(
            SealState.SealedLocally,
            sealed_,
            signature,
            SHA256.HashData(sealed_));
    }

    /// <summary>
    /// تضمين التوقيع داخل امتدادات المستند. <b>الشكل مؤقَّت</b> — الموضع الحقيقي
    /// وبنيته يُثبَّتان من مواصفة التوقيع.
    /// </summary>
    [Provisional("موضع التوقيع داخل المستند وبنيته (XAdES) وترتيب عناصره",
        DerivedFrom = "لا مصدر رسمي — تضمين مؤقَّت لتشغيل خط الأنابيب",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة الختم التشفيري وبنية التوقيع المنشورة")]
    private static byte[] EmbedSignature(ReadOnlySpan<byte> body, SignatureMaterial signature)
    {
        var text = System.Text.Encoding.UTF8.GetString(body);
        var block =
            "<UBLExtensions><UBLExtension><ExtensionContent>" +
            "<SignatureValue>" + Convert.ToBase64String(signature.Signature.Span) + "</SignatureValue>" +
            "<BinarySecurityToken>" + signature.BinarySecurityTokenDoubleBase64 + "</BinarySecurityToken>" +
            "</ExtensionContent></UBLExtension></UBLExtensions>";

        const string placeholder = "<UBLExtensions><UBLExtension><ExtensionContent></ExtensionContent></UBLExtension></UBLExtensions>";
        var replaced = text.Contains(placeholder, StringComparison.Ordinal)
            ? text.Replace(placeholder, block, StringComparison.Ordinal)
            : block + text;
        return new System.Text.UTF8Encoding(false).GetBytes(replaced);
    }
}

/// <summary>
/// <b>شكل «المزوّد يحوز المفتاح».</b> لا نملك ما نختم به؛ نسلّم المستند غير مختوم
/// والمزوّد يختمه <b>داخل نداء الإرسال</b>.
/// <para/>
/// نتيجة بنيوية مكلفة: <b>لا نملك البايتات التي وصلت الجهة</b>، ولا نستطيع ضمان
/// أن إعادة الإرسال تحمل البايتات نفسها — لأن المزوّد يعيد الختم، وتوقيع ECDSA
/// عشوائي بطبيعته. المطابقة البايتية هنا <b>وعد تعاقدي</b> لا خاصية بنيوية.
/// </summary>
public sealed class ProviderHeldSealer : IDocumentSealer
{
    public KeyCustody Custody => KeyCustody.ProviderHeld;

    public ValueTask<SealedPayload> SealAsync(
        SealingContext context, RenderedDocument document, CancellationToken cancellationToken)
    {
        // لا ختم هنا. البصمة على الجسم غير المختوم: هي كل ما نستطيع مطابقته لاحقاً،
        // وهي **ليست** بصمة ما سيصل الجهة.
        var body = document.Body.ToArray();
        return ValueTask.FromResult(new SealedPayload(
            SealState.UnsealedForProviderSeal,
            body,
            Signature: null,
            Fingerprint: SHA256.HashData(body)));
    }
}

/// <summary>
/// حائز المفتاح المحلي فوق الخزينة العابرة. <b>يرفض المدخل الذي لا يفهم شكله</b> —
/// وهذا هو الحاجز الذي يمنع التجزئة المزدوجة.
/// </summary>
public sealed class VaultKeyCustodian(EphemeralKeyVault vault) : ILocalKeyCustodian
{
    public ValueTask<CredentialRef> CreateKeyAsync(
        TenantId tenant, IssuingUnitId unit, ComplianceEnvironment environment, CancellationToken ct) =>
        ValueTask.FromResult(vault.Create(tenant, unit, environment));

    public ValueTask<SignatureMaterial> SignAsync(SigningInput input, CancellationToken ct)
    {
        var key = vault.Key(input.Credential);

        var signature = input.Form switch
        {
            // البصمة محسوبة: يُوقَّع عليها مباشرةً. تجزئتها مرة أخرى تنتج تجزئة مزدوجة
            // تتحقّق محلياً وتفشل عند الجهة — وهي أشهر أسبوع ضائع في هذا المجال.
            SigningInputForm.PrecomputedDigestSignDirectly =>
                key.SignHash(input.Payload.Span),

            SigningInputForm.RawBytesToHashThenSign =>
                key.SignData(input.Payload.Span, HashAlgorithmName.SHA256),

            _ => throw new NotSupportedException($"شكل مدخل توقيع غير مدعوم: {input.Form}")
        };

        var cert = vault.Certificate(input.Credential);
        return ValueTask.FromResult(new SignatureMaterial(
            signature,
            input.SignatureAlgorithm,
            cert?.RawData ?? [],
            DateTimeOffset.UtcNow));
    }

    public ValueTask AttachCertificateAsync(CredentialRef credential, ReadOnlyMemory<byte> certificateDer, CancellationToken ct)
    {
        if (!certificateDer.IsEmpty)
            vault.AttachCertificate(credential, System.Security.Cryptography.X509Certificates
                .X509CertificateLoader.LoadCertificate(certificateDer.Span));
        return ValueTask.CompletedTask;
    }
}
