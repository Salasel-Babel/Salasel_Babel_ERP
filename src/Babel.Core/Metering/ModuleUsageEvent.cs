using Babel.SharedKernel;

namespace Babel.Core.Metering;

/// <summary>
/// استخدام على محور الوحدة — أحد محورَي التسعير.
/// </summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="Module">الوحدة المستخدَمة.</param>
/// <param name="Operation">اسم العملية، مثل <c>Sales.Invoice.Issue</c>.</param>
/// <param name="Actor">من نفّذ العملية.</param>
/// <param name="OccurredAt">لحظة الوقوع بتوقيت UTC.</param>
/// <param name="Quantity">الكمية المقيسة. واحد للاستدعاء الواحد، وأكثر للعمليات الجماعية.</param>
public sealed record ModuleUsageEvent(
    TenantId Tenant,
    BabelModule Module,
    string Operation,
    UserId Actor,
    DateTimeOffset OccurredAt,
    long Quantity);
