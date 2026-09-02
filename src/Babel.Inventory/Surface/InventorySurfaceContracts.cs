using Babel.SharedKernel;

namespace Babel.Inventory.Surface;

/// <summary>
/// معامل تحويل وحدةٍ إلى وحدة أساس الصنف، على السطح المنشور.
/// <para>
/// <b>بسطٌ ومقام صحيحان، لا عددٌ عشري:</b> «الكرتون اثنتا عشرة حبّة» هو <c>12/1</c>،
/// و«الحبّة ثلث علبة» هو <c>1/3</c>. والثاني لا يُكتب عشرياً بلا خسارة، والخسارة في
/// كمّيةٍ تُضرب في تكلفة الوحدة تصل إلى المال.
/// </para>
/// </summary>
/// <param name="UnitCode">رمز الوحدة الأكبر.</param>
/// <param name="Numerator">البسط.</param>
/// <param name="Denominator">المقام.</param>
public sealed record InventoryUnitFactor(string UnitCode, long Numerator, long Denominator);

/// <summary>طلب تسجيل صنف.</summary>
/// <param name="Code">رمز الصنف داخل المنشأة.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل دور، لا رقم حساب.</param>
/// <param name="BaseUnit">وحدة الأساس.</param>
/// <param name="Units">الوحدات الأكبر ومعاملاتها.</param>
public sealed record InventoryItemRequest(
    string Code,
    LocalizedName Name,
    string ItemGroup,
    string BaseUnit,
    IReadOnlyList<InventoryUnitFactor> Units);

/// <summary>صنف كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="BaseUnit">وحدة الأساس.</param>
/// <param name="Units">الوحدات الأكبر ومعاملاتها.</param>
public sealed record InventoryItem(
    Guid Id,
    string Code,
    LocalizedName Name,
    string ItemGroup,
    string BaseUnit,
    IReadOnlyList<InventoryUnitFactor> Units);

/// <summary>
/// كمّية بوحدتها على السطح المنشور — <b>ولا كمّية مجرّدة تعبر هذا الحدّ</b>.
/// </summary>
/// <param name="Magnitude">المقدار.</param>
/// <param name="Unit">رمز الوحدة.</param>
public sealed record InventoryMeasure(decimal Magnitude, string Unit);

/// <summary>طلب إنشاء مستند حركة مخزون <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="Direction">‏<c>IN</c> زيادة أو رصيد افتتاحي · <c>OUT</c> عجز أو إعدام.</param>
/// <param name="ItemId">رمز الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع داخل المستودع.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="Cost">
/// تكلفة الكمّية الواردة كلّها — <b>على الوارد وحده</b>. والصادر تُحسب تكلفته في وحدة
/// المخزون ولا تُملى.
/// </param>
/// <param name="OccurredOn">تاريخ الحركة الميلادي.</param>
public sealed record InventoryStockMovementRequest(
    string Number,
    string Direction,
    string ItemId,
    string WarehouseId,
    string LocationId,
    string ItemGroup,
    InventoryMeasure Quantity,
    decimal Cost,
    DateOnly OccurredOn);

/// <summary>مستند حركة مخزون كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>POSTED</c>.</param>
/// <param name="Direction">الاتجاه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="Quantity">الكمّية بوحدتها كما سُلّمت.</param>
/// <param name="Cost">قيمة الحركة — المُسلَّمة على الوارد، والمحسوبة على الصادر بعد الترحيل.</param>
/// <param name="OccurredOn">تاريخ الحركة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟</param>
public sealed record InventoryStockMovement(
    Guid Id,
    string Number,
    string State,
    string Direction,
    string ItemId,
    string WarehouseId,
    string LocationId,
    string ItemGroup,
    InventoryMeasure Quantity,
    decimal Cost,
    DateOnly OccurredOn,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>رصيد صنف في موقعٍ من مستودع.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع.</param>
/// <param name="Quantity">الكمّية بوحدة أساسها — قد تكون سالبة.</param>
/// <param name="Value">القيمة.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا الموقع مرّةً بتكلفة؟</param>
public sealed record InventoryBalance(
    string ItemId,
    string WarehouseId,
    string LocationId,
    InventoryMeasure Quantity,
    decimal Value,
    decimal UnitCost,
    bool HasCostBasis);

/// <summary>مستندٌ منحرف بين دفتر المخزون المساعد وحسابه الضابط.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="SubledgerEffect">أثره كما يعرفه دفتر المخزون.</param>
/// <param name="ControlEffect">أثره كما هو في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
public sealed record InventoryDivergence(
    string DocumentType,
    string DocumentId,
    string ItemId,
    decimal SubledgerEffect,
    decimal ControlEffect,
    decimal Divergence,
    string ReasonCode);

/// <summary>
/// تقييم المخزون في تاريخ: <b>ثلاثة طرق مستقلّة إلى الرقم نفسه</b> — مجموع الحركات،
/// ومجموع أرصدة الأصناف، ونقطة الضبط في الدفتر.
/// </summary>
/// <param name="AsOf">تاريخ التقييم.</param>
/// <param name="SubledgerTotal">مجموع دفتر المخزون محسوباً من حركاته.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في دفتر الأستاذ.</param>
/// <param name="BalanceTotal">مجموع أرصدة الأصناف — الطريق الثالث.</param>
/// <param name="Divergence">الفارق: الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟ لا «قريب من الصفر».</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
public sealed record InventoryValuationReport(
    DateOnly AsOf,
    decimal SubledgerTotal,
    decimal ControlTotal,
    decimal BalanceTotal,
    decimal Divergence,
    bool IsReconciled,
    IReadOnlyList<InventoryDivergence> Divergences);

/// <summary>
/// طلب تسجيل مستودع.
/// <para>
/// <b>و<paramref name="Qualifier"/> مؤهّل دور لا رقم حساب</b> (القاعدة 3): مصفوفة
/// الترحيل تعرّف مؤهّل <c>inventory_control</c> بأنه «مجموعة الصنف أو صنف المستودع»،
/// وخريطة الأدوار وحدها تُحوّله إلى حساب.
/// </para>
/// </summary>
/// <param name="Code">رمز المستودع — <b>هو النصّ الذي تحمله كل حركةٍ ورصيد</b>، ولا يُغيَّر.</param>
/// <param name="Name">الاسم العربي سجلّاً وترجماته صفوفاً.</param>
/// <param name="Qualifier">صنف المستودع — مؤهّل دور، وفراغُه مشروع.</param>
public sealed record InventoryWarehouseRequest(string Code, TranslatedName Name, string Qualifier);

/// <summary>مستودع كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم وترجماته.</param>
/// <param name="Qualifier">مؤهّل الدور.</param>
/// <param name="Origin">
/// ‏<c>DECLARED</c> كتبه إنسان · <c>OBSERVED</c> رُصد نصّاً في حركةٍ أو رصيدٍ مضى واسمه
/// رمزُه. والفرق ليس زينة: شاشةٌ تعرض الثاني بلا وسمٍ تبدو ككتالوجٍ مكتوب وهي صدى.
/// </param>
/// <param name="IsActive">هل يُقبل مستودعاً لمسوّدةٍ جديدة؟</param>
public sealed record InventoryWarehouse(
    Guid Id,
    string Code,
    TranslatedName Name,
    string Qualifier,
    string Origin,
    bool IsActive);

/// <summary>طلب تسجيل موقع داخل مستودع.</summary>
/// <param name="Code">رمز الموقع — فريدٌ <b>داخل مستودعه</b> لا عبر المنشأة.</param>
/// <param name="Name">الاسم العربي سجلّاً وترجماته صفوفاً.</param>
public sealed record InventoryLocationRequest(string Code, TranslatedName Name);

/// <summary>موقع كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="WarehouseCode">رمز المستودع المالك — <b>وهوية الموقع هي الزوج</b>.</param>
/// <param name="Code">رمز الموقع.</param>
/// <param name="Name">الاسم وترجماته.</param>
/// <param name="Origin">‏<c>DECLARED</c> أو <c>OBSERVED</c>.</param>
/// <param name="IsActive">هل يُقبل موقعاً لمسوّدةٍ جديدة؟</param>
public sealed record InventoryLocation(
    Guid Id,
    string WarehouseCode,
    string Code,
    TranslatedName Name,
    string Origin,
    bool IsActive);
