namespace Babel.Ai.Capture;

/// <summary>
/// مفاتيح الموارد لمنشأ القيمة — <b>من أي مصدر مادّي</b> جاءت، لا من أي صنف.
/// تُحلّ في الواجهة كبقية مفاتيح العرض (‏ADR-0021).
/// </summary>
public static class CaptureOriginKeys
{
    /// <summary>من رمز استجابة سريعة موقَّع على الفاتورة.</summary>
    public const string SignedQr = "ai.capture.origin.signed_qr";

    /// <summary>من رمز استجابة سريعة بلا توقيع (المرحلة الأولى).</summary>
    public const string UnsignedQr = "ai.capture.origin.unsigned_qr";

    /// <summary>من قراءة ضوئية للمستند.</summary>
    public const string Optical = "ai.capture.origin.optical";

    /// <summary>من نموذج مُقترِح.</summary>
    public const string Model = "ai.capture.origin.model";

    /// <summary>من إعدادات المستأجر.</summary>
    public const string TenantSetting = "ai.capture.origin.tenant_setting";

    /// <summary>أدخله إنسان.</summary>
    public const string Human = "ai.capture.origin.human";

    /// <summary>من تفريغ صوتي في متصفّح المستخدم — لم تغادر البايتات الصوتية جهازه.</summary>
    public const string SpokenOnDevice = "ai.capture.origin.spoken_on_device";
}
