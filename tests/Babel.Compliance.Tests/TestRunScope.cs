using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Compliance.Tests;

/// <summary>
/// نطاق التشغيل: لاحقة <b>خاصّة بهذه العملية</b> تُلحَق باسم كل قاعدة بيانات تُنشئها
/// مجموعة الاختبار، فتملك كل عملية قواعدها وحدها.
/// <para>
/// <b>لماذا ليست معرّف العملية وحده:</b> نظام التشغيل <b>يعيد استعمال</b> معرّفات
/// العمليات. فلو كان الاسم <c>…_p4711</c> وحده، لتبنّت عمليةٌ لاحقة تحمل المعرّف
/// نفسه قاعدةً متروكة من تشغيل انهار قبلها — وهي بالضبط نفس المصادفة في ثوب جديد.
/// لذلك تُضاف عشرة أرقام ست عشرية من مولّد عشوائي معمّى: المعرّف يبقى ليُعرف صاحب
/// القاعدة عند التشخيص، والرمز العشوائي هو ما يضمن التفرّد.
/// </para>
/// <para>
/// <b>وحدّ الـ63 بايتاً ليس تفصيلاً:</b> معرّفات PostgreSQL تُقصّ عند
/// <c>NAMEDATALEN − 1</c> = 63 بايتاً <b>بصمت</b>، واسمان مختلفان يُقصّان إلى النصّ
/// نفسه هما قاعدة بيانات واحدة. لذلك لا يُقصّ هنا شيء أبداً: الاسم الذي لا يتّسع
/// يرفع استثناءً يسمّي الجذع وطوله والحدّ.
/// </para>
/// </summary>
internal static class TestRunScope
{
    /// <summary>حدّ المعرّف في PostgreSQL: ‏<c>NAMEDATALEN − 1</c> بايتاً.</summary>
    public const int MaxIdentifierBytes = 63;

    private const int TokenHexLength = 10;

    /// <summary>لاحقة هذه العملية بالشكل <c>p{معرّف العملية}_{رمز عشوائي}</c>.</summary>
    public static string Suffix { get; } = Build();

    /// <summary>يبني اسماً خاصّاً بهذه العملية من جذع ثابت.</summary>
    /// <param name="stem">الجذع الثابت — مثل <c>babel_api_tests</c>.</param>
    /// <returns>الجذع ولاحقة هذه العملية.</returns>
    /// <exception cref="ArgumentException">إن لم يتّسع الاسم في 63 بايتاً.</exception>
    public static string Name(string stem)
    {
        ArgumentException.ThrowIfNullOrEmpty(stem);

        string name = stem + "_" + Suffix;
        int bytes = Encoding.UTF8.GetByteCount(name);
        if (bytes > MaxIdentifierBytes)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "اسم قاعدة الاختبار «{0}» طوله {1} بايتاً ويتجاوز حدّ المعرّف ({2}). "
                    + "القصّ الصامت يجعل اسمين مختلفين قاعدةً واحدة، فالجذع «{3}» يُقصَّر في المصدر.",
                    name,
                    bytes,
                    MaxIdentifierBytes,
                    stem),
                nameof(stem));
        }

        return name;
    }

    /// <summary>
    /// معرّف العملية التي أنشأت قاعدةً بهذا الاسم، أو <c>null</c> إن لم يكن الاسم
    /// من صنع هذا النطاق أصلاً — فلا يُمَسّ.
    /// </summary>
    /// <param name="database">اسم قاعدة البيانات كما في <c>pg_database</c>.</param>
    /// <param name="stem">الجذع المتوقّع.</param>
    /// <returns>معرّف العملية المالكة، أو <c>null</c>.</returns>
    public static int? OwnerProcessId(string database, string stem)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(stem);

        string head = stem + "_p";
        if (!database.StartsWith(head, StringComparison.Ordinal))
        {
            return null;
        }

        string rest = database[head.Length..];
        int separator = rest.IndexOf('_', StringComparison.Ordinal);
        if (separator <= 0 || rest.Length - separator - 1 != TokenHexLength)
        {
            return null;
        }

        foreach (char c in rest[(separator + 1)..])
        {
            if (!char.IsAsciiDigit(c) && (c < 'a' || c > 'f'))
            {
                return null;
            }
        }

        return int.TryParse(rest[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out int pid)
            ? pid
            : null;
    }

    /// <summary>
    /// هل العملية المالكة ما زالت حيّة؟ الجواب <c>true</c> عند الشكّ: كنس قاعدة
    /// قد تكون قيد الاستعمال هو العطل نفسه الذي يُصلحه هذا الملف.
    /// </summary>
    /// <param name="processId">معرّف العملية المالكة.</param>
    /// <returns><c>false</c> إن ثبت أن العملية ماتت.</returns>
    public static bool OwnerIsAlive(int processId)
    {
        if (processId == Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // لا عملية بهذا المعرّف: ماتت يقيناً.
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string Build()
    {
        string pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenHexLength / 2))
            .ToLowerInvariant();
        return "p" + pid + "_" + token;
    }
}
