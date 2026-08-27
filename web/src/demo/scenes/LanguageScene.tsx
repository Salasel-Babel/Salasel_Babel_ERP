/* ═══════════════════════════════════════════════════════════════════════════
   المشهد الرابع — قلب اللغة في منتصف العمل.
   الشاشة المعروضة هنا هي **شاشة المنتج نفسها** (TrialBalanceScreen) لا نسخة
   منها: تُركَّب داخل المِنصّة وتقرأ من الخادم الحقيقي. وتبديل اللغة يستدعي
   نفس `setLocale` الذي يستدعيه المستخدم من شريط الأدوات.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { useLocale } from "../../i18n/react";
import { TrialBalanceScreen } from "../../screens/trial-balance/TrialBalanceScreen";

/** المشهد. */
export function LanguageScene(): ReactNode {
  const { locale, meta } = useLocale();

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel demo-panel--flush">
        <div className="demo-embed">
          <TrialBalanceScreen />
        </div>
      </section>

      <section className="demo-panel">
        <h3 className="demo-panel__head">
          ما الذي انقلب فعلاً — <strong>{meta.native}</strong>
        </h3>
        <div className="demo-panel__body">
          <table className="demo-table">
            <tbody>
              <tr>
                <th>رمز اللغة</th>
                <td className="demo-code">{locale}</td>
              </tr>
              <tr>
                <th>الاتجاه</th>
                <td className="demo-code" style={{ color: "var(--stage-brand)", fontWeight: 700 }}>
                  {meta.dir}
                </td>
              </tr>
              <tr>
                <th>الخطّ</th>
                <td className="demo-code" style={{ fontSize: 14 }}>
                  {meta.font?.ui ?? "—"}
                </td>
              </tr>
              <tr>
                <th>فاصل الآلاف / العشري</th>
                <td className="demo-code">
                  {meta.numbers.group} / {meta.numbers.decimal}
                </td>
              </tr>
              <tr>
                <th>العملة</th>
                <td className="demo-code">{meta.numbers.currency}</td>
              </tr>
              <tr>
                <th>درجات الجمع</th>
                <td className="demo-code">{meta.pluralLocale}</td>
              </tr>
            </tbody>
          </table>

          <p className="demo-note" style={{ fontSize: 20 }}>
            لا يوجد في هذا النظام شرطٌ واحد يسأل «هل اللغة عربية؟». الاتجاه والخطّ وفواصل
            الأرقام وأسماء الشهور ودرجات الجمع كلّها <strong>خانات في ملفّ اللغة</strong>. ولذلك
            لغةٌ خامسة صفوفُ إدخال لا مشروع هندسي.
          </p>
          <p className="demo-note" style={{ fontSize: 20 }}>
            وهذا المسار مُختبَر: <strong>٣٢ تركيبة</strong> (لغة × مظهر × شاشة) تعمل في مصفوفة
            الاختبار قبل كل دمج — لا في هذا العرض وحده.
          </p>
        </div>
      </section>
    </div>
  );
}
