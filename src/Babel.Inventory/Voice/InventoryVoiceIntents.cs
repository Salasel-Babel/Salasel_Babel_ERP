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
/// <b>وما لم يُعطَ صوتاً:</b> <b>تسجيل الصنف ومعاملات وحداته</b> — بسطٌ ومقامٌ لكل وحدة،
/// ويُراجَعان بالعين مرّةً واحدة في عمر الصنف؛ و<b>التقييم ومتوسط التكلفة</b> لأنهما
/// قراءةُ أعمدةٍ تُقارَن؛ و<b>مخصّص التقادم</b> لأنه حكمٌ إداري على قائمة.
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
            "تسوية جرد",
            "Stock count adjustment",
            [
                "سجل جرد", "تسوية جرد", "الجرد الفعلي", "عديت الصنف", "جرد الصنف",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", "Item", true,
                    ["الصنف", "صنف", "للصنف", "من"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية الفعلية", "Counted quantity", true,
                    ["كمية", "الكمية", "عدد", "العدد", "بمقدار", "لقيت"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", "Warehouse", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []),
                new VoiceSlot("location", VoiceSlotKind.Code, "الموقع", "Location", false,
                    ["الموقع", "موقع"], []),
                new VoiceSlot("countedOn", VoiceSlotKind.Date, "تاريخ الجرد", "Counted on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "inventory.issue_to_project.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "inventory.issue_to_project",
            "صرف مواد لمشروع",
            "Issue materials to a project",
            [
                "اصرف مواد للمشروع", "صرف مواد", "سجل صرف مواد لمشروع", "طلعت مواد للمشروع",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", "Item", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المصروفة", "Issued quantity", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", "Warehouse", true,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []),
                new VoiceSlot("project", VoiceSlotKind.Text, "المشروع", "Project", true,
                    ["للمشروع", "المشروع", "مشروع"], []),
                new VoiceSlot("issuedOn", VoiceSlotKind.Date, "تاريخ الصرف", "Issued on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "inventory.warehouse_transfer.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.Posts,
            "inventory.transfer.between_warehouses",
            "تحويل مخزني بين مستودعين",
            "Transfer stock between warehouses",
            [
                "تحويل بين مستودعين", "حول من مستودع", "نقل بضاعة بين المستودعات",
                "تحويل مخزني",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", "Item", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية المحوَّلة", "Transferred quantity", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("fromWarehouse", VoiceSlotKind.Text, "المستودع المرسِل", "From warehouse", true,
                    ["من مستودع", "من المستودع", "من مخزن"], []),
                new VoiceSlot("toWarehouse", VoiceSlotKind.Text, "المستودع المستقبِل", "To warehouse", true,
                    ["الى مستودع", "الى المستودع", "لمستودع", "الى مخزن"], []),
                new VoiceSlot("movedOn", VoiceSlotKind.Date, "تاريخ التحويل", "Moved on", true, [], []),
            ],
            false,
            null,
            null),

        new VoiceIntent(
            "inventory.location_placement.record",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.StateChange,
            VoiceIntentStatus.AwaitingOwnerDecision,
            VoiceLedgerEffect.None,
            null,
            "تسكين قطعٍ بين موقعين",
            "Bin-to-bin placement",
            [
                "تسكين القطع", "سكن الصنف", "تسكين في الموقع", "رص الصنف", "تسكين",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", "Item", true,
                    ["الصنف", "صنف", "للصنف"], []),
                new VoiceSlot("quantity", VoiceSlotKind.Quantity, "الكمية", "Quantity", true,
                    ["كمية", "الكمية", "عدد", "بمقدار"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", "Warehouse", true,
                    ["المستودع", "مستودع", "المخزن"], []),
                new VoiceSlot("fromLocation", VoiceSlotKind.Code, "الموقع المصدر", "From location", true,
                    ["من موقع", "من الموقع", "من رف", "من الرف"], []),
                new VoiceSlot("toLocation", VoiceSlotKind.Code, "الموقع الهدف", "To location", true,
                    ["الى موقع", "الى الموقع", "الى رف", "الى الرف", "لموقع"], []),
            ],
            false,
            "السطح المنشور يُخرج حركة المخزون بموقعٍ **واحد** لا بموقعين "
            + "(‏InventoryStockMovementRequest: مستودعٌ واحد وموقعٌ واحد واتجاهٌ واحد)، "
            + "فنقلُ كمّيةٍ بين موقعين داخل المستودع نفسه لا يُعبَّر عنه بمستندٍ واحد. "
            + "والقرار المطلوب من مالك المنتج: أيُفتح مستند «تسكين» يحمل موقعَي مصدرٍ ووجهة "
            + "في مستندٍ واحد، أم يُقبل مستندان مترابطان تحت هوية ترحيلٍ واحدة؟ "
            + "ويترتّب على الجواب هل للتسكين أثرٌ في الدفتر أصلاً حين يكون الحسابان واحداً.",
            "The published surface emits a stock movement with a single location, not two, so a "
            + "bin-to-bin move inside one warehouse cannot be expressed as one document. The owner "
            + "must decide: one placement document carrying source and target locations, or two "
            + "linked documents under a single posting identity."),

        new VoiceIntent(
            "inventory.stock_balance.query",
            VoiceSection.Inventory,
            BabelModule.Inventory,
            VoiceIntentKind.Query,
            VoiceIntentStatus.Published,
            VoiceLedgerEffect.None,
            null,
            "رصيد صنف",
            "Item stock balance",
            [
                "كم رصيد الصنف", "رصيد الصنف", "كم عندي من الصنف", "وش رصيد الصنف",
                "كم باقي من الصنف",
            ],
            [
                new VoiceSlot("item", VoiceSlotKind.Text, "الصنف", "Item", true,
                    ["الصنف", "صنف", "من"], []),
                new VoiceSlot("warehouse", VoiceSlotKind.Text, "المستودع", "Warehouse", false,
                    ["المستودع", "مستودع", "المخزن", "مخزن"], []),
            ],
            false,
            null,
            null),
    ];
}
