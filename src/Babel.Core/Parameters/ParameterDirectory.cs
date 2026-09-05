using Babel.Contracts.Parameters;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// <b>دليل المعامِلات — منفذا القراءة والتسجيل، بلا استحقاقٍ خاصّ بهما.</b>
/// <para>
/// <b>ولماذا ليس خدمةَ تطبيق:</b> هذا النوع يُنادى <b>داخل</b> عمليةٍ فحصَت استحقاقها
/// بالفعل — تلتقط وحدةُ الالتقاط فاتورةً بعد أن أذن لها الاستحقاق، فتسأل عن النسبة
/// السارية؛ وترحّل وحدةُ المشتريات فاتورةً بعد أن أذن لها الاستحقاق، فتسجّل ما استعملت.
/// وفحصٌ ثانٍ هنا يعني أن العملية تسقط في منتصفها برفضٍ يخصّ وحدةً أخرى.
/// </para>
/// <para>
/// وهو شكل <c>ICostCenterResolver</c> نفسه حرفاً: يُنادى من كل بوّابة ترحيل قبل أن
/// تبني طلباً، وليس خدمةَ تطبيق، وللسبب نفسه (ADR-0026).
/// </para>
/// </summary>
public sealed class ParameterDirectory : IParameterSource, IParameterUsageRecorder
{
    private readonly IParameterStore _store;

    /// <summary>ينشئ الدليل.</summary>
    /// <param name="store">المخزن.</param>
    public ParameterDirectory(IParameterStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<Result<ParameterSnapshot>> ResolveAsync(
        TenantId tenant, string setCode, DateOnly on, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setCode);

        if (ParameterCatalogue.Find(setCode) is null)
        {
            return Result<ParameterSnapshot>.Failure(ParameterErrors.SetUnknown(setCode));
        }

        ParameterVersionView? version = await _store
            .FindEffectiveAsync(tenant, setCode, on, cancellationToken)
            .ConfigureAwait(false);

        // ‏**ولا قيمة تُخترع عند الغياب.** الرفض المُسمّى هو ما يجعل غيابَ القرار مرئياً؛
        // وصفرٌ صامت أو رقمٌ «معقول» يجعل النظام يعمل بينما لا أحد يعرف بأي رقم يعمل.
        return version is null
            ? Result<ParameterSnapshot>.Failure(ParameterErrors.SetMissing(setCode, on))
            : Result<ParameterSnapshot>.Success(SnapshotOf(version));
    }

    /// <inheritdoc />
    public async ValueTask<Result> RecordAsync(
        TenantId tenant, ParameterUsage usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        await _store.RecordUsageAsync(tenant, usage, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>يبني اللقطة التي تُخزَّن على المستند من إصدارٍ قُرئ.</summary>
    /// <param name="version">الإصدار.</param>
    public static ParameterSnapshot SnapshotOf(ParameterVersionView version)
    {
        ArgumentNullException.ThrowIfNull(version);

        Dictionary<string, decimal> values = new(StringComparer.Ordinal);

        foreach (ParameterValueView value in version.Values)
        {
            values[value.Key] = value.Value;
        }

        return new ParameterSnapshot
        {
            VersionId = version.Id,
            SetCode = version.SetCode,
            Scope = version.Scope,
            EffectiveFrom = version.EffectiveFrom,
            Approval = version.Approval,
            SourceRef = version.SourceRef,
            Values = values,
        };
    }
}
