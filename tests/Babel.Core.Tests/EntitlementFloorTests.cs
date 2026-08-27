using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>الأرضية عند حدّ الإنفاذ داخل العملية.</b>
///
/// <para>يبدأ هذا الملف <b>مثبِّتاً للسلوك القائم</b>: كل فحص فيه قِيس على
/// الشيفرة كما هي. والسؤال المقيس سؤالان لا واحد:</para>
///
/// <list type="number">
/// <item>هل يستطيع اشتراكٌ منقطع أن يُخفَّض على <b>الدفتر</b> إلى قراءة فقط؟</item>
/// <item>وهل تستطيع مجموعة مقبولة أن تقطع <b>القراءة</b> عن سجلٍّ محاسبي؟</item>
/// </list>
/// </summary>
public sealed class EntitlementFloorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    /// <summary>
    /// <b>مقيس على الشيفرة القائمة:</b> خفض <b>الدفتر</b> إلى <c>ReadOnly</c>
    /// <b>مرفوض</b> بـ<c>entitlement.mandatory_disabled</c>.
    ///
    /// <para>وهذا هو العطب الأصلي: «إلزامية» مُنفَّذة بمعنى «<b>يجب أن تكون
    /// Entitled</b>»، فالحالة الوسطى التي وُضعت من أجل انقطاع السداد
    /// (‏<c>ReadOnly</c>) <b>غير قابلة للتمثيل على الوحدة التي تهمّ أكثر من
    /// غيرها</b>. أي أن الاشتراك المنقطع إمّا يبقى كامل الكتابة أو لا يكون.</para>
    /// </summary>
    [Fact]
    public void خفض_الدفتر_إلى_قراءة_فقط_مرفوض_اليوم()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Ledger] = EntitlementState.ReadOnly,
            });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "entitlement.mandatory_disabled");
    }

    /// <summary>
    /// <b>مقيس:</b> ونزع الدفتر كلّياً مرفوض أيضاً — بالرمز نفسه. فالخياران
    /// المتاحان للمشغّل على الدفتر هما «‏Entitled» أو «مجموعة مرفوضة»، ولا ثالث.
    /// </summary>
    [Fact]
    public void نزع_الدفتر_مرفوض_اليوم_بالرمز_نفسه()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Ledger] = EntitlementState.NotEntitled,
            });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "entitlement.mandatory_disabled");
    }

    /// <summary>
    /// <b>مقيس — وهو الوجه الثاني للعطب:</b> سجلّ الأصول الثابتة
    /// (<see cref="BabelModule.Assets"/>) وحدةٌ <b>غير إلزامية</b>، فيُقبل نزعها
    /// إلى <c>NotEntitled</c> بعد أن كانت مشتراة. وبعد النزع <b>تُرفض القراءة
    /// أيضاً</b>: المستأجر يفقد إخراج سجلّ أصوله، وهو سجلّ محاسبي واجب الحفظ
    /// والإبراز لا ميزة كمالية.
    /// </summary>
    [Fact]
    public void نزع_سجلّ_الأصول_بعد_شرائه_مقبول_اليوم_ويقطع_القراءة()
    {
        Result<EntitlementSet> bought = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.Entitled,
            });
        Assert.True(bought.IsSuccess);
        Assert.True(bought.Value.Allows(BabelModule.Assets, EntitlementAccess.Read));

        Result<EntitlementSet> revoked = bought.Value.With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.NotEntitled,
            });

        Assert.True(revoked.IsSuccess);
        Assert.False(revoked.Value.Allows(BabelModule.Assets, EntitlementAccess.Read));
    }

    /// <summary>
    /// <b>مقيس:</b> ولا شيء في النموذج يفرّق بين «لم تُشترَ قط» و«اشتُريت ثم
    /// نُزعت»: <see cref="EntitlementSet.With"/> يبني الناتج ويتحقّق منه وحده،
    /// فالحالة السابقة لا تدخل الحكم أصلاً.
    /// </summary>
    [Fact]
    public void الحالة_السابقة_لا_تدخل_الحكم_اليوم()
    {
        Result<EntitlementSet> neverBought = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.NotEntitled,
            });

        Result<EntitlementSet> boughtThenRevoked = EntitlementSet.Baseline(Tenant)
            .With(new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.Entitled,
            }).Value
            .With(new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.NotEntitled,
            });

        Assert.True(neverBought.IsSuccess);
        Assert.True(boughtThenRevoked.IsSuccess);
        Assert.Equal(
            neverBought.Value.StateOf(BabelModule.Assets),
            boughtThenRevoked.Value.StateOf(BabelModule.Assets));
    }
}
