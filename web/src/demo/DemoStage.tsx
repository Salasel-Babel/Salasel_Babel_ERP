/* ═══════════════════════════════════════════════════════════════════════════
   مِنصّة العرض — الهيكل الذي يُصوَّر.
   ───────────────────────────────────────────────────────────────────────────
   ليست شاشة منتج: هي طبقة عرضٍ تُرمى بعد التسجيل (ADR-0028 §2). ولا تستورد
   شيئاً من الدفتر، ولا تكتب في قاعدة، ولا تعرف كلمة مرور. وما تعرضه من أرقام
   إمّا من لقطة القاعدة المبذورة، وإمّا مُحقَنٌ حرفياً من مُخرَج أمرٍ حقيقي.
   ═══════════════════════════════════════════════════════════════════════════ */
import "./demo.css";
import { useEffect, type ReactNode } from "react";
import { useLocale } from "../i18n/react";
import { installBridge } from "./store";
import { useDemo } from "./useDemo";
import { TitleScene } from "./scenes/TitleScene";
import { TamperScene } from "./scenes/TamperScene";
import { TimeTravelScene } from "./scenes/TimeTravelScene";
import { ExplainScene } from "./scenes/ExplainScene";
import { LanguageScene } from "./scenes/LanguageScene";
import { VoiceScene } from "./scenes/VoiceScene";
import { SecondOpinionScene } from "./scenes/SecondOpinionScene";
import { QrScene } from "./scenes/QrScene";
import { ClosingScene } from "./scenes/ClosingScene";

const TRUTH_LABEL: Record<string, string> = {
  real: "حقيقي — من القاعدة والخادم مباشرةً",
  sim: "محاكاة — موسومة",
  mixed: "حقيقي، وجزءٌ منه محاكاة موسومة",
};

declare global {
   
  var __demoLocale: ((code: string) => void) | undefined;
}

/** المِنصّة. */
export function DemoStage(): ReactNode {
  const state = useDemo();
  const { setLocale } = useLocale();

  useEffect(() => {
    installBridge();
  }, []);

  useEffect(() => {
    globalThis.__demoLocale = setLocale;
    return () => {
      globalThis.__demoLocale = undefined;
    };
  }, [setLocale]);

  return (
    <div className="demo-stage" data-testid="demo-stage" data-scene={state.scene}>
      <header className="demo-cap">
        <div className="demo-cap__brand">
          <span className="demo-cap__mark" aria-hidden="true" />
          <span className="demo-cap__name">سلاسل بابل</span>
        </div>
        <div className="demo-cap__text">
          <h2 className="demo-cap__title" data-testid="demo-caption">
            {state.caption}
          </h2>
          {state.captionSub ? <p className="demo-cap__sub">{state.captionSub}</p> : null}
        </div>
        <span className="demo-truth" data-truth={state.truth} data-testid="demo-truth">
          <span className="demo-truth__dot" />
          {TRUTH_LABEL[state.truth]}
        </span>
      </header>

      <main className="demo-body" data-scene={state.scene}>
        {state.scene === "title" ? <TitleScene /> : null}
        {state.scene === "tamper" ? <TamperScene /> : null}
        {state.scene === "time" ? <TimeTravelScene /> : null}
        {state.scene === "explain" ? <ExplainScene /> : null}
        {state.scene === "language" ? <LanguageScene /> : null}
        {state.scene === "qr" ? <QrScene /> : null}
        {state.scene === "voice" ? <VoiceScene /> : null}
        {state.scene === "opinion" ? <SecondOpinionScene /> : null}
        {state.scene === "closing" ? <ClosingScene /> : null}
      </main>

      <footer className="demo-foot">
        <span>مؤسسة نخيل الشرقية للتجارة والمقاولات — شركة تجريبية مبذورة</span>
        <span className="demo-foot__spacer" />
        <span className="demo-foot__mono">PostgreSQL 16 · .NET 10 · دفتر يُضاف إليه فقط</span>
      </footer>
    </div>
  );
}
