using System.Globalization;
using Babel.Contracts.RealEstate;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.RealEstate;

/// <summary>
/// تنفيذ <see cref="IPropertyDimensionRegistrar"/> — الكاتب الوحيد في
/// <c>ledger.property_dimension</c> في <c>src/</c> كلها.
/// <para>
/// <b>ولماذا داخل الدفتر لا داخل الوحدة العقارية:</b> الجدول من جداول الدفتر، والقاعدة 5
/// تجعل جداول كل وحدة داخل حدّها. والوحدة الأفقية لا ترى مشروع الدفتر أصلاً (القاعدة 3)،
/// فترى الواجهة في العقد وتوصلها الحاوية بهذا التنفيذ.
/// </para>
/// <para>
/// <b>وثلاثة أشياء يفعلها بترتيب لا ينقلب:</b> يرفض نموذج ملكية لا تعرفه القاعدة برسالة
/// مفهومة قبل أن يصطدم بـ<c>ck_property_ownership_model</c>؛ ثم يُدرج الصفّ إدراجاً
/// <b>حصيناً ضد التكرار</b> (‏<c>on conflict do nothing</c>) لأن التسجيل يقع في عملية
/// إنشاء العقار وقد تُعاد؛ ثم يقرأ الصفّ المستقرّ ويرفض إن اختلف نموذج ملكيته — فتسجيلٌ
/// ثانٍ بنموذج آخر <b>نقلُ عقارٍ بين نموذجين</b> لا إعادة إرسال.
/// </para>
/// <para>
/// <b>وإسقاط اللقطة بعده لازم لا تحسيني:</b> ‏<c>LedgerReferenceCache</c> يقرأ سجلّ
/// الأبعاد <b>مرّةً لكل شركة</b> ويُجمّدها، وحجّته المكتوبة أن دور التطبيق لا يستطيع
/// تعديل البيانات المرجعية أصلاً. وهذا المنفذ هو أول ما ينقض تلك الحجّة، فعقارٌ يُسجَّل
/// بعد أول ترحيل في العملية كان سيبقى <b>غائباً عن اللقطة</b> فتُرفض قيوده بقاعدة حجب
/// لا تُقيَّم — أي عطلٌ يعتمد على ترتيب النداءات وحده.
/// </para>
/// <para>
/// <b>وما لا يفعله — عمداً:</b> لا يكتب صفّ ترجمة في <c>ledger.name_translation</c>.
/// المقيس أن <c>LedgerGrants.sql</c> يسحب <c>insert</c> عن ذلك الجدول بحجّة مكتوبة
/// («إضافة لغة ليست فعلاً تطبيقياً بل إدخال بيانات مرجعية بدور المالك»)، وأن <b>لا
/// قارئ واحد في الدفتر</b> يقرأ ترجمة عقار — <c>CompanyReference</c> يقرأ
/// <c>property_id</c> و<c>ownership_model</c> فقط، و<c>LedgerAuditService</c> يقرأ
/// ترجمات <c>account</c> وحدها. فتوسيعُ محيط الكتابة إلى جدول ثانٍ كان سيشتري صفّاً
/// لا يقرؤه أحد. والترجمات تعيش في جدول الوحدة العقارية، والسجلّ العربي عمودٌ هنا.
/// </para>
/// </summary>
internal sealed class PropertyDimensionRegistrar : IPropertyDimensionRegistrar, IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly LedgerRuntime _runtime;

    /// <summary>ينشئ الكاتب.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الدفتر — اتصال دور التطبيق ولقطته المرجعية.</param>
    public PropertyDimensionRegistrar(IEntitlementEnforcer enforcer, LedgerRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _runtime = runtime;
    }

    /// <inheritdoc />
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Write)]
    public async ValueTask<Result> RegisterAsync(
        TenantId tenant,
        Guid companyId,
        string propertyId,
        string ownershipModel,
        TranslatedName name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        Result gate = await _enforcer
            .EnsureAsync(tenant, UserId.SystemActor, BabelModule.Ledger, EntitlementAccess.Write,
                "Ledger.PropertyDimension.Register", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(propertyId))
        {
            return Result.Failure(PropertyDimensionErrors.MissingPropertyId);
        }

        if (!PropertyOwnershipModels.IsKnown(ownershipModel))
        {
            return Result.Failure(PropertyDimensionErrors.UnknownOwnershipModel(ownershipModel));
        }

        await using NpgsqlConnection connection = await _runtime.DataSource
            .OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand insert = new(
            """
            insert into ledger.property_dimension (company_id, property_id, ownership_model, name_ar)
            values ($1, $2, $3, $4)
            on conflict (company_id, property_id) do nothing
            """, connection))
        {
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(propertyId);
            insert.Parameters.AddWithValue(ownershipModel);
            insert.Parameters.AddWithValue(name.Arabic);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // ‏**القراءة بعد الكتابة لا قبلها**: بينهما لا شوط ولا قرار — الصفّ المستقرّ هو
        // ما يقرؤه الحارس، فإن خالف المطلوب فالنداء نقلُ عقارٍ بين نموذجين لا إعادةُ إرسال.
        string? settled;
        await using (NpgsqlCommand read = new(
            "select ownership_model from ledger.property_dimension where company_id = $1 and property_id = $2",
            connection))
        {
            read.Parameters.AddWithValue(companyId);
            read.Parameters.AddWithValue(propertyId);
            settled = (string?)await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(settled, ownershipModel, StringComparison.Ordinal))
        {
            return Result.Failure(PropertyDimensionErrors.OwnershipModelIsImmutable(
                propertyId, settled ?? string.Empty, ownershipModel));
        }

        _runtime.InvalidateReference(companyId);
        return Result.Success();
    }
}

/// <summary>أخطاء تسجيل بُعد العقار — برموز ثابتة ورسالتين، والرفض يُقرأ في تدقيق.</summary>
internal static class PropertyDimensionErrors
{
    public static Error MissingPropertyId { get; } = new(
        "ledger.property_dimension.missing_property_id",
        "معرّف العقار مفقود — والبُعد الذي لا اسم له لا يُسجَّل.",
        "The property identifier is missing — a dimension with no identifier is not registered.");

    public static Error UnknownOwnershipModel(string? value) => new(
        "ledger.property_dimension.unknown_ownership_model",
        "نموذج ملكية غير معروف «" + (value ?? string.Empty) + "». المقبول: own_property أو managed_for_others، "
        + "ولا يُخمَّن ثالث: عليه تُقيَّم قاعدة الحجب GR-RE-001.",
        "Unknown ownership model '" + (value ?? string.Empty) + "'. Accepted: own_property or managed_for_others; "
        + "a third is never guessed — guard rule GR-RE-001 is evaluated on it.");

    public static Error OwnershipModelIsImmutable(string propertyId, string settled, string requested) => new(
        "ledger.property_dimension.ownership_model_is_immutable",
        "العقار «" + propertyId + "» مسجَّل بنموذج ملكية «" + settled + "» والمطلوب «" + requested
        + "». وتغييرُ النموذج بعد التسجيل يُعيد تفسير قيودٍ سبق ترحيلها بأثر رجعي، "
        + "فهو نقلُ عقارٍ بين نموذجين له مستنده لا تعديلُ حقل.",
        "Property '" + propertyId + "' is registered with ownership model '" + settled + "' and '" + requested
        + "' was requested. Changing the model after registration reinterprets already-posted entries retroactively; "
        + "it is a documented move between models, not a field edit.");

    /// <summary>يُستعمل في الرسائل فقط.</summary>
    internal static string Format(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);
}
