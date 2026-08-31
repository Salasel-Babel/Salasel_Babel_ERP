/* ═══════════════════════════════════════════════════════════════════════════
   زرّ الصوت الحاضر دائماً  ·  The always-present voice control
   ───────────────────────────────────────────────────────────────────────────
   الصوت في هذا المنتج ليس ميزةً في شاشةٍ واحدة: هو مدخلٌ إلى النظام. ولذلك
   زرّه حاضرٌ على كل شاشة.

   **وصدقُه شرطُ وجوده:** الزرّ يسأل `speechSupport()` **قبل** أن يَعِد بشيء،
   فإن لم يكن التفريغ متاحاً قال **السبب المُسمّى** (لا متصفّح · لا اتصال
   مؤمّن · إذنٌ مرفوض) بدل أن يستمع إلى لا شيء ثم يصمت. وزرٌّ يَعِد ولا يفي
   يُفقد الثقة بكل زرٍّ بعده.

   وما يُلتقط **لا يصير حقيقة محاسبية**: يملأ مسوّدةً يؤكّدها إنسان
   (ADR-0024)، والقيمة المنطوقة مصدرها السادس «منطوق» (ADR-0030).

   **والزرّ يفتح لوحة الأمر المنطوق `/voice`** — أي أنه يفي بما يَعِد به. وكان
   قبل ذلك يبدّل حالةً ولا يقود إلى شيء، لأن ما يقود إليه لم يكن قد بُني بعد:
   لوحةٌ تحمل الأقسام الخمسة، وتقرأ ما يُقال، وتردّ الملخّص قبل أن تُنفّذ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { speechSupport } from "../../voice";

/**
 * زرّ الصوت. يعلن حاله قبل أن يَعِد، ويُبلّغ حالته لقارئ الشاشة.
 * @param props ما يقع عند طلب الاستماع.
 */
export function VoiceDock(props: { onAsk?: (available: boolean) => void }): ReactNode {
  const { t } = useT();
  const [pressed, setPressed] = useState(false);
  const support = useMemo(() => speechSupport(), []);
  const available = support === "supported";

  const navigate = useNavigate();

  const onClick = useCallback(() => {
    setPressed((v) => !v);
    props.onAsk?.(available);
    /* والانتقال يقع **سواء أكان التفريغ متاحاً أم لا**: اللوحة تعمل بالنصّ
       المكتوب حين يتعذّر الصوت، وحجبُها عمّن لا ميكروفون له حجبٌ للميزة كلّها. */
    void navigate({ to: "/voice" });
  }, [available, navigate, props]);

  const label = available
    ? pressed
      ? t("screen.voice.listening")
      : t("app.voice.open")
    : t("screen.voice.unavailable." + support);

  return (
    <button
      type="button"
      className="voicedock"
      data-testid="voice-dock"
      data-listening={pressed && available ? "true" : "false"}
      data-available={available ? "true" : "false"}
      aria-pressed={pressed}
      aria-label={label}
      title={label}
      onClick={onClick}
    >
      <span className={"voicedock__dot" + (pressed && available ? " cine-live" : "")} aria-hidden="true" />
      <span>{available ? t("app.voice.open") : t("app.voice.unavailable")}</span>
    </button>
  );
}
