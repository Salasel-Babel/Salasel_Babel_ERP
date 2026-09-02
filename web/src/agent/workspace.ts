/* ═══════════════════════════════════════════════════════════════════════════
   طيُّ أحداث الدور إلى خيطٍ يُقرأ  ·  Folding turn events into a readable thread
   ───────────────────────────────────────────────────────────────────────────
   الخادم يبثّ **أجزاءً**: جزءُ تفكيرٍ، ثم جزءُ تفكيرٍ ثانٍ، ثم جزءُ نصّ، ثم
   آخر. وعرضُ كل جزءٍ سطراً يجعل اللوحة قائمةً من مئة سطرٍ من كلمتين — تبدو
   مشغولةً ولا تُقرأ. فالأجزاء المتتالية من الصنف نفسه **تُدمَج في سطرٍ واحد
   ينمو**، وهو ما يجعل البثّ يُقرأ كتابةً لا كبرقيّات.

   **والمؤشّر هو كل آلية الاستئناف.** كل حدثٍ يحمل رقمه، واللوحة تحفظ آخر ما
   رأت، فانقطاعُ الشبكة يُستأنف بـ«ما بعد ن؟» بلا تكرارٍ ولا فجوة. ولا ترقيم
   ثانٍ في المتصفّح: الترقيم من الخادم، والمتصفّح لا يخترع ما يستطيع أن يقرأه.

   **ولا شيء هنا يعرف كم كان المرشّحون.** الأحداث لا تحمل عدداً، وهذا الملفّ
   لا يحسبه: ما لا يوجد لا يُسرَّب سهواً.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { AgentTurnEvent, ApiError } from "../api/generated/types";

/* ═══════════════════════════════════════════════════ ١ · سطرٌ في الخيط */

/** سطرٌ واحد كما يُعرض في اللوحة. */
export type AgentLine =
  /** ما كتبه المستخدم. */
  | { readonly kind: "you"; readonly text: string }
  /** ما قاله الوكيل. */
  | { readonly kind: "said"; readonly text: string }
  /** تفكيرٌ مُلخَّص — تقدّمٌ يُرى بدل صمتٍ طويل، ولا يُبنى عليه قرار. */
  | { readonly kind: "thinking"; readonly text: string }
  /** أداةٌ بدأت أو رُفضت. */
  | {
      readonly kind: "tool";
      readonly toolName: string;
      readonly refused: boolean;
      readonly refusals: readonly ApiError[];
    }
  /** خطّةٌ أُعلنت. */
  | { readonly kind: "plan"; readonly steps: readonly string[] }
  /** مسوّدةٌ هبطت على شاشتها. **ولا زرّ ترحيلٍ معها.** */
  | { readonly kind: "landed"; readonly screenRoute: string }
  /** رُفض الدور كلّه. */
  | { readonly kind: "refused"; readonly refusals: readonly ApiError[] };

/** خيطُ المحادثة كما تعرضه اللوحة، ومؤشّرُ آخر حدثٍ طُوي فيه. */
export interface AgentThread {
  readonly lines: readonly AgentLine[];
  readonly cursor: number;
}

/** خيطٌ فارغ — بدايةُ كل جلسة. */
export const EMPTY_THREAD: AgentThread = { lines: [], cursor: 0 };

/* ═════════════════════════════════════════ ٢ · رموز الرفض التي تُعرَض حالاً */

/**
 * بلغت المنشأة سقف إنفاقها. **ورمزٌ لا نصّ**: الرسالة تتغيّر بتغيّر السقف،
 * والرمز هو نقطة الاعتماد البرمجية الوحيدة على هذا السطح كلّه.
 */
export const AGENT_SPEND_CEILING_CODE = "ai.agent.spend_ceiling_reached";

/** الوكيل غير مركَّب على هذا الخادم — إعدادُ نشرٍ لا عطل. */
export const AGENT_DISABLED_CODE = "ai.workspace.agent_disabled";

/** الجلسة غير موجودة أو انقضت — «الجلسة انقطعت». */
export const AGENT_SESSION_GONE_CODE = "ai.workspace.session_not_found";

/** هل في هذه الأسباب سببٌ برمزٍ بعينه؟ */
export function hasCode(refusals: readonly ApiError[], code: string): boolean {
  return refusals.some((refusal) => refusal.code === code);
}

/* ═══════════════════════════════════════════════════ ٣ · الطيّ نفسه */

/**
 * يطوي صفحةَ أحداثٍ في الخيط ويعيد خيطاً جديداً.
 *
 * **ولا يقبل ما رآه سلفاً**: كل حدثٍ رقمُه أصغر من المؤشّر أو مساوٍ له
 * يُتخطّى، فإعادةُ طلبٍ بعد انقطاعٍ لا تكرّر سطراً. ومحاولةُ الاعتماد على
 * «ما وصل» بدل «رقمه» هي بعينها ما يجعل انقطاعاً واحداً يُنتج محادثةً مضاعفة.
 *
 * @param thread الخيط الحالي.
 * @param events أحداث الصفحة بترتيبها.
 */
export function foldAgentEvents(
  thread: AgentThread,
  events: readonly AgentTurnEvent[]
): AgentThread {
  const lines: AgentLine[] = [...thread.lines];
  let cursor = thread.cursor;

  for (const event of events) {
    if (event.sequence <= cursor) continue;
    cursor = event.sequence;

    const last = lines[lines.length - 1];

    switch (event.kind) {
      case "text": {
        const text = event.text ?? "";
        if (text === "") break;
        if (last !== undefined && last.kind === "said") {
          lines[lines.length - 1] = { kind: "said", text: last.text + text };
        } else {
          lines.push({ kind: "said", text });
        }
        break;
      }

      case "thinking": {
        const text = event.text ?? "";
        if (text === "") break;
        if (last !== undefined && last.kind === "thinking") {
          lines[lines.length - 1] = { kind: "thinking", text: last.text + text };
        } else {
          lines.push({ kind: "thinking", text });
        }
        break;
      }

      case "planProposed":
        lines.push({ kind: "plan", steps: event.steps });
        break;

      case "toolStarted":
        lines.push({ kind: "tool", toolName: event.toolName ?? "", refused: false, refusals: [] });
        break;

      case "toolRefused":
        lines.push({
          kind: "tool",
          toolName: event.toolName ?? "",
          refused: true,
          refusals: event.refusals,
        });
        break;

      case "draftLanded":
        lines.push({ kind: "landed", screenRoute: event.screenRoute ?? "" });
        break;

      case "refused":
        lines.push({ kind: "refused", refusals: event.refusals });
        break;

      /* **questionRaised لا يفتح سطراً**: الورقة نفسها تُعرض لوحةً حاجزة،
         وسطرٌ يقول «سألتُ» تحتها ضجيجٌ يُقرأ مرّتين. وcompleted نهايةُ دورٍ
         يقولها الطور لا سطرٌ في الخيط. */
      default:
        break;
    }
  }

  return { lines, cursor };
}

/** يضيف ما كتبه المستخدم إلى الخيط قبل أن يبدأ الدور. */
export function withUtterance(thread: AgentThread, text: string): AgentThread {
  return { lines: [...thread.lines, { kind: "you", text }], cursor: thread.cursor };
}
