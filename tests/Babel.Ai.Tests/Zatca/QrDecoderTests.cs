using System.Globalization;
using System.Text;
using System.Text.Json;
using Babel.Ai.Tests.Support;
using Babel.Compliance.Zatca.Qr;
using Xunit;

namespace Babel.Ai.Tests.Zatca;

/// <summary>
/// <b>فاكّ رمز الاستجابة السريعة — عكس المُرمِّز، لا قارئ ثانٍ مستقلّ.</b>
/// <para>
/// وهذا هو الفرق الذي يجعل الاختبار ذا معنى: المتجهات المقبولة <b>مولَّدة بالمُرمِّز
/// القائم</b>، فمتى انحرف أحدهما عن الآخر سقط الاختبار. وقارئ مكتوب من الصفر يتفق مع
/// نفسه ولا يُثبت شيئاً.
/// </para>
/// </summary>
public sealed class QrDecoderTests(ITestOutputHelper output)
{
    private static readonly string GoldenPath = RepositoryRoot.At("tests/Babel.Ai.Tests/golden/qr-decoder-vectors.v1.json");

    private static readonly string ComplianceGoldenPath = RepositoryRoot.At("tests/golden/zatca-vectors.v1.json");

    // ── المتجهات الذهبية ────────────────────────────────────────────────────

    /// <summary>كل متجه مقبول يُفكّ إلى الحقول المُودَعة بالضبط.</summary>
    [Fact]
    public void Every_accepted_golden_vector_decodes_to_its_recorded_fields()
    {
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(GoldenPath));
        int seen = 0;

        foreach (JsonElement vector in golden.RootElement.GetProperty("accepted").EnumerateArray())
        {
            seen++;
            string id = vector.GetProperty("id").GetString()!;
            ZatcaQrContents contents = ZatcaQrReader.Read(vector.GetProperty("payload").GetString()!);

            output.WriteLine("متجه: " + id);
            output.WriteLine("  المرحلة        : " + contents.Phase.ToString());
            output.WriteLine("  البائع         : " + contents.SellerName);
            output.WriteLine("  الرقم الضريبي  : " + contents.SellerVatNumber);
            output.WriteLine("  الطابع الزمني  : " + contents.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            output.WriteLine("  الإجمالي       : " + contents.GrossTotal.ToString("0.00", CultureInfo.InvariantCulture));
            output.WriteLine("  الضريبة        : " + contents.TaxTotal.ToString("0.00", CultureInfo.InvariantCulture));
            output.WriteLine("  أطوال الوسوم   : " + Lengths(contents));

            Assert.Equal(vector.GetProperty("phase").GetString(), contents.Phase.ToString());
            Assert.Equal(vector.GetProperty("seller_name").GetString(), contents.SellerName);
            Assert.Equal(vector.GetProperty("seller_vat_number").GetString(), contents.SellerVatNumber);
            Assert.Equal(
                vector.GetProperty("issued_at").GetString(),
                contents.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            Assert.Equal(
                decimal.Parse(vector.GetProperty("gross_total").GetString()!, CultureInfo.InvariantCulture),
                contents.GrossTotal);
            Assert.Equal(
                decimal.Parse(vector.GetProperty("tax_total").GetString()!, CultureInfo.InvariantCulture),
                contents.TaxTotal);
            Assert.Equal(vector.GetProperty("tag_lengths").GetString(), Lengths(contents));

            if (vector.TryGetProperty("invoice_hash_base64", out JsonElement hash))
            {
                Assert.Equal(hash.GetString(), contents.InvoiceHashBase64);
                Assert.Equal(vector.GetProperty("signature_base64").GetString(), contents.SignatureBase64);
                Assert.Equal(vector.GetProperty("public_key_hex").GetString(), Hex(contents.PublicKey));
                Assert.Equal(vector.GetProperty("certificate_signature_hex").GetString(), Hex(contents.CertificateSignature));
            }
        }

        Assert.True(seen >= 3, "قُرئت " + seen.ToString(CultureInfo.InvariantCulture) + " متجهات فقط — المسح ضامر والاختبار يمرّ فارغاً");
    }

    /// <summary>كل متجه مرفوض يُرفض، و<b>بالرسالة التي تسمّي العطل</b> لا برسالة عامة.</summary>
    [Fact]
    public void Every_refused_golden_vector_is_refused_by_the_message_that_names_the_fault()
    {
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(GoldenPath));
        int seen = 0;

        foreach (JsonElement vector in golden.RootElement.GetProperty("refused").EnumerateArray())
        {
            seen++;
            string id = vector.GetProperty("id").GetString()!;
            string payload = vector.GetProperty("payload").GetString()!;
            string mustSay = vector.GetProperty("must_say").GetString()!;

            ZatcaQrException error = Assert.Throws<ZatcaQrException>(() => ZatcaQrReader.Read(payload));

            output.WriteLine("متجه مرفوض: " + id);
            output.WriteLine("  " + error.Message);

            Assert.Contains(mustSay, error.Message, StringComparison.Ordinal);
        }

        Assert.True(seen >= 7, "قُرئت " + seen.ToString(CultureInfo.InvariantCulture) + " متجهات رفض فقط — المسح ضامر");
    }

    // ── الذهاب والإياب مع المُرمِّز القائم ──────────────────────────────────

    /// <summary>ما يُرمّزه المُرمِّز يعود منه حرفياً — المرحلة الأولى.</summary>
    [Fact]
    public void What_the_existing_encoder_writes_the_reader_returns_unchanged()
    {
        string encoded = ZatcaQr.Phase1(CaptureHarness.SellerName, CaptureHarness.SellerVatNumber, CaptureHarness.IssuedAt, 1150.00m, 150.00m);
        ZatcaQrContents contents = ZatcaQrReader.Read(encoded);

        output.WriteLine("الرمز: " + encoded);
        output.WriteLine("عاد: " + contents.SellerName + " · " + contents.GrossTotal.ToString("0.00", CultureInfo.InvariantCulture));

        Assert.Equal(ZatcaQrPhase.Phase1, contents.Phase);
        Assert.Equal(CaptureHarness.SellerName, contents.SellerName);
        Assert.Equal(CaptureHarness.SellerVatNumber, contents.SellerVatNumber);
        Assert.Equal(CaptureHarness.IssuedAt, contents.IssuedAt);
        Assert.Equal(1150.00m, contents.GrossTotal);
        Assert.Equal(150.00m, contents.TaxTotal);
    }

    /// <summary>والمرحلة الثانية: البصمة والتوقيع نصّاً، والمفتاح العام بايتات — بالشكل غير المتماثل نفسه.</summary>
    [Fact]
    public void The_asymmetric_value_forms_of_phase_two_survive_the_round_trip()
    {
        byte[] publicKey = [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70];
        byte[] certificateSignature = [0x01, 0x02, 0x03, 0x04];

        string simplified = ZatcaQr.Phase2(
            CaptureHarness.SellerName,
            CaptureHarness.SellerVatNumber,
            CaptureHarness.IssuedAt,
            1150.00m,
            150.00m,
            invoiceHashBase64: "3PbTHVmVaOKQd9GsNCbTZLPHVGT5xN6HvfxK9wHDnvE=",
            signatureBase64: "MEUCIQD0",
            publicKeyDer: publicKey,
            certificateSignature: certificateSignature,
            isSimplified: true);

        ZatcaQrContents contents = ZatcaQrReader.Read(simplified);

        output.WriteLine("أطوال الوسوم: " + Lengths(contents));

        Assert.Equal(ZatcaQrPhase.Phase2Simplified, contents.Phase);
        Assert.True(contents.IsCryptographicallyAttested);
        Assert.Equal("3PbTHVmVaOKQd9GsNCbTZLPHVGT5xN6HvfxK9wHDnvE=", contents.InvoiceHashBase64);
        Assert.Equal("MEUCIQD0", contents.SignatureBase64);
        Assert.Equal(publicKey, contents.PublicKey.ToArray());
        Assert.Equal(certificateSignature, contents.CertificateSignature.ToArray());
    }

    /// <summary>
    /// والذهاب والإياب في الاتجاه الآخر: ما يُفكّ يُعاد ترميزه فيعطي <b>البايتات نفسها</b>.
    /// عكسٌ يفقد بايتاً واحداً يمرّ في كل فحص إلا هذا.
    /// </summary>
    [Fact]
    public void Re_encoding_what_was_decoded_reproduces_the_very_same_bytes()
    {
        string original = CaptureHarness.Phase2Qr(1150.00m, 150.00m);
        ZatcaQrContents contents = ZatcaQrReader.Read(original);

        string reEncoded = ZatcaQr.Phase2(
            contents.SellerName,
            contents.SellerVatNumber,
            contents.IssuedAt,
            contents.GrossTotal,
            contents.TaxTotal,
            contents.InvoiceHashBase64!,
            contents.SignatureBase64!,
            contents.PublicKey,
            contents.CertificateSignature,
            isSimplified: false);

        output.WriteLine("الأصل      : " + original);
        output.WriteLine("بعد الدورة : " + reEncoded);

        Assert.Equal(original, reEncoded);
    }

    // ── الفخّ: البايت لا المحرف ─────────────────────────────────────────────

    /// <summary>
    /// <b>الفخّ الذي وثّقه المُرمِّز، مفحوصاً من جهة القارئ:</b> خانة الطول تعدّ بايتات،
    /// والاسم العربي يكلّف بايتين لكل حرف. قارئٌ يتقدّم بعدد المحارف ينزلق إلى داخل
    /// الوسم التالي فيقرأ حقولاً تبدو معقولة.
    /// </summary>
    [Fact]
    public void The_length_byte_counts_bytes_and_the_Arabic_name_costs_nearly_two_per_character()
    {
        ZatcaQrContents contents = ZatcaQrReader.Read(CaptureHarness.Phase1Qr(1150.00m, 150.00m));
        int declared = contents.TagLengths.First(static tag => tag.Tag == 1).ByteLength;

        output.WriteLine("الاسم                 : " + contents.SellerName);
        output.WriteLine("طوله بالمحارف         : " + contents.SellerName.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("طوله بالبايت (معلن)   : " + declared.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("طوله بالبايت (محسوب)  : " + Encoding.UTF8.GetByteCount(contents.SellerName).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(Encoding.UTF8.GetByteCount(contents.SellerName), declared);
        Assert.True(declared > contents.SellerName.Length, "العيّنة لا تُظهر الفارق أصلاً — الاختبار يمرّ فارغاً");
    }

    /// <summary>
    /// <b>قياس ما يمنعه الرفض:</b> يُنفَّذ هنا الفكّ المتساهل على الاسم المقصوص نفسه،
    /// ويُعرض ناتجه — كي يكون «لماذا نرفض» نصّاً منظوراً لا رأياً.
    /// </summary>
    [Fact]
    public void The_lenient_decoding_this_refusal_replaces_returns_a_replacement_character_and_passes()
    {
        byte[] all = Encoding.UTF8.GetBytes(CaptureHarness.SellerName);
        byte[] cut = all[..46];

        string lenient = Encoding.UTF8.GetString(cut);

        output.WriteLine("الأصل بالبايت      : " + all.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("بعد القصّ          : " + cut.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("الفكّ المتساهل     : " + lenient);
        output.WriteLine("يحمل محرف استبدال  : " + lenient.Contains('�').ToString(CultureInfo.InvariantCulture));

        Assert.Contains('�', lenient);

        // والقارئ الصارم يرفض البايتات نفسها بدل أن يعيدها «اسماً».
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(GoldenPath));
        string payload = golden.RootElement.GetProperty("refused").EnumerateArray()
            .Single(static v => v.GetProperty("id").GetString() == "reject.truncated_arabic_utf8")
            .GetProperty("payload").GetString()!;

        ZatcaQrException error = Assert.Throws<ZatcaQrException>(() => ZatcaQrReader.Read(payload));
        output.WriteLine("القارئ الصارم      : " + error.Message);
        Assert.Contains("ليست UTF-8 صالحة", error.Message, StringComparison.Ordinal);
    }

    // ── الاتّساق مع المتجهات المُودَعة في مسار الالتزام ─────────────────────

    /// <summary>
    /// المتجه <c>qr.phase1.tlv</c> المُودَع في متجهات الالتزام <b>يُفكّ هنا</b> ويعطي
    /// أطوالاً تطابق <c>qr.phase1.tag.lengths</c> المُودَع بجواره.
    /// <para>
    /// وهذا هو الربط الذي يمنع انفصال المتجهين: لو تحرّك المُرمِّز وحُدِّثت متجهاته
    /// وحدها لسقط هذا الاختبار.
    /// </para>
    /// </summary>
    [Fact]
    public void The_committed_compliance_vector_decodes_and_matches_its_committed_tag_lengths()
    {
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(ComplianceGoldenPath));

        Dictionary<string, string> vectors = golden.RootElement.GetProperty("vectors").EnumerateArray()
            .ToDictionary(
                static v => v.GetProperty("id").GetString()!,
                static v => v.GetProperty("text").GetString()!,
                StringComparer.Ordinal);

        ZatcaQrContents contents = ZatcaQrReader.Read(vectors["qr.phase1.tlv"]);

        output.WriteLine("من متجهات الالتزام: " + vectors["qr.phase1.tlv"]);
        output.WriteLine("الأطوال المُودَعة  : " + vectors["qr.phase1.tag.lengths"]);
        output.WriteLine("الأطوال المفكوكة  : " + Lengths(contents));

        Assert.Equal(vectors["qr.phase1.tag.lengths"], Lengths(contents));
        Assert.Equal(
            vectors["qr.timestamp"],
            contents.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
    }

    private static string Lengths(ZatcaQrContents contents) =>
        string.Join(',', contents.TagLengths.Select(static tag => FormattableString.Invariant($"{tag.Tag}:{tag.ByteLength}")));

    private static string Hex(ReadOnlyMemory<byte> value) => Convert.ToHexStringLower(value.Span);
}
