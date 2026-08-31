/* ═══════════════════════════════════════════════════════════════════════════
   أجوبة طبقة العرض — واحدةٌ لكل عملية، بمعرّفها في العقد
   ───────────────────────────────────────────────────────────────────────────
   المفتاح `operationId` كما ينشره العقد، لا مسارٌ مكتوب بيد. وكل جسمٍ يُبنى
   بـ`shaped()` فوق مخطّطه: حقلٌ لا ينشره العقد يُرفَع خطأً هنا، وحقلٌ إلزامي
   منسيّ يُملأ بصفرٍ مُعلَن لا بغيابٍ يُسقط فاكّ الترميز.

   وما ليس هنا يجيب بمخطّطه فارغاً (قراءةً) أو برفض `showcase.no_server`
   (كتابةً) — ولا يُلبَس نجاحاً كاذباً.
   ═══════════════════════════════════════════════════════════════════════════ */

import { shaped } from "./synth";
import * as S from "./seed";
import {
  CONTRACT_POLICY_PENDING,
  OWNER_SHARE_SPLIT_NOT_DECIDED,
  PAYROLL_SETTINGS_MISSING,
  UNBALANCED,
  UNIT_CONVERSION_NOT_EXACT,
  type Refusal,
} from "./refusals";

/** ما تعرفه الدالّة عن الطلب. */
export interface Ask {
  /** وسائط المسار بأسمائها في العقد. */ readonly params: Readonly<Record<string, string>>;
  /** معاملات الاستعلام. */ readonly query: URLSearchParams;
  /** الجسم المُرسَل، إن كان. */ readonly body: unknown;
}

/** جوابٌ من طبقة العرض: جسمٌ ناجح، أو رفضٌ بنصّه ورمزه. */
export type Answer = { readonly ok: true; readonly body: unknown } | { readonly ok: false; readonly refuse: Refusal };

const ok = (body: unknown): Answer => ({ ok: true, body });
const no = (refuse: Refusal): Answer => ({ ok: false, refuse });

const EN = (value: string) => [{ name: "en", value }];

/* ═══════════════════════════════════════════ الجذع — الجلسة والتأسيس ═══ */

const setup = () =>
  shaped("CompanySetup", {
    nameAr: S.COMPANY_NAME_AR,
    nameTranslations: EN(S.COMPANY_NAME_EN),
    decimalPlaces: 4,
    defaultCostCenter: S.COST_CENTER,
    costCenters: [
      shaped("CostCenter", {
        code: S.COST_CENTER,
        nameAr: S.COMPANY_NAME_AR,
        nameTranslations: EN(S.COMPANY_NAME_EN),
        isDefault: true,
        state: "Active",
        suspensionReason: "",
      }),
    ],
  });

/* ═══════════════════════════════════════════════ القسم المحاسبي ═══════ */

const trialBalance = (ask: Ask) =>
  shaped("TrialBalance", {
    book: ask.query.get("book") ?? S.BOOK,
    periodCode: ask.query.get("period") || null,
    rowCount: S.TRIAL_BALANCE_ROWS.length,
    rows: S.TRIAL_BALANCE_ROWS.map((row) => shaped("TrialBalanceRow", row)),
    totalDebit: S.TOTAL_DEBIT,
    totalCredit: S.TOTAL_CREDIT,
    balanced: S.TOTAL_DEBIT === S.TOTAL_CREDIT,
  });

/* ═══════════════════════════════════════════════════════ العقارات ═════ */

const OWNER = shaped("RealEstateParty", {
  id: S.IDS.owner,
  code: "OWN-001",
  nameAr: "ورثة عبدالله بن ناصر السالم",
  nameTranslations: EN("Heirs of Abdullah bin Nasser Al-Salem"),
  role: "owner",
  taxResidency: "resident",
  vatNumber: "310000000000003",
});

const LESSEE = shaped("RealEstateParty", {
  id: S.IDS.lessee,
  code: "LSE-014",
  nameAr: S.CUSTOMERS[1].ar,
  nameTranslations: EN(S.CUSTOMERS[1].en),
  role: "lessee",
  taxResidency: "resident",
  vatNumber: "310000000000012",
});

/* عقارٌ **مُدارٌ لغير مالكه وبأكثر من مالك** — وهو المدخل إلى رفض
   `realestate.owner_share_split_not_decided` حين تُفوتَر أقساطه. */
const PROPERTY = shaped("Property", {
  id: S.IDS.property,
  code: "PRP-004",
  nameAr: "برج النخيل التجاري — طريق الملك عبدالعزيز",
  nameTranslations: EN("Al-Nakheel Commercial Tower — King Abdulaziz Road"),
  ownerId: S.IDS.owner,
  ownershipModel: "managed_for_others",
  ownerShareNumerator: "3",
  ownerShareDenominator: "8",
});

const UNIT = shaped("Unit", {
  id: S.IDS.unit,
  code: "U-1204",
  nameAr: "مكتب ١٢٠٤ — الدور الثاني عشر",
  nameTranslations: EN("Office 1204 — twelfth floor"),
  propertyId: S.IDS.property,
  usage: "commercial",
  vatTreatment: "standard",
});

const LEASE = shaped("Lease", {
  id: S.IDS.lease,
  contractNo: "LSE-2026-0041",
  propertyId: S.IDS.property,
  unitId: S.IDS.unit,
  lesseeId: S.IDS.lessee,
  startsOn: "2026-01-01",
  endsOn: "2026-12-31",
  totalRent: "480000.0000",
  state: "ACTIVE",
});

/* أربعة أقساط ربع سنوية تجمع قيمة العقد بالضبط — والتفعيل يفحص ذلك. */
const SCHEDULE_LINES = [
  { seq: 1, from: "2026-01-01", to: "2026-03-31", due: "2026-01-05", amount: "120000.0000", invoiced: true },
  { seq: 2, from: "2026-04-01", to: "2026-06-30", due: "2026-04-05", amount: "120000.0000", invoiced: true },
  { seq: 3, from: "2026-07-01", to: "2026-09-30", due: "2026-07-05", amount: "120000.0000", invoiced: false },
  { seq: 4, from: "2026-10-01", to: "2026-12-31", due: "2026-10-05", amount: "120000.0000", invoiced: false },
].map((line, index) =>
  shaped("LeaseScheduleLine", {
    id: "aaaaaaaa-0000-4000-8000-00000000000" + String(index + 1),
    seq: line.seq,
    periodFrom: line.from,
    periodTo: line.to,
    dueOn: line.due,
    amount: line.amount,
    isInvoiced: line.invoiced,
  })
);

const arrearsParty = (
  partyId: string,
  code: string,
  ar: string,
  en: string,
  bands: Readonly<Record<string, string>>
) =>
  shaped("ArrearsParty", {
    partyId,
    code,
    nameAr: ar,
    nameTranslations: EN(en),
    bands: shaped("ArrearsBands", bands),
  });

const ARREARS_ROWS = [
  arrearsParty(S.IDS.lessee, "LSE-014", S.CUSTOMERS[1].ar, S.CUSTOMERS[1].en, {
    notDue: "138000.0000",
    days1To30: "0.0000",
    days31To60: "0.0000",
    days61To90: "0.0000",
    over90: "0.0000",
    total: "138000.0000",
  }),
  arrearsParty("9b9b9b9b-9b9b-4b9b-8b9b-9b9b9b9b9b02", "LSE-021", S.CUSTOMERS[3].ar, S.CUSTOMERS[3].en, {
    notDue: "0.0000",
    days1To30: "34500.0000",
    days31To60: "34500.0000",
    days61To90: "0.0000",
    over90: "0.0000",
    total: "69000.0000",
  }),
  arrearsParty("9b9b9b9b-9b9b-4b9b-8b9b-9b9b9b9b9b03", "LSE-033", S.CUSTOMERS[4].ar, S.CUSTOMERS[4].en, {
    notDue: "0.0000",
    days1To30: "0.0000",
    days31To60: "0.0000",
    days61To90: "18750.0000",
    over90: "42300.0000",
    total: "61050.0000",
  }),
];

const arrearsTotals = () => {
  const band = (name: string) =>
    S.sumDecimal(ARREARS_ROWS.map((p) => (p.bands as Record<string, string>)[name] ?? "0.0000"));
  return shaped("ArrearsBands", {
    notDue: band("notDue"),
    days1To30: band("days1To30"),
    days31To60: band("days31To60"),
    days61To90: band("days61To90"),
    over90: band("over90"),
    total: band("total"),
  });
};

/* ═══════════════════════════════════════════════════════ المقاولات ════ */

/** البنود المعلَّقة — منقولةٌ عن `data/posting-matrix` كما تنشرها الشاشة. */
const PENDING = [
  {
    code: "projects.retention.base",
    titleAr: "وعاء نسبة المحتجز",
    titleEn: "The retention rate's base",
    sourceRef: "posting-matrix/events/projects.json §retention",
  },
  {
    code: "projects.advance.recovery",
    titleAr: "قاعدة استرداد الدفعة المقدمة",
    titleEn: "The advance recovery rule",
    sourceRef: "posting-matrix/events/projects.json §advance",
  },
  {
    code: "projects.tax.classification_level",
    titleAr: "مستوى التصنيف الضريبي",
    titleEn: "The tax classification level",
    sourceRef: "posting-matrix/events/tax.json §classification",
  },
  {
    code: "projects.rounding.site",
    titleAr: "موضع التقريب",
    titleEn: "The rounding site",
    sourceRef: "accounts.csv vs subledger-types.csv",
  },
].map((item) => shaped("PendingPolicyItem", item));

const CONTRACT = shaped("ProjectContract", {
  id: S.IDS.contract,
  number: "PC-2026-0007",
  projectId: S.IDS.project,
  projectCode: "PRJ-004",
  customerPartyId: S.CUSTOMERS[0].code,
  currencyCode: "SAR",
  signedOn: "2026-02-11",
  guaranteeMonths: 12,
  retentionRate: "0.10",
  pendingPolicy: PENDING,
});

const BOQ = [
  { no: 1, code: "CIV-010", ar: "خرسانة القواعد", qty: "1400.000000", unit: "M3", rate: "285.0000" },
  { no: 2, code: "CIV-021", ar: "أعمال حفر وردم", qty: "8600.000000", unit: "M3", rate: "38.5000" },
  { no: 3, code: "ELE-004", ar: "توريد وتركيب أعمال كهربائية", qty: "1.000000", unit: "LS", rate: S.CATALOGUE[0].price },
  { no: 4, code: "FIN-002", ar: "أعمال تشطيبات داخلية", qty: "2600.000000", unit: "M2", rate: "218.0000" },
].map((line, index) =>
  shaped("BoqItem", {
    id: "bbbbbbbb-0000-4000-8000-00000000000" + String(index + 1),
    lineNo: line.no,
    code: line.code,
    descriptionAr: line.ar,
    contractQuantity: shaped("Measure", { magnitude: line.qty, unit: line.unit }),
    unitRate: line.rate,
    changeOrderId: null,
  })
);

const CERTIFICATE = shaped("Certificate", {
  id: S.IDS.certificate,
  number: "IPC-0002",
  ownerId: S.IDS.contract,
  sequenceNo: 2,
  periodFrom: "2026-05-01",
  periodTo: "2026-05-31",
  state: "DRAFT",
  retentionRate: "0.10",
  entryId: null,
  alreadyPosted: false,
  pendingPolicy: PENDING,
  lines: [
    shaped("CertificateLine", {
      id: "cccccccc-0000-4000-8000-000000000001",
      lineNo: 1,
      lineKind: "WORK",
      itemId: "bbbbbbbb-0000-4000-8000-000000000001",
      itemCode: "CIV-010",
      descriptionAr: "خرسانة القواعد",
      cumulativeQuantity: shaped("Measure", { magnitude: "820.000000", unit: "M3" }),
      previousQuantity: shaped("Measure", { magnitude: "540.000000", unit: "M3" }),
      amount: "233700.0000",
    }),
    shaped("CertificateLine", {
      id: "cccccccc-0000-4000-8000-000000000002",
      lineNo: 2,
      lineKind: "PENALTY",
      itemId: null,
      itemCode: "PEN-001",
      descriptionAr: "غرامة تأخير — أسبوعان",
      cumulativeQuantity: shaped("Measure", { magnitude: "1.000000", unit: "LS" }),
      previousQuantity: shaped("Measure", { magnitude: "0.000000", unit: "LS" }),
      amount: "-18500.0000",
    }),
  ],
});

const SUBCONTRACTOR = shaped("Subcontractor", {
  id: S.IDS.subcontractor,
  code: "SUB-003",
  nameAr: S.SUPPLIERS[0].ar,
  nameTranslations: EN(S.SUPPLIERS[0].en),
  vatNumber: "310000000000021",
  isActive: true,
});

const SUBCONTRACT = shaped("Subcontract", {
  id: S.IDS.subcontract,
  number: "SC-2026-0003",
  projectId: S.IDS.project,
  projectCode: "PRJ-004",
  subcontractorId: S.IDS.subcontractor,
  currencyCode: "SAR",
  signedOn: "2026-03-02",
  guaranteeMonths: 6,
  retentionRate: "0.05",
  pendingPolicy: PENDING,
});

const RETENTION_ROWS = [
  {
    side: "receivable",
    party: S.CUSTOMERS[0].code,
    kind: "customer",
    amount: "148000.0000",
    outstanding: "148000.0000",
    due: "2027-02-11",
  },
  {
    side: "payable",
    party: "SUB-003",
    kind: "subcontractor",
    amount: "62400.0000",
    outstanding: "62400.0000",
    due: "2026-09-02",
  },
].map((row, index) =>
  shaped("RetentionRegisterRow", {
    movementId: "dddddddd-0000-4000-8000-00000000000" + String(index + 1),
    documentId: index === 0 ? S.IDS.certificate : S.IDS.subcontract,
    documentType: index === 0 ? "ClientCertificate" : "SubcontractorCertificate",
    projectCode: "PRJ-004",
    partyId: row.party,
    partyKind: row.kind,
    side: row.side,
    amount: row.amount,
    outstanding: row.outstanding,
    movedOn: "2026-05-31",
    dueOn: row.due,
  })
);

/* ═══════════════════════════════════════════════════════ المخزون ══════ */

/* الأصناف والأرصدة على شكل ما تُثبته `web/tests/inventory.test.tsx` — وهي
   عيّناتٌ فُحصت ضدّ العقد في مجموعة اختبارات القسم نفسها. */
const ITEMS = [
  shaped("Item", {
    id: S.IDS.itemWater,
    code: "ITM-001",
    name: { ar: "ماء معدني ٦٠٠ مل", en: "Mineral water 600ml" },
    itemGroup: "beverages",
    baseUnit: "PCS",
    units: [
      shaped("UnitFactor", { unitCode: "CTN", numerator: 12, denominator: 1 }),
      shaped("UnitFactor", { unitCode: "PLT", numerator: 1440, denominator: 1 }),
    ],
  }),
  shaped("Item", {
    id: S.IDS.itemCement,
    code: "ITM-002",
    name: { ar: "أسمنت سائب", en: "Bulk cement" },
    itemGroup: "materials",
    baseUnit: "KG",
    units: [],
  }),
];

const BALANCES = [
  {
    itemId: "ITM-001",
    warehouseId: "WH-RIYADH",
    locationId: "A-01-3",
    quantity: shaped("Measure", { magnitude: "1440.000000", unit: "PCS" }),
    unitCost: "0.100000",
    value: "144.0000",
    hasCostBasis: true,
  },
  /* رصيدٌ سالب — واقعةٌ يومية يُعلنها العقد، وتُوسَم ولا تُخفى. */
  {
    itemId: "ITM-002",
    warehouseId: "WH-RIYADH",
    locationId: "DEFAULT",
    quantity: shaped("Measure", { magnitude: "-6.500000", unit: "KG" }),
    unitCost: "0.000000",
    value: "0.0000",
    hasCostBasis: false,
  },
  {
    itemId: "ITM-001",
    warehouseId: "WH-JEDDAH",
    locationId: "B-02-1",
    quantity: shaped("Measure", { magnitude: "12.000000", unit: "PCS" }),
    unitCost: "0.100000",
    value: "1.2000",
    hasCostBasis: true,
  },
].map((row) => shaped("StockBalance", row));

const MOVEMENT = shaped("StockMovement", {
  id: S.IDS.movement,
  number: "SM-0001",
  occurredOn: "2026-05-11",
  direction: "IN",
  itemId: "ITM-001",
  itemGroup: "beverages",
  warehouseId: "WH-RIYADH",
  locationId: "A-01-3",
  quantity: shaped("Measure", { magnitude: "120.000000", unit: "PCS" }),
  cost: "144.0000",
  state: "DRAFT",
  entryId: null,
  alreadyPosted: false,
});

/* ═══════════════════════════════════════════ الموارد البشرية ══════════ */

const EMPLOYEE = shaped("HrEmployee", {
  id: S.IDS.employee,
  code: "EMP-0007",
  nameAr: "سالم بن محمد الحربي",
  nameTranslations: EN("Salem bin Mohammed Al-Harbi"),
  classCode: "class-private",
  costCenterId: S.COST_CENTER,
  employmentId: "e1111111-1111-4111-8111-111111111111",
  startedOn: "2023-09-01",
  endedOn: null,
  state: "Active",
  /* الهوية والحساب البنكي **مقنَّعان على السلك** — العقد لا ينشر غير القناع. */
  identity: shaped("HrMaskedIdentity", { nationalIdMask: "**********7766", ibanMask: "**********1234" }),
});

const PAY_COMPONENTS = [
  { code: "BASIC", ar: "الراتب الأساسي", en: "Basic salary", kind: "earning", cw: true, eos: true },
  { code: "HOUSING", ar: "بدل سكن", en: "Housing allowance", kind: "earning", cw: true, eos: false },
  { code: "TRANSPORT", ar: "بدل نقل", en: "Transport allowance", kind: "earning", cw: false, eos: false },
  { code: "GOSI-E", ar: "حصة الموظف في التأمينات", en: "Employee social insurance", kind: "deduction", cw: false, eos: false },
].map((c, index) =>
  shaped("HrPayComponent", {
    id: "f1111111-1111-4111-8111-00000000000" + String(index + 1),
    code: c.code,
    nameAr: c.ar,
    nameTranslations: EN(c.en),
    kind: c.kind,
    entersContributoryWage: c.cw,
    entersEndOfServiceBase: c.eos,
  })
);

const PAY_ELEMENTS = [
  { code: "BASIC", amount: "9000.0000" },
  { code: "HOUSING", amount: "2250.0000" },
  { code: "TRANSPORT", amount: "800.0000" },
].map((e, index) =>
  shaped("HrPayElement", {
    id: "f2222222-2222-4222-8222-00000000000" + String(index + 1),
    componentCode: e.code,
    amount: e.amount,
    effectiveFrom: "2026-01-01",
  })
);

const AMOUNTS = shaped("HrPayrollAmounts", {
  grossEntitlements: "12050.0000",
  deductions: "1230.0000",
  employeeSocialInsurance: "1233.7500",
  employerSocialInsurance: "1345.9000",
  advanceInstalment: "0.0000",
  netPayable: "9586.2500",
});

const PAYROLL_RUN = shaped("HrPayrollRun", {
  id: S.IDS.payrollRun,
  number: "PR-2026-05",
  periodCode: "2026-05",
  periodStart: "2026-05-01",
  periodEnd: "2026-05-31",
  state: "DRAFT",
  payslipCount: 1,
  amounts: AMOUNTS,
});

const PAYSLIP = shaped("HrPayslip", {
  id: S.IDS.payslip,
  runId: S.IDS.payrollRun,
  employeeId: S.IDS.employee,
  employeeCode: "EMP-0007",
  employmentId: "e1111111-1111-4111-8111-111111111111",
  costCenterId: S.COST_CENTER,
  state: "DRAFT",
  entryId: null,
  alreadyPosted: false,
  contributoryWage: "11250.0000",
  amounts: AMOUNTS,
  components: [
    { no: 1, code: "BASIC", kind: "earning", amount: "9000.0000", cw: true },
    { no: 2, code: "HOUSING", kind: "earning", amount: "2250.0000", cw: true },
    { no: 3, code: "TRANSPORT", kind: "earning", amount: "800.0000", cw: false },
    { no: 4, code: "GOSI-E", kind: "deduction", amount: "1233.7500", cw: false },
  ].map((c) =>
    shaped("HrPayslipComponent", {
      lineNo: c.no,
      componentCode: c.code,
      kind: c.kind,
      amount: c.amount,
      entersContributoryWage: c.cw,
    })
  ),
});

/* ═══════════════════════════════════════════════ جدول الأجوبة ═════════ */

/** جوابٌ لكل عملية، بمعرّفها في العقد. */
export const ANSWERS: Readonly<Record<string, (ask: Ask) => Answer>> = {
  /* ── الجذع ─────────────────────────────────────────────────────────── */
  health: () =>
    ok(shaped("HealthResponse", { status: "ok", apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA" })),

  readSession: () =>
    ok(
      shaped("Session", {
        userId: "u0000000-0000-4000-8000-000000000001",
        tenantId: "t0000000-0000-4000-8000-000000000001",
        companyCount: 1,
        companies: [
          shaped("SessionCompany", {
            companyId: S.COMPANY_ID,
            nameAr: S.COMPANY_NAME_AR,
            nameTranslations: EN(S.COMPANY_NAME_EN),
            state: "Ready",
            decimalPlaces: 4,
            defaultCostCenter: S.COST_CENTER,
          }),
        ],
      })
    ),

  readCompanySetup: () => ok(setup()),
  initialiseCompanySetup: () => ok(setup()),
  addCostCenter: () => ok(setup()),
  renameCostCenter: () => ok(setup()),

  /* ── المحاسبة ──────────────────────────────────────────────────────── */
  readTrialBalance: (ask) => ok(trialBalance(ask)),

  /* القيد اليدوي: يتوازن فيمرّ، ولا يتوازن فيُرفض بنصّ الخادم ورمزه.
     والجسم يصل **مُرمَّزاً**: المال نصٌّ لأن `encodeSchema` حوّله قبل النقل. */
  postJournalEntry: (ask) => {
    const body = ask.body as { lines?: readonly { amount?: string; side?: string }[] } | null;
    const lines = body?.lines ?? [];
    const side = (name: string) =>
      S.sumDecimal(lines.filter((l) => l.side === name).map((l) => l.amount ?? "0.0000"));
    const debit = side("Debit");
    const credit = side("Credit");
    if (lines.length > 0 && debit !== credit) {
      return no({
        ...UNBALANCED,
        ar: "القيد غير متوازن بعملة الشركة: مدين " + debit + " ودائن " + credit + ".",
        en: "The entry does not balance in company currency: debit " + debit + " credit " + credit + ".",
      });
    }
    return ok(
      shaped("PostingReceipt", {
        entryId: S.IDS.entry,
        entryNumber: "412",
        chainSequence: "1187",
        /* بصمةٌ ثابتة: لا سلسلة تُوقَّع هنا، وثباتُها يقول ذلك. */
        entryHash: "0000000000000000000000000000000000000000000000000000000000000000",
        generation: 1,
        lineCount: Math.max(lines.length, 2),
        periodCode: S.PERIOD,
        alreadyPosted: false,
      })
    );
  },

  /* ── العقارات ──────────────────────────────────────────────────────── */
  readPropertyOwner: () => ok(OWNER),
  createPropertyOwner: () => ok(OWNER),
  readLessee: () => ok(LESSEE),
  createLessee: () => ok(LESSEE),
  readProperty: () => ok(PROPERTY),
  createProperty: () => ok(PROPERTY),
  createUnit: () => ok(UNIT),
  readUnit: () => ok(UNIT),
  readLeaseContract: () => ok(LEASE),
  draftLeaseContract: () => ok(shaped("Lease", { ...LEASE, state: "DRAFT" })),
  activateLeaseContract: () => ok(LEASE),
  readLeaseSchedule: () => ok(shaped("LeaseSchedule", { leaseId: S.IDS.lease, lines: SCHEDULE_LINES })),

  /* فوترة قسطٍ من عقارٍ مُدارٍ بأكثر من مالك: **رفضٌ مُسمّى** لا قسمةٌ مخترعة. */
  draftRentInvoice: () => no(OWNER_SHARE_SPLIT_NOT_DECIDED),
  postRentInvoice: () => no(OWNER_SHARE_SPLIT_NOT_DECIDED),
  readRentInvoice: () =>
    ok(
      shaped("RentInvoice", {
        id: S.IDS.rentInvoice,
        number: "RNT-2026-0112",
        state: "DRAFT",
        eventCode: "realestate.rent_invoice.managed_property",
        vatTreatment: "standard",
        exemptionReasonCode: "",
        exemptionReasonPending: false,
        net: "120000.0000",
        tax: "18000.0000",
        gross: "138000.0000",
        entryId: null,
        alreadyPosted: false,
      })
    ),

  readTenantArrearsAging: (ask) =>
    ok(
      shaped("TenantArrears", {
        asOf: ask.query.get("asOf") ?? S.PERIOD_END,
        parties: ARREARS_ROWS,
        totals: arrearsTotals(),
        controlTotal: "268050.0000",
        divergence: "0.0000",
        isReconciled: true,
      })
    ),

  readTenantReceipt: () =>
    ok(
      shaped("TenantReceipt", {
        id: S.IDS.receipt,
        number: "TRC-2026-0087",
        state: "DRAFT",
        eventCode: "realestate.tenant_receipt",
        received: "69000.0000",
        isAllocated: false,
        entryId: null,
        allocationEntryId: null,
        alreadyPosted: false,
      })
    ),
  draftTenantReceipt: () =>
    ok(
      shaped("TenantReceipt", {
        id: S.IDS.receipt,
        number: "TRC-2026-0087",
        state: "DRAFT",
        eventCode: "realestate.tenant_receipt",
        received: "69000.0000",
        isAllocated: false,
        entryId: null,
        allocationEntryId: null,
        alreadyPosted: false,
      })
    ),

  /* ── المقاولات ─────────────────────────────────────────────────────── */
  listProjects: () =>
    ok(
      shaped("ProjectList", {
        projectCount: 1,
        projects: [
          shaped("Project", {
            id: S.IDS.project,
            code: "PRJ-004",
            nameAr: "توسعة مستودعات الدمام — المرحلة الثانية",
            nameTranslations: EN("Dammam warehouse expansion — phase two"),
            startedOn: "2026-02-15",
            isActive: true,
            contracts: [
              shaped("ProjectContractSummary", { id: S.IDS.contract, number: "PC-2026-0007", currencyCode: "SAR" }),
            ],
          }),
        ],
      })
    ),
  readProjectContract: () => ok(CONTRACT),
  addProjectContract: () => ok(CONTRACT),
  readBoqItems: () => ok(shaped("BoqItemList", { itemCount: BOQ.length, items: BOQ })),
  readContractChangeOrders: () => ok(shaped("ChangeOrderList", { changeOrderCount: 0, changeOrders: [] })),
  readContractClientCertificates: () =>
    ok(shaped("CertificateList", { certificateCount: 1, certificates: [CERTIFICATE] })),
  readContractPosition: () =>
    ok(
      shaped("ContractPosition", {
        contractId: S.IDS.contract,
        contractNumber: "PC-2026-0007",
        postedCertificateCount: 1,
        retentionOutstanding: "148000.0000",
        advanceOutstanding: "96000.0000",
        pendingPolicy: PENDING,
      })
    ),
  readCertificate: () => ok(CERTIFICATE),
  readClientCertificate: () => ok(CERTIFICATE),
  readSubcontractorCertificate: () => ok(CERTIFICATE),
  draftClientCertificate: () => ok(CERTIFICATE),
  draftSubcontractorCertificate: () => ok(CERTIFICATE),

  /* ترحيل مستخلصٍ على عقدٍ بأربعة بنودٍ معلَّقة: **رفضٌ يسمّي البنود**. */
  postClientCertificate: () => no(CONTRACT_POLICY_PENDING),
  postSubcontractorCertificate: () => no(CONTRACT_POLICY_PENDING),

  readSubcontractor: () => ok(SUBCONTRACTOR),
  addSubcontractor: () => ok(SUBCONTRACTOR),
  readSubcontract: () => ok(SUBCONTRACT),
  addSubcontract: () => ok(SUBCONTRACT),
  readSubcontractLines: () =>
    ok(
      shaped("SubcontractLineList", {
        lineCount: 2,
        lines: BOQ.slice(0, 2).map((line, index) =>
          shaped("SubcontractLine", {
            id: "eeeeeeee-0000-4000-8000-00000000000" + String(index + 1),
            lineNo: index + 1,
            code: (line as { code: string }).code,
            descriptionAr: (line as { descriptionAr: string }).descriptionAr,
            contractQuantity: (line as { contractQuantity: unknown }).contractQuantity,
            unitRate: (line as { unitRate: string }).unitRate,
          })
        ),
      })
    ),
  readGuarantee: () =>
    ok(
      shaped("Guarantee", {
        id: S.IDS.guarantee,
        number: "BG-2026-0044",
        kind: "performance",
        issuerNameAr: "البنك الأهلي السعودي",
        amount: "240000.0000",
        effectiveFrom: "2026-03-02",
        expiresOn: "2026-09-02",
        contractId: null,
        subcontractId: S.IDS.subcontract,
        attachmentId: "a0000000-0000-4000-8000-000000000001",
      })
    ),
  readRetentionRegister: (ask) =>
    ok(
      shaped("RetentionRegister", {
        asOf: ask.query.get("asOf") ?? S.PERIOD_END,
        rows: RETENTION_ROWS,
        receivableTotal: "148000.0000",
        payableTotal: "62400.0000",
      })
    ),
  readSubcontractorStatement: (ask) =>
    ok(
      shaped("SubcontractorStatement", {
        asOf: ask.query.get("asOf") ?? S.PERIOD_END,
        rows: [
          shaped("SubcontractorStatementRow", {
            subcontractorId: S.IDS.subcontractor,
            code: "SUB-003",
            nameAr: S.SUPPLIERS[0].ar,
            nameTranslations: EN(S.SUPPLIERS[0].en),
            effect: "173450.0000",
          }),
        ],
        subledgerTotal: "173450.0000",
        controlTotal: "173450.0000",
        divergence: "0.0000",
        isReconciled: true,
      })
    ),

  /* ── المخزون ───────────────────────────────────────────────────────── */
  listItems: () => ok(shaped("ItemList", { itemCount: ITEMS.length, items: ITEMS })),
  readItem: () => ok(ITEMS[0]),
  addItem: () => ok(ITEMS[0]),
  readStockBalances: () => ok(shaped("StockBalanceList", { balanceCount: BALANCES.length, balances: BALANCES })),
  listStockMovements: () => ok(shaped("StockMovementList", { movementCount: 1, movements: [MOVEMENT] })),

  /* حركةٌ بوحدةٍ لا يقع تحويلها بلا باقٍ تُرفض ولا تُقرَّب. */
  draftStockMovement: (ask) => {
    const body = ask.body as { quantity?: { unit?: string; magnitude?: string } } | null;
    const unit = body?.quantity?.unit ?? "";
    if (unit === "CTN" || unit === "PLT") return no(UNIT_CONVERSION_NOT_EXACT);
    return ok(MOVEMENT);
  },
  postStockMovement: () => ok(shaped("StockMovement", { ...MOVEMENT, state: "POSTED", alreadyPosted: true, entryId: S.IDS.entry })),
  readInventoryValuation: (ask) =>
    ok(
      shaped("InventoryValuation", {
        asOf: ask.query.get("asOf") ?? S.PERIOD_END,
        subledgerTotal: "145.2000",
        balanceTotal: "145.2000",
        controlTotal: "144.0000",
        divergence: "1.2000",
        isReconciled: false,
        divergences: [
          shaped("InventoryDivergence", {
            documentType: "StockMovement",
            documentId: "SM-0002",
            itemId: "ITM-001",
            reasonCode: "missing_in_control",
            subledgerEffect: "1.2000",
            controlEffect: "0.0000",
            divergence: "1.2000",
          }),
        ],
      })
    ),

  /* ── الموارد البشرية ───────────────────────────────────────────────── */
  readEmployee: () => ok(EMPLOYEE),
  registerEmployee: () => ok(EMPLOYEE),
  terminateEmployee: () => ok(shaped("HrEmployee", { ...EMPLOYEE, state: "Ended", endedOn: "2026-05-31" })),
  listPayComponents: () => ok(shaped("HrPayComponentList", { itemCount: PAY_COMPONENTS.length, items: PAY_COMPONENTS })),
  listPayElements: () => ok(shaped("HrPayElementList", { itemCount: PAY_ELEMENTS.length, items: PAY_ELEMENTS })),
  addPayElement: () => ok(PAY_ELEMENTS[0]),
  readPayrollRun: () => ok(PAYROLL_RUN),
  draftPayrollRun: () => ok(PAYROLL_RUN),
  listPayslips: () => ok(shaped("HrPayslipList", { itemCount: 1, items: [PAYSLIP] })),
  readPayslip: () => ok(PAYSLIP),

  /* الجدول **يُسلَّم فارغاً عمداً**: لا نسبةَ تأميناتٍ تُخترع، والترحيل يُرفض. */
  listPayrollSettings: () => ok(shaped("HrPayrollSettingsList", { itemCount: 0, items: [] })),
  postPayrollRun: () => no(PAYROLL_SETTINGS_MISSING),
  draftPayrollPayment: () => no(PAYROLL_SETTINGS_MISSING),
  draftEndOfServiceProvision: () => no(PAYROLL_SETTINGS_MISSING),
  draftEndOfServiceSettlement: () => no(PAYROLL_SETTINGS_MISSING),
};
