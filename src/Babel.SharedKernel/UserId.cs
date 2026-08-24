namespace Babel.SharedKernel;

/// <summary>معرّف المستخدم. محور تسعير مستقل — انظر قياس الاستخدام في Babel.Core.</summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>
    /// فاعل النظام: مهام مجدولة وترحيل آلي. ليس مستخدماً مفوتراً.
    /// الاسم <c>SystemActor</c> لا <c>System</c> لأن الثاني يحجب فضاء الأسماء <c>System</c> داخل النوع.
    /// </summary>
    public static UserId SystemActor => new(new Guid("00000000-0000-0000-0000-0000000000ff"));

    /// <summary>قيمة غير مخصّصة.</summary>
    public static UserId None => new(Guid.Empty);

    /// <summary>هل المعرّف مخصّص فعلاً؟</summary>
    public bool IsAssigned => Value != Guid.Empty;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
