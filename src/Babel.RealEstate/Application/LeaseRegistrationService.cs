using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.RealEstate.Application;

/// <summary>
/// <b>قيدُ تسجيلِ عقد إيجار وجدولُ دفعاته — أرشفةٌ وفوترة، لا تحرير.</b>
/// <para>
/// <b>العقد يُحرَّر في منصّة إيجار ولا يُحرَّر هنا.</b> منصّة إيجار الحكومية هي الطرف
/// المخوَّل بتحرير عقود الإيجار؛ وما تحمله هذه الخدمة <b>قيدٌ أرشيفي</b> لعقدٍ حُرِّر
/// هناك، ومرجعُه الموثوق <b>رقم عقد إيجار</b>. ولا تكامل مع المنصّة في هذه الوحدة:
/// الرقم يُقيَّد كما يُدخله المُسجِّل ولا يُتحقَّق منه.
/// </para>
/// <para>
/// <b>ولا بوّابة ترحيل على هذا المستند إطلاقاً</b>، وغيابها ليس نقصاً: الحدث
/// <c>realestate.lease.signed</c> مُعلَنٌ في المصفوفة بـ<c>posts_entry=false</c> —
/// «العقد التزام متبادل مستقبلي لم ينفّذه أي طرف بعد». وغيابُ الباب هو ما يجعل «القيد
/// لا يُرحّل» <b>مقروءاً من شكل السطح</b> لا من تعليق.
/// </para>
/// <para>
/// <b>والاعتماد للفوترة ليس نفاذاً نظامياً</b>: هو إذنٌ داخلي يقول «من هذه اللحظة
/// تُبنى فواتير الإيجار على هذا القيد». مورد فرعي مستقلّ لا حقلٌ يُعدَّل، وهو أيضاً
/// اللحظة التي تدخل فيها مدّة القيد <b>قيد الاستبعاد الزمني</b> في قاعدة البيانات،
/// فقيدان معتمَدان بمدّتين متداخلتين على وحدة واحدة يُرفضان من القاعدة لا من الواجهة.
/// <b>وإعادةُ الاعتماد آمنة وتُعيد النتيجة نفسها.</b>
/// </para>
/// </summary>
public sealed class LeaseRegistrationService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public LeaseRegistrationService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يُسجّل عقداً مُحرَّراً في منصّة إيجار بوصفه <b>مسوّدة قيد</b>. ولا قيد محاسبي:
    /// الاعتماد للفوترة خطوة مستقلّة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">مسوّدة العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<LeaseView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        LeaseDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.Lease.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LeaseView>.Failure(gate.Errors);
        }

        if (draft.Instalments.Count == 0)
        {
            return Result<LeaseView>.Failure(RealEstateErrors.ScheduleIsNotGenerated);
        }

        UnitRow? unit = await _database.Units
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == draft.UnitId,
                cancellationToken)
            .ConfigureAwait(false);

        if (unit is null)
        {
            return Result<LeaseView>.Failure(RealEstateErrors.UnitNotFound(draft.UnitId));
        }

        if (!await _database.Parties
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId
                           && row.Id == draft.LesseeId && row.PartyRole == PartyRoles.Lessee,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<LeaseView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Lessee, draft.LesseeId));
        }

        if (await _database.Leases
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId
                           && row.EjarContractNumber == draft.EjarContractNumber,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<LeaseView>.Failure(RealEstateErrors.DuplicateCode(draft.EjarContractNumber));
        }

        // ── الثابتة تُفحص عند التسجيل أيضاً، لا عند الاعتماد وحده ──────────────
        // ‏«مجموع الأقساط = قيمة العقد بالضبط دون هللات ضائعة» نصُّ المصفوفة. والرفض
        // هنا أرحم: مسوّدةٌ تُحفظ بأقساطٍ لا تجمع قيمتها تبقى قنبلةً تنفجر عند الاعتماد.
        // **والنظام لا يوزّع الفرق من عنده**: أين يقع فائض الهللات سياسةُ تقريبٍ يملكها
        // المالك، واختيارُها هنا يجعل كشف حساب مستأجرٍ حقيقي يخالف ما اتُّفق عليه.
        decimal total = draft.Instalments.Sum(instalment => instalment.Amount.Amount);

        if (total != draft.TotalRent.Amount)
        {
            return Result<LeaseView>.Failure(
                RealEstateErrors.InstalmentsDoNotSumToTheContract(total, draft.TotalRent.Amount));
        }

        LeaseContractRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            EjarContractNumber = draft.EjarContractNumber,
            PropertyId = unit.PropertyId,
            UnitId = unit.Id,
            LesseeId = draft.LesseeId,
            StartsOn = draft.StartsOn,
            EndsOn = draft.EndsOn,
            TotalRent = draft.TotalRent.Amount,
            State = LeaseState.Draft,
        };

        _database.Leases.Add(row);

        // ‏**الأقساط تُحفظ مع المسوّدة ولا تُفوتَر بعد**: الاعتماد للفوترة هو ما يجعلها
        // جدول دفعات يُفوتَر منه. وحفظها هنا يجعل «ما الذي سيُعتمَد؟» سؤالاً له جواب واحد.
        int seq = 1;
        foreach (InstalmentDraft instalment in draft.Instalments)
        {
            _database.ScheduleLines.Add(new PaymentScheduleLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                LeaseId = row.Id,
                Seq = seq++,
                PeriodFrom = instalment.PeriodFrom,
                PeriodTo = instalment.PeriodTo,
                DueOn = instalment.DueOn,
                Amount = instalment.Amount.Amount,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<LeaseView>.Success(View(row));
    }

    /// <summary>يقرأ قيد التسجيل بحالته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<LeaseView>> ReadAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.Lease.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LeaseView>.Failure(gate.Errors);
        }

        LeaseContractRow? row = await _database.Leases
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == leaseId,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<LeaseView>.Failure(RealEstateErrors.LeaseNotFound(leaseId))
            : Result<LeaseView>.Success(View(row));
    }

    /// <summary>
    /// يقرأ جدول الدفعات <b>بمعرّفات سطوره</b> — وهي مدخل الفوترة.
    /// <para>بلا نشرها يصير باب الفوترة باباً لا يوصل إليه بابٌ آخر (ADR-0047).</para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ScheduleLineView>>> ReadScheduleAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.Lease.Schedule", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ScheduleLineView>>.Failure(gate.Errors);
        }

        if (!await _database.Leases
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == leaseId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<ScheduleLineView>>.Failure(RealEstateErrors.LeaseNotFound(leaseId));
        }

        List<PaymentScheduleLineRow> rows = await _database.ScheduleLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.LeaseId == leaseId)
            .OrderBy(row => row.Seq)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ScheduleLineView> lines =
        [
            .. rows.Select(row => new ScheduleLineView(
                row.Id, row.Seq, row.PeriodFrom, row.PeriodTo, row.DueOn,
                Money.Of(row.Amount, _currency), row.IsInvoiced)),
        ];

        return Result<IReadOnlyList<ScheduleLineView>>.Success(lines);
    }

    /// <summary>
    /// <b>يعتمد القيد للفوترة</b>: يفحص الثابتة، ويُدخل المدّة قيد الاستبعاد الزمني،
    /// ويُتيح بناء فواتير الإيجار عليه. <b>ولا يُرحّل قيداً محاسبياً</b> ومخطّط جوابه
    /// بلا معرّف قيد، <b>ولا يُنفِّذ عقداً</b>: النفاذ من المنصّة لا من هذا الباب.
    /// <para>
    /// <b>وإعادةُ الاعتماد آمنة</b>: نداءٌ ثانٍ على قيدٍ معتمَد يُعيد الجواب نفسه ولا
    /// يكتب شيئاً. والجدول مكتوبٌ عند التسجيل لا هنا، فلا شيء يُضاعَف بنداءٍ ثانٍ.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<LeaseView>> ApproveForBillingAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.Lease.ApproveForBilling", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LeaseView>.Failure(gate.Errors);
        }

        LeaseContractRow? row = await _database.Leases
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == leaseId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<LeaseView>.Failure(RealEstateErrors.LeaseNotFound(leaseId));
        }

        // ── إعادة الاعتماد تُعيد النتيجة نفسها ولا تُرفض ───────────────────────
        // ‏**والرفض الذي كان هنا كان يستند إلى دعوى غير صحيحة**: «إعادته تولّد جدول
        // دفعات ثانياً». والجدول يُكتب عند **التسجيل** لا هنا — فلا شيء يتضاعف بنداءٍ
        // ثانٍ. والاعتماد إذنُ فوترةٍ لا حدثٌ يقع مرّة، فالنداء الثاني يقرأ ما قرأه
        // الأول ويعيده. (‏ق-٣: المسوّدة ثمّ الاعتماد خطوتان، وإعادةُ الاعتماد آمنة.)
        if (string.Equals(row.State, LeaseState.Billable, StringComparison.Ordinal))
        {
            return Result<LeaseView>.Success(View(row));
        }

        List<PaymentScheduleLineRow> schedule = await _database.ScheduleLines
            .Where(line => line.TenantId == tenant.Value && line.LeaseId == leaseId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (schedule.Count == 0)
        {
            return Result<LeaseView>.Failure(RealEstateErrors.ScheduleIsNotGenerated);
        }

        // ── الثابتة تُفحص هنا لا تُصلَح ────────────────────────────────────────
        // «مجموع الأقساط = قيمة العقد بالضبط دون هللات ضائعة» نصُّ المصفوفة. والنظام
        // **لا يوزّع الفرق من عنده**: أين يقع فائض الهللات سياسةُ تقريبٍ يملكها المالك
        // (ق-ع-3)، واختيارُها هنا يجعل كشف حساب مستأجر حقيقي يختلف عمّا اتُّفق عليه.
        decimal instalments = schedule.Sum(line => line.Amount);
        if (instalments != row.TotalRent)
        {
            return Result<LeaseView>.Failure(
                RealEstateErrors.InstalmentsDoNotSumToTheContract(instalments, row.TotalRent));
        }

        row.State = LeaseState.Billable;

        try
        {
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (Overlaps(failure))
        {
            // ── الرفض من قاعدة البيانات يُترجَم ولا يُبتلع ولا يُترك خامّاً ──────────
            // ‏**ولا يُستبق بفحصٍ في الخدمة**: الفحص يقرأ ثم يكتب، وبين القراءة والكتابة
            // يمرّ نداءٌ آخر — فيجتاز اثنان الفحص معاً وتُؤجَّر الوحدة مرّتين. القيد في
            // القاعدة هو الحكم، وهذا السطر يترجم حكمه إلى رمز ثابت ورسالتين بدل خطأ
            // ‏23P01 خامّ يسمّي قيداً ولا يسمّي وحدةً ولا مدّة.
            _database.ChangeTracker.Clear();
            return Result<LeaseView>.Failure(RealEstateErrors.LeaseTermOverlaps(row.EjarContractNumber));
        }

        return Result<LeaseView>.Success(View(row));
    }

    /// <summary>هل الرفض انتهاكٌ لقيد الاستبعاد الزمني؟ ‏<c>23P01</c> ولا شيء غيره.</summary>
    private static bool Overlaps(DbUpdateException failure)
        => failure.InnerException is PostgresException postgres
           && string.Equals(postgres.SqlState, PostgresErrorCodes.ExclusionViolation, StringComparison.Ordinal);

    private LeaseView View(LeaseContractRow row) => new(
        row.Id,
        row.EjarContractNumber,
        row.PropertyId,
        row.UnitId,
        row.LesseeId,
        row.StartsOn,
        row.EndsOn,
        Money.Of(row.TotalRent, _currency),
        row.State);
}
