using Babel.Ai.Agent;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>ما مُنع من الجواب الواحد يُمنع من التكرار.</b>
/// <para>
/// جوابُ البحث لا يحمل عدداً — والنوع نفسه لا يحمله. لكنّ وكيلاً يبحث «محمد» ثم
/// «محمد ع» ثم «محمد عل» <b>يستخرج العدد بالتنصيف</b>. فالحاجز آلةُ حالةٍ في الخادم لا
/// جملةٌ في نصّ نظام: النموذج احتماليّ، وحاجزٌ يصمد تسعاً وتسعين من مئة ليس حاجزاً.
/// </para>
/// </summary>
public sealed class TheTurnStateClosesTheProbe
{
    /// <summary>السقف العددي — رابعٌ يمرّ وخامسٌ يُرفض.</summary>
    [Fact]
    public void سقفُ_البحث_في_الدور_يُغلق_بعد_أربع()
    {
        AgentTurnState state = new(4);

        for (int index = 0; index < 4; index++)
        {
            Assert.Null(state.RefuseLookup("customer", "اسم" + index));
            state.RecordLookup("customer", "اسم" + index, ambiguous: false);
        }

        Error? refusal = state.RefuseLookup("customer", "اسم خامس");

        Assert.NotNull(refusal);
        Assert.Equal("ai.agent.lookup_budget_spent", refusal.Code);
        Assert.Equal(4, state.LookupsMade);
    }

    /// <summary>وبعد الغموض: <c>ask_question</c> وحدها، لا بحثٌ ثانٍ بصياغةٍ أضيق.</summary>
    [Fact]
    public void بعد_الغموض_لا_بحث_ثانٍ_في_السجلّ_نفسه()
    {
        AgentTurnState state = new(4);
        state.RecordLookup("customer", "محمد القحطاني", ambiguous: true);

        Error? refusal = state.RefuseLookup("customer", "شركة أخرى تماماً");

        Assert.NotNull(refusal);
        Assert.Equal("ai.agent.ask_before_lookup_again", refusal.Code);
    }

    /// <summary>والحجر على ذلك السجلّ وحده — سجلٌّ آخر يمرّ.</summary>
    [Fact]
    public void الحجرُ_على_سجلّ_الغموض_وحده()
    {
        AgentTurnState state = new(4);
        state.RecordLookup("customer", "محمد القحطاني", ambiguous: true);

        Assert.Null(state.RefuseLookup("supplier", "مؤسسة النور"));
    }

    /// <summary>ويُرفع بعد أن يُسأل فعلاً.</summary>
    [Fact]
    public void الحجرُ_يُرفع_بعد_السؤال()
    {
        AgentTurnState state = new(4);
        state.RecordLookup("customer", "محمد القحطاني", ambiguous: true);
        state.RecordQuestionAnswered("customer");

        Assert.Null(state.RefuseLookup("customer", "شركة المسار الامثل"));
    }

    /// <summary>
    /// <b>وبحثان مفتاح أحدهما بادئةٌ صارمة للآخر يُرفضان سبراً</b> — والطيّ قبل المقارنة،
    /// فالتضييق بالتشكيل أو بالهمزة لا ينجو.
    /// </summary>
    [Theory]
    [InlineData("محمد", "محمد ع")]
    [InlineData("محمد ع", "محمد")]
    [InlineData("أحمد", "احمد الغامدي")]
    public void بحثان_أحدهما_بادئةُ_الآخر_يُرفضان(string first, string second)
    {
        AgentTurnState state = new(4);
        Assert.Null(state.RefuseLookup("customer", first));
        state.RecordLookup("customer", first, ambiguous: false);

        Error? refusal = state.RefuseLookup("customer", second);

        Assert.NotNull(refusal);
        Assert.Equal("ai.agent.lookup_probing_refused", refusal.Code);
    }

    /// <summary>واسمان مختلفان تماماً يمرّان — شاهدٌ موجب على أن القاعدة ليست منعاً شاملاً.</summary>
    [Fact]
    public void اسمان_مختلفان_يمرّان()
    {
        AgentTurnState state = new(4);
        Assert.Null(state.RefuseLookup("customer", "محمد القحطاني"));
        state.RecordLookup("customer", "محمد القحطاني", ambiguous: false);

        Assert.Null(state.RefuseLookup("customer", "شركة المسار الامثل"));
    }

    /// <summary>والمرفوض لم يقع: لا يُنقص من السقف ولا يُسجَّل في تاريخ الدور.</summary>
    [Fact]
    public void المرفوضُ_لا_يُحسب_في_السقف()
    {
        AgentTurnState state = new(1);
        state.RecordLookup("customer", "محمد", ambiguous: false);

        Assert.NotNull(state.RefuseLookup("customer", "شركة أخرى"));
        Assert.Equal(1, state.LookupsMade);
    }
}
