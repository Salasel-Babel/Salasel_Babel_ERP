using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Core.Access;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لا نصّ اعتماد يعبر حدّ المخزن — والتجزئة تقع فوقه دائماً.</b>
/// <para>
/// وهذا هو الشرط الذي يجعل «لا يُخزَّن اعتمادٌ قابل للاستعمال» جملةً <b>مفروضة</b> لا
/// موصوفة. فالخرق لا يقع بقرارٍ سيّئ بل بسهو: مؤلّفٌ يضيف دالّةً إلى
/// <see cref="IAccessDirectory"/> تأخذ <c>credential</c> بدل <c>digest</c> «مؤقّتاً»،
/// فيصير النصّ في نطاق المخزن — ثم في وسيط استعلام، ثم في سجلّ بطيء، ثم في نسخة احتياطية.
/// ولا يشتكي شيء: الشيفرة تعمل، والاختبارات خضراء، والفرق سطرُ توقيع واحد.
/// </para>
/// <para>
/// <b>ولماذا حارسٌ لا مراجعة:</b> المراجع يرى وسيطاً اسمه <c>credential</c> في واجهة
/// مخزن فيقرؤه طبيعياً — الأسماء الطبيعية هي بالضبط ما يمرّ. والشرط بنيوي: <b>موضع
/// التجزئة الوحيد فوق هذا الحدّ</b>، وما تحته لا يعرف إلا بصمات.
/// </para>
/// <para>
/// وفحصان: أحدهما على الحدّ (التوقيعات)، والآخر على المخزن (أعمدة الاستمرارية) — لأن
/// حدّاً نظيفاً فوق جدولٍ يحمل عموداً نصّياً للاعتماد لا يُثبت شيئاً.
/// </para>
/// </summary>
public sealed class NoPlaintextCredentialCrossesTheAccessStoreBoundary
{
    /// <summary>ألفاظٌ تدلّ على نصّ اعتماد لا على بصمته.</summary>
    private static readonly string[] PlaintextWords = ["credential", "token", "secret", "password"];

    private static MethodInfo[] DirectoryMethods() =>
        typeof(IAccessDirectory).GetMethods(BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void EveryCredentialCrossingTheDirectoryBoundaryIsADigest()
    {
        List<string> violations = [];
        int digestParameters = 0;
        int methods = 0;

        foreach (MethodInfo method in DirectoryMethods())
        {
            methods++;

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string name = parameter.Name ?? string.Empty;

                if (name.EndsWith("Digest", StringComparison.Ordinal))
                {
                    digestParameters++;
                    continue;
                }

                // ‏**استثناءٌ واحد مسمّى حرفياً لا بنمط:** رمز الإلغاء يحمل لفظ «token» ولا
                // علاقة له باعتماد. ونمطٌ فضفاض («ما انتهى بـToken مسموح») كان سيقبل
                // ‏sessionToken وaccessToken معه — وهو بالضبط ما يجعل حارساً كهذا يمرّ على
                // العطل الذي وُجد لأجله. والاسم مقارَنٌ كاملاً وبحساسية حالة الأحرف.
                if (string.Equals(name, "cancellationToken", StringComparison.Ordinal))
                {
                    continue;
                }

                if (PlaintextWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{typeof(IAccessDirectory).Name}.{method.Name}({name})");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "وسيطٌ يحمل نصّ اعتماد إلى حدّ المخزن. والتجزئة تقع **فوق** هذا الحدّ في "
            + "AccessCredentials.Digest، وما تحته لا يعرف إلا بصمات — وإلا صار النصّ في وسيط "
            + "استعلام ثم في سجلّ ثم في نسخة احتياطية:\n"
            + string.Join('\n', violations));

        // حارس اللافراغ من الطرفين: واجهةٌ فارغة تمرّ دائماً، وواجهةٌ بلا بصمة واحدة
        // تعني أن الماسح لم يعد يرى ما وُجد لأجله (فخ-43 · فخ-68).
        Assert.True(methods >= 5, $"قُرئت {methods} دوالّ فقط على حدّ المخزن — الماسح ضامر");
        Assert.True(
            digestParameters >= 4,
            $"وُجدت {digestParameters} بصمات فقط في التوقيعات — الماسح لا يرى ما وُجد لأجله");
    }

    [Fact]
    public void NoAccessTableCarriesAColumnThatHoldsAUsableCredential()
    {
        string path = Path.Combine(RepositoryLayout.Root, "src/Babel.Core/Persistence/AccessRows.cs");
        Assert.True(File.Exists(path), $"صفوف استمرارية المصادقة غير موجودة عند {path} — الماسح يقرأ لا شيء");

        string[] lines = File.ReadAllLines(path);
        List<string> violations = [];
        int properties = 0;
        int digestProperties = 0;

        foreach (string line in lines)
        {
            string text = line.Trim();

            if (!text.StartsWith("public ", StringComparison.Ordinal) || !text.Contains(" { get; set; }", StringComparison.Ordinal))
            {
                continue;
            }

            properties++;

            if (text.Contains(" Digest ", StringComparison.Ordinal))
            {
                digestProperties++;
                continue;
            }

            if (PlaintextWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(text);
            }
        }

        Assert.True(
            violations.Count == 0,
            "عمودُ استمراريةٍ يحمل نصّ اعتماد. والمُودَع بصمة، فمن يقرأ نسخةً احتياطية لا ينتحل بها أحداً:\n"
            + string.Join('\n', violations));

        Assert.True(properties >= 20, $"قُرئت {properties} خاصية فقط — الماسح ضامر");
        Assert.True(digestProperties >= 2, $"وُجدت {digestProperties} بصمة فقط في الصفوف — الماسح لا يرى ما وُجد لأجله");
    }
}
