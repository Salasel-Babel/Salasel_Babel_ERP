/* ═══════════════════════════════════════════════════════════════════════════
   بذرة العرض — أسماءٌ وأرقامٌ من المستودع نفسه، لا من خيال
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة مصادر، وكلٌّ مُسمّىً عند موضعه:

   ١ · `demo/company/Company.cs` — اسم المنشأة وعملاؤها ومورّدوها وأصنافها
       بأسعارها. هذه هي بذرة العرض التي يُشغِّلها المستودع على خادمٍ حقيقي.
   ٢ · `data/chart-of-accounts/accounts.csv` — أرقام الحسابات وأسماؤها
       بلغتيها. الدليل المنشور، لا دليلٌ مكتوب هنا.
   ٣ · ما بقي — أرصدةٌ وحركاتٌ ومستندات — **مُركَّبٌ لهذا العرض**، لأن الأرصدة
       الحقيقية تُبنى بتشغيل البذرة على قاعدة بيانات، ولا قاعدة هنا. وهي
       مُعلَّمةٌ عند مواضعها، ومبنيّةٌ بحسابٍ نصّيٍّ مضبوط لا بعائم.

   والمال والكمّية والنسبة **نصوصٌ** في كل ما يلي — لأن العقد يقول إنها كذلك،
   ولأن العائم يفقد `1000000000000.4013` صامتاً.
   ═══════════════════════════════════════════════════════════════════════════ */

/* ─────────────────────────────────── حسابٌ عشريٌّ نصّي، بلا عائم ───────── */

/**
 * يجمع عددين عشريين نصّاً بمقياسٍ واحد — بلا `Number` في أي خطوة.
 * @param a الأول.
 * @param b الثاني.
 */
export function addDecimal(a: string, b: string): string {
  const split = (t: string): [string, string] => {
    const dot = t.indexOf(".");
    return dot < 0 ? [t, ""] : [t.slice(0, dot), t.slice(dot + 1)];
  };
  const [ai, af] = split(a);
  const [bi, bf] = split(b);
  const width = Math.max(af.length, bf.length);
  const sum = (
    BigInt(ai + af.padEnd(width, "0")) + BigInt(bi + bf.padEnd(width, "0"))
  ).toString();
  const negative = sum.startsWith("-");
  const digits = (negative ? sum.slice(1) : sum).padStart(width + 1, "0");
  const text = width === 0 ? digits : digits.slice(0, -width) + "." + digits.slice(-width);
  return negative ? "-" + text : text;
}

/** يجمع قائمة مبالغ نصّياً. */
export const sumDecimal = (values: readonly string[]): string =>
  values.reduce((total, value) => addDecimal(total, value), "0.0000");

/* ────────────────────────────────────────────── المنشأة والدفتر ───────── */

/** معرّف المنشأة في العرض. */
export const COMPANY_ID = "00000000-0000-4000-8000-00000000cafe";
/** اسم المنشأة — `demo/company/Company.cs`. */
export const COMPANY_NAME_AR = "مؤسسة نخيل الشرقية للتجارة والمقاولات";
/** ترجمة الاسم — عرضٌ لا سجلّ (ADR-0021). */
export const COMPANY_NAME_EN = "Eastern Palms Trading & Contracting Est.";
/** الدفتر — `demo/company/Settings.cs`. */
export const BOOK = "MAIN";
/** الفترة المعروضة. */
export const PERIOD = "2026-05";
/** آخر يوم في الفترة. */
export const PERIOD_END = "2026-05-31";
/** مركز التكلفة الافتراضي. */
export const COST_CENTER = "main";
/** نسبة ضريبة القيمة المضافة كما تبذرها البذرة — كسرٌ لا نسبة مئوية. */
export const VAT_RATE = "0.15";

/* ─────────────────────────── ميزان المراجعة — يتوازن أو لا يُعرض ──────── */

/** صفّ ميزان: حسابٌ من الدليل المنشور، ومبلغه على جانبٍ واحد. */
interface SeedRow {
  readonly code: string;
  readonly ar: string;
  readonly en: string;
  readonly side: "debit" | "credit";
  readonly amount: string;
}

/* الحسابات وأسماؤها منقولةٌ عن `data/chart-of-accounts/accounts.csv`.
   والمبالغ **مُركَّبة لهذا العرض**، ومُختارةٌ بحيث يتوازن الميزان بالضبط:
   مجموع المدين = مجموع الدائن = 4,566,850.0000 — والفحص أدناه يمنع الانحراف. */
const SEED_ROWS: readonly SeedRow[] = [
  { code: "1101", ar: "النقد بالصندوق", en: "Cash in Hand", side: "debit", amount: "84500.0000" },
  { code: "1201", ar: "النقد لدى البنوك", en: "Cash at Banks", side: "debit", amount: "1236400.0000" },
  { code: "1301", ar: "العملاء — ذمم مدينة", en: "Customers — Trade Receivables", side: "debit", amount: "912350.0000" },
  { code: "1302", ar: "محتجزات لدى العملاء", en: "Retentions Held by Customers", side: "debit", amount: "148000.0000" },
  { code: "1305", ar: "ضريبة القيمة المضافة المستردة", en: "VAT Recoverable from Authority", side: "debit", amount: "61275.0000" },
  { code: "1401", ar: "مخزون البضاعة", en: "Merchandise Inventory", side: "debit", amount: "473200.0000" },
  { code: "1403", ar: "أعمال تحت التنفيذ", en: "Work in Progress", side: "debit", amount: "318900.0000" },
  { code: "1501", ar: "الأصول الثابتة — التكلفة", en: "Fixed Assets — Cost", side: "debit", amount: "640000.0000" },
  { code: "1502", ar: "مجمع إهلاك الأصول الثابتة", en: "Accumulated Depreciation — Fixed Assets", side: "credit", amount: "192000.0000" },
  { code: "2101", ar: "الموردون — ذمم دائنة", en: "Suppliers — Trade Payables", side: "credit", amount: "406820.0000" },
  { code: "2102", ar: "المقاولون من الباطن", en: "Subcontractors Payable", side: "credit", amount: "173450.0000" },
  { code: "2131", ar: "ضريبة القيمة المضافة — مخرجات", en: "VAT — Output Tax", side: "credit", amount: "138562.5000" },
  { code: "2161", ar: "محتجزات دائنة — ضمان حسن التنفيذ", en: "Retentions Payable — Performance Guarantee", side: "credit", amount: "62400.0000" },
  { code: "2201", ar: "رواتب مستحقة الدفع", en: "Salaries Payable", side: "credit", amount: "97300.0000" },
  { code: "2204", ar: "مخصص مكافأة نهاية الخدمة", en: "End-of-Service Benefits Provision", side: "credit", amount: "214600.0000" },
  { code: "3101", ar: "رأس المال", en: "Share Capital", side: "credit", amount: "2000000.0000" },
  { code: "3201", ar: "الأرباح المبقاة", en: "Retained Earnings", side: "credit", amount: "357217.5000" },
  { code: "4101", ar: "إيرادات المبيعات", en: "Sales Revenue", side: "credit", amount: "737600.0000" },
  { code: "4201", ar: "إيرادات عقود المقاولات", en: "Construction Contract Revenue", side: "credit", amount: "186900.0000" },
  { code: "5101", ar: "تكلفة البضاعة المباعة", en: "Cost of Goods Sold", side: "debit", amount: "398450.0000" },
  { code: "5202", ar: "تكلفة أعمال المقاولين من الباطن", en: "Subcontractor Works Cost", side: "debit", amount: "119600.0000" },
  { code: "5501", ar: "رواتب وأجور", en: "Salaries and Wages", side: "debit", amount: "104725.0000" },
  { code: "5510", ar: "إيجارات", en: "Rent Expense", side: "debit", amount: "48000.0000" },
  { code: "5511", ar: "مرافق — كهرباء ومياه واتصالات", en: "Utilities — Electricity Water and Telecom", side: "debit", amount: "21450.0000" },
];

/** صفوف الميزان كما ينشرها العقد — المال نصّاً. */
export const TRIAL_BALANCE_ROWS = SEED_ROWS.map((row) => ({
  accountCode: row.code,
  nameAr: row.ar,
  nameTranslations: [{ name: "en", value: row.en }],
  debit: row.side === "debit" ? row.amount : "0.0000",
  credit: row.side === "credit" ? row.amount : "0.0000",
}));

/** مجموع المدين. */
export const TOTAL_DEBIT = sumDecimal(TRIAL_BALANCE_ROWS.map((r) => r.debit));
/** مجموع الدائن. */
export const TOTAL_CREDIT = sumDecimal(TRIAL_BALANCE_ROWS.map((r) => r.credit));

/* **ميزانٌ لا يتوازن أسوأ من لا ميزان.** الفحص هنا لا في اختبار: صفٌّ يُعدَّل
   بعد اليوم يُسقِط الصفحة عند التحميل بدل أن يعرض دفتراً كاذباً. */
if (TOTAL_DEBIT !== TOTAL_CREDIT) {
  throw new Error(
    "بذرة العرض: الميزان لا يتوازن — مدين " + TOTAL_DEBIT + " ودائن " + TOTAL_CREDIT +
      ". ولا يُعرض دفترٌ لا يتوازن."
  );
}

/* ───────────────────────────────── أطرافٌ وأصنافٌ من بذرة العرض ────────── */

/** العملاء — `demo/company/Company.cs`. */
export const CUSTOMERS = [
  { code: "CUS-001", ar: "شركة الفيصلية للمقاولات المحدودة", en: "Al-Faisaliah Contracting Ltd." },
  { code: "CUS-002", ar: "مؤسسة درّة الخليج للتجارة", en: "Durrat Al-Khaleej Trading Est." },
  { code: "CUS-003", ar: "شركة الرياض للتطوير العمراني", en: "Riyadh Urban Development Co." },
  { code: "CUS-004", ar: "مصنع الجزيرة للمنتجات البلاستيكية", en: "Al-Jazeera Plastics Factory" },
  { code: "CUS-007", ar: "مجموعة السلام الطبية", en: "Al-Salam Medical Group" },
] as const;

/** المورّدون — `demo/company/Company.cs`. */
export const SUPPLIERS = [
  { code: "SUP-001", ar: "شركة الخليج لمواد البناء", en: "Gulf Building Materials Co." },
  { code: "SUP-002", ar: "مؤسسة الشرق للتوريدات الصناعية", en: "Al-Sharq Industrial Supplies Est." },
  { code: "SUP-004", ar: "مكتب الأمانة للاستشارات المحاسبية", en: "Al-Amanah Accounting Consultancy" },
] as const;

/** بنودٌ بأسعارها — `demo/company/Company.cs`. */
export const CATALOGUE = [
  { ar: "توريد وتركيب أعمال كهربائية", en: "Electrical works supply and installation", price: "4500.0000" },
  { ar: "أعمال تشطيبات داخلية", en: "Interior finishing works", price: "7250.0000" },
  { ar: "توريد مواد عزل مائي", en: "Waterproofing materials supply", price: "1875.0000" },
  { ar: "خدمات إشراف هندسي", en: "Engineering supervision services", price: "12000.0000" },
  { ar: "أعمال حفر وردم", en: "Excavation and backfilling works", price: "3300.0000" },
] as const;

/* ─────────────────────────────── معرّفات ثابتة يمشي عليها العرض ───────── */

/** معرّفات المستندات في العرض — ثابتةٌ كي يعمل الرابط العميق بعد إعادة التحميل. */
export const IDS = {
  contract: "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  project: "1f0c6f7a-2b3c-4d5e-8f90-a1b2c3d4e5f6",
  certificate: "2a2a2a2a-2a2a-4a2a-8a2a-2a2a2a2a2a2a",
  subcontract: "3b3b3b3b-3b3b-4b3b-8b3b-3b3b3b3b3b3b",
  subcontractor: "4c4c4c4c-4c4c-4c4c-8c4c-4c4c4c4c4c4c",
  guarantee: "5d5d5d5d-5d5d-4d5d-8d5d-5d5d5d5d5d5d",
  lease: "6e6e6e6e-6e6e-4e6e-8e6e-6e6e6e6e6e6e",
  property: "7f7f7f7f-7f7f-4f7f-8f7f-7f7f7f7f7f7f",
  unit: "8a8a8a8a-8a8a-4a8a-8a8a-8a8a8a8a8a8a",
  lessee: "9b9b9b9b-9b9b-4b9b-8b9b-9b9b9b9b9b9b",
  owner: "0c0c0c0c-0c0c-4c0c-8c0c-0c0c0c0c0c0c",
  rentInvoice: "1d1d1d1d-1d1d-4d1d-8d1d-1d1d1d1d1d1d",
  receipt: "2e2e2e2e-2e2e-4e2e-8e2e-2e2e2e2e2e2e",
  employee: "3f3f3f3f-3f3f-4f3f-8f3f-3f3f3f3f3f3f",
  payrollRun: "4a4a4a4a-4a4a-4a4a-8a4a-4a4a4a4a4a4a",
  payslip: "5b5b5b5b-5b5b-4b5b-8b5b-5b5b5b5b5b5b",
  provision: "6c6c6c6c-6c6c-4c6c-8c6c-6c6c6c6c6c6c",
  settlement: "7d7d7d7d-7d7d-4d7d-8d7d-7d7d7d7d7d7d",
  entry: "8e8e8e8e-8e8e-4e8e-8e8e-8e8e8e8e8e8e",
  movement: "9f9f9f9f-9f9f-4f9f-8f9f-9f9f9f9f9f9f",
  itemWater: "11111111-1111-4111-8111-111111111111",
  itemCement: "22222222-2222-4222-8222-222222222222",
} as const;
