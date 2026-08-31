using System.Diagnostics;
using System.Reflection;
using System.Text;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>الالتقاط يشير إلى مستند ولا يحمله.</b>
/// <para>
/// <b>ما كان، مقيساً:</b> <c>InvoiceCaptureService.CaptureAsync</c> كانت تأخذ
/// <c>ExtractionRequest</c> وفيه <c>ReadOnlyMemory&lt;byte&gt;</c>. ونوعٌ يحمل بايتات
/// عند نقطة دخولٍ تُنشَر على HTTP هو نوعٌ تعبر بايتاته السلك <c>base64</c> داخل جسم
/// JSON: صورة الفاتورة تنتفخ الثلث، وتهبط في سجلّ الطلب كاملةً، <b>ولا تُخزَّن في
/// مكان</b> — فالمستند الذي يستند إليه القيد يُقرأ ثم يختفي ويبقى في السجلّ وحده.
/// </para>
/// <para>
/// <b>ولماذا حارسٌ لا تعليق:</b> العودة إلى الشكل القديم <b>لا تكسر شيئاً</b>. لا
/// اختبار يحمرّ، ولا ترجمة تسقط: يكفي أن يضيف أحدهم حقل بايتات إلى طلب الالتقاط
/// «مؤقتاً، للرفع المباشر». وهذا الملفّ هو ما يجعل ذلك قراراً يُتّخذ لا انحداراً يقع.
/// </para>
/// <para>
/// ولكل حارسٍ هنا <b>شاهدٌ موجب</b> يعيش في تجميعة الاختبار، يُثبت أن الماسح ما زال
/// يطابق — فلا تُلتبَس الخُضرة بالفراغ (فخ-68).
/// </para>
/// </summary>
public sealed class CaptureCarriesAReferenceNotTheBytes
{
    private const string CaptureService = "InvoiceCaptureService";

    /// <summary>
    /// الأنواع التي تعني «بايتات خام». ‏<c>Stream</c> منها: تمريره عبر نقطة دخول
    /// يعني أن جسم الطلب نفسه هو المستند.
    /// </summary>
    private static readonly string[] RawByteTypes =
    [
        "System.Byte[]",
        "System.ReadOnlyMemory`1[System.Byte]",
        "System.Memory`1[System.Byte]",
        "System.IO.Stream",
    ];

    /// <summary>
    /// الملفّ الوحيد في <c>src/Babel.Ai/</c> الذي يجوز أن يُرمّز بايتات مستند نصّاً،
    /// ومعه سببه. <b>وهو الطرف البعيد لا القريب</b>: نموذج رؤية عبر HTTP لا يقبل
    /// الثنائي إلّا هكذا، والقرار الذي يسبقه تنظيمي (<c>ExtractionResidency</c>)
    /// ويُقرأ وقت التركيب.
    /// </summary>
    private static readonly (string Path, string Why)[] MayEncodeDocumentBytes =
    [
        ("src/Babel.Ai/Extraction/GitHubModels/ExtractionPrompt.cs",
            "الطرف البعيد: نموذج رؤية عبر HTTP لا يقبل الثنائي إلّا مُرمَّزاً — والإقامة تُقرأ وقت التركيب"),
    ];

    // ── الحارس الأول: لا بايتة في نقطة دخول الالتقاط ─────────────────────────

    /// <summary>
    /// لا معامِل في أي دالّة عامّة على خدمة الالتقاط يحمل بايتات خام، لا مباشرةً ولا
    /// في خصائص نوعٍ يُمرَّر إليها.
    /// </summary>
    [Fact]
    public void NoPublicEntryPointOnTheCaptureServiceCarriesRawBytes()
    {
        List<string> violations = [.. RawByteCarriersOn(CaptureServiceType())];

        Assert.True(
            violations.Count == 0,
            "بايتات مستند عند نقطة دخول الالتقاط — وذلك يعني base64 في جسم الطلب، "
            + "وانتفاخاً بالثلث، وصورةً كاملة في سجلّ الطلب، ومستنداً لا يُخزَّن:\n"
            + string.Join('\n', violations));
    }

    /// <summary>
    /// <b>الشاهد الموجب.</b> نوعٌ متعمَّد يحمل بايتات، ودالّةٌ متعمَّدة تأخذه — لو كفّ
    /// الماسح عن المطابقة (لأن شكل المعامل تغيّر، أو لأن قائمة الأنواع ضمرت) لسقط
    /// <b>هذا</b> الاختبار، ولم يمرّ الأول أخضر صامتاً.
    /// </summary>
    [Fact]
    public void TheEntryPointGuardBitesOnItsOwnControl()
    {
        List<string> caught = [.. RawByteCarriersOn(typeof(CaptureMutationProbe))];

        Assert.Contains(caught, entry => entry.Contains("System.Byte[]", StringComparison.Ordinal));
        Assert.Contains(caught, entry => entry.Contains("ReadOnlyMemory", StringComparison.Ordinal));
        Assert.Contains(caught, entry => entry.Contains("System.IO.Stream", StringComparison.Ordinal));

        // أربعة مواضع بالضبط: ثلاثة معامِلات مباشرة (مصفوفة، ودفق، ومصفوفة شاهد
        // الماسح النصّي) وخاصّيةٌ واحدة داخل نوعٍ مُمرَّر. والعدد مكتوب صراحةً كي
        // يُفشِل البناءَ من يضيف شكلاً رابعاً إلى الشاهد بلا أن يقرّر ما يعنيه.
        Assert.Equal(4, caught.Count);
    }

    /// <summary>
    /// وشاهدٌ على أن الماسح يقرأ خدمةً حقيقية لا نوعاً فارغاً: خدمة الالتقاط موجودة،
    /// ولها دوالّ عامّة، وأولها يأخذ طلباً يحمل معرّف مرفق.
    /// </summary>
    [Fact]
    public void TheScanReadsTheRealCaptureServiceAndItStillTakesAnAttachmentReference()
    {
        Type service = CaptureServiceType();

        MethodInfo capture = service
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(method => method.Name == "CaptureAsync");

        Type request = capture.GetParameters().Single(p => p.ParameterType.Name == "CaptureRequest").ParameterType;

        Assert.Contains(
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType.FullName == "Babel.Contracts.Storage.AttachmentId");
    }

    // ── الحارس الثاني: الترميز نصّاً في موضع واحد معلَن ───────────────────────

    /// <summary>
    /// ‏<c>Convert.ToBase64String</c> داخل <c>src/Babel.Ai/</c> يظهر في الملفّات
    /// المعلَنة أعلاه <b>وحدها</b>. والفحص على مجموعة <c>git ls-files</c> لا على القرص.
    /// </summary>
    [Fact]
    public void DocumentBytesAreEncodedAsTextInExactlyTheDeclaredPlaces()
    {
        string[] declared = [.. MayEncodeDocumentBytes.Select(static entry => entry.Path).Order(StringComparer.Ordinal)];
        string[] actual = [.. FilesEncodingBytesUnder("src/Babel.Ai/")];

        List<string> problems = [];

        foreach (string appeared in actual.Except(declared, StringComparer.Ordinal))
        {
            problems.Add(
                $"موضع جديد يُرمّز بايتات نصّاً: {appeared}\n"
                + "  → إن كان طرفاً بعيداً فاكتبه في MayEncodeDocumentBytes بسببه؛ وإن كان طرفاً "
                + "قريباً فهو base64 في جسم طلب، وهو ما فُتح هذا الحارس لمنعه.");
        }

        foreach (string gone in declared.Except(actual, StringComparer.Ordinal))
        {
            problems.Add($"موضع معلَن ولم يعد يُرمّز شيئاً: {gone} — احذفه من القائمة.");
        }

        Assert.True(problems.Count == 0, "قائمة مواضع الترميز لم تعد تطابق الواقع:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// <b>الشاهد الموجب للماسح النصّي.</b> ملفٌّ متعمَّد في تجميعة الاختبار يحمل النمط
    /// نفسه: لو كفّ التعبير عن المطابقة لسقط هذا، ولم يمرّ الأول أخضر على قائمة فارغة.
    /// </summary>
    [Fact]
    public void TheTextScanBitesOnItsOwnControl()
    {
        string[] found = [.. FilesEncodingBytesUnder("tests/Babel.ArchitectureTests/")];

        Assert.Contains("tests/Babel.ArchitectureTests/CaptureCarriesAReferenceNotTheBytes.cs", found, StringComparer.Ordinal);
    }

    // ── الأدوات ──────────────────────────────────────────────────────────────

    private static Type CaptureServiceType() => BabelAssemblies
        .TypesOf(BabelAssemblies.Named("Babel.Ai"))
        .Single(type => type.Name == CaptureService);

    /// <summary>
    /// يعدّد كل موضعٍ في دوالّ نوعٍ عامّة يحمل بايتات خام: معامِلاً مباشراً، أو
    /// خاصّيةً عامّة في نوعٍ مُمرَّر — مستوى واحد، وهو ما يكفي لشكل الطلبات هنا.
    /// </summary>
    private static IEnumerable<string> RawByteCarriersOn(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (IsRawBytes(parameter.ParameterType))
                {
                    yield return $"{type.Name}.{method.Name}({parameter.Name}) : {Describe(parameter.ParameterType)}";
                    continue;
                }

                if (parameter.ParameterType.Assembly.GetName().Name?.StartsWith("Babel.", StringComparison.Ordinal) != true)
                {
                    continue;
                }

                foreach (PropertyInfo property in parameter.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsRawBytes(property.PropertyType))
                    {
                        yield return $"{type.Name}.{method.Name}({parameter.Name}).{property.Name} : {Describe(property.PropertyType)}";
                    }
                }
            }
        }
    }

    private static bool IsRawBytes(Type type) =>
        Array.Exists(RawByteTypes, candidate => string.Equals(candidate, Describe(type), StringComparison.Ordinal))
        || (type.BaseType is not null && string.Equals("System.IO.Stream", type.BaseType.FullName, StringComparison.Ordinal));

    /// <summary>
    /// اسمٌ قانوني للنوع. <b>و<c>Type.FullName</c> وحده لا يكفي</b>: على نوع عام
    /// يُرجع وسائطه <b>مُؤهَّلةً بالتجميعة</b> — أي <c>ReadOnlyMemory`1[[System.Byte,
    /// System.Private.CoreLib, Version=…]]</c> — فالمقارنة بنصّ مكتوب بيد تفشل دائماً
    /// وتمرّ خضراء. وهذا بالضبط ما أمسكه الشاهد الموجب قبل أن يُودَع هذا الملفّ.
    /// </summary>
    private static string Describe(Type type) => type.IsGenericType
        ? (type.GetGenericTypeDefinition().FullName ?? type.Name)
            + "[" + string.Join(',', type.GetGenericArguments().Select(Describe)) + "]"
        : type.FullName ?? type.Name;

    /// <summary>
    /// الملفّات المتعقَّبة تحت مجلد، التي تحوي نداء ترميز بايتات نصّاً.
    /// <b>والمصدر <c>git ls-files</c></b>: ملفٌّ غير متعقَّب لا يصل أحداً.
    /// </summary>
    private static IEnumerable<string> FilesEncodingBytesUnder(string folder)
    {
        // ‏"Convert" ثم "ToBase64String" مفصولتين كي لا يطابق هذا السطرُ نفسَه.
        const string Needle = "Convert" + ".ToBase64String";

        return TrackedFiles()
            .Where(path => path.StartsWith(folder, StringComparison.Ordinal))
            .Where(static path => path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(Path.Combine(RepositoryLayout.Root, path)).Contains(Needle, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }

    private static string[] TrackedFiles()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        return git.ExitCode == 0
            ? output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            : throw new InvalidOperationException(
                "‏git ls-files أخفق، فلا سبيل إلى معرفة محتوى المستودع — والحارس يرمي ولا "
                + "يخمّن على ما يقع على القرص. / git ls-files failed: " + error);
    }
}

/// <summary>
/// <b>شاهدٌ موجب متعمَّد، ويعيش في تجميعة الاختبار لا في المنتج.</b> يحمل بالضبط الشكل
/// الذي يمنعه الحارس أعلاه، فيُثبت أن الماسح ما زال يطابق.
/// </summary>
internal sealed class CaptureMutationProbe
{
    /// <summary>طلبٌ يحمل بايتات — الشكل الممنوع.</summary>
    internal sealed record ProbeRequest
    {
        public ReadOnlyMemory<byte> Content { get; init; }
    }

    /// <summary>عدّادٌ يُلمَس كي تبقى الدوالّ دوالَّ نسخة — والماسح يقرأ دوالّ النسخة.</summary>
    private int _touched;

    public void TakesABareArray(byte[] content) => _touched += content.Length;

    public void TakesAStream(Stream content) => _touched += content is null ? 0 : 1;

    public void TakesARequestCarryingBytes(ProbeRequest request) => _touched += request.Content.Length;

    /// <summary>
    /// <b>وهذا النداء هو الشاهد الموجب للماسح النصّي</b>: نداءُ ترميزٍ حقيقي في ملفّ
    /// متعقَّب، فلو كفّ التعبير عن المطابقة لعادت المجموعة فارغةً وسقط شاهده — بدل أن
    /// يمرّ حارس «موضع واحد معلَن» أخضر على لا شيء.
    /// </summary>
    public string EncodesForTheTextScanControl(byte[] content) =>
        Convert.ToBase64String(content) + _touched.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
