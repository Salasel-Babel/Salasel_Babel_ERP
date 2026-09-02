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
import type { VoiceSlot } from "./catalogue";

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
  /**
   * **هل يجب أن تُحلّ هذه القيمة إلى معرّف صفٍّ واحد قبل أن تغادر الشاشة؟**
   *
   * الطبقة الأولى (حكمُ المقطع في القارئ) مرشّحٌ رخيص يحوّل أعلى الضجيج إلى رفضٍ
   * عند الميكروفون، **وله ثقبٌ لا يُسدّ بلا معجم**: ثلاث كلمات عربية غير مشكولة
   * لا يفصل بينها اسماً وبين اسمٍ يتبعه ذيلُ إسنادٍ قصيرٍ أيُّ قاعدةٍ إملائية.
   *
   * وهذه الطبقة هي المناعة **البنيوية**: القيمة المنطوقة **اسم**، والشاشة تملك
   * القوائم والمُنتقِيات فتحلّه إلى **معرّف** أمام عين الإنسان. و«شركة المسار
   * الامثل وانشئ لها حسابا» **لا يطابق صفّاً واحداً**، و«مؤسسة الرياض» يطابق
   * واحداً بعينه. فالمجموعةُ المغلقة التي لا يُتحايَل عليها بتسميةِ ما لم يخطر
   * لأحد هي **صفوف المنشأة نفسها** لا قائمةُ كلماتٍ في شيفرة.
   *
   * وصفرُ مطابقاتٍ أو أكثرُ من واحدة **رفضٌ مُسمّى** — لا طرفٌ نصّيّ حرّ، ولا
   * «أنشئ جديداً» مملوءاً سلفاً بما نُطق.
   */
  readonly requiresResolution: boolean;
}

/**
 * **شرائح النصّ الحرّ — القائمة المغلقة الوحيدة هنا، وقطبُها آمن.**
 *
 * ما ليس فيها من شرائح `Text` **يلزمه حلٌّ إلى معرّف**. أي أن شريحةً جديدة تُضاف
 * غداً تبدأ **مطلوبةَ الحلّ افتراضاً**: النسيان يُنتج تشدّداً لا تساهلاً — وذلك
 * قطبُ القائمة الصحيح، وهو ما يفرق بين مجموعةٍ مغلقة تملكها وقائمةِ محظوراتٍ تعدّها.
 */
export const FREE_TEXT_SLOTS: readonly string[] = ["description", "reason"];

/**
 * هل تلزم هذه الشريحة حلّاً إلى معرّف صفّ؟
 * @param slot الشريحة كما أعلنتها الوحدة المالكة.
 */
export function requiresResolution(slot: VoiceSlot): boolean {
  return slot.kind === "Text" && !FREE_TEXT_SLOTS.includes(slot.name);
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

function fieldOf(value: SpokenSlotValue, nameAr: string, needsResolution: boolean): VoiceDraftField {
  return {
    name: value.name,
    nameAr,
    text: value.text,
    unit: value.unit ?? null,
    provenance: value.provenance,
    heard: value.heard ?? "",
    requiresResolution: needsResolution,
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

  const slotOf = (name: string): VoiceSlot | undefined =>
    dispatch.intent.slots.find((slot) => slot.name === name);

  return {
    intentId: dispatch.intent.id,
    nameAr: dispatch.intent.nameAr,
    section: dispatch.intent.section,
    operationId,
    companyId: dispatch.companyId,
    /* شريحةٌ لا يعرفها السجلّ **تلزمها الحلّ**: النسيان يُنتج تشدّداً لا تساهلاً. */
    fields: dispatch.slots.map((value) => {
      const slot = slotOf(value.name);
      return fieldOf(value, slot?.nameAr ?? value.name, slot ? requiresResolution(slot) : true);
    }),
  };
}

/**
 * الحقول التي **لا يجوز أن تغادر الشاشة نصّاً**: على الشاشة أن تحلّ كلاً منها إلى
 * معرّف صفٍّ واحد بمُنتقٍ أمام الإنسان، وأن ترفض باسمه عند صفر مطابقاتٍ أو أكثر من واحدة.
 * @param handoff التسليم.
 */
export function fieldsAwaitingResolution(handoff: VoiceDraftHandoff): readonly VoiceDraftField[] {
  return handoff.fields.filter((field) => field.requiresResolution);
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
