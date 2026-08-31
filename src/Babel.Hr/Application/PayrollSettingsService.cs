using System.Globalization;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// إعدادات نِسَب الاشتراك — <b>الموضع الوحيد الذي تدخل منه نسبة إلى هذا النظام</b>.
/// <para>
/// والجدول يُسلَّم <b>فارغاً</b>. ولا نسبة اشتراك، ولا حدّ أجرٍ خاضع، ولا فرقٌ بين
/// السعودي وغيره، ولا معادلة مكافأة، ولا مدّة إشعار، ولا حدّ أقصى لنسبة الاستقطاع —
/// <b>ولا واحد منها في شيفرة ولا في اختبار</b>. كلّها موسومة <b>غير متحقَّق منها</b>
/// في البند م-14، ورقمٌ يُكتب في اختبار يُنسخ إلى إنتاج بعد شهرين.
/// </para>
/// <para>
/// و<c>POST</c> لا <c>PUT</c>: نسبة فترةٍ ماضية لا تُعدَّل، والزيادة إصدارٌ جديد بتاريخ
/// سريانه ومعتمِده ومصدره.
/// </para>
/// </summary>
public sealed class PayrollSettingsService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly HrDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public PayrollSettingsService(IEntitlementEnforcer enforcer, HrRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يودِع إصداراً جديداً من النِّسَب وحدودها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Write)]
    public async ValueTask<Result<PayrollSettingsView>> DepositAsync(
        TenantId tenant,
        UserId actor,
        PayrollSettingsDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Write, "Hr.PayrollSettings.Deposit", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PayrollSettingsView>.Failure(gate.Errors);
        }

        if (draft.EmployerRate < 0m || draft.EmployeeRate < 0m
            || draft.MinimumContributoryWage.Amount < 0m || draft.MaximumContributoryWage.Amount < 0m)
        {
            return Result<PayrollSettingsView>.Failure(HrErrors.NegativeAmount);
        }

        if (await _database.PayrollSettings
                .AnyAsync(
                    row => row.TenantId == tenant.Value
                           && row.ClassCode == draft.ClassCode
                           && row.EffectiveFrom == draft.EffectiveFrom,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PayrollSettingsView>.Failure(
                HrErrors.DuplicateNumber(draft.ClassCode + "@" + draft.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        PayrollSettingsRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ClassCode = draft.ClassCode,
            EffectiveFrom = draft.EffectiveFrom,
            EmployerRate = draft.EmployerRate,
            EmployeeRate = draft.EmployeeRate,
            MinimumContributoryWage = draft.MinimumContributoryWage.Amount,
            MaximumContributoryWage = draft.MaximumContributoryWage.Amount,
            ApprovedBy = draft.ApprovedBy,
            ApprovedOn = draft.ApprovedOn,
            SourceRef = draft.SourceRef,
        };

        _database.PayrollSettings.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PayrollSettingsView>.Success(View(row, draft.MinimumContributoryWage.Currency));
    }

    /// <summary>
    /// يقرأ الإصدارات بسريانها. ومن لا يستطيع قراءة النسبة السارية لتاريخٍ لا يستطيع
    /// مراجعة مسيّر.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="currency">عملة المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Hr, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PayrollSettingsView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CurrencyCode currency,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Hr, EntitlementAccess.Read, "Hr.PayrollSettings.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PayrollSettingsView>>.Failure(gate.Errors);
        }

        List<PayrollSettingsRow> rows = await _database.PayrollSettings
            .Where(row => row.TenantId == tenant.Value)
            .OrderBy(row => row.ClassCode).ThenBy(row => row.EffectiveFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PayrollSettingsView>>.Success([.. rows.Select(row => View(row, currency))]);
    }

    /// <summary>
    /// الصفّ السارِي لتصنيفٍ في تاريخ، أو <c>null</c> — <b>ولا افتراضي يُخترع عند
    /// الغياب</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="classCode">التصنيف.</param>
    /// <param name="on">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    internal async Task<PayrollSettingsRow?> EffectiveAsync(
        TenantId tenant, string classCode, DateOnly on, CancellationToken cancellationToken)
        => await _database.PayrollSettings
            .Where(row => row.TenantId == tenant.Value && row.ClassCode == classCode && row.EffectiveFrom <= on)
            .OrderByDescending(row => row.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private static PayrollSettingsView View(PayrollSettingsRow row, CurrencyCode currency) => new(
        row.Id,
        row.ClassCode,
        row.EffectiveFrom,
        row.EmployerRate,
        row.EmployeeRate,
        Money.Of(row.MinimumContributoryWage, currency),
        Money.Of(row.MaximumContributoryWage, currency),
        row.ApprovedBy,
        row.ApprovedOn,
        row.SourceRef);
}
