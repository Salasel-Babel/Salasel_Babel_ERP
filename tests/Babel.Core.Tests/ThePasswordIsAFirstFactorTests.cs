using System.Text.RegularExpressions;
using Babel.Core.Access;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>كلمةُ المرور عاملُ إثباتٍ أوّل فوق محرّك الجلسات — وما يُقاس هنا هو ما ينكسر صامتاً.</b>
/// <para>
/// وستّةُ أعطالٍ من هذا الصنف لا يُحمّرها شيءٌ غير شاهدٍ مقصود: تسويةٌ تختلف بين
/// الكتابة والقراءة فيصير البابُ لا يُفتح؛ وشكلُ إثباتٍ تقبله الشيفرة ويرفضه قيدُ
/// القاعدة فتسقط أوّلُ كتابة في الإنتاج؛ وإثباتٌ تالفٌ يرمي استثناءً فيُسقط الخادمَ
/// بدل أن يُغلق الباب؛ وملحٌ ثابت يجعل جدولاً واحداً يكسر كلَّ الحسابات؛ وتكراراتٌ
/// تُخفَّض بسطرٍ فتصير الكلفةُ صفراً على من يخمّن؛ وحدُّ طولٍ يُنسى فتصير حمولةُ
/// ميغابايتٍ على بابٍ بلا اعتماد بابَ إنهاك.
/// </para>
/// </summary>
public sealed class ThePasswordIsAFirstFactorTests
{
    private const string Password = "a-long-enough-password";

    /// <summary>
    /// التسويةُ تُشذّب وتُصغّر — <b>وبثقافةٍ ثابتة</b>.
    /// <para>
    /// والحرفُ التركيّ هو الشاهد: <c>ToLower()</c> بثقافة <c>tr-TR</c> يجعل
    /// <c>I</c> حرفاً بلا نقطة، فيصير بريدٌ واحد بريدين بحسب لغة الخادم.
    /// </para>
    /// </summary>
    [Fact]
    public void التسويةُ_تُشذّب_وتُصغّر_ولا_تتبع_ثقافةَ_الخادم()
    {
        Assert.Equal("owner@example.sa", AccessPasswords.Normalise("  Owner@Example.SA  "));
        Assert.Equal("id@example.sa", AccessPasswords.Normalise("ID@example.sa"));
        Assert.Equal(string.Empty, AccessPasswords.Normalise(null));
    }

    /// <summary>شكلُ المعرّف: «@» واحدة بلا فراغ، وطرفان غيرُ فارغين.</summary>
    [Theory]
    [InlineData("owner@example.sa", true)]
    [InlineData("a@b", true)]
    [InlineData("محاسب@شركة.السعودية", true)]
    [InlineData("owner example.sa", false)]
    [InlineData("@example.sa", false)]
    [InlineData("owner@", false)]
    [InlineData("owner@@example.sa", false)]
    [InlineData("own er@example.sa", false)]
    [InlineData("", false)]
    public void شكلُ_المعرّف_يُفحَص_ولا_يُفترَض(string handle, bool accepted) =>
        Assert.Equal(accepted, AccessPasswords.HasHandleShape(AccessPasswords.Normalise(handle)));

    /// <summary>ومعرّفٌ أطول من حدّ RFC 5321 يُرفض قبل أي بحث.</summary>
    [Fact]
    public void ومعرّفٌ_فوق_الحدّ_يُرفض()
    {
        string tooLong = new string('a', AccessPasswords.MaximumHandleLength) + "@x.sa";
        Assert.False(AccessPasswords.HasHandleShape(tooLong));
    }

    /// <summary>
    /// الطولُ وحده ما يُقاس — <b>ولا قواعدَ تركيب</b> (‏NIST 800-63B).
    /// </summary>
    [Fact]
    public void الطولُ_وحده_يُقاس_ولا_يُطلَب_تركيب()
    {
        Assert.False(AccessPasswords.HasPasswordLength(new string('x', AccessPasswords.MinimumPasswordLength - 1)));
        Assert.True(AccessPasswords.HasPasswordLength(new string('x', AccessPasswords.MinimumPasswordLength)));
        Assert.True(AccessPasswords.HasPasswordLength(new string('x', AccessPasswords.MaximumPasswordLength)));
        Assert.False(AccessPasswords.HasPasswordLength(new string('x', AccessPasswords.MaximumPasswordLength + 1)));
        Assert.False(AccessPasswords.HasPasswordLength(null));

        // ‏**لا حرفَ كبيراً ولا رقماً ولا رمزاً**: اثنا عشر حرفاً صغيراً تمرّ.
        Assert.True(AccessPasswords.HasPasswordLength("abcdefghijkl"));
    }

    /// <summary>الاشتقاقُ يُعكَس بالتحقّق، والكلمةُ الخاطئة تُردّ.</summary>
    [Fact]
    public void الاشتقاقُ_يُتحقَّق_منه_والخاطئةُ_تُردّ()
    {
        string proof = AccessPasswords.Derive(Password);

        Assert.True(AccessPasswords.Verify(Password, proof));
        Assert.False(AccessPasswords.Verify(Password + "x", proof));
        Assert.False(AccessPasswords.Verify(string.Empty, proof));
    }

    /// <summary>
    /// <b>ولا نصَّ كلمةٍ في الإثبات</b> — وهذا ما يُقرأ من نسخةٍ احتياطية.
    /// </summary>
    [Fact]
    public void ولا_نصَّ_كلمةٍ_في_الإثبات()
    {
        string proof = AccessPasswords.Derive(Password);
        Assert.DoesNotContain(Password, proof, StringComparison.Ordinal);
    }

    /// <summary>
    /// ملحٌ لكل صفّ: كلمتان متطابقتان تُنتجان إثباتين مختلفين.
    /// <para>
    /// وبلا هذا يصير جدولٌ واحد مسبوقُ الحساب كافياً لكسر كلّ من اختار الكلمة نفسها.
    /// </para>
    /// </summary>
    [Fact]
    public void ملحٌ_لكل_صفّ_فلا_يتطابق_إثباتان()
    {
        Assert.NotEqual(AccessPasswords.Derive(Password), AccessPasswords.Derive(Password));
    }

    /// <summary>
    /// <b>الشكلُ الذي تُنتجه الشيفرة هو الشكلُ الذي يقبله قيدُ قاعدة البيانات.</b>
    /// <para>
    /// والتعبيرُ مصدرُه واحد (<see cref="AccessPasswords.ProofPattern"/>) يقرؤه
    /// <c>CoreDbContext</c>، فهذا الشاهد يقيس أن المُنتَج يطابق المُعلَن — وانحرافُهما
    /// كان سيُسقط <b>أوّل</b> كتابةٍ في الإنتاج ولا يظهر في أي اختبارٍ بلا قاعدة.
    /// </para>
    /// </summary>
    [Fact]
    public void الشكلُ_المُنتَج_يطابق_قيدَ_القاعدة()
    {
        string pattern = AccessPasswords.ProofPattern.Replace("\\\\$", "\\$", StringComparison.Ordinal);
        Assert.Matches(new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(2)), AccessPasswords.Derive(Password));
    }

    /// <summary>
    /// إثباتٌ مشوَّه يُقرأ <c>false</c> <b>ولا يرمي</b>: صفٌّ تالفٌ يُغلق الباب
    /// ولا يُسقط الخادم بخمسمئة.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("لا-شيء")]
    [InlineData("pbkdf2-sha256$600000$ملحٌ ليس base64$x")]
    [InlineData("pbkdf2-sha256$0$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$ليس رقماً$AAAA$AAAA")]
    [InlineData("argon2id$3$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$600000$$")]
    public void إثباتٌ_مشوَّه_يُغلق_الباب_ولا_يرمي(string proof) =>
        Assert.False(AccessPasswords.Verify(Password, proof));

    /// <summary>
    /// <b>عاملُ الكلفة لا يُخفَّض بسطرٍ صامت.</b>
    /// <para>
    /// ستّمئةُ ألفٍ هي توصيةُ OWASP المعلنة لـPBKDF2-HMAC-SHA256. وخفضُها يجعل
    /// التخمينَ أرخصَ بالنسبة نفسها، ولا يُحمّر شيئاً — فيُقاس هنا صراحةً.
    /// </para>
    /// </summary>
    [Fact]
    public void عاملُ_الكلفة_لا_يُخفَّض_صامتاً()
    {
        Assert.True(AccessPasswords.Iterations >= 600_000);
        Assert.Equal(16, AccessPasswords.SaltBytes);
        Assert.Equal(32, AccessPasswords.HashBytes);
        Assert.Equal(12, AccessPasswords.MinimumPasswordLength);
    }

    /// <summary>
    /// إنفاقُ الزمن يردّ <c>false</c> دائماً — وهو ما يُستدعى لمعرّفٍ لا صفَّ له.
    /// </summary>
    [Fact]
    public void إنفاقُ_الزمنِ_يردّ_رفضاً_دائماً() =>
        Assert.False(AccessPasswords.WasteEqualTime(Password));
}
