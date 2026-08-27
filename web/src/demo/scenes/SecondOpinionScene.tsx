/* ═══════════════════════════════════════════════════════════════════════════
   المشهد السابع — الرأي الثاني. **مُحاكاة كاملة، وموسومة**.
   الاقتراح أدناه مكتوب في هذا الملفّ ولم ينتجه نموذج. وما يُعرَض هو **الشكل**
   الذي سيأخذه: طابور مراجعة لا يحجب الترحيل، واقتراحٌ يُقبل أو يُرفض بيد إنسان،
   ولا يمسّ الدفتر بحال.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Amount, Num } from "../../i18n/react";
import { snapshot, wire } from "../data";
import { bagOf, useDemo } from "../useDemo";

/** المشهد. */
export function SecondOpinionScene(): ReactNode {
  const state = useDemo();
  const shown = bagOf<number>(state, "suggestions") ?? 0;
  const decided = bagOf<string>(state, "decision");
  const bill = snapshot.supplierBills.find((b) => b.expenseCategory === "maintenance") ?? snapshot.supplierBills[0]!;

  return (
    <div className="demo-grid demo-grid--wide">
      <section className="demo-panel">
        <h3 className="demo-panel__head">
          طابور المراجعة — <strong>لا يحجب الترحيل</strong>
        </h3>
        <div className="demo-panel__body">
          <div className="demo-queue">
            {shown >= 1 ? (
              <div className="demo-sugg demo-fade">
                <div className="demo-sugg__head">
                  <span>◆ اقتراح مراجعة</span>
                  <span className="demo-code" style={{ color: "var(--stage-text-3)" }}>
                    قيدٌ مُرحَّل بالفعل · {bill.number}
                  </span>
                </div>
                <div className="demo-sugg__body">
                  هذا يبدو <strong>مصروفاً رأسمالياً</strong> لا مصروف صيانة: المبلغ{" "}
                  <Amount value={wire(bill.grossTotal)} /> ريالاً على مورّد{" "}
                  {bill.partyNameAr}، والوصف يشير إلى استبدال أصل لا إصلاحه.
                  <br />
                  <span style={{ color: "var(--stage-text-2)", fontSize: 18 }}>
                    الأثر إن صحّ: مصروف السنة أقلّ، وأصلٌ يُستهلك على عمره.
                  </span>
                </div>
                <div className="demo-sugg__acts">
                  <button type="button" className="demo-btn" data-kind="primary">
                    افتح للمراجعة
                  </button>
                  <button type="button" className="demo-btn">
                    اصرف النظر
                  </button>
                </div>
              </div>
            ) : null}

            {shown >= 2 ? (
              <div className="demo-sugg demo-fade">
                <div className="demo-sugg__head">
                  <span>◆ اقتراح مراجعة</span>
                  <span className="demo-code" style={{ color: "var(--stage-text-3)" }}>
                    نمطٌ متكرّر · 3 قيود
                  </span>
                </div>
                <div className="demo-sugg__body">
                  ثلاث فواتير من المورّد نفسه في الشهر نفسه بنفس المبلغ — يستحقّ النظر قبل
                  السداد.
                </div>
                <div className="demo-sugg__acts">
                  <button type="button" className="demo-btn" data-kind="primary">
                    افتح للمراجعة
                  </button>
                  <button type="button" className="demo-btn">
                    اصرف النظر
                  </button>
                </div>
              </div>
            ) : null}
          </div>

          {decided ? (
            <div className="demo-verdict" data-tone="warn">
              <div className="demo-verdict__code">{decided}</div>
              <div className="demo-verdict__why">
                القرار بيد المحاسب. والاقتراح لا يعدّل قيداً ولا يعكسه ولا يؤخّره — يُفتح
                <strong> قيدُ تصحيحٍ جديد</strong> إن قُبل، فيبقى الأصل والتصحيح كلاهما في السجلّ.
              </div>
            </div>
          ) : null}
        </div>
      </section>

      <section className="demo-panel">
        <h3 className="demo-panel__head">الحدّ المعلَن</h3>
        <div className="demo-panel__body">
          <ul className="demo-list">
            <li>
              <strong>لا يحجب</strong>: القيد رُحِّل، والاقتراح جاء بعده.
            </li>
            <li>
              <strong>لا يعدّل</strong>: أقصى ما يفعله فتحُ قيد تصحيح جديد بيد إنسان.
            </li>
            <li>
              <strong>لا يُخفى</strong>: كل اقتراح يحمل مصدره، ورفضُه فعلٌ مُسجَّل كقبوله.
            </li>
            <li>
              <strong>ولا يدخل الدفتر أبداً</strong>: طبقة سلامة الدفتر ليست مكاناً لرأي.
            </li>
          </ul>

          <div className="demo-stats" style={{ marginTop: 22 }}>
            <div className="demo-stat">
              <div className="demo-stat__k">قيود مُرحَّلة اليوم</div>
              <div className="demo-stat__v">
                <Num value={snapshot.totals.entryCount} />
              </div>
            </div>
            <div className="demo-stat">
              <div className="demo-stat__k">قيود حجبها اقتراح</div>
              <div className="demo-stat__v" style={{ color: "var(--stage-good)" }}>
                0
              </div>
            </div>
          </div>

          <p className="demo-note" style={{ fontSize: 20, color: "var(--stage-sim)" }}>
            ⬤ هذا المشهد <strong>محاكاة</strong>: نصّ الاقتراح مكتوب في شيفرة العرض ولم ينتجه
            نموذج، ولا يوجد اليوم في المنتج مُقترِح يعمل. المعروض هو <strong>الشكل والحدّ</strong>،
            وأرقام المستند تحته حقيقية من القاعدة المبذورة.
          </p>
        </div>
      </section>
    </div>
  );
}
