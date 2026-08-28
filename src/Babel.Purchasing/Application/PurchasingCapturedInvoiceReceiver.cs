using System.Globalization;
using Babel.Contracts.Capture;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>
/// <b>الطرف المفقود من السلسلة: مستقبِل الفاتورة الملتقَطة في وحدة المشتريات.</b>
/// <para>
/// وحدة الالتقاط تقرأ رمز فاتورة المورد فتُخرج <b>مسوّدة ما قبل الإدخال</b> بمصدر لكل
/// حقل، ويؤكّدها إنسان، ثم تُسلّم <see cref="PromotionOrder"/> عبر منفذ في العقود. وهذا
/// هو من ينفّذ المنفذ — <b>ولا يكتب صفّاً واحداً بنفسه</b>: يحلّ المورد بخدمة الموردين،
/// ويُنشئ الفاتورة بـ<see cref="SupplierBillService.CreateExpenseBillAsync"/>. فكل ما
/// يفرضه ذلك المسار — رقم غير مكرَّر، ومورد قائم، وحساب سطر واحد، ومهلة سداد المورد —
/// يُفرض على المستند الملتقَط كما يُفرض على المستند المكتوب باليد.
/// </para>
/// <para>
/// <b>وما لا يفعله:</b> لا يُرحّل. الترحيل خطوة اعتماد مستقلّة في هذه الوحدة
/// (<see cref="SupplierBillService.PostBillAsync"/>)، ومستندٌ ملتقَط يبلغ الدفتر بخطوة
/// واحدة بينما المكتوب باليد يبلغه بخطوتين هو <b>مسار ثانٍ أقصر</b> — وهو بالضبط صنف
/// العطل الذي أنفق هذا المستودع شهراً في إزالته.
/// </para>
/// <para>
/// <b>وثلاثة رفوض قبل أي كتابة</b>، وكلها من صنف واحد: «لا يُخمَّن ما لم يُقَل».
/// <list type="number">
///   <item>حدثٌ غير حدث فاتورة المصروف — الفاتورة المخزنية تحتاج أضلاعاً لا تُخترع من صورة.</item>
///   <item>رقمٌ ضريبي غير <b>مُصدَّق</b> — المطابقة الآلية امتياز المُصدَّق وحده.</item>
///   <item>تصنيف مصروف لم يكتبه إنسان — التصنيف مؤهّل دور يختار حساباً.</item>
/// </list>
/// </para>
/// </summary>
public sealed class PurchasingCapturedInvoiceReceiver : ICapturedInvoiceReceiver, IApplicationService
{
    /// <summary>الحدث الوحيد الذي تُنشئ منه الترقية مستنداً في هذه الوحدة.</summary>
    internal const string ExpenseBillEvent = "purchasing.invoice.expense.posted";

    /// <summary>
    /// المؤهّل العام في خريطة الأدوار — <b>«مصروف غير مصنَّف» وظاهرٌ أنه كذلك</b>.
    /// وهو صفٌّ قائم في <c>role-map.default.csv</c> لا قيمة تُخترع هنا.
    /// </summary>
    internal const string UnqualifiedExpenseCategory = "*";

    private readonly SupplierService _suppliers;
    private readonly SupplierBillService _bills;

    /// <summary>ينشئ المستقبِل.</summary>
    /// <param name="suppliers">خدمة الموردين — بها يُحلّ الرقم الضريبي إلى مورد.</param>
    /// <param name="bills">خدمة فواتير الموردين — بها تُنشأ الفاتورة، ولا سبيل غيرها.</param>
    public PurchasingCapturedInvoiceReceiver(SupplierService suppliers, SupplierBillService bills)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        ArgumentNullException.ThrowIfNull(bills);
        _suppliers = suppliers;
        _bills = bills;
    }

    /// <inheritdoc />
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PromotedDocumentReference>> ReceiveAsync(
        PromotionOrder order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        // ── 1 · الحدث: فاتورة مصروف لا غير ────────────────────────────────────
        if (!string.Equals(order.EventCode, ExpenseBillEvent, StringComparison.Ordinal))
        {
            return Result<PromotedDocumentReference>.Failure(
                PurchasingErrors.PromotionEventNotASupplierBill(order.EventCode, ExpenseBillEvent));
        }

        // ── 2 · الرقم الضريبي مُصدَّق أو لا مطابقة ────────────────────────────
        // المصدر يعبر مع الأمر، فالرفض هنا ممكن. ورقمٌ مقروء ضوئياً يُطابق مورداً بعينه
        // يُنتج إسناداً يحمل **مظهر التحقّق** بلا التحقّق — وذلك أسوأ من غياب الإسناد.
        FieldProvenance? vatSource = order.ProvenanceOf(PromotionFields.SellerVatNumber);
        if (vatSource != FieldProvenance.Attested)
        {
            return Result<PromotedDocumentReference>.Failure(
                PurchasingErrors.PromotionVatNumberNotAttested(Describe(vatSource)));
        }

        // ── 3 · تصنيف المصروف: بيد إنسان، أو المؤهّل العام ────────────────────
        Result<string> category = CategoryOf(order);
        if (category.IsFailure)
        {
            return Result<PromotedDocumentReference>.Failure(category.Errors);
        }

        // ── 4 · المورد: يُطابَق أو يُرفض، ولا يُختار أبداً ─────────────────────
        // ثلاث نتائج لا اثنتان: واحد فعّال ⇒ نجاح؛ لا أحد ⇒ رفض؛ أكثر من واحد ⇒ رفض
        // بالغموض مسمّياً المرشّحين. والرفض يعبر كما هو إلى شاشة الترقية.
        Result<SupplierView> supplier = await _suppliers
            .FindByVatNumberAsync(order.Tenant, order.PromotedBy, order.SupplierVatNumber, cancellationToken)
            .ConfigureAwait(false);

        if (supplier.IsFailure)
        {
            return Result<PromotedDocumentReference>.Failure(supplier.Errors);
        }

        // ── 5 · حساب الوحدة يجب أن يوافق الأرقام المُصدَّقة ───────────────────
        // الفاتورة تُحسب من سطورها بتقريب على مستوى السطر، والرمز يحمل إجمالياً كتبه
        // المُصدِر. فإن اختلفا — ولو بهللة — **يُرفض ولا يُكتب فوق رقم مُصدَّق**: كتابةُ
        // رقم محسوب مكان رقم موقَّع تُنتج فاتورة تخالف ما وقّعه المورد، ولا يظهر ذلك
        // إلا عند المطابقة معه بعد أشهر.
        Result computed = EnsureComputationMatchesAttested(order);
        if (computed.IsFailure)
        {
            return Result<PromotedDocumentReference>.Failure(computed.Errors);
        }

        // ── 6 · الإنشاء بالمسار المعتاد ───────────────────────────────────────
        // ولا مركز تكلفة هنا: الفراغ يعني «لم يُذكر»، فيحلّه ICostCenterResolver إلى
        // المركز الافتراضي للمنشأة (‏ADR-0026) — نفسه الذي تحصل عليه أي فاتورة مصروف
        // لم يُذكر عليها مركز. وهو تكوينُ المستأجر لا تخمينُ نموذج.
        ExpenseBillDraft draft = new(
            order.InvoiceNumber,
            supplier.Value.Id,
            order.IssuedOn,
            category.Value,
            string.Empty,
            [.. order.Lines.OrderBy(static line => line.LineNo).Select(line => LineOf(order, line))]);

        Result<PurchasingDocumentView> created = await _bills
            .CreateExpenseBillAsync(order.Tenant, order.PromotedBy, draft, cancellationToken)
            .ConfigureAwait(false);

        return created.IsFailure
            ? Result<PromotedDocumentReference>.Failure(created.Errors)
            : Result<PromotedDocumentReference>.Success(new PromotedDocumentReference(
                BabelModule.Purchasing,
                SupplierBillService.BillDocument,
                created.Value.Id.ToString("D", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// تصنيف المصروف: مكتوباً بيد إنسان، أو المؤهّل العام حين لا يُكتب.
    /// <para>
    /// و<b>لا يُقبل مصدر ثالث</b>: مفردات النموذج المغلقة رموزُ أحداث وأدوار، وليس فيها
    /// مؤهّلات — فمؤهّلٌ «مقترَح» سلسلةٌ حرّة تبلغ خريطة الأدوار بلا فحص، وتختار حساباً.
    /// </para>
    /// </summary>
    private static Result<string> CategoryOf(PromotionOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.ExpenseCategory))
        {
            return Result<string>.Success(UnqualifiedExpenseCategory);
        }

        FieldProvenance? source = order.ProvenanceOf(PromotionFields.ExpenseCategory);

        return source == FieldProvenance.Typed
            ? Result<string>.Success(order.ExpenseCategory)
            : Result<string>.Failure(PurchasingErrors.PromotionExpenseCategoryNotTyped(Describe(source)));
    }

    /// <summary>
    /// يتحقّق أن ما تحسبه الوحدة من السطور يساوي ما يحمله الأمر — صافياً وضريبةً وإجمالياً.
    /// </summary>
    private static Result EnsureComputationMatchesAttested(PromotionOrder order)
    {
        decimal net = 0m;
        decimal tax = 0m;

        foreach (PromotionLine line in order.Lines)
        {
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice, 0m, order.TaxRate, ClassificationOf(order));
            net += lineNet;
            tax += lineTax;
        }

        if (net == order.Net && tax == order.TaxTotal && net + tax == order.GrossTotal)
        {
            return Result.Success();
        }

        return Result.Failure(PurchasingErrors.PromotionTotalsDisagreeWithAttested(
            net, order.Net, tax, order.TaxTotal, net + tax, order.GrossTotal));
    }

    /// <summary>
    /// التصنيف الضريبي مشتقٌّ من ضريبة الأمر نفسها لا مخمَّن: ضريبة موجبة ⇒ خاضع.
    /// </summary>
    private static string ClassificationOf(PromotionOrder order)
        => order.TaxTotal > 0m ? "standard" : "exempt";

    /// <summary>
    /// سطر الفاتورة كما تفهمه الوحدة.
    /// <para>
    /// <b>ولا صنف ولا مجموعة صنف:</b> قالب المصروف في المصفوفة لا دفتر مساعد لأصنافه،
    /// ومؤهّله <c>line.expense_category</c> لا <c>line.item_group</c>. فمعرّف صنف مخترَع
    /// هنا كان سيدخل دفتر الأصناف باسم لا يقابله صنف.
    /// </para>
    /// <para>
    /// و<c>TaxRecoverable</c> يبقى على افتراض الوحدة نفسه للفاتورة المكتوبة باليد: نشاطٌ
    /// خاضع بالكامل. والنشاط المختلط سيناريو <b>موقوف في المصفوفة نفسها</b> بانتظار
    /// المستشار الضريبي، فلا يُبنى له مسارٌ من هنا.
    /// </para>
    /// </summary>
    private static PurchaseLineDraft LineOf(PromotionOrder order, PromotionLine line) => new(
        string.Empty,
        UnqualifiedExpenseCategory,
        new LocalizedName(line.Description, line.Description),
        line.Quantity,

        // فاتورة مصروف لا تُحرّك مخزوناً، فوحدتها **العدّ صراحةً** لا فراغٌ يُقرأ
        // «وحدة مجهولة». والفرق بينهما هو الفرق بين صفٍّ يُجمَع وصفٍّ لا يُدرى بأي مقياس.
        Babel.Contracts.Inventory.InventoryUnits.Each,
        Money.Of(line.UnitPrice, order.Currency),
        ClassificationOf(order),
        order.TaxRate);

    private static string Describe(FieldProvenance? provenance)
        => provenance is null ? "(غائب)" : provenance.Value.ToString();
}
