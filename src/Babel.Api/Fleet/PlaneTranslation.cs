using System.Collections.Immutable;
using System.Globalization;
using Babel.Api.Ports;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Api.Fleet;

/// <summary>
/// <b>الترجمة بين المستويين — في موضعٍ واحد، وهو الجذر التركيبي.</b>
/// <para>
/// المستويان يحملان <b>كتالوجَي وحدات مختلفين عمداً</b>: تسعة رموز نصّية تُباع في مستوى
/// التحكّم (<c>ModuleCatalog</c>)، وثلاثة عشر عضواً معدوداً تُركَّب في المنتَج
/// (<c>BabelModule</c>). وربطُهما دَينٌ <b>مُعلَن ومفتوح بقرار</b> في
/// <c>ADR-0034</c> بند ٣ و<c>ADR-0036</c> §3: «ربطهما يحتاج خريطة تناظر مُعلَنة — وهي
/// قرارٌ عن أي الوحدات تُباع، لا عن أي الحالات تسمح».
/// </para>
/// <para>
/// <b>وهذا الملفّ ليس إغلاقاً لذلك الدَّين.</b> هو <b>الخريطة التي يستعملها سطح
/// الاشتراك</b>، مُعلَنةً صفّاً صفّاً بما فيها الفراغات: ما لا نظير له في أحد الطرفين
/// مكتوبٌ بذلك صراحةً. ولا حارس هنا يدّعي أن الكتالوجين متناظران — لأنهما ليسا كذلك،
/// ولأن حارساً يدّعي تناظراً لم يقرّره أحد يُقرأ بعد سنتين مواصفةً.
/// </para>
/// <para>
/// <b>ولا جدول قرارٍ هنا ولا تفريعٌ على حالة استحقاق.</b> الحالة تعبر <b>باسمها نصّاً</b>
/// من مستوى التحكّم وتُقرأ باسمها في النواة — التعدادان يحملان الأعضاء الثلاثة نفسها
/// بالأسماء نفسها، و<see cref="StatesAgree"/> يقول ذلك صراحةً ويسقط بصوته إن انحرفا.
/// ولو فُرِّع هنا على حالة لكانت نسخةً ثانية من جدول القرار خارج حدّه، وهو ما تمنعه
/// القاعدة 6 بحقّ.
/// </para>
/// </summary>
internal static class PlaneTranslation
{
    /// <summary>
    /// خريطة التناظر: رمز وحدةٍ في مستوى التحكّم ⇐ الوحدات التي يُشغّلها في المنتَج.
    /// <para>
    /// <b>وصفَّان يستحقّان القراءة:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><b>وحدة الأستاذ العام تُشغّل اثنتين</b>: النواة والدفتر. ومستوى التحكّم
    ///         يبيعهما رمزاً واحداً لأن العميل يشتري «دفتراً»، والمنتَج يفصلهما لأن
    ///         الهوية والصلاحيات شيء والقيود شيء آخر.</item>
    ///   <item><b>وحدة المبيعات تُشغّل الالتزام معها</b>: الالتزام <b>إلزامي</b> في
    ///         <c>ModuleDependencyGraph</c> ويعتمد على المبيعات، فحالةٌ له تخالف حالتها
    ///         تُرفض عند التحقّق. وهو لا يُباع منفصلاً في السوق السعودي أصلاً — الفوترة
    ///         الإلكترونية التزامٌ لا خيار — فربطُه بالمبيعات <b>وصفٌ لما هو، لا اختصار</b>.</item>
    /// </list>
    /// <para>
    /// <b>وما لا نظير له، مُعلَناً لا مسكوتاً عنه:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><c>REP</c> (التقارير التحليلية) تُباع في مستوى التحكّم و<b>لا وحدة لها في
    ///         المنتَج</b>: لا مشروع <c>Babel.Rep</c> ولا عضو في <c>BabelModule</c>.
    ///         فحالتها لا تُترجَم إلى شيء، وهي الوحدة الوحيدة التي أرضيتها نزعٌ فعلي.</item>
    ///   <item><c>RealEstate</c> و<c>Portals</c> و<c>Ai</c> مبنيّة في المنتَج و<b>لا
    ///         تُباع بعد</b>: لا رمز لها في كتالوج مستوى التحكّم. فهي تبقى على حالتها ولا
    ///         يمسّها تغيير خطّة — ووحدةٌ لا تُباع لا يُقرَّر استحقاقها من اشتراك.</item>
    /// </list>
    /// </summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<BabelModule>> Correspondence =
        ImmutableDictionary.CreateRange(StringComparer.Ordinal,
        [
            KeyValuePair.Create("AP", ImmutableArray.Create(BabelModule.Purchasing)),
            KeyValuePair.Create("AR", ImmutableArray.Create(BabelModule.Sales, BabelModule.Compliance)),
            KeyValuePair.Create("CORE", ImmutableArray.Create(BabelModule.Core, BabelModule.Ledger)),
            KeyValuePair.Create("FA", ImmutableArray.Create(BabelModule.Assets)),
            KeyValuePair.Create("INV", ImmutableArray.Create(BabelModule.Inventory)),
            KeyValuePair.Create("PAY", ImmutableArray.Create(BabelModule.Hr)),
            KeyValuePair.Create("POS", ImmutableArray.Create(BabelModule.Pos)),
            KeyValuePair.Create("PRJ", ImmutableArray.Create(BabelModule.Projects)),
            KeyValuePair.Create("REP", ImmutableArray<BabelModule>.Empty),
        ]);

    /// <summary>
    /// أسماء حالات الاستحقاق كما يعرفها هذا الطرف — تُقرأ من التعداد نفسه لا تُكتب بيد.
    /// </summary>
    private static ImmutableHashSet<string> KnownStates { get; } =
        [.. Enum.GetNames<EntitlementState>()];

    /// <summary>
    /// هل يتّفق التعدادان على أسماء الحالات؟
    /// <para>
    /// وهذا هو <b>شرط الترجمة كلّها</b>: الاسم يعبر نصّاً، فاسمٌ لا يعرفه الطرف الآخر
    /// يعني ترجمةً مستحيلة. والسؤال يُطرَح صراحةً كي يُجاب بصوتٍ عالٍ عند أول انحراف،
    /// لا أن يُقرأ استثناءُ تحليلٍ غامض في سجلّ خادم.
    /// </para>
    /// </summary>
    /// <param name="stateName">اسم الحالة كما وصل من مستوى التحكّم.</param>
    public static bool StatesAgree(string stateName) => KnownStates.Contains(stateName);

    /// <summary>
    /// يُسقط اشتراكاً من مستوى التحكّم على مجموعة تغييرات استحقاق في المنتَج.
    /// <para>
    /// <b>والوحدات التي لا نظير لها لا تدخل المجموعة إطلاقاً</b>: تغييرٌ لا يقابله رمزٌ
    /// مبيع يعني أن أحدهم قرّر استحقاق وحدةٍ لم يشترها أحد ولم يبعها أحد.
    /// </para>
    /// </summary>
    /// <param name="subscription">الاشتراك كما قرأه المحوّل.</param>
    /// <returns>الحالة المطلوبة لكل وحدة منتَج لها نظير مبيع.</returns>
    /// <exception cref="InvalidOperationException">اسم حالة لا يعرفه تعداد المنتَج.</exception>
    public static IReadOnlyDictionary<BabelModule, EntitlementState> Project(FleetSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        Dictionary<BabelModule, EntitlementState> changes = [];

        foreach (FleetModule module in subscription.Modules)
        {
            if (!Correspondence.TryGetValue(module.Code, out ImmutableArray<BabelModule> targets)
                || targets.Length == 0)
            {
                continue;
            }

            if (!StatesAgree(module.State))
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"حالةُ استحقاقٍ لا يعرفها تعداد المنتَج: «{module.State}» على الوحدة «{module.Code}». "
                    + $"والتعدادان يعبران بالاسم لا بالقيمة، فانحرافُ الأسماء يوقف الترجمة بصوتٍ عالٍ. / "
                    + $"An entitlement state the product enum does not know: '{module.State}' on module '{module.Code}'."));
            }

            EntitlementState state = Enum.Parse<EntitlementState>(module.State, ignoreCase: false);

            foreach (BabelModule target in targets)
            {
                changes[target] = state;
            }
        }

        return changes;
    }

    /// <summary>
    /// يُطبّق اشتراك مستوى التحكّم على استحقاق المنتَج في هذه العملية.
    /// <para>
    /// <b>وهذا هو ما يجعل الانقطاع فعلاً لا صفّاً:</b> بلا هذا السطر يبقى
    /// <c>control.subscription</c> يقول <c>Lapsed</c> والخادم يواصل قبول الترحيل —
    /// وهو أسوأ من ألّا يوجد انقطاع أصلاً، لأنه يبدو منفَّذاً.
    /// </para>
    /// </summary>
    /// <param name="entitlements">خدمة استحقاق المنتَج.</param>
    /// <param name="subscription">الاشتراك بعد التغيير.</param>
    /// <param name="scopes">
    /// <b>مفاتيح الاستحقاق التي يُكتب عليها</b>: معرّف المستأجر، ومعرّف كل منشأة له.
    /// <para>
    /// وهما مفتاحان لا واحد لأن المستودع يسأل الاستحقاق بمفتاحين: مسارات المنشأة
    /// تسأله بـ<c>new TenantId(companyId)</c>، ومسارات المصادقة تسأله بمعرّف المستأجر.
    /// فكتابةٌ على أحدهما وحده تُنتج انقطاعاً <b>يبدو منفَّذاً</b> والكتابة ما تزال
    /// تمرّ — وهو أسوأ من ألّا يوجد انقطاع
    /// (‏<c>traps.md#fakh-the-entitlement-key-is-the-company-on-one-surface-and-the-tenant-on-another</c>).
    /// </para>
    /// </param>
    /// <param name="actor">الفاعل الذي يُكتب على سطر التدقيق.</param>
    /// <param name="reason">سبب التغيير بلغتيه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    /// <returns>أول فشل إن وقع، وإلا نجاح — والفشل يعبر كما هو ولا يُبتلع.</returns>
    public static async Task<Result> ApplyAsync(
        IEntitlementService entitlements,
        FleetSubscription subscription,
        IEnumerable<Guid> scopes,
        UserId actor,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(scopes);

        IReadOnlyDictionary<BabelModule, EntitlementState> changes = Project(subscription);

        foreach (Guid scope in scopes.Distinct().OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            Result<EntitlementSet> applied = await entitlements
                .ApplyAsync(
                    new EntitlementChangeRequest(new TenantId(scope), changes, actor, reason),
                    cancellationToken)
                .ConfigureAwait(false);

            if (applied.IsFailure)
            {
                return Result.Failure(applied.Errors);
            }
        }

        return Result.Success();
    }
}
