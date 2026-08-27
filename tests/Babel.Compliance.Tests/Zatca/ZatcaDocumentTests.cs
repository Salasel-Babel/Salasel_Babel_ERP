using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Chain;
using Babel.Compliance.Zatca.Documents;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// المستند وقاعدة الاستبعاد والسلسلة والمسارَان المتعاكسان.
/// </summary>
public sealed class ZatcaDocumentTests(ITestOutputHelper output)
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static ZatcaDocumentRenderer Renderer => new(ZatcaFixtures.Seller);

    // ── قاعدة الاستبعاد ─────────────────────────────────────────────────────

    [Fact]
    public void The_signing_transform_removes_exactly_three_node_sets_and_touches_neither_the_counter_nor_the_previous_hash()
    {
        RenderedDocument rendered = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2));
        XElement tree = ZatcaDocumentRenderer.Parse(rendered.Body.Span);

        SigningTransformResult result = Renderer.Transform.Apply(tree);

        Assert.Equal(3, result.ExcludedNodeSets);

        string signed = Utf8.GetString(result.Canonical);
        output.WriteLine("عدد المجموعات المُزالة: " + result.ExcludedNodeSets.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("عدد العقد المُزالة: " + result.RemovedNodes.ToString(CultureInfo.InvariantCulture));

        // المُزال فعلاً مُزال.
        Assert.DoesNotContain("UBLExtensions", signed, StringComparison.Ordinal);
        Assert.DoesNotContain(">QR<", signed, StringComparison.Ordinal);

        // والباقي باقٍ: العدّاد والبصمة السابقة داخل البايتات الموقَّعة.
        Assert.Contains(">ICV<", signed, StringComparison.Ordinal);
        Assert.Contains(">PIH<", signed, StringComparison.Ordinal);
        Assert.Contains(ZatcaChain.PreviousInvoiceHash(ZatcaFixtures.Slot(2)), signed, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>إثبات لافراغ القاعدة:</b> تُحقن مخالفة حقيقية — يُنزع مرجع QR من المستند —
    /// فيسقط الحارس. قاعدةٌ لم يُر سقوطها لا يُعرف أنها تعمل.
    /// </summary>
    [Fact]
    public void The_exclusion_guard_actually_fires_when_a_node_set_is_missing()
    {
        RenderedDocument rendered = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2));
        XElement tree = ZatcaDocumentRenderer.Parse(rendered.Body.Span);

        // المخالفة: يُنزع مرجع QR كأنه لم يُبنَ أصلاً.
        XElement qr = tree.Descendants(ZatcaProfile.Cac + "AdditionalDocumentReference")
            .First(r => r.Element(ZatcaProfile.Cbc + "ID")?.Value == "QR");
        qr.Remove();

        ZatcaCanonicalisationException error =
            Assert.Throws<ZatcaCanonicalisationException>(() => Renderer.Transform.Apply(tree));

        output.WriteLine("الحارس أطلق: " + error.Message);
        Assert.Contains("2 مجموعة والمطلوب 3", error.Message, StringComparison.Ordinal);

        // وبلا الحارس كان التحويل سيمرّ ويُنتج بصمة «صالحة» على مستند ناقص.
        SigningTransformResult without = Renderer.Transform.Apply(tree, requireExactlyThree: false);
        output.WriteLine("وبلا الحارس: أُزيلت " + without.ExcludedNodeSets.ToString(CultureInfo.InvariantCulture)
            + " مجموعة، والبصمة تُحسب على " + without.Canonical.Length.ToString(CultureInfo.InvariantCulture) + " بايتاً");
        Assert.Equal(2, without.ExcludedNodeSets);
    }

    // ── السلسلة ─────────────────────────────────────────────────────────────

    [Fact]
    public void The_counter_and_the_previous_hash_are_inside_the_hashed_bytes_not_beside_them()
    {
        byte[] atTwo = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).SigningInputDigest.ToArray();
        byte[] atThree = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(3)).SigningInputDigest.ToArray();
        byte[] otherPrevious = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2, 0xCD)).SigningInputDigest.ToArray();

        output.WriteLine("العدّاد 2: " + Convert.ToHexString(atTwo));
        output.WriteLine("العدّاد 3: " + Convert.ToHexString(atThree));
        output.WriteLine("بصمة سابقة أخرى: " + Convert.ToHexString(otherPrevious));

        Assert.False(atTwo.AsSpan().SequenceEqual(atThree),
            "تغيير العدّاد لم يغيّر البصمة: السلسلة زخرفية، والرابط خارج البايتات المُجزَّأة");
        Assert.False(atTwo.AsSpan().SequenceEqual(otherPrevious),
            "تغيير البصمة السابقة لم يغيّر البصمة: السلسلة زخرفية");
    }

    [Fact]
    public void The_first_document_writes_the_published_seed_and_later_documents_write_the_previous_hash()
    {
        Assert.Equal(ZatcaChain.GenesisPreviousInvoiceHash, ZatcaChain.PreviousInvoiceHash(ZatcaFixtures.Slot(1)));

        ChainSlot second = ZatcaFixtures.Slot(2);
        Assert.Equal(Convert.ToBase64String(second.PreviousHash.Span), ZatcaChain.PreviousInvoiceHash(second));

        output.WriteLine("PIH عند العدّاد 1: " + ZatcaChain.PreviousInvoiceHash(ZatcaFixtures.Slot(1)));
        output.WriteLine("PIH عند العدّاد 2: " + ZatcaChain.PreviousInvoiceHash(second));

        // عدّاد صفر أو سالب ليس خانة سلسلة، وهو عطل بناء لا حالة تشغيل.
        Assert.Throws<ArgumentOutOfRangeException>(() => ZatcaChain.PreviousInvoiceHash(new ChainSlot(0, new byte[32])));
    }

    [Fact]
    public void Our_chain_is_the_authority_chain_not_a_second_one_beside_it()
    {
        RenderedDocument rendered = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2));

        Assert.True(rendered.DomainChainDigest.Span.SequenceEqual(rendered.SigningInputDigest.Span),
            "سلسلة ثانية مستقلة تعني سلسلة تتحقّق عندنا وليست السلسلة التي تفحصها الجهة");

        output.WriteLine("بصمة السلسلة = بصمة الفاتورة = " + Convert.ToHexString(rendered.DomainChainDigest.Span));
    }

    // ── المساران المتعاكسان ─────────────────────────────────────────────────

    [Fact]
    public void A_buyer_with_a_tax_number_takes_the_blocking_clearance_path_and_says_so_in_its_own_bytes()
    {
        ZatcaFlowPolicy policy = new();
        ComplianceDocument standard = ZatcaFixtures.Standard();

        ComplianceFlow flow = policy.FlowFor(standard.Kind, standard.Buyer, standard.Totals);
        Assert.Equal(ComplianceFlow.Clearance, flow);
        Assert.True(ZatcaFlowPolicy.RequiresBlockingResponse(flow));

        string name = TypeName(standard);
        output.WriteLine("السمة name للقياسية: " + name);
        Assert.StartsWith("01", name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_buyer_without_a_tax_number_takes_the_after_the_fact_reporting_path_and_says_so_in_its_own_bytes()
    {
        ZatcaFlowPolicy policy = new();
        ComplianceDocument simplified = ZatcaFixtures.Simplified();

        ComplianceFlow flow = policy.FlowFor(simplified.Kind, simplified.Buyer, simplified.Totals);
        Assert.Equal(ComplianceFlow.Reporting, flow);
        Assert.False(ZatcaFlowPolicy.RequiresBlockingResponse(flow));

        string name = TypeName(simplified);
        output.WriteLine("السمة name للمبسّطة: " + name);
        Assert.StartsWith("02", name, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>المفصل الذي يمنع فخّ خلط المسارين:</b> السمة داخل المستند والمسار الذي يسلكه
    /// يشتقّان من <b>القيمة نفسها</b>، فلا يمكن أن يفترقا. الاختبار يُثبت التلازم على
    /// كل قيمة في التعداد، لا على مثال.
    /// </summary>
    [Theory]
    [InlineData(ComplianceFlow.Clearance, "01")]
    [InlineData(ComplianceFlow.Reporting, "02")]
    public void The_flow_and_the_two_digits_inside_the_document_cannot_disagree(ComplianceFlow flow, string expected)
    {
        string name = ZatcaProfile.TypeNameOf(flow, InvoiceTraits.None);
        Assert.StartsWith(expected, name, StringComparison.Ordinal);
        Assert.Equal(7, name.Length);
    }

    // ── الأرقام ─────────────────────────────────────────────────────────────

    [Fact]
    public void An_amount_that_does_not_fit_two_decimals_is_refused_not_rounded()
    {
        // 4 خانات تسع في مقياس النظام ولا تسع في المستند. الرفض مقصود.
        ZatcaDocumentException error = Assert.Throws<ZatcaDocumentException>(
            () => ZatcaAmounts.Render(1000.5050m, "line_net"));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("التقريب قرار محاسبي", error.Message, StringComparison.Ordinal);

        // وما يسع يُكتب بلا تشويه، مهما كان مقياسه الداخلي.
        Assert.Equal("1000.50", ZatcaAmounts.Render(1000.5000m, "t"));
        Assert.Equal("1000.50", ZatcaAmounts.Render(1000.5m, "t"));
    }

    [Fact]
    public void A_credit_note_without_an_Arabic_reason_is_refused()
    {
        ComplianceDocument note = ZatcaFixtures.Standard() with
        {
            Kind = ComplianceDocumentKind.CreditNote,
            OriginalDocument = ZatcaFixtures.StandardId,
            CorrectionReasonAr = null
        };

        ZatcaDocumentException error = Assert.Throws<ZatcaDocumentException>(
            () => Renderer.Render(note, ZatcaFixtures.Slot(2)));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("سبب تصحيح", error.Message, StringComparison.Ordinal);
    }

    // ── التوحيد القياسي ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>إثبات لافراغ حارس النطاق:</b> تُحقن سمة في مساحة <c>xml:</c> — وهي المساحة
    /// الوحيدة التي يختلف فيها C14N 1.1 عن التنفيذ المتاح — فيسقط التوحيد القياسي.
    /// </summary>
    [Fact]
    public void Canonicalisation_refuses_a_tree_that_leaves_the_range_where_the_two_C14N_versions_agree()
    {
        XElement tree = XElement.Parse("<Root xmlns=\"urn:x\"><Child/></Root>");
        tree.Element(XName.Get("Child", "urn:x"))!.Add(new XAttribute(XNamespace.Xml + "base", "urn:example"));

        ZatcaCanonicalisationException error =
            Assert.Throws<ZatcaCanonicalisationException>(() => new ZatcaCanonicalXml().Canonicalise(tree));

        output.WriteLine("الحارس أطلق: " + error.Message);
        Assert.Contains("xml:", error.Message, StringComparison.Ordinal);

        // وبلا السمة يمرّ التوحيد القياسي: الحارس يمنع الخارج عن النطاق لا كل شيء.
        Assert.NotEmpty(new ZatcaCanonicalXml().Canonicalise(XElement.Parse("<Root xmlns=\"urn:x\"><Child/></Root>")));
    }

    [Fact]
    public void Canonicalising_a_subtree_carries_its_ancestors_namespace_declarations()
    {
        ZatcaCanonicalXml canonicaliser = new();
        XNamespace outerNs = "urn:example:outer";
        XNamespace innerNs = "urn:example:inner";

        XElement inner = new(innerNs + "Inner", "قيمة");
        XElement outer = new(outerNs + "Outer",
            new XAttribute(XNamespace.Xmlns + "o", outerNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "i", innerNs.NamespaceName),
            inner);
        _ = outer;

        string inPlace = Utf8.GetString(canonicaliser.CanonicaliseInScope(inner));
        string detached = Utf8.GetString(canonicaliser.Canonicalise(new XElement(inner)));

        output.WriteLine("في موضعه : " + inPlace);
        output.WriteLine("مقتطعاً  : " + detached);
        output.WriteLine("طول «في موضعه» بالبايت: "
            + Utf8.GetByteCount(inPlace).ToString(CultureInfo.InvariantCulture));
        output.WriteLine("طول «مقتطعاً» بالبايت : "
            + Utf8.GetByteCount(detached).ToString(CultureInfo.InvariantCulture));

        Assert.Contains("xmlns:o=", inPlace, StringComparison.Ordinal);
        Assert.DoesNotContain("xmlns:o=", detached, StringComparison.Ordinal);
        Assert.NotEqual(inPlace, detached);
    }

    private static string TypeName(ComplianceDocument document)
    {
        XElement tree = ZatcaDocumentRenderer.Parse(
            new ZatcaDocumentRenderer(ZatcaFixtures.Seller).Render(document, ZatcaFixtures.Slot(2)).Body.Span);
        return tree.Element(ZatcaProfile.Cbc + "InvoiceTypeCode")!.Attribute("name")!.Value;
    }
}
