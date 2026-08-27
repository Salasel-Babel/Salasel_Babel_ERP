/* ═══════════════════════════════════════════════════════════════════════════
   المشهد السادس — الإدخال المنطوق.
   **التفريغ محقون لا مسموع**، والمكوّن يرسم وسمه بنفسه ولا يُخفى هنا: تعرّف
   المتصفّح على الكلام لا يعمل في متصفّح بلا رأس على هذا الجهاز — جُرّبت ثلاث
   تهيئات وكلّها أعطت `audio-capture`.
   وما تحت التفريغ **حقيقي بالكامل**: نفس قارئ الأرقام العربية، ونفس تلوين
   المصدر، ونفس الرفض بأن يصير حقلٌ «منطوق» حقيقةً قبل أن يؤكّده إنسان.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { VoiceCapture } from "../../voice";
import { bagOf, useDemo } from "../useDemo";

/** المشهد. */
export function VoiceScene(): ReactNode {
  const state = useDemo();
  const transcript = bagOf<string>(state, "transcript") ?? "";
  const dictionary = bagOf<readonly { spoken: string; value: string }[]>(state, "dictionary") ?? [];
  const refusal = bagOf<string>(state, "refusal");

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel demo-panel--flush">
        <div className="demo-embed" style={{ overflow: "auto" }}>
          <VoiceCapture today="2026-08-25" simulatedTranscript={transcript} />
        </div>
      </section>

      <section className="demo-panel">
        <h3 className="demo-panel__head">الأرقام العربية — قاموس مغلق لا نموذج</h3>
        <div className="demo-panel__body">
          <table className="demo-table">
            <thead>
              <tr>
                <th>المنطوق</th>
                <th>القيمة</th>
              </tr>
            </thead>
            <tbody>
              {dictionary.map((row) => (
                <tr key={row.spoken}>
                  <td style={{ fontSize: 21 }}>{row.spoken}</td>
                  <td className="demo-code" style={{ fontSize: 21, color: "var(--stage-good)" }}>
                    {row.value}
                  </td>
                </tr>
              ))}
              {refusal ? (
                <tr className="demo-row-bad">
                  <td style={{ fontSize: 21 }}>{refusal}</td>
                  <td className="demo-code" style={{ fontSize: 19 }}>
                    مرفوض بالاسم — لا تخمين
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>

          <p className="demo-note" style={{ fontSize: 20 }}>
            أربعة أنظمة أرقام تُوحَّد (عربية‑هندية، وفارسية، ولاتينية، ومنطوقة)، والعامّية
            المكتوبة خطأً <strong>تُرفَض بالاسم بدل أن تُخمَّن</strong>. ونظامٌ يمتنع عن اختراع
            رقم هو ما يريده المحاسب، لا نظامٌ يجرؤ.
          </p>
          <p className="demo-note" style={{ fontSize: 20 }}>
            ولا شيء ممّا امتلأ هنا صار قيداً: المكوّن يملأ <strong>مسوّدة</strong> يؤكّدها إنسان
            حقلاً حقلاً — وحقلٌ مصدره «منطوق» لا يُرقّى إلى مستند قبل ذلك.
          </p>
        </div>
      </section>
    </div>
  );
}
