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
  /** الاسم العربي — **هو السجلّ** لا ترجمته (ADR-0021). */
  readonly nameAr: string;
  readonly nameEn: string;
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
  /** مُشتقّة من الصنف في الخادم، ومحمولةٌ هنا كي لا تُعاد الاشتقاقة ناقصة. */
  readonly requiresConfirmation: boolean;
  readonly readsPersonalData: boolean;
  readonly nameAr: string;
  readonly nameEn: string;
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
    "id": "accounting.customer_balance.query",
    "section": "Accounting",
    "module": "Sales",
    "kind": "Query",
    "status": "Published",
    "ledgerEffect": "None",
    "eventCode": null,
    "requiresConfirmation": false,
    "readsPersonalData": false,
    "nameAr": "رصيد عميل",
    "nameEn": "Customer balance",
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
        "nameEn": "Customer",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "سند قبض من عميل",
    "nameEn": "Record a customer receipt",
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
        "nameEn": "Customer",
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
        "nameEn": "Amount received",
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
        "nameEn": "Receipt method",
        "required": true,
        "cues": [],
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
        "nameEn": "Received on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "التقاط فاتورة مصروف من مورد",
    "nameEn": "Capture a supplier expense bill",
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
        "nameEn": "Supplier",
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
        "nameEn": "Gross total",
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
        "nameEn": "Tax rate",
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
        "nameEn": "Bill number",
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
        "nameEn": "Issued on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "سند صرف لمورد",
    "nameEn": "Record a supplier payment",
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
        "nameEn": "Supplier",
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
        "nameEn": "Amount paid",
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
        "nameEn": "Payment method",
        "required": true,
        "cues": [],
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
        "nameEn": "Paid on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "قياس بندٍ في مستخلص عميل",
    "nameEn": "Measure a client certificate line",
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
        "nameEn": "Contract",
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
        "nameEn": "BoQ item",
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
        "nameEn": "Executed quantity",
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
        "nameEn": "Measured on",
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
    "requiresConfirmation": false,
    "readsPersonalData": false,
    "nameAr": "موقف العقد",
    "nameEn": "Contract position",
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
        "nameEn": "Contract",
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
    "id": "contracting.subcontractor_advance.record",
    "section": "Contracting",
    "module": "Projects",
    "kind": "StateChange",
    "status": "Published",
    "ledgerEffect": "Posts",
    "eventCode": "projects.subcontractor_advance.paid",
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "دفعة مقدمة لمقاول من الباطن",
    "nameEn": "Pay a subcontractor advance",
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
        "nameEn": "Subcontractor",
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
        "nameEn": "Amount",
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
        "nameEn": "Paid on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "قياس بندٍ في مستخلص مقاول من الباطن",
    "nameEn": "Measure a subcontractor certificate line",
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
        "nameEn": "Subcontract",
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
        "nameEn": "BoQ item",
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
        "nameEn": "Executed quantity",
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
        "nameEn": "Measured on",
        "required": true,
        "cues": [],
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
    "requiresConfirmation": false,
    "readsPersonalData": true,
    "nameAr": "بطاقة موظف — مُقنَّعة",
    "nameEn": "Employee card — masked",
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
        "nameEn": "Employee",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "سلفة موظف",
    "nameEn": "Employee advance",
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
        "nameEn": "Employee",
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
        "nameEn": "Advance amount",
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
        "nameEn": "Instalments",
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
        "nameEn": "Granted on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "خصم على موظف",
    "nameEn": "Employee deduction",
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
        "nameEn": "Employee",
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
        "nameEn": "Deduction amount",
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
        "nameEn": "Effective on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "تسوية جرد",
    "nameEn": "Stock count adjustment",
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
        "nameEn": "Item",
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
        "nameEn": "Counted quantity",
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
        "nameEn": "Warehouse",
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
        "nameEn": "Location",
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
        "nameEn": "Counted on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "صرف مواد لمشروع",
    "nameEn": "Issue materials to a project",
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
        "nameEn": "Item",
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
        "nameEn": "Issued quantity",
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
        "nameEn": "Warehouse",
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
        "nameEn": "Project",
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
        "nameEn": "Issued on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "تسكين قطعٍ بين موقعين",
    "nameEn": "Bin-to-bin placement",
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
        "nameEn": "Item",
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
        "nameEn": "Quantity",
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
        "nameEn": "Warehouse",
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
        "nameEn": "From location",
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
        "nameEn": "To location",
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
    "requiresConfirmation": false,
    "readsPersonalData": false,
    "nameAr": "رصيد صنف",
    "nameEn": "Item stock balance",
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
        "nameEn": "Item",
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
        "nameEn": "Warehouse",
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
    "id": "inventory.warehouse_transfer.record",
    "section": "Inventory",
    "module": "Inventory",
    "kind": "StateChange",
    "status": "Published",
    "ledgerEffect": "Posts",
    "eventCode": "inventory.transfer.between_warehouses",
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "تحويل مخزني بين مستودعين",
    "nameEn": "Transfer stock between warehouses",
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
        "nameEn": "Item",
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
        "nameEn": "Transferred quantity",
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
        "nameEn": "From warehouse",
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
        "nameEn": "To warehouse",
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
        "nameEn": "Moved on",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "مصروف صيانة على الشركة",
    "nameEn": "Maintenance expense borne by the company",
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
        "nameEn": "Unit",
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
        "nameEn": "Maintenance amount",
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
        "nameEn": "Spent on",
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
    "requiresConfirmation": false,
    "readsPersonalData": false,
    "nameAr": "متأخرات مستأجر",
    "nameEn": "Tenant arrears",
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
        "nameEn": "Lessee",
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
    "requiresConfirmation": true,
    "readsPersonalData": false,
    "nameAr": "تحصيل من مستأجر",
    "nameEn": "Record a tenant collection",
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
        "nameEn": "Lessee",
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
        "nameEn": "Amount collected",
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
        "nameEn": "Collection method",
        "required": true,
        "cues": [],
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
        "nameEn": "Received on",
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
    "requiresConfirmation": false,
    "readsPersonalData": false,
    "nameAr": "حالة وحدة",
    "nameEn": "Unit status",
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
        "nameEn": "Unit",
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

/** كل رمز حدثٍ ينطق به المتصفّح — يقرؤه حارسٌ في الخادم ويطابقه بالمصفوفة. */
export const SPOKEN_EVENT_CODES: readonly string[] = VOICE_INTENTS
  .map((intent) => intent.eventCode)
  .filter((code): code is string => code !== null);
