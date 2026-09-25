using Babel.Core;
using Babel.Core.Access;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace BabelDemoCompany;

/// <summary>
/// بذر بابِ الدخول — <b>عضويةُ مالكٍ، وبريدٌ وكلمةُ مرور، ثمّ دخولٌ فعليّ يُثبتهما</b>.
/// <para>
/// <b>والعطل الذي يُصلحه هذا الملفّ كان يجعل العرضَ كلَّه غيرَ مرئيّ:</b> ثمانيةُ
/// أشهرٍ من النشاط، ودورةُ رواتب، ومشروعٌ بمستخلصين، وعقدُ إيجارٍ بفواتيره — كلُّها
/// مبذورة في قواعد البيانات، <b>ولا أحدَ يستطيع أن ينظر إليها</b>. لأن البذر لم يكن
/// يكتب صفَّ عضويةٍ واحداً ولا معرّفَ دخولٍ واحداً، والبابُ الوحيد المفتوح كان رمزَ
/// عرضٍ يُحقَن في إعداد الخادم ويُسلَّم يداً بيد — فإذا ضاع، ضاع العرض معه.
/// </para>
/// <para>
/// <b>ويمرّ هذا كلُّه بـ<see cref="AccessService"/> نفسها التي يناديها سطح HTTP</b>، لا
/// بإدراجٍ خام في <c>access_membership</c> و<c>access_sign_in</c>. وهو الشرط نفسه الذي
/// يحكم بقيّة البذر وللسبب نفسه: صفٌّ يُكتب بيد يُنتج بريداً «مضبوطاً» بإثباتٍ لا
/// يقبله المُتحقِّق، أو عضويةً لا يقابلها سطرٌ في سجلّ التدقيق. فيبدو الدخولُ مبذوراً
/// ولا يفتح شيئاً — وذلك أسوأ من لا بذر، لأن الفشل يقع أمام صاحب القرار لا قبله.
/// </para>
/// <para>
/// <b>ولا كلمةَ مرورٍ افتراضية في هذا المستودع ولا في هذا الملفّ.</b> الكلمة تصل من
/// <c>BABEL_DEMO_SIGNIN_PASSWORD</c> وحده، وغيابُها يُخطّي الخطوة برسالةٍ تسمّي
/// المتغيّر — ولا يُخترع بديل. وكلمةٌ افتراضية مكتوبة هنا كانت ستصير، بعد نشرةٍ
/// واحدة، باباً معروفاً لكل من قرأ المستودع على كل خادمٍ لم يضبطها.
/// </para>
/// <para>
/// <b>والمخزون هو الإثبات لا الكلمة:</b> ما يُودَع في الصفّ هو
/// <c>pbkdf2-sha256$600000$ملح$تجزئة</c>، ولا طريق منه إلى النصّ. ومن يقرأ نسخةً
/// احتياطية أو لقطةَ دعمٍ لا ينتحل بها أحداً (ADR-0094).
/// </para>
/// </summary>
internal sealed class AccessSeed : IDisposable
{
    /// <summary>متغيّرُ البريد. له افتراضٌ لأنه ليس سرّاً — وهو ما يُكتب في شاشة الدخول.</summary>
    public const string HandleVariable = "BABEL_DEMO_SIGNIN_HANDLE";

    /// <summary>متغيّرُ كلمة المرور. <b>لا افتراض له</b>، وغيابه يُخطّي الخطوة.</summary>
    public const string PasswordVariable = "BABEL_DEMO_SIGNIN_PASSWORD";

    /// <summary>البريد حين لا يُذكر — نطاقٌ مُفتعَل بوضوح لا يخصّ منشأةً قائمة.</summary>
    public const string DefaultHandle = "demo@salasel-babel.example";

    /// <summary>الاسم العربي المعروض لصاحب العضوية — وهو السجلّ (ADR-0021).</summary>
    private const string OwnerNameArabic = "مالك المنشأة التجريبية";

    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly AccessService _access;

    private AccessSeed(Settings settings)
    {
        _settings = settings;

        ServiceCollection services = new();

        // النواةُ وحدها: بابُ الدخول لا يمسّ دفتراً ولا وحدةً. واتصالُ دور التطبيق
        // هو ما يُستعمل — وهو الدور نفسه الذي يعمل به الخادم، فما ينجح هنا ينجح هناك.
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _access = _scope.ServiceProvider.GetRequiredService<AccessService>();
    }

    private TenantId Tenant => new(_settings.Company);

    /// <summary>
    /// يبذر البابَ إن كانت كلمةُ المرور موجودة في البيئة، ويُرجع البريد المضبوط أو الفراغ.
    /// </summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<string> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر بابِ الدخول: عضويةُ مالكٍ وبريدٌ وكلمةُ مرور / seeding the front door");

        string? password = Environment.GetEnvironmentVariable(PasswordVariable);

        if (string.IsNullOrWhiteSpace(password))
        {
            Say.Detail(
                PasswordVariable + " غير مضبوط — لا بريدَ دخولٍ يُبذَر، ولا كلمةَ مرورٍ تُخترَع. "
                + "والعرضُ يبقى بلا بابٍ حتى يُضبط.");
            return string.Empty;
        }

        string handle = Environment.GetEnvironmentVariable(HandleVariable) is { } configured
                        && !string.IsNullOrWhiteSpace(configured)
            ? configured
            : DefaultHandle;

        using AccessSeed seed = new(settings);
        await seed.GrantAsync(cancellationToken).ConfigureAwait(false);
        await seed.SetAsync(handle, password, cancellationToken).ConfigureAwait(false);
        await seed.ProveAsync(handle, password, cancellationToken).ConfigureAwait(false);
        return AccessPasswords.Normalise(handle);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    /// <summary>
    /// عضويةُ مالكٍ للمستخدم التجريبي في المنشأة التجريبية.
    /// <para>
    /// <b>وهو الدّاعي والمدعوّ معاً بقصد</b>: البابُ الأول في هذا السطح يمرّ منه مَن لا
    /// عضويةَ له بعدُ مالكاً — وذلك مكتوب في <see cref="AccessService.GrantMembershipAsync"/>
    /// بنصّه، لا التفافٌ عليه. ومنشأةٌ بلا مالكٍ أوّل لا تُفتح أبداً.
    /// </para>
    /// <para>
    /// <b>والإعادة بلا أثر</b>: النشر يقع مرّتين عند أول انقطاع شبكة، ورفضُ
    /// «عضويةٌ قائمة» هو الجوابُ الصحيح لا عطلاً — فيُقرأ كذلك ولا يُوقف البذر.
    /// </para>
    /// </summary>
    private async Task GrantAsync(CancellationToken cancellationToken)
    {
        Result<GrantedMembership> granted = await _access
            .GrantMembershipAsync(
                new MembershipGrantRequest(
                    Tenant, _settings.Company, Seed.Actor, OwnerNameArabic, MembershipRole.Owner,
                    Member: Seed.Actor),
                cancellationToken)
            .ConfigureAwait(false);

        if (granted.IsSuccess)
        {
            Say.Detail("عضويةُ مالكٍ مُنحت للمستخدم التجريبي في المنشأة التجريبية.");
            return;
        }

        bool already = granted.Errors.Any(static error =>
            string.Equals(error.Code, "membership.already_granted", StringComparison.Ordinal));

        Say.Require(
            already,
            "عضويةُ المالك قائمة",
            already
                ? "مُنحت في تشغيلةٍ سابقة — والإعادة بلا أثر."
                : string.Join(" | ", granted.Errors.Select(static error => error.ToString())));
    }

    /// <summary>
    /// يضبط البريد وكلمةَ المرور لصاحب العضوية.
    /// <para>
    /// <b>وضبطُه في كل تشغيلة بقصد</b>: من بدّل السرَّ في مخزن الأسرار يريد أن تتبدّل
    /// الكلمةُ على الخادم بالنشرة التالية. وضبطٌ «مرّةً واحدة» كان سيجعل تدوير كلمةٍ
    /// مسرَّبة يحتاج يداً على قاعدة البيانات.
    /// </para>
    /// </summary>
    private async Task SetAsync(string handle, string password, CancellationToken cancellationToken)
    {
        Result<SignInSet> set = await _access
            .SetPasswordAsync(Tenant, Seed.Actor, handle, password, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(
            set.IsSuccess,
            "بريدُ الدخول وكلمتُه مضبوطان",
            set.IsSuccess
                ? set.Value.Handle
                : string.Join(" | ", set.Errors.Select(static error => error.ToString())));
    }

    /// <summary>
    /// <b>يدخل فعلاً بما ضُبط</b> — ولا يكتفي بأن الكتابة لم ترمِ.
    /// <para>
    /// وهذا هو الفرق بين «بُذر» و«يعمل»: الاشتقاقُ قد ينجح والتحقّقُ يفشل لو انحرف نحوُ
    /// الإثبات، والعضويةُ قد تُمنح ولا تبلغها الجلسةُ لو اختلف المستأجر عن المنشأة.
    /// والحكمُ هنا يُقاس بعدد المنشآت التي بلغتها الجلسة، لا بكلمة «تمّ».
    /// </para>
    /// </summary>
    private async Task ProveAsync(string handle, string password, CancellationToken cancellationToken)
    {
        Result<OpenedSession> opened = await _access
            .OpenSessionWithPasswordAsync(handle, password, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(
            opened.IsSuccess,
            "الدخولُ بالبريد وكلمة المرور يفتح جلسة",
            opened.IsSuccess
                ? "منشآتٌ تبلغها الجلسة: " + Say.Count(opened.Value.Memberships.Count)
                : string.Join(" | ", opened.Errors.Select(static error => error.ToString())));

        bool reaches = opened.Value.Memberships.Any(membership => membership.Company == _settings.Company);

        Say.Require(
            reaches,
            "الجلسةُ تبلغ المنشأة التجريبية نفسها التي بُذرت",
            _settings.Company.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
    }
}
