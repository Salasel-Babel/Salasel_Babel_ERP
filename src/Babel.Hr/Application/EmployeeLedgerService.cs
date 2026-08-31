using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// سجلّ الجزاءات المعتمد وسلف الموظفين — <b>مستندان إداريان لا يُرحَّل أيٌّ منهما بذاته
/// في هذا التسليم</b>.
/// <para>
/// <b>الجزاء</b> يُرحَّل داخل المسيّر لا بذاته، فلا مورد ترحيل له ولا حقل قيدٍ على
/// جوابه: بابٌ يوحي بغير ذلك يُبنى عليه عميل.
/// </para>
/// <para>
/// <b>والسلفة</b> باب ترحيلها <b>غير منشور</b>: حدث صرف السلفة غير موجود في مصفوفة
/// الترحيل، والمحرك يرفض رمزاً لا يعرفه ولا يخترع قالباً. وذلك يترك عطلاً محاسبياً
/// حقيقياً مُعلَناً — القسط يُستقطع ولا يُصرَف أصلٌ يقابله — <b>وهو مُسجَّل في دَين
/// التحقّق ولم يُبتلع</b>.
/// </para>
/// </summary>
public sealed class EmployeeLedgerService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public EmployeeLedgerService(IEntitlementEnforcer enforcer, HrRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يقيّد جزاءً معتمداً بفئة سببه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EmployeeDeductionView>> RecordDeductionAsync(
        TenantId tenant,
        UserId actor,
        EmployeeDeductionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.Deduction.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeDeductionView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount < 0m)
        {
            return Result<EmployeeDeductionView>.Failure(HrErrors.NegativeAmount);
        }

        if (draft.Amount.Currency != _currency)
        {
            return Result<EmployeeDeductionView>.Failure(
                HrErrors.CurrencyMismatch(_currency, draft.Amount.Currency, "amount"));
        }

        EmployeeRow? employee = await _database.Employees
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result<EmployeeDeductionView>.Failure(HrErrors.EmployeeNotFound(draft.EmployeeId));
        }

        // ‏**ولا حدّ أقصى لنسبة الاستقطاع يُفرَض هنا.** الحدّ النظامي للاستقطاع من الأجر
        // **غير متحقَّق منه**، وحدٌّ مخترَع يرفض مسيّرات مشروعة ويُدرّب المستخدم على
        // الالتفاف. والبند مفتوح على المالك ومُسجَّل في دَين التحقّق.
        EmployeeDeductionRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            EmployeeId = employee.Id,
            PeriodCode = draft.PeriodCode,
            CategoryKey = draft.CategoryKey,
            Amount = draft.Amount.Amount,
            ApprovedBy = draft.ApprovedBy,
            ApprovedOn = draft.ApprovedOn,
        };

        _database.Deductions.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EmployeeDeductionView>.Success(View(row, employee.Code));
    }

    /// <summary>يقرأ جزاءً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="deductionId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EmployeeDeductionView>> GetDeductionAsync(
        TenantId tenant,
        UserId actor,
        Guid deductionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Deduction.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeDeductionView>.Failure(gate.Errors);
        }

        EmployeeDeductionRow? row = await _database.Deductions
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == deductionId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<EmployeeDeductionView>.Failure(HrErrors.DocumentNotFound("EmployeeDeduction", deductionId));
        }

        EmployeeRow employee = await _database.Employees
            .FirstAsync(entity => entity.TenantId == tenant.Value && entity.Id == row.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return Result<EmployeeDeductionView>.Success(View(row, employee.Code));
    }

    /// <summary>يُنشئ سلفة <b>مسوّدة</b> بجدول أقساطها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<EmployeeAdvanceView>> DraftAdvanceAsync(
        TenantId tenant,
        UserId actor,
        EmployeeAdvanceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.Advance.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeAdvanceView>.Failure(gate.Errors);
        }

        if (draft.Instalments.Count == 0)
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.NoLines);
        }

        if (draft.Amount.Amount < 0m || draft.Instalments.Any(static line => line.Amount.Amount < 0m))
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.NegativeAmount);
        }

        if (string.IsNullOrWhiteSpace(draft.TreasuryPartyId))
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.TreasuryPartyMissing(draft.Number));
        }

        if (!SettlementMethods.IsAccepted(draft.SettlementMethod))
        {
            return Result<EmployeeAdvanceView>.Failure(
                HrErrors.UnknownSettlementMethod(draft.SettlementMethod, SettlementMethods.Accepted));
        }

        decimal scheduled = draft.Instalments.Sum(static line => line.Amount.Amount);

        if (scheduled != draft.Amount.Amount)
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.OverAllocation(draft.Number, scheduled, draft.Amount.Amount));
        }

        EmployeeRow? employee = await _database.Employees
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.EmployeeNotFound(draft.EmployeeId));
        }

        if (await _database.Advances
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.DuplicateNumber(draft.Number));
        }

        EmployeeAdvanceRow advance = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            EmployeeId = employee.Id,
            IssuedOn = draft.IssuedOn,
            Amount = draft.Amount.Amount,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            State = HrDocumentState.Draft,
        };

        List<AdvanceInstalmentRow> instalments = [];
        int lineNo = 0;

        foreach (AdvanceInstalmentDraft line in draft.Instalments)
        {
            instalments.Add(new AdvanceInstalmentRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                AdvanceId = advance.Id,
                LineNo = ++lineNo,
                PeriodCode = line.PeriodCode,
                Amount = line.Amount.Amount,
            });
        }

        _database.Advances.Add(advance);
        _database.AdvanceInstalments.AddRange(instalments);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<EmployeeAdvanceView>.Success(View(advance, employee.Code, instalments));
    }

    /// <summary>
    /// يقرأ سلفة بجدول سدادها والمتبقّي منها — <b>مشتقّاً من الأقساط المستقطعة فعلاً
    /// وحدها</b>، لا من مرور الزمن ولا من الجدول المخطَّط.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<EmployeeAdvanceView>> GetAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.Advance.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EmployeeAdvanceView>.Failure(gate.Errors);
        }

        EmployeeAdvanceRow? advance = await _database.Advances
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == advanceId, cancellationToken)
            .ConfigureAwait(false);

        if (advance is null)
        {
            return Result<EmployeeAdvanceView>.Failure(HrErrors.DocumentNotFound("EmployeeAdvance", advanceId));
        }

        List<AdvanceInstalmentRow> instalments = await _database.AdvanceInstalments
            .Where(row => row.AdvanceId == advanceId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        EmployeeRow employee = await _database.Employees
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == advance.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return Result<EmployeeAdvanceView>.Success(View(advance, employee.Code, instalments));
    }

    private EmployeeDeductionView View(EmployeeDeductionRow row, string employeeCode) => new(
        row.Id,
        row.EmployeeId,
        employeeCode,
        row.PeriodCode,
        row.CategoryKey,
        Money.Of(row.Amount, _currency),
        row.ApprovedBy,
        row.ApprovedOn,
        row.ConsumedByPayslipId);

    private EmployeeAdvanceView View(
        EmployeeAdvanceRow advance, string employeeCode, IReadOnlyList<AdvanceInstalmentRow> instalments)
    {
        decimal repaid = instalments.Where(static row => row.ConsumedByPayslipId is not null).Sum(static row => row.Amount);

        return new EmployeeAdvanceView(
            advance.Id,
            advance.Number,
            advance.EmployeeId,
            employeeCode,
            advance.IssuedOn,
            Money.Of(advance.Amount, _currency),
            advance.SettlementMethod,
            advance.TreasuryPartyId,
            Money.Of(advance.Amount - repaid, _currency),
            advance.State,
            [
                .. instalments.Select(row => new AdvanceInstalmentView(
                    row.LineNo, row.PeriodCode, Money.Of(row.Amount, _currency), row.ConsumedByPayslipId)),
            ]);
    }
}
