using System.Globalization;
using System.Text.Json;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>قارئ الأعداد المنطوقة، مقيساً بمتجهات مودَعة.</b>
/// <para>
/// والمتجهات في ملف <b>واحد يقرؤه تنفيذان</b>: هذا، ونظيره في
/// <c>web/src/voice/arabic-number.ts</c>. تنفيذان يقرآن ملفَّين ينحرفان، ولا يظهر
/// الانحراف إلا على شاشة صاحب المصلحة.
/// </para>
/// </summary>
public sealed class ArabicSpokenNumberTests
{
    private static readonly JsonDocument Vectors = Load();

    private static JsonDocument Load()
    {
        using FileStream stream = File.OpenRead(RepositoryRoot.At("tests/Babel.Ai.Tests/golden/arabic-spoken-numbers.v1.json"));
        return JsonDocument.Parse(stream);
    }

    public static TheoryData<string, string> Accepted()
    {
        TheoryData<string, string> data = [];

        foreach (JsonElement vector in Vectors.RootElement.GetProperty("accepted").EnumerateArray())
        {
            data.Add(vector.GetProperty("phrase").GetString()!, vector.GetProperty("value").GetString()!);
        }

        return data;
    }

    public static TheoryData<string, string> Rejected()
    {
        TheoryData<string, string> data = [];

        foreach (JsonElement vector in Vectors.RootElement.GetProperty("rejected").EnumerateArray())
        {
            data.Add(vector.GetProperty("phrase").GetString()!, vector.GetProperty("code").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Accepted))]
    public void العدد_المنطوق_يُقرأ_كما_يُودَع_في_المتجهات(string phrase, string expected)
    {
        Result<decimal> read = ArabicSpokenNumber.Read(phrase);

        Assert.True(read.IsSuccess, "رُفضت عبارة مقبولة: «" + phrase + "» — " + Describe(read.Errors));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), read.Value);
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void ما_لا_يُقرأ_يقيناً_يُرفض_برمزه_ولا_يُخمَّن(string phrase, string code)
    {
        Result<decimal> read = ArabicSpokenNumber.Read(phrase);

        Assert.True(read.IsFailure, "قُبلت عبارة يجب أن تُرفض: «" + phrase + "» ← " + (read.IsSuccess ? read.Value.ToString(CultureInfo.InvariantCulture) : string.Empty));
        Assert.Contains(read.Errors, error => error.Code == code);
    }

    /// <summary>
    /// <b>حارس لا فراغ.</b> مجموعة متجهات فارغة تجعل كل الاختبارات أعلاه تمرّ بلا أن
    /// تقرأ شيئاً — وهو بالضبط فخ-43 في هذا المستودع.
    /// </summary>
    [Fact]
    public void مجموعة_المتجهات_ليست_ضامرة()
    {
        int accepted = Vectors.RootElement.GetProperty("accepted").GetArrayLength();
        int rejected = Vectors.RootElement.GetProperty("rejected").GetArrayLength();

        Assert.True(accepted >= 20, "متجهات مقبولة: " + accepted.ToString(CultureInfo.InvariantCulture));
        Assert.True(rejected >= 6, "متجهات مرفوضة: " + rejected.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <b>شاهد موجب على الرفض نفسه:</b> المتجهات المرفوضة تغطي الأصناف الثلاثة كلها،
    /// وإلا كان «كل المرفوض مرفوض» صحيحاً وفارغاً لأن كلّه من صنف واحد.
    /// </summary>
    [Fact]
    public void المرفوض_يغطي_أصناف_الرفض_الثلاثة()
    {
        HashSet<string> codes = [];

        foreach (JsonElement vector in Vectors.RootElement.GetProperty("rejected").EnumerateArray())
        {
            codes.Add(vector.GetProperty("code").GetString()!);
        }

        Assert.Contains("ai.voice.mixed_digit_systems", codes);
        Assert.Contains("ai.voice.digits_and_words_mixed", codes);
        Assert.Contains("ai.voice.unknown_number_word", codes);
    }

    /// <summary>
    /// الأنظمة الأربعة كلها تُقرأ، <b>والخلط بينها وحده يُرفض</b> — وهذا هو الفرق بين
    /// «تطبيع بقرار» و«تطبيع بالصدفة».
    /// </summary>
    [Theory]
    [InlineData("١٢٣", "123")]
    [InlineData("۱۲۳", "123")]
    [InlineData("१२३", "123")]
    [InlineData("123", "123")]
    public void كل_نظام_أرقام_على_حدة_يُطبَّع(string token, string expected)
    {
        Result<string> normalised = ArabicNumerals.NormaliseToken(token);

        Assert.True(normalised.IsSuccess);
        Assert.Equal(expected, normalised.Value);
    }

    [Fact]
    public void الخلط_داخل_الكلمة_يُرفض_والخلط_عبر_الجملة_لا_يُرفض()
    {
        Assert.True(ArabicNumerals.NormaliseToken("١٢3").IsFailure);

        // جملةٌ فيها عددان كلٌّ من نظام واحد ليست خلطاً: الفحص داخل الكلمة لا عبرها.
        Result<string> sentence = ArabicNumerals.Normalise("الفاتورة ١٢٣ والمبلغ 500");

        Assert.True(sentence.IsSuccess);
        Assert.Equal("الفاتورة 123 والمبلغ 500", sentence.Value);
    }

    private static string Describe(IReadOnlyList<Error> errors) =>
        string.Join(" · ", errors.Select(static error => error.Code + ": " + error.MessageAr));
}
