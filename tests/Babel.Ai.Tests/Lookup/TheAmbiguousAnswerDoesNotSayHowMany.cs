using System.Globalization;
using System.Reflection;
using Babel.Ai.Lookup;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Lookup;

/// <summary>
/// <b>الغموض لا يُقاس منه عدد — لا بحقل، ولا بطول قائمة، ولا بحجم الجواب.</b>
/// <para>
/// <b>والخطر الحقيقيّ ليس جواباً واحداً بل التكرار:</b> من يسأل «محمد» ثم «محمد ع» ثم
/// «محمد عل» ويقرأ عدداً في كل مرّة يكون قد بحث في دفتر العملاء بحثاً ثنائياً. ولذلك لا
/// يكفي حذف الحقل من الاستجابة.
/// </para>
/// <para>
/// <b>ثلاث طبقات، وكلٌّ منها تُثبَت هنا:</b>
/// ‏(١) <b>العدد لا يُحسب</b> — الاستعلام ينتهي بـ<c>limit 2</c>؛
/// ‏(٢) <b>ولا نوع في المسار يستطيع حمله</b> — <c>NameCandidateProbe</c> ثلاث حالات وقيمة،
/// و<c>NameLookupResult</c> ثلاثة حقول؛
/// ‏(٣) <b>وطول البايتات ثابت</b> — جوابُ اثنين وجوابُ خمسين متطابقان طولاً.
/// </para>
/// </summary>
public sealed class TheAmbiguousAnswerDoesNotSayHowMany
{
    private static Guid Company => new("c0000000-0000-4000-8000-000000000002");

    private static Guid Session => new("5e551000-0000-4000-8000-000000000002");

    /// <summary>
    /// الطبقة الثالثة، وهي المقيسة: 2 ثم 3 ثم 7 ثم 50 مرشّحاً في المنشأة نفسها،
    /// والجواب في المرّات الأربع <b>غامضٌ وطولُه بالبايت واحد</b>.
    /// </summary>
    [Fact]
    public async Task TwoCandidatesAndFiftyProduceAnswersOfIdenticalLength()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.ScaleTenant;

        NameRegisterLookup lookup = new(
            [LookupTestEnvironment.Register()],
            LookupTestEnvironment.Handles(),
            LookupTestEnvironment.Options);

        LookupSession session = new(tenant, Company, Session);
        List<int> lengths = [];
        int planted = 0;

        foreach (int population in (int[])[2, 3, 7, 50])
        {
            while (planted < population)
            {
                planted++;
                await LookupTestEnvironment.SeedCustomerAsync(
                    tenant,
                    "محمد " + planted.ToString(CultureInfo.InvariantCulture) + " القحطاني",
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            Result<NameLookupResult> answer = await lookup.ResolveAsync(
                "customer", "محمد القحطاني", session, TestContext.Current.CancellationToken);

            Assert.True(answer.IsSuccess);
            Assert.Equal(NameLookupOutcome.NeedsQuestion, answer.Value.Outcome);

            lengths.Add(System.Text.Encoding.UTF8.GetByteCount(NameLookupWire.Write(answer.Value)));
        }

        Assert.Equal(4, lengths.Count);
        Assert.Single(lengths.Distinct());
    }

    /// <summary>
    /// الطبقة الثانية: <b>لا عضو في مسار الجواب يحمل عدداً أو درجة</b>. حارسٌ بالانعكاس
    /// لأن الحقل الذي يُضاف «للتشخيص» غداً هو بالضبط ما يُسرَّب.
    /// </summary>
    [Fact]
    public void NoTypeOnThePathCarriesACountOrAScore()
    {
        string[] forbidden =
        [
            "count", "candidatecount", "total", "score", "confidence",
            "topmatch", "similarity", "names", "candidates", "matches",
        ];

        foreach (Type type in (Type[])[typeof(NameLookupResult), typeof(NameCandidateProbe)])
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(property.Name.ToLowerInvariant(), forbidden);
            }
        }

        // ومجموعة المفاتيح واحدة في الحالات الثلاث — الشكل وحده لا يفرّق بينها.
        Assert.Equal(3, typeof(NameLookupResult).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.Equal(2, typeof(NameCandidateProbe).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    /// <summary>
    /// الطبقة الأولى: <b>العدد لا يُطرح على القاعدة أصلاً</b>. نصّ استعلام السبر معلَن
    /// ليُقرأ — فالحارس يُرى ولا يُوصف — وينتهي بـ<c>limit 2</c> ولا يحمل <c>count(</c>.
    /// </summary>
    [Fact]
    public void TheProbeQueryStopsAtTwoRowsAndCountsNothing()
    {
        string sql = LookupTestEnvironment.Register().ProbeCommandText;

        Assert.EndsWith("limit 2", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("count(", sql, StringComparison.OrdinalIgnoreCase);

        // ولا ترتيب بالدرجة: «أفضل تطابق» قرارٌ لا يُتّخذ هنا بحال.
        Assert.DoesNotContain("order by similarity", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>والشكل السلكيّ نفسه: مفاتيح متطابقة وترتيبٌ واحد في الحالات الثلاث.</b>
    /// فحالةُ «لا شيء» وحالةُ السؤال لا تفترقان في مجموعة المفاتيح ولا في ترتيبها.
    /// </summary>
    [Fact]
    public void TheWireShapeIsTheSameInAllThreeOutcomes()
    {
        string token = new('A', SignedLookupHandles.TokenLength);

        string none = NameLookupWire.Write(NameLookupResult.None);
        string resolved = NameLookupWire.Write(NameLookupResult.Resolved(token));
        string asks = NameLookupWire.Write(NameLookupResult.NeedsQuestion(token));

        foreach (string wire in (string[])[none, resolved, asks])
        {
            Assert.StartsWith("{\"outcome\":\"", wire, StringComparison.Ordinal);
            Assert.Contains(",\"handle\":", wire, StringComparison.Ordinal);
            Assert.Contains(",\"questionId\":", wire, StringComparison.Ordinal);
            Assert.EndsWith("}", wire, StringComparison.Ordinal);
        }

        // والمِقبض ومعرّف الورقة طولهما واحد، فالحالتان اللتان تحملان قيمةً متساويتان طولاً.
        Assert.Equal(resolved.Length - "resolved".Length, asks.Length - "needs_question".Length);
    }
}
