/* ═══════════════════════════════════════════════════════════════════════════
   فهرس نظام التصميم — العقد مقروءاً بالآلة
   ───────────────────────────────────────────────────────────────────────────
   صفحة `/design` **تُبنى من هذا الفهرس** ولا تكرّره. والسبب عملي لا جمالي:
   فهرسٌ يُكتب مرّتين — مرّةً في الرموز ومرّةً في الصفحة التي تعرضها — ينحرف
   عند أول إضافة، فتُعرض على المالك لوحةٌ ليست لوحة المنتج. ومن يضيف رمزاً
   يضيف صفّاً هنا، فيظهر في الصفحة وحده.
   ═══════════════════════════════════════════════════════════════════════════ */

/** رمزُ لونٍ في اللوحة، بدوره لا بشكله. */
export interface PaletteEntry {
  /** اسم الرمز كما يُكتب في CSS. */
  readonly token: string;
  /** مفتاح الدور في طبقة اللغة. */
  readonly roleKey: string;
  /** أين يُستعمل: خلفية أم نصّ أم حدّ. */
  readonly kind: "surface" | "ink" | "edge";
}

/** لوحة الألوان مسمّاةً بأدوارها — وهي مُثبتة، تُورَث ولا تُخترع. */
export const PALETTE: readonly PaletteEntry[] = [
  { token: "--surface-ground", roleKey: "screen.design.role.ground", kind: "surface" },
  { token: "--surface-base", roleKey: "screen.design.role.base", kind: "surface" },
  { token: "--surface-raised", roleKey: "screen.design.role.raised", kind: "surface" },
  { token: "--surface-inset", roleKey: "screen.design.role.inset", kind: "surface" },
  { token: "--surface-overlay", roleKey: "screen.design.role.overlay", kind: "surface" },
  { token: "--edge-line", roleKey: "screen.design.role.edgeLine", kind: "edge" },
  { token: "--edge-strong", roleKey: "screen.design.role.edgeStrong", kind: "edge" },
  { token: "--color-text", roleKey: "screen.design.role.text", kind: "ink" },
  { token: "--color-text-muted", roleKey: "screen.design.role.textMuted", kind: "ink" },
  { token: "--color-text-subtle", roleKey: "screen.design.role.textSubtle", kind: "ink" },
  { token: "--color-primary", roleKey: "screen.design.role.brand", kind: "ink" },
  { token: "--color-success", roleKey: "screen.design.role.good", kind: "ink" },
  { token: "--color-warning", roleKey: "screen.design.role.warn", kind: "ink" },
  { token: "--color-danger", roleKey: "screen.design.role.bad", kind: "ink" },
  { token: "--color-debit", roleKey: "screen.design.role.debit", kind: "ink" },
  { token: "--color-credit", roleKey: "screen.design.role.credit", kind: "ink" },
  { token: "--color-ai", roleKey: "screen.design.role.ai", kind: "ink" },
  { token: "--section-accounting", roleKey: "app.section.accounting", kind: "ink" },
  { token: "--section-inventory", roleKey: "app.section.inventory", kind: "ink" },
  { token: "--section-hr", roleKey: "app.section.hr", kind: "ink" },
  { token: "--section-contracting", roleKey: "app.section.contracting", kind: "ink" },
  { token: "--section-realestate", roleKey: "app.section.realestate", kind: "ink" },
];

/** الأضواء — ولكلٍّ منها معلومةٌ يحملها، لا شكلٌ يُعجب. */
export const GLOWS: readonly PaletteEntry[] = [
  { token: "--glow-arrival", roleKey: "screen.design.motion.arriveWhen", kind: "edge" },
  { token: "--glow-posted", roleKey: "screen.design.motion.postWhen", kind: "edge" },
  { token: "--glow-refusal", roleKey: "screen.design.motion.refuseWhen", kind: "edge" },
  { token: "--glow-inferred", roleKey: "screen.design.motion.inferWhen", kind: "edge" },
  { token: "--glow-focus", roleKey: "screen.design.role.focus", kind: "edge" },
];

/** مفردةُ حركةٍ معروضةً: اسمها، ومتى تُستعمل، ومدّتها ومنحناها. */
export interface MotionEntry {
  /** اسم المفردة في {@link MOTION}. */
  readonly name: "arrive" | "post" | "refuse" | "infer" | "reveal" | "transit" | "live" | "scan";
  readonly titleKey: string;
  readonly whenKey: string;
  /** رمز المدّة والمنحنى، كما يُكتبان في CSS. */
  readonly duration: string;
  readonly ease: string;
}

/** مفردات الحركة — قائمةٌ مغلقة، ولكلٍّ منها متى تُستعمل. */
export const MOTIONS: readonly MotionEntry[] = [
  {
    name: "arrive",
    titleKey: "screen.design.motion.arrive",
    whenKey: "screen.design.motion.arriveWhen",
    duration: "--motion-dwell",
    ease: "--ease-enter",
  },
  {
    name: "post",
    titleKey: "screen.design.motion.post",
    whenKey: "screen.design.motion.postWhen",
    duration: "--motion-weighty",
    ease: "--ease-settle",
  },
  {
    name: "refuse",
    titleKey: "screen.design.motion.refuse",
    whenKey: "screen.design.motion.refuseWhen",
    duration: "--motion-deliberate",
    ease: "--ease-refuse",
  },
  {
    name: "infer",
    titleKey: "screen.design.motion.infer",
    whenKey: "screen.design.motion.inferWhen",
    duration: "--motion-deliberate",
    ease: "--ease-enter",
  },
  {
    name: "reveal",
    titleKey: "screen.design.motion.reveal",
    whenKey: "screen.design.motion.revealWhen",
    duration: "--motion-deliberate",
    ease: "--ease-enter",
  },
  {
    name: "transit",
    titleKey: "screen.design.motion.transit",
    whenKey: "screen.design.motion.transitWhen",
    duration: "--motion-cinematic",
    ease: "--ease-enter",
  },
  {
    name: "live",
    titleKey: "screen.design.motion.live",
    whenKey: "screen.design.motion.liveWhen",
    duration: "--motion-dwell",
    ease: "--ease-enter",
  },
  {
    name: "scan",
    titleKey: "screen.design.motion.scan",
    whenKey: "screen.design.motion.scanWhen",
    duration: "--motion-dwell",
    ease: "--ease-enter",
  },
];

/** المصادر الستّة، بمفاتيح أسمائها القائمة في طبقة الصوت. */
export const PROVENANCES = [
  "typed",
  "attested",
  "read",
  "spoken",
  "inferred",
  "defaulted",
] as const;

/** حالات المستند الستّ، بمفاتيح أسمائها القائمة في `acct.status.*`. */
export const DOC_STATES = [
  "draft",
  "posted",
  "reversed",
  "pending",
  "rejected",
  "archived",
] as const;

/* ═══════════════════════════════ الكمّية ووحدتها · quantity with its unit
   أُضيفت إلى الفهرس مع أوّليّة `QuantityValue`: الطبقة كانت تحمل عرضاً
   للمال (`<Amount>`) ولا تحمل عرضاً للكمّية، فكان كلّ قسمٍ سيخترع واحداً.
   ومفاتيح تسمياتها تحت `inventory.design.*` لأن الكمّية بوحدتها مفردةُ القسم
   المخزني، والفهرس يعرض ما هو موجود لا ما يملكه هو. */

/** عيّنةُ كمّيةٍ في الفهرس: مقدارٌ **نصّاً** ووحدةٌ معه، وتسميةُ ما تُظهره. */
export interface QuantitySample {
  /** مفتاح العيّنة — للفهرسة لا للعرض. */
  readonly key: string;
  /** المقدار كما يصل على السلك، بمقياسه الكامل. */
  readonly magnitude: string;
  /** رمز الوحدة كما يسجّله المستأجر — معرّفٌ لا يُترجَم. */
  readonly unit: string;
  /** مفتاح ما تُظهره هذه العيّنة. */
  readonly labelKey: string;
}

/** ثلاث عيّنات: صحيحٌ تُقصّ أصفاره، وكسرٌ يبقى، وسالبٌ يُوسَم ولا يُخفى. */
export const QUANTITY_SAMPLES: readonly QuantitySample[] = [
  { key: "whole", magnitude: "1440.000000", unit: "PCS", labelKey: "inventory.design.whole" },
  { key: "fraction", magnitude: "12.500000", unit: "KG", labelKey: "inventory.design.fraction" },
  { key: "negative", magnitude: "-6.000000", unit: "حبة", labelKey: "inventory.design.negative" },
];
