using Babel.ControlPlane.Entitlement;
using Xunit;

namespace Babel.ControlPlane.Tests;

/// <summary>
/// <b>الأرضية: وحدة رحّلت قيوداً تُخفَّض ولا تُنتزَع.</b>
///
/// <para>بدأ هذا الملف مثبِّتاً للسلوك القائم — مجموعةٌ تنزع الأستاذ العام كانت
/// <b>متماسكة ومقبولة</b> — ثم قُلب فحصاً فحصاً مع تغيير السلوك.</para>
///
/// <para><b>السؤال المقيس:</b> هل تستطيع عمليةٌ مدعومة أن تقطع عن مستأجرٍ قراءة
/// دفتره؟ الجواب اليوم: <b>لا</b>. ونظام يحجز دفاتر منشأة رهينةَ سداد ليس نظاماً
/// تستطيع منشأة سعودية أن تعتمده: حفظ السجلات المحاسبية وإبرازها التزامٌ على
/// المنشأة، ونزاعٌ تجاري بيننا وبين عميل لا يجوز أن يضعه في مخالفة.</para>
/// </summary>
public class EntitlementFloorTests
{
    private static Dictionary<string, EntitlementState> All(EntitlementState s) =>
        ModuleCatalog.All.ToDictionary(m => m.Code, _ => s, StringComparer.Ordinal);

    // ── ١ · الانتقال لا المجموعة ────────────────────────────────────────────

    /// <summary>
    /// <b>كان مقبولاً، وصار مرفوضاً.</b> مستأجرٌ دفتره مستحقّ، وطُلب نزعه — أي
    /// نزع كل وحدة إلى <c>NotEntitled</c>. المجموعة نفسها ما تزال <b>متماسكة</b>
    /// بمقياس التماسك القديم؛ و<b>الانتقال إليها مرفوض</b>.
    /// </summary>
    [Fact]
    public void نزع_الأستاذ_العام_بعد_شرائه_مرفوض()
    {
        var current = All(EntitlementState.Entitled);
        var next = All(EntitlementState.NotEntitled);

        // المجموعة المطلوبة متماسكة في ذاتها — وهذا هو بيت الداء القديم.
        Assert.Empty(EntitlementValidator.Validate(next));

        var v = EntitlementValidator.ValidateTransition(current, next);
        Assert.Contains(v, x => x.ModuleCode == "CORE");
        Assert.All(v, x => Assert.False(string.IsNullOrWhiteSpace(x.MessageEn)));
    }

    /// <summary>
    /// <b>والمسار الصحيح مفتوح:</b> الخفض إلى <c>ReadOnly</c> مقبول، فالانقطاع
    /// له تعبير ولا يُدفع المشغّل إلى النزع لعدم وجود بديل.
    /// </summary>
    [Fact]
    public void الخفض_إلى_قراءة_فقط_مقبول()
    {
        var current = All(EntitlementState.Entitled);
        var next = All(EntitlementState.ReadOnly);

        Assert.Empty(EntitlementValidator.ValidateTransition(current, next));
    }

    /// <summary>
    /// والأرضية <b>لا تلزم قبل الشراء الأول</b>: مستأجرٌ جديد كل وحداته
    /// <c>NotEntitled</c>، وهي حالة مشروعة تماماً — الوحدة مخفيّة ولا سجلّ لها.
    /// </summary>
    [Fact]
    public void الوحدة_التي_لم_تُشترَ_قط_تبقى_غير_مستحقّة()
    {
        var fresh = All(EntitlementState.NotEntitled);

        Assert.Empty(EntitlementValidator.ValidateTransition(fresh, fresh));
    }

    /// <summary>
    /// <b>الجرد الصريح:</b> ثماني وحدات تُرحّل قيوداً فأرضيتها <c>ReadOnly</c>،
    /// وواحدة لا تُرحّل قيوداً فتُنزَع فعلاً. والقائمة جردٌ لا حدّ أعلى: وحدةٌ
    /// جديدة تدخلها بقرار واعٍ.
    /// </summary>
    [Fact]
    public void جدول_ما_يُنزَع_وما_يُخفَّض_جردٌ_صريح()
    {
        var degradableOnly = ModuleCatalog.All
            .Where(m => ModuleCatalog.MayOnlyBeDegraded(m.Code))
            .Select(m => m.Code).OrderBy(x => x, StringComparer.Ordinal).ToList();

        var revocable = ModuleCatalog.All
            .Where(m => !ModuleCatalog.MayOnlyBeDegraded(m.Code))
            .Select(m => m.Code).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(["AP", "AR", "CORE", "FA", "INV", "PAY", "POS", "PRJ"], degradableOnly);
        Assert.Equal(["REP"], revocable);
    }

    /// <summary>
    /// وأرضية الوحدة لا تتجاوز أرضية اعتمادياتها — وإلا وُجدت مجموعةٌ لا يبلغها
    /// أي خفض مشروع.
    /// </summary>
    [Fact]
    public void أرضية_الوحدة_لا_تتجاوز_أرضية_اعتمادياتها()
    {
        foreach (var m in ModuleCatalog.All)
            foreach (var dep in m.DependsOn)
                Assert.True(ModuleCatalog.FloorOf(dep) >= m.Floor,
                    $"{m.Code} أرضيتها {m.Floor} واعتماديتها {dep} أرضيتها {ModuleCatalog.FloorOf(dep)}");
    }

    // ── ٢ · الخفض المحسوب ───────────────────────────────────────────────────

    /// <summary>
    /// <b>انقطاع السداد الكامل:</b> كل وحدة تُرحّل قيوداً تهبط إلى <c>ReadOnly</c>،
    /// والتقارير التحليلية وحدها تُنزَع. والناتج <b>متماسك</b> بلا تدخّل.
    /// </summary>
    [Fact]
    public void انقطاع_السداد_يهبط_بالكل_إلى_أرضيته_لا_إلى_العدم()
    {
        var current = All(EntitlementState.Entitled);
        var next = EntitlementValidator.Degrade(current, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(EntitlementState.ReadOnly, next["CORE"]);
        Assert.Equal(EntitlementState.ReadOnly, next["PAY"]);
        Assert.Equal(EntitlementState.NotEntitled, next["REP"]);
        Assert.Empty(EntitlementValidator.ValidateTransition(current, next));
    }

    /// <summary>
    /// <b>خفض الحزمة:</b> عميلٌ على الشاملة ينزل إلى الأساسية. ما تغطّيه الأساسية
    /// يبقى مستحقّاً، وما خرج منها يهبط إلى <c>ReadOnly</c> — <b>ولا يُنزَع</b>.
    /// ونقاط البيع تُقصّ إلى حالة اعتماديتها فلا تبيع بلا حركة مخزون.
    /// </summary>
    [Fact]
    public void خفض_الحزمة_يُبقي_المخرَج_من_الحزمة_مقروءاً()
    {
        var current = All(EntitlementState.Entitled);
        var essential = PlanCatalog.Require("ESSENTIAL");
        var covered = new HashSet<string>(essential.Modules, StringComparer.Ordinal);
        foreach (var m in essential.Modules)
            foreach (var d in ModuleCatalog.TransitiveDependencies(m)) covered.Add(d);

        var next = EntitlementValidator.Degrade(current, covered);

        Assert.Equal(EntitlementState.Entitled, next["CORE"]);
        Assert.Equal(EntitlementState.Entitled, next["AR"]);
        Assert.Equal(EntitlementState.ReadOnly, next["INV"]);
        Assert.Equal(EntitlementState.ReadOnly, next["POS"]);
        Assert.Equal(EntitlementState.NotEntitled, next["REP"]);
        Assert.Empty(EntitlementValidator.ValidateTransition(current, next));
    }

    // ── ٣ · جدول القرار وموضعه الوحيد ───────────────────────────────────────

    /// <summary>
    /// جدول القرار كاملاً: القراءة تعمل في <c>ReadOnly</c>، والكتابة لا.
    /// وهو الفحص الذي يجعل نسخةً ثانية من الجدول في أي وحدة <b>خطأً مرئياً</b>.
    /// </summary>
    [Theory]
    [InlineData(EntitlementState.Entitled, AccessIntent.Read, true)]
    [InlineData(EntitlementState.Entitled, AccessIntent.Write, true)]
    [InlineData(EntitlementState.ReadOnly, AccessIntent.Read, true)]
    [InlineData(EntitlementState.ReadOnly, AccessIntent.Write, false)]
    [InlineData(EntitlementState.NotEntitled, AccessIntent.Read, false)]
    [InlineData(EntitlementState.NotEntitled, AccessIntent.Write, false)]
    public void جدول_القرار_موضعٌ_واحد(EntitlementState state, AccessIntent intent, bool allowed) =>
        Assert.Equal(allowed, EntitlementRules.Allows(state, intent));

    // ── ٤ · الرفض يُقال بلغتين ولا يتنكّر في عطل تقني ───────────────────────

    /// <summary>
    /// <b>كان بالعربية وحدها، وصار بلغتين.</b> ونصّه يُسمّي <b>السبب</b> —
    /// انقطاع الاشتراك — ويقول صراحةً ما الذي <b>يبقى</b> متاحاً، فلا يقرؤه أحد
    /// عطلاً تقنياً.
    /// </summary>
    [Fact]
    public void رفض_الكتابة_على_وحدة_للقراءة_فقط_يُقال_بلغتين_ويُسمّي_السبب()
    {
        var ex = new EntitlementDeniedException(
            "T-1", "CORE", EntitlementState.ReadOnly, AccessIntent.Write);

        Assert.Equal("entitlement.read_only", ex.Code);
        Assert.Contains("انقطاع الاشتراك", ex.MessageAr, StringComparison.Ordinal);
        Assert.Contains("القراءة والتقارير", ex.MessageAr, StringComparison.Ordinal);
        Assert.Contains("subscription has lapsed", ex.MessageEn, StringComparison.Ordinal);
        Assert.Contains("remain fully available", ex.MessageEn, StringComparison.Ordinal);

        // والنصّ الكامل يحمل الرمز واللغتين — نفس شكل Babel.SharedKernel.Error.
        Assert.Contains(ex.MessageAr, ex.Message, StringComparison.Ordinal);
        Assert.Contains(ex.MessageEn, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ووحدة لم تُشترَ قط تُرفض بسببها هي، لا بسبب انقطاع لم يقع.</summary>
    [Fact]
    public void رفض_وحدة_لم_تُشترَ_يُسمّي_سببه_هو()
    {
        var ex = new EntitlementDeniedException(
            "T-1", "REP", EntitlementState.NotEntitled, AccessIntent.Read);

        Assert.Equal("entitlement.not_entitled", ex.Code);
        Assert.Contains("لم تُشترَ", ex.MessageAr, StringComparison.Ordinal);
        Assert.Contains("never purchased", ex.MessageEn, StringComparison.Ordinal);
    }

    /// <summary>وكل مخالفة أرضية تحمل اللغتين — لا مخالفة بنصف بيان.</summary>
    [Fact]
    public void كل_مخالفة_تحمل_اللغتين()
    {
        var v = EntitlementValidator.ValidateTransition(
            All(EntitlementState.Entitled), All(EntitlementState.NotEntitled));

        Assert.NotEmpty(v);
        Assert.All(v, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.MessageAr));
            Assert.False(string.IsNullOrWhiteSpace(x.MessageEn));
        });
    }
}
