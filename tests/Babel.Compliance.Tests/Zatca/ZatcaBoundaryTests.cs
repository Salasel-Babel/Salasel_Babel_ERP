using System.Globalization;
using System.Reflection;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Onboarding;
using Babel.Compliance.Zatca.Signing;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// حدود المزوّد: ما لا يعرفه، وما لا يحوزه، وما لا يدّعيه.
/// </summary>
public sealed class ZatcaBoundaryTests(ITestOutputHelper output)
{
    private static Assembly Provider => typeof(ZatcaComplianceProvider).Assembly;

    // ── حدّ الدفتر ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>‏ADR-0011 وADR-0012 منفَّذان لا موصوفان:</b> المزوّد لا يعرف الدفتر أصلاً،
    /// فلا يستطيع أن يكتب فيه. وهذا أقوى من قاعدة مراجعة، لأن الوحدة التي لا تُحمَّل
    /// لا تُستدعى.
    /// </summary>
    [Fact]
    public void The_provider_assembly_has_no_reference_to_the_ledger_or_to_any_accounting_module()
    {
        string[] forbidden =
        [
            "Babel.Ledger", "Babel.Sales", "Babel.Purchasing", "Babel.Core",
            "Babel.Contracts", "Babel.Compliance", "Babel.Api"
        ];

        List<string> referenced = [.. Provider.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith("Babel.", StringComparison.Ordinal))];

        output.WriteLine("مراجع بابل في المزوّد: " + string.Join("، ", referenced));

        // حارس اللافراغ: مرجع واحد على الأقل يجب أن يوجد، وإلا كان الفحص فارغاً.
        Assert.NotEmpty(referenced);

        List<string> violations = [.. referenced
            .Where(name => forbidden.Contains(name, StringComparer.Ordinal))];

        Assert.True(violations.Count == 0,
            "المزوّد يعرف وحدة محاسبية: " + string.Join("، ", violations));

        // والمرجع الوحيد المسموح هو العقد.
        Assert.Equal(["Babel.Compliance.Abstractions"], referenced);
    }

    /// <summary>
    /// ولا نوع في المزوّد يحمل اسماً يوحي بكتابة محاسبية. حارس نصّي فوق حارس المراجع:
    /// المرجع قد يُضاف يوماً، والاسم يُقرأ في المراجعة.
    /// </summary>
    [Fact]
    public void No_type_in_the_provider_is_named_as_if_it_posted_to_the_ledger()
    {
        string[] suspicious = ["Posting", "JournalWriter", "LedgerWriter", "Voucher", "Debit", "Credit"];

        List<string> hits = [.. Provider.GetTypes()
            .Select(t => t.FullName ?? t.Name)
            .Where(name => suspicious.Any(s => name.Contains(s, StringComparison.Ordinal)))];

        output.WriteLine("عدد أنواع المزوّد: " + Provider.GetTypes().Length.ToString(CultureInfo.InvariantCulture));
        Assert.True(Provider.GetTypes().Length >= 20, "عدد الأنواع أقل من المتوقَّع — هل يفحص هذا شيئاً؟");
        Assert.True(hits.Count == 0, "أنواع بأسماء توحي بالترحيل: " + string.Join("، ", hits));
    }

    // ── حيازة المفتاح ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا مفتاح خاص يعبر أي حدّ في هذا المزوّد.</b> الفحص بالانعكاس لا بالمراجعة:
    /// دالة واحدة تُعيد <c>ECDsa</c> أو تُصدّر مفتاحاً خاصاً تُسقط هذا الاختبار.
    /// </summary>
    [Fact]
    public void No_public_member_in_the_key_boundary_returns_or_exports_a_private_key()
    {
        Type[] custody = [typeof(IZatcaKeyStore), typeof(ZatcaKeyCustodian), typeof(EphemeralZatcaKeyStore)];

        List<string> leaks = [];
        int inspected = 0;

        foreach (Type type in custody)
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                inspected++;

                Type? returned = member switch
                {
                    MethodInfo method => method.ReturnType,
                    PropertyInfo property => property.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => null
                };

                if (returned is null)
                {
                    continue;
                }

                if (typeof(System.Security.Cryptography.AsymmetricAlgorithm).IsAssignableFrom(returned)
                    || returned == typeof(System.Security.Cryptography.ECParameters))
                {
                    leaks.Add($"{type.Name}.{member.Name} ⇒ {returned.Name}");
                }

                if (member.Name.Contains("ExportPrivate", StringComparison.Ordinal)
                    || member.Name.Contains("PrivateKey", StringComparison.Ordinal))
                {
                    leaks.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        output.WriteLine("أعضاء مفحوصة: " + inspected.ToString(CultureInfo.InvariantCulture));

        // حارس اللافراغ: مسحٌ لا يقرأ شيئاً يمرّ دائماً.
        Assert.True(inspected >= 20, "المسح ضامر — لا يفحص شيئاً");
        Assert.True(leaks.Count == 0, "مادة مفتاح خاص تعبر الحدّ: " + string.Join("، ", leaks));
    }

    /// <summary>
    /// النطاق <b>وحدة إصدار</b> لا مستأجر: مقبضان لوحدتين مختلفتين لا يتقاطعان،
    /// ومفتاح إحداهما لا يوقّع عن الأخرى.
    /// </summary>
    [Fact]
    public void Each_issuing_unit_gets_its_own_key_and_its_own_handle()
    {
        using EphemeralZatcaKeyStore keys = new();

        CredentialRef first = keys.Create(ZatcaFixtures.Tenant, new IssuingUnitId("POS-01"), ComplianceEnvironment.Simulation);
        CredentialRef second = keys.Create(ZatcaFixtures.Tenant, new IssuingUnitId("POS-02"), ComplianceEnvironment.Simulation);

        output.WriteLine("مقبض الوحدة الأولى: " + first.Value);
        output.WriteLine("مقبض الوحدة الثانية: " + second.Value);

        Assert.NotEqual(first.Value, second.Value);
        Assert.Contains("POS-01", first.Value, StringComparison.Ordinal);
        Assert.Contains("POS-02", second.Value, StringComparison.Ordinal);

        // والمفتاحان مختلفان فعلاً: المفتاح العام يفصلهما.
        Assert.NotEqual(
            Convert.ToHexString(keys.ExportPublicKeyDer(first)),
            Convert.ToHexString(keys.ExportPublicKeyDer(second)));

        // والبيئة داخل المقبض: شهادة محاكاة لا تُستعمل في إنتاج بالخطأ.
        CredentialRef production = keys.Create(ZatcaFixtures.Tenant, new IssuingUnitId("POS-01"), ComplianceEnvironment.Production);
        Assert.NotEqual(first.Value, production.Value);
        Assert.Contains("Production", production.Value, StringComparison.Ordinal);
    }

    // ── طلب توقيع الشهادة ───────────────────────────────────────────────────

    [Fact]
    public void The_signing_request_carries_the_template_extension_and_the_five_RDNs_in_the_declared_order()
    {
        using EphemeralZatcaKeyStore keys = new();
        CredentialRef credential = keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);

        ZatcaUnitRegistration registration = new(
            CommonName: "acme-POS-01",
            OrganisationNameAr: "شركة سلاسل بابل للمقاولات",
            OrganisationalUnitName: "المبيعات",
            VatRegistrationNumber: "300000000000003",
            DeviceSerialNumber: ZatcaUnitRegistration.ComposeSerial("Babel", "POS", "0001"),
            RegisteredAddress: "الرياض، حي العليا",
            BusinessCategory: "مقاولات");

        CsrSubject subject = ZatcaCertificateRequest.SubjectFor(registration, ComplianceEnvironment.Simulation);
        byte[] csr = keys.BuildSigningRequest(credential, subject);

        output.WriteLine("طول الطلب بالبايت: " + csr.Length.ToString(CultureInfo.InvariantCulture));
        Assert.NotEmpty(csr);

        string der = Convert.ToHexString(csr);

        // معرّف امتداد القالب داخل الترميز: 1.3.6.1.4.1.311.20.2 ⇒ 2B 06 01 04 01 82 37 14 02
        Assert.Contains("2B060104018237140" + "2", der, StringComparison.OrdinalIgnoreCase);

        // والاسم البديل موجود: 2.5.29.17 ⇒ 55 1D 11
        Assert.Contains("551D11", der, StringComparison.OrdinalIgnoreCase);

        output.WriteLine("ترتيب المعرّفات المفروض: " + string.Join(" ← ", ZatcaCertificateRequest.RdnOrder));
        Assert.Equal(["SN", "UID", "title", "registeredAddress", "businessCategory"], ZatcaCertificateRequest.RdnOrder);
    }

    /// <summary>
    /// معرّف RDN خارج القائمة المعلنة <b>يُرفض</b> ولا يُلحق في موضع عشوائي:
    /// ترتيب مكوّنات الاسم المميّز جزء من ترميزه، وترتيبان مختلفان يعطيان طلبين مختلفين.
    /// </summary>
    [Fact]
    public void An_unknown_RDN_identifier_is_refused_rather_than_appended_somewhere()
    {
        using EphemeralZatcaKeyStore keys = new();
        CredentialRef credential = keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);

        CsrSubject subject = new(
            "cn", "org", "ou", "SA",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["SN"] = "x", ["surprise"] = "y" },
            "PREZATCA-Code-Signing");

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => keys.BuildSigningRequest(credential, subject));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("surprise", error.Message, StringComparison.Ordinal);
    }

    // ── ما يُصرَّح به ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>القدرات تصف ما نستطيع إثباته، لا ما نأمله.</b> ادّعاء كشف تكرار من جانب الجهة
    /// بلا وثيقة هو بالضبط ما يُنتج فاتورة مُصفَّاة مرتين يوم يُصدَّق الادّعاء.
    /// </summary>
    [Fact]
    public async Task The_declared_capabilities_claim_nothing_that_cannot_be_shown()
    {
        using ZatcaHarness harness = new();
        await harness.OnboardAsync(TestContext.Current.CancellationToken);

        ProviderCapabilities capabilities = harness.Provider.Capabilities;

        output.WriteLine("المزوّد: " + capabilities.DisplayNameAr);
        output.WriteLine("الحيازة: " + capabilities.Custody);
        output.WriteLine("استعلام الحالة: " + capabilities.StatusQuery);
        output.WriteLine("كشف تكرار لدى الجهة: " + capabilities.DeduplicatesBySubmissionFingerprint);
        output.WriteLine("مطابقة بايتية بنيوية: " + capabilities.ByteStableRetriesAreStructural);
        output.WriteLine("يمكن حسم الغموض آلياً: " + capabilities.AmbiguityCanBeResolvedAutomatically);

        Assert.Equal(KeyCustody.SelfHeld, capabilities.Custody);
        Assert.Equal(StatusProbeSupport.NotSupported, capabilities.StatusQuery);
        Assert.False(capabilities.DeduplicatesBySubmissionFingerprint);
        Assert.True(capabilities.ByteStableRetriesAreStructural);

        // والنتيجة المنطقية لما سبق: لا حسم آلي للغموض. كل مهلة غامضة تنتهي إلى إنسان.
        Assert.False(capabilities.AmbiguityCanBeResolvedAutomatically);
        Assert.Null(harness.Provider.StatusQuery);
    }

    /// <summary>
    /// طابور التحقق قبل البناء يشمل المزوّد الحقيقي، وكل بند فيه يقول <b>كيف يُغلق</b>.
    /// </summary>
    [Fact]
    public void Every_unverified_detail_in_the_provider_is_registered_with_a_way_to_close_it()
    {
        IReadOnlyList<ProvisionalItem> items = ProvisionalRegistry.Collect(Provider);

        output.WriteLine(ProvisionalRegistry.Render(items));

        Assert.NotEmpty(items);

        List<ProvisionalItem> orphans = [.. items.Where(i => string.IsNullOrWhiteSpace(i.VerifyBy))];
        Assert.True(orphans.Count == 0,
            "بنود مؤقَّتة بلا طريقة تحقق:\n" + string.Join("\n", orphans.Select(i => i.Location)));

        int structural = items.Count(i => i.Risk == ProvisionalRisk.Structural);
        output.WriteLine(FormattableString.Invariant($"\nبنود المزوّد: {items.Count} — منها بنيوية: {structural}"));

        Assert.True(structural >= 5, "البنود البنيوية أقل من المتوقَّع — هل فُقدت سمات؟");
    }
}
