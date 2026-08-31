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

/** كل رمز حدثٍ ينطق به المتصفّح — يقرؤه حارسٌ في الخادم ويطابقه بالمصفوفة. */
export const SPOKEN_EVENT_CODES: readonly string[] = VOICE_INTENTS
  .map((intent) => intent.eventCode)
  .filter((code): code is string => code !== null);
