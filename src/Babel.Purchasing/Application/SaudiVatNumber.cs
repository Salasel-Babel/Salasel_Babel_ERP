using System.Buffers;
using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>
/// <b>شكل رقم التسجيل الضريبي السعودي عند حدّ الإدخال.</b>
/// <para>
/// الرقم يصل من رمز الفاتورة <b>مُصدَّقاً</b> — كتبه المُصدِر لا قارئ ضوئي — ثم يُطابَق
/// به المورد. ومطابقةٌ على معرّف لا يُتحقَّق من شكله أسوأ من غياب المعرّف: تُنتج
/// <b>مظهر التحقّق</b> بلا تحقّق.
/// </para>
/// <para>
/// <b>ولا تحويل صامت لأي رقم:</b> الأرقام العربية-الهندية <c>U+0660</c>–<c>U+0669</c>
/// والشرقية <c>U+06F0</c>–<c>U+06F9</c> والديفاناغارية <c>U+0966</c>–<c>U+096F</c> وكل
/// رقم يونيكودي آخر <b>تُرفض</b> ولا تُحوَّل. وهذا هو السلوك المعتمد في هذا المستودع
/// لكل حقل يُطابَق أو يُجزَّأ (‏<c>ComplianceText</c>، وفخ الأرقام العربية-الهندية).
/// والسبب هنا خاصّ: الطرف الآخر من المطابقة رقمٌ <b>مُصدَّق</b> بمحارف لاتينية، فتحويلٌ
/// صامت يجعل ما خُزِّن غير ما كتبه الإنسان وغير ما في الرمز معاً.
/// </para>
/// <para>
/// <b>ولا يُستعمل <c>char.IsDigit</c> ولا <c>int.Parse</c> هنا إطلاقاً:</b> كلاهما يقبل
/// الأرقام العربية-الهندية والديفاناغارية بلا شكوى — وهو بالضبط باب الدخول الصامت الذي
/// يُغلقه هذا الملف. الفحص بـ<c>char.IsAsciiDigit</c> وحده.
/// </para>
/// </summary>
internal static class SaudiVatNumber
{
    /// <summary>طول رقم التسجيل الضريبي: خمس عشرة خانة.</summary>
    public const int Length = 15;

    /// <summary>الخانة الأولى — رمز دولة مجلس التعاون للسعودية.</summary>
    public const char CountryDigit = '3';

    /// <summary>الخانة الأخيرة — رمز نوع الضريبة (القيمة المضافة).</summary>
    public const char TaxTypeDigit = '3';

    /// <summary>القيمة التي تعني «لم يُسجَّل رقم» — نصّ فارغ لا قيمة معدومة.</summary>
    public const string Unrecorded = "";

    /// <summary>محارف تحكّم اتجاهي وعرض صفر — غير مرئية، وتُفسد المطابقة بلا أن تُرى.</summary>
    private static readonly SearchValues<char> InvisibleControls = SearchValues.Create(
    [
        '‎', '‏', '؜',
        '‪', '‫', '‬', '‭', '‮',
        '⁦', '⁧', '⁨', '⁩',
        '​', '‌', '‍', '﻿',
    ]);

    /// <summary>
    /// يتحقّق من شكل رقم غير فارغ، ويُعيده كما هو عند القبول — <b>بلا تشذيب ولا تحويل</b>.
    /// </summary>
    /// <param name="value">الرقم كما ورد.</param>
    public static Result<string> Validate(string? value)
    {
        string candidate = value ?? string.Empty;

        if (candidate.Length == 0)
        {
            return Result<string>.Failure(PurchasingErrors.VatNumberEmpty);
        }

        // غير المرئي أولاً: طولٌ يبدو صحيحاً وخانةٌ لا تُرى تجعل كل رسالة أخرى كاذبة.
        if (candidate.AsSpan().IndexOfAny(InvisibleControls) >= 0)
        {
            return Result<string>.Failure(PurchasingErrors.VatNumberCarriesInvisibleControl(candidate.Length));
        }

        foreach (char character in candidate)
        {
            if (char.IsAsciiDigit(character))
            {
                continue;
            }

            // رقمٌ يونيكودي ليس ASCII: عربي-هندي أو شرقي أو ديفاناغاري أو غيرها.
            // يُسمّى بذاته لأن المحاسب يراه رقماً صحيحاً ولا يفهم رسالة «ليست أرقاماً».
            return Result<string>.Failure(char.IsDigit(character)
                ? PurchasingErrors.VatNumberHasNonAsciiDigits(character)
                : PurchasingErrors.VatNumberHasNonDigits(character));
        }

        if (candidate.Length != Length)
        {
            return Result<string>.Failure(PurchasingErrors.VatNumberLength(candidate.Length));
        }

        if (candidate[0] != CountryDigit)
        {
            return Result<string>.Failure(PurchasingErrors.VatNumberPrefix(candidate[0]));
        }

        return candidate[^1] != TaxTypeDigit
            ? Result<string>.Failure(PurchasingErrors.VatNumberSuffix(candidate[^1]))
            : Result<string>.Success(candidate);
    }

    /// <summary>هل هذا نصّ «لم يُسجَّل رقم»؟ الفراغ وحده، ولا وجود لقيمة معدومة في العمود.</summary>
    /// <param name="value">القيمة.</param>
    public static bool IsUnrecorded(string? value) => string.IsNullOrEmpty(value);
}
