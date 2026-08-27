using System.Globalization;
using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>الشاهد الموجب — خدمةُ تطبيقٍ تخالف القاعدة عمداً، وتعيش في تجميعة الاختبار.</b>
/// <para>
/// وهي تحمل رمز حدث <b>تفتحه قدرة</b> نصّاً، ولا تبلغ أي بوابة قبول. فلو كفّ الماسح عن
/// القراءة — رمز عملية جديد، أو آلة حالة لم تُتبَّع، أو نطاق فحصٍ ضاق — لمرّ «لا مخالفات»
/// وهو لا يفحص شيئاً. ووجود هذا النوع هو ما يجعل الفراغ <b>غير قابل للالتباس بالنجاح</b>.
/// </para>
/// <para>
/// ولا خطر منه على الإنتاج: هو في تجميعة اختبارات المعمارية، ولا يُشير إليه مشروع منتج،
/// ولا يبلغ محرك ترحيل — يعيد رمزاً نصّاً ولا شيء غيره.
/// </para>
/// </summary>
public sealed class UnguardedGatedEventControl : IApplicationService
{
    /// <summary>لاحقة لا معنى لها إلا أن الدالّتين أدناه <b>تُنشئان مثيلاً</b>.</summary>
    /// <remarks>
    /// والماسح يقرأ نقاط الدخول <b>العامة على مثيل</b> — وهو الشكل الذي يأخذه كل كاتب في
    /// الوحدات. فلو صارتا ساكنتين لخرجتا من نطاق الفحص، ولمرّ الشاهد وهو لا يشهد.
    /// </remarks>
    private readonly string _suffix = string.Empty;

    /// <summary>رمز حدث تفتحه قدرة في وحدة المشتريات — مكتوباً نصّاً كما يكتبه الإنتاج.</summary>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public string PostsAGatedEventWithoutAdmission() => "purchasing.invoice.stock.posted" + _suffix;

    /// <summary>ورمز حدث تفتحه قدرة في وحدة المبيعات — كي يعضّ الشاهد على الوحدتين معاً.</summary>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public string PostsASalesGatedEventWithoutAdmission() => "sales.invoice.cost_of_sales" + _suffix;
}

/// <summary>
/// <b>لا باب غير محروس — في المبيعات <i>وفي المشتريات</i>.</b>
/// <para>
/// ‏ADR-0023 يبني ملفّ قدرات مغلقاً مُطابَقاً بالمصفوفة، و‏ADR-0025 يجعل القبول
/// <b>مطلوباً في توقيع الكاتب</b> لا في انضباط المستدعي. وهذا الحارس هو ما يمنع الباب
/// الثاني: كاتبٌ ثانٍ للحدث نفسه يُضاف بعد شهر ولا يمرّ بالقبول.
/// </para>
/// <para>
/// <b>ولماذا انتقل إلى هنا:</b> كان يعيش في <c>Babel.Sales.Tests</c> ويقرأ تجميعة
/// المبيعات وحدها — فكانت الجملة «لا باب غير محروس» جملةً عن وحدة واحدة. وحين رُبطت
/// البوابة في المشتريات لم يكن هناك ما يمتدّ إليها. ومشروع اختبارات المعمارية يشير إلى
/// الوحدتين معاً، فهو الموضع الذي يستطيع أن يسأل السؤال نفسه عن كلتيهما.
/// </para>
/// </summary>
public sealed class CapabilityAdmissionIsReachedFromEveryGatedEntryPoint(ITestOutputHelper output)
{
    private static AdmissionScan Sales => AdmissionScan.Run(
        BabelAssemblies.Named("Babel.Sales"),
        BabelModule.Sales,
        AdmissionScan.AdmissionOf(
            BabelAssemblies.Named("Babel.Sales"), "Babel.Sales.Application.SalesAdmission", "AdmitInvoiceAsync"));

    private static AdmissionScan Purchasing => AdmissionScan.Run(
        BabelAssemblies.Named("Babel.Purchasing"),
        BabelModule.Purchasing,
        AdmissionScan.AdmissionOf(
            BabelAssemblies.Named("Babel.Purchasing"), "Babel.Purchasing.Application.PurchasingAdmission", "AdmitBillAsync"));

    [Fact]
    public void NoPublicEntryPointInSalesReachesACapabilityGatedEventWithoutReachingAdmission()
        => Check(Sales, "Babel.Sales");

    [Fact]
    public void NoPublicEntryPointInPurchasingReachesACapabilityGatedEventWithoutReachingAdmission()
        => Check(Purchasing, "Babel.Purchasing");

    /// <summary>
    /// <b>الحارس يعضّ.</b> الماسح نفسه يُشغَّل على تجميعة الاختبار — وفيها
    /// <see cref="UnguardedGatedEventControl"/> يخالف عمداً — فيجب أن <b>يُبلّغ</b> عن
    /// المخالفة ويُسمّيها.
    /// <para>
    /// ولولا هذا البند لكان «صفر مخالفات» جملةً لا تُميَّز عن «لم يُفحص شيء»: ونطاقٌ
    /// يستحيل بنيوياً أن يحوي مخالفة يُنتج المجموعة الفارغة نفسها بالضبط.
    /// </para>
    /// </summary>
    [Fact]
    public void TheScannerIsNotVacuousBecauseItCatchesADeliberateViolationInThisVeryAssembly()
    {
        Assembly control = typeof(UnguardedGatedEventControl).Assembly;

        foreach ((BabelModule module, string gate, string method) in new[]
                 {
                     (BabelModule.Purchasing, "Babel.Purchasing.Application.PurchasingAdmission", "AdmitBillAsync"),
                     (BabelModule.Sales, "Babel.Sales.Application.SalesAdmission", "AdmitInvoiceAsync"),
                 })
        {
            AdmissionScan scan = AdmissionScan.Run(
                control,
                module,
                AdmissionScan.AdmissionOf(BabelAssemblies.Named(ModuleMap.ProjectOf(module)), gate, method));

            output.WriteLine(module + " · مخالفات الشاهد: " + string.Join(" | ", scan.Violations));

            Xunit.Assert.True(
                scan.GatedEvents.Count > 0,
                $"لا أحداث محروسة لـ{module} — الشاهد يمرّ فراغاً.");

            Xunit.Assert.Contains(
                scan.Violations,
                violation => violation.Contains(nameof(UnguardedGatedEventControl), StringComparison.Ordinal));
        }
    }

    private void Check(AdmissionScan scan, string assembly)
    {
        output.WriteLine(assembly + " · الأحداث المحروسة من الكتالوج: " + string.Join(" · ", scan.GatedEvents));
        output.WriteLine(assembly + " · الدوالّ الحاملة لرمز محروس: " + string.Join(" · ", scan.Bearers));
        output.WriteLine(assembly + " · نقاط الدخول المحروسة فعلاً: " + string.Join(" · ", scan.Guarded));
        output.WriteLine(assembly + " · نقاط الدخول المفحوصة: " + scan.EntryPointCount.ToString(CultureInfo.InvariantCulture));
        output.WriteLine(assembly + " · المخالفات: " + (scan.Violations.Count == 0 ? "لا شيء" : string.Join(" | ", scan.Violations)));

        Xunit.Assert.True(
            scan.GatedEvents.Count > 0,
            $"{assembly}: لا حدث تفتحه قدرة في الكتالوج — الفحص يمرّ فراغاً.");

        Xunit.Assert.True(
            scan.Bearers.Count > 0,
            $"{assembly}: لا دالّة تحمل رمز حدث محروس — الوحدة لا تُرحّل أي حدث تفتحه قدرة، "
            + "أو الماسح كفّ عن قراءة النصوص الحرفية.");

        Xunit.Assert.True(scan.EntryPointCount > 0, $"{assembly}: لا نقاط دخول.");

        Xunit.Assert.True(
            scan.Guarded.Count > 0,
            $"{assembly}: لا نقطة دخول محروسة — البوابة مبنيّة ولا يستدعيها شيء.");

        Xunit.Assert.True(
            scan.Violations.Count == 0,
            assembly + ": نقاط دخول تبلغ حدثاً تفتحه قدرة ولا تبلغ القبول:\n"
            + string.Join('\n', scan.Violations)
            + "\n        الأحداث المحروسة: " + string.Join(" · ", scan.GatedEvents)
            + "\n        الحاملات: " + string.Join(" · ", scan.Bearers)
            + "\n        المحروسة: " + string.Join(" · ", scan.Guarded)
            + "\n        نقاط الدخول: " + scan.EntryPointCount.ToString(CultureInfo.InvariantCulture));
    }
}
