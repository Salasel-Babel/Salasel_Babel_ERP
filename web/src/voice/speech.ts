/* ═══════════════════════════════════════════════════════════════════════════
   غلاف واجهة الكلام في المتصفّح — Web Speech API، بالعربية السعودية.
   ───────────────────────────────────────────────────────────────────────────
   ما قيس فعلاً في بيئة البناء (لا استُنتج):
     • webkitSpeechRecognition **موجود** في Chromium 141 بلا رأس؛
     • و start() يُطلق onstart ثم onerror = "audio-capture" — حتى مع
       --use-fake-device-for-media-stream و --use-file-for-fake-audio-capture،
       وحتى مع نافذة حقيقية تحت Xvfb، وحتى بعد نجاح getUserMedia.
   أي أن **المسار الصوتي الحقيقي لا يُقاد بلا رأس في هذه البيئة**. ولذلك يحمل
   هذا الملف مساراً بديلاً معلناً (نصّ يُحقن) — لا لأنه ألطف، بل لأن غيابه يعني
   مكوّناً لا يُختبَر إلا بيد إنسان أمام ميكروفون.

   ولا يغادر الصوت الجهاز إلى خوادمنا في أي مسار هنا: المتصفّح يفرّغ، ونحن نأخذ
   **النصّ**. وما يفعله المتصفّح بالصوت داخلياً قرارُ المتصفّح لا قرارنا، وهو
   مذكور في القرار المرافق كي لا يُقال ما لا يُملك إثباته.
   ═══════════════════════════════════════════════════════════════════════════ */

/** لماذا لا يعمل التفريغ. كل حالة لها رسالة ومخرج، ولا حالة «لا أدري». */
export type SpeechUnavailable =
  | "unsupported"   /* المتصفّح لا يحمل الواجهة أصلاً (فَيَرفُكس، وويب-فيو قديم) */
  | "insecure"      /* الصفحة ليست HTTPS ولا localhost */
  | "denied"        /* رفض المستخدم إذن الميكروفون */
  | "no-audio"      /* لا مدخل صوتي — وهي حالة البناء بلا رأس المقيسة */
  | "network"       /* المتصفّح لا يبلغ خدمة التعرّف */
  | "aborted";      /* أُلغي */

/** حدث نتيجة: نصّ، وهل هو نهائي. */
export interface SpeechChunk {
  readonly text: string;
  readonly final: boolean;
  readonly confidence: number;
}

/** جلسة استماع قائمة. */
export interface SpeechSession {
  /** يوقف الاستماع ويطلب النتيجة النهائية. يُستدعى عند إفلات الزرّ. */
  stop(): void;
  /** يقطع بلا نتيجة. */
  abort(): void;
}

interface RecognitionLike {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  maxAlternatives: number;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: ((event: SpeechRecognitionEventLike) => void) | null;
  onerror: ((event: { error: string }) => void) | null;
  onend: (() => void) | null;
  onstart: (() => void) | null;
}

interface SpeechRecognitionEventLike {
  resultIndex: number;
  results: {
    length: number;
    [index: number]: { isFinal: boolean; length: number; [alt: number]: { transcript: string; confidence: number } };
  };
}

type RecognitionConstructor = new () => RecognitionLike;

function constructorOf(): RecognitionConstructor | null {
  const scope = globalThis as unknown as Record<string, RecognitionConstructor | undefined>;
  return scope.SpeechRecognition ?? scope.webkitSpeechRecognition ?? null;
}

/** هل الواجهة متاحة هنا؟ يُسأل قبل رسم الزرّ لا بعد الضغط عليه. */
export function speechSupport(): "supported" | SpeechUnavailable {
  if (!constructorOf()) return "unsupported";
  /* السياق الآمن شرطٌ في المتصفّحات: صفحةٌ على http عادي تعطي «not-allowed»
     بعد الضغط، وهي رسالة تُقرأ «رفض المستخدم» وليست كذلك. */
  if (typeof globalThis.isSecureContext === "boolean" && !globalThis.isSecureContext) return "insecure";
  return "supported";
}

/** يترجم رمز عطل المتصفّح إلى حالتنا المُسمّاة. */
export function translateError(code: string): SpeechUnavailable {
  switch (code) {
    case "not-allowed":
    case "service-not-allowed":
      return "denied";
    case "audio-capture":
      return "no-audio";
    case "network":
      return "network";
    case "aborted":
      return "aborted";
    default:
      return "unsupported";
  }
}

/** خيارات بدء الاستماع. */
export interface ListenOptions {
  readonly lang: string;
  readonly onChunk: (chunk: SpeechChunk) => void;
  readonly onFail: (reason: SpeechUnavailable) => void;
  readonly onEnd: () => void;
}

/**
 * يبدأ الاستماع. **يعيد جلسة لا وعداً**: الضغط المستمرّ يبدأ، والإفلات يوقف —
 * ولا يوجد في هذا الملف كشفٌ عن نهاية الكلام (VAD) بحال.
 *
 * ولماذا لا يوجد: كشفُ نهاية الكلام يقطع في منتصف رقم يتردّد قائله، ويلتقط
 * زميلاً يتكلّم في المكتب. والضغط المستمرّ يُلغي السؤال كلّه بدل أن يُحسِّن جوابه.
 * @param options الخيارات.
 */
export function listen(options: ListenOptions): SpeechSession | null {
  const Ctor = constructorOf();
  if (!Ctor) {
    options.onFail("unsupported");
    return null;
  }

  const recognition = new Ctor();
  recognition.lang = options.lang;
  recognition.continuous = true;
  recognition.interimResults = true;
  recognition.maxAlternatives = 1;

  recognition.onresult = (event) => {
    for (let i = event.resultIndex; i < event.results.length; i++) {
      const result = event.results[i];
      if (!result) continue;
      const alternative = result[0];
      if (!alternative) continue;
      options.onChunk({
        text: alternative.transcript,
        final: result.isFinal,
        confidence: typeof alternative.confidence === "number" ? alternative.confidence : 0,
      });
    }
  };
  recognition.onerror = (event) => options.onFail(translateError(event.error));
  recognition.onend = () => options.onEnd();

  try {
    recognition.start();
  } catch {
    options.onFail("unsupported");
    return null;
  }

  return {
    stop: () => {
      try {
        recognition.stop();
      } catch {
        /* جلسة انتهت سلفاً: الإيقاف بعد الانتهاء ليس عطلاً. */
      }
    },
    abort: () => {
      try {
        recognition.abort();
      } catch {
        /* كما أعلاه. */
      }
    },
  };
}
