using System.Globalization;
using Babel.Ai.Boundary;
using Babel.Ai.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>«مِقبضٌ معتم» كان وعداً مكسوراً — والكسر كان مقروءاً بلا مفتاح.</b>
/// <para>
/// كان المِقبض <c>payload ‖ HMAC</c> و<c>payload</c> نصٌّ صريح، ففكُّ base64url وحده
/// كان يُخرج <b>معرّف صفّ العميل</b> والمنشأة والشركة والجلسة. والتوقيع يمنع التبديل
/// ولا يمنع القراءة. وقاعدة المالك أنّ ما شكلُه معرّف لا يعبر إلى النموذج — <b>وكان
/// يعبر في كل حلٍّ ناجح</b>، في نصٍّ يقرؤه كل من يملك نسخة المحادثة.
/// </para>
/// <para>
/// وهذا الملفّ يقيس أن المِقبض اليوم <b>لا يقول لحامله شيئاً</b>، وأنه لا يقول ذلك
/// بالبناء لا بالتفاؤل: البايتات مُعمّاة، والإصداران للصفّ الواحد لا يتشابهان، والنصّ
/// لا يحمل شكلاً يُلتقط عند الحدّ.
/// </para>
/// </summary>
public sealed class TheHandleTellsItsHolderNothing
{
    private static readonly TenantId Tenant = new(new Guid("100c0a5e-0000-4000-8000-0000000000aa"));
    private static readonly Guid Company = new("c0000000-0000-4000-8000-0000000000bb");
    private static readonly Guid Session = new("5e551000-0000-4000-8000-0000000000cc");
    private static readonly Guid Subject = new("c5700000-0000-4000-8000-0000000000dd");

    private static SignedLookupHandles Handles()
    {
        byte[] key = new byte[32];
        for (int index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(index * 7);
        }

        return new SignedLookupHandles(key, new LookupOptions(), TimeProvider.System);
    }

    private static string Mint(SignedLookupHandles handles, Guid subject) => handles
        .Issue(LookupHandlePurpose.Entity, Tenant, Company, Session, subject, TimeSpan.FromMinutes(10))
        .Value;

    private static byte[] Decode(string token)
    {
        string flat = token.Replace(
            SignedLookupHandles.GroupSeparator.ToString(), string.Empty, StringComparison.Ordinal);

        string standard = flat.Replace('-', '+').Replace('_', '/')
            + new string('=', (4 - (flat.Length % 4)) % 4);

        return Convert.FromBase64String(standard);
    }

    /// <summary>
    /// <b>القياس الذي كان يفضح العطل:</b> فكُّ الترميز بلا مفتاح لا يُخرج معرّفاً واحداً
    /// من الأربعة — لا الصفّ، ولا المنشأة، ولا الشركة، ولا الجلسة.
    /// </summary>
    [Fact]
    public void DecodingTheTokenWithNoKeyYieldsNoneOfTheIdentifiersInsideIt()
    {
        byte[] raw = Decode(Mint(Handles(), Subject));

        foreach ((string name, byte[] identifier) in new (string, byte[])[]
        {
            ("الصفّ", Subject.ToByteArray()),
            ("المنشأة", Tenant.Value.ToByteArray()),
            ("الشركة", Company.ToByteArray()),
            ("الجلسة", Session.ToByteArray()),
        })
        {
            Assert.False(
                Contains(raw, identifier),
                name + ": معرّفٌ يُقرأ من المِقبض بلا مفتاح — وهو بعينه ما يمنعه قرار المالك");
        }

        // ولا حتّى الغرض: بايتٌ واحد صريح هو رقم النسخة، وما عداه مُعمّى.
        Assert.Equal(2, raw[0]);
    }

    /// <summary>
    /// ولا يُقارَن مِقبضان ليُعرف أنهما لصفٍّ واحد: لكل إصدارٍ <c>nonce</c> خاصّ به.
    /// <b>وبدونه</b> يصير تكرار المِقبض نفسه إعلاناً بأن الاسم لم يتغيّر.
    /// </summary>
    [Fact]
    public void TwoHandlesForTheSameRowShareNoBytesAfterTheVersion()
    {
        SignedLookupHandles handles = Handles();

        string first = Mint(handles, Subject);
        string second = Mint(handles, Subject);

        Assert.NotEqual(first, second);
        Assert.Equal(SignedLookupHandles.TokenLength, first.Length);
        Assert.Equal(SignedLookupHandles.TokenLength, second.Length);

        // وكلاهما يفتدي إلى الصفّ نفسه — فالاختلاف في الشكل لا في المعنى.
        foreach (string token in new[] { first, second })
        {
            Result<RedeemedLookupHandle> redeemed = handles.Redeem(
                token, LookupHandlePurpose.Entity, Tenant, Company, Session);

            Assert.True(redeemed.IsSuccess);
            Assert.Equal(Subject, redeemed.Value.Subject);
        }
    }

    /// <summary>
    /// <b>والاستحالة بنيوية لا احتمالية:</b> لا مسار رقميّ في أي مِقبض يتجاوز
    /// <see cref="SignedLookupHandles.GroupLength"/> خانات، فلا يبلغ الشامل (تسع) ولا
    /// الهوية (عشر) ولا الضريبي (خمس عشرة). وكان يُقاس أن مِقبضاً من كل ‎مئتَي ألف
    /// يُرفض صدفةً <b>مرّةً لا تتكرّر ولا تُشخَّص</b>.
    /// </summary>
    [Fact]
    public void NoMintedHandleEverCarriesAnIdentifierShape()
    {
        SignedLookupHandles handles = Handles();
        int longestRun = 0;

        for (int attempt = 0; attempt < 20_000; attempt++)
        {
            string token = Mint(handles, Guid.NewGuid());
            longestRun = Math.Max(longestRun, LongestDigitRun(token));

            if (attempt % 500 != 0)
            {
                continue;
            }

            // والفحص الكامل على عيّنة: النصّ السلكيّ كما يعبر الحدّ فعلاً.
            Assert.True(
                AgentOutboundScrubber.Inspect(NameLookupWire.Write(NameLookupResult.Resolved(token))).IsClean,
                "مِقبضٌ رُفض عند الحدّ: " + token);
        }

        Assert.True(
            longestRun <= SignedLookupHandles.GroupLength,
            "أطول مسارٍ رقميّ في عشرين ألف مِقبض: "
            + longestRun.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <b>ولا كتابة ثانية لمِقبضٍ واحد.</b> كان تبديل المحرف الأخير يُنتج ستّة عشر نصّاً
    /// تُفتدى جميعاً إلى المحتوى نفسه — ولا شيء يُفهرَس بالنصّ اليوم، لكن أوّل قائمة
    /// إبطالٍ أو نافذة تكرارٍ تُبنى عليه تُهزَم بإعادة التهجئة.
    /// </summary>
    [Fact]
    public void ARespellingOfTheSameHandleIsRefusedNotAccepted()
    {
        SignedLookupHandles handles = Handles();
        string token = Mint(handles, Subject);

        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        int respellings = 0;

        foreach (char replacement in Alphabet)
        {
            if (replacement == token[^1])
            {
                continue;
            }

            string respelled = token[..^1] + replacement;

            if (!handles.Redeem(respelled, LookupHandlePurpose.Entity, Tenant, Company, Session).IsFailure)
            {
                respellings++;
            }
        }

        Assert.Equal(0, respellings);

        // والفواصل في غير مواضعها كتابةٌ أخرى كذلك.
        Assert.True(handles.Redeem(
            token.Replace(SignedLookupHandles.GroupSeparator, 'A'),
            LookupHandlePurpose.Entity, Tenant, Company, Session).IsFailure);
    }

    /// <summary>
    /// ‏<b>وغرضٌ خارج المفردة لا يُسكّ</b>: غرضٌ لا اسم له لا يُقارَن بشيءٍ عند
    /// الاسترداد، فيصير المِقبض صالحاً لكل باب.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(99)]
    public void AnUndefinedPurposeIsNeverMinted(int purpose)
    {
        Result<string> issued = Handles().Issue(
            (LookupHandlePurpose)purpose, Tenant, Company, Session, Subject, TimeSpan.FromMinutes(10));

        Assert.True(issued.IsFailure);
        Assert.Equal("ai.lookup.handle_purpose_undefined", Assert.Single(issued.Errors).Code);
    }

    /// <summary>وأغراض المفردة الأربعة كلّها تُسكّ، ولا يُفتدى أحدها مكان آخر.</summary>
    [Fact]
    public void EveryDeclaredPurposeMintsAndOnlyRedeemsAsItself()
    {
        SignedLookupHandles handles = Handles();

        foreach (LookupHandlePurpose minted in Enum.GetValues<LookupHandlePurpose>())
        {
            string token = handles
                .Issue(minted, Tenant, Company, Session, Subject, TimeSpan.FromMinutes(10))
                .Value;

            foreach (LookupHandlePurpose expected in Enum.GetValues<LookupHandlePurpose>())
            {
                Result<RedeemedLookupHandle> redeemed =
                    handles.Redeem(token, expected, Tenant, Company, Session);

                Assert.Equal(minted == expected, redeemed.IsSuccess);
            }
        }
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int start = 0; start + needle.Length <= haystack.Length; start++)
        {
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static int LongestDigitRun(string text)
    {
        int longest = 0;
        int run = 0;

        foreach (char character in text)
        {
            run = char.IsAsciiDigit(character) ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
