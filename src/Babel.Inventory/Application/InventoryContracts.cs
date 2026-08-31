using Babel.Contracts.Inventory;
using Babel.SharedKernel;

namespace Babel.Inventory.Application;

/// <summary>
/// معامل تحويل وحدةٍ إلى وحدة أساس الصنف — كما يُسلَّم عند تسجيل الصنف.
/// <para>
/// <b>بسطٌ ومقام لا عددٌ عشري:</b> «الكرتون اثنتا عشرة حبّة» هو <c>12/1</c>، و«الحبّة
/// ثلث علبة» هو <c>1/3</c> — والثاني لا يُكتب عشرياً بلا خسارة، وخسارةٌ في كمّيةٍ
/// تُضرب في تكلفة الوحدة تصل إلى المال.
/// </para>
/// </summary>
/// <param name="UnitCode">رمز الوحدة الأكبر.</param>
/// <param name="Numerator">كم وحدةَ أساسٍ في <paramref name="Denominator"/> من هذه الوحدة.</param>
/// <param name="Denominator">المقام — موجب.</param>
public sealed record ItemUnitDraft(string UnitCode, long Numerator, long Denominator);

/// <summary>مسوّدة تسجيل صنف.</summary>
/// <param name="Code">رمز الصنف داخل المنشأة — هوية تحملها حركاته وقيوده.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور، لا رقم حساب.</param>
/// <param name="BaseUnit">وحدة الأساس: أصغر وحدة يُمسَك بها الصنف، وإليها تُحوَّل البقية.</param>
/// <param name="Units">وحداتٌ أكبر ومعاملاتها إلى وحدة الأساس.</param>
public sealed record ItemDraft(
    string Code,
    LocalizedName Name,
    string ItemGroup,
    string BaseUnit,
    IReadOnlyList<ItemUnitDraft> Units);

/// <summary>صنف كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="BaseUnit">وحدة الأساس.</param>
/// <param name="Units">الوحدات الأكبر ومعاملاتها.</param>
public sealed record ItemView(
    Guid Id,
    string Code,
    LocalizedName Name,
    string ItemGroup,
    string BaseUnit,
    IReadOnlyList<ItemUnitDraft> Units);

/// <summary>
/// مسوّدة مستند حركة مخزون قائم بذاته — تسوية جرد، أو رصيد افتتاحي، أو إعدام.
/// <para>
/// <b>والتكلفة على الوارد وحده.</b> الصادر تُحسب تكلفته في وحدة المخزون ولا تُملى
/// (‏ADR-0039)، فحقلُ التكلفة عليه يُرفض بدل أن يُتجاهَل: تجاهُلُه يجعل المُرسِل يظنّ
/// أنه سعّر صرفاً بسعرٍ لم يصل.
/// </para>
/// </summary>
/// <param name="Number">رقم المستند — فريد داخل المنشأة.</param>
/// <param name="Direction">‏<c>IN</c> زيادة جرد أو رصيد افتتاحي · <c>OUT</c> عجز أو إعدام.</param>
/// <param name="ItemId">رمز الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع داخل المستودع.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="Cost">تكلفة الكمّية الواردة كلّها — على الوارد وحده، وصفرٌ على الصادر.</param>
/// <param name="OccurredOn">تاريخ الحركة الميلادي.</param>
public sealed record StockDocumentDraft(
    string Number,
    string Direction,
    string ItemId,
    string WarehouseId,
    string LocationId,
    string ItemGroup,
    InventoryQuantity Quantity,
    Money Cost,
    DateOnly OccurredOn);

/// <summary>مستند حركة مخزون كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> أو <c>POSTED</c>.</param>
/// <param name="Direction">الاتجاه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="Quantity">الكمّية بوحدتها كما سُلّمت.</param>
/// <param name="Cost">
/// قيمة الحركة: التكلفة المُسلَّمة على الوارد، و<b>التكلفة المحسوبة</b> على الصادر بعد
/// الترحيل — ولا يُسلّمها أحد.
/// </param>
/// <param name="OccurredOn">تاريخ الحركة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">
/// هل كانت هذه الهوية مُرحَّلة <b>قبل</b> هذا النداء؟ ولا تُشتقّ من الحالة: المستند بعد
/// أي ترحيل ناجح حالته <c>POSTED</c> — الأول والثاني سواء.
/// </param>
public sealed record StockDocumentView(
    Guid Id,
    string Number,
    string State,
    string Direction,
    string ItemId,
    string WarehouseId,
    string LocationId,
    string ItemGroup,
    InventoryQuantity Quantity,
    Money Cost,
    DateOnly OccurredOn,
    Guid? EntryId,
    bool AlreadyPosted = false);
