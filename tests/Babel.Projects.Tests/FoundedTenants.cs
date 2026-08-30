using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace Babel.Projects.Tests;

/// <summary>
/// <b>منشآت مؤسَّسة لهذه المجموعة — لأن الترحيل يفترض التأسيس، لا العكس.</b>
/// <para>
/// ‏ADR-0026: لكل منشأة مركز تكلفة واحد على الأقل، وهو يُنشأ <b>عند التأسيس</b>. وبوّابة
/// الترحيل تسأل النواة عن المركز قبل أن تبني طلباً، فمنشأةٌ لم تُؤسَّس ترتدّ بـ
/// <c>company_setup.not_found</c> — <b>وذلك هو السلوك الصحيح</b>: دفترٌ لمنشأة لم
/// تُؤسَّس هو دفترٌ بلا مقياس عرض ولا مركز تكلفة.
/// </para>
/// <para>
/// و<b>كل مجموعة تبني تأسيسها بنفسها</b> ولا ترثه من جارتها: مخزنٌ مشترك بين المجموعات
/// يجعل «مرّ لأن غيره سبقه» ممكناً من جديد، وهو العطل الذي وُجد مسح العزل لأجله.
/// </para>
/// </summary>
internal static class FoundedTenants
{
    /// <summary>يبني مخزن تأسيس فيه هذه المنشآت، مؤسَّسةً بمركز تكلفة واحد.</summary>
    /// <param name="tenants">المنشآت.</param>
    public static InMemoryCompanySetupStore StoreFor(params TenantId[] tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        InMemoryCompanySetupStore store = new();

        foreach (TenantId tenant in tenants)
        {
            CompanySetupDraft draft = new(
                CompanyNameAr: "منشأة اختبار " + tenant.Value.ToString("N")[..8],
                CompanyNameTranslations: null,
                CostCenters: CostCenterPlan.One,
                FirstCostCenterNameAr: null,
                FirstCostCenterTranslations: null,
                DecimalPlaces: 2);

            Result<FoundedCompany> founded = FoundedCompany.Found(tenant, draft);

            if (founded.IsFailure)
            {
                throw new InvalidOperationException(
                    "تعذّر تأسيس منشأة الاختبار: " + string.Join(" · ", founded.Errors.Select(e => e.Code)));
            }

            if (!store.TryFoundAsync(founded.Value).AsTask().GetAwaiter().GetResult())
            {
                throw new InvalidOperationException("منشأة الاختبار مؤسَّسة مرّتين في المخزن نفسه.");
            }
        }

        return store;
    }

    /// <summary>حالُّ مركز التكلفة فوق مخزنٍ فيه هذه المنشآت.</summary>
    /// <param name="tenants">المنشآت.</param>
    public static ICostCenterResolver ResolverFor(params TenantId[] tenants)
        => new CostCenterResolver(StoreFor(tenants));

    /// <summary>
    /// رمز المركز الافتراضي لأول مركز في أي منشأة — <c>cc.001</c>.
    /// مكتوب هنا مرّة، فالاختبار الذي يتوقّعه يقرؤه ولا يخمّنه.
    /// </summary>
    public const string DefaultCode = "cc.001";
}
