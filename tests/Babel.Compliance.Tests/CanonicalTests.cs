using System.Text;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// المتجهات الذهبية للتوحيد القياسي. <b>هذه هي الاختبارات التي تكشف أن ترقية مكتبة
/// أو تغيير لغة النظام غيّرا التمثيل — قبل أن يدخل الإنتاج.</b>
/// </summary>
public class CanonicalTests
{
    private static ComplianceDocument Doc(string nameAr, decimal net = 100m, DateTimeOffset? at = null)
    {
        var tax = decimal.Round(net * 0.15m, 4);
        return new ComplianceDocument(
            new ComplianceDocumentId(Guid.Parse("01912345-0000-7000-8000-000000000001")),
            Guid.Parse("01912345-0000-7000-8000-000000000002"),
            new TenantId("acme"), new IssuingUnitId("POS-01"),
            ComplianceDocumentKind.Invoice, ComplianceFlow.Clearance,
            "INV-1", at ?? new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), "SAR",
            new PartyRef(nameAr, "Seller", "300000000000003"),
            null,
            [new DocumentLine(1, "بند", "line", 1m, net, net, 0.15m, tax, net + tax)],
            new DocumentTotals(net, tax, net + tax),
            new JournalEntryRef(Guid.Parse("01912345-0000-7000-8000-000000000003")));
    }

    private static readonly ChainSlot Slot = new(1, ComplianceCanonical.Genesis(new TenantId("acme"), new IssuingUnitId("POS-01")));

    [Fact]
    public void Composed_and_decomposed_Arabic_hash_identically_after_normalisation()
    {
        // «أ» مركّبة U+0623، ومفكّكة U+0627 U+0654 — حرفان مختلفان بايتياً، متطابقان بصرياً.
        const string composed = "أحمد";
        const string decomposed = "أحمد";
        Assert.NotEqual(composed, decomposed);

        var a = ComplianceCanonical.Hash(Doc(ComplianceText.Normalise(composed, "n")), Slot);
        var b = ComplianceCanonical.Hash(Doc(ComplianceText.Normalise(decomposed, "n")), Slot);
        Assert.Equal(a, b);
    }

    [Fact]
    public void An_invisible_RLM_changes_the_hash_unless_it_is_removed_at_the_boundary()
    {
        const string clean = "شركة النور";
        const string withRlm = "شركة‏النور";

        // بلا تطبيع: محرف واحد غير مرئي يغيّر البصمة، وتشخيصه شبه مستحيل.
        Assert.NotEqual(ComplianceCanonical.Hash(Doc(clean.Replace(" ", "")), Slot),
                        ComplianceCanonical.Hash(Doc(withRlm), Slot));

        // مع التطبيع عند الحدّ: يُزال، فتتطابق البصمة.
        Assert.Equal(ComplianceCanonical.Hash(Doc(ComplianceText.Normalise(clean.Replace(" ", ""), "n")), Slot),
                     ComplianceCanonical.Hash(Doc(ComplianceText.Normalise(withRlm, "n")), Slot));
    }

    [Fact]
    public void Arabic_Indic_digits_are_rejected_at_the_boundary_not_silently_converted()
    {
        var ex = Assert.Throws<CanonicalisationException>(() => ComplianceText.Normalise("فاتورة ١٢٣", "number"));
        Assert.Contains("أرقام ASCII فقط", ex.Message);
        Assert.Throws<CanonicalisationException>(() => ComplianceText.Normalise("۱۲۳", "number"));
    }

    [Fact]
    public void Money_renders_at_a_fixed_scale_of_four_in_the_invariant_culture()
    {
        Assert.Equal("100.0000", ComplianceCanonical.Money(100m));
        Assert.Equal("100.0000", ComplianceCanonical.Money(100.00m));
        Assert.Equal("100.0000", ComplianceCanonical.Money(100.0000m));
        Assert.Equal("-0.5000", ComplianceCanonical.Money(-0.5m));
        Assert.Equal("0.0000", ComplianceCanonical.Money(0m));

        // مقياس 2 ومقياس 4 لنفس القيمة يعطيان البصمة نفسها.
        Assert.Equal(ComplianceCanonical.Hash(Doc("بائع", 100.00m), Slot),
                     ComplianceCanonical.Hash(Doc("بائع", 100.0000m), Slot));

        // ومقياس أعلى من القانوني يُرفض بدل أن يُقرَّب بصمت.
        Assert.Throws<CanonicalisationException>(() => ComplianceCanonical.Money(1.00001m));
    }

    [Fact]
    public void Timestamps_are_truncated_to_microseconds_before_hashing_and_before_storing()
    {
        var withTicks = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero).AddTicks(12_345_679);
        var truncated = ComplianceCanonical.PgInstant(withTicks);

        Assert.Equal(0, truncated.Ticks % 10);
        // ذهاب وإياب عبر التمثيل النصّي لا يغيّر شيئاً — وهذا هو المطلوب إثباته.
        Assert.Equal(ComplianceCanonical.Instant(truncated), ComplianceCanonical.Instant(withTicks));
        Assert.Equal(ComplianceCanonical.Hash(Doc("بائع", at: withTicks), Slot),
                     ComplianceCanonical.Hash(Doc("بائع", at: truncated), Slot));
    }

    [Fact]
    public void The_counter_and_the_previous_hash_are_inside_the_hashed_bytes()
    {
        var d = Doc("بائع");
        var atOne = ComplianceCanonical.Hash(d, new ChainSlot(1, Slot.PreviousHash));
        var atTwo = ComplianceCanonical.Hash(d, new ChainSlot(2, Slot.PreviousHash));
        var otherPrev = ComplianceCanonical.Hash(d, new ChainSlot(1, new byte[32]));

        Assert.NotEqual(atOne, atTwo);
        Assert.NotEqual(atOne, otherPrev);

        var rendered = ComplianceCanonical.Render(d, new ChainSlot(7, Slot.PreviousHash));
        Assert.Contains("counter=7", rendered, StringComparison.Ordinal);
        Assert.Contains("prev_hash=" + Convert.ToHexString(Slot.PreviousHash.Span).ToLowerInvariant(),
            rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Genesis_is_scoped_per_issuing_unit_never_globally()
    {
        var a = ComplianceCanonical.Genesis(new TenantId("acme"), new IssuingUnitId("POS-01"));
        var b = ComplianceCanonical.Genesis(new TenantId("acme"), new IssuingUnitId("POS-02"));
        var c = ComplianceCanonical.Genesis(new TenantId("other"), new IssuingUnitId("POS-01"));
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Search_folding_can_never_be_applied_to_a_signed_field()
    {
        var ex = Assert.Throws<NotSupportedException>(() => ComplianceText.SearchFold("الأحمد"));
        Assert.Contains("ع-4", ex.Message);
    }

    /// <summary>
    /// قاعدة الاستبعاد: <b>ثلاث مجموعات بالضبط</b>، والعدّاد والبصمة السابقة <b>ليسا منها</b>.
    /// </summary>
    [Fact]
    public void The_signing_input_excludes_exactly_three_node_sets_and_keeps_the_chain_carriers()
    {
        var renderer = new ProvisionalDocumentRenderer();
        var d = Doc("بائع");
        var rendered = renderer.Render(d, new ChainSlot(42, Slot.PreviousHash));

        Assert.Equal(3, renderer.Extractor.Rule.ExcludedNodeSetCount);
        Assert.Equal(3, renderer.Extractor.LastExcludedNodeSets);

        var body = Encoding.UTF8.GetString(rendered.Body.Span);
        var signed = Encoding.UTF8.GetString(rendered.SigningInput.Span);

        // ما هو مستبعد
        Assert.Contains("<UBLExtensions>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<UBLExtensions>", signed, StringComparison.Ordinal);
        Assert.Contains("<Signature>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<Signature>", signed, StringComparison.Ordinal);
        Assert.Contains(">QR<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">QR<", signed, StringComparison.Ordinal);

        // وما هو **ليس** مستبعداً — وهذا ما يجعل السلسلة رابطة تشفيرياً.
        Assert.Contains(">ICV<", signed, StringComparison.Ordinal);
        Assert.Contains(">42<", signed, StringComparison.Ordinal);
        Assert.Contains(">PIH<", signed, StringComparison.Ordinal);
        Assert.Contains(Convert.ToBase64String(Slot.PreviousHash.Span), signed, StringComparison.Ordinal);
    }

    [Fact]
    public void Changing_the_counter_changes_the_signing_input_bytes()
    {
        var renderer = new ProvisionalDocumentRenderer();
        var d = Doc("بائع");
        var one = renderer.Render(d, new ChainSlot(1, Slot.PreviousHash)).SigningInputDigest.ToArray();
        var two = renderer.Render(d, new ChainSlot(2, Slot.PreviousHash)).SigningInputDigest.ToArray();
        Assert.NotEqual(one, two);
    }

    [Fact]
    public void The_exclusion_rule_lives_in_configuration_so_the_real_spec_changes_one_record()
    {
        // قاعدة بديلة بأسماء أخرى — يجب أن يعمل المُستخرِج كما هو دون تعديل كود.
        var custom = new SigningExclusionRule(["Extensions", "Seal"], "DocRef", "Id", "QRCODE");
        var extractor = new SigningInputExtractor(new DeterministicXmlSerialiser(), custom);

        var xml = new XElement("Doc",
            new XElement("Extensions", "x"),
            new XElement("Seal", "y"),
            new XElement("DocRef", new XElement("Id", "QRCODE"), new XElement("V", "drop-me")),
            new XElement("DocRef", new XElement("Id", "ICV"), new XElement("V", "keep-me")));

        var bytes = Encoding.UTF8.GetString(extractor.Extract(xml));
        Assert.Equal(3, extractor.LastExcludedNodeSets);
        Assert.DoesNotContain("drop-me", bytes, StringComparison.Ordinal);
        Assert.Contains("keep-me", bytes, StringComparison.Ordinal);
    }
}
