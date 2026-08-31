using System.Globalization;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>
/// <b>حارس بيئة التشغيل — وهذا أخطر شيء في المكتبة كلها.</b>
///
/// مقيس على .NET 10.0.111 في هذا المستودع:
///
/// <code>
///   $ dotnet run
///     NFC(مفكّك) == مركّب      : True
///     IsNormalized(مفكّك)      : False
///
///   $ DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet run
///     NFC(مفكّك) == مركّب      : False      &lt;-- التطبيع صار «لا شيء» بصمت
///     IsNormalized(مفكّك)      : True       &lt;-- والحارس نفسه صار يكذب
///     AppContext switch "System.Globalization.Invariant" : False   &lt;-- ولا يعلن عن نفسه
/// </code>
///
/// أي أن متغيّر بيئة واحداً يُضبط عند النشر (وهو شائع جداً في صور Docker النحيفة
/// وفي Alpine) يجعل:
///   1. <c>String.Normalize(FormC)</c> عملية لا شيء، فتتغيّر بصمة كل نص عربي فيه أ/إ/آ؛
///   2. <c>String.IsNormalized(FormC)</c> يعيد <c>true</c> لنص غير مطبَّع، فيسقط التحقّق أيضاً؛
///   3. <c>AppContext.TryGetSwitch("System.Globalization.Invariant")</c> يعيد <c>false</c>،
///      فلا يمكن كشف الوضع بالسؤال المباشر.
///
/// ولذلك الكشف هنا <b>سلوكي</b>، لا استعلامي: نطبّع نصاً عربياً معروفاً ونقارن.
/// والنتيجة: انفجار عند تحميل المكتبة، لا سلسلة غير قابلة للتحقق بعد مليون قيد.
///
/// The single most dangerous deployment trap in this library: with
/// DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 the runtime silently turns NFC
/// normalisation into a no-op AND makes IsNormalized() lie, without setting the
/// AppContext switch that would let you ask. Detection must be behavioural.
/// </summary>
public static class CanonicalRuntime
{
    /// <summary>«أرباح» بالشكل المفكّك: U+0627 U+0654 ...</summary>
    private const string ProbeDecomposed = "أرباح";

    /// <summary>«أرباح» بالشكل المركّب: U+0623 ...</summary>
    private const string ProbeComposed = "أرباح";

    /// <summary>«ؤ» المفكّك U+0648 U+0654 -> U+0624 المركّب.</summary>
    private const string ProbeWawDecomposed = "ؤ";

    private const string ProbeWawComposed = "ؤ";

    private static readonly Lazy<RuntimeReport> Report = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// تقرير فحص بيئة التشغيل. لا يرمي؛ للتشخيص والعرض.
    /// </summary>
    public sealed record RuntimeReport(
        bool NfcComposesArabic,
        bool IsNormalizedDetectsDecomposed,
        bool InvariantSwitchClaimed,
        bool ArabicCultureAvailable,
        bool InvariantDecimalFormatStable,
        string FrameworkDescription)
    {
        public bool Ok => NfcComposesArabic && IsNormalizedDetectsDecomposed && InvariantDecimalFormatStable;
    }

    private static RuntimeReport Probe()
    {
        bool nfcOk;
        bool isNormalizedOk;
        try
        {
            nfcOk = ProbeDecomposed.Normalize(NormalizationForm.FormC) == ProbeComposed
                    && ProbeWawDecomposed.Normalize(NormalizationForm.FormC) == ProbeWawComposed;
            isNormalizedOk = !ProbeDecomposed.IsNormalized(NormalizationForm.FormC)
                             && ProbeComposed.IsNormalized(NormalizationForm.FormC);
        }
        catch (Exception)
        {
            nfcOk = false;
            isNormalizedOk = false;
        }

        var switchClaimed = AppContext.TryGetSwitch("System.Globalization.Invariant", out var inv) && inv;

        bool arabicCulture;
        try { arabicCulture = new CultureInfo("ar-SA").Name == "ar-SA"; }
        catch (CultureNotFoundException) { arabicCulture = false; }

        // لو كانت لغة الجهاز ar-SA فإن "0.0000" بلا ثقافة صريحة تنتج U+066B لا '.'
        // (مقيس). هنا نتأكّد أن الصيغة الثابتة ما تزال تنتج نقطة ASCII.
        var decimalOk = 100.5m.ToString("0.0000", CultureInfo.InvariantCulture) == "100.5000";

        return new RuntimeReport(
            nfcOk, isNormalizedOk, switchClaimed, arabicCulture, decimalOk,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
    }

    /// <summary>فحص بيئة التشغيل دون رمي — للتشخيص والتقارير.</summary>
    public static RuntimeReport SelfTest() => Report.Value;

    /// <summary>
    /// يُستدعى من المُنشئ الساكن لكل مسار يؤدي إلى دالة التجزئة.
    /// ينفجر عند التحميل إن كانت البيئة تكسر التطبيع.
    /// </summary>
    public static void EnsureSupported()
    {
        var r = Report.Value;
        if (r.Ok) return;

        var sb = new StringBuilder();
        sb.Append("بيئة التشغيل لا تصلح للتوحيد القياسي — لا يجوز حساب أي بصمة عليها. ");
        sb.Append("This runtime cannot produce canonical bytes. ");
        sb.Append(CultureInfo.InvariantCulture, $"framework={r.FrameworkDescription}; ");
        sb.Append(CultureInfo.InvariantCulture, $"nfc_composes_arabic={r.NfcComposesArabic}; ");
        sb.Append(CultureInfo.InvariantCulture, $"is_normalized_detects_decomposed={r.IsNormalizedDetectsDecomposed}; ");
        sb.Append(CultureInfo.InvariantCulture, $"invariant_switch_claimed={r.InvariantSwitchClaimed}; ");
        sb.Append(CultureInfo.InvariantCulture, $"arabic_culture_available={r.ArabicCultureAvailable}; ");
        sb.Append(CultureInfo.InvariantCulture, $"invariant_decimal_format_stable={r.InvariantDecimalFormatStable}. ");

        if (!r.NfcComposesArabic || !r.IsNormalizedDetectsDecomposed)
        {
            sb.Append("السبب شبه المؤكّد: وضع العولمة الثابتة (invariant globalization). ");
            sb.Append("أزِل DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 من البيئة، و<InvariantGlobalization>false</InvariantGlobalization> ");
            sb.Append("في كل مشروع في سلسلة النشر، وثبّت حزمة ICU في صورة الحاوية. ");
            sb.Append("Almost certainly invariant globalization: String.Normalize is a silent no-op there.");
            throw new CanonicalizationException(CanonErrors.RuntimeInvariantGlobalization, sb.ToString());
        }

        throw new CanonicalizationException(CanonErrors.RuntimeNormalizationBroken, sb.ToString());
    }
}
