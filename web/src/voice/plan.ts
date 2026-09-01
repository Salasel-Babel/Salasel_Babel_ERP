/* ═══════════════════════════════════════════════════════════════════════════
   مُشغّل الخطّة المنطوقة — في المتصفّح، لأن الإنسان هنا.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ **ولماذا هذا الملفّ في `web/src/voice/` بالذات — وهو قرارٌ لا ذوق.** الحارس
   `NoVoiceIntentReachesAPostingOperation` يُعدِّد هذا المجلّد كلَّه
   (`Directory.EnumerateFiles(..., AllDirectories)`) ويمسح كل .ts/.tsx فيه بحثاً عن
   اسم عمليةِ ترحيلٍ منشورة وعن مقطعِ بابِ الترحيل في المسار. فوضعُ المُشغّل هنا يجعله **محروساً
   من يوم كُتب بلا تعديل حارسٍ واحد**؛ ووضعُه في `web/src/app/` كان سيُخرجه من المسح
   بصمت — وهو أخطر ما يمكن أن يقع لملفٍّ يُرتِّب عدّة مستندات.

   ⚠ **ولماذا في المتصفّح لا في الخادم.** الطبقةُ التي تقف وتسأل هي اللوحة، والإنسانُ
   عندها. ولا يوجد في العقد المنشور بابُ صوتٍ واحد؛ فاختراعُ بابٍ يستضيف مُرتِّباً
   يُنشئ **سطحاً في الخادم يُرتّب عدّة مستندات** — وهو نقيضُ الحدّ الأبله المقصود الذي
   يشرحه `handoff.ts`.

   ⚠ **ولا يُنفِّذ هذا الملفّ شيئاً.** لا ينادي باباً، ولا يُرحّل، ولا يملك طريقاً إلى
   أمرٍ مُنفَّذ إلا بأن يسأل `authorise` **لكل خطوة على حدة**. والحارس بنيوي لا انضباطي:
   ليس هنا دالّةٌ تُنتج `VoiceDispatch` بغير البوابة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { intentById, VOICE_PLANS, type VoicePlan, type VoicePlanStep } from "./catalogue";
import {
  authorise,
  fold,
  readCommandInto,
  type SpokenSlotValue,
  type VoiceCaller,
  type VoiceReadingOptions,
  type VoiceResolution,
} from "./command";
import { handoffOf, type VoiceDraftHandoff } from "./handoff";

/**
 * حال خطوةٍ في جريان. **وتُعرض نصّاً لا لوناً**: من لا يميّز الألوان يقرأ الحال
 * كما يقرؤها غيره، والمستودع يقيس تباين الألوان أصلاً (`web/scripts/contrast.mjs`).
 */
export type VoicePlanStepState =
  /** لم يبلغها الدور بعد. */
  | "pending"
  /** مُتخطّاة بشرطها — قال الإنسان إنه وجد ما بحث عنه. */
  | "skipped"
  /** واقفةٌ تسأل عن شريحةٍ لازمة لم يقلها أحد. */
  | "asking"
  /** سُلّمت مسوّدتُها إلى شاشتها، والشاشةُ تملك الالتزام. */
  | "handedOff"
  /** قال الإنسان إنها تمّت. */
  | "done"
  /** تركها الإنسان. */
  | "abandoned"
  /** رُفضت، ومعها أسبابُها بأسمائها. */
  | "refused";

/** خطوةٌ في جريان: تعريفُها، وحالُها، وما قُرئ فيها. */
export interface VoicePlanStepRun {
  readonly step: VoicePlanStep;
  readonly state: VoicePlanStepState;
  /** ما فهمه القارئ لهذه الخطوة — أو `null` قبل أن يبلغها الدور. */
  readonly resolution: VoiceResolution | null;
  /** التسليم بعد أن اجتازت البوابة. */
  readonly handoff: VoiceDraftHandoff | null;
  /** أسبابُ الرفض بأسمائها — تُعرض ولا تُبتلع. */
  readonly refusals: readonly string[];
}

/**
 * جريانُ خطّة. **في الذاكرة، ويموت مع إعادة التحميل** — تماماً كحافظة المسوّدة
 * وللسبب نفسه بقوّةٍ أكبر: خطّةٌ تنجو من إعادة التحميل تُستأنف بعد يومٍ بملخّصٍ
 * سمعه صاحبُها ولم يعد يذكره.
 */
export interface VoicePlanRun {
  readonly plan: VoicePlan;
  /** التفريغ الأوّل كما نُطق — تُقرأ منه كلُّ خطوةٍ شرائحَها. */
  readonly transcript: string;
  readonly steps: readonly VoicePlanStepRun[];
  /** الخطوة العاملة، أو `-1` حين انتهى الجريان. */
  readonly at: number;
  /** توجيهُ الخطّة — نصٌّ واحد يُعرض ويُنطَق، **ولا يأذن بشيء**. */
  readonly orientationAr: string;
}

/** الأرقام العربية-الهندية — نظير `VoicePlanReadback.Numeral` رقماً برقم. */
const NUMERALS = ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];

/**
 * يرقّم عدداً بالأرقام العربية-الهندية.
 * @param value العدد.
 */
export function numeral(value: number): string {
  return String(value)
    .split("")
    .map((digit) => (digit >= "0" && digit <= "9" ? NUMERALS[Number(digit)] : digit))
    .join("");
}

/**
 * جملةُ خطوةٍ داخل التوجيه.
 * @param step الخطوة.
 * @param ordinal ترتيبها من واحد.
 */
export function planStepSentence(step: VoicePlanStep, ordinal: number): string {
  const sentence = "(" + numeral(ordinal) + ") " + step.purposeAr;
  return step.screenAsksForAr.length === 0
    ? sentence
    : sentence + " وتطلب شاشتُه: " + step.screenAsksForAr.join("، ") + ".";
}

/**
 * **توجيهُ الخطّة — نصٌّ واحد يُعرض ويُنطَق، ولا يحمل رمزاً ولا يُصاحبه زرّ تأكيد.**
 *
 * وهو ليس ترفاً: التأكيدُ لكل خطوةٍ على حدة (وهو مفروضٌ لا مُختار — رمزُ التأكيد
 * صورةٌ حتمية لأمرٍ واحد)، وثلاثةُ ملخّصاتٍ بلا جملةٍ جامعة انحدارٌ في الفهم. فتُقال
 * الخطّةُ كلُّها **مرّةً واحدة قبل أن تبدأ**. وزرٌّ بجانبه كان سيعلّم الناس أن يضغطوا
 * على ما لم يقرأوه.
 * @param plan الخطّة.
 */
export function planReadbackArabic(plan: VoicePlan): string {
  const head = plan.nameAr + " — خطّة من " + numeral(plan.steps.length) + " خطوات.";
  const body = plan.steps.map((step, index) => planStepSentence(step, index + 1)).join(" ");
  return plan.steps.length === 0 ? head + " ولا يُرحَّل شيء بالصوت." : head + " " + body + " ولا يُرحَّل شيء بالصوت.";
}

/**
 * ترويسةُ ملخّص خطوةٍ — **تُلصَق أمام الملخّص القائم بلا تغييره**، فبوابةُ التأكيد
 * ورمزُها يبقيان على أمرٍ واحد كما كانا.
 * @param ordinal ترتيب الخطوة من واحد.
 * @param total عدد الخطوات.
 */
export function planStepPrefix(ordinal: number, total: number): string {
  return "الخطوة " + numeral(ordinal) + " من " + numeral(total) + " — ";
}

function longestHit(text: string, phrases: readonly string[]): number {
  let best = 0;
  for (const phrase of phrases) {
    const needle = fold(phrase);
    if (needle.length > best && text.includes(needle)) best = needle.length;
  }
  return best;
}

/**
 * **يطابق خطّةً بجملة: طلبٌ وشرطٌ معاً — لا عبارةٌ واحدة.**
 *
 * ⚠ ولماذا اجتماعُ مقطعين لا احتواءُ عبارة: لأن الطلب والشرط **لا يتجاوران** في كلام
 * الناس. جملةُ المالك «سجّل سند قبض **من شركة المسار الأمثل** فإن لم تجدها…» بينهما
 * اسمُ العميل، وأيُّ عبارةٍ كاملة تُكتب في السجلّ كانت ستفترض جواراً لا يقع.
 *
 * والخطّة **تفوز حين تُطابق**: دليلُها أقوى — طلبٌ وشرط — من دليل النيّة المفردة.
 * وجملةٌ بلا شرطٍ تبقى نيّةً واحدة كما كانت، فلا تنكسر جملةٌ تعمل اليوم.
 * @param transcript التفريغ.
 * @param plans الخطط المتاحة.
 */
export function matchPlan(transcript: string, plans: readonly VoicePlan[] = VOICE_PLANS): VoicePlan | null {
  const text = fold(transcript ?? "");
  let best = 0;
  let winners: VoicePlan[] = [];

  for (const plan of plans) {
    const trigger = longestHit(text, plan.triggerPhrases);
    const condition = longestHit(text, plan.conditionPhrases);
    /* **كلاهما أو لا شيء**: شرطٌ بلا طلبٍ ليس خطّةً، وطلبٌ بلا شرطٍ نيّةٌ مفردة. */
    if (trigger === 0 || condition === 0) continue;

    const score = trigger + condition;
    if (score > best) {
      best = score;
      winners = [plan];
    } else if (score === best) {
      winners.push(plan);
    }
  }

  /* تعادلٌ رفضٌ لا قرعة: خطّتان متساويتان تعنيان أن الجملة تحتمل معنيين. */
  return winners.length === 1 ? (winners[0] as VoicePlan) : null;
}

/**
 * يبدأ جرياناً. **ولا ينفّذ شيئاً**: يبني الحال ويقرأ الشرائح ويقف عند أول ما ينقص.
 * @param plan الخطّة.
 * @param transcript التفريغ الأوّل.
 */
export function startPlan(plan: VoicePlan, transcript: string): VoicePlanRun {
  return {
    plan,
    transcript,
    at: 0,
    orientationAr: planReadbackArabic(plan),
    steps: plan.steps.map((step) => ({
      step,
      state: "pending",
      resolution: null,
      handoff: null,
      refusals: [],
    })),
  };
}

function replace(run: VoicePlanRun, index: number, patch: Partial<VoicePlanStepRun>): VoicePlanRun {
  return {
    ...run,
    steps: run.steps.map((entry, at) => (at === index ? { ...entry, ...patch } : entry)),
  };
}

/** الخطوة العاملة، أو `null` حين انتهى الجريان. */
export function currentStep(run: VoicePlanRun): VoicePlanStepRun | null {
  return run.at >= 0 && run.at < run.steps.length ? (run.steps[run.at] as VoicePlanStepRun) : null;
}

/**
 * يقرأ شرائح الخطوة العاملة من التفريغ الأوّل — **ولا يعيد المطابقة على السجلّ**.
 * @param run الجريان.
 * @param options ما يُحقن كي تكون القراءة حتمية.
 */
export function readCurrentStep(run: VoicePlanRun, options: VoiceReadingOptions): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null) return run;

  const intent = intentById(entry.step.intentId);
  if (intent === null) {
    /* لا يقع إلا إن انحرفت المرآة — والحارس يمنع ذلك عند البناء. ويُقال ولا يُخمَّن. */
    return replace(run, run.at, { state: "refused", refusals: ["ai.voice.intent_not_understood"] });
  }

  const reading = readCommandInto(intent, run.transcript, options);
  if (!reading.ok) return replace(run, run.at, { state: "refused", refusals: reading.codes });

  return replace(run, run.at, {
    resolution: reading.resolution,
    state: reading.resolution.missingSlots.length > 0 ? "asking" : "pending",
  });
}

/**
 * يقرأ **جواب إنسان** في نيّة الخطوة العاملة وحدها.
 *
 * ⚠ ولا يُعاد الجواب على السجلّ كلّه: «خمسة آلاف» تُطابق نيّةً أخرى، فيُملأ حقلٌ في
 * مستندٍ لم يطلبه أحد. **وهذا هو الفرق بين خطّةٍ ومالئِ نماذج ثانٍ بلا حارس.**
 * @param run الجريان.
 * @param answer ما قاله الإنسان.
 * @param options ما يُحقن.
 */
export function answerCurrentStep(
  run: VoicePlanRun,
  answer: string,
  options: VoiceReadingOptions
): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null) return run;

  const intent = intentById(entry.step.intentId);
  if (intent === null) return run;

  /* **الجواب يُضاف إلى الجملة الأولى لا يُبدلها**: من سُئل عن طريقة القبض قال «نقد»
     وحدها، وقراءتُها منفردةً تُفقد المبلغَ والتاريخ اللذين قالهما قبل قليل. */
  const merged = run.transcript + " " + answer;
  const reading = readCommandInto(intent, merged, options);
  if (!reading.ok) return replace(run, run.at, { state: "refused", refusals: reading.codes });

  return {
    ...replace(run, run.at, {
      resolution: reading.resolution,
      state: reading.resolution.missingSlots.length > 0 ? "asking" : "pending",
    }),
    transcript: merged,
  };
}

/**
 * **يؤكّد الخطوة العاملة ويُسلّم مسوّدتها.** الباب الوحيد، ولكل خطوةٍ مرّة.
 *
 * ⚠ ويمرّ من `authorise` بلا التفاف: لا تملك هذه الوحدة طريقاً آخر إلى `VoiceDispatch`.
 * @param run الجريان.
 * @param caller المتكلّم ومنشأته وصلاحياته.
 */
export function confirmCurrentStep(run: VoicePlanRun, caller: VoiceCaller): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null || entry.resolution === null) return run;

  const decided = authorise(entry.resolution, caller, entry.resolution.confirmationToken);
  if (!decided.ok) return replace(run, run.at, { state: "refused", refusals: decided.codes });

  return replace(run, run.at, { state: "handedOff", handoff: handoffOf(decided.dispatch), refusals: [] });
}

/**
 * جوابُ الإنسان عن شرط الخطوة: **هل وجد ما بحث عنه؟**
 *
 * ⚠ ولماذا يُسأل الإنسان أصلاً: لأن المسار المنطوق **لا يستطيع أن يبحث**. لا ينادي
 * باباً، ولا يوجد في العقد بحثٌ عن عميلٍ بالاسم — `readCustomer` يطلب معرّفاً وهو
 * جوابُ البحث نفسه. والوحيد القادر على حلّ اسمٍ إلى هويّة في هذه المعمارية هو إنسانٌ
 * أمام شاشةٍ فيها مُنتقٍ. **فيُسأل ولا يُخمَّن.**
 * @param run الجريان.
 * @param found هل وجده؟
 */
export function answerCondition(run: VoicePlanRun, found: boolean): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null || entry.step.condition !== "WhenHumanFindsNothing") return run;
  return found ? advance(replace(run, run.at, { state: "skipped" })) : run;
}

/** يُعلن أن الخطوة العاملة تمّت على شاشتها، ويتقدّم. */
export function completeCurrentStep(run: VoicePlanRun): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null) return run;
  return advance(replace(run, run.at, { state: "done" }));
}

/** يُعلن أن الإنسان ترك الخطوة — **ويقف الجريان عندها**، ولا يُستأنف تلقائياً. */
export function abandonCurrentStep(run: VoicePlanRun): VoicePlanRun {
  const entry = currentStep(run);
  if (entry === null) return run;
  return { ...replace(run, run.at, { state: "abandoned" }), at: -1 };
}

function advance(run: VoicePlanRun): VoicePlanRun {
  const next = run.at + 1;
  return { ...run, at: next < run.steps.length ? next : -1 };
}

/**
 * **دفترُ الخطّة: ما تمّ وما لم يتمّ، بالاسم.**
 *
 * ⚠ ولا يُقترح هنا تراجعٌ ولا تعويض. **ولا حذف في هذا النظام أصلاً** (`delete` فعلٌ
 * ممنوع). وإن تمّت الخطوة الأولى ولم تتمّ الثانية فليس ذلك نصفَ تطبيق: الخطوةُ الأولى
 * **تمّت**، والعميل باق، ويُعاد السند وحده متى شاء صاحبُه. والمطلوب إفصاحٌ لا رجوع.
 * @param run الجريان.
 */
export function planLedgerArabic(run: VoicePlanRun): string {
  const lines = run.steps.map((entry, index) => {
    const head = "الخطوة " + numeral(index + 1);
    switch (entry.state) {
      case "done":
        return head + " تمّت: " + entry.step.purposeAr;
      case "handedOff":
        return head + " سُلّمت إلى شاشتها ولم يُعلَن تمامُها: " + entry.step.purposeAr;
      case "skipped":
        return head + " لم تلزم: " + entry.step.purposeAr;
      case "asking":
        return head + " واقفةٌ تسأل عمّا ينقص.";
      case "abandoned":
        return head + " تُركت.";
      case "refused":
        return head + " رُفضت.";
      default:
        return head + " لم تبدأ.";
    }
  });

  return lines.join(" ") + " ولا شيء يُحذف — ما تمّ باق، وأعِد ما لم يتمّ وحده متى شئت.";
}

/**
 * **الحالة التي تُرفض ولا تُخمَّن**: انقطع الاتصال بعد أن غادرت الشاشةُ، فلا يُعرف
 * هل تمّت الخطوة أم لا. **وإعادتُها تُنشئ عميلاً مكرّراً.** فلا تُعاد.
 * @param ordinal ترتيب الخطوة من واحد.
 */
export function planUncertainAr(ordinal: number): string {
  return (
    "لا أعرف إن تمّت الخطوة " +
    numeral(ordinal) +
    ". افتح شاشتها وتحقّق قبل أن تعيدها — وإعادتُها بلا تحقّقٍ تُنشئ نظيراً مكرّراً."
  );
}

/** الشرائح الممتلئة في خطوةٍ — للعرض. */
export function filledSlots(entry: VoicePlanStepRun): readonly SpokenSlotValue[] {
  return entry.resolution?.slots ?? [];
}
