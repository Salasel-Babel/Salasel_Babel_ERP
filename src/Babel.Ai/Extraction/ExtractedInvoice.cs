using Babel.SharedKernel;

namespace Babel.Ai.Extraction;

/// <summary>قيمة نصّية مستخرَجة ودرجة ثقتها. القيمة سجلٌّ كما وردت، بلا ترجمة (‏ADR-0021).</summary>
/// <param name="Value">النصّ كما ورد.</param>
/// <param name="Confidence">درجة الثقة بين صفر وواحد.</param>
public sealed record ExtractedText(string Value, decimal Confidence);

/// <summary>عدد مستخرَج ودرجة ثقته. <c>decimal</c> دائماً — ولا <c>double</c> في هذا المسار كلّه.</summary>
/// <param name="Value">القيمة.</param>
/// <param name="Confidence">درجة الثقة بين صفر وواحد.</param>
public sealed record ExtractedNumber(decimal Value, decimal Confidence);

/// <summary>تاريخ مستخرَج ميلادياً ودرجة ثقته.</summary>
/// <param name="Value">التاريخ الميلادي.</param>
/// <param name="Confidence">درجة الثقة بين صفر وواحد.</param>
public sealed record ExtractedDate(DateOnly Value, decimal Confidence);

/// <summary>عملة مستخرَجة ودرجة ثقتها.</summary>
/// <param name="Value">رمز العملة.</param>
/// <param name="Confidence">درجة الثقة بين صفر وواحد.</param>
public sealed record ExtractedCurrency(CurrencyCode Value, decimal Confidence);

/// <summary>سطر مستخرَج.</summary>
/// <param name="LineNo">ترتيب السطر كما ورد.</param>
/// <param name="Description">البيان.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="LineNet">صافي السطر كما قُرئ.</param>
public sealed record ExtractedLine(
    int LineNo,
    ExtractedText Description,
    ExtractedNumber Quantity,
    ExtractedNumber UnitPrice,
    ExtractedNumber LineNet);

/// <summary>اقتراح النموذج كما ورد — قبل أن يمرّ بحارس المفردات المغلقة.</summary>
/// <param name="EventCode">رمز الحدث المقترح.</param>
/// <param name="RoleCode">رمز الدور المقترح، أو فارغ.</param>
/// <param name="Confidence">درجة الثقة بين صفر وواحد.</param>
/// <param name="Rationale">تعليل النموذج كما كتبه.</param>
public sealed record ExtractedSuggestion(string EventCode, string RoleCode, decimal Confidence, string Rationale);

/// <summary>
/// مُخرَج الاستخراج بعد التحقق من المخطط — <b>وهو النوع الوحيد الذي يعبر إلى بقية الوحدة</b>.
/// نصّ JSON الخام لا يتجاوز <see cref="ExtractionSchema"/>.
/// </summary>
public sealed record ExtractedInvoice
{
    /// <summary>اسم البائع كما ورد.</summary>
    public required ExtractedText SellerName { get; init; }

    /// <summary>الرقم الضريبي للبائع.</summary>
    public required ExtractedText SellerVatNumber { get; init; }

    /// <summary>رقم الفاتورة لدى المورد.</summary>
    public required ExtractedText InvoiceNumber { get; init; }

    /// <summary>تاريخ الإصدار الميلادي.</summary>
    public required ExtractedDate IssuedOn { get; init; }

    /// <summary>العملة كما وردت، أو <c>null</c> إن لم تُطبع على المستند.</summary>
    public ExtractedCurrency? Currency { get; init; }

    /// <summary>الصافي قبل الضريبة.</summary>
    public required ExtractedNumber Net { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً، أو <c>null</c> إن لم تُطبع على المستند.</summary>
    public ExtractedNumber? TaxRate { get; init; }

    /// <summary>مبلغ الضريبة.</summary>
    public required ExtractedNumber TaxTotal { get; init; }

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public required ExtractedNumber GrossTotal { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<ExtractedLine> Lines { get; init; }

    /// <summary>الاقتراح إن وُجد.</summary>
    public ExtractedSuggestion? Suggestion { get; init; }
}
