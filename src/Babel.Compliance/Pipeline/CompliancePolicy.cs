using System.Globalization;
using Babel.Compliance.Abstractions;
using Babel.Contracts.Parameters;
using Babel.Core.Parameters;
using Babel.SharedKernel;
using TenantId = Babel.Compliance.Abstractions.TenantId;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// <b>سياسةُ الإبلاغ لمنشأةٍ في تاريخ</b> — نافذةُ الإبلاغ، وعتبةُ الإنذار، وسقفُ الحسم الآلي.
/// <para>
/// كانت الثلاثةُ خصائصَ في <see cref="ComplianceSettings"/> مسجَّلةً مفردةً للعملية كلّها
/// وموصوفةً «لكل مستأجر». وموضعُها الصحيح صفٌّ في خدمة المعامِلات: افتراضُ منصّةٍ
/// بمصدرٍ مكتوب، وتجاوزٌ لكلّ منشأة، ولقطةٌ تُقرأ بتاريخ (ADR-0090). وما بقي في
/// <c>ComplianceSettings</c> تقنيٌّ محض — مُهَلُ الاتصال والمحاولة — يُضبط من البيئة.
/// </para>
/// </summary>
/// <param name="ReportingWindow">نافذةُ الإبلاغ النظامية من الإصدار.</param>
/// <param name="QueueAgeAlarm">عتبةُ الإنذار: مستندٌ في الطابور أطول منها يُرفع بنداً.</param>
/// <param name="MaxResolutionAttempts">سقفُ محاولات الحسم الآلي قبل الطابور البشري.</param>
public sealed record CompliancePolicy(
    [property: Provisional("نافذة الإبلاغ النظامية ومتى تبدأ ومتى تنتهي",
        DerivedFrom = "افتراضُ المنصّة في data/parameters/platform-defaults.json (compliance.reporting · reporting_window_hours) منقولٌ من docs/analysis/04-zatca-integration.md §3 — وثيقةُ تخطيطٍ داخلية لا مصدرٌ رسمي",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "اللائحة السارية ومواصفة الإبلاغ المنشورة — ثم إيداعُ إصدارٍ مُعتمَد من لوحة التحكّم")]
    TimeSpan ReportingWindow,
    TimeSpan QueueAgeAlarm,
    int MaxResolutionAttempts)
{
    /// <summary>
    /// يُعيد بناء السياسة من لقطة المجموعة <c>compliance.reporting</c>. وغيابُ مفتاحٍ فيها
    /// خطأٌ برمجي لا افتراض: المجموعةُ تُودَع كاملةً أو تُرفض.
    /// </summary>
    /// <param name="snapshot">اللقطة السارية.</param>
    public static CompliancePolicy From(ParameterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CompliancePolicy(
            TimeSpan.FromHours((double)Require(snapshot, ParameterCatalogue.ComplianceReportingWindowHours)),
            TimeSpan.FromHours((double)Require(snapshot, ParameterCatalogue.ComplianceQueueAgeAlarmHours)),
            (int)Require(snapshot, ParameterCatalogue.ComplianceMaxResolutionAttempts));
    }

    private static decimal Require(ParameterSnapshot snapshot, string key)
        => snapshot.Find(key) ?? throw new InvalidOperationException(
            "لقطةُ معامِلات الإبلاغ بلا المفتاح «" + key + "» — المجموعة تُودَع كاملةً. / "
            + "The reporting parameter snapshot lacks the key '" + key + "'.");
}

/// <summary>مصدرُ سياسة الإبلاغ لمنشأة — والغيابُ رفضٌ مُسمّى لا افتراض.</summary>
public interface ICompliancePolicySource
{
    /// <summary>السياسةُ السارية للمنشأة الآن.</summary>
    /// <param name="tenant">المنشأة كما تعرفها وحدة الالتزام.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    ValueTask<Result<CompliancePolicy>> ResolveAsync(TenantId tenant, CancellationToken ct = default);
}

/// <summary>
/// التطبيقُ فوق خدمة المعامِلات. <b>وجسرُ الهويّة صريح:</b> وحدةُ الالتزام تعرف المنشأة نصّاً،
/// وخدمةُ المعامِلات تعرفها معرّفاً — فنصٌّ ليس معرّفَ منشأةٍ يُرفض باسمه لا يُملأ بافتراض.
/// </summary>
public sealed class ParameterCompliancePolicySource(IParameterSource parameters, TimeProvider clock) : ICompliancePolicySource
{
    /// <inheritdoc />
    public async ValueTask<Result<CompliancePolicy>> ResolveAsync(TenantId tenant, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(tenant.Value, "D", out Guid company))
        {
            return Result<CompliancePolicy>.Failure(new Error(
                "compliance.policy.tenant_not_a_company",
                "المنشأة «" + tenant.Value + "» ليست معرّفَ منشأةٍ (8-4-4-4-12)، فلا تُقرأ لها سياسةُ إبلاغ.",
                "The tenant '" + tenant.Value + "' is not a company identifier (8-4-4-4-12), so no reporting policy is read for it."));
        }

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        Result<ParameterSnapshot> snapshot = await parameters
            .ResolveAsync(new Babel.SharedKernel.TenantId(company), ParameterCatalogue.ComplianceReporting, today, ct)
            .ConfigureAwait(false);

        return snapshot.IsFailure
            ? Result<CompliancePolicy>.Failure(snapshot.Errors)
            : Result<CompliancePolicy>.Success(CompliancePolicy.From(snapshot.Value));
    }
}

/// <summary>يصوغ الرفض نصّاً واحداً للاستثناءات التي تلقيها وحدةُ الالتزام حين لا يوجد <c>Result</c> في التوقيع.</summary>
internal static class CompliancePolicyRefusal
{
    public static InvalidOperationException Of(IReadOnlyList<Error> errors)
        => new(string.Create(CultureInfo.InvariantCulture,
            $"لا سياسةَ إبلاغٍ لهذه المنشأة: {string.Join(" · ", errors.Select(static e => e.Code + " — " + e.MessageAr))} / "
            + $"no reporting policy for this tenant: {string.Join(" · ", errors.Select(static e => e.Code + " — " + e.MessageEn))}"));
}
