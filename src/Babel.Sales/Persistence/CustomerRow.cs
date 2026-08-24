namespace Babel.Sales.Persistence;

/// <summary>صف العميل. <c>internal</c> — لا يعبر حدّ وحدة المبيعات.</summary>
internal sealed class CustomerRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>حد الائتمان. <c>decimal</c> لا <c>double</c> — مفروض ببناء في Rule04.</summary>
    public decimal CreditLimit { get; set; }
}
