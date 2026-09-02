using Babel.Ai.Tests.Lookup;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>ورفضُ اسمٍ حقيقيّ عطلٌ آخر لا عطلٌ أصغر — وقصُّه أخبثُ منه لأنه يمرّ.</b>
/// <para>
/// أسماءٌ مشروعة تحمل كلماتٍ كانت القاعدة القديمة تراها ذاتَ دلالة: «مؤسسة <b>اليوم</b>
/// للدعاية» فيها كلمةُ تاريخ، و«شركة النور <b>على</b> البحر» فيها كلمةُ إيقاف. وكلماتُ
/// الإيقاف وكلماتُ التاريخ كانت تُنهي المقطع، فيخرج «مؤسسة» و«شركة النور» — <b>جذعان
/// عامّان</b>.
/// </para>
/// <para>
/// <b>ولماذا الجذع أسوأ من المقطع الطويل — مقيساً على قاعدةٍ حقيقية لا مُستنتَجاً:</b>
/// في منشأةٍ فيها «مؤسسة اليوم للدعاية» و«مؤسسة الرياض»، يُجيب السجلُّ على الجذع «مؤسسة»
/// بـ<b>صفٍّ واحد</b> — وهو <b>«مؤسسة الرياض»، أي الطرف الخطأ</b>. فالقصُّ لا يُنتج سؤالاً
/// يراه إنسان، بل <b>مِقبضاً صحيحاً لطرفٍ لم يُقصد</b> يعبر البوّابة بلا عطل. وهو العطل
/// الذي أُغلق، عائداً من باب «رفضِ اسمٍ حقيقي» بدل باب «اختيارِ طرفٍ ثالث».
/// <para>
/// والمقطع الأطول يتدهور تدهوراً لطيفاً بالمقابل: «الرئيسي اليوم»~«الرئيسي» = 0.667،
/// فيُحلّ صحيحاً أو يُسأل عنه.
/// </para>
/// </para>
/// </summary>
public sealed class ALegitimateNameIsNeverCutAtASignificantWord
{
    private static readonly (string Transcript, string Slot, string Span)[] Cases =
    [
        ("سجل سند قبض من العميل مؤسسة اليوم للدعاية بمبلغ ألف ريال نقد اليوم",
         "customer", "مؤسسة اليوم للدعاية"),
        ("سجل سند قبض من العميل شركة النور على البحر بمبلغ ألف ريال نقد اليوم",
         "customer", "شركة النور علي البحر"),
        ("سجل سند قبض من العميل مؤسسة الرياض للمقاولات بمبلغ ألف ريال نقد اليوم",
         "customer", "مؤسسة الرياض للمقاولات"),
    ];

    public static TheoryData<string> Transcripts()
    {
        TheoryData<string> data = [];
        foreach ((string transcript, _, _) in Cases)
        {
            data.Add(transcript);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Transcripts))]
    public void الاسم_المشروع_يُحمل_كاملاً_ولا_يُقصّ(string transcript)
    {
        (_, string slot, string span) = Cases.Single(entry => entry.Transcript == transcript);

        Result<VoiceResolution> read = SpokenCommandReader.Read(
            transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);

        SlotReading.Pending pending = Assert.IsType<SlotReading.Pending>(read.Value.Readings[slot]);
        Assert.Equal(span, pending.Span.Text);
    }

    /// <summary>
    /// <b>والاسم الكامل يُحلّ، والجذعُ الذي كان يُنتَج يطابق طرفاً آخر.</b> يُزرع الاسمان
    /// المشروعان معاً، فيُقاس أنّ المقطع الكامل يبلغ صفَّه بعينه، وأنّ «مؤسسة» وحدها —
    /// وهو ما كان القصُّ يُخرجه — <b>لا تبلغ صفّاً واحداً بل تلتبس</b>.
    /// </summary>
    [Fact]
    public async Task الجذع_الذي_كان_يُنتَج_يلتبس_والاسم_الكامل_يُحلّ()
    {
        await LookupTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = LookupTestEnvironment.SignificantWordTenant;

        Guid today = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة اليوم للدعاية", cancellationToken: TestContext.Current.CancellationToken);
        Guid riyadh = await LookupTestEnvironment.SeedCustomerAsync(
            tenant, "مؤسسة الرياض", cancellationToken: TestContext.Current.CancellationToken);

        Core.NameRegister.PostgresNameRegister register = LookupTestEnvironment.Register();

        NameCandidateProbe whole = await register.ProbeAsync(
            new NameCandidateRequest("مؤسسة اليوم للدعاية", tenant, Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Equal(NameCandidateCardinality.One, whole.Cardinality);
        Assert.Equal(today, whole.Only);

        // ‏**والجذع — وهو ما كان القصُّ يُخرجه — يبلغ صفّاً واحداً بعينه، وهو الصفّ الخطأ.**
        // ‏وهذا أسوأ من الالتباس: التباسٌ يُنتج سؤالاً يراه إنسان، وهذا يُنتج **مِقبضاً
        // صحيحاً لطرفٍ لم يُقصد** فيمرّ من البوّابة بلا عطلٍ واحد. وهو العطل بعينه عائداً
        // من باب «رفض اسمٍ حقيقي» بدل باب «اختيار طرفٍ ثالث».
        NameCandidateProbe stem = await register.ProbeAsync(
            new NameCandidateRequest("مؤسسة", tenant, Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Equal(NameCandidateCardinality.One, stem.Cardinality);
        Assert.Equal(riyadh, stem.Only);
        Assert.NotEqual(today, stem.Only);
    }
}
