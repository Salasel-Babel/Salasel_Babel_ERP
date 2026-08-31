/* ═══════════════════════════════════════════════════════════════════════════
   لغة الحركة — الحدّ البرمجي  ·  The motion vocabulary — its code boundary
   ───────────────────────────────────────────────────────────────────────────
   **الأسماء هنا عقدٌ لا اقتراح.** خمسةُ وكلاءٍ يبنون الأقسام فوق هذا الملفّ،
   وما لا يُسمّى هنا يخترع كلٌّ منهم بديلاً له. فمن احتاج مفردةً ليست في
   {@link MOTION} فليضفها **هنا ومعها جملةُ متى تُستعمل**، ثم يضف قاعدتها في
   `styles/motion.css` — ولا يكتب اسم صنفٍ حرفياً في شاشة.

   **ولماذا مؤقّتٌ لا `animationend`:** لأن `prefers-reduced-motion` يقصّ كل
   مدّة إلى `.001ms`، فـ`animationend` يقع فوراً وتُرفع الحالة **قبل أن
   تُقرأ**. والمؤقّت يُبقي الرسالة ظاهرةً المدّة نفسها في الحالتين — متحرّكةً
   لمن يقبل الحركة، وساكنةً لمن لا يقبلها. وهذا هو الفرق بين احترام التفضيل
   وبين إلغاء الرسالة (docs/evidence/traps.md#fakh-reduced-motion-erases-the-message-not-only-the-motion).
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useRef, useState, type CSSProperties } from "react";

/**
 * مفردات الحركة السبع. المفتاح هو الاسم الذي يُستعمل في الشيفرة، والقيمة صنف
 * CSS في `styles/motion.css`.
 *
 * - `arrive` — صفٌّ أو رقمٌ **جاء من الخادم للتوّ**.
 * - `post` — **لحظة الترحيل**: الفعل المالي الذي لا رجعة فيه. ولا يُصرَف على غيره.
 * - `refuse` — **الرفض**: النظام رفض ولم يخمّن. حالةٌ أولى لا خطأ يُخفى.
 * - `infer` — **الاستنتاج**: قيمةٌ اشتقّها النظام لا أدخلها المستخدم.
 * - `inferWash` — موجة الاستنتاج على وعاءٍ كامل (حقل، خليّة، لوح).
 * - `reveal` — **كشفٌ متدفّق**: قيمٌ تظهر واحدةً بعد أخرى بترتيب `--reveal-index`.
 * - `transit` — **الانتقال بين الأقسام**: مسار العرض.
 * - `live` — شيءٌ **يعمل الآن**: الميكروفون يستمع، الخادم يقرأ.
 * - `scan` — النظام **يقرأ مستنداً**.
 */
export const MOTION = {
  arrive: "cine-arrive",
  post: "cine-post",
  refuse: "cine-refuse",
  infer: "cine-infer",
  inferWash: "cine-infer-wash",
  reveal: "cine-reveal",
  transit: "cine-transit",
  live: "cine-live",
  scan: "cine-scan",
} as const;

/** اسم مفردةٍ من مفردات الحركة. */
export type MotionName = keyof typeof MOTION;

/**
 * كم تبقى المفردة قائمةً قبل أن تُرفع، بالملّي ثانية. القيم مرآةُ
 * `--motion-*` في `styles/cinematic.css` — ومن غيّر هناك غيّر هنا.
 */
export const MOTION_DWELL_MS: Readonly<Record<MotionName, number>> = {
  arrive: 1100,
  post: 1100,
  refuse: 340,
  infer: 340,
  inferWash: 1100,
  reveal: 340,
  transit: 620,
  live: 0,
  scan: 0,
};

/**
 * يُشعل مفردةَ حركةٍ لمرّةٍ واحدة ويرفعها بعد مدّتها.
 * @param name اسم المفردة.
 * @returns الصنف الحالي (نصّ فارغ حين تكون مُطفأة) ودالّة الإشعال.
 */
export function useMoment(name: MotionName): readonly [string, () => void] {
  const [on, setOn] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (timer.current !== null) clearTimeout(timer.current);
    },
    []
  );

  const fire = useCallback(() => {
    if (timer.current !== null) clearTimeout(timer.current);
    /* الإطفاء ثم الإشعال في إطارٍ لاحق: إعادةُ إشعالٍ بلا إطفاء لا تُعيد
       تشغيل الحركة، فيبدو الحدث الثاني وكأنه لم يقع. */
    setOn(false);
    timer.current = setTimeout(() => {
      setOn(true);
      timer.current = setTimeout(() => setOn(false), MOTION_DWELL_MS[name]);
    }, 0);
  }, [name]);

  return [on ? MOTION[name] : "", fire] as const;
}

/**
 * ترتيب العنصر في كشفٍ متدفّق، أسلوباً جاهزاً للوضع على العنصر.
 * @param index ترتيب العنصر من الصفر.
 */
export function revealAt(index: number): CSSProperties {
  return { "--reveal-index": index } as CSSProperties;
}
