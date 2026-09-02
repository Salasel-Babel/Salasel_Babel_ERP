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

/// <summary>
/// مسوّدة تسجيل موضعٍ في هرم التسكين.
/// <para>
/// <b>ولا رمز أبٍ فيها:</b> الأب يُسلَّم <b>بمعرّفه</b> إلى الخدمة، وتترجمه هي إلى
/// رمزه بعد أن تتحقّق من وجوده. ورمزُ أبٍ في المسوّدة كان سيقبل رمزاً لا صفَّ له
/// فيُنشئ ابناً معلّقاً تحت أبٍ لا وجود له.
/// </para>
/// </summary>
/// <param name="Code">رمز الموضع داخل مستواه — هوية تحملها الحركات، لا نصّاً معروضاً.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
public sealed record StoragePlaceDraft(string Code, LocalizedName Name);

/// <summary>موضعٌ في هرم التسكين كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Level">المستوى: <c>WAREHOUSE</c> · <c>LOCATION</c> · <c>BIN</c>.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ParentCode">رمز الأب — نصّ فارغ للمستودع.</param>
/// <param name="IsActive">هل هو عامل؟</param>
public sealed record StoragePlaceView(
    Guid Id,
    string Level,
    string Code,
    LocalizedName Name,
    string ParentCode,
    bool IsActive);

/// <summary>
/// مسوّدة نقلٍ بين موقعين.
/// <para>
/// <b>ولا تكلفة فيها:</b> المنقول يخرج بتكلفة مصدره لحظة النقل، وتُحسب في الدفتر
/// المساعد ولا تُملى (‏ADR-0039). وحقلُ تكلفةٍ هنا كان سيسمح بنقلٍ «يُعيد تسعير»
/// البضاعة وهو ينقلها — أي بجعل حركة مكانٍ حركةَ قيمة.
/// </para>
/// </summary>
/// <param name="Number">رقم المستند — فريد داخل المنشأة.</param>
/// <param name="ItemId">رمز الصنف — <b>واحدٌ على الطرفين</b>.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="FromWarehouseId">مستودع المصدر.</param>
/// <param name="FromLocationId">موقع المصدر.</param>
/// <param name="ToWarehouseId">مستودع الوجهة.</param>
/// <param name="ToLocationId">موقع الوجهة.</param>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="OccurredOn">تاريخ النقل الميلادي.</param>
public sealed record StockTransferDraft(
    string Number,
    string ItemId,
    string ItemGroup,
    string FromWarehouseId,
    string FromLocationId,
    string ToWarehouseId,
    string ToLocationId,
    InventoryQuantity Quantity,
    DateOnly OccurredOn);

/// <summary>مستند نقلٍ كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> أو <c>MOVED</c>.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="FromWarehouseId">مستودع المصدر.</param>
/// <param name="FromLocationId">موقع المصدر.</param>
/// <param name="ToWarehouseId">مستودع الوجهة.</param>
/// <param name="ToLocationId">موقع الوجهة.</param>
/// <param name="Quantity">الكمّية بوحدتها كما سُلّمت.</param>
/// <param name="Value">
/// قيمة المنقول كما حسبها الدفتر المساعد بعد التنفيذ — <b>وهي تُقرأ ولا تُملى، ولا
/// تصل إلى دفتر الأستاذ</b>.
/// </param>
/// <param name="OccurredOn">تاريخ النقل.</param>
/// <param name="AlreadyMoved">هل كانت هذه الهوية مُنفَّذة <b>قبل</b> هذا النداء؟</param>
public sealed record StockTransferView(
    Guid Id,
    string Number,
    string State,
    string ItemId,
    string ItemGroup,
    string FromWarehouseId,
    string FromLocationId,
    string ToWarehouseId,
    string ToLocationId,
    InventoryQuantity Quantity,
    Money Value,
    DateOnly OccurredOn,
    bool AlreadyMoved = false);

/// <summary>
/// رصيدٌ مقروءٌ <b>بتسكينه</b>: الرصيد نفسه، ومعه اسم مستودعه واسم موقعه من السجلّ.
/// <para>
/// <b>و«غير مسجَّل» حالةٌ تُقال لا تُخفى:</b> الحركات القائمة تحمل رموزاً كُتبت قبل
/// وجود السجلّ، فرمزٌ لا صفَّ له يخرج باسمه هو و<see cref="WarehouseRegistered"/>
/// كاذبة. وإخفاؤه — بحذفه من القائمة أو باختراع اسمٍ له — كان سيجعل مجموع الأرصدة
/// المقروءة أقلّ من مجموع الأرصدة الفعلي، وهو انحرافٌ لا يُظهره أي فحص توازن.
/// </para>
/// </summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">رمز المستودع.</param>
/// <param name="WarehouseName">اسم المستودع من السجلّ — أو رمزه إن لم يكن مسجَّلاً.</param>
/// <param name="WarehouseRegistered">هل رمز المستودع مسجَّل في سجلّ التسكين؟</param>
/// <param name="LocationId">رمز الموقع.</param>
/// <param name="LocationName">اسم الموقع من السجلّ — أو رمزه إن لم يكن مسجَّلاً.</param>
/// <param name="LocationRegistered">هل رمز الموقع مسجَّل في سجلّ التسكين؟</param>
/// <param name="Quantity">الكمّية بوحدة أساسها — قد تكون سالبة.</param>
/// <param name="Value">القيمة.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا الموضع مرّةً بتكلفة؟</param>
public sealed record PlacementBalanceView(
    string ItemId,
    string WarehouseId,
    LocalizedName WarehouseName,
    bool WarehouseRegistered,
    string LocationId,
    LocalizedName LocationName,
    bool LocationRegistered,
    InventoryQuantity Quantity,
    Money Value,
    decimal UnitCost,
    bool HasCostBasis);

/// <summary>مسوّدة تسجيل وحدة قياس.</summary>
/// <param name="Code">رمز الوحدة — هوية تحملها كل حركة، لا نصّاً معروضاً.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="QuantityClass">صنف الكمّية: <c>COUNT</c> · <c>WEIGHT</c> · <c>VOLUME</c> · <c>LENGTH</c> · <c>AREA</c>.</param>
public sealed record UnitOfMeasureDraft(string Code, LocalizedName Name, string QuantityClass);

/// <summary>وحدة قياس كما تخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="QuantityClass">صنف الكمّية.</param>
/// <param name="IsActive">هل هي عاملة؟</param>
public sealed record UnitOfMeasureView(
    Guid Id,
    string Code,
    LocalizedName Name,
    string QuantityClass,
    bool IsActive);

/// <summary>
/// مسوّدة معامل تحويل بين وحدتين على مستوى المنشأة.
/// </summary>
/// <param name="FromUnit">الوحدة المُحوَّل منها.</param>
/// <param name="ToUnit">الوحدة المُحوَّل إليها.</param>
/// <param name="Numerator">البسط: كم وحدةً من <paramref name="ToUnit"/> في <paramref name="Denominator"/> من <paramref name="FromUnit"/>.</param>
/// <param name="Denominator">المقام — موجب.</param>
public sealed record UnitConversionDraft(string FromUnit, string ToUnit, long Numerator, long Denominator);

/// <summary>معامل تحويل كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="FromUnit">الوحدة المُحوَّل منها.</param>
/// <param name="ToUnit">الوحدة المُحوَّل إليها.</param>
/// <param name="QuantityClass">صنف الكمّية المشترك بين الوحدتين.</param>
/// <param name="Numerator">البسط.</param>
/// <param name="Denominator">المقام.</param>
public sealed record UnitConversionView(
    Guid Id,
    string FromUnit,
    string ToUnit,
    string QuantityClass,
    long Numerator,
    long Denominator);

/// <summary>
/// طلب تحويل كمّيةٍ من وحدةٍ إلى أخرى — <b>مسبارٌ لا يكتب شيئاً</b>.
/// <para>
/// وُجد كي يُجرَّب التحويل <b>قبل</b> أن يُبنى عليه مستند: يُجيب بالناتج الدقيق أو
/// <b>بالرفض المُسمّى</b>، ولا يُقرّب في الحالتين.
/// </para>
/// </summary>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="ToUnit">الوحدة المطلوب التحويل إليها.</param>
public sealed record UnitConversionTrial(InventoryQuantity Quantity, string ToUnit);

/// <summary>نتيجة تحويلٍ وقع بلا باقٍ.</summary>
/// <param name="From">الكمّية كما سُلّمت.</param>
/// <param name="To">الكمّية بعد التحويل — <b>دقيقةً لا مقرَّبة</b>.</param>
/// <param name="Numerator">بسط المعامل المُستعمَل.</param>
/// <param name="Denominator">مقام المعامل المُستعمَل.</param>
/// <param name="QuantityClass">صنف الكمّية المشترك.</param>
public sealed record UnitConversionResult(
    InventoryQuantity From,
    InventoryQuantity To,
    long Numerator,
    long Denominator,
    string QuantityClass);
