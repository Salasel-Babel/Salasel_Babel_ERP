using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// البيانات الأساسية: الموظف وعلاقة عمله، ومكوّنات الأجر وإسناد قيمها.
/// <para>
/// <b>ولا مبلغ نظامي ولا نسبة في هذا الملفّ</b>: المكوّن تصنيفٌ بوسمَين يملؤهما
/// المحاسب، والقيمة صفٌّ بتاريخ سريان يُضاف ولا يُعدَّل.
/// </para>
/// </summary>
public sealed class EmployeeService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public EmployeeService(IEntitlementEnforcer enforcer, HrRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>
    /// يسجّل موظفاً وعلاقة عمله الأولى. <b>والخادم يولّد الرمز المعتم ولا يرسله العميل</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EmployeeView>> RegisterAsync(
        TenantId tenant,
        UserId actor,
        EmployeeDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.Employee.Register", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeView>.Failure(gate.Errors);
        }

        // الرمز يُسَكّ هنا ويُعاد سكّه عند التصادم — والتصادم على 128 بتّاً واقعةٌ لا
        // تقع عملياً، لكن حلقةً محدودة أرخص من افتراض أنها لا تقع أبداً.
        string code = await MintCodeAsync(tenant, cancellationToken).ConfigureAwait(false);

        EmployeeRow employee = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = code,
            NameAr = draft.Name.Arabic,
            CostCenterId = draft.CostCenterId,
            ClassCode = draft.ClassCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        EmploymentRow employment = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            EmployeeId = employee.Id,
            StartedOn = draft.HiredOn,
            State = EmploymentState.Active,
        };

        _database.Employees.Add(employee);
        _database.Employments.Add(employment);

        _database.Identities.Add(new EmployeeIdentityRow
        {
            TenantId = tenant.Value,
            EmployeeId = employee.Id,
            NationalId = draft.Identity.NationalId,
            Iban = draft.Identity.Iban,
            BirthDate = draft.Identity.BirthDate,
        });

        // الترجمات صفوف لا أعمدة (القاعدة 14 · ADR-0021). والعربي سجلٌّ على الصفّ نفسه.
        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.EmployeeNames.Add(new EmployeeNameTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                EmployeeCode = code,
                Locale = translation.Key,
                Text = translation.Value,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EmployeeView>.Success(View(employee, employment, draft.Name, draft.Identity));
    }

    /// <summary>يقرأ موظفاً بعلاقته الجارية وهويته <b>مقنَّعة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EmployeeView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Employee.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeView>.Failure(gate.Errors);
        }

        EmployeeRow? employee = await _database.Employees
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result<EmployeeView>.Failure(HrErrors.EmployeeNotFound(employeeId));
        }

        return Result<EmployeeView>.Success(await ReadAsync(tenant, employee, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يُنهي خدمة موظف — <b>مورداً فرعياً لا حقلَ حالة يُعدَّل</b>، بسابقة إيقاف مركز
    /// التكلفة وعكس القيد. وهو ما يفتح المخالصة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="endedOn">تاريخ انتهاء الخدمة.</param>
    /// <param name="reasonKey">مفتاح سبب الإنهاء — رمزٌ لا نصٌّ يُعرض.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EmployeeView>> TerminateAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        DateOnly endedOn,
        string reasonKey,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.Employee.Terminate", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeView>.Failure(gate.Errors);
        }

        EmployeeRow? employee = await _database.Employees
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result<EmployeeView>.Failure(HrErrors.EmployeeNotFound(employeeId));
        }

        EmploymentRow? employment = await _database.Employments
            .Where(row => row.TenantId == tenant.Value && row.EmployeeId == employeeId && row.State == EmploymentState.Active)
            .OrderByDescending(row => row.StartedOn)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return Result<EmployeeView>.Failure(HrErrors.EmploymentNotFound(employeeId));
        }

        employment.State = EmploymentState.Terminated;
        employment.EndedOn = endedOn;
        employment.TerminationReasonKey = reasonKey;
        employee.IsActive = false;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EmployeeView>.Success(await ReadAsync(tenant, employee, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يعرّف مكوّن أجر بوسمَيه. <b>وهذا هو الباب الذي يجعل الأثر التنظيمي بياناتٍ لا
    /// شيفرة</b>: أي المكوّنات تدخل وعاء الاشتراك وأيّها يدخل وعاء نهاية الخدمة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayComponentView>> AddPayComponentAsync(
        TenantId tenant,
        UserId actor,
        PayComponentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayComponent.Add", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayComponentView>.Failure(gate.Errors);
        }

        if (await _database.PayComponents
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PayComponentView>.Failure(HrErrors.DuplicateNumber(draft.Code));
        }

        PayComponentRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            Kind = draft.Kind,
            EntersContributoryWage = draft.EntersContributoryWage,
            EntersEndOfServiceBase = draft.EntersEndOfServiceBase,
        };

        _database.PayComponents.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayComponentView>.Success(new PayComponentView(
            row.Id, row.Code, draft.Name, row.Kind, row.EntersContributoryWage, row.EntersEndOfServiceBase));
    }

    /// <summary>
    /// يقرأ تصنيفات مكوّنات الأجر. ومراجعٌ خارجي يجب أن يرى <b>على أي أساس</b> تكوّن
    /// الوعاء، لا أن يُخبَر بالنتيجة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PayComponentView>>> ListPayComponentsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.PayComponent.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayComponentView>>.Failure(gate.Errors);
        }

        List<PayComponentRow> rows = await _database.PayComponents
            .Where(row => row.TenantId == tenant.Value)
            .OrderBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PayComponentView>>.Success(
        [
            .. rows.Select(static row => new PayComponentView(
                row.Id,
                row.Code,
                new TranslatedName(row.NameAr),
                row.Kind,
                row.EntersContributoryWage,
                row.EntersEndOfServiceBase)),
        ]);
    }

    /// <summary>
    /// يُسند قيمة مكوّن بتاريخ سريان. <b>إنشاءٌ لا تعديل</b>: الزيادة صفٌّ جديد، وإلا
    /// استحال إعادة حساب مسيّرٍ ماضٍ ليطابق قيده المُرحَّل.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayElementView>> AddPayElementAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        PayElementDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayElement.Add", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayElementView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount < 0m)
        {
            return Result<PayElementView>.Failure(HrErrors.NegativeAmount);
        }

        EmploymentRow? employment = await CurrentEmploymentAsync(tenant, employeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return Result<PayElementView>.Failure(HrErrors.EmployeeNotFound(employeeId));
        }

        if (!await _database.PayComponents
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.ComponentCode, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PayElementView>.Failure(HrErrors.PayComponentNotFound(draft.ComponentCode));
        }

        PayElementRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            EmploymentId = employment.Id,
            ComponentCode = draft.ComponentCode,
            EffectiveFrom = draft.EffectiveFrom,
            Amount = draft.Amount.Amount,
        };

        _database.PayElements.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayElementView>.Success(
            new PayElementView(row.Id, row.ComponentCode, row.EffectiveFrom, draft.Amount));
    }

    /// <summary>يقرأ أجر الموظف بسريانه — كل الصفوف، لأن مراجعة مسيّرٍ ماضٍ تحتاجها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="employeeId">الموظف.</param>
    /// <param name="currency">عملة المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PayElementView>>> ListPayElementsAsync(
        TenantId tenant,
        UserId actor,
        Guid employeeId,
        CurrencyCode currency,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.PayElement.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayElementView>>.Failure(gate.Errors);
        }

        List<Guid> employments = await _database.Employments
            .Where(row => row.TenantId == tenant.Value && row.EmployeeId == employeeId)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (employments.Count == 0)
        {
            return Result<IReadOnlyList<PayElementView>>.Failure(HrErrors.EmployeeNotFound(employeeId));
        }

        List<PayElementRow> rows = await _database.PayElements
            .Where(row => row.TenantId == tenant.Value && employments.Contains(row.EmploymentId))
            .OrderBy(row => row.ComponentCode).ThenBy(row => row.EffectiveFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PayElementView>>.Success(
        [
            .. rows.Select(row => new PayElementView(
                row.Id, row.ComponentCode, row.EffectiveFrom, Money.Of(row.Amount, currency))),
        ]);
    }

    private async Task<EmploymentRow?> CurrentEmploymentAsync(
        TenantId tenant, Guid employeeId, CancellationToken cancellationToken)
        => await _database.Employments
            .Where(row => row.TenantId == tenant.Value && row.EmployeeId == employeeId)
            .OrderByDescending(row => row.StartedOn)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<string> MintCodeAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            string candidate = EmployeeCodes.Mint();

            if (!await _database.Employees
                    .AnyAsync(row => row.TenantId == tenant.Value && row.Code == candidate, cancellationToken)
                    .ConfigureAwait(false))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "تعذّر سكّ رمز موظف معتم فريد بعد ثماني محاولات — وذلك خللٌ في مولّد العشوائية "
            + "لا تصادفٌ على 128 بتّاً. / Could not mint a unique opaque employee code after eight attempts; "
            + "that is a fault in the entropy source, not a 128-bit collision.");
    }

    private async Task<EmployeeView> ReadAsync(TenantId tenant, EmployeeRow employee, CancellationToken cancellationToken)
    {
        EmploymentRow employment = await _database.Employments
            .Where(row => row.TenantId == tenant.Value && row.EmployeeId == employee.Id)
            .OrderByDescending(row => row.StartedOn)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        List<EmployeeNameTranslationRow> translations = await _database.EmployeeNames
            .Where(row => row.TenantId == tenant.Value && row.EmployeeCode == employee.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        EmployeeIdentityRow? identity = await _database.Identities
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.EmployeeId == employee.Id, cancellationToken)
            .ConfigureAwait(false);

        TranslatedName name = new(
            employee.NameAr,
            translations.ToDictionary(static row => row.Locale, static row => row.Text, StringComparer.Ordinal));

        return new EmployeeView(
            employee.Id,
            employee.Code,
            name,
            employee.ClassCode,
            employee.CostCenterId,
            employment.Id,
            employment.StartedOn,
            employment.EndedOn,
            employment.State,
            new MaskedIdentityView(Mask(identity?.NationalId), Mask(identity?.Iban)));
    }

    private static EmployeeView View(
        EmployeeRow employee, EmploymentRow employment, TranslatedName name, EmployeeIdentityDraft identity)
        => new(
            employee.Id,
            employee.Code,
            name,
            employee.ClassCode,
            employee.CostCenterId,
            employment.Id,
            employment.StartedOn,
            employment.EndedOn,
            employment.State,
            new MaskedIdentityView(Mask(identity.NationalId), Mask(identity.Iban)));

    /// <summary>
    /// يُقنّع قيمة شخصية: آخر أربعة محارف وحدها، وما قبلها نجوم بعدد ثابت.
    /// <para>
    /// <b>وعددُ النجوم ثابت لا يساوي طول الأصل</b>: قناعٌ يحفظ الطول يُسرّب الطول،
    /// وطولُ الآيبان يُميّز بلد إصداره.
    /// </para>
    /// </summary>
    private static string Mask(string? value)
        => value is { Length: > 4 } ? "••••" + value[^4..] : "••••";
}
