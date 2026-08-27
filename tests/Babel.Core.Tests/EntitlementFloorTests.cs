using System.Globalization;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>الأرضية عند حدّ الإنفاذ داخل العملية — والجدول الصريح لما هو قابل للتمثيل.</b>
///
/// <para>هذا هو «الاختبار الذي يجب أن يوجد» في
/// <c>docs/evidence/traps.md#fakh-mandatory-module-cannot-be-read-only</c>:
/// يُثبت <b>صراحةً</b> أي الحالات قابلة للتمثيل لكل وحدة، فيصير القيد
/// <b>قراراً مرئياً</b> لا أثراً جانبياً لشرط تحقّق.</para>
///
/// <para><b>الوقاية المُنفَّذة هي الخيار الأول من الثلاثة المذكورة في الفخّ:</b>
/// تمييز «إلزامية» عن «لا تُطفأ». الوحدة الإلزامية لا تبلغ <c>NotEntitled</c>
/// أبداً، ولكنها <b>تبلغ <c>ReadOnly</c></b> — وهو ما يصف الواقع التجاري
/// والقانوني معاً: العميل يبقى قادراً على قراءة دفتره وإخراج تقاريره وتقديم
/// إقراره، ولا يُنشئ مستنداً جديداً ولا يرحّل قيداً.</para>
/// </summary>
public sealed class EntitlementFloorTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    // ── ١ · ما صار قابلاً للتمثيل: الاشتراك المنقطع ─────────────────────────

    /// <summary>
    /// <b>كان مرفوضاً، وصار مقبولاً.</b> خفض <b>الدفتر</b> إلى <c>ReadOnly</c>
    /// هو الحالة التي وُجد <c>ReadOnly</c> من أجلها، وكانت غير قابلة للتمثيل.
    /// والقراءة بعده تعمل، والكتابة وحدها تتوقف.
    /// </summary>
    [Fact]
    public void خفض_الدفتر_إلى_قراءة_فقط_مقبول_والقراءة_تبقى()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Ledger] = EntitlementState.ReadOnly,
                [BabelModule.Sales] = EntitlementState.ReadOnly,
                [BabelModule.Purchasing] = EntitlementState.ReadOnly,
                [BabelModule.Compliance] = EntitlementState.ReadOnly,
                [BabelModule.Core] = EntitlementState.ReadOnly,
            });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Allows(BabelModule.Ledger, EntitlementAccess.Read));
        Assert.False(result.Value.Allows(BabelModule.Ledger, EntitlementAccess.Write));
    }

    /// <summary>
    /// <b>لم يتغيّر:</b> نزع الدفتر كلّياً ما زال مرفوضاً — بالرمز نفسه. «إلزامية»
    /// تعني «لا تُطفأ»، وهذا هو المعنى الذي بقي.
    /// </summary>
    [Fact]
    public void نزع_الدفتر_ما_يزال_مرفوضاً_بالرمز_نفسه()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Ledger] = EntitlementState.NotEntitled,
            });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "entitlement.mandatory_disabled");
    }

    // ── ٢ · ما صار ممنوعاً: نزع سجلٍّ محاسبي بعد شرائه ───────────────────────

    /// <summary>
    /// <b>كان مقبولاً، وصار مرفوضاً.</b> سجلّ الأصول الثابتة وحدةٌ غير إلزامية،
    /// فكان يُنزَع بعد شرائه <b>فتُقطَع قراءته</b>. صار يُخفَّض ولا يُنزَع.
    /// </summary>
    [Fact]
    public void نزع_سجلّ_الأصول_بعد_شرائه_مرفوض_والخفض_مقبول()
    {
        EntitlementSet bought = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.Entitled,
            }).Value;

        Result<EntitlementSet> revoked = bought.With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.NotEntitled,
            });

        Assert.True(revoked.IsFailure);
        Assert.Contains(revoked.Errors, e => e.Code == "entitlement.record_bearing_revoked");

        Result<EntitlementSet> degraded = bought.With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.ReadOnly,
            });

        Assert.True(degraded.IsSuccess);
        Assert.True(degraded.Value.Allows(BabelModule.Assets, EntitlementAccess.Read));
        Assert.False(degraded.Value.Allows(BabelModule.Assets, EntitlementAccess.Write));
    }

    /// <summary>
    /// <b>كان لا يفرّق، وصار يفرّق.</b> «لم تُشترَ قط» و«اشتُريت ثم نُزعت»
    /// جملتان مختلفتان: الأولى مقبولة (الوحدة مخفيّة ولا سجلّ لها)، والثانية
    /// مرفوضة (السجلّ موجود ولا يُحجَب عن صاحبه).
    /// </summary>
    [Fact]
    public void الحالة_السابقة_تدخل_الحكم_الآن()
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
        Assert.True(boughtThenRevoked.IsFailure);
    }

    /// <summary>
    /// والأرضية <b>لا تُصعِّد</b>: وحدةٌ عند <c>ReadOnly</c> تبقى قابلة للبقاء
    /// عند <c>ReadOnly</c>، وترقّيها إلى <c>Entitled</c> حرّ. القيد نزولي فقط.
    /// </summary>
    [Fact]
    public void الأرضية_تلزم_النزول_وحده_لا_الصعود()
    {
        EntitlementSet lapsed = EntitlementSet.Baseline(Tenant).With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.ReadOnly,
            }).Value;

        Result<EntitlementSet> resumed = lapsed.With(
            new Dictionary<BabelModule, EntitlementState>
            {
                [BabelModule.Assets] = EntitlementState.Entitled,
            });

        Assert.True(resumed.IsSuccess);
        Assert.True(resumed.Value.Allows(BabelModule.Assets, EntitlementAccess.Write));
    }

    // ── ٣ · الجدول الصريح: أي الحالات قابلة للتمثيل لكل وحدة ────────────────

    /// <summary>
    /// <b>الجدول المطلوب في الفخّ، مُنفَّذاً.</b> لكل وحدة: هل تبلغ
    /// <c>NotEntitled</c> بعد أن تكون قد اشتُريت؟ الجواب <c>false</c> لكل وحدة
    /// يقوم عليها سجلّ محاسبي، و<c>true</c> لأداة الالتقاط وحدها.
    ///
    /// <para>والقائمة <b>جرد صريح لا حدّ أعلى</b>: وحدةٌ جديدة تُضاف هنا بقرار
    /// واعٍ — وهو ما يمنع ظهور وحدة تحمل سجلاً محاسبياً وتُنزَع دون أن يراها أحد.</para>
    /// </summary>
    [Fact]
    public void جدول_ما_يُنزَع_وما_يُخفَّض_جردٌ_صريح()
    {
        List<BabelModule> revocable = [];
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            if (ModuleDependencyGraph.FloorOf(module) == EntitlementState.NotEntitled)
            {
                revocable.Add(module);
            }
        }

        Assert.Equal([BabelModule.Ai], revocable);
    }

    // ── ٤ · محور الفوترة: يُلتقَط ولا يُحتسَب ────────────────────────────────

    /// <summary>
    /// <b>حالة الاستحقاق تُلتقَط على محور المستخدم — والعدّ الافتراضي لم يتغيّر.</b>
    ///
    /// <para>مستخدمان: أحدهما على وحدة مستحقّة والآخر على وحدة للقراءة فقط.
    /// الحدث المكتوب يحمل حالة كلٍّ منهما، فيصير «هل يُعدّ من يقرأ فقط؟» سؤالاً
    /// <b>قابلاً للحساب بأثر رجعي</b>. وحتى يُحسم، <c>GetActiveUsersAsync</c>
    /// يُعيد <b>الاثنين</b> كما كان — لم يتحرّك رقمٌ تجاري.</para>
    /// </summary>
    [Fact]
    public async Task حالة_الاستحقاق_تُلتقَط_على_محور_المستخدم_ولا_تُغيّر_العدّ()
    {
        UserId reader = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        UserId writer = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));

        InMemoryUsageStore store = new();
        DateTimeOffset now = DateTimeOffset.Parse("2026-08-27T10:00:00Z", CultureInfo.InvariantCulture);

        await store.RecordUserActivityAsync(new UserActivityEvent(
            Tenant, writer, BabelModule.Ledger, "Ledger.Post", now, EntitlementState.Entitled), TestContext.Current.CancellationToken);
        await store.RecordUserActivityAsync(new UserActivityEvent(
            Tenant, reader, BabelModule.Ledger, "Ledger.TrialBalance", now, EntitlementState.ReadOnly), TestContext.Current.CancellationToken);

        IReadOnlyCollection<UserId> active =
            await store.GetActiveUsersAsync(Tenant, BillingPeriod.FromInstant(now), TestContext.Current.CancellationToken);

        Assert.Equal(2, active.Count);
        Assert.Contains(reader, active);
        Assert.Contains(writer, active);
    }

    /// <summary>
    /// وحالة الاستحقاق حقلٌ <b>إلزامي</b> على الحدث، فلا تُنسى ولا تُملأ بافتراض
    /// صامت: مؤلّف مسار قياسٍ جديد <b>مضطرّ</b> إلى أن يقرّر.
    /// </summary>
    [Fact]
    public void حالة_الاستحقاق_حقل_إلزامي_لا_افتراض_صامت()
    {
        System.Reflection.ParameterInfo state = typeof(UserActivityEvent)
            .GetConstructors()[0].GetParameters()
            .Single(p => p.Name == "State");

        Assert.False(state.HasDefaultValue);
        Assert.Equal(typeof(EntitlementState), state.ParameterType);
    }

    /// <summary>
    /// وأرضية كل وحدة <b>لا تتجاوز</b> أرضية ما تعتمد عليه: أرضيةٌ أعلى فوق
    /// اعتمادية أدنى تُنتج مجموعةً لا يمكن الوصول إليها بأي خفض مشروع.
    /// </summary>
    [Fact]
    public void أرضية_الوحدة_لا_تتجاوز_أرضية_اعتمادياتها()
    {
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            foreach (BabelModule requirement in ModuleDependencyGraph.RequirementsOf(module))
            {
                Assert.True(
                    ModuleDependencyGraph.FloorOf(requirement) >= ModuleDependencyGraph.FloorOf(module),
                    $"{module} أرضيتها {ModuleDependencyGraph.FloorOf(module)} "
                    + $"واعتماديتها {requirement} أرضيتها {ModuleDependencyGraph.FloorOf(requirement)}");
            }
        }
    }
}
