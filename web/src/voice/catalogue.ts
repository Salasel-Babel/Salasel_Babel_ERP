/* ═══════════════════════════════════════════════════════════════════════════
   سجلّ النيّات المنطوقة في المتصفّح — **مرآةٌ محروسة، لا نسخةٌ ثانية.**
   ───────────────────────────────────────────────────────────────────────────
   المصدر الحقيقي لهذا السجلّ هو **الوحدات نفسها** في الخادم: كلٌّ منها تُعلن
   نيّاتها عبر `IVoiceIntentCatalogue` في العقد، ويجمعها `VoiceIntentRegistry`.
   وما هنا صورةٌ منه يقرؤها المتصفّح **بلا شبكة وبلا خادم** — وهو شرط أن يعمل
   الصوت في مستودعٍ بلا تغطية وعلى موقع صبٍّ بلا شبكة.

   ⚠ **وانحرافُ الصورة عن أصلها يُحمِّر بوّابةً لا شاشة**: هذا الملفّ وملفّ
   المتجهات `tests/Babel.Ai.Tests/golden/voice-intents.v1.json` يُقارَنان في
   اختبارٍ على الطرفين — في الخادم (TheBrowserCatalogueMirrorsTheServer) وفي
   المتصفّح (web/tests/voice-command.test.ts). ونيّةٌ تُضاف في وحدةٍ ولا تصل
   هنا تُسقط البناء، ولا تعيش يوماً واحداً «تعمل في الخادم ولا تعمل في اليد».

   ⚠ **ولا يُحرَّر هذا الملفّ بيدٍ وحده**: يُولَّد من ملفّ المتجهات نفسه، وأي
   تحرير يخالفه يُكتشف في الحال. ورموز الأحداث فيه مطابَقةٌ بمصفوفة الترحيل في
   الخادم، فرمزٌ مخترَع هنا لا يمرّ (ADR-0016 · ADR-0030).
   ═══════════════════════════════════════════════════════════════════════════ */

/** القسم كما يراه المستخدم — نظير VoiceSection في العقد بالأسماء نفسها. */
export type VoiceSection = "Accounting" | "Contracting" | "HumanResources" | "Inventory" | "RealEstate";

/** صنف النيّة — وهو ما يُملي التأكيد. */
export type VoiceIntentKind = "Query" | "Navigation" | "StateChange";

/** حال النيّة في المنتج. */
export type VoiceIntentStatus = "Published" | "AwaitingOwnerDecision";

/** أثرها على الدفتر. */
export type VoiceLedgerEffect = "None" | "Posts";

/** صنف الشريحة. */
export type VoiceSlotKind = "Text" | "Number" | "Money" | "Quantity" | "Date" | "Code" | "Choice";

/** شريحةٌ تُستخرج من الكلام. */
export interface VoiceSlot {
  readonly name: string;
  readonly kind: VoiceSlotKind;
  /**
   * الاسم العربي — **هو السجلّ** لا ترجمته، ولا نصف إنجليزيّ بجانبه: زوج ar/en
   * ثابت عاجزٌ بنيوياً عن اللغة الثالثة (ADR-0021 §6.3 · القاعدة 14).
   */
  readonly nameAr: string;
  readonly required: boolean;
  readonly cues: readonly string[];
  readonly choices: readonly string[];
}

/** نيّةٌ منطوقة. */
export interface VoiceIntent {
  readonly id: string;
  readonly section: VoiceSection;
  readonly module: string;
  readonly kind: VoiceIntentKind;
  readonly status: VoiceIntentStatus;
  readonly ledgerEffect: VoiceLedgerEffect;
  readonly eventCode: string | null;
  /**
   * **العملية المنشورة الوحيدة التي تبلغها هذه النيّة** — بمعرّفها في العقد حرفاً بحرف.
   * فارغة للنيّة التي تنتظر قراراً، لأن عمليتها لم تُفتح بعد.
   *
   * ⚠ **ولا تكون عمليةَ ترحيلٍ ولا توقيعٍ ولا اعتماد أبداً.** الصوت يبلغ المسوّدة،
   * والشاشةُ — لا هذه الطبقة — تملك زرّ الترحيل. ويقيسه حارسٌ في الخادم يقرأ هذا
   * الملفّ نفسه ويطابقه بالعقد المنشور.
   */
  readonly operationId: string | null;
  /** مُشتقّة من الصنف في الخادم، ومحمولةٌ هنا كي لا تُعاد الاشتقاقة ناقصة. */
  readonly requiresConfirmation: boolean;
  readonly readsPersonalData: boolean;
  readonly nameAr: string;
  readonly phrases: readonly string[];
  readonly slots: readonly VoiceSlot[];
}

/** الأقسام الخمسة بترتيب عرضها، ومفاتيح أسمائها في طبقة اللغة. */
export const VOICE_SECTIONS: readonly { readonly id: VoiceSection; readonly labelKey: string }[] = [
  { id: "Accounting", labelKey: "screen.voice.section.accounting" },
  { id: "Contracting", labelKey: "screen.voice.section.contracting" },
  { id: "HumanResources", labelKey: "screen.voice.section.hr" },
  { id: "Inventory", labelKey: "screen.voice.section.inventory" },
  { id: "RealEstate", labelKey: "screen.voice.section.realestate" },
];

/** السجلّ. مغلق: ما ليس فيه لا يُنطَق ولا يُخمَّن. */
export const VOICE_INTENTS: readonly VoiceIntent[] = [
    {
      "id": "accounting.credit_note.draft",
      "section": "Accounting",
      "module": "Sales",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "sales.credit_note.posted",
      "operationId": "draftCreditNote",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة إشعار دائن",
      "phrases": [
        "سجل اشعار دائن",
        "اشعار دائن للعميل",
        "اشعار دائن",
        "مرتجع مبيعات",
        "العميل رجع البضاعة"
      ],
      "slots": [
        {
          "name": "customer",
          "kind": "Text",
          "nameAr": "العميل",
          "required": true,
          "cues": [
            "للعميل",
            "على العميل",
            "العميل",
            "عميل"
          ],
          "choices": []
        },
        {
          "name": "invoiceNumber",
          "kind": "Code",
          "nameAr": "الفاتورة الأصلية",
          "required": true,
          "cues": [
            "على الفاتورة",
            "الفاتورة",
            "فاتورة"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة الإشعار",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإصدار",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.customer.add",
      "section": "Accounting",
      "module": "Sales",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "addCustomer",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "إنشاء عميل",
      "phrases": [
        "انشئ عميل",
        "انشاء عميل",
        "اضف عميل",
        "عميل جديد",
        "افتح حساب عميل",
        "سوي لي عميل"
      ],
      "slots": [
        {
          "name": "name",
          "kind": "Text",
          "nameAr": "اسم العميل",
          "required": true,
          "cues": [
            "باسم",
            "اسمه",
            "للعميل",
            "العميل",
            "عميل",
            "من"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.customer_balance.query",
      "section": "Accounting",
      "module": "Sales",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readReceivablesAging",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "رصيد عميل",
      "phrases": [
        "كم رصيد العميل",
        "رصيد العميل",
        "كم على العميل",
        "وش رصيد العميل",
        "كم باقي على العميل"
      ],
      "slots": [
        {
          "name": "customer",
          "kind": "Text",
          "nameAr": "العميل",
          "required": true,
          "cues": [
            "العميل",
            "عميل",
            "على",
            "حق"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.customer_receipt.record",
      "section": "Accounting",
      "module": "Sales",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "sales.receipt.posted",
      "operationId": "draftCustomerReceipt",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "سند قبض من عميل",
      "phrases": [
        "سجل سند قبض",
        "سند قبض",
        "استلمت من العميل",
        "قبضت من العميل",
        "تحصيل من عميل",
        "حصلت من العميل"
      ],
      "slots": [
        {
          "name": "customer",
          "kind": "Text",
          "nameAr": "العميل",
          "required": true,
          "cues": [
            "العميل",
            "عميل",
            "من",
            "لصالح"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المقبوض",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمته",
            "قيمتها"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة القبض",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "receivedOn",
          "kind": "Date",
          "nameAr": "تاريخ القبض",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.goods_receipt.draft",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "purchasing.goods_receipt.posted",
      "operationId": "draftGoodsReceipt",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة محضر استلام بضاعة",
      "phrases": [
        "سجل استلام بضاعة",
        "محضر استلام بضاعة",
        "سجل محضر استلام",
        "وصلت البضاعة",
        "استلمت البضاعة"
      ],
      "slots": [
        {
          "name": "orderNumber",
          "kind": "Code",
          "nameAr": "أمر الشراء",
          "required": true,
          "cues": [
            "بامر شراء",
            "على امر شراء",
            "امر الشراء"
          ],
          "choices": []
        },
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المستلمة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "العدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "receivedOn",
          "kind": "Date",
          "nameAr": "تاريخ الاستلام",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.journal_entry.draft",
      "section": "Accounting",
      "module": "Ledger",
      "kind": "StateChange",
      "status": "AwaitingOwnerDecision",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": null,
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة قيد يومية يدوي",
      "phrases": [
        "سجل قيد يومية",
        "قيد يومية يدوي",
        "اكتب قيد يومية",
        "افتح قيد يومية",
        "قيد يومية"
      ],
      "slots": [
        {
          "name": "description",
          "kind": "Text",
          "nameAr": "بيان القيد",
          "required": true,
          "cues": [
            "بيان",
            "البيان",
            "وصف",
            "عن"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة القيد",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "entryOn",
          "kind": "Date",
          "nameAr": "تاريخ القيد",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.payables_aging.query",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readPayablesAging",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "تقادم الذمم الدائنة",
      "phrases": [
        "تقادم الذمم الدائنة",
        "اعمار الذمم الدائنة",
        "تقادم الموردين",
        "كم علينا للموردين"
      ],
      "slots": [
        {
          "name": "asOf",
          "kind": "Date",
          "nameAr": "تاريخ القطع",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.purchase_order.draft",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "createPurchaseOrder",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة أمر شراء",
      "phrases": [
        "افتح امر شراء",
        "سجل امر شراء",
        "اطلب من المورد",
        "اطلب بضاعة من المورد",
        "امر شراء جديد"
      ],
      "slots": [
        {
          "name": "supplier",
          "kind": "Text",
          "nameAr": "المورد",
          "required": true,
          "cues": [
            "من المورد",
            "المورد",
            "مورد"
          ],
          "choices": []
        },
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المطلوبة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "العدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": true,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن",
            "مخزن"
          ],
          "choices": []
        },
        {
          "name": "orderedOn",
          "kind": "Date",
          "nameAr": "تاريخ الأمر",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.purchase_return.draft",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "purchasing.debit_note.posted",
      "operationId": "draftPurchaseReturn",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة مرتجع مشتريات",
      "phrases": [
        "سجل مرتجع مشتريات",
        "مرتجع مشتريات",
        "اشعار مدين على المورد",
        "رجعت بضاعة للمورد"
      ],
      "slots": [
        {
          "name": "billNumber",
          "kind": "Code",
          "nameAr": "فاتورة المورد",
          "required": true,
          "cues": [
            "على الفاتورة",
            "الفاتورة",
            "فاتورة"
          ],
          "choices": []
        },
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المرتجعة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "العدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإشعار",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.receivables_aging.query",
      "section": "Accounting",
      "module": "Sales",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readReceivablesAging",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "تقادم الذمم المدينة",
      "phrases": [
        "تقادم الذمم المدينة",
        "اعمار الذمم المدينة",
        "تقادم العملاء",
        "كم على العملاء"
      ],
      "slots": [
        {
          "name": "asOf",
          "kind": "Date",
          "nameAr": "تاريخ القطع",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.sales_invoice.draft",
      "section": "Accounting",
      "module": "Sales",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "sales.invoice.posted",
      "operationId": "draftSalesInvoice",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة فاتورة مبيعات",
      "phrases": [
        "سجل فاتورة مبيعات",
        "فاتورة مبيعات",
        "افتح فاتورة مبيعات",
        "بعت على العميل",
        "اكتب فاتورة للعميل",
        "بيع للعميل"
      ],
      "slots": [
        {
          "name": "customer",
          "kind": "Text",
          "nameAr": "العميل",
          "required": true,
          "cues": [
            "على العميل",
            "للعميل",
            "العميل",
            "عميل"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "الإجمالي شامل الضريبة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "taxRate",
          "kind": "Number",
          "nameAr": "نسبة الضريبة",
          "required": false,
          "cues": [
            "ضريبة",
            "وضريبة",
            "بنسبة"
          ],
          "choices": []
        },
        {
          "name": "invoiceNumber",
          "kind": "Code",
          "nameAr": "رقم الفاتورة",
          "required": false,
          "cues": [
            "رقم",
            "برقم",
            "رقمها"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإصدار",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.stock_bill.capture",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "draftStockBill",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "التقاط فاتورة مشتريات مخزنية",
      "phrases": [
        "سجل فاتورة مشتريات مخزنية",
        "فاتورة مشتريات مخزنية",
        "فاتورة بضاعة من المورد",
        "قيد فاتورة مخزنية"
      ],
      "slots": [
        {
          "name": "receiptNumber",
          "kind": "Code",
          "nameAr": "محضر الاستلام",
          "required": true,
          "cues": [
            "على محضر استلام",
            "محضر الاستلام"
          ],
          "choices": []
        },
        {
          "name": "supplier",
          "kind": "Text",
          "nameAr": "المورد",
          "required": true,
          "cues": [
            "من المورد",
            "المورد",
            "مورد"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "الإجمالي شامل الضريبة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الفاتورة",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.supplier_bill.capture",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "purchasing.invoice.expense.posted",
      "operationId": "draftExpenseBill",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "التقاط فاتورة مصروف من مورد",
      "phrases": [
        "سجل فاتورة مصروف",
        "قيد فاتورة مصروف",
        "فاتورة مصروف",
        "ادخل فاتورة مورد",
        "اكتب فاتورة مورد",
        "عندي فاتورة مصروف"
      ],
      "slots": [
        {
          "name": "supplier",
          "kind": "Text",
          "nameAr": "المورد",
          "required": true,
          "cues": [
            "من",
            "المورد",
            "مورد",
            "باسم",
            "لصالح"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "الإجمالي شامل الضريبة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "قيمته",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "taxRate",
          "kind": "Number",
          "nameAr": "نسبة الضريبة",
          "required": false,
          "cues": [
            "ضريبة",
            "وضريبة",
            "الضريبة",
            "بنسبة"
          ],
          "choices": []
        },
        {
          "name": "billNumber",
          "kind": "Code",
          "nameAr": "رقم الفاتورة",
          "required": false,
          "cues": [
            "رقم",
            "برقم",
            "رقمها"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإصدار",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "accounting.supplier_payment.record",
      "section": "Accounting",
      "module": "Purchasing",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "purchasing.payment.posted",
      "operationId": "draftSupplierPayment",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "سند صرف لمورد",
      "phrases": [
        "سجل سند صرف",
        "سند صرف",
        "صرفت للمورد",
        "سددت للمورد",
        "دفعت للمورد",
        "اصرف للمورد"
      ],
      "slots": [
        {
          "name": "supplier",
          "kind": "Text",
          "nameAr": "المورد",
          "required": true,
          "cues": [
            "للمورد",
            "المورد",
            "مورد",
            "لصالح",
            "الى"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المدفوع",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمته",
            "قيمتها"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة الدفع",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "paidOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.change_order.draft",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "addChangeOrder",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة أمر تغيير",
      "phrases": [
        "سجل امر تغيير",
        "امر تغيير على العقد",
        "افتح امر تغيير",
        "امر تغيير"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        },
        {
          "name": "reason",
          "kind": "Text",
          "nameAr": "سبب التغيير",
          "required": true,
          "cues": [
            "بسبب",
            "السبب",
            "لان"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة التغيير",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الأمر",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.client_certificate.measure",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "projects.client_certificate.posted",
      "operationId": "draftClientCertificate",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "قياس بندٍ في مستخلص عميل",
      "phrases": [
        "سجل مستخلص عميل",
        "مستخلص عميل",
        "قياس مستخلص",
        "سجل كمية منفذة",
        "قست في المستخلص",
        "اضف الى مستخلص العميل"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "عقد",
            "العقد",
            "للعقد",
            "بعقد"
          ],
          "choices": []
        },
        {
          "name": "boqItem",
          "kind": "Text",
          "nameAr": "بند جدول الكميات",
          "required": true,
          "cues": [
            "بند",
            "البند",
            "للبند",
            "بندي"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المنفذة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "بمقدار",
            "عدد",
            "منفذ"
          ],
          "choices": []
        },
        {
          "name": "measuredOn",
          "kind": "Date",
          "nameAr": "تاريخ القياس",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.contract_position.query",
      "section": "Contracting",
      "module": "Projects",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readContractPosition",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "موقف العقد",
      "phrases": [
        "كم موقف العقد",
        "موقف العقد",
        "وضع العقد",
        "كم المنجز في العقد",
        "وش موقف العقد"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "عقد",
            "العقد",
            "للعقد"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.guarantee.draft",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "addGuarantee",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة ضمان بنكي",
      "phrases": [
        "سجل ضمان بنكي",
        "سجل خطاب ضمان",
        "خطاب ضمان",
        "ضمان بنكي"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        },
        {
          "name": "guaranteeNumber",
          "kind": "Code",
          "nameAr": "رقم الضمان",
          "required": true,
          "cues": [
            "رقم الضمان",
            "الخطاب رقم",
            "الخطاب"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة الضمان",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "expiresOn",
          "kind": "Date",
          "nameAr": "تاريخ الانتهاء",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.retention_collection.draft",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "projects.client_retention.collected",
      "operationId": "draftRetentionCollection",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة تحصيل محتجز من عميل",
      "phrases": [
        "سجل تحصيل محتجز",
        "تحصيل محتجز من العميل",
        "تحصيل المحتجز",
        "قبضت المحتجز",
        "استلمت المحتجز"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المُحصَّل",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة التحصيل",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "collectedOn",
          "kind": "Date",
          "nameAr": "تاريخ التحصيل",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.retention_register.query",
      "section": "Contracting",
      "module": "Projects",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readRetentionRegister",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "كشف المحتجزات",
      "phrases": [
        "كشف المحتجزات",
        "كم المحتجز في العقد",
        "وش المحتجز في العقد",
        "المحتجزات في العقد"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.retention_release.draft",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "projects.retention.released",
      "operationId": "draftRetentionRelease",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة إفراج عن محتجز",
      "phrases": [
        "سجل افراج عن محتجز",
        "الافراج عن المحتجز",
        "افراج عن محتجز",
        "افرج عن المحتجز",
        "اطلق المحتجز"
      ],
      "slots": [
        {
          "name": "contract",
          "kind": "Text",
          "nameAr": "العقد",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المُفرَج عنه",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "releasedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإفراج",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.subcontractor_advance.record",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "projects.subcontractor_advance.paid",
      "operationId": "draftSubcontractorAdvance",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "دفعة مقدمة لمقاول من الباطن",
      "phrases": [
        "دفعة مقدمة لمقاول",
        "سلفة مقاول من الباطن",
        "صرفت دفعة مقدمة",
        "دفعة مقدمة للمقاول"
      ],
      "slots": [
        {
          "name": "subcontractor",
          "kind": "Text",
          "nameAr": "المقاول من الباطن",
          "required": true,
          "cues": [
            "للمقاول",
            "المقاول",
            "مقاول",
            "لصالح"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمته"
          ],
          "choices": []
        },
        {
          "name": "paidOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.subcontractor_certificate.measure",
      "section": "Contracting",
      "module": "Projects",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "projects.subcontractor_certificate.posted",
      "operationId": "draftSubcontractorCertificate",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "قياس بندٍ في مستخلص مقاول من الباطن",
      "phrases": [
        "مستخلص مقاول من الباطن",
        "مستخلص من الباطن",
        "سجل مستخلص مقاول",
        "قياس مقاول الباطن"
      ],
      "slots": [
        {
          "name": "subcontract",
          "kind": "Text",
          "nameAr": "عقد الباطن",
          "required": true,
          "cues": [
            "عقد",
            "العقد",
            "للعقد"
          ],
          "choices": []
        },
        {
          "name": "boqItem",
          "kind": "Text",
          "nameAr": "بند جدول الكميات",
          "required": true,
          "cues": [
            "بند",
            "البند",
            "للبند"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المنفذة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "بمقدار",
            "عدد"
          ],
          "choices": []
        },
        {
          "name": "measuredOn",
          "kind": "Date",
          "nameAr": "تاريخ القياس",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "contracting.subcontractor_statement.query",
      "section": "Contracting",
      "module": "Projects",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readSubcontractorStatement",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "كشف حساب مقاول من الباطن",
      "phrases": [
        "كشف حساب مقاول من الباطن",
        "كشف حساب المقاول",
        "كشف المقاول",
        "وش موقف المقاول"
      ],
      "slots": [
        {
          "name": "subcontractor",
          "kind": "Text",
          "nameAr": "المقاول من الباطن",
          "required": true,
          "cues": [
            "للمقاول",
            "المقاول",
            "مقاول"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.employee.query",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readEmployee",
      "requiresConfirmation": false,
      "readsPersonalData": true,
      "nameAr": "بطاقة موظف — مُقنَّعة",
      "phrases": [
        "بيانات الموظف",
        "كرت الموظف",
        "ملف الموظف",
        "بطاقة الموظف",
        "وش بيانات الموظف"
      ],
      "slots": [
        {
          "name": "employee",
          "kind": "Text",
          "nameAr": "الموظف",
          "required": true,
          "cues": [
            "الموظف",
            "موظف",
            "عن"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.employee_advance.record",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "draftEmployeeAdvance",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "سلفة موظف",
      "phrases": [
        "سجل سلفة موظف",
        "سلفة للموظف",
        "اصرف سلفة",
        "سلفة موظف",
        "ابغى اسجل سلفة"
      ],
      "slots": [
        {
          "name": "employee",
          "kind": "Text",
          "nameAr": "الموظف",
          "required": true,
          "cues": [
            "للموظف",
            "الموظف",
            "موظف",
            "لصالح"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "مبلغ السلفة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها"
          ],
          "choices": []
        },
        {
          "name": "instalments",
          "kind": "Number",
          "nameAr": "عدد الأقساط",
          "required": false,
          "cues": [
            "اقساط",
            "قسط",
            "على"
          ],
          "choices": []
        },
        {
          "name": "grantedOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.employee_deduction.record",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "recordEmployeeDeduction",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "خصم على موظف",
      "phrases": [
        "سجل خصم على الموظف",
        "خصم على الموظف",
        "جزاء على الموظف",
        "سجل جزاء"
      ],
      "slots": [
        {
          "name": "employee",
          "kind": "Text",
          "nameAr": "الموظف",
          "required": true,
          "cues": [
            "الموظف",
            "موظف",
            "على"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "مبلغ الخصم",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمته"
          ],
          "choices": []
        },
        {
          "name": "effectiveOn",
          "kind": "Date",
          "nameAr": "تاريخ الاستحقاق",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.end_of_service_provision.draft",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "hr.end_of_service.accrual",
      "operationId": "draftEndOfServiceProvision",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة مخصص نهاية الخدمة",
      "phrases": [
        "سجل مخصص نهاية الخدمة",
        "مخصص نهاية الخدمة",
        "استحقاق نهاية الخدمة",
        "احتساب المخصص"
      ],
      "slots": [
        {
          "name": "periodCode",
          "kind": "Code",
          "nameAr": "رمز الفترة",
          "required": true,
          "cues": [
            "لفترة",
            "الفترة",
            "لشهر",
            "عن شهر"
          ],
          "choices": []
        },
        {
          "name": "accruedOn",
          "kind": "Date",
          "nameAr": "تاريخ الاستحقاق",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.end_of_service_settlement.draft",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "hr.end_of_service.settlement",
      "operationId": "draftEndOfServiceSettlement",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة تصفية نهاية خدمة",
      "phrases": [
        "سجل تصفية نهاية الخدمة",
        "تصفية نهاية الخدمة",
        "مستحقات نهاية الخدمة",
        "صرف نهاية الخدمة"
      ],
      "slots": [
        {
          "name": "employee",
          "kind": "Text",
          "nameAr": "الموظف",
          "required": true,
          "cues": [
            "للموظف",
            "الموظف",
            "موظف"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المستحق",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة الصرف",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "settledOn",
          "kind": "Date",
          "nameAr": "تاريخ التصفية",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.payroll_payment.draft",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "hr.payroll.payment",
      "operationId": "draftPayrollPayment",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة صرف رواتب",
      "phrases": [
        "سجل صرف الرواتب",
        "اصرف الرواتب",
        "صرف الرواتب",
        "دفع الرواتب"
      ],
      "slots": [
        {
          "name": "runNumber",
          "kind": "Code",
          "nameAr": "المسير",
          "required": true,
          "cues": [
            "لمسير",
            "المسير رقم",
            "المسير"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة الصرف",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "paidOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.payroll_run.draft",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "hr.payroll.accrual",
      "operationId": "draftPayrollRun",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة مسير رواتب",
      "phrases": [
        "جهز مسير الرواتب",
        "افتح مسير رواتب",
        "سوي مسير الرواتب",
        "مسير الرواتب",
        "مسير رواتب"
      ],
      "slots": [
        {
          "name": "periodCode",
          "kind": "Code",
          "nameAr": "رمز الفترة",
          "required": true,
          "cues": [
            "لفترة",
            "الفترة",
            "لشهر",
            "عن شهر"
          ],
          "choices": []
        },
        {
          "name": "preparedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإعداد",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "hr.social_insurance_payment.draft",
      "section": "HumanResources",
      "module": "Hr",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "hr.social_insurance.payment",
      "operationId": "draftSocialInsurancePayment",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة سداد تأمينات اجتماعية",
      "phrases": [
        "سجل سداد التامينات",
        "سداد التامينات الاجتماعية",
        "سداد التامينات",
        "دفعت التامينات"
      ],
      "slots": [
        {
          "name": "periodCode",
          "kind": "Code",
          "nameAr": "رمز الفترة",
          "required": true,
          "cues": [
            "لفترة",
            "الفترة",
            "لشهر",
            "عن شهر"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المسدَّد",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة السداد",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "paidOn",
          "kind": "Date",
          "nameAr": "تاريخ السداد",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.count_adjustment.record",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "inventory.count_adjustment.posted",
      "operationId": "draftStockMovement",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "تسوية جرد",
      "phrases": [
        "سجل جرد",
        "تسوية جرد",
        "الجرد الفعلي",
        "عديت الصنف",
        "جرد الصنف"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف",
            "من"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية الفعلية",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "العدد",
            "بمقدار",
            "لقيت"
          ],
          "choices": []
        },
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": true,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن",
            "مخزن"
          ],
          "choices": []
        },
        {
          "name": "location",
          "kind": "Code",
          "nameAr": "الموقع",
          "required": false,
          "cues": [
            "الموقع",
            "موقع"
          ],
          "choices": []
        },
        {
          "name": "countedOn",
          "kind": "Date",
          "nameAr": "تاريخ الجرد",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.issue_to_project.record",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "inventory.issue_to_project",
      "operationId": "draftStockMovement",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "صرف مواد لمشروع",
      "phrases": [
        "اصرف مواد للمشروع",
        "صرف مواد",
        "سجل صرف مواد لمشروع",
        "طلعت مواد للمشروع"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المصروفة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": true,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن",
            "مخزن"
          ],
          "choices": []
        },
        {
          "name": "project",
          "kind": "Text",
          "nameAr": "المشروع",
          "required": true,
          "cues": [
            "للمشروع",
            "المشروع",
            "مشروع"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.location_placement.record",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "StateChange",
      "status": "AwaitingOwnerDecision",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": null,
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "تسكين قطعٍ بين موقعين",
      "phrases": [
        "تسكين القطع",
        "سكن الصنف",
        "تسكين في الموقع",
        "رص الصنف",
        "تسكين"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": true,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن"
          ],
          "choices": []
        },
        {
          "name": "fromLocation",
          "kind": "Code",
          "nameAr": "الموقع المصدر",
          "required": true,
          "cues": [
            "من موقع",
            "من الموقع",
            "من رف",
            "من الرف"
          ],
          "choices": []
        },
        {
          "name": "toLocation",
          "kind": "Code",
          "nameAr": "الموقع الهدف",
          "required": true,
          "cues": [
            "الى موقع",
            "الى الموقع",
            "الى رف",
            "الى الرف",
            "لموقع"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.stock_balance.query",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readStockBalances",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "رصيد صنف",
      "phrases": [
        "كم رصيد الصنف",
        "رصيد الصنف",
        "كم عندي من الصنف",
        "وش رصيد الصنف",
        "كم باقي من الصنف"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "من"
          ],
          "choices": []
        },
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": false,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن",
            "مخزن"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.stock_movement.query",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "listStockMovements",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "حركات صنف",
      "phrases": [
        "حركات الصنف",
        "كشف حركة الصنف",
        "وش حركات الصنف",
        "حركة الصنف"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.valuation.query",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readInventoryValuation",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "تقييم المخزون",
      "phrases": [
        "تقييم المخزون",
        "كم قيمة المخزون",
        "وش قيمة المخزون",
        "قيمة المخزون"
      ],
      "slots": [
        {
          "name": "warehouse",
          "kind": "Text",
          "nameAr": "المستودع",
          "required": true,
          "cues": [
            "المستودع",
            "مستودع",
            "المخزن",
            "مخزن"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "inventory.warehouse_transfer.record",
      "section": "Inventory",
      "module": "Inventory",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "inventory.transfer.between_warehouses",
      "operationId": "draftStockMovement",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "تحويل مخزني بين مستودعين",
      "phrases": [
        "تحويل بين مستودعين",
        "حول من مستودع",
        "نقل بضاعة بين المستودعات",
        "تحويل مخزني"
      ],
      "slots": [
        {
          "name": "item",
          "kind": "Text",
          "nameAr": "الصنف",
          "required": true,
          "cues": [
            "الصنف",
            "صنف",
            "للصنف"
          ],
          "choices": []
        },
        {
          "name": "quantity",
          "kind": "Quantity",
          "nameAr": "الكمية المحوَّلة",
          "required": true,
          "cues": [
            "كمية",
            "الكمية",
            "عدد",
            "بمقدار"
          ],
          "choices": []
        },
        {
          "name": "fromWarehouse",
          "kind": "Text",
          "nameAr": "المستودع المرسِل",
          "required": true,
          "cues": [
            "من مستودع",
            "من المستودع",
            "من مخزن"
          ],
          "choices": []
        },
        {
          "name": "toWarehouse",
          "kind": "Text",
          "nameAr": "المستودع المستقبِل",
          "required": true,
          "cues": [
            "الى مستودع",
            "الى المستودع",
            "لمستودع",
            "الى مخزن"
          ],
          "choices": []
        },
        {
          "name": "movedOn",
          "kind": "Date",
          "nameAr": "تاريخ التحويل",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.lease_contract.draft",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "draftLeaseContract",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة عقد إيجار",
      "phrases": [
        "سجل عقد ايجار",
        "عقد ايجار جديد",
        "افتح عقد ايجار",
        "مسودة عقد ايجار"
      ],
      "slots": [
        {
          "name": "lessee",
          "kind": "Text",
          "nameAr": "المستأجر",
          "required": true,
          "cues": [
            "للمستاجر",
            "المستاجر",
            "مستاجر"
          ],
          "choices": []
        },
        {
          "name": "unit",
          "kind": "Code",
          "nameAr": "الوحدة",
          "required": true,
          "cues": [
            "للوحدة",
            "الوحدة",
            "وحدة"
          ],
          "choices": []
        },
        {
          "name": "totalRent",
          "kind": "Money",
          "nameAr": "إجمالي الإيجار",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "startsOn",
          "kind": "Date",
          "nameAr": "تاريخ البداية",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.maintenance_expense.record",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "realestate.maintenance.company_expense",
      "operationId": "draftExpenseBill",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مصروف صيانة على الشركة",
      "phrases": [
        "سجل مصروف صيانة",
        "صيانة على الشركة",
        "فاتورة صيانة",
        "مصروف صيانة"
      ],
      "slots": [
        {
          "name": "unit",
          "kind": "Code",
          "nameAr": "الوحدة",
          "required": true,
          "cues": [
            "للوحدة",
            "الوحدة",
            "وحدة"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة الصيانة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها"
          ],
          "choices": []
        },
        {
          "name": "spentOn",
          "kind": "Date",
          "nameAr": "تاريخ الصرف",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.rent_invoice.draft",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "realestate.rent_invoice.own_property",
      "operationId": "draftRentInvoice",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "مسودة فاتورة إيجار",
      "phrases": [
        "سجل فاتورة ايجار",
        "اصدر فاتورة ايجار",
        "افتح فاتورة ايجار",
        "فاتورة ايجار"
      ],
      "slots": [
        {
          "name": "lease",
          "kind": "Code",
          "nameAr": "عقد الإيجار",
          "required": true,
          "cues": [
            "للعقد",
            "العقد",
            "عقد"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "قيمة الفاتورة",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمتها",
            "الاجمالي",
            "اجمالي",
            "المجموع"
          ],
          "choices": []
        },
        {
          "name": "taxRate",
          "kind": "Number",
          "nameAr": "نسبة الضريبة",
          "required": false,
          "cues": [
            "ضريبة",
            "وضريبة",
            "بنسبة"
          ],
          "choices": []
        },
        {
          "name": "issuedOn",
          "kind": "Date",
          "nameAr": "تاريخ الإصدار",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.tenant_arrears.query",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readTenantArrearsAging",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "متأخرات مستأجر",
      "phrases": [
        "كم متاخرات المستاجر",
        "متاخرات المستاجر",
        "كم على المستاجر",
        "وش متاخرات المستاجر"
      ],
      "slots": [
        {
          "name": "lessee",
          "kind": "Text",
          "nameAr": "المستأجر",
          "required": true,
          "cues": [
            "المستاجر",
            "مستاجر",
            "على"
          ],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.tenant_receipt.record",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "StateChange",
      "status": "Published",
      "ledgerEffect": "Posts",
      "eventCode": "realestate.collection.received",
      "operationId": "draftTenantReceipt",
      "requiresConfirmation": true,
      "readsPersonalData": false,
      "nameAr": "تحصيل من مستأجر",
      "phrases": [
        "سجل تحصيل من مستاجر",
        "قبضت من المستاجر",
        "تحصيل ايجار",
        "استلمت ايجار",
        "حصلت من المستاجر"
      ],
      "slots": [
        {
          "name": "lessee",
          "kind": "Text",
          "nameAr": "المستأجر",
          "required": true,
          "cues": [
            "من المستاجر",
            "المستاجر",
            "مستاجر",
            "من"
          ],
          "choices": []
        },
        {
          "name": "amount",
          "kind": "Money",
          "nameAr": "المبلغ المحصَّل",
          "required": true,
          "cues": [
            "بمبلغ",
            "مبلغ",
            "بقيمة",
            "قيمته"
          ],
          "choices": []
        },
        {
          "name": "method",
          "kind": "Choice",
          "nameAr": "طريقة التحصيل",
          "required": true,
          "cues": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ],
          "choices": [
            "نقد",
            "تحويل",
            "شيك",
            "شبكة"
          ]
        },
        {
          "name": "receivedOn",
          "kind": "Date",
          "nameAr": "تاريخ التحصيل",
          "required": true,
          "cues": [],
          "choices": []
        }
      ]
    },
    {
      "id": "realestate.unit_status.query",
      "section": "RealEstate",
      "module": "RealEstate",
      "kind": "Query",
      "status": "Published",
      "ledgerEffect": "None",
      "eventCode": null,
      "operationId": "readUnit",
      "requiresConfirmation": false,
      "readsPersonalData": false,
      "nameAr": "حالة وحدة",
      "phrases": [
        "حالة الوحدة",
        "وش وضع الوحدة",
        "الوحدة مؤجرة",
        "وضع الوحدة"
      ],
      "slots": [
        {
          "name": "unit",
          "kind": "Code",
          "nameAr": "الوحدة",
          "required": true,
          "cues": [
            "للوحدة",
            "الوحدة",
            "وحدة"
          ],
          "choices": []
        }
      ]
    }
  ];
/** نيّات قسمٍ بعينه. */
export function intentsOf(section: VoiceSection): readonly VoiceIntent[] {
  return VOICE_INTENTS.filter((intent) => intent.section === section);
}

/** نيّةٌ بمعرّفها، أو لا شيء. */
export function intentById(id: string): VoiceIntent | null {
  return VOICE_INTENTS.find((intent) => intent.id === id) ?? null;
}

/* ── الخطط ─────────────────────────────────────────────────────────────────
   **خطّةٌ منطوقة: عدّةُ خطواتٍ من جملةٍ واحدة، وهي بيانات لا شيفرة.** مرآةٌ محروسة
   للخطط التي تُعلنها الوحدات في الخادم، بالحارس نفسه الذي يحرس النيّات وللسبب نفسه.

   ⚠ **والخطوة تسمّي نيّةً — ولا تسمّي عمليةً بحال.** فلا توجد في هذه البيانات خانةٌ
   يُكتب فيها اسم بابٍ منشور أصلاً؛ والعمليةُ تُقرأ من النيّة المُحلّاة، وكلُّ نيّةٍ في
   السجلّ قد اجتازت حارسَ العمليات عند البناء. ولا شيء يُهرَّب لأن الخطّة **لا تملك
   أن تسمّي باباً**. */

/** شرطُ تنفيذ خطوة. */
export type VoicePlanCondition = "Always" | "WhenHumanFindsNothing";

/**
 * مصدرُ قيمة شريحة في خطوة. **ولا «من خطوةٍ سابقة»**: لا خطوةَ هنا تحسب شيئاً —
 * كلُّها تنتهي عند شاشة — والمعرّفُ ممنوعٌ أن يحمله الصوت. فما يربط خطوتين هو
 * **الجملة نفسها**، تُقرأ في كلٍّ منهما من الفم نفسه.
 */
export type VoiceSlotSource = "FromUtterance" | "AskedOfHuman";

/** كيف تمتلئ شريحةٌ في خطوة. */
export interface VoiceSlotBinding {
  readonly slotName: string;
  readonly source: VoiceSlotSource;
}

/** خطوةٌ واحدة في خطّة. */
export interface VoicePlanStep {
  readonly stepId: string;
  /** **معرّفُ نيّةٍ في السجلّ — لا معرّفُ عملية.** */
  readonly intentId: string;
  readonly condition: VoicePlanCondition;
  /** ما تفعله هذه الخطوة بالعربية كما يُقرأ في التوجيه — وهو السجلّ لا ترجمته. */
  readonly purposeAr: string;
  readonly bindings: readonly VoiceSlotBinding[];
  /**
   * **حقولٌ تطلبها شاشةُ هذه الخطوة ولا يطلبها الصوت** — بأسمائها العربية، وتُقال
   * جهراً في التوجيه قبل أن تبدأ الخطّة.
   */
  readonly screenAsksForAr: readonly string[];
}

/** خطّةٌ منطوقة. */
export interface VoicePlan {
  readonly id: string;
  readonly section: VoiceSection;
  readonly module: string;
  readonly nameAr: string;
  /** **ما يريده الإنسان** — بدائل، يكفي أن تظهر إحداها. */
  readonly triggerPhrases: readonly string[];
  /**
   * **الشرطُ الذي يجعل الجملة خطّةً لا أمراً واحداً** — بدائل كذلك.
   * والحقلان اثنان لأن الطلب والشرط **لا يتجاوران** في كلام الناس: «سند قبض **من شركة
   * المسار الأمثل** فإن لم تجدها…». فتُطابق الخطّةُ باجتماعهما لا بعبارةٍ واحدة.
   */
  readonly conditionPhrases: readonly string[];
  readonly steps: readonly VoicePlanStep[];
}

/** سجلّ الخطط. مغلق: ما ليس فيه لا يُنطَق ولا يُخمَّن. */
export const VOICE_PLANS: readonly VoicePlan[] = [
    {
      "id": "accounting.customer_receipt.with_new_customer",
      "section": "Accounting",
      "module": "Sales",
      "nameAr": "سند قبض من عميل — مع إنشائه إن لم يوجد",
      "triggerPhrases": [
        "سند قبض",
        "سجل سند قبض",
        "قبضت من العميل",
        "استلمت من العميل",
        "تحصيل من عميل"
      ],
      "conditionPhrases": [
        "فان لم تجدها",
        "فان لم تجده",
        "ان لم تجدها",
        "ان لم تجده",
        "وان لم يكن العميل موجودا",
        "والا انشئ",
        "فان لم يكن موجودا"
      ],
      "steps": [
        {
          "stepId": "create-customer",
          "intentId": "accounting.customer.add",
          "condition": "WhenHumanFindsNothing",
          "purposeAr": "إن لم تجد العميل، تُفتح شاشة العملاء باسمه مملوءاً.",
          "bindings": [
            {
              "slotName": "name",
              "source": "FromUtterance"
            }
          ],
          "screenAsksForAr": [
            "رمز العميل",
            "حدّ الائتمان",
            "مهلة السداد"
          ]
        },
        {
          "stepId": "draft-receipt",
          "intentId": "accounting.customer_receipt.record",
          "condition": "Always",
          "purposeAr": "ثم مسوّدة سند القبض — تُراجَع على الشاشة، ويُرحّلها إنسانٌ بيده.",
          "bindings": [
            {
              "slotName": "customer",
              "source": "FromUtterance"
            },
            {
              "slotName": "amount",
              "source": "FromUtterance"
            },
            {
              "slotName": "receivedOn",
              "source": "FromUtterance"
            },
            {
              "slotName": "method",
              "source": "AskedOfHuman"
            }
          ],
          "screenAsksForAr": []
        }
      ]
    }
  ];
/** خطط قسمٍ بعينه. */
export function plansOf(section: VoiceSection): readonly VoicePlan[] {
  return VOICE_PLANS.filter((plan) => plan.section === section);
}

/** خطّةٌ بمعرّفها، أو لا شيء. */
export function planById(id: string): VoicePlan | null {
  return VOICE_PLANS.find((plan) => plan.id === id) ?? null;
}

/** كل رمز حدثٍ ينطق به المتصفّح — يقرؤه حارسٌ في الخادم ويطابقه بالمصفوفة. */
export const SPOKEN_EVENT_CODES: readonly string[] = VOICE_INTENTS
  .map((intent) => intent.eventCode)
  .filter((code): code is string => code !== null);
