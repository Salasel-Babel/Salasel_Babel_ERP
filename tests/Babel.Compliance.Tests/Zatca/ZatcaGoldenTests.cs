using System.Globalization;
using System.Text;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// <b>المتجهات الذهبية.</b> شبكة الشبكات كلها: أي انحراف بايتة واحدة في أي مسار من
/// مسارات هذا المزوّد يسقط هنا قبل أن يصل إلى مراجعة.
/// <para/>
/// <b>وحدود ما تُثبته مكتوبة في الاختبار نفسه</b>، لا في تقرير يُقرأ مرة: هذه المتجهات
/// تُثبت <b>الحتمية</b> لا <b>القبول لدى الهيئة</b>. بيئة البناء محجوبة عنها
/// (‏<c>403</c> مقيس على <c>gw-fatoora.zatca.gov.sa</c> و<c>zatca.gov.sa</c>).
/// </summary>
public sealed class ZatcaGoldenTests(ITestOutputHelper output)
{
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>
    /// إعادة التوليد تقع فقط حين يُطلب صراحةً بمتغيّر بيئة. <b>ولا تقع تلقائياً أبداً</b>:
    /// ملفٌ يُعيد كتابة نفسه عند الاختلاف ليس متجهاً ذهبياً بل مرآة.
    /// </summary>
    private const string EmitSwitch = "BABEL_ZATCA_GOLDEN_EMIT";

    [Fact]
    public void The_golden_file_exists_and_is_not_empty()
    {
        string path = ZatcaGoldenFile.Path();

        if (!File.Exists(path) && Environment.GetEnvironmentVariable(EmitSwitch) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ZatcaGoldenFile.Emit(ZatcaGoldenSet.All), Utf8);
            output.WriteLine("وُلِّد الملف الذهبي: " + path);
        }

        Assert.True(File.Exists(path), $"الملف الذهبي مفقود: {path}");
        Assert.NotEmpty(File.ReadAllText(path, Utf8));
    }

    /// <summary>
    /// حارس اللافراغ: مجموعة متجهات ضامرة تمرّ دائماً، وهي أسوأ من غياب المجموعة
    /// لأنها توحي بتغطية فتُوقف البحث.
    /// </summary>
    [Fact]
    public void The_vector_set_is_not_vacuous_and_its_identifiers_are_unique()
    {
        Assert.True(ZatcaGoldenSet.All.Count >= 25,
            FormattableString.Invariant($"عدد المتجهات {ZatcaGoldenSet.All.Count} — النطاق ضامر"));

        List<string> duplicates = [.. ZatcaGoldenSet.All
            .GroupBy(v => v.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        Assert.True(duplicates.Count == 0, "معرّفات مكرّرة: " + string.Join("، ", duplicates));

        // ولكل متجه وصف عربي: متجه بلا وصف لا يُقرأ عند انحرافه فيُقبل انحرافه.
        Assert.All(ZatcaGoldenSet.All, v => Assert.False(string.IsNullOrWhiteSpace(v.DescriptionAr)));
    }

    [Fact]
    public void Every_vector_matches_the_committed_golden_file()
    {
        string path = ZatcaGoldenFile.Path();

        if (Environment.GetEnvironmentVariable(EmitSwitch) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ZatcaGoldenFile.Emit(ZatcaGoldenSet.All), Utf8);
            output.WriteLine("أُعيد توليد الملف الذهبي بطلب صريح: " + path);
        }

        IReadOnlyList<ZatcaGoldenFile.Drift> drifts =
            ZatcaGoldenFile.Verify(File.ReadAllText(path, Utf8), ZatcaGoldenSet.All);

        if (drifts.Count == 0)
        {
            return;
        }

        StringBuilder message = new();
        message.AppendLine("انحراف في مسار الهيئة — كل بايتة هنا دخلت توقيعاً أو رمز QR:");
        foreach (ZatcaGoldenFile.Drift drift in drifts.Take(20))
        {
            message.AppendLine(CultureInfo.InvariantCulture,
                $"  [{drift.Id}] {drift.Field}\n      المتوقَّع: {Cut(drift.Expected)}\n      الفعلي  : {Cut(drift.Actual)}");
        }

        Assert.Fail(message.ToString());
    }

    /// <summary>
    /// <b>المصيدة نفسها التي أسقطت الشكل القانوني في هذا المستودع، مطبَّقة هنا:</b>
    /// جهاز واحد بلغة مختلفة يُنتج بايتات مختلفة بصمت. مقيس في هذه المنظومة:
    /// تحت <c>ar-SA</c> يعطي <c>100.5m.ToString("0.00")</c> فاصلة عربية، ويعطي
    /// تنسيق التاريخ الافتراضي تقويماً هجرياً يحمل <c>U+200F</c> بداخله.
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("ar-EG")]
    [InlineData("de-DE")]
    [InlineData("fa-IR")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void Canonical_bytes_are_identical_under_any_ambient_culture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = cultureName.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(cultureName);

            IReadOnlyList<ZatcaGoldenFile.Drift> drifts = ZatcaGoldenFile.Verify(
                File.ReadAllText(ZatcaGoldenFile.Path(), Utf8), ZatcaGoldenSet.All);

            Assert.True(drifts.Count == 0,
                $"الثقافة «{cultureName}» غيّرت بايتات مسار الهيئة: " +
                string.Join("؛ ", drifts.Take(5).Select(d => $"[{d.Id}] {d.Field}")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// <b>إثبات أن الاختبار السابق غير فارغ:</b> الثقافة العربية تُفسد التحويل الساذج
    /// فعلاً على هذا الجهاز. لولا هذا لكان «لا انحراف تحت ar-SA» عبارة بلا معنى.
    /// </summary>
    [Fact]
    public void The_hostile_culture_theory_is_not_vacuous_on_this_machine()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            // التحويل الساذج بلا مزوّد تنسيق هو **موضوع** الاختبار لا سهو فيه: الغرض
            // إثبات أن الثقافة المحيطة تُفسده. إسكات موضعي مُعلَّل.
#pragma warning disable CA1305 // Specify IFormatProvider
            string naiveAmount = 1350.50m.ToString("0.00");
            string naiveDate = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc).ToString("d");
#pragma warning restore CA1305

            output.WriteLine("المبلغ الساذج تحت ar-SA: " + naiveAmount);
            output.WriteLine("التاريخ الساذج تحت ar-SA: " + naiveDate);

            Assert.DoesNotContain('.', naiveAmount);
            Assert.Contains('‏', naiveDate);

            // وما نكتبه نحن لا يتأثر.
            Assert.Equal("1350.50", Babel.Compliance.Zatca.Documents.ZatcaAmounts.Render(1350.50m, "t"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// <b>وهذا الاختبار هو أهم سطر في هذا الملف:</b> يطبع، في كل تشغيل، ما تُثبته هذه
    /// المتجهات وما لا تُثبته. الغرض ألّا يقرأ أحد «أخضر» فيفهم «مقبول لدى الهيئة».
    /// </summary>
    [Fact]
    public void The_scope_of_what_these_vectors_prove_is_stated_in_the_run_output()
    {
        output.WriteLine("ما تُثبته المتجهات الذهبية:");
        output.WriteLine("  • أن كل بايت يخرج من هذا المزوّد حتمي: لا يتحرّك بترقية، ولا بثقافة، ولا بترتيب.");
        output.WriteLine("  • أن السلسلة رابطة تشفيرياً: العدّاد والبصمة السابقة داخل البايتات المُجزَّأة.");
        output.WriteLine("  • أن قاعدة الاستبعاد تُزيل ثلاث مجموعات بالضبط.");
        output.WriteLine("");
        output.WriteLine("ما لا تُثبته، ولا تدّعيه:");
        output.WriteLine("  • أن الهيئة تقبل هذه البايتات. لا مرجع من الهيئة في هذه البيئة: النطاقات تُعيد 403.");
        output.WriteLine("  • أن الترميزات المختارة (خام مقابل ستّ‌عشري) هي المطلوبة.");
        output.WriteLine("  • أن ترتيب وسوم QR وأشكال قيمها هي المطلوبة.");
        output.WriteLine("");
        output.WriteLine($"عدد المتجهات: {ZatcaGoldenSet.All.Count}");
        output.WriteLine("كل بند غير مُتحقَّق منه مُسجَّل في docs/evidence/verification-debt.md ومعه ما يُغلقه.");

        Assert.NotEmpty(ZatcaGoldenSet.All);
    }

    private static string Cut(string value)
    {
        string single = value
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
        return single.Length <= 240 ? single : single[..240] + "…";
    }
}
