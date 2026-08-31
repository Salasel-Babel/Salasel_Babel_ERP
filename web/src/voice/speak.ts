/* ═══════════════════════════════════════════════════════════════════════════
   النُّطق — الطرف الثاني من التأكيد.
   ───────────────────────────────────────────────────────────────────────────
   الملخّص المرتدّ يجب أن يصل **مستخدمين لا يتقاسمان حاسّة**: من لا يرى يسمعه،
   ومن لا يسمع يقرؤه. ولذلك النصّ **واحد** يُمرَّر إلى هذا الملفّ وإلى الشاشة
   معاً — نصّان ينحرفان، فيؤكّد كلٌّ منهما ما لم يؤكّده الآخر.

   ⚠ **والنُّطق زينة لا شرط.** متصفّحٌ بلا `speechSynthesis` — أو مستخدمٌ أطفأ
   الصوت — يبقى قادراً على إتمام العملية كاملة: القراءة المرتدّة معروضة، وزرّ
   التأكيد حقيقي، والرفض مكتوب. ودالّةٌ تعيد `false` هنا **لا تمنع شيئاً**.

   ولا يُنطَق ما لم يمرّ بحارس الإفشاء: الشاشة تُرى بزاوية، والصوت يُسمَع في
   الغرفة كلّها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { disclosureFault } from "./command";

/** لماذا لم يُنطَق النصّ. كل حالة مُسمّاة، ولا حالة «لا أدري». */
export type SpeakRefusal =
  | "unsupported" /* المتصفّح لا يحمل تركيب الكلام */
  | "empty" /* لا نصّ */
  | "masked-read-required"; /* النصّ يحمل قيمةً شخصية غير مُقنَّعة */

/** نتيجة محاولة النطق. */
export type SpeakOutcome = { readonly ok: true } | { readonly ok: false; readonly reason: SpeakRefusal };

interface SynthesisLike {
  speak(utterance: unknown): void;
  cancel(): void;
}

function synthesis(): SynthesisLike | null {
  const scope = globalThis as unknown as { speechSynthesis?: SynthesisLike };
  return scope.speechSynthesis ?? null;
}

function utteranceConstructor(): (new (text: string) => { lang: string; rate: number }) | null {
  const scope = globalThis as unknown as {
    SpeechSynthesisUtterance?: new (text: string) => { lang: string; rate: number };
  };
  return scope.SpeechSynthesisUtterance ?? null;
}

/** هل يستطيع هذا المتصفّح أن ينطق؟ يُسأل قبل الوعد لا بعده. */
export function canSpeak(): boolean {
  return synthesis() !== null && utteranceConstructor() !== null;
}

/**
 * ينطق نصّاً بالعربية. **يمرّ بحارس الإفشاء أولاً**، ويعيد سبباً مُسمّى حين يمتنع.
 * @param text النصّ — وهو **نفسه** المعروض على الشاشة.
 * @param lang لغة النطق.
 */
export function speak(text: string, lang = "ar-SA"): SpeakOutcome {
  if (!text || text.trim().length === 0) return { ok: false, reason: "empty" };
  if (disclosureFault(text)) return { ok: false, reason: "masked-read-required" };

  const engine = synthesis();
  const Utterance = utteranceConstructor();
  if (!engine || !Utterance) return { ok: false, reason: "unsupported" };

  const utterance = new Utterance(text);
  utterance.lang = lang;
  /* أبطأ قليلاً من الافتراضي: الملخّص يحمل أرقاماً، والرقم المنطوق بسرعة يُسمع رقماً آخر. */
  utterance.rate = 0.95;
  engine.cancel();
  engine.speak(utterance);
  return { ok: true };
}

/** يُسكت ما يُنطَق الآن. يُستدعى عند الإلغاء وعند مغادرة اللوحة. */
export function hush(): void {
  synthesis()?.cancel();
}
