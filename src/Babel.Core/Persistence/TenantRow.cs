namespace Babel.Core.Persistence;

/// <summary>
/// صف المستأجر. <c>internal</c>: أنواع الاستمرارية لا تعبر حدّ الوحدة أبداً —
/// ما يعبر هو واجهات معلنة وعقود، لا كيانات EF (القاعدة 5).
/// كل مسمّى يحمل <c>name_ar</c> و<c>name_en</c> (CONTRIBUTING §3 بند 5).
/// </summary>
internal sealed class TenantRow
{
    public Guid Id { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;
}
