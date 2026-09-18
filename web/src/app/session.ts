/* ═══════════════════════════════════════════════════════════════════════════
   الجلسة في المتصفّح — ما يُعرَف منها، ومتى تُجدَّد
   ───────────────────────────────────────────────────────────────────────────
   **ثلاثةُ أسئلةٍ يجيبها هذا الملفّ وحده، فلا تُجاب في ثلاث شاشات بثلاث طرق:**
   «هل عندي جلسة؟» و«ما الذي أحفظه بعد فتحها؟» و«متى أُجدّدها؟».

   **والتجديدُ قبل الانقضاء لا بعده:** الاعتمادُ الفاعل يعيش خمس عشرة دقيقة،
   ومحاسبٌ يكتب قيداً لا يجوز أن يُطرَد في منتصفه ليدخل من جديد. فيُجدَّد وهو
   حيٌّ، ولا يرى المستخدم شيئاً — وهذا هو الفرق بين «جلسةٌ قصيرة» و«نظامٌ
   يطردك كلَّ ربع ساعة».
   ═══════════════════════════════════════════════════════════════════════════ */
import type { AccessSession } from "../api/generated/types";
import type { ApiConfig } from "./config";

/**
 * هل في هذا الإعداد اعتمادٌ يُقدَّم؟
 * <p>
 * <b>والاعتمادُ وحده هو المقياس، لا اعتمادُ التجديد:</b> رمزُ العرض المُهيَّأ من
 * إعداد الخادم لا عائلةَ له ولا تجديدَ معه، وهو اعتمادٌ صحيح يفتح النظام. فمقياسٌ
 * يشترط التجديد كان سيحجب العرضَ عن مالكه.
 * </p>
 * @param config الإعداد الجاري.
 */
export function hasSession(config: ApiConfig): boolean {
  return config.token.trim() !== "";
}

/**
 * يكتب ما تُسلّمه جلسةٌ مفتوحة في الإعداد.
 * <p>
 * <b>والمنشأةُ تُختار ولا تُخمَّن:</b> جلسةٌ تبلغ منشأةً واحدة تُفتح عليها، وأكثرُ
 * من واحدة تترك الاختيار لصاحبها — واختيارُ الأولى حرفياً يفتح دفترَ منشأةٍ لم
 * يطلبها أحد، وهو في نظامٍ محاسبيّ خطأٌ يُكتب فيه قيد.
 * </p>
 * @param config الإعداد الجاري.
 * @param session الجلسة كما سلّمها الخادم.
 */
export function applySession(config: ApiConfig, session: AccessSession): ApiConfig {
  const only = session.memberships.length === 1 ? session.memberships[0]?.companyId : undefined;
  return {
    ...config,
    token: session.accessCredential,
    refreshToken: session.refreshCredential,
    tokenExpiresAt: session.accessExpiresAt,
    companyId: only ?? config.companyId,
  };
}

/** يمحو كلَّ أثرٍ للجلسة من الإعداد — والخروجُ محوٌ لا إخفاء. */
export function clearSession(config: ApiConfig): ApiConfig {
  return { ...config, token: "", refreshToken: "", tokenExpiresAt: "", companyId: "" };
}

/**
 * المهلةُ التي يُجدَّد قبلها الاعتماد — دقيقتان.
 * <p>
 * ولماذا دقيقتان لا ثانيتان: النداءُ نفسه يستغرق وقتاً، والساعتان — ساعةُ
 * المتصفّح وساعةُ الخادم — تنحرفان. ودقيقتان أوسعُ من انحرافٍ معقول وأضيقُ من
 * أن تُجدَّد الجلسةُ بلا سبب.
 * </p>
 */
export const RENEW_BEFORE_MS = 120_000;

/**
 * هل حان وقتُ التجديد؟
 * @param config الإعداد الجاري.
 * @param now اللحظة — تُمرَّر كي يُقاس هذا بلا ساعةٍ حقيقية.
 */
export function needsRenewal(config: ApiConfig, now: number): boolean {
  if (config.refreshToken.trim() === "" || config.tokenExpiresAt === "") return false;
  const expires = Date.parse(config.tokenExpiresAt);
  /* لحظةٌ لا تُقرأ لا تعني «جدِّد الآن»: تعني أننا لا نعرف، والتجديدُ بلا معرفةٍ
     يُستهلك اعتمادَ تجديدٍ في كل رسمة. */
  return Number.isFinite(expires) && expires - now <= RENEW_BEFORE_MS;
}
