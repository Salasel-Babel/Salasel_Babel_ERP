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

/** ما فهمه المحرّك من جملةٍ واحدة. **وليس إذناً بالتنفيذ.** */
export interface VoiceResolution {
  readonly intent: VoiceIntent;
  readonly slots: readonly SpokenSlotValue[];
  readonly missingSlots: readonly string[];
  /** رموز أعطالٍ وقعت أثناء القراءة — تُعرض ولا تُبتلع. */
  readonly faults: readonly string[];
  readonly spokenCompany: string | null;
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

/** مَن يتكلّم، وفي أي منشأة، وبأي صلاحيات. */
export interface VoiceCaller {
  readonly companyId: string;
  readonly companyNameAr: string;
  readonly permittedIntentIds: readonly string[];
}

/** أمرٌ اجتاز البوابة. */
export interface VoiceDispatch {
  readonly intent: VoiceIntent;
  readonly slots: readonly SpokenSlotValue[];
  readonly companyId: string;
  readonly confirmedByHuman: boolean;
}

/** نتيجة البوابة. */
export type VoiceAuthorisation =
  | { readonly ok: true; readonly dispatch: VoiceDispatch }
  | { readonly ok: false; readonly codes: readonly string[] };

/** أقصى طول تفريغ يُقبل — نفس حدّ الخادم. */
export const TRANSCRIPT_LIMIT = 600;

/**
 * أقصى عدد كلماتٍ في اسمٍ منطوق — **مشتقٌّ من ملفّ المتجهات لا مختار**.
 * يقيسه اختبارٌ يعيد حسابه من `voice-intents.v1.json` ويحمرّ إن خالف. فالرقم
 * **بيانات مملوكة** لا عدداً سحرياً: يتحرّك حين يُضاف متجهٌ يحتاجه، وذلك فرقٌ يُراجَع.
 */
export const NAME_WORD_LIMIT = 3;

/** أقصى عدد كلماتٍ في رمزٍ منطوق — مشتقٌّ من المتجهات كذلك. */
export const CODE_WORD_LIMIT = 2;

/** ما يُقال بعد الملخّص لكل عمليةٍ تُغيّر الحال — نفس نصّ الخادم حرفاً. */
export const CONFIRM_CALL_AR = "قل «تأكيد» أو اضغط زرّ التأكيد.";

/** كلمات التأكيد المنطوقة — قائمة مغلقة. */
export const CONFIRM_WORDS_AR: readonly string[] = ["تأكيد", "أكّد", "اعتمد", "تمام", "نعم"];

/** كلمات الإلغاء المنطوقة. */
export const CANCEL_WORDS_AR: readonly string[] = ["إلغاء", "ألغِ", "لا", "تراجع"];

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
const COMPANY_CUES = ["في شركة", "بشركة", "لشركة", "في منشأة", "بمنشأة", "لمنشأة", "على شركة"];

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

/* ── حكمٌ على المقطع: يُقبل كاملاً أو يُرفض باسمه — ولا يُبتَر ─────────────
   **القاعدة**: مقطعُ نصٍّ أو رمزٍ لا يُسلَّم إلى مستند إلا إذا استطاع القارئ أن
   **يبرّره كلَّه**. وما لا يُبرَّر **يُرفض برمزٍ مُسمّى، ولا يُقصّ إلى الجزء الذي
   يعجبه**. وبترُ «شركة المسار الامثل وانشئ لها حسابا» إلى «شركة المسار» يُنتج
   **عميلاً خاطئاً معقولاً** — وهو بالضبط الضرر الذي وُجد هذا المستودع ليمنعه.
   والرفض يكلّف دورةً واحدة.

   ولماذا **حكمان مغلقان** لا قائمةُ كلماتٍ فاصلة: قائمةُ الفواصل قائمةٌ **مفتوحة**
   يهزمها أول بندٍ لم يخطر لكاتبها
   (docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy).
   وهذان الحكمان يقيسان **المقطع نفسه**: عددَ كلماته مقابل حدٍّ مشتقٍّ من المتجهات،
   وشكلَ كلماته مقابل **المجموعة النحوية الكاملة** للضمائر المتّصلة. */

/**
 * الضمائر المتّصلة متعدّدة الحروف — **المجموعة النحوية كاملةً لا عيّنةً منها**.
 * والاسم العلم لا يحمل ضميراً متّصلاً؛ والفعلُ بمفعوله يحمله. فهذا يلتقط أسرة
 * «فعل + مفعول» كلَّها **بالشكل لا بالقائمة**: «سجلها» و«لقيتها» و«وحولها»
 * و«راجعهم» — ومنها صيغٌ لا تُطابقها قائمةُ كلماتٍ كاملةٍ أبداً.
 *
 * **وما أُخرج عمداً**: الهاء المفردة (تصطدم بـ«وجه» و«فقه» وبالتاء المربوطة
 * المطويّة)، والكاف المفردة، و«نا» (تصطدم بأسماء حقيقية: رنا، دينا، لينا، سنا).
 */
const OBJECT_CLITICS: readonly string[] = ["هما", "كما", "ها", "هم", "هن", "كم", "كن"];

/** أقلّ جذعٍ يبقى بعد نزع الضمير. دونه تُصاب أسماءٌ حقيقية: «مها» و«سها» و«سهم». */
const CLITIC_STEM_FLOOR = 3;

/** سوابق تسبق أداة التعريف: و ف ب ك ل. */
const PROCLITICS = "وفبكل";

/** هل يحمل الرمز أداة التعريف؟ **والأداة لا تجتمع مع فعلٍ تامّ** — فهي تُبرّئ الاسم. */
function carriesDefiniteArticle(token: string): boolean {
  const article = (text: string): boolean => text.startsWith("ال") || text.startsWith("لل");
  if (article(token)) return true;
  return token.length > 3 && PROCLITICS.includes(token.charAt(0)) && article(token.slice(1));
}

/** هل تحمل الكلمة ضميراً متّصلاً مفعولاً؟ يُقاس على **المجرَّد الأمين** لا المطويّ. */
function bearsObjectClitic(word: string): boolean {
  const token = strip(word ?? "");
  if (carriesDefiniteArticle(token)) return false;
  return OBJECT_CLITICS.some(
    (clitic) => token.length - clitic.length >= CLITIC_STEM_FLOOR && token.endsWith(clitic)
  );
}

/** حكمُ المقطع: مقبولٌ، أو مرفوضٌ بسببٍ مُسمّى. */
export type SpanVerdict = "admitted" | "tooManyWords" | "predicationTail";

/**
 * يحكم على مقطعٍ أنتجه القارئ. **لا يُقطّع ولا يُزيح حدّاً**: المقطع كما هو،
 * والحكم عليه كاملاً.
 * @param parts كلمات المقطع.
 * @param limit أقصى عدد كلماتٍ مسموح — مشتقٌّ من المتجهات.
 */
export function adjudicateSpan(parts: readonly string[], limit: number): SpanVerdict {
  if (parts.length > limit) return "tooManyWords";
  if (parts.length >= 2 && parts.some((word) => bearsObjectClitic(word))) return "predicationTail";
  return "admitted";
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

function readQuantity(
  slot: VoiceSlot,
  tokens: readonly string[],
  faults: string[]
): SpokenSlotValue | null {
  let heardWithoutUnit: string | null = null;

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

    if (!unit) {
      heardWithoutUnit ??= span.text;
      continue;
    }

    return {
      name: slot.name,
      text: trim(reading.text),
      unit,
      heard: span.text + " " + tokens.slice(span.next, span.next + width).join(" "),
      provenance: "spoken",
    };
  }

  if (heardWithoutUnit !== null) faults.push("ai.voice.unit_missing");
  return null;
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

/**
 * رمزٌ أو رقمُ مستندٍ أو موقعٍ في رفّ. **ولا بترَ عند الحدّ**: المشي يبلغ الحدّ
 * الطبيعي، ثم يُحكَم على المقطع كلِّه — فبترُ رمزٍ إلى أوّل كلمتين كان يُنتج **رمزاً
 * صحيح الشكل لمستندٍ آخر**، وهو الصورة نفسها من الفساد الصامت.
 */
function readCode(
  slot: VoiceSlot,
  tokens: readonly string[],
  boundaries: Set<string>,
  faults: string[]
): SpokenSlotValue | null {
  for (const start of cuePositions(slot, tokens)) {
    let at = start;
    while (at < tokens.length && CONNECTORS.has(fold(tokens[at] ?? ""))) at++;

    const parts: string[] = [];
    for (let index = at; index < tokens.length; index++) {
      const word = tokens[index] ?? "";
      if (boundaries.has(fold(word)) || unitCodeOf(word)) break;
      parts.push(word);
    }
    if (parts.length === 0) continue;

    if (adjudicateSpan(parts, CODE_WORD_LIMIT) !== "admitted") {
      // **والرفض يُلزِم** — نظيرُ الخادم حرفاً. تخزينُ الرفض والمضيّ إلى الدليل
      // التالي كان يُعيد **طرفاً آخر** بلا عطل: انظر
      // SpokenCommandReader.ReadText و«فخ · الرفض المخزَّن يُستبدَل بطرفٍ آخر».
      faults.push("ai.voice.name_not_bounded");
      return null;
    }

    const text = parts.join(" ");
    return { name: slot.name, text, heard: text, provenance: "spoken" };
  }

  return null;
}

/**
 * نصٌّ حرّ: ما بين الدليل وأول حدّ. **ثم يُحكَم على المقطع كلِّه**: ما لا يُبرَّر
 * يُرفض باسمه وتبقى الشريحة فارغة، ولا يُقصّ إلى الجزء الذي يعجب القارئ.
 */
function readText(
  slot: VoiceSlot,
  tokens: readonly string[],
  boundaries: Set<string>,
  faults: string[]
): SpokenSlotValue | null {
  for (const at of cuePositions(slot, tokens)) {
    const parts: string[] = [];
    for (let index = at; index < tokens.length; index++) {
      const word = tokens[index] ?? "";
      if (boundaries.has(fold(word)) || unitCodeOf(word) || isNumberish(word)) break;
      parts.push(word);
    }
    if (parts.length === 0) continue;

    if (adjudicateSpan(parts, NAME_WORD_LIMIT) !== "admitted") {
      // **والرفض يُلزِم** — نظيرُ الخادم حرفاً. تخزينُ الرفض والمضيّ إلى الدليل
      // التالي كان يُعيد **طرفاً آخر** بلا عطل: انظر
      // SpokenCommandReader.ReadText و«فخ · الرفض المخزَّن يُستبدَل بطرفٍ آخر».
      faults.push("ai.voice.name_not_bounded");
      return null;
    }

    const text = parts.join(" ");
    return { name: slot.name, text, heard: text, provenance: "spoken" };
  }

  return null;
}

/**
 * اسم شركةٍ نُطق داخل الأمر — **ويخضع للحكم نفسه**. ومشيٌ بلا سقف كان يضع كلاماً
 * كاملاً داخل رسالة «الشركة المنطوقة غير المفتوحة»، فتُقرأ الرسالة ولا تُفهَم.
 */
function readCompany(tokens: readonly string[], faults: string[]): string | null {
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
      if (!hit) continue;

      const name: string[] = [];
      for (let at = index + parts.length; at < tokens.length; at++) {
        const word = tokens[at] ?? "";
        if (STOP_WORDS.has(fold(word)) || isNumberish(word)) break;
        name.push(word);
      }
      if (name.length === 0) continue;

      if (adjudicateSpan(name, NAME_WORD_LIMIT) !== "admitted") {
        // والرفض يُلزِم هنا كذلك — للسبب نفسه.
        faults.push("ai.voice.name_not_bounded");
        return null;
      }

      return name.join(" ");
    }
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
  const slots: SpokenSlotValue[] = [];
  const missingSlots: string[] = [];
  const faults: string[] = [];

  for (const slot of intent.slots) {
    let value: SpokenSlotValue | null;
    switch (slot.kind) {
      case "Money":
      case "Number":
        value = readNumeric(slot, tokens);
        break;
      case "Quantity":
        value = readQuantity(slot, tokens, faults);
        break;
      case "Date":
        value = readDate(slot, tokens, options);
        break;
      case "Choice":
        value = readChoice(slot, tokens);
        break;
      case "Code":
        value = readCode(slot, tokens, boundaries, faults);
        break;
      default:
        value = readText(slot, tokens, boundaries, faults);
        break;
    }

    if (value) slots.push(value);
    else if (slot.required) missingSlots.push(slot.name);
  }

  const readbackAr = readbackArabic(intent, slots);
  const leak = disclosureFault(readbackAr);
  if (leak) return { ok: false, codes: [leak] };

  return {
    ok: true,
    resolution: {
      intent,
      slots,
      missingSlots,
      faults,
      spokenCompany: readCompany(tokens, faults),
      readbackAr,
      confirmationToken: confirmationToken(intent, slots),
    },
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
  for (let i = 0; i < resolution.missingSlots.length; i++) codes.push("ai.voice.slot_missing");
  codes.push(...resolution.faults);

  if (resolution.spokenCompany !== null && !same(resolution.spokenCompany, caller.companyNameAr)) {
    codes.push("ai.voice.company_not_switched");
  }

  if (intent.requiresConfirmation) {
    if (token === null) codes.push("ai.voice.confirmation_required");
    else if (token !== resolution.confirmationToken) codes.push("ai.voice.confirmation_mismatch");
  }

  if (codes.length > 0) return { ok: false, codes };

  return {
    ok: true,
    dispatch: {
      intent,
      slots: resolution.slots,
      companyId: caller.companyId,
      confirmedByHuman: intent.requiresConfirmation,
    },
  };
}

/** النيّة بمعرّفها — يُعاد تصديرها كي لا يستورد المستهلك ملفَّين. */
export { intentById };
