using System.Globalization;
using System.Text.RegularExpressions;
using Babel.Ai.Lookup;
using Babel.Ai.Tests.Support;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>عقدُ المتصفّح كان يصف رمزاً لا يسكّه أحد.</b>
/// <para>
/// ‏<c>web/src/agent/sheet.ts</c> يبني عليه المكوّن كلّه، وتجهيزتُه كانت تُهيّئ رموزاً
/// من <b>ثمانٍ وثمانين</b> خانة وتكتب ذلك في تعليقٍ صريح — بينما المسار الوحيد الذي
/// يسكّ رمزاً في الخادم يُخرج طولاً آخر. فكانت «الأطوال واحدة فلا يُقاس منها العدد»
/// خاصّيةً مُثبَتةً على <b>عددٍ لا يُنتجه شيء</b>.
/// </para>
/// <para>
/// وهذا الحارس هو <c>TheBrowserCatalogueMirrorsTheServer</c> نفسه مطبَّقاً على شكل
/// الرمز: يقرأ الملفّ نفسه — لا وصفاً له — ويطابق الأعداد الثلاثة.
/// </para>
/// </summary>
public sealed class TheBrowserTokenShapeMirrorsTheServer
{
    private const string SheetModule = "web/src/agent/sheet.ts";

    private static string Source() => File.ReadAllText(RepositoryRoot.At(SheetModule));

    private static string Declared(string name)
    {
        Match match = Regex.Match(
            Source(),
            @"export const " + Regex.Escape(name) + @" = (?<value>[^;]+);",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "الثابت «" + name + "» غير معلَن في " + SheetModule);
        return match.Groups["value"].Value.Trim();
    }

    /// <summary>طول الرمز في المتصفّح هو طوله في الخادم.</summary>
    [Fact]
    public void TheDeclaredTokenLengthIsTheOneTheServerMints()
    {
        Assert.Equal(
            SignedLookupHandles.TokenLength.ToString(CultureInfo.InvariantCulture),
            Declared("AGENT_TOKEN_LENGTH"));
    }

    /// <summary>وحجم المجموعة وفاصلها كذلك.</summary>
    [Fact]
    public void TheDeclaredGroupShapeIsTheOneTheServerMints()
    {
        Assert.Equal(
            SignedLookupHandles.GroupLength.ToString(CultureInfo.InvariantCulture),
            Declared("AGENT_TOKEN_GROUP_LENGTH"));

        Assert.Equal(
            "\"" + SignedLookupHandles.GroupSeparator + "\"",
            Declared("AGENT_TOKEN_GROUP_SEPARATOR"));
    }

    /// <summary>
    /// <b>والقياس على رمزٍ مسكوكٍ فعلاً لا على العدد وحده:</b> طولُه، ومجموعاته،
    /// وفاصلُه — الثلاثة كما يعلنها المتصفّح.
    /// </summary>
    [Fact]
    public void AMintedTokenHasExactlyTheShapeTheBrowserExpects()
    {
        byte[] key = new byte[32];
        for (int index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(index + 3);
        }

        string token = new SignedLookupHandles(key, new LookupOptions(), TimeProvider.System)
            .Issue(
                LookupHandlePurpose.Option,
                new Babel.SharedKernel.TenantId(new Guid("100c0a5e-0000-4000-8000-0000000000aa")),
                new Guid("c0000000-0000-4000-8000-0000000000bb"),
                new Guid("5e551000-0000-4000-8000-0000000000cc"),
                new Guid("c5700000-0000-4000-8000-0000000000dd"),
                TimeSpan.FromMinutes(10))
            .Value;

        Assert.Equal(SignedLookupHandles.TokenLength, token.Length);

        string[] groups = token.Split(SignedLookupHandles.GroupSeparator);
        Assert.All(groups, group => Assert.Equal(SignedLookupHandles.GroupLength, group.Length));
        Assert.Equal(
            SignedLookupHandles.TokenLength,
            (groups.Length * SignedLookupHandles.GroupLength) + groups.Length - 1);
    }
}
