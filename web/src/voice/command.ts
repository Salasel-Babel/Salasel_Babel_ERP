/* ═══════════════════════════════════════════════════════════════════════════
   قارئ الأمر المنطوق في المتصفّح — حتمي، بلا شبكة، وبلا نموذج.
   ───────────────────────────────────────────────────────────────────────────
   نظيرُ `SpokenCommandReader` في الخادم، **ويقرأ معه ملفّ متجهاتٍ واحداً**
   (ADR-0030 خامساً): تنفيذان بملفَّي متجهات ينحرفان، ولا يُكتشف انحرافهما إلا
   على شاشة صاحب المصلحة.

   ولماذا في المتصفّح أصلاً: لأن هذا المسار هو **ما يعمل حين لا يعمل شيء** —
   في مستودعٍ بلا تغطية، وعلى موقع صبٍّ بلا شبكة. والخادم يُنفّذ ما يُؤكَّد، لا
   ما يُفهَم.

   ⚠ وما يُنتجه هذا الملفّ **ليس إذناً**. الإذن من `authorise` وحدها، ولا تمرّ
   عمليةٌ تُغيّر الحال بلا تأكيدٍ صريح — بلا استثناء.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readArabicNumber, strip } from "./arabic-number";
import { intentById, VOICE_INTENTS, type VoiceIntent, type VoiceSlot } from "./catalogue";

/** مصدر القيمة — نظير FieldProvenance في الخادم، بالأسماء نفسها. */
export type Provenance = "attested" | "read" | "inferred" | "defaulted" | "typed" | "spoken";

/** قيمةُ شريحةٍ كما خرجت من الكلام. نصّ دائماً — المال نصّ والكمّية نصّ. */
export interface SpokenSlotValue {
  readonly name: string;
  readonly text: string;
  /** رمز الوحدة حين تكون الشريحة كمّية. */
  readonly unit?: string;
  /** المقطع من الكلام الذي أنتج القيمة — يُعرض كي يرى الإنسان **لماذا**. */
  readonly heard: string;
  readonly provenance: Provenance;
}

/**
 * **قراءةُ شريحةٍ واحدة — مجموعةٌ مغلقة ليس فيها «تابع».**
 *
 * نظيرُ `SlotReading` في الخادم حالةً بحالة. و`pending` ليست «واصلْ البحث» بل
 * **«هذا المقطع نهائيّ، وعلى السجلّ أن يجيب عنه مرّةً واحدة»**. وكلُّ `switch`
 * عليها يُغلَق بفرع `never`، فصنفٌ يُضاف يُحمِّر الترجمة ولا يسقط صامتاً إلى «مرّ».
 */
export type SlotReading =
  | { readonly kind: "silent" }
  | { readonly kind: "filled"; readonly value: SpokenSlotValue }
  | { readonly kind: "pending"; readonly slot: string; readonly span: string; readonly registerKey: string }
  | { readonly kind: "resolved"; readonly slot: string; readonly handle: string; readonly span: string }
  | { readonly kind: "asked"; readonly slot: string; readonly questionId: string; readonly span: string }
  | { readonly kind: "refused"; readonly code: string };

/** ما فهمه المحرّك من جملةٍ واحدة. **وليس إذناً بالتنفيذ.** */
export interface VoiceResolution {
  readonly intent: VoiceIntent;
  /** قراءةٌ واحدة لكل شريحةٍ معلَنة — **دائماً**، حتى للصامتة. */
  readonly readings: Readonly<Record<string, SlotReading>>;
  readonly slots: readonly SpokenSlotValue[];
  readonly missingSlots: readonly string[];
  /** رموز أعطالٍ وقعت أثناء القراءة — تُعرض ولا تُبتلع. */
  readonly faults: readonly string[];
  /** الأطراف التي لم تُحلّ بعد — تُعرض، **ويُمنع التأكيد ما دامت**. */
  readonly pending: readonly string[];
  /**
   * هل نُطق دليلُ شركة؟ **والدليل هو الإشارة لا الاسمُ المُحلَّل**: تحليلُ الاسم
   * ثم مقارنتُه بتساوي نصّين حكمٌ على الهوية بالتخمين، وقد حُذف.
   */
  readonly companyCueHeard: boolean;
  /**
   * الملخّص المرتدّ — يُقرأ ويُعرض معاً. **وواحدٌ بالعربية لا اثنان**: الملخّص نصّ
   * عرض، والعربية سجلُّه، ولغةٌ ثالثة صفٌّ لا عمود (ADR-0021 · القاعدة 14).
   */
  readonly readbackAr: string;
  /** صورةٌ نصّية حتمية للأمر — تأكيدٌ برمزٍ آخر يُرفض. */
  readonly confirmationToken: string;
}

/** نتيجة القراءة: نيّةٌ مفهومة، أو رفضٌ برمز. */
export type VoiceReading =
  | { readonly ok: true; readonly resolution: VoiceResolution }
  | { readonly ok: false; readonly codes: readonly string[]; readonly detail?: string };

/** ما يُحقن كي تكون القراءة حتمية. لا ساعةَ جهازٍ داخل المحرّك. */
export interface VoiceReadingOptions {
  readonly today?: string;
  readonly statutoryTaxRate?: string;
}

/** مَن يتكلّم، وفي أي منشأة، وبأي صلاحيات. **ولا اسمَ شركةٍ فيه**: لا يُقارَن نصٌّ بنصّ. */
export interface VoiceCaller {
  readonly companyId: string;
  readonly permittedIntentIds: readonly string[];
}

/**
 * قيمةُ شريحةٍ في أمرٍ اجتاز البوابة — **والطرف فيها مِقبضٌ لا اسم**.
 * لا `text` ولا `heard` على حالة الكِيان: فلا يجد بانٍ لجسم مسوّدةٍ اسماً منطوقاً
 * يضعه في موضع معرّف، **لأنه غير موجود**.
 */
export interface ResolvedSlotValue {
  readonly name: string;
  readonly text: string | null;
  readonly unit: string | null;
  readonly handle: string | null;
  readonly provenance: Provenance;
}

/** أمرٌ اجتاز البوابة. */
export interface VoiceDispatch {
  readonly intent: VoiceIntent;
  readonly slots: readonly ResolvedSlotValue[];
  readonly companyId: string;
  readonly confirmedByHuman: boolean;
}

/** نتيجة البوابة. */
export type VoiceAuthorisation =
  | { readonly ok: true; readonly dispatch: VoiceDispatch }
  | { readonly ok: false; readonly codes: readonly string[] };

/** أقصى طول تفريغ يُقبل — نفس حدّ الخادم. */
export const TRANSCRIPT_LIMIT = 600;

/** أقصى عدد كلماتٍ في رمزٍ منطوق. */
const CODE_WORD_LIMIT = 4;

/** ما يُقال بعد الملخّص لكل عمليةٍ تُغيّر الحال — نفس نصّ الخادم حرفاً. */
export const CONFIRM_CALL_AR = "قل «تأكيد» أو اضغط زرّ التأكيد.";

/** كلمات التأكيد المنطوقة — قائمة مغلقة. */
export const CONFIRM_WORDS_AR: readonly string[] = ["تأكيد", "أكّد", "اعتمد", "تمام", "نعم"];

/** كلمات الإلغاء المنطوقة. */
export const CANCEL_WORDS_AR: readonly string[] = ["إلغاء", "ألغِ", "لا", "تراجع"];

/** وسمُ الطرف الذي طابق صفّاً واحداً في السجلّ — نفس نصّ الخادم حرفاً. */
export const FROM_YOUR_REGISTER_AR = "من سجلّك";

/** وسمُ الطرف الذي لم يُحلّ بعد. */
export const NOT_RESOLVED_YET_AR = "لم يُحلّ بعد";

/** وسمُ الطرف الذي وُرِقت له ورقةُ سؤال. **ولا يُقال كم كان المرشّحون.** */
export const WHICH_ONE_AR = "أيّهم تقصد؟";

/* ── التجريد على مرتبتين: أمينٌ يُعرَض، ومطويٌّ يُطابَق ──────────────────── */

/** التجريد للمطابقة وحده — يضيف التاء المربوطة إلى الهاء. **لا يُعرض**. */
export function fold(text: string): string {
  return strip(text ?? "").replace(/ة/g, "ه");
}

function words(text: string): string[] {
  return (text ?? "")
    .split(/[\s،,.؟?!؛;:]+/)
    .map((word) => strip(word))
    .filter((word) => word.length > 0);
}

function same(left: string, right: string): boolean {
  return fold(left) === fold(right);
}

const STOP_WORDS = new Set(
  [
    "و", "في", "على", "من", "الى", "عن", "مع", "ثم",
    "بمبلغ", "مبلغ", "بقيمة", "قيمتها", "قيمته", "الاجمالي", "اجمالي", "المجموع",
    "ريال", "ريالا", "ريالات", "ريالين", "هللة",
    "بتاريخ", "تاريخ", "اليوم", "امس", "البارحة", "أمس",
    "رقم", "رقمها", "برقم", "كمية", "الكمية", "عدد", "العدد",
    "ضريبة", "الضريبة", "وضريبة", "بالمئة", "بالمائة", "المئة", "المائة",
    "تأكيد", "الغاء", "إلغاء",
  ].map(fold)
);

const PERCENT_WORDS = new Set(["بالمئة", "بالمائة", "المئة", "المائة", "٪", "%"].map(fold));
const CONNECTORS = new Set(["رقم", "برقم", "رقمها", "هو", "هي"].map(fold));
const TODAY_WORD = fold("اليوم");
const YESTERDAY_WORDS = new Set(["امس", "أمس", "البارحة"].map(fold));
/** دلائل الشركة — **مغلقة ومركّبة**، ووجودُها هو الإشارة: لا يُحلَّل ما بعدها. */
const COMPANY_CUES = ["في شركة", "بشركة", "لشركة", "في منشأة", "بمنشأة", "لمنشأة", "على شركة"];

/** هل نُطق دليلُ شركة؟ **ولا يُقرأ ما بعده** — تحليلُ الاسم هو ما حُذف. */
function companyCueHeard(tokens: readonly string[]): boolean {
  for (const cue of COMPANY_CUES) {
    const parts = words(cue);
    for (let index = 0; index + parts.length <= tokens.length; index++) {
      let hit = true;
      for (let offset = 0; offset < parts.length; offset++) {
        if (!same(tokens[index + offset] ?? "", parts[offset] ?? "")) {
          hit = false;
          break;
        }
      }
      if (hit) return true;
    }
  }
  return false;
}

/**
 * معجم الوحدات المنطوقة — مغلق، ونظير `VoiceUnits` في الخادم مدخلاً بمدخل.
 * ولا وحدةَ افتراضية: غيابُها رفضٌ مُسمّى لا سقوطٌ إلى وحدة الأساس.
 */
const UNITS: readonly (readonly [string, string])[] = [
  ["حبة", "EA"], ["حبات", "EA"], ["حبه", "EA"], ["قطعة", "EA"], ["قطع", "EA"], ["وحدة", "EA"],
  ["علبة", "BOX"], ["علب", "BOX"], ["صندوق", "BOX"], ["صناديق", "BOX"],
  ["كرتون", "CTN"], ["كراتين", "CTN"], ["كرتونة", "CTN"],
  ["كيس", "BAG"], ["أكياس", "BAG"], ["شوال", "BAG"],
  ["طبلية", "PAL"], ["بالتة", "PAL"], ["بالت", "PAL"],
  ["كيلو", "KG"], ["كيلوجرام", "KG"], ["كجم", "KG"],
  ["طن", "TON"], ["أطنان", "TON"],
  ["لتر", "L"], ["لترات", "L"],
  ["متر", "M"], ["أمتار", "M"],
  ["متر مربع", "M2"], ["مربع", "M2"],
  ["متر مكعب", "M3"], ["مكعب", "M3"], ["مكعبة", "M3"],
  ["يوم", "DAY"], ["أيام", "DAY"],
  ["ساعة", "HR"], ["ساعات", "HR"],
  ["لفة", "ROL"], ["رول", "ROL"],
];

const UNIT_LEXICON = new Map(UNITS.map(([word, code]) => [fold(word), code]));

/** رمز الوحدة، أو لا شيء. */
export function unitCodeOf(word: string): string | null {
  return UNIT_LEXICON.get(fold(word ?? "")) ?? null;
}

/** رمز وحدةٍ من كلمتين — «متر مكعب» تبدأ بـ«متر»، وأخذُ الأولى وحدها يخسر مرتبتين. */
function unitCodeOfPair(first: string, second: string): string | null {
  return UNIT_LEXICON.get(fold(first ?? "") + " " + fold(second ?? "")) ?? null;
}

/* ── المطابقة ───────────────────────────────────────────────────────────── */

/**
 * يطابق نيّةً واحدة. الأطول يفوز كي لا تبتلع عبارةٌ عامّة عبارةً أخصّ، و**تعادل
 * نيّتين رفضٌ لا قرعة**.
 * @param transcript التفريغ.
 */
export function matchIntent(transcript: string): VoiceReading | VoiceIntent {
  const folded = fold(transcript ?? "");
  let best = 0;
  let winners: VoiceIntent[] = [];

  for (const intent of VOICE_INTENTS) {
    let score = 0;
    for (const phrase of intent.phrases) {
      const needle = fold(phrase);
      if (needle.length > score && folded.includes(needle)) score = needle.length;
    }
    if (score === 0) continue;
    if (score > best) {
      best = score;
      winners = [intent];
    } else if (score === best) {
      winners.push(intent);
    }
  }

  if (winners.length === 0) return { ok: false, codes: ["ai.voice.intent_not_understood"], detail: transcript };
  if (winners.length > 1) {
    return { ok: false, codes: ["ai.voice.intent_ambiguous"], detail: winners.map((w) => w.id).join(" · ") };
  }
  return winners[0] as VoiceIntent;
}

/* ── الشرائح ────────────────────────────────────────────────────────────── */

function boundariesOf(intent: VoiceIntent): Set<string> {
  const out = new Set(STOP_WORDS);
  for (const slot of intent.slots) {
    for (const cue of slot.cues) for (const word of words(cue)) out.add(fold(word));
  }
  return out;
}

/** مواضع ما بعد كل دليل — **كلُّها لا أوّلُها**: «على ستة أقساط» فيها دليلان. */
function cuePositions(slot: VoiceSlot, tokens: readonly string[]): number[] {
  const found: number[] = [];
  for (const cue of slot.cues) {
    const parts = words(cue);
    if (parts.length === 0) continue;
    for (let index = 0; index + parts.length <= tokens.length; index++) {
      let hit = true;
      for (let offset = 0; offset < parts.length; offset++) {
        if (!same(tokens[index + offset] ?? "", parts[offset] ?? "")) {
          hit = false;
          break;
        }
      }
      if (hit) found.push(index + parts.length);
    }
  }
  return found;
}

function isNumberish(word: string): boolean {
  return readArabicNumber(word).ok;
}

function numberSpan(tokens: readonly string[], from: number): { text: string; next: number } | null {
  let end = from;
  while (end < tokens.length && (isNumberish(tokens[end] ?? "") || tokens[end] === "و")) end++;
  while (end > from && tokens[end - 1] === "و") end--;
  if (end === from) return null;
  return { text: tokens.slice(from, end).join(" "), next: end };
}

/* القسمة على مئة نصّاً: النسبة كسر، و«خمسة عشر بالمئة» = 0.15 لا 15. */
function divideByHundred(text: string): string {
  const negative = text.startsWith("-");
  const body = negative ? text.slice(1) : text;
  const dot = body.indexOf(".");
  const digits = body.replace(".", "");
  const fracLength = (dot < 0 ? 0 : body.length - dot - 1) + 2;
  const padded = digits.padStart(fracLength + 1, "0");
  const cut = padded.length - fracLength;
  const out = padded.slice(0, cut) + "." + padded.slice(cut);
  return (negative ? "-" : "") + out.replace(/(\.\d*?)0+$/, "$1").replace(/\.$/, "");
}

/* تشذيب الصفر العشري الزائد كي يطابق "0.####" في الخادم حرفاً بحرف. */
function trim(text: string): string {
  return text.includes(".") ? text.replace(/0+$/, "").replace(/\.$/, "") : text;
}

function readNumeric(slot: VoiceSlot, tokens: readonly string[]): SpokenSlotValue | null {
  for (const at of cuePositions(slot, tokens)) {
    const span = numberSpan(tokens, at);
    if (!span) continue;
    const reading = readArabicNumber(span.text);
    if (!reading.ok) continue;
    const after = tokens[span.next];
    const text = after && PERCENT_WORDS.has(fold(after)) ? divideByHundred(reading.text) : trim(reading.text);
    return { name: slot.name, text, heard: span.text, provenance: "spoken" };
  }
  return null;
}

function readQuantity(slot: VoiceSlot, tokens: readonly string[]): SlotReading {
  /* **مرورانِ لا مرورٌ واحد بحالةٍ تعبر `continue`** — نظير الخادم حرفاً: كان الشكل
     «سجّل ما سُمع ثم تابعْ» وهو الشكل نفسه الذي جعل رفضَ مقطعٍ سبباً في قبول آخر. */
  for (const at of cuePositions(slot, tokens)) {
    const span = numberSpan(tokens, at);
    if (!span) continue;
    const reading = readArabicNumber(span.text);
    if (!reading.ok) continue;

    const first = tokens[span.next];
    const second = tokens[span.next + 1];
    let unit = first && second ? unitCodeOfPair(first, second) : null;
    const width = unit ? 2 : 1;
    if (!unit && first) unit = unitCodeOf(first);
    if (!unit) continue;

    return {
      kind: "filled",
      value: {
        name: slot.name,
        text: trim(reading.text),
        unit,
        heard: span.text + " " + tokens.slice(span.next, span.next + width).join(" "),
        provenance: "spoken",
      },
    };
  }

  for (const at of cuePositions(slot, tokens)) {
    const span = numberSpan(tokens, at);
    if (span && readArabicNumber(span.text).ok) return { kind: "refused", code: "ai.voice.unit_missing" };
  }

  return { kind: "silent" };
}

function shiftDays(iso: string, days: number): string {
  const parts = iso.split("-").map(Number);
  const date = new Date(Date.UTC(parts[0] ?? 1970, (parts[1] ?? 1) - 1, (parts[2] ?? 1) + days));
  return date.toISOString().slice(0, 10);
}

function readDate(slot: VoiceSlot, tokens: readonly string[], options: VoiceReadingOptions): SpokenSlotValue | null {
  for (const word of tokens) {
    if (options.today && fold(word) === TODAY_WORD) {
      return { name: slot.name, text: options.today, heard: word, provenance: "spoken" };
    }
    if (options.today && YESTERDAY_WORDS.has(fold(word))) {
      return { name: slot.name, text: shiftDays(options.today, -1), heard: word, provenance: "spoken" };
    }
    if (/^\d{4}-\d{2}-\d{2}$/.test(word)) {
      return { name: slot.name, text: word, heard: word, provenance: "spoken" };
    }
  }
  /* بلا حقنٍ لتاريخ اليوم **لا يُملأ الحقل إطلاقاً** — لا ساعةَ جهازٍ هنا. */
  return options.today ? { name: slot.name, text: options.today, heard: "", provenance: "defaulted" } : null;
}

function readChoice(slot: VoiceSlot, tokens: readonly string[]): SpokenSlotValue | null {
  for (const choice of slot.choices) {
    for (const word of tokens) {
      if (same(word, choice)) return { name: slot.name, text: choice, heard: word, provenance: "spoken" };
    }
  }
  return null;
}

function readCode(slot: VoiceSlot, tokens: readonly string[], boundaries: Set<string>): SpokenSlotValue | null {
  for (const start of cuePositions(slot, tokens)) {
    let at = start;
    while (at < tokens.length && CONNECTORS.has(fold(tokens[at] ?? ""))) at++;

    const parts: string[] = [];
    for (let index = at; index < tokens.length && parts.length < CODE_WORD_LIMIT; index++) {
      const word = tokens[index] ?? "";
      if (boundaries.has(fold(word)) || unitCodeOf(word)) break;
      parts.push(word);
    }
    if (parts.length > 0) {
      const text = parts.join(" ");
      return { name: slot.name, text, heard: text, provenance: "spoken" };
    }
  }
  return null;
}

/**
 * نصٌّ حرّ **لا يسمّي أحداً**: «بيان القيد» و«سبب التغيير».
 * ويحتفظ بالقاعدة التقريبية **عمداً وبالتباين مُسمّى**: بيانٌ خاطئ سطرٌ تجميليّ،
 * وعميلٌ خاطئ **طرفٌ آخر** على مستندٍ يُرحَّل.
 */
function readProse(slot: VoiceSlot, tokens: readonly string[], boundaries: Set<string>): SpokenSlotValue | null {
  for (const at of cuePositions(slot, tokens)) {
    const parts: string[] = [];
    for (let index = at; index < tokens.length; index++) {
      const word = tokens[index] ?? "";
      if (boundaries.has(fold(word)) || unitCodeOf(word) || isNumberish(word)) break;
      parts.push(word);
    }
    if (parts.length > 0) {
      const text = parts.join(" ");
      return { name: slot.name, text, heard: text, provenance: "spoken" };
    }
  }
  return null;
}

/** كلماتُ دلائل شريحةٍ واحدة، مطويّة. */
function cueWords(slot: VoiceSlot): Set<string> {
  const out = new Set<string>();
  for (const cue of slot.cues) for (const word of words(cue)) out.add(fold(word));
  return out;
}

/** دلائلُ الشرائح الأخرى **عباراتٍ كاملة** — لا كلماتٍ مبعثرة. */
function foreignCues(intent: VoiceIntent, slot: VoiceSlot): string[][] {
  const out: string[][] = [];
  for (const other of intent.slots) {
    if (other.name === slot.name) continue;
    for (const cue of other.cues) {
      const parts = words(cue).map(fold);
      if (parts.length > 0) out.push(parts);
    }
  }
  return out;
}

function terminates(tokens: readonly string[], at: number, foreign: readonly string[][]): boolean {
  const word = tokens[at] ?? "";
  if (isNumberish(word) || unitCodeOf(word)) return true;

  for (const phrase of foreign) {
    if (at + phrase.length > tokens.length) continue;
    let hit = true;
    for (let offset = 0; offset < phrase.length; offset++) {
      if (fold(tokens[at + offset] ?? "") !== phrase[offset]) {
        hit = false;
        break;
      }
    }
    if (hit) return true;
  }
  return false;
}

/**
 * **مقطعُ الطرف — قاعدةٌ كُلّية لا تستطيع أن ترفض ولا أن تختار بين مقطعين.**
 * نظيرُ `SpokenSpans.Locate` في الخادم: الأبكرُ **بموضع الكلمة** لا بترتيب الإعلان،
 * ثم تخطّي دلائل الشريحة نفسها في الرأس، ثم الجمع حتى **بدايةِ حقلٍ آخر**.
 * ودليلُ الشريحة نفسها **ليس حدّاً** — وتلك الطيّةُ بعينها كانت تقطع اسم العميل.
 */
function locateSpan(slot: VoiceSlot, intent: VoiceIntent, tokens: readonly string[]): string | null {
  const positions = cuePositions(slot, tokens);
  if (positions.length === 0) return null;

  const own = cueWords(slot);
  const foreign = foreignCues(intent, slot);

  /* **بترتيب ورود الكلمات لا بترتيب إعلان الدلائل** — نظير الخادم. وأوّل موضعٍ
     يُنتج مقطعاً يفوز، **ولا يُقارَن مقطعُه بمقطعِ موضعٍ آخر** بطولٍ ولا بتشابه. */
  for (const position of [...new Set(positions)].sort((a, b) => a - b)) {
    let start = position;
    while (start < tokens.length && own.has(fold(tokens[start] ?? ""))) start++;

    let end = start;
    while (end < tokens.length && !terminates(tokens, end, foreign)) end++;

    if (end > start) return tokens.slice(start, end).join(" ");
  }

  return null;
}

/* ── الملخّص المرتدّ ─────────────────────────────────────────────────────── */

/** الملخّص العربي — **نصٌّ واحد يُقرأ ويُعرض معاً**، فلا يسمع الأعمى غير ما يرى الأصمّ. */
export function readbackArabic(intent: VoiceIntent, slots: readonly SpokenSlotValue[]): string {
  const parts = slots.map((value) => {
    const slot = intent.slots.find((candidate) => candidate.name === value.name);
    const label = slot ? slot.nameAr : value.name;
    const unit = value.unit ? " " + value.unit : "";
    const source = value.provenance === "defaulted" ? " (من الإعدادات)" : "";
    return label + ": " + value.text + unit + source;
  });
  const head = intent.nameAr + " — " + (parts.length === 0 ? "بلا شرائح" : parts.join("، ")) + ".";
  return intent.requiresConfirmation ? head + " " + CONFIRM_CALL_AR : head;
}

/** رمز التأكيد: صورةٌ نصّية مرتَّبة للأمر. الترتيب باسم الشريحة لا بترتيب الكلام. */
export function confirmationToken(intent: VoiceIntent, slots: readonly SpokenSlotValue[]): string {
  const ordered = [...slots]
    .sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))
    .map((value) => value.name + "=" + value.text + (value.unit ? ":" + value.unit : ""));
  return intent.id + "|" + ordered.join(";");
}

/* ── حارس الإفشاء ───────────────────────────────────────────────────────── */

const IDENTITY_SHAPE = /(?<![0-9])[0-9]{9,}(?![0-9])/;
const IBAN_SHAPE = /(?<![A-Za-z0-9])SA[0-9]{22}(?![0-9])/i;

/** يُقنّع قيمةً شخصية بالقاعدة نفسها التي تُقنّع بها وحدة الموارد البشرية. */
export function maskPersonal(value: string | null | undefined): string {
  return value && value.length > 4 ? "••••" + value.slice(-4) : "••••";
}

/** هل يحمل هذا النصّ قيمةً شخصية غير مُقنَّعة؟ الصوت يُسمَع في الغرفة كلّها. */
export function disclosureFault(text: string): string | null {
  return IBAN_SHAPE.test(text) || IDENTITY_SHAPE.test(text) ? "ai.voice.masked_read_required" : null;
}

/* ── القراءة ────────────────────────────────────────────────────────────── */

/**
 * يقرأ جملةً واحدة أمراً واحداً.
 * @param transcript التفريغ كما ورد.
 * @param options ما يُحقن كي تكون القراءة حتمية.
 */
export function readCommand(transcript: string, options: VoiceReadingOptions = {}): VoiceReading {
  if (!transcript || transcript.trim().length === 0) {
    return { ok: false, codes: ["ai.voice.transcript_empty"] };
  }
  if (transcript.length > TRANSCRIPT_LIMIT) {
    return { ok: false, codes: ["ai.voice.transcript_too_long"] };
  }

  const matched = matchIntent(transcript);
  if ("ok" in matched) return matched;

  const intent = matched;
  const tokens = words(transcript);
  const boundaries = boundariesOf(intent);

  /* **قراءةٌ واحدة لكل شريحة، ولا تُكتب مرّتين.** المفتاح اسمُ الشريحة، والسجلّ
     في الخادم يرفض تكرار الاسم عند البناء — فطمسُ رفضٍ سابق غير قابل للتعبير. */
  const readings: Record<string, SlotReading> = {};

  for (const slot of intent.slots) {
    if (Object.prototype.hasOwnProperty.call(readings, slot.name)) {
      return { ok: false, codes: ["ai.voice.slot_declared_twice"], detail: slot.name };
    }
    readings[slot.name] = readSlot(slot, intent, tokens, boundaries, options);
  }

  const slots: SpokenSlotValue[] = [];
  const missingSlots: string[] = [];
  const faults: string[] = [];
  const pending: string[] = [];

  for (const slot of intent.slots) {
    const reading = readings[slot.name] as SlotReading;
    switch (reading.kind) {
      case "filled":
        slots.push(reading.value);
        break;
      case "pending":
      case "asked":
        pending.push(slot.name);
        break;
      case "refused":
        faults.push(reading.code);
        break;
      case "silent":
        if (slot.required) missingSlots.push(slot.name);
        break;
      case "resolved":
        break;
      default: {
        /* فرعُ الاستحالة: صنفٌ يُضاف يُحمِّر الترجمة ولا يسقط صامتاً إلى «مرّ». */
        const never: never = reading;
        return { ok: false, codes: ["ai.voice.unknown_reading"], detail: String(never) };
      }
    }
  }

  const readbackAr = readbackFromReadings(intent, readings);
  const leak = disclosureFault(readbackAr);
  if (leak) return { ok: false, codes: [leak] };

  return {
    ok: true,
    resolution: {
      intent,
      readings,
      slots,
      missingSlots,
      faults,
      pending,
      companyCueHeard: companyCueHeard(tokens),
      readbackAr,
      confirmationToken: tokenFromReadings(intent, readings),
    },
  };
}

/** قراءةُ شريحةٍ واحدة — **وتُعيد حالةً من مجموعةٍ مغلقة، لا قيمةً أو لا شيء**. */
function readSlot(
  slot: VoiceSlot,
  intent: VoiceIntent,
  tokens: readonly string[],
  boundaries: Set<string>,
  options: VoiceReadingOptions
): SlotReading {
  if (slot.kind === "Entity") {
    const span = locateSpan(slot, intent, tokens);
    if (span === null) return { kind: "silent" };
    /* مفتاح السجلّ مضمونٌ عند بناء السجلّ في الخادم، ومرآتُه تحمله. */
    return { kind: "pending", slot: slot.name, span, registerKey: slot.registerKey ?? "" };
  }

  if (slot.kind === "Quantity") return readQuantity(slot, tokens);

  let value: SpokenSlotValue | null;
  switch (slot.kind) {
    case "Money":
    case "Number":
      value = readNumeric(slot, tokens);
      break;
    case "Date":
      value = readDate(slot, tokens, options);
      break;
    case "Choice":
      value = readChoice(slot, tokens);
      break;
    case "Code":
      value = readCode(slot, tokens, boundaries);
      break;
    case "Prose":
      value = readProse(slot, tokens, boundaries);
      break;
    default: {
      const never: never = slot.kind;
      throw new Error("صنفُ شريحةٍ خارج المفردات المغلقة: " + String(never));
    }
  }

  return value === null ? { kind: "silent" } : { kind: "filled", value };
}

/** الملخّص من القراءات — **والأطراف تُوسَم ولا تُخفى**. */
export function readbackFromReadings(
  intent: VoiceIntent,
  readings: Readonly<Record<string, SlotReading>>
): string {
  const parts: string[] = [];

  for (const slot of intent.slots) {
    const reading = readings[slot.name];
    if (!reading) continue;

    if (reading.kind === "filled") {
      const unit = reading.value.unit ? " " + reading.value.unit : "";
      const source = reading.value.provenance === "defaulted" ? " (من الإعدادات)" : "";
      parts.push(slot.nameAr + ": " + reading.value.text + unit + source);
    } else if (reading.kind === "resolved") {
      parts.push(slot.nameAr + ": " + reading.span + " (" + FROM_YOUR_REGISTER_AR + ")");
    } else if (reading.kind === "pending") {
      parts.push(slot.nameAr + ": " + reading.span + " (" + NOT_RESOLVED_YET_AR + ")");
    } else if (reading.kind === "asked") {
      parts.push(slot.nameAr + ": " + reading.span + " (" + WHICH_ONE_AR + ")");
    }
  }

  const head = intent.nameAr + " — " + (parts.length === 0 ? "بلا شرائح" : parts.join("، ")) + ".";
  return intent.requiresConfirmation ? head + " " + CONFIRM_CALL_AR : head;
}

/**
 * رمز التأكيد من القراءات. **وحلُّ شريحةٍ يغيّره** — فتأكيدٌ قيل بينما كان السؤال
 * مفتوحاً يُرفض، وهو الصواب: تأكيدٌ على أمرٍ لم يكن قد اكتمل ليس تأكيداً.
 */
export function tokenFromReadings(
  intent: VoiceIntent,
  readings: Readonly<Record<string, SlotReading>>
): string {
  const ordered: string[] = [];

  for (const name of Object.keys(readings).sort()) {
    const reading = readings[name] as SlotReading;
    if (reading.kind === "filled") {
      ordered.push(name + "=" + reading.value.text + (reading.value.unit ? ":" + reading.value.unit : ""));
    } else if (reading.kind === "resolved") {
      ordered.push(name + "@" + reading.handle);
    } else if (reading.kind === "pending") {
      ordered.push(name + "?" + reading.span);
    } else if (reading.kind === "asked") {
      ordered.push(name + "!" + reading.questionId);
    }
  }

  return intent.id + "|" + ordered.join(";");
}

/* ── الحلّ: من مقطعٍ معلَّق إلى مِقبضٍ أو رفضٍ مُسمّى ───────────────────────── */

/**
 * جوابُ السجلّ عن مقطعٍ واحد — **ثلاث حالات لا رابع لها**، بأسماء `NameLookupOutcome`
 * في الخادم. ولا عدد، ولا درجة، ولا مرشّح، ولا «أفضل تطابق».
 */
export type NameAnswer =
  | { readonly outcome: "resolved"; readonly handle: string }
  | { readonly outcome: "needs_question"; readonly questionId: string }
  | { readonly outcome: "none" };

/**
 * **يطبّق أجوبة السجلّ على القراءات — مرّةً واحدة لكل شريحة.**
 *
 * نظيرُ `SpokenNameResolver` في الخادم. وتُبنى مجموعةٌ **جديدة** مدخلاً لكل شريحة،
 * ولا تُكتب شريحةٌ مرّتين: فلا يوجد شكلٌ يُكتب فيه «إن رُفض فجرّب مقطعاً آخر» —
 * المقطع مفردٌ، والجواب مفرد، والمدخل يُقفل.
 *
 * @param resolution ما قرأه القارئ.
 * @param answers أجوبة السجلّ بمفاتيح أسماء الشرائح — وما لم يُجَب عنه يبقى معلَّقاً.
 */
export function applyNameAnswers(
  resolution: VoiceResolution,
  answers: Readonly<Record<string, NameAnswer>>
): VoiceResolution {
  const answered: Record<string, SlotReading> = {};

  for (const slot of resolution.intent.slots) {
    const reading = resolution.readings[slot.name] as SlotReading;

    if (reading.kind !== "pending") {
      answered[slot.name] = reading;
      continue;
    }

    const answer = answers[slot.name];
    if (!answer) {
      answered[slot.name] = reading;
      continue;
    }

    switch (answer.outcome) {
      case "resolved":
        answered[slot.name] = {
          kind: "resolved",
          slot: slot.name,
          handle: answer.handle,
          span: reading.span,
        };
        break;
      case "needs_question":
        answered[slot.name] = {
          kind: "asked",
          slot: slot.name,
          questionId: answer.questionId,
          span: reading.span,
        };
        break;
      case "none":
        answered[slot.name] = { kind: "refused", code: "ai.voice.name_not_in_register" };
        break;
      default: {
        const never: never = answer;
        answered[slot.name] = { kind: "refused", code: "ai.voice.unknown_answer:" + String(never) };
        break;
      }
    }
  }

  const slots: SpokenSlotValue[] = [];
  const missingSlots: string[] = [];
  const faults: string[] = [];
  const pending: string[] = [];

  for (const slot of resolution.intent.slots) {
    const reading = answered[slot.name] as SlotReading;
    if (reading.kind === "filled") slots.push(reading.value);
    else if (reading.kind === "pending" || reading.kind === "asked") pending.push(slot.name);
    else if (reading.kind === "refused") faults.push(reading.code);
    else if (reading.kind === "silent" && slot.required) missingSlots.push(slot.name);
  }

  return {
    intent: resolution.intent,
    readings: answered,
    slots,
    missingSlots,
    faults,
    pending,
    companyCueHeard: resolution.companyCueHeard,
    readbackAr: readbackFromReadings(resolution.intent, answered),
    confirmationToken: tokenFromReadings(resolution.intent, answered),
  };
}

/* ── البوابة ────────────────────────────────────────────────────────────── */

/** هل هذه الجملة تأكيدٌ منطوق؟ القائمة مغلقة ولا تُقارَب بأقرب شبيه. */
export function isSpokenConfirmation(utterance: string): boolean {
  const tokens = words(utterance ?? "");
  return CONFIRM_WORDS_AR.some((word) => tokens.some((spoken) => same(spoken, word)));
}

/** هل هي إلغاء؟ */
export function isSpokenCancellation(utterance: string): boolean {
  const tokens = words(utterance ?? "");
  return CANCEL_WORDS_AR.some((word) => tokens.some((spoken) => same(spoken, word)));
}

/**
 * يأذن — أو يرفض ويسمّي، **وبكل الأسباب لا أوّلها**.
 * والترتيب مقصود: الصلاحية، ثم القرار المعلَّق، ثم النقص، ثم الشركة، ثم التأكيد.
 * @param resolution ما فهمه القارئ.
 * @param caller المتكلّم ومنشأته وصلاحياته.
 * @param token رمز التأكيد كما عاد من الإنسان، أو لا شيء.
 */
export function authorise(
  resolution: VoiceResolution,
  caller: VoiceCaller,
  token: string | null
): VoiceAuthorisation {
  const intent = resolution.intent;

  if (!caller.permittedIntentIds.includes(intent.id)) {
    return { ok: false, codes: ["ai.voice.not_permitted"] };
  }
  if (intent.status === "AwaitingOwnerDecision") {
    return { ok: false, codes: ["ai.voice.owner_decision_pending"] };
  }

  const codes: string[] = [];
  const slots: ResolvedSlotValue[] = [];

  /* **فرعٌ واحد لكل حالة، ومجموعةٌ مغلقة** — فصنفٌ يُضاف يُحمِّر الترجمة. */
  for (const slot of intent.slots) {
    const reading = resolution.readings[slot.name] as SlotReading;
    switch (reading.kind) {
      case "filled":
        slots.push({
          name: slot.name,
          text: reading.value.text,
          unit: reading.value.unit ?? null,
          handle: null,
          provenance: reading.value.provenance,
        });
        break;
      case "resolved":
        slots.push({ name: slot.name, text: null, unit: null, handle: reading.handle, provenance: "spoken" });
        break;
      /* **طرفٌ معلَّق لا يمرّ** — وهو الفرق كلّه: كان المقطع يُمرَّر نصّاً فيصير طرفَ المستند. */
      case "pending":
        codes.push("ai.voice.name_unresolved");
        break;
      case "asked":
        codes.push("ai.voice.name_needs_question");
        break;
      case "refused":
        codes.push(reading.code);
        break;
      case "silent":
        if (slot.required) codes.push("ai.voice.slot_missing");
        break;
      default: {
        const never: never = reading;
        codes.push("ai.voice.unknown_reading:" + String(never));
        break;
      }
    }
  }

  if (resolution.companyCueHeard) codes.push("ai.voice.company_not_switched");

  if (intent.requiresConfirmation) {
    if (token === null) codes.push("ai.voice.confirmation_required");
    else if (token !== resolution.confirmationToken) codes.push("ai.voice.confirmation_mismatch");
  }

  if (codes.length > 0) return { ok: false, codes };

  return {
    ok: true,
    dispatch: {
      intent,
      slots,
      companyId: caller.companyId,
      confirmedByHuman: intent.requiresConfirmation,
    },
  };
}

/** النيّة بمعرّفها — يُعاد تصديرها كي لا يستورد المستهلك ملفَّين. */
export { intentById };
