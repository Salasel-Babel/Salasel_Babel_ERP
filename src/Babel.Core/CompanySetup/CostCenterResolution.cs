using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// <b>حلّ مركز التكلفة — الموضع الوحيد الذي يُجاب فيه سؤال «أيّ مركز؟».</b>
/// <para>
/// ‏ADR-0026: لكل منشأة مركز تكلفة واحد على الأقل، والمذكورُ على المستند يُقبل إن كان
/// عاملاً، وإن لم يُذكر شيء فالمركز الافتراضي. وهذه الواجهة هي ذلك القرار <b>مُعلَناً
/// لمن يحتاجه</b>: البوّابات فوق النواة تسأل، ولا تعرف شجرة المراكز ولا تُخزّنها.
/// </para>
/// <para>
/// <b>ولماذا واجهة في النواة لا نداء مباشر إلى <see cref="ICompanySetupStore"/>:</b> لأن
/// السؤال الذي تطرحه البوّابة ليس «ما تأسيس هذه المنشأة؟» بل «ما مركز تكلفة هذا السطر؟».
/// وواجهةٌ تصف السؤال الحقيقي تمنع البوّابة من قراءة مقياس العرض أو الاسم أو أي شيء لا
/// يخصّها، وتُبقي قاعدة الحلّ في مكانٍ واحد بدل نسخةٍ في كل وحدة.
/// </para>
/// <para>
/// <b>ولا ارتداد صامت إلى قيمة مخترَعة:</b> منشأةٌ لم تُؤسَّس ليس لها مركز افتراضي أصلاً،
/// فالجواب رفضٌ برمزه <c>company_setup.not_found</c> لا نصٌّ يُخترع. ومنشأةٌ مؤسَّسة لا
/// تعجز أبداً، لأن سجلّها غير فارغ بحكم بنائه.
/// </para>
/// </summary>
public interface ICostCenterResolver
{
    /// <summary>
    /// يحلّ مركز التكلفة لمنشأة: المذكور إن كان عاملاً، والافتراضي إن لم يُذكر شيء.
    /// </summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="requested">الرمز المذكور على المستند، أو غيابه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    /// <returns>رمز مركز التكلفة، أو رفضاً برمزه.</returns>
    ValueTask<Result<string>> ResolveAsync(
        TenantId company,
        string? requested,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// التنفيذ فوق مخزن التأسيس. <b>لا قرار فيه</b>: قاعدة الحلّ في
/// <see cref="CostCenterRegister.Resolve"/>، وهذا نقلٌ وسؤال.
/// </summary>
public sealed class CostCenterResolver : ICostCenterResolver
{
    private readonly ICompanySetupStore _store;

    /// <summary>ينشئ الحالّ.</summary>
    /// <param name="store">مخزن التأسيس.</param>
    public CostCenterResolver(ICompanySetupStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<Result<string>> ResolveAsync(
        TenantId company,
        string? requested,
        CancellationToken cancellationToken = default)
    {
        FoundedCompany? setup = await _store.FindAsync(company, cancellationToken).ConfigureAwait(false);

        if (setup is null)
        {
            return Result<string>.Failure(CompanySetupErrors.NotFound);
        }

        Result<CostCenterCode> resolved = setup.CostCenters.Resolve(requested);

        return resolved.IsFailure
            ? Result<string>.Failure(resolved.Errors)
            : Result<string>.Success(resolved.Value.Value!);
    }
}
