using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// إثبات أن الحدّ يحتمل الشكلين فعلاً: <b>المُنسِّق نفسه، بلا تفريع واحد على شكل الحيازة،
/// يقود المزوّدين إلى النتيجة نفسها.</b>
/// </summary>
public class CustodyTests
{
    [Theory]
    [InlineData(KeyCustody.SelfHeld)]
    [InlineData(KeyCustody.ProviderHeld)]
    public async Task The_same_orchestration_drives_both_custody_shapes_end_to_end(KeyCustody custody)
    {
        using var h = new Harness(custody);
        await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        var cleared = await h.Service.ClearAsync(
            h.NewDocument(ComplianceFlow.Clearance, "INV-X1"), TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Accepted, cleared.Status);

        var receipt = await h.Service.QueueForReportingAsync(
            h.NewDocument(ComplianceFlow.Reporting, "SIMP-X1"), TestContext.Current.CancellationToken);
        await h.Service.DrainReportingQueueAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(ComplianceStatus.Accepted, h.Record(receipt.DocumentId).Status);

        // عدّاد واحد متسلسل لوحدة الإصدار مهما كان شكل الحيازة.
        Assert.Equal(2, receipt.Counter);
        h.Ledger.AssertUntouched();
    }

    [Theory]
    [InlineData(KeyCustody.SelfHeld)]
    [InlineData(KeyCustody.ProviderHeld)]
    public async Task Onboarding_has_the_same_shape_under_both_custody_models(KeyCustody custody)
    {
        using var h = new Harness(custody);
        var registration = await h.OnboardAsync(ct: TestContext.Current.CancellationToken);

        Assert.Equal(OnboardingStage.Active, registration.Stage);
        Assert.False(registration.Credential.IsNone);
        Assert.True(registration.CanIssue);
        Assert.NotNull(registration.CertificateNotAfter);

        // المقبض مقبض: لا مادة مفتاح عبرت الحدّ في أي اتجاه.
        Assert.StartsWith("vault://", registration.Credential.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_certificate_signing_request_carries_the_template_extension_and_five_custom_RDNs()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        var csr = await h.Provider.Onboarding.CreateSigningRequestAsync(
            new CsrRequest(Harness.Tenant, Harness.Unit, ComplianceEnvironment.Simulation, new CsrSubject(
                "babel-egs", "سلاسل بابل", "المبيعات", "SA",
                new Dictionary<string, string>
                {
                    ["1.3.6.1.4.1.311.20.2.3"] = "egs-01",
                    ["2.5.4.4"] = "serial",
                    ["2.5.4.5"] = "300000000000003",
                    ["2.5.4.12"] = "1100",
                    ["2.5.4.26"] = "الرياض"
                },
                "PREZATCA-Code-Signing")), TestContext.Current.CancellationToken);

        Assert.NotEmpty(csr.CsrDer.ToArray());
        Assert.Contains("BEGIN CERTIFICATE REQUEST", csr.CsrPem, StringComparison.Ordinal);

        // القالب على المعرّف 1.3.6.1.4.1.311.20.2 و SAN على 2.5.29.17 — كلاهما داخل البايتات.
        var hex = Convert.ToHexString(csr.CsrDer.ToArray());
        Assert.Contains("06092B0601040182371402", hex, StringComparison.OrdinalIgnoreCase); // 1.3.6.1.4.1.311.20.2
        Assert.Contains("0603551D11", hex, StringComparison.OrdinalIgnoreCase);               // 2.5.29.17 subjectAltName
    }

    /// <summary>
    /// <b>الفخّ الأول، مُنفَّذاً بالنوع لا بالتعليق.</b> الموقِّع يرفض ما لا يفهم شكله،
    /// والتصريح بالشكل جزء من العقد.
    /// </summary>
    [Fact]
    public async Task The_signer_refuses_an_input_whose_form_it_does_not_understand()
    {
        using var h = new Harness(KeyCustody.SelfHeld);
        var registration = await h.OnboardAsync(ct: TestContext.Current.CancellationToken);
        var custodian = h.Provider.LocalCustodian!;

        // شكل معلن غير موجود ⇒ رفض صريح، لا تخمين.
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await custodian.SignAsync(new SigningInput(
                Harness.Unit, registration.Credential, new byte[32],
                (SigningInputForm)99, "SHA-256", "ECDSA-secp256k1"), TestContext.Current.CancellationToken));

        // والشكلان المعلنان يُعطيان توقيعين مختلفين على المدخل نفسه — وهذا هو جوهر الفخّ:
        // تمرير بصمة إلى مسار «جزّئ ثم وقّع» ينتج تجزئة مزدوجة تتحقّق محلياً وتفشل عند الجهة.
        var digest = System.Security.Cryptography.SHA256.HashData("payload"u8.ToArray());
        var direct = await custodian.SignAsync(new SigningInput(
            Harness.Unit, registration.Credential, digest,
            SigningInputForm.PrecomputedDigestSignDirectly, "SHA-256", "ECDSA-secp256k1"),
            TestContext.Current.CancellationToken);
        var doubled = await custodian.SignAsync(new SigningInput(
            Harness.Unit, registration.Credential, digest,
            SigningInputForm.RawBytesToHashThenSign, "SHA-256", "ECDSA-secp256k1"),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(direct.Signature.ToArray(), doubled.Signature.ToArray());
    }

    /// <summary>رمز الأمان الثنائي: base64 لـbase64 لـDER — دورتا فكّ ترميز، لا واحدة.</summary>
    [Fact]
    public void The_binary_security_token_is_base64_of_base64_of_DER()
    {
        var der = new byte[] { 0x30, 0x82, 0x01, 0x0A };
        var material = new SignatureMaterial(new byte[] { 1, 2, 3 }, "ECDSA-secp256k1", der, DateTimeOffset.UtcNow);

        var once = Convert.FromBase64String(material.BinarySecurityTokenDoubleBase64);
        var inner = System.Text.Encoding.ASCII.GetString(once);
        var twice = Convert.FromBase64String(inner);

        Assert.Equal(der, twice);
        Assert.NotEqual(der, once);     // دورة واحدة لا تكفي — وهذا هو الفخّ الثاني بالضبط
    }
}
