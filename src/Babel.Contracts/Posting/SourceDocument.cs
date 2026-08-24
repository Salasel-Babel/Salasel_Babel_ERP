using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// المستند المصدر الذي أطلق الترحيل. يبقى المستند مملوكاً لوحدته؛
/// الدفتر يحتفظ بالإشارة فقط، ولا يقرأ جداول الوحدة (القاعدة 5).
/// </summary>
/// <param name="Module">الوحدة المالكة للمستند.</param>
/// <param name="DocumentType">نوع المستند داخل تلك الوحدة، مثل <c>SalesInvoice</c>.</param>
/// <param name="DocumentId">معرّف المستند داخل تلك الوحدة.</param>
public sealed record SourceDocument(BabelModule Module, string DocumentType, string DocumentId);
