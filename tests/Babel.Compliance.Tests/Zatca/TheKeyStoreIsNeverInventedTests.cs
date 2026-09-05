using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Signing;
using Babel.Compliance.Zatca.Transport;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// <b>مخزن المفاتيح لا يُخترع، والعابر منه لا يبلغ الإنتاج.</b>
/// <para>
/// <b>العطل الذي أُغلق، بنصّ ما كان مكتوباً:</b>
/// <c>_ownedKeys = keys is null ? new EphemeralZatcaKeyStore() : null;</c> — مُعامِلٌ
/// اختياري، وغيابُه يُركّب <b>صمتاً</b> مخزناً في الذاكرة يُولَّد عند الإقلاع ويموت مع
/// العملية. وأثرُ ذلك على مزوّدٍ إنتاجي ليس تعطُّلاً: الفواتير النظامية تُختم وتُوقَّع
/// و<b>تتحقّق محلياً بنجاح تام</b>، ثمّ تسقط عند الجهة بعد أول إعادة تشغيل — والفشل
/// يُقرأ «الاعتماد منتهٍ» لا «لا مفتاح»، فيُرسل من يبحث إلى المكان الخطأ.
/// </para>
/// <para>
/// <b>والوضعُ المشروع بقي — لكنه صار باسمه:</b> المحاكاة والاختبار تحتاجان مخزناً
/// عابراً فعلاً، وهو نوعٌ اسمُه يقول ما هو (<see cref="EphemeralZatcaKeyStore"/>) ولا
/// يُبلَغ إلا بذكره. والذي مُنع هو أن يُذكر في إعدادٍ بيئتُه <c>Production</c>.
/// </para>
/// </summary>
public sealed class TheKeyStoreIsNeverInventedTests
{
    private static ZatcaSettings Settings(ComplianceEnvironment environment) => new(
        new Uri("https://gw-fatoora.example.invalid/e-invoicing/simulation/"),
        environment,
        ZatcaFixtures.Seller,
        ClearanceTimeout: TimeSpan.FromSeconds(30),
        ReportingTimeout: TimeSpan.FromSeconds(30));

    private static ZatcaComplianceProvider Build(
        ComplianceEnvironment environment, IZatcaKeyStore keys, ManualClock clock)
        => new(
            Settings(environment),
            new FakeZatcaWire(clock),
            new DictionarySecretResolver { ["vault://zatca/secret"] = "test-secret-not-a-credential" },
            credential => new ZatcaCredential(credential, new SecretRef("vault://zatca/secret")),
            clock,
            keys);

    /// <summary>
    /// تركيبٌ بلا مخزن مفاتيح <b>يُرفض</b>، والرسالة تسمّي الواجهة المطلوبة والنوع
    /// العابر معاً — فمن يقرأها يعرف ماذا يمرّر وأين.
    /// </summary>
    [Fact]
    public void Composing_the_provider_without_a_key_store_is_refused_by_name()
    {
        ManualClock clock = new(ZatcaFixtures.IssuedAt);

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => Build(ComplianceEnvironment.Simulation, keys: null!, clock));

        Assert.StartsWith(ZatcaComplianceProvider.NoKeyStoreCode, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("IZatcaKeyStore", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EphemeralZatcaKeyStore), refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// والمخزن العابر <b>في بيئة الإنتاج يُرفض</b> — ولو مُرّر بالاسم. الوضع مشروع،
    /// والبيئة هي التي تحسم.
    /// </summary>
    [Fact]
    public void An_ephemeral_key_store_is_refused_when_the_settings_declare_production()
    {
        ManualClock clock = new(ZatcaFixtures.IssuedAt);
        using EphemeralZatcaKeyStore keys = new();

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => Build(ComplianceEnvironment.Production, keys, clock));

        Assert.StartsWith(
            ZatcaComplianceProvider.EphemeralInProductionCode, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>الشاهد الموجب (ADR-0056):</b> الوضع المشروع يمرّ فعلاً — عابرٌ باسمه في
    /// المحاكاة يُركّب المزوّد ويُسلِّم المخزن نفسه الذي مُرّر، لا نسخةً اخترعها أحد.
    /// وبلا هذا الشاهد لا يُفرَّق حارسٌ يرفض كلَّ شيء عن حارسٍ يرفض ما يجب.
    /// </summary>
    [Fact]
    public void An_ephemeral_key_store_is_accepted_in_simulation_and_the_provider_holds_the_one_passed()
    {
        ManualClock clock = new(ZatcaFixtures.IssuedAt);
        using EphemeralZatcaKeyStore keys = new();

        ZatcaComplianceProvider provider = Build(ComplianceEnvironment.Simulation, keys, clock);

        Assert.Same(keys, provider.Keys);
    }

    /// <summary>
    /// و<b>لا سبيل إلى تركيبٍ بلا مخزن أصلاً</b>: المُعامِل صار إلزامياً في التوقيع،
    /// فالعطل يُمنع عند الترجمة لا عند التشغيل. وهذا الفحص يقرأ التوقيع بالانعكاس كي
    /// لا يعود «اختيارياً» في إيداعٍ لاحق بلا أن يقول أحد شيئاً.
    /// </summary>
    [Fact]
    public void The_key_store_parameter_carries_no_default_value()
    {
        System.Reflection.ParameterInfo keys = typeof(ZatcaComplianceProvider)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(parameter => string.Equals(parameter.Name, "keys", StringComparison.Ordinal));

        Assert.False(keys.IsOptional, "مُعامِل مخزن المفاتيح صار اختيارياً — وهذا هو الارتداد الصامت بعينه.");
        Assert.Equal(typeof(IZatcaKeyStore), keys.ParameterType);
    }
}
