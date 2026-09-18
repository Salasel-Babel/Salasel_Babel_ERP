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
import type { IconName } from "./icons";

/** قسمٌ من أقسام النظام الخمسة. */
export interface Section {
  /** معرّفٌ ثابت — يُستعمل في الاختبارات وفي رمز اللون. */
  readonly id: "accounting" | "inventory" | "hr" | "contracting" | "realestate";
  /** مفتاح الاسم في طبقة اللغة. العربية مصدرٌ والبقية صفوف (ADR-0021). */
  readonly labelKey: string;
  /** رمز لون القسم في `styles/cinematic.css`. */
  readonly tint: string;
  /**
   * رمزُ النظام في مُشغّل الأنظمة وفي مربّعات صفحة البداية.
   * <p>
   * **وهو نفسه في الموضعين** — فمن يرى مربّع «المخزني» في البداية يجد الرمزَ
   * ذاته في المُشغّل وفي رأس الصفحة، فيتعلّمه مرّةً. ورمزان لنظامٍ واحد
   * يُعلّمان ألّا يُعتمَد على الرمز أصلاً.
   * </p>
   */
  readonly icon: IconName;
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
  icon: "ledger",
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
    icon: "boxes",
    path: "/inventory/stock",
    built: true,
  },
  {
    id: "hr",
    labelKey: "app.section.hr",
    tint: "var(--section-hr)",
    icon: "people",
    path: "/hr",
    built: true,
  },
  {
    id: "contracting",
    labelKey: "app.section.contracting",
    tint: "var(--section-contracting)",
    icon: "crane",
    path: "/contracting",
    built: true,
  },
  {
    id: "realestate",
    labelKey: "app.section.realestate",
    tint: "var(--section-realestate)",
    icon: "building",
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
   * رمزُ العقدة في شجرة الملاحة — <b>وهو حقلٌ إلزامي لا اختياري</b>.
   * <p>
   * وإلزامُه هو الحارس: شجرةٌ بعض عقدها مرسومٌ وبعضها فارغ تُقرأ معطوبةً لا
   * ناقصة، فتُفقد الأيقونةُ فائدتَها كلَّها. ومن يضيف شاشةً يختار رمزاً من
   * {@link IconName} — والاتّحاد المقفل يمنعه من اختراع رمزٍ سادسٍ وأربعين
   * في شاشته.
   * </p>
   */
  readonly icon: IconName;
  /**
   * مجموعةٌ **داخل** القسم — لا قسمٌ سادس.
   * <p>
   * دورة المستندات تنقسم مجموعتين: ما يخرج إلى العميل («المبيعات») وما يدخل
   * من المورّد («المشتريات»). وكلتاهما **في القسم المحاسبي** — وهو ما يقوله
   * العقد المنشور نفسه: نيّاتُهما كلّها `"section": "Accounting"` وإنّما
   * `"module"` فيها Sales أو Purchasing. فلو صارتا صفّين في {@link SECTIONS}
   * لصارت الأقسام سبعةً وانكسر عقدُ الملاحة الخماسي بلا حاجة.
   * </p>
   * <p>
   * **والمجموعاتُ اليوم أكثرُ من اثنتين، وليس في ذلك نقضٌ لما سبق.** كانت
   * `sales` و`purchasing` وحدهما لأنهما الوحيدتان اللتان يسمّيهما العقد
   * المنشور في `"module"`. ثم طلب المالك شجرةً من طبقتين في كل نظام — أي
   * **فصلَ قراءةٍ داخل القسم**، لا وحدةَ ترخيصٍ ولا قسماً في العقد. فما زاد
   * هنا هو فصلُ قراءةٍ فقط: لا يُغيّر نيّةً، ولا يظهر في العقد، ولا يُحسب في
   * عدّ الأقسام. وعقدُ الملاحة خماسيٌّ مقفلٌ كما هو (ADR-0069)، ويحرسه
   * `SECTIONS.length === 5` في `tests/accounting.test.tsx`.
   * </p>
   * <p>وغيابُها يعني أن الشاشة عقدةٌ في الطبقة الأولى — ورقةٌ لا تحتها شيء.</p>
   */
  readonly group?: GroupId;
  /**
   * شاشةٌ لا يملكها نظامٌ واحد فتُعرَض في ملاحة الأنظمة كلّها.
   * <p>
   * **وواحدةٌ فقط اليوم: صفحة البداية.** وهي ليست استثناءً مفتوحاً — كلُّ
   * ما يحمل هذه الراية يُستثنى من حارس «لا يعرض القسمُ شاشةَ قسمٍ آخر»، فلو
   * صارت راية تُوزَّع لبطل الحارس. والقاعدة: ما يحملها **مدخلٌ إلى الأنظمة
   * لا شاشةُ عمل في أحدها**.
   * </p>
   */
  readonly universal?: true;
}

/**
 * معرّفاتُ مجموعات القراءة — <b>مقفلةٌ كما تُقفل الأقسام</b>.
 * <p>
 * واتّحادٌ مُسمّى لا نصٌّ حرّ، فالمجموعةُ التي يكتبها من يضيف شاشةً يجب أن
 * يكون لها صفٌّ في {@link SCREEN_GROUPS} باسمٍ ورمزٍ ونظام — وإلّا سقطت
 * الشاشةُ من الشجرة صامتةً. والمترجمُ يمنع ذلك قبل الاختبار.
 * </p>
 */
export type GroupId =
  | "sales"
  | "purchasing"
  | "postings"
  | "evidence"
  | "admin"
  | "setup"
  | "tools"
  | "invCatalogue"
  | "invMovement"
  | "invPlaces"
  | "hrPayroll"
  | "hrObligations"
  | "ctrScope"
  | "ctrBilling"
  | "ctrSub"
  | "reRegistry"
  | "reLease";

/** مجموعةٌ مُسمّاة داخل قسم، بمفتاح اسمها ورمزها وأولى شاشاتها. */
export interface ScreenGroup {
  readonly id: GroupId;
  readonly labelKey: string;
  readonly section: Section["id"];
  /** رمزُ عقدة الطبقة الأولى في الشجرة. */
  readonly icon: IconName;
  /** أول شاشةٍ في المجموعة — وهي ما يُفتح حين تُختار المجموعة. */
  readonly path: string;
}

/* ═══════════════════════════════════════════════════════════════════════════
   مجموعاتُ القراءة — الطبقةُ الأولى من شجرة كل نظام
   ───────────────────────────────────────────────────────────────────────────
   **الترتيب هنا هو ترتيبُ العرض**، وهو ترتيبُ العمل لا ترتيبُ الحروف: في
   المحاسبي تُقرأ الدورتان أوّلاً (ما يخرج إلى العميل وما يدخل من المورّد)،
   ثم ما يُصحَّح بعد الترحيل، ثم ما يُوثَّق به، ثم ما يُؤسَّس مرّةً، ثم
   الإدارة، ثم الأدوات. ومن يقلب الترتيب يقلب ما تراه العينُ أوّلاً.

   **ولا مجموعةَ من شاشةٍ واحدة**: عقدةٌ تنفتح على ورقةٍ يتيمة تُكلّف نقرةً
   ولا تُعطي تصنيفاً — والشاشةُ الوحيدة تبقى ورقةً في الطبقة الأولى. ويحرس
   ذلك `tests/shell-nav.test.tsx`.
   ═══════════════════════════════════════════════════════════════════════════ */
export const SCREEN_GROUPS: readonly ScreenGroup[] = [
  /* ── المحاسبي ─────────────────────────────────────────────────────────── */
  {
    id: "sales",
    labelKey: "accounting.group.sales",
    section: "accounting",
    icon: "cart",
    path: "/sales/invoice",
  },
  {
    id: "purchasing",
    labelKey: "accounting.group.purchasing",
    section: "accounting",
    icon: "bag",
    path: "/purchasing/order",
  },
  {
    id: "postings",
    labelKey: "app.group.postings",
    section: "accounting",
    icon: "undo",
    path: "/ledger/entry",
  },
  {
    id: "evidence",
    labelKey: "app.group.evidence",
    section: "accounting",
    icon: "paperclip",
    path: "/attachments",
  },
  {
    id: "setup",
    labelKey: "app.nav.setupGroup",
    section: "accounting",
    icon: "sliders",
    path: "/setup",
  },
  {
    id: "admin",
    labelKey: "app.nav.administration",
    section: "accounting",
    icon: "shield",
    path: "/admin/session",
  },
  {
    id: "tools",
    labelKey: "app.group.tools",
    section: "accounting",
    icon: "palette",
    path: "/design",
  },
  /* ── المخزني ──────────────────────────────────────────────────────────── */
  {
    id: "invCatalogue",
    labelKey: "app.group.invCatalogue",
    section: "inventory",
    icon: "tag",
    path: "/inventory/items",
  },
  {
    id: "invMovement",
    labelKey: "app.group.invMovement",
    section: "inventory",
    icon: "arrows",
    path: "/inventory/movements",
  },
  {
    id: "invPlaces",
    labelKey: "app.group.invPlaces",
    section: "inventory",
    icon: "warehouse",
    path: "/inventory/warehouses",
  },
  /* ── الموارد البشرية ──────────────────────────────────────────────────── */
  {
    id: "hrPayroll",
    labelKey: "app.group.hrPayroll",
    section: "hr",
    icon: "wallet",
    path: "/hr/payroll",
  },
  {
    id: "hrObligations",
    labelKey: "app.group.hrObligations",
    section: "hr",
    icon: "shield",
    path: "/hr/social-insurance",
  },
  /* ── المقاولات ────────────────────────────────────────────────────────── */
  {
    id: "ctrScope",
    labelKey: "app.group.ctrScope",
    section: "contracting",
    icon: "swap",
    path: "/contracting/change-orders",
  },
  {
    id: "ctrBilling",
    labelKey: "app.group.ctrBilling",
    section: "contracting",
    icon: "receipt",
    path: "/contracting/certificate",
  },
  {
    id: "ctrSub",
    labelKey: "app.group.ctrSub",
    section: "contracting",
    icon: "network",
    path: "/contracting/subcontracting",
  },
  /* ── العقاري ──────────────────────────────────────────────────────────── */
  {
    id: "reRegistry",
    labelKey: "app.group.reRegistry",
    section: "realestate",
    icon: "building",
    path: "/realestate",
  },
  {
    id: "reLease",
    labelKey: "app.group.reLease",
    section: "realestate",
    icon: "key",
    path: "/realestate/lease",
  },
];

/** كل شاشةٍ مبنيّة، بمسارها ومفتاح اسمها. */
export const SCREENS: readonly ScreenEntry[] = [
  /* **صفحةُ البداية — مدخلُ الأنظمة، ولا نظامَ يملكها.** وهي في القسم
     المحاسبي **لأجل لونه وحده** كما `/voice` و`/design` قبلها (وهو اللون
     المرجعي حين لا يُعرَف القسم)، و`universal` هي التي تجعلها تُعرَض في
     ملاحة الأنظمة الخمسة كلّها لا في المحاسبي وحده. */
  {
    path: "/home",
    labelKey: "app.nav.home",
    section: "accounting",
    icon: "home",
    universal: true,
  },
  {
    path: "/",
    labelKey: "app.nav.trialBalance",
    section: "accounting",
    icon: "scale",
  },
  {
    path: "/voucher",
    labelKey: "app.nav.voucher",
    section: "accounting",
    icon: "pen",
  },
  {
    path: "/sign-in",
    labelKey: "app.nav.signIn",
    section: "accounting",
    icon: "key",
    group: "tools",
  },
  {
    path: "/contract",
    labelKey: "app.nav.contract",
    section: "accounting",
    icon: "file",
    group: "tools",
  },
  {
    path: "/design",
    labelKey: "app.nav.design",
    section: "accounting",
    icon: "palette",
    group: "tools",
  },
  /* ── العقارات — أربعٌ **بترتيب العمل لا بترتيب الحروف**: العقارُ ووحداته
     يُعرَّفان مرّةً ← ثم طرفا العقد (المالك الذي نُحصّل له والمستأجر الذي
     نُحصّل منه) ← ثم العقد وجدوله ← ثم ما تأخّر وما قُبض. والترتيب هنا هو
     ترتيب الشريط داخل القسم في `screens/realestate/parts.tsx` نفسه. */
  {
    path: "/realestate",
    labelKey: "realestate.nav.register",
    section: "realestate",
    icon: "building",
    group: "reRegistry",
  },
  {
    path: "/realestate/parties",
    labelKey: "realestate.nav.parties",
    section: "realestate",
    icon: "people",
    group: "reRegistry",
  },
  {
    path: "/realestate/lease",
    labelKey: "realestate.nav.lease",
    section: "realestate",
    icon: "key",
    group: "reLease",
  },
  {
    path: "/realestate/arrears",
    labelKey: "realestate.nav.arrears",
    section: "realestate",
    icon: "clock",
    group: "reLease",
  },
  /* ── المقاولات — سبعٌ **بترتيب العمل**: المشروع وعقده يُسجَّلان ← ما يغيّر
     نطاق العقد ← ما يُوثَّق عليه قبل أن يتحرّك مال ← المستخلص ← الباطن ←
     دفعته المقدمة ← ما يُحتجز ويُطابَق عند الإقفال. */
  {
    path: "/contracting",
    labelKey: "contracting.nav.register",
    section: "contracting",
    icon: "clipboard",
  },
  {
    path: "/contracting/change-orders",
    labelKey: "contracting.nav.changeOrders",
    section: "contracting",
    icon: "swap",
    group: "ctrScope",
  },
  {
    path: "/contracting/guarantees",
    labelKey: "contracting.nav.guarantees",
    section: "contracting",
    icon: "shield",
    group: "ctrScope",
  },
  {
    path: "/contracting/certificate",
    labelKey: "contracting.nav.certificate",
    section: "contracting",
    icon: "receipt",
    group: "ctrBilling",
  },
  {
    path: "/contracting/subcontracting",
    labelKey: "contracting.nav.subcontracting",
    section: "contracting",
    icon: "network",
    group: "ctrSub",
  },
  {
    path: "/contracting/advances",
    labelKey: "contracting.nav.advances",
    section: "contracting",
    icon: "cash",
    group: "ctrSub",
  },
  {
    path: "/contracting/retention",
    labelKey: "contracting.nav.retention",
    section: "contracting",
    icon: "lock",
    group: "ctrBilling",
  },
  {
    path: "/inventory/stock",
    labelKey: "inventory.nav.stock",
    section: "inventory",
    icon: "boxes",
  },
  {
    path: "/inventory/items",
    labelKey: "inventory.nav.items",
    section: "inventory",
    icon: "tag",
    group: "invCatalogue",
  },
  {
    path: "/inventory/movements",
    labelKey: "inventory.nav.movements",
    section: "inventory",
    icon: "arrows",
    group: "invMovement",
  },
  {
    path: "/inventory/valuation",
    labelKey: "inventory.nav.valuation",
    section: "inventory",
    icon: "chart",
  },
  /* ── الموارد البشرية — ثمانٍ **بترتيب العمل لا بترتيب الحروف**: ما يُعرَّف
     مرّةً (مكوّنات الأجر) ← من يُسجَّل ← ما يُقيَّد عليه قبل الشهر (السلف
     والاستقطاعات) ← المسيّر ← قسيمته ← ما يُسدَّد عن الشهر إلى الجهة ← ما
     يُنهي العلاقة ← ما يُطابَق عند الإقفال. والترتيب هنا هو ترتيب الشريط
     داخل القسم في `screens/hr/parts.tsx` نفسه. */
  {
    path: "/hr/pay-components",
    labelKey: "hr.nav.payComponents",
    section: "hr",
    icon: "sliders",
    group: "hrPayroll",
  },
  /* ── التسكين ووحداته — الشاشات الخمس التي جاءت بعد نزول أبوابها ─────────
     إضافةٌ في موضعٍ واحد متّصل، فتندمج مع من يعمل على هذا الملفّ بلا تعارض. */
  {
    path: "/inventory/warehouses",
    labelKey: "inventory.nav.warehouses",
    section: "inventory",
    icon: "warehouse",
    group: "invPlaces",
  },
  {
    path: "/inventory/placement",
    labelKey: "inventory.nav.placement",
    section: "inventory",
    icon: "map",
    group: "invPlaces",
  },
  {
    path: "/inventory/placement-balances",
    labelKey: "inventory.nav.placementBalances",
    section: "inventory",
    icon: "target",
    group: "invPlaces",
  },
  {
    path: "/inventory/transfers",
    labelKey: "inventory.nav.transfers",
    section: "inventory",
    icon: "swap",
    group: "invMovement",
  },
  {
    path: "/inventory/units",
    labelKey: "inventory.nav.units",
    section: "inventory",
    icon: "ruler",
    group: "invCatalogue",
  },
  {
    path: "/hr",
    labelKey: "hr.nav.register",
    section: "hr",
    icon: "people",
  },
  {
    path: "/hr/advances-deductions",
    labelKey: "hr.nav.advances",
    section: "hr",
    icon: "cash",
    group: "hrPayroll",
  },
  {
    path: "/hr/payroll",
    labelKey: "hr.nav.payroll",
    section: "hr",
    icon: "wallet",
    group: "hrPayroll",
  },
  {
    path: "/hr/payslip",
    labelKey: "hr.nav.payslip",
    section: "hr",
    icon: "slip",
    group: "hrPayroll",
  },
  {
    path: "/hr/social-insurance",
    labelKey: "hr.nav.socialInsurance",
    section: "hr",
    icon: "shield",
    group: "hrObligations",
  },
  {
    path: "/hr/end-of-service",
    labelKey: "hr.nav.endOfService",
    section: "hr",
    icon: "exit",
    group: "hrObligations",
  },
  {
    path: "/hr/subledger-reconciliation",
    labelKey: "hr.nav.reconciliation",
    section: "hr",
    icon: "check",
    group: "hrObligations",
  },
  /* الأمر المنطوق يعبر الأقسام الخمسة كلّها، ولا قسمَ واحداً يملكه. وهو مُدرَجٌ
     هنا تحت المحاسبة **لأجل لونه وحده** — وهو اللون المرجعي حين لا يُعرَف القسم.
     (وكُتب هذا الصفّ حين كانت الأقسام الأربعة الأخرى `built: false`؛ وقد صارت
     كلّها مبنيّةً عند إنزال شاشاتها، فالنيّةُ المؤكَّدة تجد اليوم شاشةً تقودها
     إليها.) */
  {
    path: "/voice",
    labelKey: "app.nav.voice",
    section: "accounting",
    icon: "mic",
    group: "tools",
  },
  /* ── دورة المستندات المحاسبية: المبيعات ─────────────────────────────────
     الدورة التي وصفها صاحب المصلحة — فاتورة، ثم سند قبض — ثم ما تُقرأ به
     ذمّة العميل. وهي في القسم المحاسبي كما ينصّ العقد، ومجموعتُها مُسمّاة. */
  {
    path: "/sales/invoice",
    labelKey: "accounting.nav.salesInvoice",
    section: "accounting",
    icon: "receipt",
    group: "sales",
  },
  {
    path: "/sales/receipt",
    labelKey: "accounting.nav.customerReceipt",
    section: "accounting",
    icon: "cash",
    group: "sales",
  },
  {
    path: "/sales/receivables",
    labelKey: "accounting.nav.receivables",
    section: "accounting",
    icon: "clock",
    group: "sales",
  },
  /* ── والمشتريات، **بترتيب الدورة لا بترتيب الحروف**: أمرٌ ← استلام ←
     فاتورة ← صرف. وترتيبٌ أبجدي هنا كان سيُخفي أن الأربع سلسلةٌ مرتَّبة. */
  {
    path: "/purchasing/order",
    labelKey: "accounting.nav.purchaseOrder",
    section: "accounting",
    icon: "clipboard",
    group: "purchasing",
  },
  {
    path: "/purchasing/goods-receipt",
    labelKey: "accounting.nav.goodsReceipt",
    section: "accounting",
    icon: "truck",
    group: "purchasing",
  },
  {
    path: "/purchasing/bill",
    labelKey: "accounting.nav.supplierBill",
    section: "accounting",
    icon: "receipt",
    group: "purchasing",
  },
  {
    path: "/purchasing/payment",
    labelKey: "accounting.nav.supplierPayment",
    section: "accounting",
    icon: "cash",
    group: "purchasing",
  },
  {
    path: "/purchasing/payables",
    labelKey: "accounting.nav.payables",
    section: "accounting",
    icon: "clock",
    group: "purchasing",
  },
  /* ── سجلُّ المرفقات وعهدةُ سنده، وحالُ الصنف — **كتلةٌ واحدة متّصلة** كي
     يندمج جانباها آلياً حين يلمس أسطولٌ آخر هذا الملفّ.

     والمرفقات في القسم **المحاسبي لأجل لونه** لا لأنها تخصّه: السند يعبر
     الأقسام الخمسة كلَّها — خطابُ ضمانٍ في المقاولات، وعقدُ إيجارٍ في
     العقارات، وفاتورةٌ في المبيعات — ولا قسمَ واحداً يملكه. وهو الحكم نفسه
     المكتوب للأمر المنطوق أعلاه، والقسم المحاسبي هو اللون المرجعي حين لا
     يُعرَف القسم. وسندُ القيد أقربُ ما يكون إلى الدفتر على أي حال (ADR-0046:
     «المرفق دليلٌ فيخضع لانضباط الدفتر»).

     وشاشتان للمرفقات لا واحدة: أبوابُ الكتابة فيها **ثلاثة** — إيداعٌ
     وتصحيحٌ وسحب — وحدُّ ADR-0080 اثنان. وشاشةٌ واحدة للصنف: أبوابُ الكتابة
     فيه **اثنان** بالضبط. والترتيب ترتيبُ العمل: ما يُودَع ويُستخرَج ← ما
     يُحكَم عليه بعد إيداعه. */
  {
    path: "/attachments",
    labelKey: "accounting.nav.attachments",
    section: "accounting",
    icon: "paperclip",
    group: "evidence",
  },
  {
    path: "/attachments/custody",
    labelKey: "accounting.nav.attachmentCustody",
    section: "accounting",
    icon: "shield",
    group: "evidence",
  },
  {
    path: "/inventory/item-lifecycle",
    labelKey: "inventory.nav.itemLifecycle",
    section: "inventory",
    icon: "refresh",
    group: "invCatalogue",
  },
  /* ── ما بعد الترحيل — أربعٌ **بترتيب العمل لا بترتيب الحروف**، و**كتلةٌ
     واحدة متّصلة** كي يندمج جانباها آلياً حين يلمس أسطولٌ آخر هذا الملفّ.

     والسؤال الذي تجيبه المجموعة واحد: **ما رُحّل خطأً، كيف يُصحَّح، وكيف
     نُثبت أنه لم يُعدَّل؟** فالقيدُ يُعكَس بقيدٍ مضادّ على الدفتر نفسه ←
     ثم المستندُ التجاري يُصحَّح تجاه المورّد ← ثم تجاه العميل ← ثم يُحكَم
     على سلامة السلسلة بعد ذلك كلّه.

     **ولا مجموعةٌ ثالثة ولا قسمٌ سادس**: عقد الملاحة خماسيّ مقفل (ADR-0069)،
     والمجموعتان المُسمّاتان `sales` و`purchasing` **سلسلتان مرتَّبتان** —
     أمرٌ ← استلام ← فاتورة ← صرف، وفاتورة ← قبض ← ذمم. والمرتجعُ والإشعار
     **فرعان عن السلسلتين لا خطوتان فيهما**: لا يُبلَغان إلا بمستندٍ
     مُرحَّلٍ سبقهما، وإقحامُهما خامسةً في الشريط يُعلّم أن كل شراءٍ ينتهي
     بمرتجع. والتبرير كاملاً في
     `ADR-after-posting-is-a-group-and-a-reversal-is-not-a-delete`. */
  {
    path: "/ledger/entry",
    labelKey: "accounting.ledger.nav.entry",
    section: "accounting",
    icon: "pen",
    group: "postings",
  },
  {
    path: "/ledger/purchase-return",
    labelKey: "accounting.ledger.nav.purchaseReturn",
    section: "accounting",
    icon: "undo",
    group: "postings",
  },
  {
    path: "/ledger/credit-note",
    labelKey: "accounting.ledger.nav.creditNote",
    section: "accounting",
    icon: "receipt",
    group: "postings",
  },
  {
    path: "/ledger/chain",
    labelKey: "accounting.ledger.nav.chain",
    section: "accounting",
    icon: "link",
    group: "postings",
  },
  /* ── الإدارة والاشتراك — أربعٌ **بترتيب العمل لا بترتيب الحروف**: كيف
     أدخل أوّل مرّة ← ما الذي بيدي الآن ← من يدخل معي ← ماذا اشتريتُ وما
     الذي يعمل. وهي **كتلةٌ واحدة متّصلة** كي يندمج جانباها آلياً حين يلمس
     أسطولٌ آخر هذا الملفّ.

     **ولا قسمٌ سادس، ولا مجموعةٌ ثالثة.** عقد الملاحة خماسيّ وهو مقفل
     (ADR-0069)، والمجموعتان المُسمّاتان مبرَّرتان بأن العقد المنشور يضع
     نيّاتهما في `"section": "Accounting"` — ولا نيّة واحدة لهذه الأربع
     أصلاً، فليس لها في العقد قسمٌ تُنسب إليه. فهي هنا تحت المحاسبة **لأجل
     لونها وحده** — وهو اللون المرجعي حين لا يُعرف القسم — كما `/sign-in`
     و`/design` و`/voice` قبلها. **وفصلُها عن العمل اليومي يقع في الملاحة
     نفسها**: عنوانٌ ثانٍ في `App.tsx` وشريطٌ خاصّ بها، لا صفٌّ سادس هنا. */
  {
    path: "/admin/enrolment",
    labelKey: "app.nav.enrolment",
    section: "accounting",
    icon: "plus",
    group: "admin",
  },
  {
    path: "/admin/session",
    labelKey: "app.nav.mySession",
    section: "accounting",
    icon: "key",
    group: "admin",
  },
  {
    path: "/admin/members",
    labelKey: "app.nav.members",
    section: "accounting",
    icon: "people",
    group: "admin",
  },
  {
    path: "/admin/subscription",
    labelKey: "app.nav.subscription",
    section: "accounting",
    icon: "card",
    group: "admin",
  },
  {
    path: "/admin/plans",
    labelKey: "app.nav.plans",
    section: "accounting",
    icon: "layers",
    group: "admin",
  },
  /* ── التأسيس والثوابت — أربعٌ **بترتيب العمل لا بترتيب الحروف**: ما يقع
     مرّةً فيؤسّس المنشأة ← ما يُبوَّب عليه كلُّ سطرٍ بعده ← ما يُرخَّص من حقول
     المستندات ← ما يقبل السطر أصلاً. وهي **كتلةٌ واحدة متّصلة** كي يندمج
     جانباها آلياً حين يلمس أسطولٌ آخر هذا الملفّ.

     **وهي في القسم المحاسبي لأنها محاسبية لا لأجل لونه وحده**: مركز التكلفة
     بُعدُ تبويبٍ على سطر القيد، ودليلُ الحسابات دليلُ الدفتر، وشكلُ المستند
     ما يقبله الدفتر منه. و`CostCenter` يعيش في `CompanySetup` في العقد
     المنشور، ولا مخطّط في المقاولات ولا في العقارات يحمل حقل مركز تكلفة —
     فبيتُها شاشةُ تأسيسٍ محاسبيّة (ADR-0080 §7، ثمّ ADR-جديد).

     **ولا مجموعةٌ ثالثة ولا قسمٌ سادس**: `group` مقصورةٌ على المبيعات
     والمشتريات لأن العقد يضع نيّاتهما في `"section": "Accounting"`، ولا
     نيّة لهذه الأربع أصلاً. وفصلُها عن العمل اليومي يقع في الملاحة نفسها:
     عنوانٌ ثانٍ في `App.tsx` وشريطٌ خاصّ بها. */
  {
    path: "/setup",
    labelKey: "app.nav.companySetup",
    section: "accounting",
    icon: "building",
    group: "setup",
  },
  {
    path: "/setup/cost-centers",
    labelKey: "app.nav.costCenters",
    section: "accounting",
    icon: "target",
    group: "setup",
  },
  {
    path: "/setup/document-shapes",
    labelKey: "app.nav.documentShapes",
    section: "accounting",
    icon: "file",
    group: "setup",
  },
  {
    path: "/setup/chart-of-accounts",
    labelKey: "app.nav.chartOfAccounts",
    section: "accounting",
    icon: "tree",
    group: "setup",
  },
  {
    path: "/setup/parameters",
    labelKey: "app.nav.parameters",
    section: "accounting",
    icon: "sliders",
    group: "setup",
  },
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
