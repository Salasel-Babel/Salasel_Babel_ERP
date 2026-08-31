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
    path: null,
    built: false,
  },
  {
    id: "hr",
    labelKey: "app.section.hr",
    tint: "var(--section-hr)",
    path: null,
    built: false,
  },
  {
    id: "contracting",
    labelKey: "app.section.contracting",
    tint: "var(--section-contracting)",
    path: null,
    built: false,
  },
  {
    id: "realestate",
    labelKey: "app.section.realestate",
    tint: "var(--section-realestate)",
    path: null,
    built: false,
  },
];

/** الشاشات المبنيّة داخل القسم المحاسبي — وهي ما تفتحه لوحة الأوامر. */
export interface ScreenEntry {
  readonly path: string;
  readonly labelKey: string;
  readonly section: Section["id"];
}

/** كل شاشةٍ مبنيّة، بمسارها ومفتاح اسمها. */
export const SCREENS: readonly ScreenEntry[] = [
  { path: "/", labelKey: "app.nav.trialBalance", section: "accounting" },
  { path: "/voucher", labelKey: "app.nav.voucher", section: "accounting" },
  { path: "/sign-in", labelKey: "app.nav.signIn", section: "accounting" },
  { path: "/contract", labelKey: "app.nav.contract", section: "accounting" },
  { path: "/design", labelKey: "app.nav.design", section: "accounting" },
  /* الأمر المنطوق يعبر الأقسام الخمسة كلّها، ولا قسمَ واحداً يملكه. وهو مُدرَجٌ
     هنا تحت المحاسبة **لأجل لونه وحده** — وهو اللون المرجعي حين لا يُعرَف القسم —
     ولا يُقرأ ذلك ادّعاءً بأن الأقسام الأربعة الأخرى بُنيت: `built` عندها ما زال
     false، وهذه الشاشة تعرض نيّاتها ولا تفتح شاشاتها. */
  { path: "/voice", labelKey: "app.nav.voice", section: "accounting" },
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
