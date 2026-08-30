using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Posting;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 13 — لا منطق أعمال في <c>Babel.Api</c>.</b>
/// <para>
/// طلب المالك: <b>«العزل التام بين فرونت اند وباك اند»</b>. والقرار لا يصمد بالنيّة: طبقة
/// HTTP <b>يُسمح</b> لها بحمل منطق تصير خلال سنة <b>المكان الذي يعيش فيه المنطق</b> — لا
/// لسوء نيّة، بل لأنها أسهل مكان. فمن يريد رقماً في شاشة يجده أقرب في المعالج منه في وحدة.
/// وحين يقع ذلك تكون الواجهة الثانية (جوال، نقطة بيع، بوابة) قد صارت مستحيلة بلا إعادة كتابة.
/// </para>
/// <para>
/// <b>و«لا منطق أعمال» عبارة لا اختبار.</b> فما يلي تحويلها إلى وقائع تُفحص آلياً:
/// </para>
/// <list type="number">
///   <item><b>(أ) لا حساب على <c>decimal</c> في تجميعة <c>Babel.Api</c> إطلاقاً.</b>
///         السطح <b>يحمل</b> المال ولا <b>يحسبه</b>. وهذا أقوى بنود القاعدة، ولا يُعبَّر
///         عنه بـNetArchTest: يُفحص بمسح IL.</item>
///   <item><b>(ب) لا نوع داخلي لأي وحدة يُذكر في <c>Babel.Api</c></b> — لا سياق قاعدة
///         بيانات، ولا نوع صفّ، ولا نوع من فضاء <c>Persistence</c> أو <c>Application</c>.
///         والمسموح قائمة معلَنة تمرّ بمراجعة.</item>
///   <item><b>(ج) لا رقم حساب في <c>Babel.Api</c></b> — امتداد للقاعدة 2 إلى طبقة HTTP.</item>
///   <item><b>(د) لا قرار مصفوفة ترحيل في <c>Babel.Api</c></b>: لا اسم دور، ولا رمز حدث،
///         ولا رمز دور في المصفوفة. السطح ينقل واقعة أعمال؛ ولا يقرّر أي حساب تمسّه.</item>
///   <item><b>(هـ) لا شيء يعتمد على <c>Babel.Api</c></b> — والقاعدة 3 تحرسه، ويُعاد
///         تأكيده هنا بعد دخول مشروع اختبار جديد إلى الشجرة.</item>
/// </list>
/// <para>
/// <b>وعن الأوامر الأوّلية في IL — البند الذي طُلب التحقق منه لا افتراضه:</b> صيغة IL
/// للجمع العشري ليست <c>add</c> بل نداء <c>System.Decimal::op_Addition</c>؛ ‏<c>decimal</c>
/// نوع مركّب من 128 بت ولا يملك المُصرِّف له تعليمة آلة. <b>وهذا مُتحقَّق منه هنا لا
/// مفترَض</b>: <see cref="ThePrimitiveOpcodeClaimIsVerifiedNotAssumed"/> يفكّ جسم
/// <c>Babel.SharedKernel.Money::Add</c> — وهو جمعٌ عشري حقيقي في هذا المستودع — ويُثبت أنه
/// يحمل <c>call op_Addition</c> ولا يحمل <c>add</c> واحدة. <b>ولذلك يعتمد البند (أ) على
/// فحص نداءات المشغِّلات، لا على الأوامر الأوّلية</b>؛ والأوامر الأوّلية تُعدّ وتُعرَض
/// لأنها تخصّ <c>int</c> و<c>long</c> (طول نصّ، وعدّ سطور) ولا علاقة لها بالمال.
/// </para>
/// <para>
/// <b>وما لا تلتقطه هذه القاعدة</b> — مكتوب هنا عمداً، لأن قاعدة توحي بتغطية لا تملكها
/// أخطر من غياب القاعدة:
/// </para>
/// <list type="bullet">
///   <item><description>
///     دورٌ يُبنى اسمه <b>ديناميكياً</b> في وقت التشغيل (تركيب محارف، أو قراءة من إعداد)
///     لا يظهر في <c>ldstr</c> ولا في المصدر. الحاجز الوحيد ضده مراجعة بشرية.
///   </description></item>
///   <item><description>
///     منطق أعمال <b>بلا حساب وبلا تسمية</b>: شرطٌ يقرّر أي نقطة نهاية تُستدعى بناءً على
///     قيمة أعمال (‏<c>if (amount &gt; limit)</c>) — البند (أ) يلتقط المقارنة العشرية فقط
///     إن مرّت بمشغّل مقارنة على <c>decimal</c>، ولا يلتقط منطقاً على أعداد صحيحة.
///   </description></item>
///   <item><description>
///     منطق يهاجر إلى <b>مشروع آخر</b> يعتمد عليه السطح. القاعدة تحرس تجميعة واحدة.
///   </description></item>
/// </list>
/// </summary>
public sealed class Rule13_NoBusinessLogicInTheApi
{
    /// <summary>مسح تجميعة السطح — يُقرأ مرّة ويُعاد استعماله في كل بند.</summary>
    private static AssemblyScan Api { get; } = AssemblyScan.Of(ModuleMap.Api);

    /// <summary>مسح النواة المشتركة — مرجع المقارنة الذي يُثبت أن الماسح يرى فعلاً.</summary>
    private static AssemblyScan SharedKernel { get; } = AssemblyScan.Of(ModuleMap.SharedKernel);

    /// <summary>مصادر <c>Babel.Api</c> بلا تعليقات: القاعدة تفحص ما يُنفَّذ لا ما يُشرَح.</summary>
    private static IReadOnlyList<(string Path, string Code)> ApiSources { get; } = LoadApiSources();

    // ── (أ) لا حساب على المال ────────────────────────────────────────────────

    /// <summary>
    /// أعضاء الحساب على <c>System.Decimal</c> وعلى <c>Money</c>. القائمة تشمل المشغِّلات
    /// والدوال المسمّاة معاً: <c>a + b</c> و<c>decimal.Add(a, b)</c> شيء واحد في IL بعد
    /// المُصرِّف، والاثنان ممنوعان.
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenArithmetic =
    [
        "System.Decimal::op_Addition",
        "System.Decimal::op_Subtraction",
        "System.Decimal::op_Multiply",
        "System.Decimal::op_Division",
        "System.Decimal::op_Modulus",
        "System.Decimal::op_UnaryNegation",
        "System.Decimal::op_Increment",
        "System.Decimal::op_Decrement",
        "System.Decimal::Add",
        "System.Decimal::Subtract",
        "System.Decimal::Multiply",
        "System.Decimal::Divide",
        "System.Decimal::Remainder",
        "System.Decimal::Negate",
        "System.Decimal::Round",
        "System.Decimal::Floor",
        "System.Decimal::Ceiling",
        "System.Decimal::Truncate",

        // والمال المُغلَّف كذلك: Money يحمل المشغِّلات نفسها، وجمع مبلغين في طبقة HTTP
        // هو الحساب الممنوع بعينه ولو لم يذكر decimal.
        "Babel.SharedKernel.Money::op_Addition",
        "Babel.SharedKernel.Money::op_Subtraction",
        "Babel.SharedKernel.Money::op_UnaryNegation",
        "Babel.SharedKernel.Money::Add",
        "Babel.SharedKernel.Money::Subtract",
        "Babel.SharedKernel.Money::Negate",

        // ‏Math لا شأن له بطبقة نقل. المنع هنا أوسع من «التقريب العشري» عمداً: توقيع
        // العضو لا يُفكّ من الرمز وحده، والسطح ليس له سبب واحد لنداء Math أصلاً.
        "System.Math::Round",
        "System.Math::Floor",
        "System.Math::Ceiling",
        "System.Math::Truncate",
        "System.Math::Abs",
    ];

    [Fact]
    public void TheApiCarriesMoneyAndNeverComputesIt()
    {
        List<string> violations = [.. Api.Calls
            .Where(call => ForbiddenArithmetic.Contains(call.Target))
            .Select(static call => $"{call.DeclaringType}.{call.Method} → {call.Target}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        Assert.True(
            violations.Count == 0,
            "حساب على المال داخل الجذر التركيبي. السطح يحمل المال ولا يحسبه: أي مجموع أو فرق أو "
            + "تقريب يعيش في الدفتر أو في SQL، لا في طبقة HTTP.\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void ThePrimitiveOpcodeClaimIsVerifiedNotAssumed()
    {
        // الادّعاء: حساب decimal في IL يمرّ **دائماً** بنداء مشغّل، ولا يستعمل add/sub/mul/div/rem.
        // ولا يُفترض هنا بل يُفكّ من جسم دالة جمعٍ عشري حقيقية في هذا المستودع.
        MethodScan add = Assert.Single(
            SharedKernel.Methods,
            static method => method.DeclaringType == "Babel.SharedKernel.Money" && method.Name == "Add");

        Assert.Contains("System.Decimal::op_Addition", add.Calls);

        string[] primitives = ["add", "sub", "mul", "div", "rem", "add.ovf", "sub.ovf", "mul.ovf"];
        List<string> found = [.. add.Opcodes.Where(name => primitives.Contains(name, StringComparer.Ordinal))];

        Assert.True(
            found.Count == 0,
            "الادّعاء سقط: جمعٌ عشري استعمل أمراً أوّلياً — عندئذ لا يكفي فحص نداءات المشغِّلات:\n"
            + string.Join(", ", found));

        // وبما أن الادّعاء صمد، تُعرَض الأوامر الأوّلية في السطح للعلم لا للمنع: كلها
        // على أعداد صحيحة (طول نصّ، وفهرس سطر، وعدّ صفوف).
        int primitiveCount = Api.Methods.Sum(m => m.Opcodes.Count(name => primitives.Contains(name, StringComparer.Ordinal)));
        Assert.True(primitiveCount >= 0);
    }

    // ── (ب) لا نوع داخلي لأي وحدة ────────────────────────────────────────────

    /// <summary>
    /// السطح المنشور لكل وحدة كما يراه الجذر التركيبي. كل اسم هنا قرار واعٍ:
    /// إضافة سطر تعني أن مفردة جديدة من مفردات وحدة دخلت طبقة HTTP، وهو ما يجب أن
    /// يُقرأ في مراجعة لا أن يمرّ في تحويل نوع.
    /// </summary>
    private static readonly ImmutableHashSet<string> PublishedModuleSurface =
    [
        // نقطة تركيب الدفتر وإعداده وسطح تدقيقه — ولا شيء من استمراريته ولا من محرّكه.
        "Babel.Ledger.LedgerModuleRegistration",
        "Babel.Ledger.LedgerOptions",
        "Babel.Ledger.Audit.LedgerAuditService",
        "Babel.Ledger.Audit.LedgerChainReport",
        "Babel.Ledger.Audit.TrialBalanceRow",

        // ‏TrialBalanceReport — نموذج قراءة يُرحّله السطح ولا يحسبه، وقرارُ نشره مكتوب هنا
        // لا في تقرير: هو نوع الإرجاع لدالّة على `LedgerAuditService` (منشورة أعلاه)،
        // مركَّب من `TrialBalanceRow` (منشور أعلاه)، في فضاء `Audit` الذي ليس من فضاءات
        // الداخل المُعلَنة أدناه. أي أن كل مفرداته منشورة سلفاً وهذا اسمُ اجتماعها.
        // وكان صفّاً مجهول الاسم قبل هذا السطر — وذلك أضعفُ لا أقوى: `System.ValueTuple`
        // ليس نوعاً في تجميعة وحدة فلا يُحصى هنا أصلاً، فكان السطح يسمّي مفردة من الدفتر
        // خارج هذا الإحصاء. وتسميته تُعيدها إلى داخله.
        "Babel.Ledger.Audit.TrialBalanceReport",

        // ‏ChartAccount و ChartOfAccountsReport — نموذج قراءة لدليل الحسابات بشروط
        // الترحيل، ونوعا الإرجاع لـ`LedgerAuditService.ChartOfAccountsAsync` (منشورة
        // أعلاه) في فضاء `Audit` الذي ليس من فضاءات الداخل المُعلَنة أدناه.
        //
        // **ولماذا نشرُ اسمٍ يسمّي حساباً قرارٌ لا سهو:** القاعدة 2 تمنع الوحدة من أن
        // **تختار** حساباً — والوحدة تصف حدثاً والمصفوفة تختار. وهذان النوعان لا
        // يختاران شيئاً: هما دليل المستأجر نفسه مقروءاً، بشروطٍ كان الدفتر يرفض بها
        // (`ledger.posting.missing_subledger` و `guard.GR-COA-002`) ولا يستطيع العميل
        // بلوغها إلا **بأن يُرحِّل فيُرفَض**. أي أن نشرهما يُضيّق فجوةً بين ما يعرفه
        // الخادم وما يعرفه العميل، ولا يفتح للوحدات باباً إلى تسمية حساب: `AccountCode`
        // ما زال `internal` في الدفتر، و`Babel.Contracts` ما زال لا يكشف عضواً يسمّي
        // حساباً، وحارسا القاعدة 2 على ذلك خضراوان.
        //
        // **ولاحظ أين وقع الحدّ فعلاً:** أسماء أنواع النقل في `Babel.Api` — `PostingChartDto`
        // و `PostingChartEntryDto` — **لا تحمل كلمة «حساب»**، لأن الحارس
        // `NoAssemblyOutsideTheLedgerDeclaresAnAccountIdentifierType` يمنع أي تجميعة خارج
        // الدفتر من إعلان نوعٍ يسمّيه. والمنع في محلّه ولم يُضعَّف من أجل تسمية أجمل؛
        // والدفتر وحده يُعلن الاسم، والسطح ينقله.
        "Babel.Ledger.Audit.ChartAccount",
        "Babel.Ledger.Audit.ChartOfAccountsReport",

        // نقاط تركيب الوحدات الأفقية — دالة واحدة لكل وحدة، ولا شيء غيرها.
        "Babel.Sales.SalesModuleRegistration",
        "Babel.Purchasing.PurchasingModuleRegistration",
        "Babel.Compliance.ComplianceModuleRegistration",
        "Babel.Inventory.InventoryModuleRegistration",
        "Babel.RealEstate.RealEstateModuleRegistration",

        // ── السطح المنشور لوحدتَي المبيعات والمشتريات ────────────────────────
        //
        // **لماذا هذه الأسماء هنا، وكيف قرّرها البند (ب) نفسه:** خدمات الوحدتين كلها
        // تسكن `…​.Application`، و`Application` من فضاءات الداخل المُعلَنة أدناه —
        // فيرفضها الفحص الثاني **ولو أُضيفت إلى هذه القائمة**. أي أن هذه القاعدة لم
        // تكن تمنع «تسمية خاطئة» بل كانت تمنع **وجود سطح HTTP للوحدتين أصلاً**، ما لم
        // تُنشئا سطحاً منشوراً خارج فضاءات الداخل. وذلك ما فعلتاه: `…​.Surface`.
        //
        // والشكل مأخوذ حرفياً من الدفتر: `Babel.Ledger.Audit.LedgerAuditService`
        // وأنواع تقاريره أعلاه — خدمةٌ واحدة منشورة ونماذج قراءة، لا سياق قاعدة بيانات
        // ولا نوع صفّ ولا رمز حدث.
        //
        // **وكل اسم هنا هو نوع نقلٍ أو نقطة نداء واحدة، ولا واحد منها يسمّي حساباً**:
        // السطر يحمل `ItemGroup` — مؤهّل دور — والمصفوفة وحدها تُحوّله إلى حساب،
        // وحارس القاعدة 2 على ذلك خضراء.
        //
        // وإضافة سطر هنا تعني أن مفردة جديدة من مفردات وحدة دخلت طبقة HTTP: تُقرأ في
        // مراجعة ولا تمرّ في تحويل نوع.
        "Babel.Sales.Surface.SalesSurface",
        "Babel.Sales.Surface.SalesPartyRequest",
        "Babel.Sales.Surface.SalesParty",
        "Babel.Sales.Surface.SalesLineRequest",
        "Babel.Sales.Surface.SalesInvoiceRequest",
        "Babel.Sales.Surface.SalesCreditNoteRequest",
        "Babel.Sales.Surface.SalesDocument",
        "Babel.Sales.Surface.SalesAgingBands",
        "Babel.Sales.Surface.SalesAgingParty",
        "Babel.Sales.Surface.SalesAging",

        // ── سندات القبض ──────────────────────────────────────────────────────
        // نوعا نقلٍ لا غير، ولا واحد منهما يسمّي حساباً: السند يحمل «طريقة تسوية»
        // و«طرف خزينة» — مؤهّلَي دور — والمصفوفة وحدها تُحوّلهما إلى حسابين.
        "Babel.Sales.Surface.SalesReceiptAllocationRequest",
        "Babel.Sales.Surface.SalesReceiptRequest",

        "Babel.Purchasing.Surface.PurchasingSurface",
        "Babel.Purchasing.Surface.PurchasingPartyRequest",
        "Babel.Purchasing.Surface.PurchasingParty",
        "Babel.Purchasing.Surface.PurchasingLineRequest",
        "Babel.Purchasing.Surface.PurchasingExpenseBillRequest",
        "Babel.Purchasing.Surface.PurchasingDocument",
        "Babel.Purchasing.Surface.PurchasingAgingBands",
        "Babel.Purchasing.Surface.PurchasingAgingParty",
        "Babel.Purchasing.Surface.PurchasingAging",
        "Babel.Purchasing.Surface.PurchasingOrderRequest",
        "Babel.Purchasing.Surface.PurchasingDocumentLine",
        "Babel.Purchasing.Surface.PurchasingStockBillLineRequest",
        "Babel.Purchasing.Surface.PurchasingStockBillRequest",
        "Babel.Purchasing.Surface.PurchasingReturnRequest",

        // ── السطح المنشور لوحدة المخزون ──────────────────────────────────────
        //
        // وبالشكل نفسه وللسبب نفسه: خدمات المخزون كلّها في `Babel.Inventory.Application`
        // و`…​.Subledger`، وكلاهما من فضاءات الداخل المُعلَنة أدناه — فلا يستطيع سطح
        // ‏HTTP أن يناديها ولو أُدرجت هنا. والباب المشروع سطحٌ منشور خارجها: `…​.Surface`.
        //
        // **ولا واحد من هذه الأسماء يسمّي حساباً**: الصنف يحمل `ItemGroup` — مؤهّل دور —
        // والمصفوفة وحدها تُحوّله إلى حساب.
        "Babel.Inventory.Surface.InventorySurface",
        "Babel.Inventory.Surface.InventoryUnitFactor",
        "Babel.Inventory.Surface.InventoryItemRequest",
        "Babel.Inventory.Surface.InventoryItem",
        "Babel.Inventory.Surface.InventoryMeasure",
        "Babel.Inventory.Surface.InventoryStockMovementRequest",
        "Babel.Inventory.Surface.InventoryStockMovement",
        "Babel.Inventory.Surface.InventoryBalance",
        "Babel.Inventory.Surface.InventoryDivergence",
        "Babel.Inventory.Surface.InventoryValuationReport",
        "Babel.Inventory.InventoryOptions",

        // ── سندات الصرف وأوامر الشراء والاستلام ──────────────────────────────
        // و`PurchasingOrder` نوعٌ **بلا معرّف قيد وبلا رُحّل-سلفاً** عمداً: أمر الشراء
        // التزام تعاقدي لا واقعة محاسبية، وحقلٌ فارغ لهما كان سيُقرأ «لم يُرحَّل بعد»
        // بدل «لا يُرحَّل أبداً».
        "Babel.Purchasing.Surface.PurchasingPaymentAllocationRequest",
        "Babel.Purchasing.Surface.PurchasingPaymentRequest",
        "Babel.Purchasing.Surface.PurchasingOrderRequest",
        "Babel.Purchasing.Surface.PurchasingOrderLine",
        "Babel.Purchasing.Surface.PurchasingOrder",
        "Babel.Purchasing.Surface.PurchasingGoodsReceiptLineRequest",
        "Babel.Purchasing.Surface.PurchasingGoodsReceiptRequest",

        // وإعدادات الوحدتين — كإعدادات الدفتر أعلاه وللسبب نفسه: الجذر التركيبي هو من
        // يقرأ اتصال النشر من الإعداد ويُسلّمه، ولا سبيل إلى ذلك بلا تسمية نوع الإعداد.
        // ولا اتصال مالك في أيّهما: نشر المخطّط عملية مالك لا يملكها مسار التطبيق.
        "Babel.Sales.SalesOptions",
        "Babel.Purchasing.PurchasingOptions",

        // وإعدادات المخزون معهما — **وسببُ إضافتها بعدهما بتسليم كامل هو الدليل نفسه**:
        // ما دام لا باب HTTP يبلغ منفذ تقييم المخزون، لم يكن للجذر سببٌ يقرأ به اتصالها،
        // فبقيت تُقرأ من الافتراضي بلا أن يظهر ذلك. وأوّل باب فوق الاستلام هو ما أظهره.
        "Babel.Inventory.InventoryOptions",

        // ── العقارات: سطحٌ منشور بالشكل نفسه، ونوعُ إعداداته معه ────────────────
        // ولا نوع واحد من `Application` ولا من `Persistence` في القائمة: الوحدة تُخاطَب
        // من سطحها وحده، وطلباتُها وأجوبتها أنواعٌ في `Surface` لا في داخلها.
        //
        // ‏**ولاحظ ما ليس هنا: لا نوع طلبٍ لترحيل عقدِ إيجار.** حدث توقيع العقد مُعلَنٌ
        // في المصفوفة بأنه لا يُنشئ قيداً، فلا مورد ترحيل عليه ولا نوع يمرّره — وغياب
        // النوع من هذه القائمة هو الوجه الآخر لغياب الباب من العقد.
        "Babel.RealEstate.RealEstateOptions",
        "Babel.RealEstate.Surface.RealEstateSurface",
        "Babel.RealEstate.Surface.RealEstateProperty",
        "Babel.RealEstate.Surface.RealEstatePropertyRequest",
        "Babel.RealEstate.Surface.RealEstateUnit",
        "Babel.RealEstate.Surface.RealEstateUnitRequest",
        "Babel.RealEstate.Surface.RealEstateParty",
        "Babel.RealEstate.Surface.RealEstatePartyRequest",
        "Babel.RealEstate.Surface.RealEstateLease",
        "Babel.RealEstate.Surface.RealEstateLeaseRequest",
        "Babel.RealEstate.Surface.RealEstateInstalmentRequest",
        "Babel.RealEstate.Surface.RealEstateScheduleLine",
        "Babel.RealEstate.Surface.RealEstateRentInvoice",
        "Babel.RealEstate.Surface.RealEstateRentInvoiceRequest",
        "Babel.RealEstate.Surface.RealEstateReceipt",
        "Babel.RealEstate.Surface.RealEstateReceiptRequest",
        "Babel.RealEstate.Surface.RealEstateAllocationRequest",
        "Babel.RealEstate.Surface.RealEstateArrears",
        "Babel.RealEstate.Surface.RealEstateArrearsParty",
        "Babel.RealEstate.Surface.RealEstateArrearsBands",
    ];

    /// <summary>أجزاء فضاء اسم تدلّ على داخل وحدة، لا على سطحها المنشور.</summary>
    private static readonly string[] InternalNamespaceSegments =
        ["Persistence", "Application", "Posting", "PostingMatrix", "Accounts", "Subledger", "Migrations"];

    [Fact]
    public void TheApiNamesOnlyThePublishedSurfaceOfEachModule()
    {
        string[] moduleAssemblies = [ModuleMap.Ledger, .. ModuleMap.Horizontal];

        List<TypeReferenceScan> fromModules = [.. Api.TypeReferences
            .Where(reference => moduleAssemblies.Contains(reference.Assembly, StringComparer.Ordinal))
            .OrderBy(static reference => reference.FullName, StringComparer.Ordinal)];

        List<string> unpublished = [.. fromModules
            .Where(reference => !PublishedModuleSurface.Contains(reference.FullName))
            .Select(static reference => $"{reference.FullName} (من {reference.Assembly})")
            .Distinct(StringComparer.Ordinal)];

        Assert.True(
            unpublished.Count == 0,
            "الجذر التركيبي يسمّي نوعاً ليس في السطح المنشور لوحدته. لو كان مقصوداً فأضِفه إلى "
            + "PublishedModuleSurface بقرار معماري صريح؛ ولو لم يكن، فهو أول تسرّب من داخل وحدة إلى طبقة HTTP:\n"
            + string.Join('\n', unpublished));

        // وأي نوع من فضاء اسم داخلي مرفوض بذاته، حتى لو أُضيف سهواً إلى القائمة أعلاه.
        List<string> internals = [.. fromModules
            .Where(static reference => reference.Namespace
                .Split('.')
                .Any(segment => InternalNamespaceSegments.Contains(segment, StringComparer.Ordinal)))
            .Select(static reference => reference.FullName)
            .Distinct(StringComparer.Ordinal)];

        Assert.True(
            internals.Count == 0,
            "نوع من فضاء اسم داخلي لوحدة، مذكور في الجذر التركيبي:\n" + string.Join('\n', internals));
    }

    [Fact]
    public void TheApiNamesNoDatabaseContextAndNoPersistenceRow()
    {
        List<string> violations = [.. Api.TypeReferences
            .Where(static reference =>
                reference.Name.EndsWith("DbContext", StringComparison.Ordinal)
                || reference.Name.EndsWith("DbSet", StringComparison.Ordinal)
                || (reference.Name.EndsWith("Row", StringComparison.Ordinal)
                    && !reference.Namespace.StartsWith("Babel.Ledger.Audit", StringComparison.Ordinal)
                    && !reference.Namespace.StartsWith("Babel.Api", StringComparison.Ordinal)))
            .Select(static reference => reference.FullName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        Assert.True(
            violations.Count == 0,
            "سياق قاعدة بيانات أو نوع صفّ استمرارية مذكور في الجذر التركيبي:\n" + string.Join('\n', violations));

        // ولا حزمة استمرارية في ملف مشروع السطح: المرجع يُمنع حتى وهو غير مستعمل.
        ProjectFile api = RepositoryLayout.SourceProjects.Single(static project => project.Name == ModuleMap.Api);

        foreach (string package in api.PackageReferences)
        {
            Assert.False(
                package.Contains("EntityFrameworkCore", StringComparison.Ordinal)
                || package.StartsWith("Npgsql", StringComparison.Ordinal),
                $"حزمة استمرارية في Babel.Api.csproj: {package}");
        }
    }

    // ── (ج) لا رقم حساب ──────────────────────────────────────────────────────

    private static readonly IReadOnlySet<string> AccountWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "account", "accounts", "gl", "coa" };

    [Fact]
    public void TheApiNamesNoAccountCode()
    {
        HashSet<string> accountCodes = ReferenceData.AccountCodes;
        Assert.True(accountCodes.Count > 50, $"دليل الحسابات قُرئ بـ{accountCodes.Count} رمزاً — القاعدة تمرّ فراغاً.");

        List<string> literals = [.. Api.StringLiterals
            .Where(literal => accountCodes.Contains(literal))
            .Order(StringComparer.Ordinal)];

        Assert.True(
            literals.Count == 0,
            "رقم حساب حقيقي مكتوب حرفياً في الجذر التركيبي — القاعدة 2 ممتدّةً إلى طبقة HTTP. "
            + "تعديل دليل الحسابات لدى العميل يجب أن يبقى صفّاً في جدول، لا نشرَ إصدار:\n"
            + string.Join('\n', literals));

        // ولا نوع في السطح يُسمّى باسم حساب — بنفس معيار القاعدة 2 على الأنواع.
        Assembly api = BabelAssemblies.Named(ModuleMap.Api);

        List<string> types = [.. BabelAssemblies.TypesOf(api)
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => Identifiers.ContainsWord(type.Name, AccountWords))
            .Select(static type => type.FullName!)];

        Assert.True(types.Count == 0, "نوع يسمّي حساباً في الجذر التركيبي:\n" + string.Join('\n', types));
    }

    // ── (د) لا قرار مصفوفة ───────────────────────────────────────────────────

    [Fact]
    public void TheApiNamesNoPostingRoleAndNoEventCode()
    {
        HashSet<string> eventCodes = ReferenceData.EventCodes;
        HashSet<string> roleCodes = ReferenceData.RoleCodes;
        string[] roleNames = Enum.GetNames<PostingRole>();

        Assert.True(eventCodes.Count > 20, $"رموز الأحداث قُرئت بـ{eventCodes.Count} رمزاً — القاعدة تمرّ فراغاً.");
        Assert.True(roleCodes.Count > 50, $"رموز الأدوار قُرئت بـ{roleCodes.Count} رمزاً — القاعدة تمرّ فراغاً.");
        Assert.True(roleNames.Length >= 14, "مجموعة الأدوار في العقد أصغر من المتوقّع.");

        List<string> violations = [];

        violations.AddRange(Api.StringLiterals.Where(eventCodes.Contains).Select(static value => $"رمز حدث: «{value}»"));
        violations.AddRange(Api.StringLiterals.Where(roleCodes.Contains).Select(static value => $"رمز دور في المصفوفة: «{value}»"));
        violations.AddRange(Api.StringLiterals.Where(literal => roleNames.Contains(literal, StringComparer.Ordinal))
            .Select(static value => $"اسم دور في العقد: «{value}»"));

        Assert.True(
            violations.Count == 0,
            "الجذر التركيبي يسمّي دوراً أو حدثاً. السطح ينقل واقعة أعمال وصلت من العميل، ولا يقرّر "
            + "أي قالب تفتحه ولا أي حساب تمسّه:\n" + string.Join('\n', violations.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void TheApiSourceSelectsNoRoleAndConstructsNoEventCodeFromALiteral()
    {
        Regex roleMember = new(@"\bPostingRole\s*\.\s*[A-Z]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        Regex literalEvent = new(@"new\s+PostingEventCode\s*\(\s*""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        Regex literalRole = new(@"\bPostingRole\s*\)\s*[0-9]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

        List<string> violations = [];

        foreach ((string path, string code) in ApiSources)
        {
            if (roleMember.IsMatch(code))
            {
                violations.Add($"{path}: وصولٌ إلى عضو من PostingRole — اختيار دور، لا نقلٌ له.");
            }

            if (literalEvent.IsMatch(code))
            {
                violations.Add($"{path}: بناء PostingEventCode من نصّ حرفي — تسمية حدث، لا نقلٌ له.");
            }

            if (literalRole.IsMatch(code))
            {
                violations.Add($"{path}: تحويل رقم إلى PostingRole — ترتيب التعداد ليس عقداً.");
            }
        }

        Assert.True(violations.Count == 0, "قرار مصفوفة ترحيل في الجذر التركيبي:\n" + string.Join('\n', violations));

        // والشكل المطلوب موجود فعلاً: التعدادات تُقرأ من نصّ العميل بمطابقة حرفية.
        Assert.Contains("System.Enum::TryParse", Api.Calls.Select(static call => call.Target));

        Assert.Contains(
            ApiSources,
            file => file.Code.Contains("ignoreCase: false", StringComparison.Ordinal));
    }

    // ── (هـ) لا شيء يعتمد على الجذر التركيبي ─────────────────────────────────

    [Fact]
    public void NothingDependsOnTheCompositionRootIncludingItsOwnTestSuite()
    {
        List<string> violations = [.. RepositoryLayout.Projects
            .Where(static project => project.Name != ModuleMap.Api && project.Name != "Babel.ArchitectureTests")
            .Where(static project => project.ProjectReferences.Contains(ModuleMap.Api, StringComparer.Ordinal))
            .Select(static project => project.RelativePath)];

        Assert.True(
            violations.Count == 0,
            "مشروع يعتمد على الجذر التركيبي. ومجموعة اختبارات السطح ليست استثناءً: هي تُقلع الثنائي "
            + "عمليةً مستقلّة وتخاطبه عبر HTTP، لأن ذلك — لا مرجع المشروع — هو ما يُثبت العزل:\n"
            + string.Join('\n', violations));

        // وحضور مجموعة الاختبارات نفسه يُتحقَّق منه: لو غابت لمرّ الاختبار أعلاه بلا معنى.
        Assert.Contains(RepositoryLayout.Projects, static project => project.Name == "Babel.Api.Tests");
    }

    // ── حارس اللافراغ ────────────────────────────────────────────────────────

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // ١ — الماسح رأى تجميعة حقيقية: أنواعاً، ودوالّ بأجسام، وتعليمات مفكوكة.
        Assert.True(Api.TypeCount >= 20, $"عدد الأنواع الممسوحة {Api.TypeCount} أقل من أن يثبت شيئاً.");
        Assert.True(Api.Methods.Count >= 100, $"عدد الدوالّ ذات الأجسام {Api.Methods.Count} أقل من أن يثبت شيئاً.");
        Assert.True(Api.InstructionCount >= 3000, $"عدد التعليمات المفكوكة {Api.InstructionCount} أقل من أن يثبت شيئاً.");
        Assert.True(Api.Calls.Count >= 300, $"عدد النداءات المفحوصة {Api.Calls.Count} أقل من أن يثبت شيئاً.");
        Assert.True(Api.StringLiterals.Count >= 100, $"عدد النصوص الحرفية {Api.StringLiterals.Count} أقل من أن يثبت شيئاً.");
        Assert.True(Api.TypeReferences.Count >= 50, $"عدد مراجع الأنواع {Api.TypeReferences.Count} أقل من أن يثبت شيئاً.");
        Assert.True(ApiSources.Count >= 10, $"عدد الملفات المصدرية {ApiSources.Count} أقل من أن يثبت شيئاً.");

        // ٢ — والماسح يلتقط المخالفة حين توجد فعلاً: النواة المشتركة **تحسب** المال،
        //     فلو مسحناها بالبند (أ) لسقطت. هذا هو ما يمنع البند من أن يكون ادّعاءً فارغاً.
        List<string> inSharedKernel = [.. SharedKernel.Calls
            .Where(call => ForbiddenArithmetic.Contains(call.Target))
            .Select(static call => call.Target)
            .Distinct(StringComparer.Ordinal)];

        Assert.True(
            inSharedKernel.Count >= 2,
            "الماسح لم يجد حساباً عشرياً في Babel.SharedKernel — وهناك حسابٌ عشري مؤكَّد (Money.Add/Subtract). "
            + "أي أن الماسح توقّف عن الرؤية، وكل بند فوقه يمرّ فراغاً.");

        Assert.Contains("System.Decimal::op_Addition", inSharedKernel);

        // ٣ — والبيانات المرجعية قُرئت فعلاً، فمقارنة النصوص الحرفية لها ليست مقارنة بالفراغ.
        Assert.True(ReferenceData.AccountCodes.Count > 50);
        Assert.True(ReferenceData.EventCodes.Count > 20);
        Assert.True(ReferenceData.RoleCodes.Count > 50);
        Assert.Contains("1101", ReferenceData.AccountCodes);
        Assert.Contains("settlement_account", ReferenceData.RoleCodes);

        // ٤ — وتجريد التعليقات يعمل: القاعدة تفحص ما يُنفَّذ لا ما يُشرَح، وهذا الملف نفسه
        //     يذكر الأشكال الممنوعة حرفياً كي يُبيّن سبب منعها.
        Assert.Equal(" ", StripComments("// PostingRole.NetAmount"));
        Assert.Equal(" ", StripComments("/* new PostingEventCode(\"x.y.z\") */"));
        Assert.Contains("PostingRole.NetAmount", StripComments("var r = PostingRole.NetAmount; // تعليق"), StringComparison.Ordinal);
    }

    // ── الأدوات ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<(string Path, string Code)> LoadApiSources()
    {
        string root = Path.Combine(RepositoryLayout.Root, "src", ModuleMap.Api);

        return [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(static path => (Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/'), StripComments(File.ReadAllText(path))))];
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"//.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return text;
    }

    /// <summary>البيانات المرجعية التي تُقارَن بها النصوص الحرفية — مقروءة من <c>data/</c> نفسها.</summary>
    private static class ReferenceData
    {
        public static HashSet<string> AccountCodes { get; } = LoadAccountCodes();

        public static HashSet<string> RoleCodes { get; } = LoadRoleCodes();

        public static HashSet<string> EventCodes { get; } = LoadEventCodes();

        private static HashSet<string> LoadAccountCodes()
        {
            string path = Path.Combine(RepositoryLayout.Root, "data", "chart-of-accounts", "accounts.csv");
            return [.. File.ReadLines(path)
                .Skip(1)
                .Select(static line => line.Split(',')[0])
                .Where(static code => code.Length >= 3 && code.All(char.IsAsciiDigit))];
        }

        private static HashSet<string> LoadRoleCodes()
        {
            string path = Path.Combine(RepositoryLayout.Root, "data", "posting-matrix", "account-roles.csv");
            return [.. File.ReadLines(path)
                .Skip(1)
                .Select(static line => line.Split(',')[0])
                .Where(static code => code.Length > 0)];
        }

        private static HashSet<string> LoadEventCodes()
        {
            string directory = Path.Combine(RepositoryLayout.Root, "data", "posting-matrix", "events");
            HashSet<string> codes = [];

            foreach (string file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));

                if (!document.RootElement.TryGetProperty("events", out JsonElement events))
                {
                    continue;
                }

                foreach (JsonElement declared in events.EnumerateArray())
                {
                    if (declared.TryGetProperty("event_code", out JsonElement code) && code.GetString() is { } value)
                    {
                        codes.Add(value);
                    }
                }
            }

            return codes;
        }
    }

    /// <summary>نداء واحد كما فُكّ من IL.</summary>
    private sealed record CallScan(string DeclaringType, string Method, string Target);

    /// <summary>دالة واحدة بجسمها المفكوك.</summary>
    private sealed record MethodScan(string DeclaringType, string Name, IReadOnlyList<string> Opcodes, IReadOnlyList<string> Calls);

    /// <summary>مرجع نوع من تجميعة أخرى.</summary>
    private sealed record TypeReferenceScan(string Assembly, string Namespace, string Name)
    {
        public string FullName => Namespace.Length == 0 ? Name : Namespace + "." + Name;
    }

    /// <summary>
    /// ماسح IL مبنيّ على <c>System.Reflection.Metadata</c> — من مكتبة الأساس، بلا حزمة جديدة.
    /// <para>
    /// ولماذا لا يكفي الانعكاس العادي: <c>MethodInfo</c> لا يعطي جسم الدالة. والقاعدة هنا
    /// تفحص <b>ما يُنفَّذ</b> — أي التعليمات نفسها — لا تواقيع الأنواع.
    /// </para>
    /// </summary>
    private sealed class AssemblyScan
    {
        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodeTable();

        private AssemblyScan(
            int typeCount,
            int instructionCount,
            IReadOnlyList<MethodScan> methods,
            IReadOnlyList<CallScan> calls,
            IReadOnlySet<string> stringLiterals,
            IReadOnlyList<TypeReferenceScan> typeReferences)
        {
            TypeCount = typeCount;
            InstructionCount = instructionCount;
            Methods = methods;
            Calls = calls;
            StringLiterals = stringLiterals;
            TypeReferences = typeReferences;
        }

        public int TypeCount { get; }

        public int InstructionCount { get; }

        public IReadOnlyList<MethodScan> Methods { get; }

        public IReadOnlyList<CallScan> Calls { get; }

        public IReadOnlySet<string> StringLiterals { get; }

        public IReadOnlyList<TypeReferenceScan> TypeReferences { get; }

        public static AssemblyScan Of(string assemblyName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"لم يُعثر على {path} — القاعدة 13 تفحص IL ولا تستطيع أن تفحص ما لم يُبنَ. / "
                    + $"{path} was not found; Rule 13 reads IL and cannot read what was not built.");
            }

            using FileStream stream = File.OpenRead(path);
            using PEReader pe = new(stream);
            MetadataReader reader = pe.GetMetadataReader();

            List<MethodScan> methods = [];
            List<CallScan> calls = [];
            HashSet<string> literals = new(StringComparer.Ordinal);
            int instructions = 0;

            foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
            {
                MethodDefinition definition = reader.GetMethodDefinition(handle);
                if (definition.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                string declaringType = FullNameOf(reader, definition.GetDeclaringType());
                string name = reader.GetString(definition.Name);

                MethodBodyBlock body = pe.GetMethodBody(definition.RelativeVirtualAddress);
                (List<string> opcodes, List<string> targets, List<string> strings) = Decode(reader, body.GetILContent());

                instructions += opcodes.Count;
                methods.Add(new MethodScan(declaringType, name, opcodes, targets));
                calls.AddRange(targets.Select(target => new CallScan(declaringType, name, target)));
                literals.UnionWith(strings);
            }

            List<TypeReferenceScan> typeReferences = [];

            foreach (TypeReferenceHandle handle in reader.TypeReferences)
            {
                TypeReference reference = reader.GetTypeReference(handle);
                typeReferences.Add(new TypeReferenceScan(
                    ResolutionAssembly(reader, reference.ResolutionScope),
                    reference.Namespace.IsNil ? string.Empty : reader.GetString(reference.Namespace),
                    reader.GetString(reference.Name)));
            }

            return new AssemblyScan(
                reader.TypeDefinitions.Count,
                instructions,
                methods,
                calls,
                literals,
                typeReferences);
        }

        /// <summary>
        /// يفكّ تدفّق IL تعليمةً تعليمة. حجم المُعامل يُقرأ من <c>OperandType</c> للأمر نفسه،
        /// لا من جدول مكتوب بيد ينحرف عن المواصفة.
        /// </summary>
        private static (List<string> Opcodes, List<string> Calls, List<string> Strings) Decode(
            MetadataReader reader,
            ImmutableArray<byte> il)
        {
            List<string> opcodes = [];
            List<string> calls = [];
            List<string> strings = [];

            int offset = 0;

            while (offset < il.Length)
            {
                short value = il[offset];
                offset++;

                if (value == 0xFE)
                {
                    value = (short)(0xFE00 | il[offset]);
                    offset++;
                }

                if (!OpCodesByValue.TryGetValue(value, out OpCode opcode))
                {
                    // أمر غير معروف: لا يمكن معرفة حجم مُعامله، فالمسح يتوقّف بصوت عالٍ
                    // بدل أن يستمرّ على إزاحة خاطئة ويقرأ ضجيجاً يظنّه تعليمات.
                    throw new InvalidOperationException(
                        FormattableString.Invariant($"أمر IL غير معروف 0x{value:X4} عند الإزاحة {offset - 1}."));
                }

                opcodes.Add(opcode.Name ?? "?");

                int operandSize = SizeOf(opcode.OperandType, il, offset);

                if (opcode.OperandType == OperandType.InlineMethod && offset + 4 <= il.Length)
                {
                    string? target = ResolveMethod(reader, ReadInt32(il, offset));
                    if (target is not null)
                    {
                        calls.Add(target);
                    }
                }
                else if (opcode.OperandType == OperandType.InlineString && offset + 4 <= il.Length)
                {
                    int token = ReadInt32(il, offset);
                    if ((token & 0x70000000) == 0x70000000)
                    {
                        strings.Add(reader.GetUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF)));
                    }
                }

                offset += operandSize;
            }

            return (opcodes, calls, strings);
        }

        private static int SizeOf(OperandType type, ImmutableArray<byte> il, int offset) => type switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * ReadInt32(il, offset)),
            _ => 4,
        };

        private static int ReadInt32(ImmutableArray<byte> il, int offset) =>
            il[offset] | (il[offset + 1] << 8) | (il[offset + 2] << 16) | (il[offset + 3] << 24);

        private static string? ResolveMethod(MetadataReader reader, int token)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(token);

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    MethodDefinition definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    return FullNameOf(reader, definition.GetDeclaringType()) + "::" + reader.GetString(definition.Name);
                }

                case HandleKind.MemberReference:
                {
                    MemberReference reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                    return ParentName(reader, reference.Parent) + "::" + reader.GetString(reference.Name);
                }

                case HandleKind.MethodSpecification:
                {
                    MethodSpecification specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    return ResolveMethod(reader, MetadataTokens.GetToken(specification.Method));
                }

                default:
                    return null;
            }
        }

        private static string ParentName(MetadataReader reader, EntityHandle parent) => parent.Kind switch
        {
            HandleKind.TypeReference => FullNameOf(reader, (TypeReferenceHandle)parent),
            HandleKind.TypeDefinition => FullNameOf(reader, (TypeDefinitionHandle)parent),
            HandleKind.TypeSpecification => "<generic>",
            _ => "<unknown>",
        };

        private static string FullNameOf(MetadataReader reader, TypeDefinitionHandle handle)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);
            string name = reader.GetString(definition.Name);
            string @namespace = definition.Namespace.IsNil ? string.Empty : reader.GetString(definition.Namespace);
            return @namespace.Length == 0 ? name : @namespace + "." + name;
        }

        private static string FullNameOf(MetadataReader reader, TypeReferenceHandle handle)
        {
            TypeReference reference = reader.GetTypeReference(handle);
            string name = reader.GetString(reference.Name);
            string @namespace = reference.Namespace.IsNil ? string.Empty : reader.GetString(reference.Namespace);
            return @namespace.Length == 0 ? name : @namespace + "." + name;
        }

        private static string ResolutionAssembly(MetadataReader reader, EntityHandle scope) => scope.Kind switch
        {
            HandleKind.AssemblyReference =>
                reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),
            HandleKind.TypeReference => ResolutionAssembly(reader, reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope),
            _ => string.Empty,
        };

        private static Dictionary<short, OpCode> BuildOpCodeTable()
        {
            Dictionary<short, OpCode> table = [];

            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is OpCode opcode)
                {
                    table[opcode.Value] = opcode;
                }
            }

            if (table.Count < 200)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"جدول أوامر IL بُني بـ{table.Count} أمراً فقط — الفكّ سيقرأ ضجيجاً."));
            }

            return table;
        }
    }
}
