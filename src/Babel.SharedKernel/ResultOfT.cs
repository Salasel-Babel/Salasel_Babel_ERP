using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Babel.SharedKernel;

/// <summary>نتيجة عملية تحمل قيمة عند النجاح.</summary>
/// <typeparam name="T">نوع القيمة المعادة.</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<Error> errors)
    {
        _value = value;
        Errors = errors;
    }

    /// <summary>أخطاء الفشل. فارغة عند النجاح.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>هل نجحت العملية؟</summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>هل فشلت العملية؟</summary>
    public bool IsFailure => Errors.Count > 0;

    /// <summary>القيمة عند النجاح. قراءتها عند الفشل خطأ برمجي.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("قراءة قيمة نتيجة فاشلة. / Reading the value of a failed result.");

    /// <summary>نجاح بقيمة.</summary>
    public static Result<T> Success(T value) => new(value, []);

    /// <summary>فشل بخطأ واحد.</summary>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, new ReadOnlyCollection<Error>([error]));
    }

    /// <summary>فشل بمجموعة أخطاء. القائمة الفارغة خطأ استعمال.</summary>
    public static Result<T> Failure(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("الفشل بلا أخطاء غير ممكن. / A failure must carry at least one error.", nameof(errors));
        }

        return new Result<T>(default, new ReadOnlyCollection<Error>([.. errors]));
    }

    /// <summary>محاولة قراءة القيمة دون استثناء.</summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return IsSuccess;
    }
}
