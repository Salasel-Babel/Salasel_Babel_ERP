/* ═══════════════════════════════════════════════════════════════════════════
   تسليم المسوّدة — من اللوحة إلى شاشة المستند.
   ───────────────────────────────────────────────────────────────────────────
   كانت اللوحة تنتهي عند نداءٍ خارجٍ (`onDispatch`) لا يعرف أحدٌ ما يفعله به. وهذه
   الطبقة تُغلق ذلك: الأمرُ المؤكَّد يصير **تسليمَ مسوّدة** يحمل — بلا زيادة —
   **معرّف العملية المنشورة** وقيمَ الشرائح ومصادرَها. ثم تلتقطه شاشةُ المستند
   وتملأ به نموذجها، ويُراجعه إنسان بيده، **وهي — لا هذه الطبقة — تملك زرّ الترحيل**.

   ⚠ **ولماذا لا تنادي هذه الطبقة الباب بنفسها.** أجسامُ المسوّدات المنشورة تطلب
   **معرّفات**: `customerId` و`invoiceId` و`receiptLineId` و`itemId`. والصوت يحمل
   **أسماء**: «مؤسسة الرياض» و«اسمنت مقاوم». وتحويلُ اسمٍ منطوق إلى معرّفٍ بالتخمين
   هو بالضبط ما يمنعه انضباط هذا المستودع — والخطأ فيه يُنشئ مستنداً على **عميلٍ آخر**
   صحيحَ الشكل. فالحلّ الوحيد الأمين: الشاشةُ تملك القوائم والمُنتقِيات، وهي التي
   تحلّ الاسم إلى معرّف **أمام عين الإنسان**، ثم تنادي العملية.

   ⚠ **ولا يُذكر هنا اسمُ عمليةِ ترحيلٍ واحدة.** معرّف العملية يأتي من النيّة كما
   أعلنتها الوحدة المالكة، وحارسٌ في الخادم يقرأ ملفّات هذا المجلّد كلّها ويطابقها
   بالعقد المنشور: اسمُ عمليةٍ تبدأ بـ`post` — أو توقيعٌ أو اعتماد — يُحمِّر بوّابة.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { VoiceDispatch, SpokenSlotValue } from "./command";

/** حقلٌ في المسوّدة كما وصل من الكلام: اسمُه العربي، وقيمتُه، **ومصدرُه**. */
export interface VoiceDraftField {
  /** مفتاح الشريحة — تعتمد عليه الشاشة لتملأ حقلها. */
  readonly name: string;
  /** الاسم العربي كما يُقرأ على المستخدم — وهو السجلّ لا ترجمتُه. */
  readonly nameAr: string;
  /** القيمة **نصّاً دائماً**. المال نصّ، ولا عائمة في هذا المسار كلّه. */
  readonly text: string;
  /** رمز الوحدة حين تكون الشريحة كمّية — وكمّيةٌ بلا وحدة لا تصل إلى هنا أصلاً. */
  readonly unit: string | null;
  /** المصدر: منطوق، أو من الإعدادات. ولا قيمة بلا مصدر. */
  readonly provenance: string;
  /** المقطع من الكلام الذي أنتج القيمة — يُعرض كي يرى الإنسان **لماذا**. */
  readonly heard: string;
}

/** مسوّدةٌ مؤكَّدة، جاهزةٌ لتُسلَّم إلى شاشة مستندها. */
export interface VoiceDraftHandoff {
  readonly intentId: string;
  /** اسم النيّة بالعربية — يُعرض ويُنطَق. */
  readonly nameAr: string;
  /** القسم الذي يراه المستخدم. */
  readonly section: string;
  /**
   * **العملية المنشورة التي تُنشئ المسوّدة.** تُنادى من الشاشة لا من هنا.
   * ولا تكون عمليةَ ترحيلٍ أبداً — يقيسه حارسٌ على العقد المنشور.
   */
  readonly operationId: string;
  /** المنشأة كما جاءت من الجلسة — لا تُكتب بيد ولا تُنطَق. */
  readonly companyId: string;
  /** الحقول الممتلئة. */
  readonly fields: readonly VoiceDraftField[];
}

function fieldOf(value: SpokenSlotValue, nameAr: string): VoiceDraftField {
  return {
    name: value.name,
    nameAr,
    text: value.text,
    unit: value.unit ?? null,
    provenance: value.provenance,
    heard: value.heard ?? "",
  };
}

/**
 * يبني التسليم من أمرٍ اجتاز البوابة.
 * @param dispatch الأمر المؤكَّد.
 * @returns التسليم، أو `null` للنيّة التي تنتظر قراراً — فلا عملية لها تُنادى.
 */
export function handoffOf(dispatch: VoiceDispatch): VoiceDraftHandoff | null {
  const operationId = dispatch.intent.operationId;
  if (operationId === null) return null;

  const nameOf = (name: string): string =>
    dispatch.intent.slots.find((slot) => slot.name === name)?.nameAr ?? name;

  return {
    intentId: dispatch.intent.id,
    nameAr: dispatch.intent.nameAr,
    section: dispatch.intent.section,
    operationId,
    companyId: dispatch.companyId,
    fields: dispatch.slots.map((value) => fieldOf(value, nameOf(value.name))),
  };
}

/* ── الحافظة بين الشاشتين ──────────────────────────────────────────────────
   **خانةٌ واحدة تُستهلَك عند القراءة.** ومسوّدةٌ تبقى بعد أن قُرئت تُملأ في نموذجٍ
   ثانٍ بلا أن يقولها أحد — وهو أخبث ما يمكن أن يفعله تسليمٌ صامت.

   وهي **في الذاكرة لا في مخزن المتصفّح**: مسوّدةٌ تنجو من إعادة تحميل الصفحة
   تظهر بعد يومٍ في نموذجٍ لا يعرف صاحبُه من أين جاءت. وإعادةُ التحميل تعني
   إعادةَ القول — وهو أرخص من مسوّدةٍ شبح. */
let held: VoiceDraftHandoff | null = null;
const listeners = new Set<() => void>();

function announce(): void {
  for (const listener of listeners) listener();
}

/** يودع المسوّدة كي تلتقطها شاشةُ المستند. */
export function stashVoiceDraft(handoff: VoiceDraftHandoff): void {
  held = handoff;
  announce();
}

/** يقرأ المسوّدة بلا استهلاك — للعرض وحده. */
export function peekVoiceDraft(): VoiceDraftHandoff | null {
  return held;
}

/**
 * يأخذ المسوّدة **ويستهلكها**.
 * @param intentId حين يُمرَّر، لا تُؤخَذ إلا مسوّدةُ هذه النيّة بعينها.
 */
export function takeVoiceDraft(intentId?: string): VoiceDraftHandoff | null {
  if (held === null) return null;
  if (intentId !== undefined && held.intentId !== intentId) return null;
  const taken = held;
  held = null;
  announce();
  return taken;
}

/** يُفرِغ الحافظة — عند الإلغاء، وعند مغادرة اللوحة. */
export function dropVoiceDraft(): void {
  if (held === null) return;
  held = null;
  announce();
}

/**
 * يشترك في تغيّر الحافظة — كي تُعيد الشاشةُ رسمَ نفسها حين تصلها مسوّدة.
 * @param listener ما يُنادى عند كل تغيّر.
 * @returns دالّةٌ تفكّ الاشتراك.
 */
export function subscribeVoiceDraft(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
