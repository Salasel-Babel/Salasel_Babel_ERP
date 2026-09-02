using System.Globalization;
using System.Text;
using Babel.Ai.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>المِقبض المعتِم لا يُفكّ بلا مفتاح — وهذا حارسٌ يقيس البايتات لا يقرأ وعداً.</b>
/// <para>
/// <b>ولماذا حارسٌ لا مراجعة:</b> العطل الذي أُغلق هنا عاش في هذا المستودع فعلاً —
/// كان المِقبض <c>payload ‖ HMAC</c> و<c>payload</c> <b>نصّاً صريحاً</b>، ففكُّ
/// ‏base64url بلا مفتاحٍ واحد كان يُخرج المنشأة والشركة والجلسة <b>ومعرّف صفّ العميل
/// بعينه</b>. والتوقيع يُثبت أن البايتات لم تُبدَّل، <b>ولا يُخفيها</b> — والفرق بين
/// الجملتين هو الفرق بين وعدٍ مكتوب وحدٍّ قائم.
/// </para>
/// <para>
/// <b>وأربعة تُقاس، وكلٌّ منها يُغلق باباً مختلفاً:</b>
/// </para>
/// <list type="number">
///   <item><b>لا معرّف في البايتات:</b> لا المنشأة ولا الشركة ولا الجلسة ولا الصفّ
///         يظهر بايتاتُه في المِقبض المفكوك من الترميز — بأي من الترتيبين اللذين
///         يكتب بهما <c>Guid</c> نفسه.</item>
///   <item><b>ولا غرضَ يُقرأ:</b> مِقبضا غرضين مختلفين على الموضوع نفسه لا يشتركان
///         في بايتةٍ واحدة عند أي موضع — فلا تُقرأ منه رايةٌ ولا يُقارَن مِقبضان.</item>
///   <item><b>ومفتاحٌ آخر لا يفكّه:</b> مُصدِرٌ بمفتاحٍ ثانٍ يرفضه بـ«ليس موقَّعاً» —
///         لا يقرأ منه حقلاً ثم يرفض، بل لا يقرأ شيئاً أصلاً (‏AES-GCM يتحقّق من
///         العلامة قبل أن يُخرج بايتةً).</item>
///   <item><b>وطولُه واحد مهما كان موضوعه:</b> فلا يُقاس من الطول شيء.</item>
/// </list>
/// <para>
/// <b>والشاهد الموجب على الحارس نفسه (فخ-43):</b> البند الأول يُثبت أولاً أنّ
/// الماسح <b>يرى</b> — يجد بايتات المعرّف في نصٍّ وُضعت فيه عمداً — قبل أن يُثبت
/// أنه لا يجدها في المِقبض. وماسحٌ لا يُثبت أنه يرى يمرّ على كل شيء.
/// </para>
/// </summary>
public sealed class AnOpaqueHandleIsNeverReadWithoutItsKey
{
    private static readonly TenantId Tenant = new(Guid.Parse("aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"));
    private static readonly Guid Company = Guid.Parse("bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb");
    private static readonly Guid Session = Guid.Parse("cccccccc-3333-4333-8333-cccccccccccc");
    private static readonly Guid Subject = Guid.Parse("dddddddd-4444-4444-8444-dddddddddddd");

    private static readonly byte[] KeyOne = Encoding.UTF8.GetBytes("k1-" + new string('x', 61));
    private static readonly byte[] KeyTwo = Encoding.UTF8.GetBytes("k2-" + new string('y', 61));

    private static SignedLookupHandles Handles(byte[] key) =>
        new(key, new LookupOptions(), TimeProvider.System);

    private static string Issue(byte[] key, LookupHandlePurpose purpose, Guid subject)
    {
        Result<string> minted = Handles(key).Issue(
            purpose, Tenant, Company, Session, subject, TimeSpan.FromMinutes(10));

        Assert.True(minted.IsSuccess, "لم يُسكّ المِقبض: " + string.Join(" · ", minted.Errors.Select(e => e.Code)));
        return minted.Value;
    }

    /// <summary>يفكّ ترميز المِقبض بلا مفتاح — وهو كل ما يملكه من يقرأ نسخة المحادثة.</summary>
    private static byte[] DecodeWithoutAnyKey(string token)
    {
        string flat = token.Replace(
            SignedLookupHandles.GroupSeparator.ToString(CultureInfo.InvariantCulture),
            string.Empty,
            StringComparison.Ordinal);

        StringBuilder standard = new(flat.Length + 3);
        foreach (char character in flat)
        {
            standard.Append(character switch { '-' => '+', '_' => '/', _ => character });
        }

        standard.Append('=', (4 - (flat.Length % 4)) % 4);
        return Convert.FromBase64String(standard.ToString());
    }

    /// <summary>هل تظهر بايتات هذا المعرّف — بأي من كتابتيه — داخل هذه البايتات؟</summary>
    private static bool Carries(byte[] haystack, Guid identifier)
    {
        byte[] littleEndian = identifier.ToByteArray();
        byte[] bigEndian = identifier.ToByteArray(bigEndian: true);

        return Contains(haystack, littleEndian) || Contains(haystack, bigEndian);
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int at = 0; at + needle.Length <= haystack.Length; at++)
        {
            if (haystack.AsSpan(at, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>الشاهد الموجب أولاً</b>: الماسح يرى المعرّف حيث يوجد. وماسحٌ لا يُثبت أنه
    /// يرى ليس حارساً — هو جملة «لم أجد شيئاً» من عينٍ مغمضة.
    /// </summary>
    [Fact]
    public void الماسحُ_يرى_المعرّف_حين_يكون_موجوداً()
    {
        byte[] plain = [.. new byte[7], .. Subject.ToByteArray(), .. new byte[9]];
        Assert.True(Carries(plain, Subject), "الماسح لا يرى معرّفاً وُضع أمامه — فهو أعمى لا حارس.");

        byte[] alsoPlain = [.. new byte[3], .. Subject.ToByteArray(bigEndian: true), .. new byte[5]];
        Assert.True(Carries(alsoPlain, Subject), "الماسح يرى كتابةً واحدة من كتابتَي المعرّف.");

        Assert.False(Carries(new byte[64], Subject));
    }

    /// <summary>ولا معرّفَ واحداً في بايتات المِقبض — لا المنشأة ولا الشركة ولا الجلسة ولا الصفّ.</summary>
    [Fact]
    public void لا_معرّف_يُقرأ_من_المِقبض_بلا_مفتاح()
    {
        byte[] raw = DecodeWithoutAnyKey(Issue(KeyOne, LookupHandlePurpose.Entity, Subject));

        Assert.False(Carries(raw, Subject), "معرّف الصفّ يظهر في بايتات المِقبض.");
        Assert.False(Carries(raw, Tenant.Value), "معرّف المنشأة يظهر في بايتات المِقبض.");
        Assert.False(Carries(raw, Company), "معرّف الشركة يظهر في بايتات المِقبض.");
        Assert.False(Carries(raw, Session), "معرّف الجلسة يظهر في بايتات المِقبض.");
    }

    /// <summary>
    /// ولا غرضَ يُقرأ ولا يُقارَن: مِقبضان لغرضين على الموضوع نفسه لا يشتركان في
    /// بايتةٍ واحدة عند موضعٍ واحد بعد رأس النسخة.
    /// </summary>
    [Fact]
    public void لا_غرض_يُقرأ_من_المِقبض_ولا_يُقارَن_مِقبضان()
    {
        byte[] entity = DecodeWithoutAnyKey(Issue(KeyOne, LookupHandlePurpose.Entity, Subject));
        byte[] question = DecodeWithoutAnyKey(Issue(KeyOne, LookupHandlePurpose.Question, Subject));

        Assert.Equal(entity.Length, question.Length);

        int same = 0;
        for (int at = 1; at < entity.Length; at++)
        {
            if (entity[at] == question[at])
            {
                same++;
            }
        }

        // مصادفةٌ بايتيّة واردة (‏1/256 لكل موضع)؛ والتطابق الواسع ليس مصادفة.
        Assert.True(
            same < entity.Length / 4,
            "مِقبضان لغرضين مختلفين يتشابهان في " + same.ToString(CultureInfo.InvariantCulture) + " بايتة.");

        // ومِقبضان **للغرض والموضوع نفسيهما** يختلفان كذلك: nonce عشوائي لكل إصدار.
        Assert.NotEqual(
            Issue(KeyOne, LookupHandlePurpose.Entity, Subject),
            Issue(KeyOne, LookupHandlePurpose.Entity, Subject));
    }

    /// <summary>ومُصدِرٌ بمفتاحٍ آخر لا يفكّه — ولا يقرأ منه حقلاً قبل أن يرفض.</summary>
    [Fact]
    public void مفتاحٌ_آخر_لا_يفكّ_المِقبض()
    {
        string token = Issue(KeyOne, LookupHandlePurpose.Entity, Subject);

        Result<RedeemedLookupHandle> byTheRightKey =
            Handles(KeyOne).Redeem(token, LookupHandlePurpose.Entity, Tenant, Company, Session);

        Assert.True(byTheRightKey.IsSuccess);
        Assert.Equal(Subject, byTheRightKey.Value.Subject);

        Result<RedeemedLookupHandle> byAnotherKey =
            Handles(KeyTwo).Redeem(token, LookupHandlePurpose.Entity, Tenant, Company, Session);

        Assert.True(byAnotherKey.IsFailure, "مفتاحٌ آخر فكّ المِقبض.");
        Assert.Equal(LookupErrors.HandleNotSigned.Code, byAnotherKey.Errors[0].Code);

        // ‏**ومِقبضٌ بُدّلت فيه بايتةٌ واحدة يسقط كذلك** — والعلامة تُفحص قبل الفكّ.
        char[] tampered = token.ToCharArray();
        int at = Array.FindIndex(tampered, c => c != SignedLookupHandles.GroupSeparator);
        tampered[at] = tampered[at] == 'A' ? 'B' : 'A';

        Assert.True(Handles(KeyOne)
            .Redeem(new string(tampered), LookupHandlePurpose.Entity, Tenant, Company, Session)
            .IsFailure);
    }

    /// <summary>وطولُ المِقبض واحدٌ مهما كان غرضه أو موضوعه — فلا يُقاس منه شيء.</summary>
    [Fact]
    public void طولُ_المِقبض_واحد_مهما_كان_موضوعه()
    {
        List<string> minted =
        [
            Issue(KeyOne, LookupHandlePurpose.Entity, Subject),
            Issue(KeyOne, LookupHandlePurpose.Question, Guid.NewGuid()),
            Issue(KeyOne, LookupHandlePurpose.Option, Guid.NewGuid()),
            Issue(KeyOne, LookupHandlePurpose.CreateSheet, Guid.Empty),
        ];

        foreach (string token in minted)
        {
            Assert.Equal(SignedLookupHandles.TokenLength, token.Length);
        }

        Assert.Single(minted.Select(static token => token.Length).Distinct());
    }
}
