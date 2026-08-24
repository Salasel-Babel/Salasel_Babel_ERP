using System.Globalization;
using System.Reflection;
using System.Text;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Tests;

/// <summary>اختبارات المواصفة نفسها: القواعد البنيوية التي لا يجوز أن تنكسر.</summary>
public sealed class SpecificationTests
{
    // ═════════════════ الطريق الوحيد إلى دالة التجزئة ═════════════════

    [Fact]
    public void CanonicalizeRejectsAnUnboundDocument()
    {
        var ex = Assert.Throws<CanonicalizationException>(() => Canonicalizer.Canonicalize(Fixtures.Entry()));
        Assert.Equal(CanonErrors.DocumentUnbound, ex.Code);
    }

    [Fact]
    public void SequenceAndPreviousHashAreInsideTheHashedBytes()
    {
        var doc = Fixtures.Entry();
        var link = Canonicalizer.Compute(doc, 7, Fixtures.Genesis);
        var text = link.CanonicalText;

        Assert.Contains("chain_seq\tI\t1\t7\n", text, StringComparison.Ordinal);
        Assert.Contains("prev_hash\tB\t64\t" + Canonicalizer.Hex(Fixtures.Genesis) + "\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangingOnlyTheSequenceChangesTheHash()
    {
        var doc = Fixtures.Entry();
        Assert.NotEqual(
            Canonicalizer.Compute(doc, 1, Fixtures.Genesis).HashHex,
            Canonicalizer.Compute(doc, 2, Fixtures.Genesis).HashHex);
    }

    [Fact]
    public void ChangingOnlyThePreviousHashChangesTheHash()
    {
        var doc = Fixtures.Entry();
        var other = new byte[32];
        other[0] = 1;
        Assert.NotEqual(
            Canonicalizer.Compute(doc, 1, Fixtures.Genesis).HashHex,
            Canonicalizer.Compute(doc, 1, other).HashHex);
    }

    [Fact]
    public void PublicApiExposesExactlyTwoWaysToProduceCanonicalBytes()
    {
        var methods = typeof(Canonicalizer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(byte[]) || m.ReturnType == typeof(ChainLink))
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Canonicalize (بايتات) + Compute (حلقة) + Genesis (بصمة تكوين النطاق)
        Assert.Equal(["Canonicalize", "Compute", "Genesis"], methods);
    }

    // ═════════════════ الترميز والشكل السلكي ═════════════════

    [Fact]
    public void OutputIsUtf8WithoutBom()
    {
        var bytes = Canonicalizer.Compute(Fixtures.Entry(), 1, Fixtures.Genesis).CanonicalBytes;
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal(Encoding.UTF8.GetString(bytes).Normalize(NormalizationForm.FormC), Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void OutputNeverContainsCarriageReturn()
    {
        var bytes = Canonicalizer.Compute(
            Fixtures.Entry(memoAr: "سطر\nسطر آخر"), 1, Fixtures.Genesis).CanonicalBytes;
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void EveryFieldLineCarriesItsUtf8ByteLength()
    {
        var text = Canonicalizer.Compute(Fixtures.Entry(), 1, Fixtures.Genesis).CanonicalText;
        var utf8 = new UTF8Encoding(false);

        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0 || line == Canonicalizer.Magic) continue;
            var parts = line.Split('\t', 4);
            if (parts.Length < 4) continue;
            var declared = int.Parse(parts[2], CultureInfo.InvariantCulture);
            Assert.Equal(declared, utf8.GetByteCount(parts[3]));
        }
    }

    /// <summary>
    /// بيان يحاول تزوير حدود الحقول. الفاصل TAB ممنوع في النص أصلاً، وسابقة الطول
    /// حزام ثانٍ: مستندان مختلفان لا يمكن أن يعطيا البايتات نفسها.
    /// </summary>
    [Fact]
    public void FieldBoundaryInjectionIsImpossible()
    {
        var a = Canonicalizer.Compute(Fixtures.Entry(memoAr: "أ\nmemo T 1 ب"), 1, Fixtures.Genesis);
        var b = Canonicalizer.Compute(Fixtures.Entry(memoAr: "أ", memo: "ب"), 1, Fixtures.Genesis);
        Assert.NotEqual(a.HashHex, b.HashHex);

        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Text("أ\tب"));
        Assert.Equal(CanonErrors.TextControlChar, ex.Code);
    }

    // ═════════════════ ترتيب الحقول ═════════════════

    [Fact]
    public void FieldOrderFollowsTheSchemaNotTheSetCallOrder()
    {
        var text = Canonicalizer.Compute(Fixtures.Entry(), 1, Fixtures.Genesis).CanonicalText;
        var names = text.Split('\n')
            .Where(l => l.Contains('\t', StringComparison.Ordinal))
            .Select(l => l.Split('\t')[0])
            .Where(n => !n.Contains('/', StringComparison.Ordinal))
            .Skip(3)          // kind, chain_seq, prev_hash
            .SkipLast(1)      // end
            .ToList();

        Assert.Equal(JournalEntrySchema.V1.Fields.Select(f => f.Name), names);
    }

    // ═════════════════ مجموعة الاستثناء ═════════════════

    [Fact]
    public void ExclusionSetIsExplicitAndNonEmpty()
    {
        Assert.NotEmpty(JournalEntrySchema.V1.Exclusions);
        Assert.All(JournalEntrySchema.V1.Exclusions, e => Assert.False(string.IsNullOrWhiteSpace(e.RationaleAr)));
    }

    [Fact]
    public void ExclusionSetCoversEveryDocumentedReasonCategory()
    {
        var reasons = JournalEntrySchema.V1.Exclusions.Select(e => e.Reason).Distinct().ToHashSet();
        foreach (var r in Enum.GetValues<ExclusionReason>())
            Assert.Contains(r, reasons);
    }

    [Fact]
    public void ExclusionSetNamesAreFrozen()
    {
        // أي إضافة أو حذف هنا تعديل على المواصفة الموقَّعة، لا تحسين.
        string[] expected =
        [
            "entry_hash", "canon_version",
            "memo_ar_search", "account_name_search", "actor_search", "search_vector",
            "running_balance", "total_debit", "total_credit", "line_count", "account_balance_snapshot",
            "row_version", "db_inserted_at", "updated_at", "outbox_status", "sync_state",
            "zatca_submission_status", "retry_count",
            "client_ip", "user_agent", "session_id", "trace_id",
            "display_order", "printed_count", "attachment_thumbnail"
        ];
        Assert.Equal(expected, JournalEntrySchema.V1.Exclusions.Select(e => e.Name));
    }

    [Fact]
    public void SettingAnExcludedFieldIsRejected()
    {
        var ex = Assert.Throws<CanonicalizationException>(() =>
            JournalEntrySchema.V1.NewDocument().Set("memo_ar_search", CanonicalValue.Text("x")));
        Assert.Equal(CanonErrors.SchemaExcludedField, ex.Code);
    }

    // ═════════════════ الحماية البنيوية لمفتاح البحث ═════════════════

    /// <summary>
    /// المصيدة ع-4: <c>CanonicalValue.Text(SearchKey)</c> موجود ومُعلَّم
    /// <c>[Obsolete(error: true)]</c>، فاستدعاؤه <b>خطأ ترجمة</b> برسالة تشرح السبب.
    /// </summary>
    [Fact]
    public void HashingASearchKeyIsACompileTimeError()
    {
        var overload = typeof(CanonicalValue)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Text" &&
                         m.GetParameters().Length == 1 &&
                         m.GetParameters()[0].ParameterType == typeof(ArabicSearch.SearchKey));

        var obsolete = overload.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(obsolete);
        Assert.True(obsolete!.IsError, "يجب أن يكون خطأ ترجمة لا تحذيراً.");
        Assert.Contains("لا يُجزَّأ مفتاح البحث", obsolete.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchKeyHasNoImplicitConversionToString()
    {
        var conversions = typeof(ArabicSearch.SearchKey)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name is "op_Implicit" or "op_Explicit");
        Assert.Empty(conversions);
    }

    [Fact]
    public void SearchNormalisationFoldsWhatSigningMustKeep()
    {
        string[] variants = ["الرياض", "ألرياض", "إلرياض", "آلرياض", "اـلرياض"];

        var signed = variants
            .Select(v => Canonicalizer.Compute(Fixtures.Entry(memoAr: v), 1, Fixtures.Genesis).HashHex)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(variants.Length, signed.Count);          // خمس قيم موقَّعة مختلفة

        var keys = variants.Select(v => ArabicSearch.Normalize(v).Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Single(keys);                                  // ومفتاح بحث واحد
    }

    // ═════════════════ الإصدارات ═════════════════

    [Fact]
    public void V1IsRegistered() => Assert.Contains("v1", CanonRegistry.Versions);

    [Fact]
    public void UnknownVersionIsRejectedNotAssumed()
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonRegistry.Resolve("v99"));
        Assert.Equal(CanonErrors.ChainUnknownVersion, ex.Code);
    }

    [Fact]
    public void RegisteringADifferentCanonicalizerForAnExistingVersionIsRefused()
        => Assert.Throws<InvalidOperationException>(() => CanonRegistry.Register(new FakeV1()));

    private sealed class FakeV1 : ICanonicalizer
    {
        public string Version => "v1";
        public byte[] Canonicalize(CanonicalDocument document) => [];
    }

    // ═════════════════ بيئة التشغيل ═════════════════

    [Fact]
    public void RuntimeSelfTestPasses()
    {
        var r = CanonicalRuntime.SelfTest();
        Assert.True(r.NfcComposesArabic, "String.Normalize لا يركّب العربية — وضع عولمة ثابتة؟");
        Assert.True(r.IsNormalizedDetectsDecomposed, "IsNormalized يكذب — وضع عولمة ثابتة؟");
        Assert.True(r.InvariantDecimalFormatStable);
        Assert.True(r.Ok);
    }

    // ═════════════════ الحدود والقيم المتطرفة ═════════════════

    [Fact]
    public void EmptyStringAndNullProduceDifferentBytes()
    {
        var a = Canonicalizer.Compute(Fixtures.Entry(memo: ""), 1, Fixtures.Genesis);
        var b = Canonicalizer.Compute(Fixtures.Entry(memo: null), 1, Fixtures.Genesis);
        Assert.NotEqual(a.HashHex, b.HashHex);
        Assert.Contains("memo\tT\t0\t\n", a.CanonicalText, StringComparison.Ordinal);
        Assert.Contains("memo\tN\t0\t\n", b.CanonicalText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public void LargeEntriesCanonicaliseWithoutSurprise(int lines)
    {
        var link = Canonicalizer.Compute(Fixtures.Entry(lineCount: lines), 1, Fixtures.Genesis);
        Assert.Contains($"lines\tG\t{lines.ToString(CultureInfo.InvariantCulture).Length}\t{lines}\n",
            link.CanonicalText, StringComparison.Ordinal);
    }

    [Fact]
    public void GenesisDiffersPerChainScope()
    {
        var a = JournalEntrySchema.Genesis("acme", "MAIN", 2026);
        var b = JournalEntrySchema.Genesis("acme", "MAIN", 2027);
        var c = JournalEntrySchema.Genesis("acme", "SALES", 2026);
        var d = JournalEntrySchema.Genesis("other", "MAIN", 2026);
        Assert.Equal(4, new[] { a, b, c, d }.Select(Canonicalizer.Hex).Distinct(StringComparer.Ordinal).Count());
        Assert.All([a, b, c, d], h => Assert.Equal(32, h.Length));
    }
}
