using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>بيانات الموردين الأساسية: الاسم ثنائي اللغة، والسقف، وشروط السداد.</summary>
public sealed class SupplierService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public SupplierService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل مورداً جديداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<SupplierView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        SupplierDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Supplier.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        if (await _database.Suppliers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SupplierView>.Failure(PurchasingErrors.DuplicateNumber(draft.Code));
        }

        // الرقم يُتحقّق من شكله **قبل** أي كتابة. ورقمٌ مقبول شكلاً قد يتكرّر على أكثر
        // من مورد، وذلك مسموح عمداً — الغموض يُكشف عند البحث لا يُمنع عند الإنشاء.
        Result<string> vatNumber = Vat(draft.VatNumber);
        if (vatNumber.IsFailure)
        {
            return Result<SupplierView>.Failure(vatNumber.Errors);
        }

        SupplierRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            NameEn = draft.Name.English,
            VatNumber = vatNumber.Value,
            CreditLimit = draft.CreditLimit.Amount,
            PaymentTermsDays = draft.PaymentTermsDays,
            IsActive = true,
        };

        _database.Suppliers.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SupplierView>.Success(
            new SupplierView(row.Id, row.Code, draft.Name, draft.CreditLimit, row.PaymentTermsDays, row.VatNumber));
    }

    /// <summary>يقرأ مورداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<SupplierView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Supplier.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        SupplierRow? row = await _database.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == supplierId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound(supplierId))
            : Result<SupplierView>.Success(ViewOf(row));
    }

    /// <summary>
    /// <b>يبحث عن مورد برقم تسجيله الضريبي، أو يرفض ويُسمّي السبب — ولا يختار أبداً.</b>
    /// <para>
    /// هذا هو الطرف الثاني من المطابقة: رمز فاتورة المورد يحمل رقماً <b>مُصدَّقاً</b>،
    /// وهذه الدالة هي ما يحوّله إلى مورد. وقبلها كان الرقم المُصدَّق يصل ولا يُطابقه شيء،
    /// فيُسند المستند بيد المحاسب في كل التقاط — وهي الخطوة نفسها التي وُجدت الميزة
    /// لإزالتها.
    /// </para>
    /// <para>
    /// <b>وثلاث نتائج لا اثنتان:</b> مورد واحد فعّال ⇒ نجاح؛ ولا مورد ⇒ رفض؛ وأكثر من
    /// مورد فعّال ⇒ <b>رفض بالغموض مع تسمية المرشّحين</b>. والثالثة هي الغرض كلّه:
    /// اختيار «الأول» بين مرشّحين يُنتج إسناداً خاطئاً يحمل <b>مظهر التحقّق</b> —
    /// وذلك أسوأ من غياب المعرّف أصلاً.
    /// </para>
    /// <para>
    /// والموقوفون خارج المطابقة عمداً: الإيقاف قول المستأجر «لا تستعمل هذا الصفّ». لكن
    /// «لا مورد» لا تُقال حين يوجد موقوف بالرقم — تلك الرسالة تدفع إلى إنشاء مورد ثالث
    /// بالرقم نفسه، فيتضاعف الغموض الذي نحرسه.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="vatNumber">رقم التسجيل الضريبي كما ورد من الرمز المُصدَّق.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<SupplierView>> FindByVatNumberAsync(
        TenantId tenant,
        UserId actor,
        string vatNumber,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Supplier.FindByVatNumber", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        // المطلوب يُتحقّق من شكله أيضاً: بحثٌ برقم مشوَّه يُرجع «غير موجود» فيبدو أن
        // المورد ناقص وهو موجود، فيُنشأ ثانٍ بالرقم نفسه.
        Result<string> requested = SaudiVatNumber.Validate(vatNumber);
        if (requested.IsFailure)
        {
            return Result<SupplierView>.Failure(requested.Errors);
        }

        string wanted = requested.Value;

        List<SupplierRow> matches = await _database.Suppliers
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenant.Value && entity.VatNumber == wanted)
            .OrderBy(entity => entity.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SupplierRow> active = [.. matches.Where(static entity => entity.IsActive)];

        if (active.Count == 1)
        {
            return Result<SupplierView>.Success(ViewOf(active[0]));
        }

        if (active.Count > 1)
        {
            return Result<SupplierView>.Failure(
                PurchasingErrors.SupplierVatNumberAmbiguous(wanted, [.. active.Select(static entity => entity.Code)]));
        }

        return Result<SupplierView>.Failure(matches.Count == 0
            ? PurchasingErrors.SupplierVatNumberNotFound(wanted)
            : PurchasingErrors.SupplierVatNumberOnlyInactive(
                wanted, [.. matches.Select(static entity => entity.Code)]));
    }

    /// <summary>
    /// يُسجّل رقم التسجيل الضريبي على مورد قائم، أو يمسحه.
    /// <para>
    /// بدونها يبقى الحقل <b>خاوياً على كل مورد قائم إلى الأبد</b>: العمود يصل على جدول
    /// مملوء، والهجرة لا تخترع أرقاماً — فلا سبيل إلى ملئه إلا هنا.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="vatNumber">الرقم، أو فراغ لمسحه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<SupplierView>> SetVatNumberAsync(
        TenantId tenant,
        UserId actor,
        Guid supplierId,
        string vatNumber,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Supplier.SetVatNumber", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        Result<string> validated = Vat(vatNumber);
        if (validated.IsFailure)
        {
            return Result<SupplierView>.Failure(validated.Errors);
        }

        SupplierRow? row = await _database.Suppliers
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == supplierId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound(supplierId));
        }

        row.VatNumber = validated.Value;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SupplierView>.Success(ViewOf(row));
    }

    /// <summary>الفراغ يمرّ بلا فحص — «لم يُسجَّل» حالة مشروعة؛ وما عداه يُفحص كاملاً.</summary>
    private static Result<string> Vat(string? value)
        => SaudiVatNumber.IsUnrecorded(value)
            ? Result<string>.Success(SaudiVatNumber.Unrecorded)
            : SaudiVatNumber.Validate(value);

    private static SupplierView ViewOf(SupplierRow row) => new(
        row.Id,
        row.Code,
        new LocalizedName(row.NameAr, row.NameEn),
        Money.Of(row.CreditLimit, CurrencyCode.Sar),
        row.PaymentTermsDays,
        row.VatNumber);
}
