using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace BabelDemoCompany;

/// <summary>
/// <b>تأسيس المنشأة التجريبية — الخطوة التي يفترضها كل ما بعدها.</b>
/// <para>
/// ‏ADR-0026: بوّابة الترحيل تسأل <see cref="ICostCenterResolver"/> عن مركز التكلفة
/// <b>قبل</b> أن تبني طلباً، والحالّ يقرأ مخزن التأسيس. فمنشأةٌ لم تُؤسَّس ترتدّ بـ
/// <c>company_setup.not_found</c> — <b>وهو الرفض الصحيح</b>: دفترٌ لمنشأة بلا مقياس عرض
/// ولا مركز تكلفة ليس دفتراً. وقد كان البذر يفترض التأسيس ولا يفعله، فكان يسقط عند أول
/// فاتورة (<c>INV-202601-0001</c>) — عطلٌ كان مختبئاً خلف عطل ترجمة في
/// <see cref="Verify"/> فلم يبلغه أحد.
/// </para>
/// <para>
/// <b>ولماذا موضعٌ واحد يستدعيه البذر والإثبات معاً:</b> مخزن التأسيس في هذه الموجة
/// <see cref="InMemoryCompanySetupStore"/> — أي أن حالته <b>عمر العملية</b> لا عمر
/// القاعدة. فكل خطوة تبني حاويتها تبني مخزنها، وتأسيسٌ مكتوب مرّتين يفترق بحرفٍ يوماً
/// ما فيصير المركز المُرحَّل به غير المركز المقروء به. والإعلان هنا واحد: <see
/// cref="Company.SetupDraft"/>، ومن يقرأ المركز يقرؤه من السجلّ المؤسَّس لا من ثابتة
/// مكتوبة بيد.
/// </para>
/// </summary>
internal static class Founding
{
    /// <summary>
    /// يؤسّس المنشأة التجريبية إن لم تكن مؤسَّسة في هذا المخزن، ويُرجع تأسيسها.
    /// <b>يُعاد بلا أثر</b>: «مؤسَّسة سلفاً» ليست عطلاً بل الحالة المطلوبة نفسها.
    /// </summary>
    /// <param name="setup">خدمة التأسيس — الطريق المُعلَن نفسه الذي يسلكه سطح HTTP.</param>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<FoundedCompany> EnsureAsync(
        CompanySetupService setup,
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setup);

        Result<FoundedCompany> founded = await setup
            .InitialiseAsync(
                new CompanyInitialisationRequest(tenant, actor, Company.SetupDraft),
                cancellationToken)
            .ConfigureAwait(false);

        if (founded.IsSuccess)
        {
            return founded.Value;
        }

        // مؤسَّسة سلفاً: تُقرأ ولا يُعاد تأسيسها — وهذا هو الفرق بين «أعِدْ التشغيل»
        // و«استبدِل ما بُني». وأي رفض آخر يخرج كما هو، بنصّه ورمزه.
        if (!founded.Errors.Any(static error =>
                string.Equals(error.Code, CompanySetupErrors.AlreadyInitialised.Code, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "تعذّر تأسيس المنشأة التجريبية — رُفض: "
                + string.Join(" | ", founded.Errors.Select(static error => error.ToString())));
        }

        Result<FoundedCompany> existing = await setup
            .GetAsync(tenant, actor, cancellationToken)
            .ConfigureAwait(false);

        if (existing.IsFailure)
        {
            throw new InvalidOperationException(
                "المنشأة مؤسَّسة ولا تُقرأ — رُفض: "
                + string.Join(" | ", existing.Errors.Select(static error => error.ToString())));
        }

        return existing.Value;
    }
}
