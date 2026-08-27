using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// أول ما يحتاجه مستخدم حقيقي: <b>من أنا، وأي شركة أستطيع أن أفتح؟</b>
/// <para>
/// وقبل هذه النقطة كان معرّف الشركة جزءاً إلزامياً من كل مسار وهو معرّف بصيغة
/// 8-4-4-4-12 — أي شيء <b>لا يستطيع إنسان أن يكتبه</b>. فكانت الشاشة الأولى التي
/// يحتاجها كل عميل — «اختر شركتك» — مستحيلةَ البناء بينما كل شاشات القراءة تعمل.
/// والمعلومة كانت موجودة طوال الوقت داخل <see cref="IApiPrincipalResolver"/>: المستأجر،
/// والمستخدم، والشركات المبلوغة. لم يكن ينقص إلا بابٌ يقرؤها.
/// </para>
/// <para>
/// <b>وهذا المسار خارج نطاق الشركة عمداً — وهو الوحيد كذلك بعد نقطة الصحّة.</b> ولا
/// يستطيع أن يكون داخله: من لا يعرف معرّف شركته لا يستطيع أن يضعه في المسار ليسأل عن
/// شركاته. ومع ذلك <b>لا يخرج منه شيء عن مستأجر آخر</b>: القائمة هي مجموعة الاعتماد
/// نفسها حرفاً بحرف، لا استعلامٌ على جدول شركات بمرشِّح.
/// </para>
/// <para>
/// <b>والفشل مغلق في الاتجاهين:</b> اعتماد لا يبلغ شركةً واحدة يُرفض هنا برمزه
/// <c>session.no_reachable_company</c> ولا يُسلَّم قائمةً فارغة — لأن قائمة فارغة تُقرأ
/// «لا شركات بعد» فيبقى المستخدم ينتظر بيانات لن تأتي، بينما الحقيقة أن اعتماده لم
/// يُربط بشيء. وشركةٌ تُختار ولا يبلغها الاعتماد تُرفض في <see cref="Scope.TryCompany"/>
/// كأي مسار آخر — لا استثناء لهذا الباب.
/// </para>
/// </summary>
internal static class SessionEndpoints
{
    /// <summary>يسجّل نقطة نهاية الجلسة.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapSessionApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet(ApiRoutes.Session, ReadAsync);
    }

    private static async Task<IResult> ReadAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        ApiPrincipal principal = RequestPrincipal.Of(context);

        if (principal.Companies.Count == 0)
        {
            return HttpProblemResults.Code(
                context,
                "session.no_reachable_company",
                "هذا الاعتماد صحيح ولا يبلغ أي شركة. لا شيء يمكن فتحه به، ولا تُسلَّم قائمة فارغة "
                + "تُقرأ «لا بيانات بعد»: الناقص ربطُ الاعتماد بمنشأة، لا بياناتٌ داخلها.",
                "This credential is valid and reaches no company. There is nothing to open with it, and no empty "
                + "list is handed back to be read as 'no data yet': what is missing is the credential's link to a "
                + "company, not data inside one.",
                status: StatusCodes.Status403Forbidden);
        }

        List<SessionCompanyDto> companies = [];

        // الترتيب حرفي وثابت على نصّ المعرّف: قائمةٌ يتغيّر ترتيبها بين نداءين تجعل
        // «الشركة الثانية» تعني شركتين مختلفتين في دقيقتين، وهي حالة تُختار فيها شركةٌ
        // غير المقصودة بضغطة معتادة.
        foreach (Guid companyId in principal.Companies
            .OrderBy(static id => id.ToString("D", CultureInfo.InvariantCulture), StringComparer.Ordinal))
        {
            Result<FoundedCompany> setup = await setups
                .GetAsync(new TenantId(companyId), principal.User, cancellationToken)
                .ConfigureAwait(false);

            if (setup.IsFailure)
            {
                // «لم تُؤسَّس» حالةُ عملٍ معلومة تُعرض، وما عداها عطلٌ يُرفع بصوته: شركةٌ
                // واحدة معطوبة لا تُبتلع بصمت داخل قائمة تبدو سليمة.
                if (setup.Errors.Any(static error => error.Code == "company_setup.not_found"))
                {
                    companies.Add(new SessionCompanyDto(
                        companyId.ToString("D", CultureInfo.InvariantCulture), "NotSetUp", null, [], null, null));
                    continue;
                }

                return HttpProblemResults.Domain(context, setup.Errors);
            }

            FoundedCompany founded = setup.Value;

            companies.Add(new SessionCompanyDto(
                companyId.ToString("D", CultureInfo.InvariantCulture),
                "Ready",
                founded.NameAr,
                [.. founded.Translations.Select(static entry => new NameValueDto(entry.Key, entry.Value))],
                founded.DisplayScale.Places,
                founded.CostCenters.Default.Value));
        }

        return Results.Json(
            new SessionDto(
                principal.Tenant.Value.ToString("D", CultureInfo.InvariantCulture),
                principal.User.Value.ToString("D", CultureInfo.InvariantCulture),
                companies.Count,
                companies),
            ApiJson.Options);
    }
}
