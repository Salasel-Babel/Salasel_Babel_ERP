using System.Collections.ObjectModel;

namespace Babel.SharedKernel;

/// <summary>
/// نتيجة عملية بلا قيمة. الفشل المتوقّع يُعاد قيمةً لا استثناءً — الاستثناء لخلل برمجي،
/// والفشل المحاسبي (فترة مقفلة، استحقاق منقضٍ، قيد غير متوازن) ليس خللاً برمجياً.
/// </summary>
public sealed class Result
{
    private static readonly Result SuccessInstance = new([]);

    private Result(IReadOnlyList<Error> errors) => Errors = errors;

    /// <summary>أخطاء الفشل. فارغة عند النجاح.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>هل نجحت العملية؟</summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>هل فشلت العملية؟</summary>
    public bool IsFailure => Errors.Count > 0;

    /// <summary>نجاح.</summary>
    public static Result Success() => SuccessInstance;

    /// <summary>فشل بخطأ واحد.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(new ReadOnlyCollection<Error>([error]));
    }

    /// <summary>فشل بمجموعة أخطاء. القائمة الفارغة خطأ استعمال.</summary>
    public static Result Failure(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("الفشل بلا أخطاء غير ممكن. / A failure must carry at least one error.", nameof(errors));
        }

        return new Result(new ReadOnlyCollection<Error>([.. errors]));
    }
}
