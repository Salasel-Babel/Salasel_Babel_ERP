/* ═══════════════════════════════════════════════════════════════════════════
   الأقسام الخمسة — عقدُ الملاحة  ·  The five sections — the navigation contract
   ───────────────────────────────────────────────────────────────────────────
   **الملاحة تحمل الأقسام الخمسة ولو لم تُبنَ شاشاتها.** والقسم غير المبنيّ
   يظهر **بحالةٍ صريحة «قيد البناء»**، لا برابطٍ ميت ولا بغياب: رابطٌ يقود
   إلى لا شيء يُعلّم المستخدم ألّا يثق بالملاحة كلّها، وذلك أغلى من نقصٍ
   مُعلَن. والغياب أسوأ: يجعل النظام يبدو أصغر مما بيع.

   ومن يبني قسماً يبدّل `built` إلى `true` ويكتب `path` — **ولا يضيف صفّاً
   جديداً هنا ولا يسمّي لوناً جديداً**. اللون رمزٌ من `cinematic.css §8`.
   ═══════════════════════════════════════════════════════════════════════════ */

/** قسمٌ من أقسام النظام الخمسة. */
export interface Section {
  /** معرّفٌ ثابت — يُستعمل في الاختبارات وفي رمز اللون. */
  readonly id: "accounting" | "inventory" | "hr" | "contracting" | "realestate";
  /** مفتاح الاسم في طبقة اللغة. العربية مصدرٌ والبقية صفوف (ADR-0021). */
  readonly labelKey: string;
  /** رمز لون القسم في `styles/cinematic.css`. */
  readonly tint: string;
  /** المسار حين يكون مبنيّاً؛ و`null` حين لا يكون. */
  readonly path: string | null;
  /** هل بُنيت له شاشةٌ واحدة على الأقل؟ */
  readonly built: boolean;
}

/** القسم المحاسبي — وهو المرجع حين لا يُعرَف قسمُ مسارٍ ما. */
const ACCOUNTING: Section = {
  id: "accounting",
  labelKey: "app.section.accounting",
  tint: "var(--section-accounting)",
  path: "/",
  built: true,
};

/** الأقسام الخمسة بترتيب عرضها. */
export const SECTIONS: readonly Section[] = [
  ACCOUNTING,
  {
    id: "inventory",
    labelKey: "app.section.inventory",
    tint: "var(--section-inventory)",
    path: "/inventory/stock",
    built: true,
  },
  {
    id: "hr",
    labelKey: "app.section.hr",
    tint: "var(--section-hr)",
    path: "/hr",
    built: true,
  },
  {
    id: "contracting",
    labelKey: "app.section.contracting",
    tint: "var(--section-contracting)",
    path: "/contracting",
    built: true,
  },
  {
    id: "realestate",
    labelKey: "app.section.realestate",
    tint: "var(--section-realestate)",
    path: "/realestate",
    built: true,
  },
];

/** الشاشات المبنيّة داخل القسم المحاسبي — وهي ما تفتحه لوحة الأوامر. */
export interface ScreenEntry {
  readonly path: string;
  readonly labelKey: string;
  readonly section: Section["id"];
  /**
   * مجموعةٌ **داخل** القسم — لا قسمٌ سادس.
   * <p>
   * دورة المستندات تنقسم مجموعتين: ما يخرج إلى العميل («المبيعات») وما يدخل
   * من المورّد («المشتريات»). وكلتاهما **في القسم المحاسبي** — وهو ما يقوله
   * العقد المنشور نفسه: نيّاتُهما كلّها `"section": "Accounting"` وإنّما
   * `"module"` فيها Sales أو Purchasing. فلو صارتا صفّين في {@link SECTIONS}
   * لصارت الأقسام سبعةً وانكسر عقدُ الملاحة الخماسي بلا حاجة.
   * </p>
   * <p>وغيابُها يعني أن الشاشة لا تنتمي إلى مجموعةٍ مُسمّاة داخل قسمها.</p>
   */
  readonly group?: "sales" | "purchasing";
}

/** مجموعةٌ مُسمّاة داخل قسم، بمفتاح اسمها وأولى شاشاتها. */
export interface ScreenGroup {
  readonly id: NonNullable<ScreenEntry["group"]>;
  readonly labelKey: string;
  readonly section: Section["id"];
  /** أول شاشةٍ في المجموعة — وهي ما يُفتح حين تُختار المجموعة. */
  readonly path: string;
}

/** المجموعتان المُسمّاتان داخل القسم المحاسبي، بترتيب الدورة. */
export const SCREEN_GROUPS: readonly ScreenGroup[] = [
  {
    id: "sales",
    labelKey: "accounting.group.sales",
    section: "accounting",
    path: "/sales/invoice",
  },
  {
    id: "purchasing",
    labelKey: "accounting.group.purchasing",
    section: "accounting",
    path: "/purchasing/order",
  },
];

/** كل شاشةٍ مبنيّة، بمسارها ومفتاح اسمها. */
export const SCREENS: readonly ScreenEntry[] = [
  { path: "/", labelKey: "app.nav.trialBalance", section: "accounting" },
  { path: "/voucher", labelKey: "app.nav.voucher", section: "accounting" },
  { path: "/sign-in", labelKey: "app.nav.signIn", section: "accounting" },
  { path: "/contract", labelKey: "app.nav.contract", section: "accounting" },
  { path: "/design", labelKey: "app.nav.design", section: "accounting" },
  { path: "/realestate", labelKey: "realestate.nav.register", section: "realestate" },
  { path: "/realestate/lease", labelKey: "realestate.nav.lease", section: "realestate" },
  { path: "/realestate/arrears", labelKey: "realestate.nav.arrears", section: "realestate" },
  { path: "/contracting", labelKey: "contracting.nav.register", section: "contracting" },
  { path: "/contracting/certificate", labelKey: "contracting.nav.certificate", section: "contracting" },
  { path: "/contracting/subcontracting", labelKey: "contracting.nav.subcontracting", section: "contracting" },
  { path: "/contracting/retention", labelKey: "contracting.nav.retention", section: "contracting" },
  { path: "/inventory/stock", labelKey: "inventory.nav.stock", section: "inventory" },
  { path: "/inventory/items", labelKey: "inventory.nav.items", section: "inventory" },
  { path: "/inventory/movements", labelKey: "inventory.nav.movements", section: "inventory" },
  { path: "/inventory/valuation", labelKey: "inventory.nav.valuation", section: "inventory" },
  /* ── التسكين ووحداته — الشاشات الخمس التي جاءت بعد نزول أبوابها ─────────
     إضافةٌ في موضعٍ واحد متّصل، فتندمج مع من يعمل على هذا الملفّ بلا تعارض. */
  { path: "/inventory/warehouses", labelKey: "inventory.nav.warehouses", section: "inventory" },
  { path: "/inventory/placement", labelKey: "inventory.nav.placement", section: "inventory" },
  { path: "/inventory/placement-balances", labelKey: "inventory.nav.placementBalances", section: "inventory" },
  { path: "/inventory/transfers", labelKey: "inventory.nav.transfers", section: "inventory" },
  { path: "/inventory/units", labelKey: "inventory.nav.units", section: "inventory" },
  { path: "/hr", labelKey: "hr.nav.register", section: "hr" },
  { path: "/hr/payroll", labelKey: "hr.nav.payroll", section: "hr" },
  { path: "/hr/payslip", labelKey: "hr.nav.payslip", section: "hr" },
  { path: "/hr/end-of-service", labelKey: "hr.nav.endOfService", section: "hr" },
  /* الأمر المنطوق يعبر الأقسام الخمسة كلّها، ولا قسمَ واحداً يملكه. وهو مُدرَجٌ
     هنا تحت المحاسبة **لأجل لونه وحده** — وهو اللون المرجعي حين لا يُعرَف القسم.
     (وكُتب هذا الصفّ حين كانت الأقسام الأربعة الأخرى `built: false`؛ وقد صارت
     كلّها مبنيّةً عند إنزال شاشاتها، فالنيّةُ المؤكَّدة تجد اليوم شاشةً تقودها
     إليها.) */
  { path: "/voice", labelKey: "app.nav.voice", section: "accounting" },
  /* ── دورة المستندات المحاسبية: المبيعات ─────────────────────────────────
     الدورة التي وصفها صاحب المصلحة — فاتورة، ثم سند قبض — ثم ما تُقرأ به
     ذمّة العميل. وهي في القسم المحاسبي كما ينصّ العقد، ومجموعتُها مُسمّاة. */
  { path: "/sales/invoice", labelKey: "accounting.nav.salesInvoice", section: "accounting", group: "sales" },
  { path: "/sales/receipt", labelKey: "accounting.nav.customerReceipt", section: "accounting", group: "sales" },
  { path: "/sales/receivables", labelKey: "accounting.nav.receivables", section: "accounting", group: "sales" },
  /* ── والمشتريات، **بترتيب الدورة لا بترتيب الحروف**: أمرٌ ← استلام ←
     فاتورة ← صرف. وترتيبٌ أبجدي هنا كان سيُخفي أن الأربع سلسلةٌ مرتَّبة. */
  { path: "/purchasing/order", labelKey: "accounting.nav.purchaseOrder", section: "accounting", group: "purchasing" },
  { path: "/purchasing/goods-receipt", labelKey: "accounting.nav.goodsReceipt", section: "accounting", group: "purchasing" },
  { path: "/purchasing/bill", labelKey: "accounting.nav.supplierBill", section: "accounting", group: "purchasing" },
  { path: "/purchasing/payment", labelKey: "accounting.nav.supplierPayment", section: "accounting", group: "purchasing" },
];

/**
 * يجد القسم الذي يقع فيه مسارٌ ما.
 * @param path المسار الحالي.
 */
export function sectionOf(path: string): Section {
  const screen = SCREENS.find((s) => s.path === path);
  const id = screen?.section ?? "accounting";
  return SECTIONS.find((s) => s.id === id) ?? ACCOUNTING;
}
