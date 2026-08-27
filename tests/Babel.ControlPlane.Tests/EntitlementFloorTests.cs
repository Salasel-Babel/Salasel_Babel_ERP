using Babel.ControlPlane.Entitlement;
using Xunit;

namespace Babel.ControlPlane.Tests;

/// <summary>
/// <b>الأرضية: وحدة رحّلت قيوداً تُخفَّض ولا تُنتزَع.</b>
///
/// <para>هذا الملف يبدأ حياته <b>مثبِّتاً للسلوك القائم</b> قبل تغييره: كل فحص
/// فيه قِيس على الشيفرة كما هي، لا على الشيفرة كما نريدها. ثم يُقلَب فحصاً
/// فحصاً مع تغيير السلوك، فيُظهر الفرق <b>ما تغيّر</b> لا ما نتمنّاه.</para>
///
/// <para><b>السؤال المقيس:</b> هل تستطيع مجموعة استحقاق مقبولة أن تقطع عن
/// مستأجرٍ <b>قراءة</b> دفتره؟ الجواب اليوم: نعم.</para>
/// </summary>
public class EntitlementFloorTests
{
    private static Dictionary<string, EntitlementState> All(EntitlementState s) =>
        ModuleCatalog.All.ToDictionary(m => m.Code, _ => s, StringComparer.Ordinal);

    // ── ١ · الحالة القائمة: الأستاذ العام يُنتزَع انتزاعاً ────────────────────

    /// <summary>
    /// <b>مقيس على الشيفرة القائمة:</b> مجموعةٌ تنزع كل وحدة — <b>ومنها الأستاذ
    /// العام</b> — إلى <c>NotEntitled</c> <b>متماسكة ومقبولة</b>. ومعنى قبولها
    /// أن <c>EntitlementService.ApplyAsync</c> يُثبّتها، وأن
    /// <c>EntitlementGuard.RequireReadAsync("CORE")</c> يرمي بعدها — أي أن
    /// المستأجر يفقد <b>قراءة دفتره هو</b> بعملية مدعومة لا بعطل.
    /// </summary>
    [Fact]
    public void نزع_الأستاذ_العام_كلّياً_مقبول_اليوم()
    {
        var set = All(EntitlementState.NotEntitled);

        Assert.Equal(EntitlementState.NotEntitled, set["CORE"]);
        Assert.Empty(EntitlementValidator.Validate(set));
    }

    /// <summary>
    /// <b>مقيس:</b> لا وحدة واحدة في الكتالوج محميّة من النزع. المُتحقِّق دالّة
    /// في المجموعة المطلوبة وحدها، ولا يعرف الحالة السابقة، فلا يميّز
    /// «لم تُشترَ قط» من «اشتُريت ثم نُزعت».
    /// </summary>
    [Fact]
    public void لا_وحدة_محميّة_من_النزع_اليوم()
    {
        var revocable = ModuleCatalog.All
            .Where(m =>
            {
                var set = All(EntitlementState.NotEntitled);
                set[m.Code] = EntitlementState.NotEntitled;
                return EntitlementValidator.Validate(set).Count == 0;
            })
            .Select(m => m.Code)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["AP", "AR", "CORE", "FA", "INV", "PAY", "POS", "PRJ", "REP"],
            revocable);
    }

    /// <summary>
    /// <b>مقيس:</b> الخفض من <c>Entitled</c> إلى <c>NotEntitled</c> والخفض إلى
    /// <c>ReadOnly</c> <b>لا يُفرَّق بينهما</b> على مستوى المُتحقِّق: كلاهما
    /// مجموعة مقبولة، فاختيار أيّهما متروك لمن ينادي بلا حدٍّ بنيوي.
    /// </summary>
    [Fact]
    public void النزع_والخفض_سواء_عند_المُتحقِّق_اليوم()
    {
        var revoked = All(EntitlementState.NotEntitled);
        var degraded = All(EntitlementState.ReadOnly);

        Assert.Empty(EntitlementValidator.Validate(revoked));
        Assert.Empty(EntitlementValidator.Validate(degraded));
    }

    // ── ٢ · الحالة القائمة: رسالة الرفض بلغة واحدة ───────────────────────────

    /// <summary>
    /// <b>مقيس:</b> نصّ <see cref="EntitlementDeniedException"/> عربيٌّ وحده —
    /// ولا حرف لاتيني فيه إلا رمز الوحدة ورمز المستأجر. وهذا يخالف عُرف هذا
    /// المستودع في نصّ التشخيص: <see cref="EntitlementViolation"/> يحمل
    /// <c>MessageAr</c> و<c>MessageEn</c> معاً، و<c>Babel.Core</c>
    /// <c>EntitlementErrors</c> كذلك.
    /// </summary>
    [Fact]
    public void رسالة_الرفض_بلا_إنجليزية_اليوم()
    {
        var ex = new EntitlementDeniedException(
            "T-1", "CORE", EntitlementState.ReadOnly, AccessIntent.Write);

        // ما يبقى بعد نزع رمز الوحدة ورمز المستأجر: عربيّة خالصة.
        var residue = ex.Message.Replace("CORE", "", StringComparison.Ordinal)
                                .Replace("T-1", "", StringComparison.Ordinal);

        Assert.DoesNotContain(residue, ch => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
    }
}
