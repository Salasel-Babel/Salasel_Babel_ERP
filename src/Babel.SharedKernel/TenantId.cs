namespace Babel.SharedKernel;

/// <summary>معرّف المستأجر. تعدد المستأجرين معطى لا خيار (وثيقة المعمارية ت-5).</summary>
public readonly record struct TenantId(Guid Value)
{
    /// <summary>قيمة غير مخصّصة. وجودها في مسار كتابة خطأ برمجي.</summary>
    public static TenantId None => new(Guid.Empty);

    /// <summary>هل المعرّف مخصّص فعلاً؟</summary>
    public bool IsAssigned => Value != Guid.Empty;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
