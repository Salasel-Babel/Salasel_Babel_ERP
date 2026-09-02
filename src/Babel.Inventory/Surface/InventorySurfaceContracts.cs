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

/// <summary>طلب تسجيل موضعٍ في هرم التسكين — مستودعاً أو موقعاً أو رفّاً.</summary>
/// <param name="Code">رمز الموضع داخل مستواه — هوية تحملها الحركات، لا نصّاً معروضاً.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
public sealed record InventoryStoragePlaceRequest(string Code, LocalizedName Name);

/// <summary>طلب إعادة تسمية موضع — <b>الاسم وحده، ولا رمز فيه</b>.</summary>
/// <param name="Name">الاسم الجديد.</param>
public sealed record InventoryPlaceNameRequest(LocalizedName Name);

/// <summary>موضعٌ في هرم التسكين كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Level">المستوى: <c>WAREHOUSE</c> · <c>LOCATION</c> · <c>BIN</c>.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ParentCode">رمز الأب — نصّ فارغ للمستودع.</param>
/// <param name="IsActive">هل هو عامل؟</param>
public sealed record InventoryStoragePlace(
    Guid Id,
    string Level,
    string Code,
    LocalizedName Name,
    string ParentCode,
    bool IsActive);

/// <summary>طلب إنشاء مستند نقلٍ بين موقعين <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="ItemId">رمز الصنف — واحدٌ على الطرفين.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="FromWarehouseId">مستودع المصدر.</param>
/// <param name="FromLocationId">موقع المصدر.</param>
/// <param name="ToWarehouseId">مستودع الوجهة.</param>
/// <param name="ToLocationId">موقع الوجهة.</param>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="OccurredOn">تاريخ النقل الميلادي.</param>
public sealed record InventoryStockTransferRequest(
    string Number,
    string ItemId,
    string ItemGroup,
    string FromWarehouseId,
    string FromLocationId,
    string ToWarehouseId,
    string ToLocationId,
    InventoryMeasure Quantity,
    DateOnly OccurredOn);

/// <summary>مستند نقلٍ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>MOVED</c>.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="FromWarehouseId">مستودع المصدر.</param>
/// <param name="FromLocationId">موقع المصدر.</param>
/// <param name="ToWarehouseId">مستودع الوجهة.</param>
/// <param name="ToLocationId">موقع الوجهة.</param>
/// <param name="Quantity">الكمّية بوحدتها كما سُلّمت.</param>
/// <param name="Value">قيمة المنقول بعد التنفيذ — <b>محسوبةٌ لا مُملاة، ولا تصل الدفتر</b>.</param>
/// <param name="OccurredOn">تاريخ النقل.</param>
/// <param name="AlreadyMoved">هل كانت هذه الهوية مُنفَّذة قبل هذا النداء؟</param>
public sealed record InventoryStockTransfer(
    Guid Id,
    string Number,
    string State,
    string ItemId,
    string ItemGroup,
    string FromWarehouseId,
    string FromLocationId,
    string ToWarehouseId,
    string ToLocationId,
    InventoryMeasure Quantity,
    decimal Value,
    DateOnly OccurredOn,
    bool AlreadyMoved);

/// <summary>رصيدٌ مقروءٌ بتسكينه — ومعه اسم مستودعه واسم موقعه من السجلّ.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">رمز المستودع.</param>
/// <param name="WarehouseName">اسم المستودع — أو رمزه إن لم يكن مسجَّلاً.</param>
/// <param name="WarehouseRegistered">هل رمز المستودع مسجَّل في سجلّ التسكين؟</param>
/// <param name="LocationId">رمز الموقع.</param>
/// <param name="LocationName">اسم الموقع — أو رمزه إن لم يكن مسجَّلاً.</param>
/// <param name="LocationRegistered">هل رمز الموقع مسجَّل في سجلّ التسكين؟</param>
/// <param name="Quantity">الكمّية بوحدة أساسها — قد تكون سالبة.</param>
/// <param name="Value">القيمة.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا الموضع مرّةً بتكلفة؟</param>
public sealed record InventoryPlacementBalance(
    string ItemId,
    string WarehouseId,
    LocalizedName WarehouseName,
    bool WarehouseRegistered,
    string LocationId,
    LocalizedName LocationName,
    bool LocationRegistered,
    InventoryMeasure Quantity,
    decimal Value,
    decimal UnitCost,
    bool HasCostBasis);

/// <summary>طلب تسجيل وحدة قياس.</summary>
/// <param name="Code">رمز الوحدة — هوية تحملها كل حركة، لا نصّاً معروضاً.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="QuantityClass">صنف الكمّية: عدد · وزن · حجم · طول · مساحة.</param>
public sealed record InventoryUnitRequest(string Code, LocalizedName Name, string QuantityClass);

/// <summary>وحدة قياس كما تخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="QuantityClass">صنف الكمّية.</param>
/// <param name="IsActive">هل هي عاملة؟</param>
public sealed record InventoryUnit(
    Guid Id,
    string Code,
    LocalizedName Name,
    string QuantityClass,
    bool IsActive);

/// <summary>طلب تسجيل معامل تحويل بين وحدتين على مستوى المنشأة.</summary>
/// <param name="FromUnit">الوحدة المُحوَّل منها.</param>
/// <param name="ToUnit">الوحدة المُحوَّل إليها.</param>
/// <param name="Numerator">البسط.</param>
/// <param name="Denominator">المقام.</param>
public sealed record InventoryUnitConversionRequest(
    string FromUnit,
    string ToUnit,
    long Numerator,
    long Denominator);

/// <summary>معامل تحويل كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="FromUnit">الوحدة المُحوَّل منها.</param>
/// <param name="ToUnit">الوحدة المُحوَّل إليها.</param>
/// <param name="QuantityClass">صنف الكمّية المشترك.</param>
/// <param name="Numerator">البسط.</param>
/// <param name="Denominator">المقام.</param>
public sealed record InventoryUnitConversion(
    Guid Id,
    string FromUnit,
    string ToUnit,
    string QuantityClass,
    long Numerator,
    long Denominator);

/// <summary>طلب تجربة تحويل — <b>مسبارٌ لا يكتب شيئاً</b>.</summary>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="ToUnit">الوحدة المطلوب التحويل إليها.</param>
public sealed record InventoryConversionTrialRequest(InventoryMeasure Quantity, string ToUnit);

/// <summary>نتيجة تحويلٍ وقع بلا باقٍ.</summary>
/// <param name="From">الكمّية كما سُلّمت.</param>
/// <param name="To">الكمّية بعد التحويل — دقيقةً لا مقرَّبة.</param>
/// <param name="Numerator">بسط المعامل المُستعمَل.</param>
/// <param name="Denominator">مقام المعامل المُستعمَل.</param>
/// <param name="QuantityClass">صنف الكمّية المشترك.</param>
public sealed record InventoryConversionResult(
    InventoryMeasure From,
    InventoryMeasure To,
    long Numerator,
    long Denominator,
    string QuantityClass);
