using System.Globalization;
using System.Text;
using Babel.Canonicalization.Golden;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Tests;

/// <summary>
/// المتجهات الذهبية داخل مجموعة الاختبارات. نفس التعريفات التي تستخدمها أداة
/// التكامل المستمر — مصدر واحد، لا نسختان تنحرفان.
/// </summary>
public sealed class GoldenVectorTests
{
    /// <summary>
    /// يبحث صعوداً عن جذر المستودع. ملاحظة: في worktree يكون ".git" ملفاً لا مجلداً،
    /// ولذلك نبحث عن الملف الذهبي نفسه بدل الاعتماد على شكل ".git".
    /// </summary>
    private static string GoldenPath() => GoldenPath(GoldenSetIdentity.V1);

    /// <summary>يبحث صعوداً عن ملف مجموعة إصدار بعينه.</summary>
    internal static string GoldenPath(GoldenSetIdentity identity)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(d.FullName, "tests", "golden", identity.FileName);
            if (File.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        Assert.Fail($"تعذّر العثور على tests/golden/{identity.FileName} صعوداً من " + AppContext.BaseDirectory);
        return string.Empty;
    }

    [Fact]
    public void GoldenFileExists()
        => Assert.True(File.Exists(GoldenPath()), $"الملف الذهبي مفقود: {GoldenPath()}");

    [Fact]
    public void EveryVectorMatchesTheCommittedGoldenFile()
    {
        var stored = File.ReadAllText(GoldenPath(), new UTF8Encoding(false));
        var drifts = GoldenFile.Verify(GoldenSetIdentity.V1, stored, GoldenVectorSet.All);

        if (drifts.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("انحراف في الشكل القانوني — أي تغيير هنا يُبطل كل تحقّق سابق:");
        foreach (var d in drifts.Take(20))
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  [{d.Id}] {d.Field}\n      expected: {Cut(d.Expected)}\n      actual  : {Cut(d.Actual)}");
        Assert.Fail(sb.ToString());
    }

    [Fact]
    public void EveryVectorPassesItsStructuralClaim()
    {
        var problems = GoldenFile.StructuralChecks(GoldenVectorSet.All);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void VectorCountIsAtLeastTheDocumentedMinimum()
        => Assert.True(GoldenVectorSet.All.Count >= 90,
            $"عدد المتجهات {GoldenVectorSet.All.Count} — لا يجوز أن ينقص.");

    /// <summary>
    /// ‏v1 <b>مجمَّد</b>: 97 متجهاً بالضبط، وبصمة بيان محدّدة، وبصمة مخطّط محدّدة —
    /// كلها مكتوبة هنا بقيمها الحرفية. أي تعديل على v1، مهما بدا بريئاً، يُسقط هذا
    /// الاختبار قبل أن يصل إلى مراجعة.
    /// </summary>
    [Fact]
    public void TheV1SetIsFrozenAtNinetySevenVectorsWithItsManifestAndFingerprint()
    {
        Assert.Equal(97, GoldenVectorSet.All.Count);
        Assert.Equal(
            "99d4deac27f0eed12e111c5718fda2286df165d2b2ec957f554aafc11b858310",
            JournalEntrySchema.V1.Fingerprint);

        var stored = File.ReadAllText(GoldenPath(), new UTF8Encoding(false));
        Assert.Contains(
            "\"manifest_sha256\": \"7bd2c8e8b2da05c3ad4f5a0375c8605177884e5b5a57cd5db1ca651d94bcf856\"",
            stored, StringComparison.Ordinal);
        Assert.Contains("\"vector_count\": 97", stored, StringComparison.Ordinal);
        Assert.Contains("\"canon_version\": \"v1\"", stored, StringComparison.Ordinal);
        Assert.Contains("\"wire_magic\": \"babel.canon/v1\"", stored, StringComparison.Ordinal);
    }

    /// <summary>
    /// مجموعة v2 كاملة، ولا تنقص عن مجموعة v1 عدداً — وإلّا لكان الإصدار الأوسع
    /// تغطيةً أقلَّ تثبيتاً.
    /// </summary>
    [Fact]
    public void TheV2SetIsAtLeastAsThoroughAsTheV1Set()
    {
        Assert.True(GoldenVectorSetV2.All.Count >= GoldenVectorSet.All.Count,
            $"متجهات v2 = {GoldenVectorSetV2.All.Count} ومتجهات v1 = {GoldenVectorSet.All.Count}.");
        Assert.True(File.Exists(GoldenPath(GoldenSetIdentity.V2)));
    }

    [Fact]
    public void EveryV2VectorMatchesTheCommittedGoldenFile()
    {
        var stored = File.ReadAllText(GoldenPath(GoldenSetIdentity.V2), new UTF8Encoding(false));
        var drifts = GoldenFile.Verify(GoldenSetIdentity.V2, stored, GoldenVectorSetV2.All);

        if (drifts.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("انحراف في الشكل القانوني v2:");
        foreach (var d in drifts.Take(20))
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  [{d.Id}] {d.Field}\n      expected: {Cut(d.Expected)}\n      actual  : {Cut(d.Actual)}");
        Assert.Fail(sb.ToString());
    }

    [Fact]
    public void EveryV2VectorPassesItsStructuralClaim()
    {
        var problems = GoldenFile.StructuralChecks(GoldenVectorSetV2.All);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void V2VectorIdsAreUnique()
    {
        var dupes = GoldenVectorSetV2.All.GroupBy(v => v.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, "معرّفات مكرّرة: " + string.Join(", ", dupes));
    }

    /// <summary>
    /// المصيدة نفسها، مطبَّقة على v2: كل المتجهات تحت خمس ثقافات عدائية والثابتة.
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("ar-EG")]
    [InlineData("de-DE")]
    [InlineData("fa-IR")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void V2CanonicalBytesAreIdenticalUnderAnyAmbientCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = cultureName.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(cultureName);

            var stored = File.ReadAllText(GoldenPath(GoldenSetIdentity.V2), new UTF8Encoding(false));
            var drifts = GoldenFile.Verify(GoldenSetIdentity.V2, stored, GoldenVectorSetV2.All);
            Assert.True(drifts.Count == 0,
                $"الثقافة {cultureName} غيّرت البايتات القانونية v2: " +
                string.Join("; ", drifts.Take(5).Select(d => $"[{d.Id}] {d.Field}")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>بصمة مخطّط v2 مثبَّتة في ملفها كما هي بصمة v1 في ملفه.</summary>
    [Fact]
    public void V2SchemaFingerprintIsPinnedInItsOwnFile()
    {
        var stored = File.ReadAllText(GoldenPath(GoldenSetIdentity.V2), new UTF8Encoding(false));
        Assert.Contains(JournalEntrySchema.V2.Fingerprint, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(JournalEntrySchema.V1.Fingerprint, stored, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorIdsAreUnique()
    {
        var dupes = GoldenVectorSet.All.GroupBy(v => v.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, "معرّفات مكرّرة: " + string.Join(", ", dupes));
    }

    /// <summary>
    /// المصيدة رقم 3: جهاز واحد بلغة مختلفة يُنتج سلسلة غير قابلة للتحقق بصمت.
    /// مقيس: تحت ar-SA فإن <c>100.5m.ToString("0.0000")</c> يعطي <c>100٫5000</c>،
    /// و<c>DateTime.ToString("d")</c> يعطي تاريخاً هجرياً يحمل بداخله محارف U+200F.
    /// هذا الاختبار يشغّل <b>كل</b> المتجهات تحت أربع ثقافات عدائية.
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("ar-EG")]
    [InlineData("de-DE")]
    [InlineData("fa-IR")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void CanonicalBytesAreIdenticalUnderAnyAmbientCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = cultureName.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(cultureName);

            var stored = File.ReadAllText(GoldenPath(), new UTF8Encoding(false));
            var drifts = GoldenFile.Verify(GoldenSetIdentity.V1, stored, GoldenVectorSet.All);
            Assert.True(drifts.Count == 0,
                $"الثقافة {cultureName} غيّرت البايتات القانونية: " +
                string.Join("; ", drifts.Take(5).Select(d => $"[{d.Id}] {d.Field}")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>حجّة القياس نفسها، مكشوفة: الثقافة العربية تُفسد التحويل غير الصريح.</summary>
    [Fact]
    public void AmbientArabicCultureBreaksNaiveFormattingButNotOurs()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            // التحويل «الساذج» بلا IFormatProvider هو موضوع الاختبار نفسه، لا سهو فيه:
            // الغرض إثبات أن الثقافة المحيطة تُفسده — ولو أُصلح لما بقي شيء يُثبَت،
            // وهو بالضبط ما يجب ألا يقترب من البايتات المُجزَّأة (فخ-18). إسكات موضعي مُعلَّل.
            // CA1305 is deliberate: the culture-aware call IS what this test proves is unsafe.
#pragma warning disable CA1305 // Specify IFormatProvider
            var naive = 100.5m.ToString("0.0000");
            var naiveDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).ToString("d");
#pragma warning restore CA1305

            Assert.DoesNotContain('.', naive);                 // \u200E100٫5000\u200E بفاصلة U+066B
            Assert.Contains('\u200F', naiveDate);              // التاريخ الهجري يحمل RLM داخله

            Assert.Equal("100.5000", Amounts.Render(100.5m));
            Assert.Equal("2026-01-02", Instants.RenderDate(new DateOnly(2026, 1, 2)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    /// <summary>بصمة المخطّط تشمل مجموعة الاستثناء — أي تعديل عليها يُسقط هذا الاختبار.</summary>
    [Fact]
    public void SchemaFingerprintIsFrozen()
    {
        var stored = File.ReadAllText(GoldenPath(), new UTF8Encoding(false));
        Assert.Contains(JournalEntrySchema.V1.Fingerprint, stored, StringComparison.Ordinal);
    }

    private static string Cut(string s)
    {
        var one = s.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
        return one.Length <= 200 ? one : one[..200] + "...";
    }
}
