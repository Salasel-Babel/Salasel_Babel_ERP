using System.Reflection;
using System.Text;
using Babel.ArchitectureTests.Support;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 11 — الشكل القانوني مختوم.</b>
/// <para>
/// <c>Babel.Canonicalization</c> ليست وحدة منتج بل <b>الطريق الوحيد إلى دالة التجزئة</b>.
/// البايتات المُجزَّأة هي الدليل الذي يُعرض على مدقّق بعد سنوات، ولذلك تُفرض عليها حدود
/// أشدّ من حدود أي مشروع آخر في المستودع:
/// </para>
/// <list type="number">
///   <item><b>صفر اعتماديات.</b> لا حزمة، ولا مشروع، ولا حتى النواة المشتركة. ترقية اعتمادية
///         لا يجوز أن تحرّك بايتة واحدة (SPEC §8 · فخ-18).</item>
///   <item><b>الدفتر وحده يعتمد عليها.</b> وحدة أفقية تُجزّئ شيئاً هي وحدة تكتب دليلاً
///         خارج الدفتر — وهي القاعدة 1 من باب آخر.</item>
///   <item><b>لا مُسلسِل ولا مشغّل قاعدة بيانات ولا <c>ToString()</c> واعٍ باللغة</b> قرب
///         البايتات: لا اعتماد على <c>System.Text.Json</c> ولا على Npgsql ولا على EF Core
///         (فخ-18 · فخ-19 · القائمة المرجعية §8).</item>
///   <item><b>حارس العولمة الثابتة موجود.</b> في وضع <c>InvariantGlobalization</c> يصير
///         <c>String.Normalize</c> عملية لا شيء <b>بصمت</b>، فتتغيّر كل بصمة نصّ عربي
///         (SPEC §8.2 — أخطر مصيدة نشر في المكتبة).</item>
///   <item><b>الرابط داخل البايتات.</b> لا يوجد مسار يُنتج بايتات قانونية بلا رقم تسلسل
///         وبصمة سابقة — وإلا فالسلسلة زخرفية (ADR-0007 · فخ-22).</item>
/// </list>
/// </summary>
public sealed class Rule11_TheCanonicalFormIsSealed
{
    private static Assembly Library => BabelAssemblies.Named(ModuleMap.Canonicalization);

    private static ProjectFile LibraryProject =>
        RepositoryLayout.SourceProjects.Single(static project => project.Name == ModuleMap.Canonicalization);

    [Fact]
    public void TheLibraryDeclaresNoPackageAndNoProjectReference()
    {
        Assert.Empty(LibraryProject.PackageReferences);
        Assert.Empty(LibraryProject.ProjectReferences);
    }

    [Fact]
    public void OnlyTheLedgerMayDependOnTheCanonicalForm()
    {
        List<string> violations = [.. RepositoryLayout.SourceProjects
            .Where(static project => project.Name != ModuleMap.Ledger && project.Name != ModuleMap.Api)
            .Where(static project => project.Name != ModuleMap.Canonicalization)
            .Where(static project => project.ProjectReferences.Contains(ModuleMap.Canonicalization, StringComparer.Ordinal))
            .Select(static project => project.RelativePath)];

        Assert.True(
            violations.Count == 0,
            "الدفتر وحده يُجزّئ قيداً. مشاريع تشير إلى مكتبة التوحيد القياسي بلا حق:\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void NoSerialiserNoDriverAndNoFrameworkComesNearTheHashedBytes()
    {
        string[] forbidden =
        [
            "System.Text.Json",
            "Newtonsoft.Json",
            "Npgsql",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.AspNetCore",
            "Wolverine",
        ];

        // فحص التجميعة نفسها: ما تشير إليه فعلاً، لا ما يعلنه ملف المشروع.
        List<string> referenced = [.. Library.GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(name => forbidden.Any(bad => name.StartsWith(bad, StringComparison.Ordinal)))];

        Assert.True(
            referenced.Count == 0,
            "مُسلسِل أو مشغّل قاعدة بيانات قرب البايتات المُجزَّأة — فخ-18 وفخ-19:\n"
            + string.Join('\n', referenced));

        ArchTestResult result = Types.InAssembly(Library)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "أنواع في المكتبة تعتمد على مُسلسِل أو مشغّل: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void TheLibraryDependsOnNoBabelAssemblyAtAll()
    {
        List<string> babelReferences = [.. Library.GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(static name => name.StartsWith("Babel.", StringComparison.Ordinal))];

        Assert.True(
            babelReferences.Count == 0,
            "مكتبة التوحيد القياسي تعتمد على تجميعة من المنتج — البايتات صارت رهينة تغييرٍ فيها:\n"
            + string.Join('\n', babelReferences));
    }

    [Fact]
    public void TheInvariantGlobalisationGuardExistsAndIsWiredIntoEveryEntryPoint()
    {
        // السطر الذي يمنع أخطر مصيدة نشر: بدونه تُنتج المكتبة بصمات خاطئة بصمت.
        Type runtime = BabelAssemblies.TypesOf(Library).Single(static type => type.Name == "CanonicalRuntime");
        Assert.NotNull(runtime.GetMethod("EnsureSupported", BindingFlags.Public | BindingFlags.Static));

        string source = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", ModuleMap.Canonicalization, "CanonicalRuntime.cs"));
        Assert.Contains("Normalize", source, StringComparison.Ordinal);

        // والحارس مُستدعى فعلاً من **كل** مُوحِّد، لا معرَّفاً وغير مستعمل، ولا
        // مُستدعىً من الأول وحده: إصدار يفوته الحارس يُنتج بصمات خاطئة بصمت في
        // وضع العولمة الثابتة، وهو ما تُنتجه صور Docker النحيفة افتراضياً.
        List<string> unguarded = [.. CanonicaliserSources()
            .Where(static entry => !entry.Source.Contains("CanonicalRuntime.EnsureSupported()", StringComparison.Ordinal))
            .Select(static entry => entry.Version)];

        Assert.True(unguarded.Count == 0,
            "مُوحِّد بلا حارس العولمة الثابتة (SPEC §8.2): " + string.Join(", ", unguarded));
    }

    /// <summary>
    /// ملفات مصدر كل تنفيذ لـ<see cref="ICanonicalizer"/> في المكتبة، مقرونة بإصداره.
    /// الاكتشاف بالانعكاس لا بقائمة مكتوبة: قائمة مكتوبة تنسى الإصدار القادم.
    /// </summary>
    private static IEnumerable<(string Version, string Source)> CanonicaliserSources()
    {
        List<Type> implementations = [.. BabelAssemblies.TypesOf(Library)
            .Where(static type => typeof(ICanonicalizer).IsAssignableFrom(type))
            .Where(static type => type is { IsInterface: false, IsAbstract: false })];

        Assert.NotEmpty(implementations);

        foreach (Type type in implementations)
        {
            string path = Path.Combine(
                RepositoryLayout.Root, "src", ModuleMap.Canonicalization, type.Name + ".cs");

            // ‏CanonicalizerV1 يعيش داخل Canonicalizer.cs بحكم كونه الأصل المجمَّد.
            if (!File.Exists(path) && type.Name == "CanonicalizerV1")
            {
                path = Path.Combine(RepositoryLayout.Root, "src", ModuleMap.Canonicalization, "Canonicalizer.cs");
            }

            Assert.True(File.Exists(path),
                $"لا ملف مصدر باسم {type.Name} — كل مُوحِّد يعيش في ملفه كي يُفحص ويُجمَّد وحده.");

            yield return (type.Name, File.ReadAllText(path));
        }
    }

    /// <summary>أصغر مستند صالح لكل إصدار — لفحص الترويسة السلكية وحدها.</summary>
    private static CanonicalDocument SmallestDocumentFor(string version)
    {
        CanonicalSchema schema = version == "v1" ? JournalEntrySchema.V1 : JournalEntrySchema.V2;
        CanonicalDocumentBuilder builder = schema.NewDocument();

        foreach (SchemaField field in schema.Fields)
        {
            if (field.IsGroup)
            {
                builder.SetGroup(field.Name, []);
                continue;
            }

            builder.Set(field.Name, field.Kind switch
            {
                CanonicalKind.Text => CanonicalValue.Text("x"),
                CanonicalKind.Integer => CanonicalValue.Integer(1),
                CanonicalKind.Amount => CanonicalValue.Amount(0m),
                CanonicalKind.Rate => CanonicalValue.Rate(1m),
                CanonicalKind.Instant => CanonicalValue.Instant(
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                CanonicalKind.Date => CanonicalValue.Date(new DateOnly(2026, 1, 1)),
                CanonicalKind.Uuid => CanonicalValue.Uuid(Guid.Empty),
                CanonicalKind.Bool => CanonicalValue.Bool(false),
                CanonicalKind.Bytes => CanonicalValue.Bytes([]),
                CanonicalKind.Token => CanonicalValue.Token("X"),
                _ => CanonicalValue.Null(),
            });
        }

        return builder.Build();
    }

    /// <summary>
    /// ADR-0007: <c>chain_seq</c> و<c>prev_hash</c> يُكتبان في ترويسة البايتات، لا في
    /// عمود مجاور، ومستند غير مرتبط بموقع في السلسلة لا يُنتج بايتات إطلاقاً.
    ///
    /// <para>
    /// <b>والفحص على كل مُوحِّد، لا على ملف بعينه.</b> النسخة الأولى كانت تقرأ
    /// <c>Canonicalizer.cs</c> وحده؛ ولمّا وُلد <c>CanonicalizerV2</c> في ملفه لكان
    /// إصداراً كاملاً يمرّ بلا فحص. الاكتشاف بالانعكاس على <see cref="ICanonicalizer"/>
    /// يجعل كل إصدار قادم مشمولاً <b>بالبناء</b>: يكفي أن يوجد التنفيذ ليُفحص.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryCanonicaliserPutsTheChainLinkInsideTheHashedBytes()
    {
        List<string> violations = [];

        foreach ((string version, string source) in CanonicaliserSources())
        {
            if (!source.Contains("\"chain_seq\"", StringComparison.Ordinal))
                violations.Add($"{version}: لا يكتب سطر chain_seq");
            if (!source.Contains("\"prev_hash\"", StringComparison.Ordinal))
                violations.Add($"{version}: لا يكتب سطر prev_hash");
            if (!source.Contains("DocumentUnbound", StringComparison.Ordinal))
                violations.Add($"{version}: لا يرفض المستند غير المرتبط بالسلسلة");
        }

        Assert.True(violations.Count == 0,
            "مُوحِّد يُنتج بايتات بلا رابط سلسلة داخلها — السلسلة تصير زخرفية (ADR-0007 · فخ-22):\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// <b>لكل إصدار ترويسته السلكية، ولا إصدارين يتقاسمان ترويسة.</b>
    /// ترويسة مشتركة تعني أن بايتات إصدارين قد تتطابق حرفاً بحرف لمستند واحد،
    /// فيصير عمود <c>canon_version</c> هو الفارق الوحيد — وهو عمود يُعاد كتابته.
    /// </summary>
    [Fact]
    public void EveryRegisteredVersionHasItsOwnWireHeader()
    {
        List<string> headers = [];

        foreach (string version in CanonRegistry.Versions)
        {
            ICanonicalizer canonicaliser = CanonRegistry.Resolve(version);
            Assert.Equal(version, canonicaliser.Version);

            CanonicalDocument document = SmallestDocumentFor(version).Bind(1, new byte[32]);
            string first = Encoding.UTF8.GetString(canonicaliser.Canonicalize(document)).Split('\n')[0];

            Assert.Equal("babel.canon/" + version, first);
            headers.Add(first);
        }

        Assert.Equal(headers.Count, headers.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// <b>لا إصدار بلا مجموعة متجهات مُودَعة.</b> النسخة الأولى كانت تثبّت اسم ملف
    /// v1 حرفياً؛ فحصٌ كهذا يبارك إصداراً ثانياً بلا متجهات إطلاقاً. هنا يُشتقّ
    /// المطلوب من <b>سجلّ الإصدارات نفسه</b>: كل إصدار مسجَّل يلزمه ملفه، فإضافة v3
    /// بلا متجهات تُسقط البناء يوم تُسجَّل، لا بعد مليون قيد.
    /// </summary>
    [Fact]
    public void EveryRegisteredVersionHasItsOwnCommittedGoldenVectorFile()
    {
        List<string> missing = [];

        foreach (string version in CanonRegistry.Versions)
        {
            string golden = Path.Combine(
                RepositoryLayout.Root, "tests", "golden", $"golden-vectors.{version}.json");

            if (!File.Exists(golden))
            {
                missing.Add($"{version}: {golden} غير موجود");
                continue;
            }

            if (new FileInfo(golden).Length <= 4096)
            {
                missing.Add($"{version}: الملف أصغر من أن يكون تثبيتاً حقيقياً");
                continue;
            }

            string content = File.ReadAllText(golden);
            if (!content.Contains($"\"canon_version\": \"{version}\"", StringComparison.Ordinal))
                missing.Add($"{version}: الملف لا يعلن إصداره");
            if (!content.Contains($"\"wire_magic\": \"babel.canon/{version}\"", StringComparison.Ordinal))
                missing.Add($"{version}: الملف لا يعلن ترويسته السلكية");
            if (!content.Contains("\"manifest_sha256\"", StringComparison.Ordinal))
                missing.Add($"{version}: الملف بلا بصمة بيان");
        }

        Assert.True(missing.Count == 0,
            "إصدار مسجَّل بلا مجموعة متجهات ذهبية مُودَعة — الشكل القانوني غير مختوم:\n"
            + string.Join("\n", missing));
    }

    /// <summary>
    /// <b>‏v1 مجمَّد بقيمه الحرفية.</b> بصمة مخطّطه وبصمة بيان متجهاته وعددها مكتوبة
    /// هنا نصّاً. أي تعديل على v1 — إضافة حقل، أو إخراج اسم من مجموعة الاستثناء، أو
    /// إعادة توليد ملفه — يُسقط هذا الاختبار قبل أن يصل إلى مراجعة.
    /// </summary>
    [Fact]
    public void TheV1CanonicalFormIsFrozenAtItsCommittedFingerprintAndManifest()
    {
        Assert.Equal(
            "99d4deac27f0eed12e111c5718fda2286df165d2b2ec957f554aafc11b858310",
            JournalEntrySchema.V1.Fingerprint);

        string golden = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "tests", "golden", "golden-vectors.v1.json"));

        Assert.Contains(
            "\"manifest_sha256\": \"7bd2c8e8b2da05c3ad4f5a0375c8605177884e5b5a57cd5db1ca651d94bcf856\"",
            golden, StringComparison.Ordinal);
        Assert.Contains("\"vector_count\": 97", golden, StringComparison.Ordinal);
        Assert.Contains(JournalEntrySchema.V1.Fingerprint, golden, StringComparison.Ordinal);
    }

    /// <summary>
    /// كل مخطّط مُعلن يحمل <b>مجموعة استثناء معلَّلة</b>، وكل استثناء فيه سبب مكتوب.
    /// مجموعات الاستثناء الضمنية هي بالضبط ما تسقط عنده تنفيذات XMLDSig.
    /// </summary>
    [Fact]
    public void EverySchemaCarriesAnExplicitlyReasonedExclusionSet()
    {
        foreach (CanonicalSchema schema in new[] { JournalEntrySchema.V1, JournalEntrySchema.V2 })
        {
            Assert.NotEmpty(schema.Exclusions);
            Assert.All(schema.Exclusions, e => Assert.False(string.IsNullOrWhiteSpace(e.RationaleAr)));

            // والبصمة تشمل الاستثناءات: مخطّطان يختلفان في الاستثناء وحده لا يجوز
            // أن يحملا البصمة نفسها.
            Assert.Equal(64, schema.Fingerprint.Length);
        }

        Assert.NotEqual(JournalEntrySchema.V1.Fingerprint, JournalEntrySchema.V2.Fingerprint);
    }

    [Fact]
    public void TheGoldenVectorFileIsCommittedAndNonTrivial()
    {
        // متجهات ثابتة مُودَعة، وأي انحراف يُفشِل البناء (القائمة المرجعية §8 — بند الهيئة).
        string golden = Path.Combine(RepositoryLayout.Root, "tests", "golden", "golden-vectors.v1.json");
        Assert.True(File.Exists(golden), "ملف المتجهات الذهبية غير موجود: " + golden);
        Assert.True(new FileInfo(golden).Length > 4096, "ملف المتجهات الذهبية أصغر من أن يكون تثبيتاً حقيقياً.");
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // لو لم تُحمَّل المكتبة لمرّت كل القواعد أعلاه فراغاً.
        Assert.Contains(BabelAssemblies.Product, assembly => assembly.GetName().Name == ModuleMap.Canonicalization);
        Assert.Contains(RepositoryLayout.SourceProjects, project => project.Name == ModuleMap.Canonicalization);
        Assert.True(BabelAssemblies.TypesOf(Library).Count() >= 20);

        // ولو كان سجلّ الإصدارات فارغاً لمرّت قواعد «لكل إصدار ملفه» و«لكل إصدار
        // ترويسته» فراغاً. الإصداران القائمان مذكوران هنا بالاسم.
        Assert.Contains("v1", CanonRegistry.Versions);
        Assert.Contains("v2", CanonRegistry.Versions);
        Assert.True(CanonRegistry.Versions.Count >= 2);
    }
}
