using System.Globalization;
using System.Text;
using Babel.Compliance.Zatca.Qr;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// رمز الاستجابة السريعة. <b>ثلاثة أعطال هنا تُنتج رمزاً «يعمل» وهو خاطئ</b> — وهو أسوأ
/// ناتج ممكن، لأن الرمز يُقرأ بنجاح فلا يشكّ فيه أحد حتى يصل إلى متحقّق الهيئة.
/// </summary>
public sealed class ZatcaQrTests(ITestOutputHelper output)
{
    private static readonly UTF8Encoding Utf8 = new(false);

    // ── الطول ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>العطل المقيس هنا:</b> خانة الطول في TLV بايت واحد، والاسم العربي يكلّف بايتين
    /// لكل حرف تقريباً. فاسم منشأة من 130 حرفاً عربياً يتجاوز 255 بايتاً.
    /// <para/>
    /// والقصّ ليس علاجاً بل عطلاً ثانياً: القصّ عند البايت 255 يقع <b>داخل</b> محرف عربي
    /// فيُنتج UTF-8 غير صالح — رمز يُقرأ ويعرض محرف استبدال مكان آخر حرف من اسم المنشأة.
    /// </summary>
    [Fact]
    public void A_value_longer_than_the_length_byte_is_refused_and_never_truncated()
    {
        string longName = string.Concat(Enumerable.Repeat("شركة سلاسل بابل التجارية ", 8));
        int bytes = Utf8.GetByteCount(longName);

        output.WriteLine("طول الاسم بالحروف: " + longName.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("طوله بالبايت    : " + bytes.ToString(CultureInfo.InvariantCulture));

        Assert.True(bytes > ZatcaQr.MaximumValueLength,
            "عيّنة الاختبار لا تتجاوز الحدّ أصلاً — الاختبار سيمرّ فارغاً");

        ZatcaQrException error = Assert.Throws<ZatcaQrException>(() => ZatcaQr.Phase1(
            longName, "300000000000003", ZatcaFixtures.IssuedAt, 1350.00m, 150.00m));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("القصّ ممنوع", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>قياس العطل الذي يمنعه الرفض</b>: يُنفَّذ القصّ الساذج هنا ويُعرض ناتجه، كي
    /// يكون «لماذا نرفض» رقماً منظوراً لا رأياً.
    /// </summary>
    [Fact]
    public void The_naive_truncation_this_refusal_replaces_really_does_corrupt_the_Arabic_name()
    {
        string longName = string.Concat(Enumerable.Repeat("شركة سلاسل بابل التجارية ", 8));
        byte[] all = Utf8.GetBytes(longName);
        byte[] cut = all[..ZatcaQr.MaximumValueLength];

        string decoded = Encoding.UTF8.GetString(cut);

        output.WriteLine("الأصل بالبايت : " + all.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("بعد القصّ     : " + cut.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("الناتج المقصوص: " + decoded);
        output.WriteLine("يحمل محرف استبدال: "
            + decoded.Contains('�').ToString(CultureInfo.InvariantCulture));
        output.WriteLine("بايتات ضاعت: "
            + (all.Length - cut.Length).ToString(CultureInfo.InvariantCulture));

        // القصّ وقع **داخل** محرف عربي: هذا ليس فقداناً للاسم وحده بل UTF-8 غير صالح.
        Assert.Contains('�', decoded);

        // القصّ يفقد نصف الاسم على الأقل، وقد يفسد آخر محرف.
        Assert.True(decoded.Length < longName.Length);
        Assert.NotEqual(longName, decoded);
    }

    // ── الترتيب والأطوال ────────────────────────────────────────────────────

    [Fact]
    public void Phase_one_carries_five_tags_in_order()
    {
        string encoded = ZatcaQr.Phase1(
            ZatcaFixtures.SellerParty.NameAr, "300000000000003", ZatcaFixtures.IssuedAt, 1350.00m, 150.00m);

        IReadOnlyList<QrTag> tags = ZatcaQr.Decode(encoded);

        output.WriteLine("الرمز: " + encoded);
        foreach (QrTag tag in tags)
        {
            output.WriteLine(FormattableString.Invariant($"  وسم {tag.Tag} طوله {tag.Value.Length}: {tag.AsText()}"));
        }

        Assert.Equal([1, 2, 3, 4, 5], tags.Select(t => (int)t.Tag));
        Assert.Equal(ZatcaFixtures.SellerParty.NameAr, tags[0].AsText());
        Assert.Equal("300000000000003", tags[1].AsText());
        Assert.Equal("2026-08-25T10:30:00Z", tags[2].AsText());
        Assert.Equal("1350.00", tags[3].AsText());
        Assert.Equal("150.00", tags[4].AsText());
    }

    /// <summary>
    /// <b>وسم ناقص يُقرأ بنجاح تام.</b> ولذلك المرحلة نوع صريح لا عدد وسوم متروك للنيّة:
    /// المرحلة الثانية تحمل البصمة والتوقيع والمفتاح العام، والوسم التاسع للمبسّطة وحدها.
    /// </summary>
    [Fact]
    public void Phase_two_adds_the_hash_the_signature_and_the_public_key_and_the_ninth_tag_only_for_simplified()
    {
        byte[] publicKey = [0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70];
        byte[] certificateSignature = [0x01, 0x02, 0x03, 0x04];

        string standard = ZatcaQr.Phase2(
            ZatcaFixtures.SellerParty.NameAr, "300000000000003", ZatcaFixtures.IssuedAt,
            1350.00m, 150.00m,
            invoiceHashBase64: "3PbTHVmVaOKQd9GsNCbTZLPHVGT5xN6HvfxK9wHDnvE=",
            signatureBase64: "MEUCIQD0",
            publicKeyDer: publicKey,
            certificateSignature: certificateSignature,
            isSimplified: false);

        string simplified = ZatcaQr.Phase2(
            ZatcaFixtures.SellerParty.NameAr, "300000000000003", ZatcaFixtures.IssuedAt,
            1350.00m, 150.00m,
            invoiceHashBase64: "3PbTHVmVaOKQd9GsNCbTZLPHVGT5xN6HvfxK9wHDnvE=",
            signatureBase64: "MEUCIQD0",
            publicKeyDer: publicKey,
            certificateSignature: certificateSignature,
            isSimplified: true);

        int[] standardTags = [.. ZatcaQr.Decode(standard).Select(t => (int)t.Tag)];
        int[] simplifiedTags = [.. ZatcaQr.Decode(simplified).Select(t => (int)t.Tag)];

        output.WriteLine("وسوم القياسية: " + string.Join("، ", standardTags));
        output.WriteLine("وسوم المبسّطة : " + string.Join("، ", simplifiedTags));

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], standardTags);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], simplifiedTags);

        // والوسم الثامن بايتات خام لا نصّ: طوله يساوي طول DER بالضبط.
        Assert.Equal(publicKey.Length, ZatcaQr.Decode(standard)[7].Value.Length);
    }

    [Fact]
    public void A_decoded_tag_length_always_matches_its_value()
    {
        string encoded = ZatcaQr.Phase1(
            ZatcaFixtures.SellerParty.NameAr, "300000000000003", ZatcaFixtures.IssuedAt, 1350.00m, 150.00m);

        byte[] raw = Convert.FromBase64String(encoded);
        int position = 0;
        int seen = 0;

        while (position < raw.Length)
        {
            byte tag = raw[position];
            int length = raw[position + 1];
            position += 2 + length;
            seen++;
            output.WriteLine(FormattableString.Invariant($"وسم {tag} أعلن طولاً {length}"));
        }

        Assert.Equal(raw.Length, position);
        Assert.Equal(5, seen);
    }

    [Fact]
    public void A_truncated_encoding_is_rejected_rather_than_read_as_a_short_code()
    {
        string encoded = ZatcaQr.Phase1(
            ZatcaFixtures.SellerParty.NameAr, "300000000000003", ZatcaFixtures.IssuedAt, 1350.00m, 150.00m);

        byte[] raw = Convert.FromBase64String(encoded);
        string mangled = Convert.ToBase64String(raw[..(raw.Length - 5)]);

        ZatcaQrException error = Assert.Throws<ZatcaQrException>(() => ZatcaQr.Decode(mangled));
        output.WriteLine("رُفض: " + error.Message);
    }

    [Fact]
    public void An_empty_code_is_refused_because_it_encodes_successfully_and_carries_nothing()
    {
        ZatcaQrException error = Assert.Throws<ZatcaQrException>(() => ZatcaQr.Encode([]));
        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("فارغ", error.Message, StringComparison.Ordinal);
    }

    /// <summary>الطابع الزمني ثابت الثقافة: تحت أي لغة نظام يعطي النصّ نفسه.</summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("de-DE")]
    public void The_timestamp_does_not_move_with_the_ambient_culture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            Assert.Equal("2026-08-25T10:30:00Z", ZatcaQr.Timestamp(ZatcaFixtures.IssuedAt));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
