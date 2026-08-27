/* المشهد الختامي: جدول الصدق. ما كان حقيقياً وما كان محاكاة، بلا تلطيف. */
import type { ReactNode } from "react";

const LEDGER: readonly { scene: string; truth: "real" | "sim"; note: string }[] = [
  { scene: "كشف العبث", truth: "real", note: "أوامر نُفِّذت على PostgreSQL، وأحكام عادت من الخادم" },
  { scene: "رحلة عبر الزمن", truth: "real", note: "تراكم على ١١٤ قيداً مُرحَّلاً فعلاً" },
  { scene: "فسِّر هذا الرقم", truth: "real", note: "خمس خطوات من الميزان إلى بصمة القيد" },
  { scene: "قلب اللغة", truth: "real", note: "شاشة المنتج نفسها، وأربع لغات مُختبَرة" },
  { scene: "رمز الفاتورة", truth: "real", note: "متّجه ذهبي مُودَع، والقارئ المشحون هو من قرأه" },
  { scene: "الإدخال المنطوق", truth: "sim", note: "التفريغ محقون لا مسموع — وما تحته حقيقي" },
  { scene: "الرأي الثاني", truth: "sim", note: "الاقتراح مكتوب في شيفرة العرض — الشكل لا المنتج" },
];

/** المشهد. */
export function ClosingScene(): ReactNode {
  return (
    <div className="demo-title demo-fade" style={{ justifyContent: "center", gap: 30 }}>
      <h1 className="demo-title__h" style={{ fontSize: 62 }}>
        ما كان حقيقياً، وما كان محاكاة
      </h1>
      <table className="demo-table" style={{ maxWidth: 1180, fontSize: 21 }}>
        <tbody>
          {LEDGER.map((row) => (
            <tr key={row.scene}>
              <td style={{ width: "26%", fontWeight: 600 }}>{row.scene}</td>
              <td style={{ width: "16%" }}>
                <span className="demo-truth" data-truth={row.truth} style={{ fontSize: 17, padding: "6px 14px" }}>
                  <span className="demo-truth__dot" />
                  {row.truth === "real" ? "حقيقي" : "محاكاة"}
                </span>
              </td>
              <td style={{ color: "var(--stage-text-2)" }}>{row.note}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="demo-title__p" style={{ fontSize: 24, maxWidth: 1080 }}>
        الشركة التجريبية بُنيت بالكامل عبر محرّك الترحيل — بلا إدراج خام واحد. ولذلك أمكن أن
        يكون هذا العرض صادقاً.
      </p>
    </div>
  );
}
