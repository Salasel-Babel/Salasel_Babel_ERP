using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Inventory.Voice;

/// <summary>
/// <b>ما تُسهم به وحدة المخزون في المسار المنطوق.</b>
/// <para>
/// <b>وهذا القسم مع المقاولات هما موضعا الصوت الأصليّان:</b> عاملُ المستودع يحمل كرتوناً
/// بيدين اثنتين، وينظر إلى ملصقٍ على رفٍّ فوق رأسه. وكلُّ ثانيةٍ يقضيها في وضع الكرتون
/// وإخراج جهازٍ وفتح شاشةٍ هي ثانيةٌ تُنتج جرداً يُكتب مساءً من الذاكرة.
/// </para>
/// <para>
/// <b>والوحدة جزءٌ من كل كمّية هنا</b> (‏ADR-0049): للصنف الواحد وحدةُ أساسٍ ووحداتٌ
/// أكبر بمعاملات صحيحة، و«عشرين» بلا وحدةٍ في مستودعٍ فيه الحبّة والكرتون فرقُها اثنا
/// عشر ضعفاً. فكمّيةٌ بلا وحدةٍ <b>تُرفض ولا تُفسَّر بوحدة الأساس</b>.
/// </para>
/// <para>
/// <b>وما تغيّر — مكتوباً لا مطموساً:</b> كان <b>التقييم ومتوسط التكلفة</b> ممنوعاً
/// بحجّة أنه «قراءةُ أعمدةٍ تُقارَن». وهي حجّةٌ ضدّ <b>نطق الجواب</b> لا ضدّ <b>نطق
/// السؤال</b>: والسؤال يُنطَق، والجواب يُفتح على الشاشة جدولاً كما هو.
/// </para>
/// <para>
/// <b>وتسجيلُ الصنف ومعاملات وحداته لم يُبلَغ</b> — لا لأنه ممنوع، بل لأنه ليس مستنداً
/// ولا مسوّدة: بسطٌ ومقامٌ يُراجَعان مرّةً واحدة في عمر الصنف، ولم يطلبه أحد. وهو
/// <b>مذكورٌ لا مسكوتٌ عنه</b>.
/// </para>
/// </summary>
public sealed class InventoryVoiceIntents : IVoiceIntentCatalogue
{
    /// <inheritdoc />
    public BabelModule Module => BabelModule.Inventory;

    /// <inheritdoc />
    public IReadOnlyList<VoiceIntent> Intents { get; } =
    [
        new VoiceIntent(
            "inventory.count_adjustment.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "inventory.count_adjustment.posted",
            "draftStockMovement",
            "تسوية جرد",
            [
                "سجل جرد", "تسوية جرد", "الجرد الفعلي", "عديت الصنف", "جرد الصنف",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف", "من"], []) { Entity = VoiceEntityKind.Item },
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية الفعلية", true,
                    ["كمية", "الكمية", "عدد", "العدد", "بمقدار", "لقيت"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
                new VoiceSlot("location", VoiceSlotKind.Code, "الموقع", false,
                    ["الموقع", "موقع"], []),
                new VoiceSlot("countedOn", VoiceSlotKind.Date, "تاريخ الجرد", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "inventory.issue_to_project.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "inventory.issue_to_project",
            "draftStockMovement",
            "صرف مواد لمشروع",
            [
                "اصرف مواد للمشروع", "صرف مواد", "سجل صرف مواد لمشروع", "طلعت مواد للمشروع",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []) { Entity = VoiceEntityKind.Item },
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المصروفة", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
                new VoiceSlot("project", VoiceSlotKind.Text, "المشروع", true,
                    ["للمشروع", "المشروع", "مشروع"], []) { Entity = VoiceEntityKind.Project },
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الصرف", true,
                    [], []),
            ],
            false,
            null),

        new VoiceIntent(
            "inventory.location_placement.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.AwaitingOwnerDecision,
            VoiceLedgerEffect.None,
            null,
            null,
            "تسكين قطعٍ بين موقعين",
            [
                "تسكين القطع", "سكن الصنف", "تسكين في الموقع", "رص الصنف", "تسكين",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []) { Entity = VoiceEntityKind.Item },
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", true,
                    ["المستودع", "مستودع", "المخزن"], []) { Entity = VoiceEntityKind.Warehouse },
                new VoiceSlot("fromLocation", VoiceSlotKind.Code, "الموقع المصدر", true,
                    ["من موقع", "من الموقع", "من رف", "من الرف"], []),
                new VoiceSlot("toLocation", VoiceSlotKind.Code, "الموقع الهدف", true,
                    ["الى موقع", "الى الموقع", "الى رف", "الى الرف", "لموقع"], []),
            ],
            false,
            "السطح المنشور يُخرج حركة المخزون بموقعٍ **واحد** لا بموقعين "
            + "(‏InventoryStockMovementRequest: مستودعٌ واحد وموقعٌ واحد واتجاهٌ واحد)، "
            + "فنقلُ كمّيةٍ بين موقعين داخل المستودع نفسه لا يُعبَّر عنه بمستندٍ واحد. "
            + "والقرار المطلوب من مالك المنتج: أيُفتح مستند «تسكين» يحمل موقعَي مصدرٍ ووجهة "
            + "في مستندٍ واحد، أم يُقبل مستندان مترابطان تحت هوية ترحيلٍ واحدة؟ "
            + "ويترتّب على الجواب هل للتسكين أثرٌ في الدفتر أصلاً حين يكون الحسابان واحداً."),

        new VoiceIntent(
            "inventory.stock_balance.query",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readStockBalances",
            "رصيد صنف",
            [
                "كم رصيد الصنف", "رصيد الصنف", "كم عندي من الصنف", "وش رصيد الصنف", "كم باقي من الصنف",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "من"], []) { Entity = VoiceEntityKind.Item },
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", false,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
            ],
            false,
            null),

        new VoiceIntent(
            "inventory.stock_movement.query",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "listStockMovements",
            "حركات صنف",
            [
                "حركات الصنف", "كشف حركة الصنف", "وش حركات الصنف", "حركة الصنف",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []) { Entity = VoiceEntityKind.Item },
            ],
            false,
            null),

        new VoiceIntent(
            "inventory.valuation.query",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "readInventoryValuation",
            "تقييم المخزون",
            [
                "تقييم المخزون", "كم قيمة المخزون", "وش قيمة المخزون", "قيمة المخزون",
            ],
            [
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
            ],
            false,
            null),

        new VoiceIntent(
            "inventory.warehouse_transfer.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "inventory.transfer.between_warehouses",
            "draftStockMovement",
            "تحويل مخزني بين مستودعين",
            [
                "تحويل بين مستودعين", "حول من مستودع", "نقل بضاعة بين المستودعات", "تحويل مخزني",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", true,
                    ["الصنف", "صنف", "للصنف"], []) { Entity = VoiceEntityKind.Item },
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المحوَّلة", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("fromWarehouse", VoiceSlotKind.Text, "المستودع المرسِل", true,
                    ["من مستودع", "من المستودع", "من مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
                new VoiceSlot("toWarehouse", VoiceSlotKind.Text, "المستودع المستقبِل", true,
                    ["الى مستودع", "الى المستودع", "لمستودع", "الى مخزن"], []) { Entity = VoiceEntityKind.Warehouse },
                new VoiceSlot("movedOn", VoiceSlotKind.Date, "تاريخ التحويل", true,
                    [], []),
            ],
            false,
            null),
    ];
}
