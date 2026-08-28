using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Storage.Tests;

/// <summary>
/// <b>ما يقوله العميل ليس معطى.</b> هذه المجموعة لا تحتاج قاعدة بيانات ولا قرصاً —
/// وهي التي تغطّي «لا تثق بالنوع المُعلَن ولا باسم الملفّ» كاملاً.
/// </summary>
public sealed class SniffingAndNamingTests
{
    /// <summary>عيّنات صادقة: أول بايتات كل نوع مقبول.</summary>
    public static TheoryData<string, byte[], AttachmentMediaType> HonestSamples() => new()
    {
        { "jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01], AttachmentMediaType.Jpeg },
        { "png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D], AttachmentMediaType.Png },
        { "pdf", [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x25, 0x00, 0x00], AttachmentMediaType.Pdf },
        { "tiff-le", [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], AttachmentMediaType.Tiff },
        { "tiff-be", [0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00], AttachmentMediaType.Tiff },
        { "webp", [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50], AttachmentMediaType.Webp },
        { "heic", [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63], AttachmentMediaType.Heic },
    };

    [Theory]
    [MemberData(nameof(HonestSamples))]
    public void The_type_is_read_from_the_bytes(string label, byte[] content, AttachmentMediaType expected)
    {
        Assert.Equal(expected, ContentSniff.Of(content));
        Assert.False(string.IsNullOrEmpty(label));
    }

    /// <summary>
    /// <b>ما لا يُتعرَّف عليه يُرفض.</b> ولا عضو «غير معروف» في المجموعة المغلقة —
    /// وهذا الاختبار هو ما يمنع إضافته لاحقاً بحسن نيّة.
    /// </summary>
    [Fact]
    public void Bytes_that_match_nothing_are_refused_not_stored_as_a_neutral_type()
    {
        Assert.Null(ContentSniff.Of([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00]));
        Assert.Null(ContentSniff.Of("<html><body>hello</body></html>"u8));
        Assert.Null(ContentSniff.Of([]));
        Assert.Null(ContentSniff.Of([0xFF, 0xD8]));
    }

    /// <summary>
    /// ‏<c>RIFF</c> وحده ليس WEBP — ملفّ صوت WAVE يبدأ به. والشمّ يقرأ العلامة الثانية
    /// عند الإزاحة 8، لا الأربعة الأولى وحدها.
    /// </summary>
    [Fact]
    public void A_RIFF_container_that_is_not_WEBP_is_refused()
    {
        byte[] wave = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45];
        Assert.Null(ContentSniff.Of(wave));
    }

    /// <summary>وعلامة ISO-BMFF خارج القائمة تُرفض ولو كانت الترويسة <c>ftyp</c> صحيحة.</summary>
    [Fact]
    public void An_ftyp_container_with_an_unaccepted_brand_is_refused()
    {
        byte[] mp4 = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D];
        Assert.Null(ContentSniff.Of(mp4));
    }

    /// <summary>الإعلان الصادق يمرّ، ومعه معاملاته.</summary>
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/JPEG")]
    [InlineData("image/jpeg; charset=binary")]
    [InlineData(null)]
    [InlineData("")]
    public void An_agreeing_or_absent_declaration_is_accepted(string? declared)
        => Assert.True(ContentSniff.DeclarationAgrees(declared, AttachmentMediaType.Jpeg));

    /// <summary>
    /// <b>الإعلان الكاذب رفضٌ باسمه لا تصحيحٌ صامت.</b> التصحيح الصامت يجعل العميل
    /// يظنّ أن ما أرسله قُبل كما أرسله.
    /// </summary>
    [Fact]
    public void A_declaration_that_contradicts_the_bytes_is_a_refusal()
    {
        Assert.False(ContentSniff.DeclarationAgrees("application/pdf", AttachmentMediaType.Jpeg));

        Error error = AttachmentErrors.DeclaredTypeMismatch("application/pdf", AttachmentMediaType.Jpeg);
        Assert.Equal("storage.declared_type_mismatch", error.Code);
        Assert.Contains("application/pdf", error.MessageAr, StringComparison.Ordinal);
        Assert.Contains("image/jpeg", error.MessageEn, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>اسم الملفّ بيانات لا مسار.</b> وكل صورة من صور اجتياز المسار تسقط عند
    /// التطهير — <b>ثم لا تصل المسار أصلاً</b>، لأن الاسم لا يشارك في بنائه بحال.
    /// </summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("subdir/invoice.jpg")]
    public void A_path_shaped_name_is_reduced_to_a_bare_display_name(string declared)
    {
        Result<string> sanitised = SafeFileName.Sanitise(declared, AttachmentMediaType.Jpeg);

        Assert.True(sanitised.IsSuccess, declared);
        Assert.DoesNotContain('/', sanitised.Value);
        Assert.DoesNotContain('\\', sanitised.Value);
        Assert.DoesNotContain("..", sanitised.Value, StringComparison.Ordinal);
        Assert.EndsWith(".jpg", sanitised.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// المحرف الاتجاهي <c>U+202E</c> يقلب ما بعده عند العرض، فيُقرأ <c>gpj.exe</c>
    /// على أنه <c>exe.jpg</c>. ويُسقَط هنا (فخ-23).
    /// </summary>
    [Fact]
    public void A_right_to_left_override_is_stripped_from_the_display_name()
    {
        Result<string> sanitised = SafeFileName.Sanitise("فاتورة\u202Egpj.exe", AttachmentMediaType.Jpeg);

        Assert.True(sanitised.IsSuccess);
        Assert.DoesNotContain('\u202E', sanitised.Value);
        Assert.EndsWith(".jpg", sanitised.Value, StringComparison.Ordinal);
    }

    /// <summary>محارف التحكّم لا تدخل ترويسة <c>Content-Disposition</c>.</summary>
    [Fact]
    public void Control_characters_never_reach_a_header()
    {
        Result<string> sanitised = SafeFileName.Sanitise("inv\r\nSet-Cookie: a=b", AttachmentMediaType.Pdf);

        Assert.True(sanitised.IsSuccess);
        Assert.DoesNotContain('\r', sanitised.Value);
        Assert.DoesNotContain('\n', sanitised.Value);
    }

    /// <summary>
    /// اسمٌ لا يبقى منه محرف مقبول واحد <b>رفضٌ باسمه</b> — وهذا بالضبط شكل الاسم
    /// المصنوع للهجوم، لا اسمٌ عربي طبيعي.
    /// </summary>
    [Theory]
    [InlineData("///")]
    [InlineData("...")]
    [InlineData("\u202E\u200F")]
    public void A_name_with_nothing_acceptable_left_is_refused(string declared)
    {
        Result<string> sanitised = SafeFileName.Sanitise(declared, AttachmentMediaType.Pdf);

        Assert.True(sanitised.IsFailure);
        Assert.Equal("storage.file_name_refused", sanitised.Errors[0].Code);
    }

    /// <summary>اسمٌ غائب ليس رفضاً: غيابُ ادّعاء ليس ادّعاءً كاذباً.</summary>
    [Fact]
    public void An_absent_name_gets_a_neutral_one_with_the_sniffed_extension()
    {
        Result<string> sanitised = SafeFileName.Sanitise(null, AttachmentMediaType.Pdf);

        Assert.True(sanitised.IsSuccess);
        Assert.Equal(SafeFileName.Fallback + ".pdf", sanitised.Value);
    }

    /// <summary>
    /// <b>الامتداد يأتي من البايتات لا من الاسم.</b> فاسمٌ ينتهي بـ<c>.jpg</c> وبايتاته
    /// PDF يُحفظ بامتداد <c>pdf</c>، ولا يتناقض ما يُعرض مع ما يُقدَّم.
    /// </summary>
    [Fact]
    public void The_extension_comes_from_the_bytes_not_from_the_name()
    {
        Result<string> sanitised = SafeFileName.Sanitise("فاتورة-المورد.jpg", AttachmentMediaType.Pdf);

        Assert.True(sanitised.IsSuccess);
        Assert.Equal("فاتورة-المورد.pdf", sanitised.Value);
    }

    /// <summary>والاسم العربي يبقى عربياً — الحرف العربي ليس محرفاً خطراً (‏ADR-0021).</summary>
    [Fact]
    public void An_arabic_name_survives_intact()
    {
        Result<string> sanitised = SafeFileName.Sanitise("فاتورة شركة الرياض ٢٠٢٦.jpeg", AttachmentMediaType.Jpeg);

        Assert.True(sanitised.IsSuccess);
        Assert.Equal("فاتورة شركة الرياض ٢٠٢٦.jpg", sanitised.Value);
    }

    /// <summary>ولا يتجاوز الاسم المحفوظ حدّ العمود.</summary>
    [Fact]
    public void A_very_long_name_is_bounded_by_the_column_width()
    {
        Result<string> sanitised = SafeFileName.Sanitise(new string('ب', 4000) + ".jpg", AttachmentMediaType.Jpeg);

        Assert.True(sanitised.IsSuccess);
        Assert.True(sanitised.Value.Length <= SafeFileName.MaximumLength, sanitised.Value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
