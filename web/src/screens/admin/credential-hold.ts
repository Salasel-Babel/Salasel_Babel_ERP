/* ═══════════════════════════════════════════════════════════════════════════
   حجزُ اعتمادٍ في الذاكرة — ولا موضع آخر  ·  An in-memory credential hold
   ───────────────────────────────────────────────────────────────────────────
   **المشكلة التي جاء هذا الملفّ لأجلها:** `openSession` يُخرج اعتمادَين معاً
   — فاعلاً وتجديداً — و**مرّة واحدة**؛ المُودَع في الخادم بصمتُهما. فمن فقد
   الاستجابة فقد الاعتماد ولا يُعيده أحد. واعتماد التجديد هو **المُدخَل
   الوحيد** لـ`renewSession`، وهو يقع على شاشةٍ ثانية.

   **وثلاثة حدودٍ تحكم هذا الملفّ، وكلٌّ منها منعُ عطلٍ بعينه:**

   ١ · **لا `localStorage` ولا `sessionStorage` ولا كعكة.** اعتماد التجديد
       سرٌّ يدور، وكتابتُه على القرص تجعله يعيش بعد إغلاق المتصفّح وتضعه في
       متناول كل شيفرةٍ على الأصل نفسه. فالحجز في **متغيّر وحدةٍ** يموت مع
       إعادة تحميل الصفحة — وذلك مقصود لا نقص.

   ٢ · **لا يُرسَم في DOM أبداً.** لا شاشةَ تعرض ما هنا ولا سمةَ `value` تحمله
       ولا `title`. والقياس البصري في هذا المستودع يلتقط لقطاتٍ كاملة الصفحة،
       فسرٌّ مرسوم يصير سرّاً **في ملفّ صورة**.

   ٣ · **لا يُكتب في رابط ولا يُسجَّل.** ولا يُمرَّر إلى `console` ولا إلى
       رسالة خطأ: الرفض يُقرأ برمزه، والرمز لا يحمل الاعتماد.

   **وما يُحجَز هنا اعتمادُ التجديد وحده.** الاعتماد الفاعل يذهب إلى إعداد
   النقل القائم (`app/config.ts`) كما يذهب اليوم من شاشة الدخول — وهو مسارٌ
   قائم لا يخترعه هذا الملفّ.
   ═══════════════════════════════════════════════════════════════════════════ */

/** اعتماد التجديد المحجوز، أو `null` حين لا شيء محجوز. */
let held: string | null = null;

/** ما يُعرف عن المحجوز بلا كشفه: هل يوجد، ومتى ينقضي، وأي دورة. */
export interface CredentialHold {
  /** هل يوجد اعتماد تجديد محجوز في هذه الصفحة؟ */
  readonly present: boolean;
  /** لحظة انقضائه كما وصلت من الخادم، أو `null`. */
  readonly expiresAt: string | null;
  /** رقم الدورة التي أُصدر فيها، أو `null`. */
  readonly generation: number | null;
  /** معرّف العائلة — وهو ما يُبطَل. ليس سرّاً، ويُعرض. */
  readonly sessionId: string | null;
}

let facts: CredentialHold = { present: false, expiresAt: null, generation: null, sessionId: null };

/** المشتركون في تغيّر الحجز — الشاشات تُعاد رسمُها حين يتغيّر. */
const listeners = new Set<() => void>();

function announce(): void {
  for (const listener of listeners) listener();
}

/**
 * يحجز اعتماد تجديدٍ ووقائعه.
 * @param credential اعتماد التجديد كما وصل — لا يُرسَم ولا يُسجَّل.
 * @param about وقائعه المعروضة: الانقضاء والدورة ومعرّف العائلة.
 */
export function holdRefreshCredential(
  credential: string,
  about: { expiresAt: string; generation: number; sessionId: string }
): void {
  held = credential;
  facts = {
    present: true,
    expiresAt: about.expiresAt,
    generation: about.generation,
    sessionId: about.sessionId,
  };
  announce();
}

/** يُسقط المحجوز — عند الإبطال، وعند الخروج، وبعد استهلاكه في تجديد. */
export function releaseRefreshCredential(): void {
  held = null;
  facts = { present: false, expiresAt: null, generation: null, sessionId: null };
  announce();
}

/**
 * يقرأ المحجوز **للاستعمال في نداءٍ واحد**. لا تُخزَّن نتيجتُه ولا تُعرض.
 * @returns الاعتماد، أو `null` حين لا شيء محجوز.
 */
export function takeRefreshCredential(): string | null {
  return held;
}

/** وقائع المحجوز بلا كشفه. */
export function credentialHold(): CredentialHold {
  return facts;
}

/**
 * يشترك في تغيّر الحجز.
 * @param listener ما يُنادى عند التغيّر.
 * @returns دالّة فكّ الاشتراك.
 */
export function subscribeToHold(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
