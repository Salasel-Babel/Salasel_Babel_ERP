using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>رمز نوع المستند، بصيغة <c>&lt;وحدة&gt;.&lt;مستند&gt;</c>.</summary>
/// <param name="Value">الرمز كما في الكتالوج.</param>
public readonly record struct DocumentTypeCode(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>رمز القدرة داخل نوع مستند.</summary>
/// <param name="Value">الرمز كما في الكتالوج.</param>
public readonly record struct CapabilityCode(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// تعريف قدرة واحدة: اسمها، و<b>الأحداث التي تفتحها في المصفوفة</b>، والحقول التي ترخّصها
/// على المستند.
/// </summary>
/// <param name="Code">رمز القدرة.</param>
/// <param name="NameAr">
/// الاسم العربي — <b>إلزامي، وهو الارتداد المضمون عند غياب ترجمة</b> (ADR-0021). ويعيش هنا
/// لا في الواجهة لأن المجموعة مغلقة ويملكها الخادم: واجهةٌ بلا ترجمة لهذا المفتاح تعرض
/// المفتاح نفسه، وهو ما يبدو انهياراً فيُتخطّى بلا بلاغ.
/// </param>
/// <param name="NameKey">
/// مفتاح الترجمة — <b>وليس ثنائية لغتين</b>. الإنجليزية واحدة من N، تُضاف كصفّ ترجمة كغيرها.
/// </param>
/// <param name="RequiredEvents">
/// الأحداث التي تصير قابلة للوقوع بتشغيل هذه القدرة. <b>قائمة غير فارغة بحكم البناء</b>:
/// قدرة بلا حدث ليست قدرة محاسبية بل تفضيل شاشة، ولا مكان لها هنا.
/// </param>
/// <param name="Fields">الحقول التي ترخّصها هذه القدرة على المستند.</param>
public sealed record CapabilityDefinition(
    CapabilityCode Code,
    string NameAr,
    string NameKey,
    ImmutableArray<PostingEventCode> RequiredEvents,
    ImmutableArray<string> Fields);

/// <summary>
/// تعريف نوع مستند: حدثه الأساسي، وحقوله التي لا ترتبط بقدرة، وقدراته المتاحة.
/// </summary>
/// <param name="Code">رمز نوع المستند.</param>
/// <param name="NameAr">الاسم العربي الإلزامي — الارتداد المضمون (ADR-0021).</param>
/// <param name="NameKey">مفتاح الترجمة إلى أيّ عدد من اللغات.</param>
/// <param name="Module">الوحدة المالكة — تُستعمل في الاستحقاق والعرض لا في الترحيل.</param>
/// <param name="BaseEvent">الحدث الذي يقع بمجرّد ترحيل هذا المستند، بلا أي قدرة.</param>
/// <param name="BaseFields">الحقول القائمة دائماً على هذا المستند.</param>
/// <param name="Capabilities">القدرات المتاحة لهذا النوع — <b>مجموعة مغلقة</b>.</param>
public sealed record DocumentTypeDefinition(
    DocumentTypeCode Code,
    string NameAr,
    string NameKey,
    BabelModule Module,
    PostingEventCode BaseEvent,
    ImmutableArray<string> BaseFields,
    ImmutableArray<CapabilityDefinition> Capabilities)
{
    /// <summary>يجلب تعريف قدرة، أو <c>null</c> إن لم تكن من قدرات هذا النوع.</summary>
    /// <param name="code">رمز القدرة.</param>
    public CapabilityDefinition? Find(CapabilityCode code)
        => Capabilities.FirstOrDefault(capability => capability.Code == code);
}

/// <summary>
/// <b>الكتالوج المغلق</b> — كل ما يمكن أن يختلف بين مستأجر ومستأجر، معدوداً.
/// <para>
/// <b>ولماذا مغلق:</b> البديل المرفوض هو أن يؤلّف كل عميل شاشاته بـJSON حرّ — أي لغة
/// برمجة مكتوبة بـJSON، بلا فحص أنواع ولا مُنقِّح، ومصدران متنافسان للحقيقة عن «ما هو
/// المستند الصالح». والقيمة هنا في أن المجموعة <b>معدودة وصغيرة</b>: الفارق بين عميل
/// وعميل صفوفُ بيانات، لا فرعُ شيفرة يخصّه ولا تصل إليه التحديثات.
/// </para>
/// <para>
/// <b>وشرط دخول أي سطر إلى هنا واحد:</b> أن يُجاب عن سؤال «أي حدث في مصفوفة الترحيل
/// تفتحه هذه القدرة؟». قدرة لا تفتح حدثاً هي تفضيل شاشة، ومكانها الواجهة لا هنا. وهذا
/// الشرط ليس اتفاقاً بل مفروض: <see cref="CapabilityCatalogue.IsServedBy"/> يُطابق كل
/// حدث مذكور هنا بالمصفوفة، والاختبار يُفشل البناء على أي رمز لا يقابله حدث.
/// </para>
/// </summary>
public static class CapabilityCatalogue
{
    private static readonly ImmutableArray<DocumentTypeDefinition> All =
    [
        new DocumentTypeDefinition(
            new DocumentTypeCode("sales.invoice"),
            "فاتورة مبيعات",
            "document_type.sales.invoice",
            BabelModule.Sales,
            new PostingEventCode("sales.invoice.posted"),
            ["customer", "lines", "paymentMethod"],
            [
                new CapabilityDefinition(
                    new CapabilityCode("advance"),
                    "دفعة مقدمة من العميل",
                    "capability.advance",
                    [new PostingEventCode("sales.advance.received"), new PostingEventCode("sales.advance.applied")],
                    ["advanceApplied"]),

                new CapabilityDefinition(
                    new CapabilityCode("cost_of_sales"),
                    "تكلفة المبيعات بالجرد المستمر",
                    "capability.cost_of_sales",
                    [new PostingEventCode("sales.invoice.cost_of_sales")],
                    ["warehouse"]),
            ]),

        // ═══════════════════════════════════════════════════════════════════
        // فاتورة المورد — والسؤال الوحيد المفتوح فيها: ما حدثها الأساسي؟
        // ═══════════════════════════════════════════════════════════════════
        //
        // ‏**الحدث الأساسي هو ما يقع بلا أي قدرة**، أي ما تستطيع منشأةٌ لم تشترِ شيئاً
        // إضافياً أن تفعله. وفاتورة المورد شكلان في هذه الوحدة: مصروفية ومخزنية.
        //
        // والمخزنية **غير قابلة للتعبير** بلا قدرة: ‏<c>CreateStockBillAsync</c> يطلب
        // معرّف استلام في توقيعه، والاستلام يطلب أمر شراء، ولا يقع أيٌّ منهما بلا
        // <c>three_way_match</c>. فحدثٌ أساسي مخزني كان سيسمّي واقعةً **لا يستطيع أن
        // يبلغها مستأجرٌ بلا قدرة أبداً** — أي حدثاً أساسياً لا أساس له.
        //
        // والأخطر أنه كان سيُفرغ القدرة من معناها: ‏<c>purchasing.invoice.stock.posted</c>
        // مفتوحاً بالأساس يعني أن <c>three_way_match</c> تفتح حدثاً **مفتوحاً أصلاً**،
        // وتلك «قدرة يمكن ممارستها رغم إطفائها» — أي زينة لا قدرة (‏ADR-0023).
        //
        // ومحاسبياً: القالب المخزني **يُدين** «بضاعة مستلمة غير مفوترة» ليستنفد رصيداً
        // أنشأه حدث الاستلام. فبلا استلام يُدان حسابٌ لم يُدَن قطّ — رصيد دفتر مساعد
        // سالب لا يُطابَق، وهو صنف الانحراف الصامت الذي دفع هذا المستودع ثمنه.
        //
        // فالحدث الأساسي **مصروفي**: ‏<c>CreateExpenseBillAsync</c> يطلب مورداً وسطوراً
        // ولا شيء غيرهما، وهو الشكل الوحيد التامّ بذاته — وهو أيضاً شكل الفاتورة
        // الملتقَطة من رمز مورد بلا أمر شراء ولا استلام (‏ADR-0024).
        new DocumentTypeDefinition(
            new DocumentTypeCode("purchasing.supplier_bill"),
            "فاتورة مورد",
            "document_type.purchasing.supplier_bill",
            BabelModule.Purchasing,
            new PostingEventCode("purchasing.invoice.expense.posted"),
            ["supplier", "lines", "costCenter", "expenseCategory"],
            [
                new CapabilityDefinition(
                    new CapabilityCode("three_way_match"),
                    "المطابقة الثلاثية",
                    "capability.three_way_match",
                    [
                        new PostingEventCode("purchasing.goods_receipt.posted"),
                        new PostingEventCode("purchasing.invoice.stock.posted"),
                    ],
                    ["receipt"]),

                new CapabilityDefinition(
                    new CapabilityCode("landed_cost"),
                    "تكاليف الاستيراد المحمَّلة",
                    "capability.landed_cost",
                    [new PostingEventCode("purchasing.landed_cost.allocated")],
                    ["landedCost"]),
            ]),

        new DocumentTypeDefinition(
            new DocumentTypeCode("projects.client_certificate"),
            "مستخلص عميل",
            "document_type.projects.client_certificate",
            BabelModule.Projects,
            new PostingEventCode("projects.client_certificate.posted"),
            ["contract", "workValue"],
            [
                new CapabilityDefinition(
                    new CapabilityCode("advance"),
                    "دفعة مقدمة من العميل",
                    "capability.advance",
                    [new PostingEventCode("sales.advance.received"), new PostingEventCode("sales.advance.applied")],
                    ["advanceRecovery"]),

                new CapabilityDefinition(
                    new CapabilityCode("retention"),
                    "المحتجز",
                    "capability.retention",
                    [new PostingEventCode("projects.client_retention.collected")],
                    ["retention"]),
            ]),
    ];

    /// <summary>أنواع المستندات المعرَّفة، مرتَّبة ترتيباً حرفياً ثابتاً.</summary>
    public static ImmutableArray<DocumentTypeDefinition> DocumentTypes { get; } =
        [.. All.OrderBy(static definition => definition.Code.Value, StringComparer.Ordinal)];

    /// <summary>يجلب تعريف نوع مستند، أو <c>null</c> إن لم يكن في الكتالوج.</summary>
    /// <param name="code">رمز نوع المستند.</param>
    public static DocumentTypeDefinition? Find(DocumentTypeCode code)
        => DocumentTypes.FirstOrDefault(definition => definition.Code == code);

    /// <summary>
    /// كل رموز الأحداث التي يذكرها الكتالوج — الأساسية وأحداث القدرات معاً، بلا تكرار.
    /// </summary>
    public static ImmutableArray<PostingEventCode> ReferencedEvents { get; } =
    [
        .. All
            .SelectMany(static definition => definition.Capabilities
                .SelectMany(static capability => capability.RequiredEvents)
                .Append(definition.BaseEvent))
            .Select(static code => code.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(static value => new PostingEventCode(value)),
    ];

    /// <summary>
    /// الأحداث التي يذكرها الكتالوج ولا يقابلها حدث في المصفوفة. الفراغ هو الحالة السليمة.
    /// </summary>
    /// <param name="directory">فهرس أحداث المصفوفة.</param>
    public static ImmutableArray<PostingEventCode> UnservedEvents(IPostingEventDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return [.. ReferencedEvents.Where(code => !directory.Contains(code))];
    }

    /// <summary>هل تخدم المصفوفة كل أحداث هذه القدرة؟</summary>
    /// <param name="capability">تعريف القدرة.</param>
    /// <param name="directory">فهرس أحداث المصفوفة.</param>
    public static bool IsServedBy(CapabilityDefinition capability, IPostingEventDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(directory);
        return capability.RequiredEvents.All(directory.Contains);
    }
}
